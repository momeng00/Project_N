using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 참여자 UI 표시 컴포넌트
/// </summary>
public class ParticipantUI : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("프로필 이미지")]
    public Image profileImage;

    [Tooltip("닉네임 텍스트")]
    public TextMeshProUGUI nicknameText;

    [Tooltip("참여 시간 텍스트")]
    public TextMeshProUGUI joinTimeText;

    [Tooltip("당첨자 표시 오브젝트")]
    public GameObject winnerIndicator;

    [Header("애니메이션 설정")]
    [Tooltip("등장 애니메이션 사용")]
    public bool useEntranceAnimation = true;

    [Tooltip("애니메이션 지속 시간")]
    public float animationDuration = 0.3f;

    [Header("당첨자 효과")]
    [Tooltip("당첨자 배경 색상")]
    public Color winnerColor = new Color(1f, 0.84f, 0f, 1f); // 골드

    [Tooltip("일반 참여자 배경 색상")]
    public Color normalColor = Color.white;

    private ParticipantData participantData;
    private Image backgroundImage;
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        backgroundImage = GetComponent<Image>();

        if (winnerIndicator != null)
        {
            winnerIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// 참여자 데이터 설정
    /// </summary>
    public void SetData(ParticipantData data)
    {
        participantData = data;
        UpdateUI();

        if (useEntranceAnimation)
        {
            PlayEntranceAnimation();
        }
    }

    /// <summary>
    /// UI 업데이트
    /// </summary>
    void UpdateUI()
    {
        // 닉네임 설정
        if (nicknameText != null)
        {
            nicknameText.text = participantData.nickname;
        }

        // 참여 시간 설정
        if (joinTimeText != null)
        {
            joinTimeText.text = participantData.joinTime.ToString("HH:mm:ss");
        }

        // 프로필 이미지 로드 (URL이 있는 경우)
        if (profileImage != null && !string.IsNullOrEmpty(participantData.profileImageUrl))
        {
            StartCoroutine(LoadProfileImage(participantData.profileImageUrl));
        }
    }

    /// <summary>
    /// 프로필 이미지 로드
    /// </summary>
    System.Collections.IEnumerator LoadProfileImage(string url)
    {
        UnityEngine.Networking.UnityWebRequest request =
            UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);

        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
            profileImage.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
        }
        else
        {
            Debug.LogWarning($"프로필 이미지 로드 실패: {url}");
        }
    }

    /// <summary>
    /// 당첨자로 설정
    /// </summary>
    public void SetAsWinner(bool isWinner)
    {
        if (winnerIndicator != null)
        {
            winnerIndicator.SetActive(isWinner);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = isWinner ? winnerColor : normalColor;
        }

        if (isWinner)
        {
            PlayWinnerAnimation();
        }
    }

    /// <summary>
    /// 등장 애니메이션
    /// </summary>
    void PlayEntranceAnimation()
    {
        if (rectTransform == null) return;

        // 초기 상태: 작은 크기, 투명
        Vector3 originalScale = rectTransform.localScale;
        rectTransform.localScale = Vector3.zero;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;

        // 애니메이션
        LeanTween.scale(rectTransform, originalScale, animationDuration)
            .setEase(LeanTweenType.easeOutBack);

        LeanTween.alphaCanvas(canvasGroup, 1f, animationDuration);
    }

    /// <summary>
    /// 당첨자 애니메이션
    /// </summary>
    void PlayWinnerAnimation()
    {
        if (rectTransform == null) return;

        // 펄스 효과
        Vector3 originalScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        LeanTween.scale(rectTransform, targetScale, 0.3f)
            .setEase(LeanTweenType.easeOutQuad)
            .setLoopPingPong(2);

        // 회전 효과
        LeanTween.rotateZ(gameObject, 360f, 1f)
            .setEase(LeanTweenType.easeInOutQuad);
    }

    /// <summary>
    /// 하이라이트 효과
    /// </summary>
    public void Highlight()
    {
        if (backgroundImage == null) return;

        Color originalColor = backgroundImage.color;
        Color highlightColor = new Color(
            originalColor.r * 1.3f,
            originalColor.g * 1.3f,
            originalColor.b * 1.3f,
            originalColor.a
        );

        LeanTween.value(gameObject, originalColor, highlightColor, 0.2f)
            .setOnUpdate((Color color) => {
                if (backgroundImage != null)
                    backgroundImage.color = color;
            })
            .setLoopPingPong(1);
    }
}