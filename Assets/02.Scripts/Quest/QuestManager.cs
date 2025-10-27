using JY; // 기존 TimeSystem, PlayerWallet 등을 사용하기 위해 추가
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZLinq; // Linq 사용을 위해 추가

public class ActiveQuest
{
    public QuestData data;
    public int currentAmount;
    public float remainingTime;
    public bool isCompleted;
    public int startingMoney;

    public ActiveQuest(QuestData questData)
    {
        data = questData;
        currentAmount = 0; // 초기 진행도는 0
        remainingTime = questData.timeLimitInMinutes;
        isCompleted = false;

        // 돈 벌기 퀘스트라면, 수락 시점의 돈을 기록
        if (data.completionType == QuestCompletionType.EarnMoney)
        {
            startingMoney = PlayerWallet.Instance.money;
        }
    }
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public List<QuestData>   allQuests; // 모든 퀘스트 데이터 리스트
    public List<QuestData>   availableQuests = new List<QuestData>(); // 현재 발생 가능한 퀘스트 리스트    
    public List<ActiveQuest> activeQuests    = new List<ActiveQuest>(); // 진행 중인 퀘스트 목록
    public QuestUIManager    questUIManager; // UI 매니저 참조
    public QuestLogUI        questLogUI;
    public Queue<QuestData>  pendingQuests   = new Queue<QuestData>(); // 퀘스트 대기열

    public QuestData CurrentQuest { get; private set; } // 현재 플레이어에게 제시된 퀘스트
    private bool      canShowQuest = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 시작 시 모든 퀘스트를 발생 가능 리스트에 추가
        availableQuests.AddRange(allQuests);
        // 시간 변경 이벤트를 구독하여 퀘스트 발생을 체크
        TimeSystem.Instance.OnHourChanged += CheckForAvailableQuests;
        TimeSystem.Instance.OnMinuteChanged += CheckForAvailableQuests;
        // 이벤트 구독
        PlayerWallet.Instance.OnMoneyChanged += OnMoneyChanged;

        canShowQuest = true;
    }
    private void Update()
    {
        // 매 프레임마다 진행 중인 퀘스트의 완료 조건을 확인
        CheckQuestCompletion();

        if (canShowQuest)
        {
            TryShowNextQuest();
        }
    }

    private void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        if (TimeSystem.Instance != null)
        {
            TimeSystem.Instance.OnHourChanged -= CheckForAvailableQuests;
            TimeSystem.Instance.OnMinuteChanged -= CheckForAvailableQuests;
        }
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.OnMoneyChanged -= OnMoneyChanged;       

    }

    /// <summary>
    /// 돈이 변경될 때 호출될 함수
    /// </summary>
    /// <param name="newMoneyAmount"></param>
    private void OnMoneyChanged(int newMoneyAmount)
    {
        CheckQuestCompletion(); // 돈이 바뀔 때마다 모든 퀘스트의 완료 여부 확인
    }

    /// <summary>
    /// 매 시간마다 발생 가능한 퀘스트가 있는지 확인
    /// </summary>
    /// <param name="hour"></param>
    /// <param name="minute"></param>
    private void CheckForAvailableQuests(int hour, int minute)
    {
        int currentDay = TimeSystem.Instance.CurrentDay;

        // 현재 시간까지 발생했어야 하는 모든 퀘스트를 찾습니다. (ToList()로 복사하여 순회 중 변경 문제 방지)
        List<QuestData> questsToTrigger = availableQuests.AsValueEnumerable().Where(q =>
            (q.requiredDay < currentDay) ||
            (q.requiredDay == currentDay && q.requiredHour <= hour && q.requiredMin <= minute)
        ).ToList();

        foreach (var quest in questsToTrigger)
        {
            // 대기열에 추가하고, 이제 확인이 필요 없는 availableQuests에서는 제거합니다.
            if (!pendingQuests.Contains(quest))
            {
                pendingQuests.Enqueue(quest);
                Debug.Log($"퀘스트 '{quest.questName}'가 대기열에 추가되었습니다.");
            }
            availableQuests.Remove(quest);
        }

        // 대기열에 있는 다음 퀘스트를 표시 시도합니다.
        //TryShowNextQuest();
    }

    /// <summary>
    /// 대기열에서 다음 퀘스트를 보여주는 함수
    /// </summary>
    private void TryShowNextQuest()
    {
        // 현재 다른 퀘스트가 표시 중이거나, 대기열이 비어있으면 아무것도 하지 않습니다.
        if (CurrentQuest != null || questUIManager.IsVisible() || pendingQuests.Count == 0)
        {
            return;
        }

        canShowQuest = false;

        // 대기열에서 다음 퀘스트를 꺼내 화면에 표시합니다.
        CurrentQuest = pendingQuests.Dequeue();
        questUIManager.ShowQuest(CurrentQuest);
        Debug.Log($"대기열에서 '{CurrentQuest.questName}' 퀘스트를 표시합니다.");
    }

    /// <summary>
    /// 퀘스트 수락
    /// </summary>
    public void AcceptQuest()
    {
        if (CurrentQuest == null) return;
        Debug.Log($"퀘스트 '{CurrentQuest.questName}' 수락됨");
        // 여기에 현재 진행중인 퀘스트 목록에 추가하고, 완료 조건을 체크하는 로직을 추가합니다.
        ActiveQuest newActiveQuest = new ActiveQuest(CurrentQuest);
        activeQuests.Add(newActiveQuest);
        questLogUI.AddQuestToList(newActiveQuest); // UI 로그에 퀘스트 추가

        Debug.Log($"퀘스트 '{CurrentQuest.questName}' 수락됨");
        CurrentQuest = null;

        // UI를 숨기고, 애니메이션이 끝나면 대기열의 다음 퀘스트를 확인합니다.
        questUIManager.HideQuest(true, () => {
            canShowQuest = true;
            //TryShowNextQuest();
        });
    }


    /// <summary>
    /// 퀘스트 완료 조건 확인  
    /// </summary>
    private void CheckQuestCompletion()
    {
        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            // isCompleted가 true로 바뀌는 순간에만 UI를 업데이트하기 위한 플래그
            bool justCompleted = false;
            var quest = activeQuests[i];

            if (quest.isCompleted) continue;

            // 이전에 기록된 진행도를 저장
            int previousAmount = quest.currentAmount;
            bool conditionMet = false;
            switch (quest.data.completionType)
            {
                //튜토리얼 퀘스트
                case QuestCompletionType.Tutorial:
                    conditionMet = true;
                    break;

                case QuestCompletionType.BuildObject:
                    int currentBuildCount = 0;
                    currentBuildCount = CheckBuildObject(quest, currentBuildCount);

                    quest.currentAmount = currentBuildCount; // UI 업데이트를 위해 현재 개수 저장

                    if (currentBuildCount >= quest.data.completionAmount)
                    {
                        conditionMet = true;
                    }
                    break;

                case QuestCompletionType.EarnMoney:
                    // 퀘스트 수락 후 번 돈 계산
                    int moneyEarned = PlayerWallet.Instance.money - quest.startingMoney;
                    quest.currentAmount = moneyEarned; // UI 표시를 위해 현재 진행도 업데이트
                    if (moneyEarned >= quest.data.completionAmount) conditionMet = true;
                    break;

                case QuestCompletionType.ReachReputation:
                    if (ReputationSystem.Instance.CurrentReputation >= quest.data.completionAmount)
                    {
                        conditionMet = true;
                    }
                    break;
            }

            // 진행도가 변경되었다면 UI 업데이트 호출
            if (quest.currentAmount != previousAmount)
            {
                questLogUI.UpdateQuestStatus(quest);
            }

            if (conditionMet && !quest.isCompleted)
            {
                quest.isCompleted = true;
                justCompleted = true;
            }

            // 방금 막 완료된 경우에만 UI를 한 번 더 업데이트해서 "완료 가능!"으로 바꿈
            if (justCompleted)
            {
                questLogUI.UpdateQuestStatus(quest);
            }
        }
    }

    private static int CheckBuildObject(ActiveQuest quest, int currentBuildCount)
    {
        if (PlacementSystem.Instance != null)
        {
            // 중복 계산을 방지하기 위해 이미 센 오브젝트의 인덱스를 저장합니다.
            HashSet<int> countedObjectIndices = new HashSet<int>();

            // 바닥, 가구, 벽, 장식 등 모든 종류의 그리드 데이터를 순회합니다.
            GridData[] allGridData = {
                        PlacementSystem.Instance.floorData,
                        PlacementSystem.Instance.furnitureData,
                        PlacementSystem.Instance.wallData,
                        PlacementSystem.Instance.decoData
                    };

            foreach (GridData gridData in allGridData)
            {
                if (gridData == null || gridData.placedObjects == null) continue;

                // gridData에 저장된 모든 오브젝트 정보를 확인합니다.
                foreach (var dataList in gridData.placedObjects.Values)
                {
                    foreach (var data in dataList)
                    {
                        // 퀘스트 목표 ID와 일치하고, 아직 센 적 없는 오브젝트라면 카운트합니다.
                        if (data.ID == quest.data.completionTargetID && !countedObjectIndices.Contains(data.PlacedObjectIndex))
                        {
                            currentBuildCount++;
                            countedObjectIndices.Add(data.PlacedObjectIndex); // 셌다고 표시
                        }
                    }
                }
            }
        }

        return currentBuildCount;
    }

    /// <summary>
    /// 퀘스트 완료 처리
    /// </summary>
    /// <param name="quest"></param>
    public void CompleteQuest(ActiveQuest quest)
    {
        // 보상 지급
        switch (quest.data.rewardType)
        {
            case QuestRewardType.Money:
                PlayerWallet.Instance.AddMoney(quest.data.rewardAmount);
                break;
            case QuestRewardType.Reputation:
                ReputationSystem.Instance.AddReputation(quest.data.rewardAmount, $"{quest.data.questName} 완료");
                break;
        }

        Debug.Log($"퀘스트 '{quest.data.questName}' 완료! 보상 획득.");

        // 퀘스트 목록에서 제거
        activeQuests.Remove(quest);
        questLogUI.RemoveQuestFromList(quest);
    }

    /// <summary>
    /// 퀘스트 거절
    /// </summary>
    public void DeclineQuest()
    {
        if (CurrentQuest == null) return;
        Debug.Log($"퀘스트 '{CurrentQuest.questName}' 거절됨");
        CurrentQuest = null;

        // UI를 숨기고, 애니메이션이 끝나면 대기열의 다음 퀘스트를 확인합니다.
        questUIManager.HideQuest(false, () => {
            canShowQuest = true;
            //TryShowNextQuest();
        });
    }

    public void LoadQuestData(List<QuestSaveData> activeQuestsData, List<string> pendingQuestNames, List<string> availableQuestNames, string currentQuestName)
    {
        // 1. 모든 퀘스트 목록 초기화
        activeQuests.Clear();
        pendingQuests.Clear();
        availableQuests.Clear();

        foreach (Transform child in questLogUI.questListContent)
        {
            Destroy(child.gameObject);
        }

        //questLogUI.questListContent.GetComponentsInChildren<Transform>().AsValueEnumerable().Where(t => t != questLogUI.questListContent).ToList().ForEach(t => Destroy(t.gameObject));


        // 2. 진행 중인 퀘스트(activeQuests) 복원
        foreach (var savedQuest in activeQuestsData)
        {
            QuestData questData = allQuests.AsValueEnumerable().FirstOrDefault(q => q.questName == savedQuest.questName);
            if (questData != null)
            {
                ActiveQuest activeQuest = new ActiveQuest(questData)
                {
                    currentAmount = savedQuest.currentAmount,
                    isCompleted = savedQuest.isCompleted,
                    startingMoney = savedQuest.startingMoney
                };
                activeQuests.Add(activeQuest);
                questLogUI.AddQuestToList(activeQuest); // UI에도 추가
            }
        }

        // 3. 대기 중인 퀘스트(pendingQuests) 복원
        foreach (var questName in pendingQuestNames)
        {
            QuestData questData = allQuests.AsValueEnumerable().FirstOrDefault(q => q.questName == questName);
            if (questData != null)
            {
                pendingQuests.Enqueue(questData);
            }
        }

        // 4. 발생 가능한 퀘스트(availableQuests) 복원
        foreach (var questName in availableQuestNames)
        {
            QuestData questData = allQuests.AsValueEnumerable().FirstOrDefault(q => q.questName == questName);
            if (questData != null)
            {
                availableQuests.Add(questData);
            }
        }

        // 5. 현재 제안 중이던 퀘스트 복원 (가장 중요!)
        if (!string.IsNullOrEmpty(currentQuestName))
        {
            QuestData currentQuestData = allQuests.FirstOrDefault(q => q.questName == currentQuestName);
            if (currentQuestData != null)
            {
                // 대기열의 가장 앞에 추가하여, 로드 후 바로 표시되도록 함
                var tempQueue = new Queue<QuestData>(pendingQuests);
                pendingQuests.Clear();
                pendingQuests.Enqueue(currentQuestData);
                while (tempQueue.Count > 0)
                {
                    pendingQuests.Enqueue(tempQueue.Dequeue());
                }
            }
        }

        canShowQuest = true; // 퀘스트를 보여줄 수 있도록 플래그를 true로 설정
        //TryShowNextQuest();

        Debug.Log("퀘스트 데이터 로드 완료");
    }
}