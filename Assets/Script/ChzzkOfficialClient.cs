using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SocketIOClient;

/// <summary>
/// 치지직 공식 API를 사용하는 OAuth 클라이언트
/// Socket.IO로 채팅 수신
/// </summary>
public class ChzzkOfficialClient : MonoBehaviour
{
    [Header("치지직 애플리케이션 설정")]
    [Tooltip("치지직 Developers에서 발급받은 Client ID")]
    public string clientId = "YOUR_CLIENT_ID";

    [Tooltip("치지직 Developers에서 발급받은 Client Secret")]
    public string clientSecret = "YOUR_CLIENT_SECRET";

    [Tooltip("Redirect URI (localhost 사용)")]
    public string redirectUri = "https://localhost:8080/callback";

    [Tooltip("로컬 서버 포트")]
    public int localServerPort = 8080;

    [Header("채널 설정")]
    [Tooltip("채팅을 수신할 채널 ID")]
    public string channelId = "";

    [Header("상태")]
    public bool isAuthenticated = false;
    public bool isConnected = false;

    // OAuth 토큰
    private string accessToken;
    private string refreshToken;

    // Socket.IO 세션
    private string sessionUrl;
    private string sessionKey;
    private SocketIO socket;

    // HTTP 리스너
    private HttpListener httpListener;

    [Header("이벤트")]
    // ChzzkUnity와 호환되는 이벤트
    public ProfileMessageEvent onMessage = new ProfileMessageEvent();
    public ProfileSubscriptionEvent onSubscription = new ProfileSubscriptionEvent();

    [Serializable]
    public class ProfileMessageEvent : UnityEngine.Events.UnityEvent<ChzzkUnity.Profile, string> { }

    [Serializable]
    public class ProfileSubscriptionEvent : UnityEngine.Events.UnityEvent<ChzzkUnity.Profile, ChzzkUnity.SubscriptionExtras> { }

    /// <summary>
    /// 1단계: OAuth 인증 시작
    /// </summary>
    public void StartOAuthLogin()
    {
        Debug.Log("=== 1단계: OAuth 인증 시작 ===");

        // 로컬 서버 시작
        StartLocalServer();

        // 브라우저 열기
        string authUrl = BuildAuthUrl();

        Debug.Log("=== 생성된 Auth URL ===");
        Debug.Log(authUrl);
        Debug.Log("=====================");

        Application.OpenURL(authUrl);

        Debug.Log($"브라우저에서 로그인하세요.");
    }

    string BuildAuthUrl()
    {
        // ✅ 치지직 Account Interlock URL
        string baseUrl = "https://chzzk.naver.com/account-interlock";
        string state = Guid.NewGuid().ToString("N");

        // ✅ 치지직은 쿼리 파라미터를 카멜케이스로 사용!
        return $"{baseUrl}" +
               $"?response_type=code" +
               $"&clientId={clientId}" +
               $"&redirectUri={UnityWebRequest.EscapeURL(redirectUri)}" +
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
        Debug.Log("[CALLBACK] 콜백 대기 시작...");

        while (httpListener != null && httpListener.IsListening)
        {
            try
            {
                Debug.Log("[CALLBACK] 요청 대기 중...");
                var context = await httpListener.GetContextAsync();
                Debug.Log($"한번 보자 비어있음? {context}");
                Debug.Log($"[CALLBACK] 요청 수신!");
                Debug.Log($"[CALLBACK] URL: {context.Request.Url}");
                Debug.Log($"[CALLBACK] RawUrl: {context.Request.RawUrl}");

                var query = context.Request.QueryString;
                Debug.Log($"[CALLBACK] QueryString Count: {query.Count}");

                // 모든 쿼리 파라미터 출력
                foreach (string key in query.AllKeys)
                {
                    Debug.Log($"[CALLBACK] {key} = {query[key]}");
                }

                string code = query["code"];
                string state = query["state"];
                string error = query["error"];

                Debug.Log($"[CALLBACK] code: {code ?? "NULL"}");
                Debug.Log($"[CALLBACK] state: {state ?? "NULL"}");
                Debug.Log($"[CALLBACK] error: {error ?? "NULL"}");

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[CALLBACK] OAuth 에러 발생: {error}");
                    SendBrowserResponse(context, false);
                    httpListener.Stop();
                    return;
                }

                SendBrowserResponse(context, code != null);

                if (!string.IsNullOrEmpty(code))
                {
                    Debug.Log($"✅ 인증 코드 수신: {code}");

                    // 2단계: Access Token 발급
                    await GetAccessToken(code);

                    httpListener.Stop();
                }
                else
                {
                    Debug.LogError("[CALLBACK] code가 NULL입니다! 치지직에서 redirect를 제대로 안 한 것 같습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CALLBACK] 콜백 에러: {e.Message}");
                Debug.LogError($"[CALLBACK] Stack Trace: {e.StackTrace}");
            }
        }

        Debug.Log("[CALLBACK] 콜백 리스너 종료");
    }

    void SendBrowserResponse(HttpListenerContext context, bool success)
    {
        string html = success
            ? @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>로그인 성공</title>
    <style>
        body { 
            font-family: Arial, sans-serif; 
            display: flex; 
            justify-content: center; 
            align-items: center; 
            height: 100vh; 
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        .container { 
            text-align: center; 
            background: white; 
            padding: 40px; 
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        h1 { color: #333; margin-bottom: 20px; }
        p { color: #666; font-size: 18px; }
        .success { font-size: 60px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='success'>✅</div>
        <h1>로그인 성공!</h1>
        <p>Unity로 돌아가세요.</p>
        <p style='font-size:14px; color:#999; margin-top:20px;'>이 창을 닫아도 됩니다.</p>
    </div>
    <script>
        // 3초 후 자동으로 창 닫기 시도
        setTimeout(() => {
            window.close();
        }, 3000);
    </script>
</body>
</html>"
            : @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>로그인 실패</title>
    <style>
        body { 
            font-family: Arial, sans-serif; 
            display: flex; 
            justify-content: center; 
            align-items: center; 
            height: 100vh; 
            margin: 0;
            background: #f44336;
        }
        .container { 
            text-align: center; 
            background: white; 
            padding: 40px; 
            border-radius: 10px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>❌ 로그인 실패</h1>
        <p>다시 시도해주세요.</p>
    </div>
</body>
</html>";

        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            // ✅ ContentType 설정
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.StatusCode = 200;

            // ✅ 응답 전송
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.OutputStream.Close();

            Debug.Log($"브라우저 응답 전송 완료: {(success ? "성공" : "실패")}");
        }
        catch (Exception e)
        {
            Debug.LogError($"브라우저 응답 전송 에러: {e.Message}");
        }
    }

    /// <summary>
    /// 2단계: Access Token 발급
    /// </summary>
    async Task GetAccessToken(string code)
    {
        Debug.Log("=== 2단계: Access Token 발급 ===");
        Debug.Log($"[DEBUG] Code: {code}");
        Debug.Log($"[DEBUG] Client ID: {clientId}");
        Debug.Log($"[DEBUG] Client Secret: {clientSecret.Substring(0, Math.Min(5, clientSecret.Length))}...");
        Debug.Log($"[DEBUG] Redirect URI: {redirectUri}");

        string tokenUrl = "https://auth.chzzk.naver.com/oauth/v1.0/token";
        Debug.Log($"[DEBUG] Token URL: {tokenUrl}");

        WWWForm form = new WWWForm();
        form.AddField("grant_type", "authorization_code");
        form.AddField("client_id", clientId);
        form.AddField("client_secret", clientSecret);
        form.AddField("code", code);
        form.AddField("redirect_uri", redirectUri);

        using (UnityWebRequest request = UnityWebRequest.Post(tokenUrl, form))
        {
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            Debug.Log($"[DEBUG] Response Code: {request.responseCode}");
            Debug.Log($"[DEBUG] Response: {request.downloadHandler.text}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("응답 성공! 파싱 시도...");

                try
                {
                    var response = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                    accessToken = response.access_token;
                    refreshToken = response.refresh_token;
                    isAuthenticated = true;

                    Debug.Log($"✅ Access Token: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}...");

                    // 3단계: 세션 생성
                    await CreateSession();
                }
                catch (Exception e)
                {
                    Debug.LogError($"JSON 파싱 에러: {e.Message}");
                    Debug.LogError($"응답 내용: {request.downloadHandler.text}");
                }
            }
            else
            {
                Debug.LogError($"❌ Token 발급 실패!");
                Debug.LogError($"Error: {request.error}");
                Debug.LogError($"Response Code: {request.responseCode}");
                Debug.LogError($"Response: {request.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// 3단계: Socket.IO 세션 생성
    /// </summary>
    async Task CreateSession()
    {
        Debug.Log("=== 3단계: 세션 생성 ===");

        // ✅ 올바른 세션 생성 API
        string url = "https://api.chzzk.naver.com/open/v1/sessions";

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            // ✅ OAuth Access Token 사용!
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"응답: {request.downloadHandler.text}");

                var response = JsonUtility.FromJson<SessionResponse>(request.downloadHandler.text);
                sessionUrl = response.sessionUrl;
                sessionKey = response.sessionKey;

                Debug.Log($"✅ Session URL: {sessionUrl}");
                Debug.Log($"✅ Session Key: {sessionKey}");

                // 4단계: Socket.IO 연결
                await ConnectSocket();
            }
            else
            {
                Debug.LogError($"세션 생성 실패: {request.error}");
                Debug.LogError($"응답: {request.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// 4단계: Socket.IO 연결
    /// </summary>
    async Task ConnectSocket()
    {
        Debug.Log("=== 4단계: Socket.IO 연결 ===");

        // ✅ Socket.IO 사용!
        socket = new SocketIO(sessionUrl, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Reconnection = false
        });

        socket.OnConnected += async (sender, e) =>
        {
            Debug.Log("✅ Socket.IO 연결 완료!");
            isConnected = true;

            // 5단계: 채팅 이벤트 구독
            await SubscribeChatEvents();
        };

        socket.On("connect", (data) =>
        {
            Debug.Log($"[CONNECT] {data}");
        });

        socket.On("message", (data) =>
        {
            Debug.Log($"[MESSAGE] {data}");
            ProcessMessage(data.ToString());
        });

        socket.On("disconnect", (data) =>
        {
            Debug.Log($"[DISCONNECT] {data}");
            isConnected = false;
        });

        socket.OnError += (sender, e) =>
        {
            Debug.LogError($"❌ Socket.IO 에러: {e}");
        };

        await socket.ConnectAsync();
    }

    /// <summary>
    /// 5단계: 채팅 이벤트 구독
    /// </summary>
    async Task SubscribeChatEvents()
    {
        Debug.Log("=== 5단계: 채팅 이벤트 구독 ===");

        string url = $"https://api.chzzk.naver.com/open/v1/sessions/{sessionKey}/subscriptions/chat";

        // ✅ 채널 ID 포함
        string json = $"{{\"channelId\":\"{channelId}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 채팅 이벤트 구독 완료!");
                Debug.Log($"응답: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"구독 실패: {request.error}");
                Debug.LogError($"응답: {request.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// 메시지 처리
    /// </summary>
    void ProcessMessage(string data)
    {
        try
        {
            // Socket.IO 메시지를 파싱
            var message = JsonUtility.FromJson<ChatMessage>(data);

            if (message == null || message.profile == null)
            {
                Debug.LogWarning("메시지 파싱 실패");
                return;
            }

            // ChzzkUnity.Profile 생성
            ChzzkUnity.Profile profile = new ChzzkUnity.Profile
            {
                userIdHash = message.profile.userIdHash ?? "unknown",
                nickname = message.profile.nickname ?? "Unknown",
                userRoleCode = message.profile.userRoleCode ?? "",
                badge = message.profile.badge ?? ""
            };

            string msg = message.message ?? "";

            // ✅ 이벤트 발생 (ChzzkParticipantManager가 수신!)
            onMessage?.Invoke(profile, msg);

            Debug.Log($"💬 [{profile.nickname}] {msg}");
        }
        catch (Exception e)
        {
            Debug.LogError($"메시지 처리 에러: {e.Message}");
            Debug.LogError($"원본 데이터: {data}");
        }
    }

    void OnDestroy()
    {
        if (socket != null)
        {
            _ = socket.DisconnectAsync();
        }

        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Stop();
        }
    }

    // JSON 구조체
    [Serializable]
    class TokenResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
    }

    [Serializable]
    class SessionResponse
    {
        public string sessionUrl;
        public string sessionKey;
    }

    [Serializable]
    class ChatMessage
    {
        public string message;
        public ProfileData profile;
    }

    [Serializable]
    class ProfileData
    {
        public string userIdHash;
        public string nickname;
        public string userRoleCode;
        public string badge;
    }
}