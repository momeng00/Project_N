using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RSP_Canvas : MonoBehaviour
{
    [Header("연결")]
    public ChzzkOfficialClient officialClient;

    [Header("UI - 버튼")]
    public Button startGameButton;
    public Text gameStatusText;
    public Text timerText;

    [Header("UI - 참가자 목록")]
    public Transform participantListParent;
    public GameObject participantItemPrefab;

    [Header("설정")]
    public float gameTime = 30f;

    // 게임 상태
    private bool isGameActive = false;
    private string hostChoice = ""; // "묵", "찌", "빠"
    private float remainingTime = 0f;

    // 참가자 데이터
    private Dictionary<string, ParticipantData> participants = new Dictionary<string, ParticipantData>();
    private Dictionary<string, GameObject> participantUIItems = new Dictionary<string, GameObject>();

    [System.Serializable]
    private class ParticipantData
    {
        public string userId;
        public string nickname;
        public string choice; // "묵", "찌", "빠", ""
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    //이벤트에 넣기위해 사용되는 메서드
    public void AddFunction()
    {

    }
    //이벤트에 빼기위해 사용되는 메서드
    public void RemoveFunction() 
    {
        
    }
    public void CheckRSPData()
    {

    }
}
