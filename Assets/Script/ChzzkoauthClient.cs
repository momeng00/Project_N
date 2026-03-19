using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;

/// <summary>
/// OAuth 인증을 사용하는 치지직 연결 매니저
/// 네이버 로그인 → Access Token → 치지직 API 호출
/// </summary>
public class ChzzkoauthClient : MonoBehaviour
{
    [Header("네이버 애플리케이션 설정")]
    public string clientId = "YOUR_CLIENT_ID";
    public string clientSecret = "YOUR_CLIENT_SECRET";
    public string redirectUri = "http://localhost:8080";
    public int localServerPort = 8080;

    [Header("치지직 채널 설정")]
    public string channelId = "YOUR_CHANNEL_ID";

    [Header("상태")]
    public bool isAuthenticated = false;
    public bool isConnected = false;

    private string naverAccessToken;  // 네이버 로그인 Token
    private string chatAccessToken;   // 치지직 채팅 Token
    private WebSocket websocket;
    private HttpListener httpListener;

    [Header("이벤트")]
    // ChzzkUnity와 호환되도록 UnityEvent 정의
    public ProfileMessageEvent onMessage = new ProfileMessageEvent();
    public ProfileSubscriptionEvent onSubscription = new ProfileSubscriptionEvent();

    // 이벤트 타입 정의 (ChzzkUnity와 동일한 형태)
    [Serializable]
    public class ProfileMessageEvent : UnityEngine.Events.UnityEvent<ChzzkUnity.Profile, string> { }

    [Serializable]
    public class ProfileSubscriptionEvent : UnityEngine.Events.UnityEvent<ChzzkUnity.Profile, ChzzkUnity.SubscriptionExtras> { }

    /// <summary>
    /// 1단계: 네이버 OAuth 로그인 시작
    /// </summary>
    public void StartOAuthLogin()
    {
        Debug.Log("=== 1단계: 네이버 로그인 시작 ===");

        StartLocalServer();

        string authUrl = BuildAuthUrl();
        Application.OpenURL(authUrl);

        Debug.Log($"브라우저에서 로그인하세요: {authUrl}");
    }

    string BuildAuthUrl()
    {
        string baseUrl = "GET https://chzzk.naver.com/account-interlock";
        string state = Guid.NewGuid().ToString("N");

        return $"{baseUrl}" +
               $"&client_id={clientId}" +
               $"&redirect_uri={UnityWebRequest.EscapeURL(redirectUri)}" +
               $"&state={state}";
    }

    void StartLocalServer()
    {
        try
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://localhost:{localServerPort}/");
            httpListener.Start();

            Debug.Log($"로컬 서버 시작: http://localhost:{localServerPort}/");
            _ = ListenForCallback();
        }
        catch (Exception e)
        {
            Debug.LogError($"서버 시작 실패: {e.Message}");
        }
    }

    async Task ListenForCallback()
    {
        while (httpListener != null && httpListener.IsListening)
        {
            try
            {
                var context = await httpListener.GetContextAsync();

                var query = context.Request.QueryString;
                string code = query["code"];

                // 브라우저 응답
                SendBrowserResponse(context, code != null);

                if (!string.IsNullOrEmpty(code))
                {
                    Debug.Log($"인증 코드 수신: {code}");

                    // 2단계: Access Token 받기
                    await GetNaverAccessToken(code);

                    httpListener.Stop();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"콜백 에러: {e.Message}");
            }
        }
    }

    void SendBrowserResponse(HttpListenerContext context, bool success)
    {
        string html = success
            ? "<html><body><h1>✅ 로그인 성공!</h1><p>Unity로 돌아가세요.</p></body></html>"
            : "<html><body><h1>❌ 로그인 실패</h1></body></html>";

        byte[] buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    /// <summary>
    /// 2단계: 네이버 Access Token 발급
    /// </summary>
    async Task GetNaverAccessToken(string code)
    {
        Debug.Log("=== 2단계: 네이버 Access Token 발급 ===");

        string tokenUrl = "https://nid.naver.com/oauth2.0/token";

        WWWForm form = new WWWForm();
        form.AddField("grant_type", "authorization_code");
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("code", code);

        using (UnityWebRequest request = UnityWebRequest.Post(tokenUrl, form))
        {
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<NaverTokenResponse>(request.downloadHandler.text);
                naverAccessToken = response.access_token;
                isAuthenticated = true;

                Debug.Log("✅ 네이버 Access Token 발급 완료!");

                // 3단계: 치지직 채팅 Token 발급
                await GetChatAccessToken();
            }
            else
            {
                Debug.LogError($"Token 발급 실패: {request.error}");
            }
        }
    }

    /// <summary>
    /// 3단계: 치지직 채팅 Access Token 발급
    /// 네이버 Access Token을 사용해서 요청!
    /// </summary>
    async Task GetChatAccessToken()
    {
        Debug.Log("=== 3단계: 치지직 채팅 Token 발급 ===");

        string url = $"https://comm-api.game.naver.com/nng_main/v1/chats/access-token" +
                     $"?channelId={channelId}" +
                     $"&chatType=STREAMING";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // ✅ 네이버 Access Token 사용! (핵심!)
            request.SetRequestHeader("Authorization", $"Bearer {naverAccessToken}");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<ChatTokenResponse>(request.downloadHandler.text);
                chatAccessToken = response.content.accessToken;

                Debug.Log("✅ 치지직 채팅 Token 발급 완료!");

                // 4단계: WebSocket 연결
                ConnectWebSocket();
            }
            else
            {
                Debug.LogError($"채팅 Token 발급 실패: {request.error}");
                Debug.LogError($"응답: {request.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// 4단계: WebSocket 연결
    /// 발급받은 치지직 채팅 Token 사용
    /// </summary>
    void ConnectWebSocket()
    {
        Debug.Log("=== 4단계: WebSocket 연결 ===");

        string wsUrl = "wss://kr-ss1.chat.naver.com/chat";
        websocket = new WebSocket(wsUrl);

        websocket.OnOpen += (sender, e) =>
        {
            Debug.Log("WebSocket 연결 성공!");
            SendConnectMessage();
        };

        websocket.OnMessage += (sender, e) =>
        {
            ProcessMessage(e.Data);
        };

        websocket.OnError += (sender, e) =>
        {
            Debug.LogError($"WebSocket 에러: {e.Message}");
        };

        websocket.OnClose += (sender, e) =>
        {
            Debug.Log("WebSocket 연결 종료");
            isConnected = false;
        };

        websocket.Connect();
    }

    /// <summary>
    /// 5단계: 인증 메시지 전송
    /// </summary>
    void SendConnectMessage()
    {
        Debug.Log("=== 5단계: 인증 메시지 전송 ===");

        var connectData = new
        {
            ver = "2",
            cmd = 100,  // CONNECT
            svcid = "game",
            cid = channelId,
            bdy = new
            {
                accTkn = chatAccessToken,  // ✅ 발급받은 Token 사용!
                auth = "READ"
            },
            tid = 1
        };

        // ❌ JsonUtility는 대소문자 변환함!
        // string json = JsonUtility.ToJson(connectData);

        // ✅ 수동으로 JSON 생성 (대소문자 유지!)
        string json = $@"{{
            ""ver"":""2"",
            ""cmd"":100,
            ""svcid"":""game"",
            ""cid"":""{channelId}"",
            ""bdy"":{{
                ""accTkn"":""{chatAccessToken}"",
                ""auth"":""READ""
            }},
            ""tid"":1
        }}";

        Debug.Log($"[SEND] {json}");
        websocket.Send(json);

        Debug.Log("인증 메시지 전송 완료");
    }

    /// <summary>
    /// 메시지 처리
    /// </summary>
    void ProcessMessage(string data)
    {
        // ✅ 모든 메시지 출력
        Debug.Log($"[RECV] {data}");

        try
        {
            var message = JsonUtility.FromJson<ChzzkMessage>(data);

            Debug.Log($"[CMD] {message.cmd}");

            switch (message.cmd)
            {
                case 10000: // CONNECTED
                    Debug.Log("✅ 치지직 채팅 연결 완료!");
                    isConnected = true;
                    break;

                case 93101: // CHAT
                    HandleChatMessage(message);
                    break;

                case 93103: // SUBSCRIPTION
                    HandleSubscription(message);
                    break;

                default:
                    Debug.Log($"알 수 없는 cmd: {message.cmd}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"메시지 처리 에러: {e.Message}");
            Debug.LogError($"원본 데이터: {data}");
        }
    }

    void HandleChatMessage(ChzzkMessage message)
    {
        if (message.bdy == null || message.bdy.Count == 0) return;

        var chat = message.bdy[0];

        // ChzzkUnity.Profile 객체 생성 (기존 코드와 호환)
        ChzzkUnity.Profile profile = new ChzzkUnity.Profile
        {
            userIdHash = chat.uid ?? "unknown",
            nickname = chat.profile?.nickname ?? "Unknown",
            userRoleCode = chat.profile?.userRoleCode ?? "",
            badge = chat.profile?.badge ?? "",
            profileImageUrl = chat.profile?.profileImageUrl ?? ""
        };

        string msg = chat.msg ?? "";

        // 이벤트 발생 (ChzzkUnity와 동일한 형태)
        onMessage?.Invoke(profile, msg);

        Debug.Log($"[{profile.nickname}] {msg} | 역할: {profile.userRoleCode}");
    }

    void HandleSubscription(ChzzkMessage message)
    {
        // 구독 이벤트 처리
        Debug.Log("구독 이벤트 수신");
    }

    void OnDestroy()
    {
        if (websocket != null && websocket.IsAlive)
        {
            websocket.Close();
        }

        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Stop();
        }
    }

    // JSON 응답 구조체들
    [Serializable]
    class NaverTokenResponse
    {
        public string access_token;
        public string refresh_token;
    }

    [Serializable]
    class ChatTokenResponse
    {
        public Content content;

        [Serializable]
        public class Content
        {
            public string accessToken;
        }
    }

    [Serializable]
    class ChzzkMessage
    {
        public int cmd;
        public System.Collections.Generic.List<ChatBody> bdy;
    }

    [Serializable]
    class ChatBody
    {
        public string uid;
        public string msg;
        public ProfileData profile;
    }

    [Serializable]
    class ProfileData
    {
        public string nickname;
        public string userRoleCode;
        public string badge;
        public string profileImageUrl;
    }
}