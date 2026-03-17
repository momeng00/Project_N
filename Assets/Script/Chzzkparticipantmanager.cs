using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 치지직 채팅에서 "!줄" 명령어로 참여자를 수집하고 랜덤 추첨하는 매니저
/// 
/// 주의: 이 스크립트는 ChzzkUnity 라이브러리가 필요합니다.
/// GitHub: https://github.com/JoKangHyeon/ChzzkUnity
/// ChzzkUnity.cs 파일을 프로젝트에 포함시켜야 합니다.
/// </summary>
public class ChzzkParticipantManager : MonoBehaviour
{
    [Header("연결 방식")]
    [Tooltip("OAuth 인증 사용 (권장)")]
    public bool useOAuth = false;

    [Header("치지직 연결 설정 (기존)")]
    public ChzzkUnity chzzkClient;

    [Header("OAuth 연결 설정 (새로운)")]
    public ChzzkOAuthClient oauthClient;

    [Header("치지직 연결 설정")]
    [Tooltip("치지직 채널 ID")]
    public string channelId;

    [Header("참여 설정")]
    [Tooltip("참여 명령어 (기본: !줄)")]
    public string participantCommand = "!줄";

    [Tooltip("중복 참여 허용 여부")]
    public bool allowDuplicateEntry = false;

    [Tooltip("참여자 최대 인원 (0 = 무제한)")]
    public int maxParticipants = 0;

    [Tooltip("구독자만 참여 가능")]
    public bool subscribersOnly = false;

    [Header("추첨 옵션")]
    [Tooltip("이미 당첨된 사람 제외")]
    public bool excludeWinners = false;

    [Tooltip("구독자만 추첨")]
    public bool drawSubscribersOnly = false;

    [Header("UI 설정")]
    [Tooltip("참여자 UI 프리팹")]
    public GameObject participantPrefab;

    [Tooltip("참여자 UI가 표시될 부모 Transform")]
    public Transform participantContainer;

    [Tooltip("당첨자 UI가 표시될 부모 Transform")]
    public Transform winnerContainer;

    [Tooltip("당첨자 모니터 윈도우 (선택사항)")]
    public WinnerMonitor winnerMonitorWindow;

    [Header("디버그")]
    [Tooltip("Profile 정보 자동 출력 (디버깅용)")]
    public bool debugProfileInfo = false;
    public UnityEvent onRecruitmentStarted;
    public UnityEvent onRecruitmentEnded;
    public UnityEvent<ParticipantData> onParticipantAdded;
    public UnityEvent<ParticipantData> onWinnerSelected;

    // 내부 상태
    private Dictionary<string, ParticipantData> participants = new Dictionary<string, ParticipantData>();
    public Dictionary<string, ParticipantData> Participants
    {
        get { return participants; }
    }
    private Dictionary<string, GameObject> participantUIObjects = new Dictionary<string, GameObject>();
    private HashSet<string> subscribers = new HashSet<string>(); // 구독자 목록 관리
    private Dictionary<string, int> subscriberTiers = new Dictionary<string, int>(); // 구독 등급 저장
    private Dictionary<string, string> subscriberTierNames = new Dictionary<string, string>(); // 구독 등급명 저장
    private bool isRecruiting = false;

    void Start()
    {
        InitializeClient();
    }

    void InitializeClient()
    {
        if (useOAuth)
        {
            if (oauthClient == null)
            {
                Debug.LogError("OAuth Client가 설정되지 않았습니다!");
                return;
            }

            Debug.Log("OAuth 방식으로 연결합니다.");
            InitializeOAuthClient();
        }
        else
        {
            if (chzzkClient == null)
            {
                Debug.LogError("Chzzk Client가 설정되지 않았습니다!");
                return;
            }

            Debug.Log("기존 방식으로 연결합니다.");
            InitializeChzzkClient();
        }
    }
    void InitializeOAuthClient()
    {
        // OAuth 이벤트 등록
        oauthClient.onMessage.AddListener(OnChatMessageReceived);
        oauthClient.onSubscription.AddListener(OnSubscriptionReceived);

        Debug.Log("OAuth 클라이언트 이벤트 등록 완료");

        // 사용자가 수동으로 로그인 버튼을 누를 때까지 대기
        // oauthClient.StartOAuthLogin(); // 자동 로그인 원하면 주석 해제
    }

    /// <summary>
    /// 치지직 클라이언트 초기화
    /// </summary>
    void InitializeChzzkClient()
    {
        // 기존 방식 이벤트 등록
        chzzkClient.onMessage.AddListener(OnChatMessageReceived);
        chzzkClient.onSubscription.AddListener(OnSubscriptionReceived);
        chzzkClient.Connect();
    }

    /// <summary>
    /// 구독 이벤트 수신 콜백
    /// 사용자가 구독하면 호출됨
    /// </summary>
    void OnSubscriptionReceived(ChzzkUnity.Profile profile, ChzzkUnity.SubscriptionExtras extras)
    {
        string userId = profile.userIdHash;

        // 구독자 목록에 추가
        subscribers.Add(userId);

        // 구독 등급 정보 저장
        subscriberTiers[userId] = extras.tierNo;
        subscriberTierNames[userId] = extras.tierName;

        Debug.Log($"구독 감지: {profile.nickname} (등급: {extras.tierName} [Tier {extras.tierNo}], {extras.month}개월)");

        // 이미 참여한 사용자라면 데이터 업데이트
        if (participants.ContainsKey(userId))
        {
            participants[userId].isSubscriber = true;
            participants[userId].subscriberTier = extras.tierNo;
            participants[userId].subscriberTierName = extras.tierName;

            // UI 업데이트
            if (participantUIObjects.ContainsKey(userId))
            {
                var uiComponent = participantUIObjects[userId].GetComponent<SimpleParticipantUI>();
                if (uiComponent != null)
                {
                    uiComponent.SetData(participants[userId]);
                }
            }
        }
    }

    /// <summary>
    /// 채팅 메시지 수신 콜백
    /// ChzzkUnity의 onMessage 이벤트에서 호출됩니다.
    /// Profile은 ChzzkUnity.cs에 정의된 클래스입니다.
    /// </summary>
    void OnChatMessageReceived(ChzzkUnity.Profile profile, string message)
    {
        // 디버그 모드: Profile 정보 출력
        if (debugProfileInfo)
        {
            DebugProfileInfo(profile);
        }
        else
        {
            // 간단한 역할 정보만 출력
            Debug.Log($"[{profile.nickname}] 채팅: {message} | 역할: {profile.userRoleCode ?? "null"} | 배지: {profile.badge ?? "null"}");
        }

        // 당첨자 모니터에 채팅 전달
        if (winnerMonitorWindow != null && participants.ContainsKey(profile.userIdHash))
        {
            ParticipantData participant = participants[profile.userIdHash];
            if (participant.isWinner)
            {
                winnerMonitorWindow.AddChatMessage(participant, message);
            }
        }

        if (!isRecruiting) return;

        // 메시지가 참여 명령어인지 확인
        if (message.Trim() == participantCommand)
        {
            AddParticipant(profile);
        }
    }

    /// <summary>
    /// 참여자 추가
    /// </summary>
    void AddParticipant(ChzzkUnity.Profile profile)
    {
        string userId = profile.userIdHash;

        // 구독자 전용 모드 체크
        if (subscribersOnly && !IsSubscriber(profile))
        {
            Debug.Log($"구독자가 아님: {profile.nickname}");
            return;
        }

        // 중복 체크
        if (!allowDuplicateEntry && participants.ContainsKey(userId))
        {
            Debug.Log($"이미 참여한 사용자: {profile.nickname}");
            return;
        }

        // 최대 인원 체크
        if (maxParticipants > 0 && participants.Count >= maxParticipants)
        {
            Debug.Log("최대 참여 인원에 도달했습니다.");
            return;
        }

        bool isSub = IsSubscriber(profile);
        int tier = isSub && subscriberTiers.ContainsKey(userId) ? subscriberTiers[userId] : 0;
        string tierName = isSub && subscriberTierNames.ContainsKey(userId) ? subscriberTierNames[userId] : "";

        // 참여자 데이터 생성
        ParticipantData participant = new ParticipantData
        {
            userId = userId,
            nickname = profile.nickname,
            profileImageUrl = profile.profileImageUrl,
            isSubscriber = isSub,
            subscriberTier = tier,
            subscriberTierName = tierName,
            joinTime = System.DateTime.Now,
            isWinner = false
        };

        // 참여자 추가
        participants[userId] = participant;

        // UI 생성
        CreateParticipantUI(participant);

        // 이벤트 발생
        onParticipantAdded?.Invoke(participant);

        string tierInfo = isSub ? $", 등급: {tierName} (Tier {tier})" : "";
        Debug.Log($"참여자 추가: {participant.nickname} (구독자: {participant.isSubscriber}{tierInfo}, 총 {participants.Count}명)");
    }

    /// <summary>
    /// 구독자 여부 확인
    /// 여러 방법으로 구독자 확인
    /// </summary>
    bool IsSubscriber(ChzzkUnity.Profile profile)
    {
        // 방법 1: onSubscription 이벤트로 수집한 구독자 목록에서 확인
        if (subscribers.Contains(profile.userIdHash))
        {
            return true;
        }

        // 방법 2: userRoleCode로 확인
        // 구독자는 특정 roleCode를 가짐
        if (!string.IsNullOrEmpty(profile.userRoleCode))
        {
            // 알려진 구독자 역할 코드
            string[] subscriberRoles = new string[]
            {
                "streaming_channel_manager",     // 채널 매니저
                "streaming_chat_manager",        // 채팅 매니저  
                "common_user_with_subscription", // 구독 중인 일반 사용자
                "subscription_user",             // 구독 사용자
                "subscriber"                     // 구독자
            };

            foreach (string role in subscriberRoles)
            {
                if (profile.userRoleCode.Contains(role))
                {
                    // 구독자로 자동 등록
                    if (!subscribers.Contains(profile.userIdHash))
                    {
                        subscribers.Add(profile.userIdHash);
                        Debug.Log($"[자동 감지] 구독자 추가: {profile.nickname} (역할: {profile.userRoleCode})");
                    }
                    return true;
                }
            }
        }

        // 방법 3: streamingProperty 확인 (있다면)
        if (profile.streamingProperty != null)
        {
            // streamingProperty에 구독 정보가 있을 수 있음
            // 정확한 구조는 ChzzkUnity 버전에 따라 다를 수 있음
        }

        // 방법 4: badge로 확인
        if (!string.IsNullOrEmpty(profile.badge))
        {
            // 구독 배지가 있으면 구독자
            if (profile.badge.Contains("subscription") ||
                profile.badge.Contains("subscriber"))
            {
                if (!subscribers.Contains(profile.userIdHash))
                {
                    subscribers.Add(profile.userIdHash);
                    Debug.Log($"[자동 감지] 구독자 추가: {profile.nickname} (배지: {profile.badge})");
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 수동으로 구독자 추가 (테스트용)
    /// </summary>
    public void AddSubscriberManually(string userId)
    {
        subscribers.Add(userId);
        Debug.Log($"수동으로 구독자 추가: {userId}");
    }

    /// <summary>
    /// 닉네임으로 구독자 추가 (테스트용)
    /// </summary>
    public void AddSubscriberByNickname(string nickname)
    {
        foreach (var participant in participants.Values)
        {
            if (participant.nickname == nickname)
            {
                subscribers.Add(participant.userId);
                participant.isSubscriber = true;
                Debug.Log($"닉네임으로 구독자 추가: {nickname}");

                // UI 업데이트
                if (participantUIObjects.ContainsKey(participant.userId))
                {
                    var uiComponent = participantUIObjects[participant.userId].GetComponent<SimpleParticipantUI>();
                    if (uiComponent != null)
                    {
                        uiComponent.SetData(participant);
                    }
                }
                return;
            }
        }
        Debug.LogWarning($"닉네임을 찾을 수 없음: {nickname}");
    }

    /// <summary>
    /// Profile 정보 디버깅 (테스트용)
    /// </summary>
    public void DebugProfileInfo(ChzzkUnity.Profile profile)
    {
        Debug.Log("=== Profile 정보 ===");
        Debug.Log($"userIdHash: {profile.userIdHash}");
        Debug.Log($"nickname: {profile.nickname}");
        Debug.Log($"userRoleCode: {profile.userRoleCode}");
        Debug.Log($"badge: {profile.badge}");
        Debug.Log($"title: {profile.title}");
        Debug.Log($"verifiedMark: {profile.verifiedMark}");

        if (profile.activityBadges != null && profile.activityBadges.Count > 0)
        {
            Debug.Log($"activityBadges: {string.Join(", ", profile.activityBadges)}");
        }

        if (profile.streamingProperty != null)
        {
            Debug.Log("streamingProperty: 존재함");
        }

        Debug.Log($"구독자 여부: {IsSubscriber(profile)}");
        Debug.Log("==================");
    }

    /// <summary>
    /// 구독자 목록 초기화
    /// </summary>
    public void ClearSubscribers()
    {
        subscribers.Clear();
        Debug.Log("구독자 목록이 초기화되었습니다.");
    }

    /// <summary>
    /// 참여자 UI 생성
    /// </summary>
    void CreateParticipantUI(ParticipantData participant)
    {
        if (participantPrefab == null || participantContainer == null) return;

        GameObject uiObject = Instantiate(participantPrefab, participantContainer);
        participantUIObjects[participant.userId] = uiObject;

        // SimpleParticipantUI 컴포넌트에 데이터 전달
        SimpleParticipantUI uiComponent = uiObject.GetComponent<SimpleParticipantUI>();
        if (uiComponent != null)
        {
            uiComponent.SetData(participant);
        }
    }

    /// <summary>
    /// 인원 모집 시작
    /// </summary>
    public void StartRecruitment()
    {
        if (isRecruiting)
        {
            Debug.LogWarning("이미 모집 중입니다.");
            return;
        }

        isRecruiting = true;
        ClearParticipants();
        onRecruitmentStarted?.Invoke();

        Debug.Log("인원 모집을 시작합니다.");
    }

    /// <summary>
    /// 인원 모집 종료
    /// </summary>
    public void EndRecruitment()
    {
        if (!isRecruiting)
        {
            Debug.LogWarning("현재 모집 중이 아닙니다.");
            return;
        }

        isRecruiting = false;
        onRecruitmentEnded?.Invoke();

        Debug.Log($"인원 모집을 종료합니다. 총 {participants.Count}명 참여");
    }

    /// <summary>
    /// 랜덤으로 당첨자 선정
    /// </summary>
    public ParticipantData SelectRandomWinner()
    {
        // 추첨 가능한 참여자 필터링
        List<ParticipantData> eligibleParticipants = GetEligibleParticipants();

        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("추첨 가능한 참여자가 없습니다.");
            return null;
        }

        // 랜덤 선택
        int randomIndex = Random.Range(0, eligibleParticipants.Count);
        ParticipantData winner = eligibleParticipants[randomIndex];

        // 당첨자 표시
        winner.isWinner = true;

        // 당첨자 UI 표시
        ShowWinner(winner);

        // 당첨자 모니터 윈도우 열기
        if (winnerMonitorWindow != null)
        {
            winnerMonitorWindow.OpenForWinner(winner, this);
        }

        // 이벤트 발생
        onWinnerSelected?.Invoke(winner);

        Debug.Log($"당첨자: {winner.nickname} (구독자: {winner.isSubscriber})");
        Debug.Log($"추첨 대상: {eligibleParticipants.Count}명 / 전체: {participants.Count}명");

        return winner;
    }

    /// <summary>
    /// 여러 명의 당첨자 선정
    /// </summary>
    public List<ParticipantData> SelectMultipleWinners(int count)
    {
        // 추첨 가능한 참여자 필터링
        List<ParticipantData> eligibleParticipants = GetEligibleParticipants();

        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("추첨 가능한 참여자가 없습니다.");
            return new List<ParticipantData>();
        }

        count = Mathf.Min(count, eligibleParticipants.Count);
        List<ParticipantData> winners = new List<ParticipantData>();

        // Fisher-Yates 셔플 알고리즘으로 랜덤 선택
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(i, eligibleParticipants.Count);

            // Swap
            ParticipantData temp = eligibleParticipants[i];
            eligibleParticipants[i] = eligibleParticipants[randomIndex];
            eligibleParticipants[randomIndex] = temp;

            // 당첨자 표시
            eligibleParticipants[i].isWinner = true;
            winners.Add(eligibleParticipants[i]);

            // 당첨자 UI 표시
            ShowWinner(eligibleParticipants[i]);

            // 이벤트 발생
            onWinnerSelected?.Invoke(eligibleParticipants[i]);
        }

        Debug.Log($"{count}명의 당첨자 선정 완료");
        Debug.Log($"추첨 대상: {eligibleParticipants.Count}명 / 전체: {participants.Count}명");

        return winners;
    }

    /// <summary>
    /// 추첨 가능한 참여자 필터링
    /// </summary>
    List<ParticipantData> GetEligibleParticipants()
    {
        List<ParticipantData> eligible = new List<ParticipantData>();

        foreach (var participant in participants.Values)
        {
            // 이미 당첨된 사람 제외 옵션
            if (excludeWinners && participant.isWinner)
            {
                continue;
            }

            // 구독자만 추첨 옵션
            if (drawSubscribersOnly && !participant.isSubscriber)
            {
                continue;
            }

            // 조건을 모두 통과하면 추가
            eligible.Add(participant);
        }

        return eligible;
    }

    /// <summary>
    /// 확률 검증용 대량 추첨
    /// </summary>
    public void VerifyProbability(int drawCount)
    {
        if (participants.Count == 0)
        {
            Debug.LogWarning("참여자가 없습니다.");
            return;
        }

        Debug.Log($"=== 확률 검증 시작: {drawCount}회 추첨 ===");

        // 각 참여자별 당첨 횟수 카운트
        Dictionary<string, int> winCounts = new Dictionary<string, int>();
        foreach (var participant in participants.Values)
        {
            winCounts[participant.userId] = 0;
        }

        // 대량 추첨
        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, participants.Count);
            ParticipantData winner = participants.Values.ElementAt(randomIndex);
            winCounts[winner.userId]++;
        }

        // 결과 로그 출력
        Debug.Log($"\n참여자 수: {participants.Count}명");
        Debug.Log($"총 추첨 횟수: {drawCount}회");
        Debug.Log($"이론적 확률: {(100f / participants.Count):F2}%");
        Debug.Log($"이론적 당첨 횟수: {(drawCount / (float)participants.Count):F2}회\n");

        foreach (var participant in participants.Values)
        {
            int wins = winCounts[participant.userId];
            float actualProbability = (wins / (float)drawCount) * 100f;
            Debug.Log($"{participant.nickname} (구독자: {participant.isSubscriber}): " +
                     $"{wins}회 ({actualProbability:F2}%)");
        }

        Debug.Log("=== 확률 검증 완료 ===\n");
    }

    /// <summary>
    /// 확률 검증용 대량 추첨 (UI 텍스트 반환)
    /// </summary>
    public string VerifyProbabilityWithText(int drawCount)
    {
        if (participants.Count == 0)
        {
            return "참여자가 없습니다.";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 확률 검증: {drawCount}회 추첨 ===\n");

        // 각 참여자별 당첨 횟수 카운트
        Dictionary<string, int> winCounts = new Dictionary<string, int>();
        foreach (var participant in participants.Values)
        {
            winCounts[participant.userId] = 0;
        }

        // 대량 추첨
        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, participants.Count);
            ParticipantData winner = participants.Values.ElementAt(randomIndex);
            winCounts[winner.userId]++;
        }

        // 통계 정보
        sb.AppendLine($"참여자 수: {participants.Count}명");
        sb.AppendLine($"총 추첨 횟수: {drawCount:N0}회");
        sb.AppendLine($"이론적 확률: {(100f / participants.Count):F2}%");
        sb.AppendLine($"이론적 당첨 횟수: {(drawCount / (float)participants.Count):F2}회\n");

        // 개별 참여자 결과
        sb.AppendLine("--- 참여자별 결과 ---");
        foreach (var participant in participants.Values)
        {
            int wins = winCounts[participant.userId];
            float actualProbability = (wins / (float)drawCount) * 100f;
            string subIcon = participant.isSubscriber ? "👑" : "  ";
            sb.AppendLine($"{subIcon} {participant.nickname}: {wins:N0}회 ({actualProbability:F2}%)");
        }

        sb.AppendLine("\n=== 검증 완료 ===");

        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// 당첨자 UI 표시
    /// </summary>
    void ShowWinner(ParticipantData winner)
    {
        if (participantPrefab == null || winnerContainer == null) return;

        GameObject winnerUI = Instantiate(participantPrefab, winnerContainer);

        SimpleParticipantUI uiComponent = winnerUI.GetComponent<SimpleParticipantUI>();
        if (uiComponent != null)
        {
            uiComponent.SetData(winner);
            uiComponent.SetAsWinner(true);
        }
    }

    /// <summary>
    /// 모든 참여자 초기화
    /// </summary>
    public void ClearParticipants()
    {
        participants.Clear();

        // 참여자 UI 삭제
        foreach (var uiObject in participantUIObjects.Values)
        {
            if (uiObject != null)
            {
                Destroy(uiObject);
            }
        }
        participantUIObjects.Clear();

        // 당첨자 UI 삭제
        if (winnerContainer != null)
        {
            foreach (Transform child in winnerContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 구독자 목록은 유지 (다음 추첨에서도 사용)
        Debug.Log("모든 참여자 데이터가 초기화되었습니다. (구독자 목록은 유지됨)");
    }

    /// <summary>
    /// 모든 데이터 완전 초기화 (구독자 목록 포함)
    /// </summary>
    public void ClearAll()
    {
        ClearParticipants();
        ClearSubscribers();
        Debug.Log("모든 데이터가 완전히 초기화되었습니다.");
    }

    /// <summary>
    /// 현재 참여자 수 반환
    /// </summary>
    public int GetParticipantCount()
    {
        return participants.Count;
    }

    /// <summary>
    /// 추첨 가능한 참여자 수 반환
    /// </summary>
    public int GetEligibleCount()
    {
        return GetEligibleParticipants().Count;
    }

    /// <summary>
    /// 모집 중 여부 반환
    /// </summary>
    public bool IsRecruiting()
    {
        return isRecruiting;
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (chzzkClient != null)
        {
            chzzkClient.onMessage.RemoveListener(OnChatMessageReceived);
            chzzkClient.onSubscription.RemoveListener(OnSubscriptionReceived);
        }
    }
}

/// <summary>
/// 참여자 데이터 구조체
/// </summary>
[System.Serializable]
public class ParticipantData
{
    public string userId;
    public string nickname;
    public string profileImageUrl;
    public bool isSubscriber;      // 구독자 여부
    public bool isWinner;          // 당첨 여부
    public int subscriberTier;     // 구독 등급 (0: 비구독, 1~4: 등급)
    public string subscriberTierName; // 구독 등급명 (예: "골드")
    public System.DateTime joinTime;
}