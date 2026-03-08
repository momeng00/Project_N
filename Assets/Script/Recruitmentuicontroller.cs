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
    [Tooltip("인원 모집 시작 버튼")]
    public Button startRecruitmentButton;

    [Tooltip("인원 모집 종료 버튼")]
    public Button endRecruitmentButton;

    [Tooltip("랜덤 추첨 버튼 (1명)")]
    public Button drawOneButton;

    [Tooltip("복수 추첨 버튼")]
    public Button drawMultipleButton;

    [Tooltip("초기화 버튼")]
    public Button clearButton;

    [Header("텍스트")]
    [Tooltip("상태 표시 텍스트")]
    public TextMeshProUGUI statusText;

    [Tooltip("참여자 수 표시 텍스트")]
    public TextMeshProUGUI participantCountText;

    [Tooltip("복수 추첨 인원 입력 필드")]
    public TMP_InputField multipleDrawCountInput;

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
        if (startRecruitmentButton != null)
        {
            startRecruitmentButton.onClick.AddListener(OnStartRecruitment);
        }

        if (endRecruitmentButton != null)
        {
            endRecruitmentButton.onClick.AddListener(OnEndRecruitment);
        }

        if (drawOneButton != null)
        {
            drawOneButton.onClick.AddListener(OnDrawOne);
        }

        if (drawMultipleButton != null)
        {
            drawMultipleButton.onClick.AddListener(OnDrawMultiple);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClear);
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
            int count = participantManager.GetParticipantCount();
            participantCountText.text = $"참여자: {count}명";
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

        if (startRecruitmentButton != null)
        {
            startRecruitmentButton.interactable = !isRecruiting && !isDrawing;
        }

        if (endRecruitmentButton != null)
        {
            endRecruitmentButton.interactable = isRecruiting && !isDrawing;
        }

        if (drawOneButton != null)
        {
            drawOneButton.interactable = !isRecruiting && participantCount > 0 && !isDrawing;
        }

        if (drawMultipleButton != null)
        {
            drawMultipleButton.interactable = !isRecruiting && participantCount > 1 && !isDrawing;
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

    void OnDrawMultiple()
    {
        PlaySound(buttonClickSound);

        int count = 1;
        if (multipleDrawCountInput != null && int.TryParse(multipleDrawCountInput.text, out int inputCount))
        {
            count = Mathf.Max(1, inputCount);
        }

        StartCoroutine(DrawWithAnimation(count));
    }

    void OnClear()
    {
        PlaySound(buttonClickSound);
        participantManager.ClearParticipants();
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