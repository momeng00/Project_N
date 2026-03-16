using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 개별 채팅 메시지 UI 아이템
/// </summary>
public class ChatMessage : MonoBehaviour
{
    [Header("UI 요소")]
    public TextMeshProUGUI messageText;
    public Image backgroundImage;

    [Header("설정")]
    public float fadeInDuration = 0.3f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 메시지 설정
    /// </summary>
    public void SetMessage(string message, Color color)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color;
        }
    }

    /// <summary>
    /// 페이드인 애니메이션
    /// </summary>
    public void FadeIn()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration);
    }

    /// <summary>
    /// 크기 조절 애니메이션
    /// </summary>
    public void ScaleTo(float scale, float duration)
    {
        if (rectTransform == null) return;

        Vector3 targetScale = new Vector3(scale, scale, 1f);
        LeanTween.scale(rectTransform, targetScale, duration)
            .setEase(LeanTweenType.easeOutQuad);
    }

    /// <summary>
    /// 투명도 조절
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }
}