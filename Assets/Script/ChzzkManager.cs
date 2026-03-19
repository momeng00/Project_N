using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 치지직 공식 API 클라이언트와 참여자 매니저를 연결하는 통합 매니저
/// </summary>
public class ChzzkManager : MonoBehaviour
{
    [Header("컴포넌트")]
    [Tooltip("치지직 공식 API 클라이언트")]
    public ChzzkOfficialClient officialClient;

    [Tooltip("참여자 관리 매니저")]
    public ChzzkParticipantManager participantManager;

    [Header("자동 시작 설정")]
    [Tooltip("Play 시작 시 자동으로 로그인 시작")]
    public bool autoStartLogin = true;

    [Header("UI (선택사항)")]
    [Tooltip("로그인 버튼 (수동 로그인용)")]
    public Button loginButton;

    [Tooltip("연결 상태 텍스트")]
    public TMPro.TextMeshProUGUI statusText;

    void Start()
    {
        // 컴포넌트 자동 탐색
        if (officialClient == null)
        {
            officialClient = GetComponent<ChzzkOfficialClient>();
        }

        if (participantManager == null)
        {
            participantManager = GetComponent<ChzzkParticipantManager>();
        }

        // 검증
        if (officialClient == null)
        {
            Debug.LogError("ChzzkOfficialClient를 찾을 수 없습니다!");
            return;
        }

        if (participantManager == null)
        {
            Debug.LogError("ChzzkParticipantManager를 찾을 수 없습니다!");
            return;
        }

        // ✅ 이벤트 연결!
        ConnectEvents();

        // UI 설정
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
        }

        // 자동 로그인
        if (autoStartLogin)
        {
            Debug.Log("자동 로그인 시작...");
            officialClient.StartOAuthLogin();
        }

        Debug.Log("✅ ChzzkManager 초기화 완료!");
    }

    /// <summary>
    /// 이벤트 연결
    /// </summary>
    void ConnectEvents()
    {
        // ✅ 공식 API 클라이언트의 이벤트를 참여자 매니저로 전달!
        officialClient.onMessage.AddListener(participantManager.OnChatMessageReceived);
        officialClient.onSubscription.AddListener(participantManager.OnSubscriptionReceived);

        Debug.Log("✅ 이벤트 연결 완료:");
        Debug.Log("  - officialClient.onMessage → participantManager.OnChatMessageReceived");
        Debug.Log("  - officialClient.onSubscription → participantManager.OnSubscriptionReceived");
    }

    void Update()
    {
        // 연결 상태 UI 업데이트
        UpdateStatusUI();
    }

    /// <summary>
    /// 로그인 버튼 클릭 핸들러
    /// </summary>
    void OnLoginButtonClicked()
    {
        Debug.Log("로그인 버튼 클릭!");
        officialClient.StartOAuthLogin();
    }

    /// <summary>
    /// 상태 UI 업데이트
    /// </summary>
    void UpdateStatusUI()
    {
        if (statusText == null) return;

        if (officialClient.isConnected)
        {
            statusText.text = "✅ 연결됨";
            statusText.color = Color.green;
        }
        else if (officialClient.isAuthenticated)
        {
            statusText.text = "🔄 연결 중...";
            statusText.color = Color.yellow;
        }
        else
        {
            statusText.text = "❌ 로그인 필요";
            statusText.color = Color.red;
        }
    }

    void OnDestroy()
    {
        // 이벤트 해제
        if (officialClient != null)
        {
            officialClient.onMessage.RemoveListener(participantManager.OnChatMessageReceived);
            officialClient.onSubscription.RemoveListener(participantManager.OnSubscriptionReceived);

            Debug.Log("이벤트 연결 해제 완료");
        }
    }

    /// <summary>
    /// 수동으로 연결 시작 (public 메서드)
    /// </summary>
    public void StartConnection()
    {
        if (officialClient != null)
        {
            officialClient.StartOAuthLogin();
        }
    }

    /// <summary>
    /// 현재 연결 상태 확인 (public 메서드)
    /// </summary>
    public bool IsConnected()
    {
        return officialClient != null && officialClient.isConnected;
    }
}