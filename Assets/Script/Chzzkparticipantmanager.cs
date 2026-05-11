using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 치지직 채팅에서 "!줄" 명령어로 참여자를 수집하고 랜덤 추첨하는 매니저
/// 
/// 지원하는 클라이언트:
/// 1. ChzzkUnity (비공식, WebSocket)
/// 2. ChzzkOAuthClient (비공식, OAuth + WebSocket)
/// 3. ChzzkOfficialClient (공식, OAuth + Socket.IO) ← 권장!
/// </summary>
public class ChzzkParticipantManager : MonoBehaviour
{
    [Header("연결 방식 선택")]
    [Tooltip("사용할 클라이언트 종류")]
    public ClientType clientType = ClientType.Official;

    public enum ClientType
    {
        ChzzkUnity,        // 비공식 - 기존
        OAuth,             // 비공식 - OAuth
        Official           // 공식 - 권장!
    }

    [Header("치지직 연결 설정 (기존)")]
    public ChzzkUnity chzzkClient;

    [Header("공식 API 연결 설정 (권장!)")]
    public ChzzkOfficialClient officialClient;

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



    [Tooltip("당첨자 모니터 윈도우 (선택사항)")]
    public WinnerMonitor winnerMonitorWindow;
    public Setting_Canvas settingMonitor;
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
    private HashSet<string> subscribers = new HashSet<string>();
    private Dictionary<string, int> subscriberTiers = new Dictionary<string, int>();
    private Dictionary<string, string> subscriberTierNames = new Dictionary<string, string>();
    private bool isRecruiting = false;

    void Start()
    {
        InitializeClient();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            settingMonitor.OpenMonitor();
        }
    }

    /// <summary>
    /// 클라이언트 초기화
    /// </summary>
    void InitializeClient()
    {
        switch (clientType)
        {
            case ClientType.ChzzkUnity:
                InitializeChzzkClient();
                break;

            case ClientType.Official:
                InitializeOfficialClient();
                break;
        }
    }

    /// <summary>
    /// 기존 ChzzkUnity 클라이언트 초기화
    /// </summary>
    void InitializeChzzkClient()
    {
        if (chzzkClient == null)
        {
            Debug.LogError("ChzzkUnity Client가 설정되지 않았습니다!");
            return;
        }

        Debug.Log("ChzzkUnity 방식으로 연결합니다.");

        //chzzkClient.onMessage.AddListener(OnChatMessageReceived);
        //chzzkClient.onSubscription.AddListener(OnSubscriptionReceived);
        chzzkClient.Connect();
    }

    

    /// <summary>
    /// 공식 API 클라이언트 초기화 (권장!)
    /// </summary>
    void InitializeOfficialClient()
    {
        if (officialClient == null)
        {
            Debug.LogError("Official Client가 설정되지 않았습니다!");
            Debug.LogError("ChzzkOfficialClient를 추가하거나 ChzzkManager를 사용하세요!");
            return;
        }

        Debug.Log("✅ 공식 API 방식으로 연결합니다!");

        // ✅ 공식 API 이벤트 연결
        officialClient.onMessage.AddListener(OnChatMessageReceived);
        officialClient.onSubscription.AddListener(OnSubscriptionReceived);

        Debug.Log("공식 API 클라이언트 이벤트 등록 완료");

        // 참고: ChzzkManager를 사용하면 자동으로 로그인됩니다
    }

    /// <summary>
    /// 구독 이벤트 수신 콜백
    /// </summary>
    public void OnSubscriptionReceived(ChzzkUnity.Profile profile, ChzzkUnity.SubscriptionExtras extras)
    {
        string userId = profile.userIdHash;

        subscribers.Add(userId);
        subscriberTiers[userId] = extras.tierNo;
        subscriberTierNames[userId] = extras.tierName;

        Debug.Log($"구독 감지: {profile.nickname} (등급: {extras.tierName} [Tier {extras.tierNo}], {extras.month}개월)");

        if (participants.ContainsKey(userId))
        {
            participants[userId].isSubscriber = true;
            participants[userId].subscriberTier = extras.tierNo;
            participants[userId].subscriberTierName = extras.tierName;

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
    /// ✅ public으로 변경! (ChzzkManager에서 호출 가능)
    /// </summary>
    public void OnChatMessageReceived(ChzzkUnity.Profile profile, string message)
    {
        if (debugProfileInfo)
        {
            DebugProfileInfo(profile);
        }
        else
        {
            Debug.Log($"[{profile.nickname}] 채팅: {message} | 역할: {profile.userRoleCode ?? "null"} | 배지: {profile.badge ?? "null"}");
        }

        if (winnerMonitorWindow != null && participants.ContainsKey(profile.userIdHash))
        {
            ParticipantData participant = participants[profile.userIdHash];
            if (participant.isWinner)
            {
                winnerMonitorWindow.AddChatMessage(participant, message);
            }
        }

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

        if (subscribersOnly && !IsSubscriber(profile))
        {
            Debug.Log($"구독자가 아님: {profile.nickname}");
            return;
        }

        if (!allowDuplicateEntry && participants.ContainsKey(userId))
        {
            Debug.Log($"이미 참여한 사용자: {profile.nickname}");
            return;
        }

        if (maxParticipants > 0 && participants.Count >= maxParticipants)
        {
            Debug.Log("최대 참여 인원에 도달했습니다.");
            return;
        }

        bool isSub = officialClient.IsSubscriber(profile.userIdHash);
        int tier = isSub && subscriberTiers.ContainsKey(userId) ? subscriberTiers[userId] : 0;
        string tierName = isSub && subscriberTierNames.ContainsKey(userId) ? subscriberTierNames[userId] : "";

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

        participants[userId] = participant;
        CreateParticipantUI(participant);
        onParticipantAdded?.Invoke(participant);

        string tierInfo = isSub ? $", 등급: {tierName} (Tier {tier})" : "";
        Debug.Log($"참여자 추가: {participant.nickname} (구독자: {participant.isSubscriber}{tierInfo}, 총 {participants.Count}명)");
    }

    /// <summary>
    /// 구독자 여부 확인
    /// </summary>
    bool IsSubscriber(ChzzkUnity.Profile profile)
    {
        if (subscribers.Contains(profile.userIdHash))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(profile.userRoleCode))
        {
            string[] subscriberRoles = new string[]
            {
                "streaming_channel_manager",
                "streaming_chat_manager",
                "common_user_with_subscription",
                "subscription_user",
                "subscriber"
            };

            foreach (string role in subscriberRoles)
            {
                if (profile.userRoleCode.Contains(role))
                {
                    if (!subscribers.Contains(profile.userIdHash))
                    {
                        subscribers.Add(profile.userIdHash);
                        Debug.Log($"[자동 감지] 구독자 추가: {profile.nickname} (역할: {profile.userRoleCode})");
                    }
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(profile.badge))
        {
            if (profile.badge.Contains("subscription") || profile.badge.Contains("subscriber"))
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
    /// 참여자 UI 생성
    /// </summary>
    void CreateParticipantUI(ParticipantData participant)
    {
        if (participantPrefab == null || participantContainer == null) return;

        GameObject uiObject = Instantiate(participantPrefab, participantContainer);
        participantUIObjects[participant.userId] = uiObject;

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
        List<ParticipantData> eligibleParticipants = GetEligibleParticipants();

        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("추첨 가능한 참여자가 없습니다.");
            return null;
        }

        int randomIndex = Random.Range(0, eligibleParticipants.Count);
        ParticipantData winner = eligibleParticipants[randomIndex];

        winner.isWinner = true;

        if (winnerMonitorWindow != null)
        {
            winnerMonitorWindow.OpenForWinner(winner, this);
        }

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
        List<ParticipantData> eligibleParticipants = GetEligibleParticipants();

        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("추첨 가능한 참여자가 없습니다.");
            return new List<ParticipantData>();
        }

        // 요청한 수가 가능한 수보다 크면 조정
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
            if (excludeWinners && participant.isWinner)
            {
                continue;
            }

            if (drawSubscribersOnly && !participant.isSubscriber)
            {
                continue;
            }

            eligible.Add(participant);
        }

        return eligible;
    }


    /// <summary>
    /// 모든 참여자 초기화
    /// </summary>
    public void ClearParticipants()
    {
        participants.Clear();

        foreach (var uiObject in participantUIObjects.Values)
        {
            if (uiObject != null)
            {
                Destroy(uiObject);
            }
        }
        participantUIObjects.Clear();

        Debug.Log("모든 참여자 데이터가 초기화되었습니다.");
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
    /// Profile 정보 디버깅
    /// </summary>
    public void DebugProfileInfo(ChzzkUnity.Profile profile)
    {
        Debug.Log("=== Profile 정보 ===");
        Debug.Log($"userIdHash: {profile.userIdHash}");
        Debug.Log($"nickname: {profile.nickname}");
        Debug.Log($"userRoleCode: {profile.userRoleCode}");
        Debug.Log($"badge: {profile.badge}");
        Debug.Log($"구독자 여부: {IsSubscriber(profile)}");
        Debug.Log("==================");
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


        if (officialClient != null)
        {
            officialClient.onMessage.RemoveListener(OnChatMessageReceived);
            officialClient.onSubscription.RemoveListener(OnSubscriptionReceived);
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
    public bool isSubscriber;
    public bool isWinner;
    public int subscriberTier;
    public string subscriberTierName;
    public System.DateTime joinTime;
}