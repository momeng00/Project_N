using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Setting_Canvas : MonoBehaviour
{
    public ChzzkParticipantManager participantManager;
    public TMP_Text log;
    public TMP_InputField test_count;
    private CanvasGroup canvasGroup;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        gameObject.GetComponent<Image>().raycastTarget = false;
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameObject.GetComponent<Image>().raycastTarget)
            {
                gameObject.GetComponent<Image>().raycastTarget = false;
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.GetComponent<Image>().raycastTarget = true;
                canvasGroup.alpha = 1;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
    public void OnTestBTN()
    {
        if (test_count != null && int.TryParse(test_count.text, out int inputCount))
        {
            VerifyProbability(inputCount);
        }
    }
    public void VerifyProbability(int drawCount)
    {
        StringBuilder sb = new StringBuilder();
        if (participantManager.Participants.Count == 0)
        {
            Debug.LogWarning("참여자가 없습니다.");
            return;
        }

        sb.AppendLine($"=== 확률 검증 시작: {drawCount}회 추첨 ===");

        // 각 참여자별 당첨 횟수 카운트
        Dictionary<string, int> winCounts = new Dictionary<string, int>();
        foreach (var participant in participantManager.Participants.Values)
        {
            winCounts[participant.userId] = 0;
        }

        // 대량 추첨
        for (int i = 0; i < drawCount; i++)
        {
            int randomIndex = Random.Range(0, participantManager.Participants.Count);
            ParticipantData winner = participantManager.Participants.Values.ElementAt(randomIndex);
            winCounts[winner.userId]++;
        }

        // 결과 로그 출력
        sb.AppendLine($"\n참여자 수: {participantManager.Participants.Count}명");
        sb.AppendLine($"총 추첨 횟수: {drawCount}회");
        sb.AppendLine($"이론적 확률: {(100f / participantManager.Participants.Count):F2}%");
        sb.AppendLine($"이론적 당첨 횟수: {(drawCount / (float)participantManager.Participants.Count):F2}회\n");

        foreach (var participant in participantManager.Participants.Values)
        {
            int wins = winCounts[participant.userId];
            float actualProbability = (wins / (float)drawCount) * 100f;
            sb.AppendLine($"{participant.nickname} (구독자: {participant.isSubscriber} : {wins}회 ({actualProbability:F2}%)");
        }

        sb.AppendLine("=== 확률 검증 완료 ===\n");
        log.text = sb.ToString();
    }
}
