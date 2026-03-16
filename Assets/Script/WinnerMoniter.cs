using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 당첨자의 채팅을 실시간으로 모니터링하는 창
/// 새 채팅이 위에서 추가되고 기존 채팅은 아래로 내려가면서 작아짐
/// </summary>
public class WinnerMonitor : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("당첨자 닉네임 표시")]
    public TextMeshProUGUI winnerNameText;

    [Tooltip("채팅 메시지가 추가될 컨테이너")]
    public Transform chatContainer;

    [Tooltip("채팅 메시지 프리팹")]
    public GameObject chatMessagePrefab;

    [Tooltip("닫기 버튼")]
    public Button closeButton;

    [Header("설정")]
    [Tooltip("최대 채팅 표시 개수")]
    public int maxChatMessages = 10;

    [Tooltip("메시지 간격")]
    public float messageSpacing = 10f;

    [Tooltip("크기 감소 비율 (1.0 = 100%)")]
    public float scaleFactor = 0.95f;

    [Tooltip("애니메이션 속도")]
    public float animationDuration = 0.3f;

    [Header("색상")]
    public Color normalChatColor = Color.black;
    public Color commandChatColor = Color.yellow;
    public Color timestampColor = Color.gray;

    // 내부 상태
    private ParticipantData winnerData;
    private List<GameObject> chatMessageObjects = new List<GameObject>();
    private ChzzkParticipantManager participantManager;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseWindow);
        }
        gameObject.GetComponent<Image>().raycastTarget = false;
        gameObject.GetComponent<CanvasGroup>().alpha = 0;
        gameObject.GetComponent<CanvasGroup>().interactable = false;
        gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    /// <summary>
    /// 당첨자 설정 및 창 열기
    /// </summary>
    public void OpenForWinner(ParticipantData winner, ChzzkParticipantManager manager)
    {
        gameObject.GetComponent<Image>().raycastTarget = true;
        gameObject.GetComponent<CanvasGroup>().alpha = 1;
        gameObject.GetComponent<CanvasGroup>().interactable = true;
        gameObject.GetComponent<CanvasGroup>().blocksRaycasts = true;

        winnerData = winner;
        participantManager = manager;

        // 기존 메시지 삭제
        ClearAllMessages();

        // 당첨자 이름 표시
        if (winnerNameText != null)
        {
            string subIcon = winner.isSubscriber ? "👑 " : "";
            string tierName = winner.isSubscriber ? $" [{winner.nickname}]" : "";
            winnerNameText.text = $"{subIcon}{winner.nickname}{tierName} 님의 채팅";
        }

        // 창 활성화
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 채팅 메시지 추가 (외부에서 호출)
    /// </summary>
    public void AddChatMessage(ParticipantData sender, string message)
    {
        // 당첨자의 채팅만 필터링
        if (winnerData == null || sender.userId != winnerData.userId)
        {
            return;
        }


        // 색상 선택
        Color messageColor =  normalChatColor;


        // UI 생성
        CreateChatMessageUI(message, Color.black);
    }

    /// <summary>
    /// 시스템 메시지 추가
    /// </summary>
    void AddSystemMessage(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string formattedMessage = $"[{timestamp}] {message}";
        CreateChatMessageUI(formattedMessage, Color.cyan);
    }

    /// <summary>
    /// 채팅 메시지 UI 생성
    /// </summary>
    void CreateChatMessageUI(string message, Color color)
    {
        if (chatMessagePrefab == null || chatContainer == null)
        {
            Debug.LogWarning("채팅 메시지 프리팹 또는 컨테이너가 설정되지 않았습니다.");
            return;
        }

        // 새 메시지 생성
        GameObject messageObj = Instantiate(chatMessagePrefab, chatContainer);

        // 맨 위로 이동 (첫 번째 자식으로)
        messageObj.transform.SetAsFirstSibling();

        // 메시지 설정
        ChatMessage messageItem = messageObj.GetComponent<ChatMessage>();
        if (messageItem != null)
        {
            messageItem.SetMessage(message, color);
            messageItem.FadeIn();
        }
        else
        {
            // ChatMessageItem이 없으면 직접 텍스트 설정
            TextMeshProUGUI text = messageObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = message;
                text.color = color;
            }
        }

        // 리스트에 추가
        chatMessageObjects.Insert(0, messageObj);

        // 기존 메시지들 아래로 밀기 및 크기 조절
        UpdateMessagePositions();

        // 최대 개수 제한
        if (chatMessageObjects.Count > maxChatMessages)
        {
            GameObject oldestMessage = chatMessageObjects[chatMessageObjects.Count - 1];
            chatMessageObjects.RemoveAt(chatMessageObjects.Count - 1);
            Destroy(oldestMessage);
        }
    }

    /// <summary>
    /// 메시지 위치 및 크기 업데이트
    /// </summary>
    void UpdateMessagePositions()
    {
        for (int i = 0; i < chatMessageObjects.Count; i++)
        {
            GameObject messageObj = chatMessageObjects[i];
            if (messageObj == null) continue;

            // 인덱스에 따른 크기 계산 (위쪽일수록 크게)
            float scale = Mathf.Pow(scaleFactor, i);
            scale = Mathf.Clamp(scale, 0.5f, 1.0f); // 최소 50%, 최대 100%

            // 투명도 계산 (아래로 갈수록 투명)
            float alpha = Mathf.Lerp(1.0f, 0.3f, i / (float)maxChatMessages);

            // 애니메이션 적용
            ChatMessage messageItem = messageObj.GetComponent<ChatMessage>();
            if (messageItem != null)
            {
                messageItem.ScaleTo(scale, animationDuration);
                messageItem.SetAlpha(alpha);
            }
            else
            {
                // ChatMessageItem이 없으면 직접 스케일 조절
                RectTransform rectTransform = messageObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    LeanTween.scale(rectTransform, new Vector3(scale, scale, 1f), animationDuration)
                        .setEase(LeanTweenType.easeOutQuad);
                }

                CanvasGroup canvasGroup = messageObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = messageObj.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = alpha;
            }
        }
    }

    /// <summary>
    /// 모든 메시지 삭제
    /// </summary>
    void ClearAllMessages()
    {
        foreach (GameObject messageObj in chatMessageObjects)
        {
            if (messageObj != null)
            {
                Destroy(messageObj);
            }
        }
        chatMessageObjects.Clear();
    }

    /// <summary>
    /// 창 닫기
    /// </summary>
    public void CloseWindow()
    {
        LeanTween.delayedCall(0.5f, () => {
            gameObject.GetComponent<Image>().raycastTarget = false;
            gameObject.GetComponent<CanvasGroup>().alpha = 0;
            gameObject.GetComponent<CanvasGroup>().interactable = false;
            gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
            
        });
    }

    /// <summary>
    /// 채팅 초기화
    /// </summary>
    public void ClearChat()
    {
        ClearAllMessages();
        AddSystemMessage("채팅 기록이 초기화되었습니다.");
    }

    void OnDestroy()
    {
        // LeanTween 정리
        LeanTween.cancel(gameObject);
    }
}