using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
    public string redirectUri = "http://localhost:8080/callback";

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
    void Update()
    {
        // SocketIOUnity는 자동으로 처리하므로 비워둠
    }
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
    private string currentState;
    async Task ListenForCallback()
    {
        Debug.Log("[CALLBACK] 콜백 대기 시작...");

        try
        {
            var context = await httpListener.GetContextAsync();
            Debug.Log($"FULL URL: {context.Request.Url}");
            string path = context.Request.Url.AbsolutePath;

            // ✅ favicon 무시
            if (path == "/favicon.ico")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            var query = context.Request.QueryString;

            string code = query["code"];
            string state = query["state"];
            string error = query["error"];

            Debug.Log($"code: {code}");
            Debug.Log($"state: {state}");
            Debug.Log($"error: {error}");

            // ✅ 에러 처리
            if (!string.IsNullOrEmpty(error))
            {
                SendBrowserResponse(context, false);
                httpListener.Stop();
                return;
            }

            // ✅ 성공 처리
            if (!string.IsNullOrEmpty(code))
            {
                SendBrowserResponse(context, true);

                Debug.Log($"✅ 인증 코드 수신: {code}");

                await GetAccessToken(code, query["state"]);
            }
            else
            {
                SendBrowserResponse(context, false);
                Debug.LogError("❌ code 없음");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"콜백 에러: {e}");
            httpListener.Stop();
        }
        finally
        {
            //httpListener.Stop();

        }
    }

    void SendBrowserResponse(HttpListenerContext context, bool success)
    {
        string responseString = success
            ? "<html><body><h2>로그인 성공! 창을 닫아주세요.</h2></body></html>"
            : "<html><body><h2>로그인 실패</h2></body></html>";

        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

        context.Response.ContentLength64 = buffer.Length;
        context.Response.ContentType = "text/html";
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close(); // ⭐ 중요
    }

    /// <summary>
    /// 2단계: Access Token 발급
    /// </summary>
    async Task GetAccessToken(string code, string state)
    {
        Debug.Log("=== 2단계: Access Token 발급 ===");

        string url = "https://openapi.chzzk.naver.com/auth/v1/token";

        string json = $@"{{
    ""grantType"": ""authorization_code"",
    ""clientId"": ""{clientId}"",
    ""clientSecret"": ""{clientSecret}"",
    ""code"": ""{code}"",
    ""state"":""{state}"",
    ""redirectUri"": ""http://localhost:8080/callback""
        }}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");

        var operation = request.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        Debug.Log($"Response Code: {request.responseCode}");
        Debug.Log($"Response: {request.downloadHandler.text}");
        Debug.Log($"? {request.result} ");

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("응답 성공! 파싱 시도...");

            try
            {
                Debug.Log($"json: {json}");
                Debug.Log($"real: {request.downloadHandler.text}");
                var response = JsonConvert.DeserializeObject<TokenResponse>(request.downloadHandler.text);
                Debug.Log($"response null? {response == null}");
                Debug.Log($"content null? {response?.content == null}");
                Debug.Log($"?? {response.content.accessToken}");
                // ✅ content 안에 토큰이 있음!
                if (response.content != null)
                {
                    accessToken = response.content.accessToken;
                    refreshToken = response.content.refreshToken;
                    isAuthenticated = true;

                    Debug.Log($"✅ Access Token: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}...");
                    Debug.Log($"✅ Token Type: {response.content.tokenType}");
                    Debug.Log($"✅ Expires In: {response.content.expiresIn}초");

                    // 3단계: 세션 생성
                    await CreateSession();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 파싱 에러: {e.Message}");
                Debug.LogError($"응답 내용: {request.downloadHandler.text}");
            }
        }
        else
        {
            Debug.LogError($"2단계 Token 발급 실패!");
            Debug.LogError($"Error: {request.error}");
            Debug.LogError($"Response Code: {request.responseCode}");
            Debug.LogError($"Response: {request.downloadHandler.text}");

        }
    }

    /// <summary>
    /// 3단계: 세션 생성
    /// </summary>
    async Task CreateSession()
    {
        Debug.Log("=== 3단계: 세션 생성 ===");

        string url = "https://openapi.chzzk.naver.com/open/v1/sessions/auth/client";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            //  필수 헤더
            //request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Client-Id", clientId);
            request.SetRequestHeader("Client-Secret", clientSecret);
            request.SetRequestHeader("Content-Type", "application/json");
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            Debug.Log($"[SESSION] Response Code: {request.responseCode}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ 세션 생성 실패: {request.error}");
                Debug.LogError($"Response: {request.downloadHandler.text}");
                return;
            }

            string json = request.downloadHandler.text;
            Debug.Log($"[SESSION] Raw Response: {json}");

            string extractedUrl = ParseSessionUrl(json);

            if (string.IsNullOrEmpty(extractedUrl))
            {
                Debug.LogError("❌ Session URL 파싱 실패");
                return;
            }

            sessionUrl = NormalizeSessionUrl(extractedUrl);

            Debug.Log($"✅ Session URL: {sessionUrl}");

            await ConnectSocket();
        }
    }
    #region 
    string ParseSessionUrl(string json)
    {
        try
        {
            var response = JsonUtility.FromJson<SessionResponse>(json);

            if (response?.content?.url != null)
                return response.content.url;
        }
        catch
        {
            // JsonUtility 실패 시 fallback
        }

        // 🔥 fallback: 문자열 파싱
        const string key = "\"url\":\"";
        if (json.Contains(key))
        {
            int start = json.IndexOf(key) + key.Length;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }

        return null;
    }

    string NormalizeSessionUrl(string url)
    {
        // :443 제거하지 마세요!
        return url;  // 그대로 반환
    }
    #endregion
    /// <summary>
    /// 4단계: Socket.IO 연결
    /// </summary>
    async Task ConnectSocket()
    {
        Debug.Log($"[SocketIO] 연결 시도: {sessionUrl}");
        sessionUrl = sessionUrl.Replace("https:", "wss:");
        var uri = new Uri(sessionUrl);
        string serverUrl = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        string authToken = queryParams["auth"];
        if (string.IsNullOrEmpty(authToken))
        {
            Debug.LogError("authToken비어있음");
        }
        if (socket != null) { }
        socket = new SocketIOUnity(new Uri(serverUrl), new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "auth", authToken }, },
            EIO = SocketIOClient.EngineIO.V3,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Reconnection = false,
            ConnectionTimeout = TimeSpan.FromMilliseconds(3000)
        });
        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        socket.On("SYSTEM", (ev) =>
        {
            try
            {
                string evString = ev.GetValue<string>();

                JObject json = JObject.Parse(evString);

                string type = json["type"]?.ToString() ?? "";

                switch (type)
                {
                    case "connected":
                        {
                            string parsedSessionkey = json["data"]?["sessionKey"]?.ToString();
                            if (string.IsNullOrEmpty(parsedSessionkey) == false)
                            {
                                sessionKey = parsedSessionkey;
                            }
                            else
                            {
                                Debug.Log("연결은 되었지만 key가 안나옴");
                            }
                            break;
                        }
                    case "subscibed": break;
                    case "unsubscibed": break;
                    case "revoked": break;
                }
            }
            catch (Exception ex)
            {
            }
        });
        socket.On("CHAT", (ev) =>
        {
            try
            {
                Debug.Log($"[RAW CHAT] {ev}");

                // JSON 문자열로 변환
                string jsonString = ev.GetValue<string>();
                Debug.Log($"[JSON STRING] {jsonString}");

                // JSON 파싱
                ChatMessageData chatData = JsonUtility.FromJson<ChatMessageData>(jsonString);

                // ✅ 상세 정보 출력
                Debug.Log("========== 채팅 메시지 수신 ==========");
                Debug.Log($"📌 채널 ID: {chatData.channelId}");
                Debug.Log($"👤 작성자 채널 ID: {chatData.senderChannelId}");
                Debug.Log($"👤 닉네임: {chatData.profile.nickname}");
                Debug.Log($"✅ 인증 마크: {chatData.profile.verifiedMark}");
                Debug.Log($"🎖️ 역할: {chatData.userRoleCode}");
                Debug.Log($"💬 메시지: {chatData.content}");
                Debug.Log($"🕐 시간: {chatData.messageTime}");

                // 배지 출력
                if (chatData.profile.badges != null && chatData.profile.badges.Length > 0)
                {
                    Debug.Log($"🏅 배지 개수: {chatData.profile.badges.Length}");
                    foreach (var badge in chatData.profile.badges)
                    {
                        Debug.Log($"  - {badge.badgeId}: {badge.imageUrl}");
                    }
                }

                // 이모티콘 출력
                if (chatData.emojis != null && chatData.emojis.Count > 0)
                {
                    Debug.Log($"😀 이모티콘 개수: {chatData.emojis.Count}");
                    foreach (var emoji in chatData.emojis)
                    {
                        Debug.Log($"  - {emoji.Key}: {emoji.Value}");
                    }
                }

                Debug.Log("====================================");

                // 기존 ProcessMessage 호출
                ProcessMessage(jsonString);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ CHAT 파싱 에러: {ex.Message}");
                Debug.LogError($"스택: {ex.StackTrace}");
            }
        });
        socket.OnConnected += async (sender, e) =>
        {
            Debug.Log("✅ Socket.IO 연결 성공!");
            Debug.Log("[SocketIO] ConnectAsync 시작");


            isConnected = true;
        };
        socket.OnReconnectAttempt += (sender, e) =>
        {
            StartCoroutine(CSubscribeChatEvents());
        };
        socket.OnError += (sender, e) =>
        {
            Debug.LogError($"❌ Socket.IO 에러: {e}");
        };

        socket.OnDisconnected += (sender, e) =>
        {
            Debug.Log("🔌 Socket.IO 종료");
            isConnected = false;
        };

        try
        {
            await socket.ConnectAsync();
            Debug.Log("✅ ConnectAsync 성공");

            await SubscribeChatEvents();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 연결 실패: {ex.Message}");
            Debug.LogError($"스택: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 5단계: 채팅 이벤트 구독
    /// </summary>
    async Task SubscribeChatEvents()
    {
        Debug.Log("=== 5단계: 채팅 이벤트 구독 ===");

        string url = $"https://openapi.chzzk.naver.com/open/v1/sessions/events/subscribe/chat?sessionKey={sessionKey}";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Client-Id", clientId);
            request.SetRequestHeader("Client-Secret", clientSecret);
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 채팅 이벤트 구독 완료!");
            }
            else
            {
                Debug.LogError($"구독 실패: {request.error}");
            }
        }
    }
    IEnumerator CSubscribeChatEvents()
    {
        Debug.Log("=== 5단계: 채팅 이벤트 구독 ===");

        string url = $"https://openapi.chzzk.naver.com/open/v1/sessions/events/subscribe/chat?sessionKey={sessionKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Client-Id", clientId);
            request.SetRequestHeader("Client-Secret", clientSecret);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 채팅 이벤트 구독 완료!");
                Debug.Log($"응답: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"❌ 구독 실패!");
                Debug.LogError($"Error: {request.error}");
                Debug.LogError($"Response Code: {request.responseCode}");
                Debug.LogError($"Response: {request.downloadHandler.text}");
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
    [Serializable]
    class TokenContent
    {
        public string accessToken;
        public string refreshToken;
        public string tokenType;
        public string expiresIn;
    }
    // JSON 구조체
    [Serializable]
    class TokenResponse
    {
        public string code;
        public string message;
        public TokenContent content; // message 아예 제거
    }
    [Serializable]
    class SessionResponse
    {
        public string code;
        public string message;
        public SessionContent content;
    }

    class SessionContent
    {
        public string url;  // ← url!
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

    [Serializable]
    public class ChatMessageData
    {
        public string channelId;
        public string senderChannelId;
        public ProfileDataa profile;
        public string userRoleCode;
        public string content;
        public Dictionary<string, string> emojis;
        public long messageTime;
    }

    [Serializable]
    public class ProfileDataa
    {
        public string nickname;
        public BadgeData[] badges;
        public bool verifiedMark;
    }

    [Serializable]
    public class BadgeData
    {
        public string badgeId;
        public string imageUrl;
    }
}