using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 간단한 참여자 UI - 닉네임, 구독여부, 당첨여부만 표시
/// </summary>
public class SimpleParticipantUI : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("닉네임 텍스트")]
    public TMP_Text nicknameText;

    [Tooltip("구독자 표시 오브젝트 (별, 왕관 등)")]
    public GameObject subscriberIndicator;

    [Tooltip("당첨자 표시 오브젝트")]
    public GameObject winnerIndicator;

    [Header("색상 설정")]
    [Tooltip("일반 참여자 색상")]
    public Color normalColor = Color.white;

    [Tooltip("구독자 색상")]
    public Color subscriberColor = new Color(1f, 0.84f, 0f, 1f); // 골드

    [Tooltip("당첨자 색상")]
    public Color winnerColor = new Color(0f, 1f, 0f, 1f); // 초록색

    private ParticipantData participantData;
    private Image backgroundImage;

    void Awake()
    {
        backgroundImage = GetComponent<Image>();

        if (subscriberIndicator != null)
        {
            subscriberIndicator.SetActive(false);
        }

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

        // 구독자 표시
        if (subscriberIndicator != null)
        {
            subscriberIndicator.SetActive(participantData.isSubscriber);
        }

        // 당첨자 표시
        if (winnerIndicator != null)
        {
            winnerIndicator.SetActive(participantData.isWinner);
        }

        // 배경 색상
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 배경 색상 업데이트
    /// </summary>
    void UpdateBackgroundColor()
    {
        if (backgroundImage == null) return;

        if (participantData.isWinner)
        {
            backgroundImage.color = winnerColor;
        }
        else if (participantData.isSubscriber)
        {
            backgroundImage.color = subscriberColor;
        }
        else
        {
            backgroundImage.color = normalColor;
        }
    }

    /// <summary>
    /// 당첨자로 설정
    /// </summary>
    public void SetAsWinner(bool isWinner)
    {
        if (participantData != null)
        {
            participantData.isWinner = isWinner;
            UpdateUI();
        }
    }

    /// <summary>
    /// 데이터 문자열 반환 (로그용)
    /// </summary>
    public string GetDataString()
    {
        if (participantData == null) return "";

        return $"{participantData.nickname} | " +
               $"구독자: {(participantData.isSubscriber ? "O" : "X")} | " +
               $"당첨: {(participantData.isWinner ? "O" : "X")}";
    }
}