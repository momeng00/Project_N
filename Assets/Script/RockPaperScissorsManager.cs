using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ChzzkUnity;

public class RockPaperScissorsManager : MonoBehaviour
{
    public ChzzkOfficialClient client;
    [Header("UI")]
    public Button resetButton;
    public TMP_Text statusText;
    public TMP_Text timerText;
    public TMP_Text participantCountText;
    public TMP_Text logText;
    public GameObject startPanel;
    [Header("결과 UI")]
    public Transform resultListParent;
    public GameObject resultItemPrefab;
    [Header("설정")]
    public float roundTime = 30f;

    // 참가자 데이터
    private Dictionary<string, PlayerChoice> participants = new Dictionary<string, PlayerChoice>();
    private Dictionary<string, PlayerScore> playerScores = new Dictionary<string, PlayerScore>();

    private bool isGameActive = false;
    private string hostChoice = "";
    private float currentTimer = 0f;
    [SerializeField] public List<PersonalPanel> panels = new List<PersonalPanel>();

    [Serializable]
    public struct PersonalPanel
    {
        public PanelName panelName;
        public RectTransform panel;
    }
    public enum PanelName
    {
        Start,
        Play,
        Ready,
        Result
    }
    private enum Choice
    {
        None,
        Rock,    // 주먹 (바위)
        Scissors, // 가위
        Paper    // 보
    }
    private class PlayerScore
    {
        public string channelId;
        public string nickname;
        public int score;
    }
    private class PlayerChoice
    {
        public string userId;
        public string nickname;
        public Choice choice;
    }

    void Start()
    {
        // 버튼 이벤트 연결
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetGame);

        // 초기 상태
        statusText.text = "호스트가 1(묵), 2(찌), 3(빠)를 선택하면 시작됩니다";

        // ChzzkOfficialClient 이벤트 연결
        ChzzkOfficialClient client = FindAnyObjectByType<ChzzkOfficialClient>();
        if (client != null)
        {
            client.onMessage.AddListener(OnChatMessageReceived);
        }
    }

    void Update()
    {
        // ✅ 게임이 비활성 상태에서 키보드 입력으로 시작
        if (!isGameActive)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                StartGameWithChoice(Choice.Rock);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                StartGameWithChoice(Choice.Scissors);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                StartGameWithChoice(Choice.Paper);
            }
        }

        // 타이머
        if (isGameActive)
        {
            currentTimer -= Time.deltaTime;
            timerText.text = $"남은 시간: {Mathf.Ceil(currentTimer)}초";

            if (currentTimer <= 0)
            {
                EndRound();
            }
        }
    }
    void StartGameWithChoice(Choice choice)
    {
        participants.Clear();
        isGameActive = true;
        currentTimer = roundTime;
        hostChoice = choice.ToString();

        string choiceKorean = GetChoiceKorean(choice);
        statusText.text = $"호스트가 선택했습니다!\n!묵, !찌, !빠 로 참여할 수 있습니다.";
        participantCountText.text = "참가자: 0명";
        startPanel.SetActive(false);
        Debug.Log($"게임 시작! 호스트 선택: {choiceKorean}");
    }

    // ? 채팅 메시지 수신
    void OnChatMessageReceived(ChzzkUnity.Profile profile, string message)
    {
        if (!isGameActive || string.IsNullOrEmpty(hostChoice))
            return;

        Choice playerChoice = Choice.None;

        // 메시지 파싱
        string msg = message.Trim().ToLower();
        if (msg == "!묵" || msg == "!주먹")
            playerChoice = Choice.Rock;
        else if (msg == "!찌")
            playerChoice = Choice.Scissors;
        else if (msg == "!빠")
            playerChoice = Choice.Paper;
        else
            return;

        // 참가자 추가/수정
        if (participants.ContainsKey(profile.userIdHash))
        {
            return;
        }
        else
        {
            participants[profile.userIdHash] = new PlayerChoice
            {
                userId = profile.userIdHash,
                nickname = profile.nickname,
                choice = playerChoice
            };
            Debug.Log($"? 참가: {profile.nickname} → {GetChoiceKorean(playerChoice)}");
        }
        logText.text = $"참가: {profile.nickname} → {GetChoiceKorean(playerChoice)}\n" + logText.text;
        participantCountText.text = $"참가자: {participants.Count}명";
    }

    // ? 라운드 종료
    void EndRound()
    {
        isGameActive = false;
        timerText.text = "시간 종료!";

        if (string.IsNullOrEmpty(hostChoice))
        {
            statusText.text = "호스트가 선택하지 않았습니다.";
            Debug.LogWarning("?? 호스트 미선택");
            return;
        }

        Choice host = ParseChoice(hostChoice);
        int winnerCount = 0;
        int loserCount = 0;

        Debug.Log("========== 결과 발표 ==========");

        foreach (var kvp in participants)
        {
            PlayerChoice player = kvp.Value;
            bool isWinner = CheckWinner(host, player.choice);

            if (isWinner)
            {
                winnerCount++;
                if (playerScores.ContainsKey(player.userId))
                {
                    playerScores[player.userId].score++;
                }
                else
                {
                    playerScores[player.userId] = new PlayerScore
                    {
                        channelId = player.userId,
                        nickname = player.nickname,
                        score = 1
                    };
                }
                Debug.Log($"? 승리: {player.nickname} ({GetChoiceKorean(player.choice)})");
            }
            else
            {
                loserCount++;
                Debug.Log($"? 탈락: {player.nickname} ({GetChoiceKorean(player.choice)})");
            }
        }

        Debug.Log("================================");
        Debug.Log($"승자: {winnerCount}명, 탈락자: {loserCount}명");

        statusText.text = $"결과: 승자 {winnerCount}명, 탈락자 {loserCount}명\n다음 라운드를 시작하려면 '시작하기'를 누르세요";

        participants.Clear();
        ShowResults();

    }
    void ShowResults()
    {
        Debug.Log("========== 최종 결과 ==========");
        logText.text = "";
        SwitchPanel(PanelName.Result);
        // 점수 순으로 정렬
        var sortedScores = playerScores.Values.OrderByDescending(p => p.score).ToList();

        foreach (var player in sortedScores)
        {
            Debug.Log($"🏆 {player.nickname}: {player.score}점");
        }

        Debug.Log("==============================");

        // ✅ UI에 결과 표시
        if (resultListParent != null && resultItemPrefab != null)
        {
            // 기존 결과 삭제
            foreach (Transform child in resultListParent)
            {
                Destroy(child.gameObject);
            }

            // 새 결과 생성
            foreach (var player in sortedScores)
            {
                GameObject item = Instantiate(resultItemPrefab, resultListParent);

                TMP_Text nameText = item.transform.Find("NameText")?.GetComponent<TMP_Text>();


                if (nameText != null)
                    nameText.text = $"{player.nickname} : {player.score}점";
            }
        }

        statusText.text = "게임 종료! 결과를 확인하세요.";
    }

    // 패널 전환
    void SwitchPanel(PanelName targetPanel)
    {
        foreach (var panel in panels)
        {
            panel.panel.gameObject.SetActive(panel.panelName == targetPanel);
        }
    }
    // ? 승패 판정
    bool CheckWinner(Choice host, Choice player)
    {
        if (host == player) return false; // 비김 = 패배

        if (host == Choice.Rock && player == Choice.Scissors) return false;
        if (host == Choice.Scissors && player == Choice.Paper) return false;
        if (host == Choice.Paper && player == Choice.Rock) return false;

        return true; // 이김
    }


    string GetChoiceKorean(Choice choice)
    {
        switch (choice)
        {
            case Choice.Rock: return "바위";
            case Choice.Scissors: return "가위";
            case Choice.Paper: return "보";
            default: return "없음";
        }
    }

    Choice ParseChoice(string choiceStr)
    {
        if (choiceStr == "Rock") return Choice.Rock;
        if (choiceStr == "Scissors") return Choice.Scissors;
        if (choiceStr == "Paper") return Choice.Paper;
        return Choice.None;
    }

    // ? 게임 초기화 (모든 탈락자 복구)
    public void ResetGame()
    {
        participants.Clear();
        playerScores.Clear();
        isGameActive = false;
        statusText.text = "게임이 초기화되었습니다";
        Debug.Log("?? 게임 초기화 완료");
    }
}