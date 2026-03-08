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

    [Header("UI 설정")]
    [Tooltip("참여자 UI 프리팹")]
    public GameObject participantPrefab;

    [Tooltip("참여자 UI가 표시될 부모 Transform")]
    public Transform participantContainer;

    [Tooltip("당첨자 UI가 표시될 부모 Transform")]
    public Transform winnerContainer;

    [Header("이벤트")]
    public UnityEvent onRecruitmentStarted;
    public UnityEvent onRecruitmentEnded;
    public UnityEvent<ParticipantData> onParticipantAdded;
    public UnityEvent<ParticipantData> onWinnerSelected;

    // 내부 상태
    private ChzzkUnity chzzkClient;
    private Dictionary<string, ParticipantData> participants = new Dictionary<string, ParticipantData>();
    private Dictionary<string, GameObject> participantUIObjects = new Dictionary<string, GameObject>();
    private HashSet<string> subscribers = new HashSet<string>(); // 구독자 목록 관리
    private bool isRecruiting = false;

    void Start()
    {
        InitializeChzzkClient();
    }

    /// <summary>
    /// 치지직 클라이언트 초기화
    /// </summary>
    void InitializeChzzkClient()
    {
        chzzkClient = GetComponent<ChzzkUnity>();
        if (chzzkClient == null)
        {
            chzzkClient = gameObject.AddComponent<ChzzkUnity>();
        }

        chzzkClient.channel = channelId;

        // 채팅 메시지 이벤트 구독
        chzzkClient.onMessage.AddListener(OnChatMessageReceived);

        // 구독 이벤트 구독 (구독자 추적용)
        chzzkClient.onSubscription.AddListener(OnSubscriptionReceived);

        // 치지직 연결
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

        Debug.Log($"구독 감지: {profile.nickname} (등급: {extras.tierName}, {extras.month}개월)");

        // 이미 참여한 사용자라면 데이터 업데이트
        if (participants.ContainsKey(userId))
        {
            participants[userId].isSubscriber = true;

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

        // 참여자 데이터 생성
        ParticipantData participant = new ParticipantData
        {
            userId = userId,
            nickname = profile.nickname,
            profileImageUrl = profile.profileImageUrl,
            isSubscriber = IsSubscriber(profile),
            joinTime = System.DateTime.Now,
            isWinner = false
        };

        // 참여자 추가
        participants[userId] = participant;

        // UI 생성
        CreateParticipantUI(participant);

        // 이벤트 발생
        onParticipantAdded?.Invoke(participant);

        Debug.Log($"참여자 추가: {participant.nickname} (구독자: {participant.isSubscriber}, 총 {participants.Count}명)");
    }

    /// <summary>
    /// 구독자 여부 확인
    /// onSubscription 이벤트로 수집한 구독자 목록에서 확인
    /// </summary>
    bool IsSubscriber(ChzzkUnity.Profile profile)
    {
        // 구독자 목록에 있는지 확인
        return subscribers.Contains(profile.userIdHash);
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

        // ParticipantUI 컴포넌트에 데이터 전달
        ParticipantUI uiComponent = uiObject.GetComponent<ParticipantUI>();
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
        if (participants.Count == 0)
        {
            Debug.LogWarning("참여자가 없습니다.");
            return null;
        }

        // 랜덤 선택
        int randomIndex = Random.Range(0, participants.Count);
        ParticipantData winner = participants.Values.ElementAt(randomIndex);

        // 당첨자 표시
        winner.isWinner = true;

        // 당첨자 UI 표시
        ShowWinner(winner);

        // 이벤트 발생
        onWinnerSelected?.Invoke(winner);

        Debug.Log($"당첨자: {winner.nickname} (구독자: {winner.isSubscriber})");
        return winner;
    }

    /// <summary>
    /// 여러 명의 당첨자 선정
    /// </summary>
    public List<ParticipantData> SelectMultipleWinners(int count)
    {
        if (participants.Count == 0)
        {
            Debug.LogWarning("참여자가 없습니다.");
            return new List<ParticipantData>();
        }

        count = Mathf.Min(count, participants.Count);
        List<ParticipantData> allParticipants = participants.Values.ToList();
        List<ParticipantData> winners = new List<ParticipantData>();

        // Fisher-Yates 셔플 알고리즘으로 랜덤 선택
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(i, allParticipants.Count);

            // Swap
            ParticipantData temp = allParticipants[i];
            allParticipants[i] = allParticipants[randomIndex];
            allParticipants[randomIndex] = temp;

            // 당첨자 표시
            allParticipants[i].isWinner = true;
            winners.Add(allParticipants[i]);

            // 당첨자 UI 표시
            ShowWinner(allParticipants[i]);

            // 이벤트 발생
            onWinnerSelected?.Invoke(allParticipants[i]);
        }

        Debug.Log($"{count}명의 당첨자 선정 완료");
        return winners;
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
    /// 당첨자 UI 표시
    /// </summary>
    void ShowWinner(ParticipantData winner)
    {
        if (participantPrefab == null || winnerContainer == null) return;

        GameObject winnerUI = Instantiate(participantPrefab, winnerContainer);

        ParticipantUI uiComponent = winnerUI.GetComponent<ParticipantUI>();
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
    public System.DateTime joinTime;
}