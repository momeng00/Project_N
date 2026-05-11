using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 인원 모집 및 추첨 UI 컨트롤러
/// </summary>
public class RecruitmentUIController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("ChzzkParticipantManager 참조")]
    public ChzzkParticipantManager participantManager;

    [Header("버튼")]

    [Tooltip("랜덤 추첨 버튼 (1명)")]
    public Button drawOneButton;

    [Tooltip("확률 검증 버튼")]
    public Button verifyProbabilityButton;

    [Tooltip("초기화 버튼")]
    public Button clearButton;

    [Tooltip("로그뽑기 버튼")]
    public Button logButton;

    [Header("텍스트")]
    [Tooltip("상태 표시 텍스트")]
    public TextMeshProUGUI statusText;

    [Tooltip("참여자 수 표시 텍스트")]
    public TextMeshProUGUI participantCountText;

    [Tooltip("확률 검증 횟수 입력 필드")]
    public TMP_InputField verifyCountInput;

    [Tooltip("확률 검증 결과 표시 텍스트")]
    public TextMeshProUGUI verifyResultText;

    [Header("옵션")]
    [Tooltip("구독자만 참여 가능 (참여 제한)")]
    public Toggle subscribersOnlyToggle;

    [Tooltip("당첨자 제외 (추첨 시)")]
    public Toggle excludeWinnersToggle;

    [Tooltip("구독자만 추첨 (추첨 시)")]
    public Toggle drawSubscribersOnlyToggle;

    [Header("패널")]
    [Tooltip("확률 검증 결과 패널 (선택사항)")]
    public GameObject verifyResultPanel;

    [Header("애니메이션")]
    [Tooltip("추첨 애니메이션 지속 시간")]
    public float drawAnimationDuration = 2f;

    [Tooltip("추첨 시 참여자 하이라이트 속도")]
    public float highlightSpeed = 0.1f;

    [Header("사운드")]
    [Tooltip("버튼 클릭 사운드")]
    public AudioClip buttonClickSound;

    [Tooltip("추첨 사운드")]
    public AudioClip drawSound;

    [Tooltip("당첨 사운드")]
    public AudioClip winnerSound;

    private AudioSource audioSource;
    private bool isDrawing = false;

    void Start()
    {
        InitializeUI();
        SetupEventListeners();

        // AudioSource 추가
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        UpdateUI();

    }

    /// <summary>
    /// UI 초기화
    /// </summary>
    void InitializeUI()
    {
        if (participantManager == null)
        {
            participantManager = FindObjectOfType<ChzzkParticipantManager>();
        }

        UpdateButtonStates();
    }

    /// <summary>
    /// 이벤트 리스너 설정
    /// </summary>
    void SetupEventListeners()
    {
        // 버튼 이벤트 연결
      

        if (drawOneButton != null)
        {
            drawOneButton.onClick.AddListener(OnDrawOne);
        }


        if (verifyProbabilityButton != null)
        {
            verifyProbabilityButton.onClick.AddListener(OnVerifyProbability);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClear);
        }

        // 구독자 전용 토글
        if (subscribersOnlyToggle != null)
        {
            subscribersOnlyToggle.onValueChanged.AddListener(OnSubscribersOnlyToggled);

            // 초기값 설정
            if (participantManager != null)
            {
                subscribersOnlyToggle.isOn = participantManager.subscribersOnly;
            }
        }

        // 당첨자 제외 토글
        if (excludeWinnersToggle != null)
        {
            excludeWinnersToggle.onValueChanged.AddListener(OnExcludeWinnersToggled);

            if (participantManager != null)
            {
                excludeWinnersToggle.isOn = participantManager.excludeWinners;
            }
        }

        // 구독자만 추첨 토글
        if (drawSubscribersOnlyToggle != null)
        {
            drawSubscribersOnlyToggle.onValueChanged.AddListener(OnDrawSubscribersOnlyToggled);

            if (participantManager != null)
            {
                drawSubscribersOnlyToggle.isOn = participantManager.drawSubscribersOnly;
            }
        }

        // ParticipantManager 이벤트 구독
        if (participantManager != null)
        {
            participantManager.onRecruitmentStarted.AddListener(OnRecruitmentStarted);
            participantManager.onRecruitmentEnded.AddListener(OnRecruitmentEnded);
            participantManager.onParticipantAdded.AddListener(OnParticipantAdded);
            participantManager.onWinnerSelected.AddListener(OnWinnerSelected);
        }
    }

    /// <summary>
    /// UI 업데이트
    /// </summary>
    void UpdateUI()
    {
        if (participantManager == null) return;

        // 참여자 수 업데이트
        if (participantCountText != null)
        {
            int totalCount = participantManager.GetParticipantCount();
            int eligibleCount = participantManager.GetEligibleCount();

            if (totalCount != eligibleCount)
            {
                participantCountText.text = $"참여자: {totalCount}명 (추첨 대상: {eligibleCount}명)";
            }
            else
            {
                participantCountText.text = $"참여자: {totalCount}명";
            }
        }

        // 상태 텍스트 업데이트
        if (statusText != null)
        {
            if (isDrawing)
            {
                statusText.text = "추첨 중...";
            }
            else if (participantManager.IsRecruiting())
            {
                statusText.text = $"모집 중 (명령어: {participantManager.participantCommand})";
            }
            else
            {
                statusText.text = "대기 중";
            }
        }

        UpdateButtonStates();
    }

    /// <summary>
    /// 버튼 상태 업데이트
    /// </summary>
    void UpdateButtonStates()
    {
        if (participantManager == null) return;

        bool isRecruiting = participantManager.IsRecruiting();
        int participantCount = participantManager.GetParticipantCount();


        if (drawOneButton != null)
        {
            drawOneButton.interactable = !isRecruiting && participantCount > 0 && !isDrawing;
        }

        if (verifyProbabilityButton != null)
        {
            verifyProbabilityButton.interactable = !isRecruiting && participantCount > 0 && !isDrawing;
        }

        if (clearButton != null)
        {
            clearButton.interactable = !isRecruiting && !isDrawing;
        }
    }

    // ===== 버튼 이벤트 핸들러 =====

    void OnStartRecruitment()
    {
        PlaySound(buttonClickSound);
        participantManager.StartRecruitment();
    }

    void OnEndRecruitment()
    {
        PlaySound(buttonClickSound);
        participantManager.EndRecruitment();
    }

    void OnDrawOne()
    {
        PlaySound(buttonClickSound);
        StartCoroutine(DrawWithAnimation(1));
    }


    void OnClear()
    {
        PlaySound(buttonClickSound);
        participantManager.ClearParticipants();
    }

    void OnVerifyProbability()
    {
        PlaySound(buttonClickSound);

        int count = 1000; // 기본값
        if (verifyCountInput != null && int.TryParse(verifyCountInput.text, out int inputCount))
        {
            count = Mathf.Max(10, inputCount);
        }

        Debug.Log($"=== 확률 검증 시작: {count}회 추첨 ===");

        // UI에 결과 표시
        string result = participantManager.VerifyProbabilityWithText(count);
        if (verifyResultText != null)
        {
            verifyResultText.text = result;
        }

        // 결과 패널 표시
        if (verifyResultPanel != null)
        {
            verifyResultPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 확률 검증 패널 닫기
    /// </summary>
    public void CloseVerifyPanel()
    {
        if (verifyResultPanel != null)
        {
            verifyResultPanel.SetActive(false);
        }
    }

    void OnSubscribersOnlyToggled(bool isOn)
    {
        if (participantManager != null)
        {
            participantManager.subscribersOnly = isOn;
            Debug.Log($"구독자 전용 참여: {(isOn ? "ON" : "OFF")}");
        }
    }

    void OnExcludeWinnersToggled(bool isOn)
    {
        if (participantManager != null)
        {
            participantManager.excludeWinners = isOn;
            Debug.Log($"당첨자 제외: {(isOn ? "ON" : "OFF")}");
        }
    }

    void OnDrawSubscribersOnlyToggled(bool isOn)
    {
        if (participantManager != null)
        {
            participantManager.drawSubscribersOnly = isOn;
            Debug.Log($"구독자만 추첨: {(isOn ? "ON" : "OFF")}");
        }
    }

    // ===== ParticipantManager 이벤트 핸들러 =====

    void OnRecruitmentStarted()
    {
        Debug.Log("UI: 모집 시작됨");
    }

    void OnRecruitmentEnded()
    {
        Debug.Log("UI: 모집 종료됨");
    }

    void OnParticipantAdded(ParticipantData participant)
    {
        Debug.Log($"UI: 참여자 추가됨 - {participant.nickname}");
        PlaySound(buttonClickSound);
    }

    void OnWinnerSelected(ParticipantData winner)
    {
        Debug.Log($"UI: 당첨자 선정됨 - {winner.nickname}");
    }

    // ===== 애니메이션 =====

    /// <summary>
    /// 추첨 애니메이션과 함께 당첨자 선정
    /// </summary>
    System.Collections.IEnumerator DrawWithAnimation(int winnerCount)
    {
        isDrawing = true;
        PlaySound(drawSound);

        // 참여자 UI 하이라이트 애니메이션
        float elapsed = 0f;
        while (elapsed < drawAnimationDuration)
        {
            // 여기에 참여자 UI를 순차적으로 하이라이트하는 로직 추가 가능
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 당첨자 선정
        if (winnerCount == 1)
        {
            participantManager.SelectRandomWinner();
        }
        else
        {
            participantManager.SelectMultipleWinners(winnerCount);
        }

        PlaySound(winnerSound);
        isDrawing = false;
    }

    /// <summary>
    /// 사운드 재생
    /// </summary>
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (participantManager != null)
        {
            participantManager.onRecruitmentStarted.RemoveListener(OnRecruitmentStarted);
            participantManager.onRecruitmentEnded.RemoveListener(OnRecruitmentEnded);
            participantManager.onParticipantAdded.RemoveListener(OnParticipantAdded);
            participantManager.onWinnerSelected.RemoveListener(OnWinnerSelected);
        }
    }
}