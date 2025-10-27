using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class QuestLogUI : MonoBehaviour
{
    public Transform questListContent; // 퀘스트 아이템이 추가될 Content 오브젝트
    public GameObject questLogItemPrefab; // 개별 퀘스트 UI 프리팹

    private Dictionary<ActiveQuest, GameObject> questItemObjects = new Dictionary<ActiveQuest, GameObject>();

    private void Start()
    {
        // 언어 변경 이벤트를 구독합니다.
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 이벤트 구독을 해제합니다.
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }
    private void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
    {
        // 퀘스트 목록에 있는 모든 아이템의 UI를 새로고침합니다.
        foreach (var questItemGO in questItemObjects.Values)
        {
            if (questItemGO != null)
            {
                questItemGO.GetComponent<QuestLogItem>().Refresh();
            }
        }
    }

    public void AddQuestToList(ActiveQuest quest)
    {
        GameObject itemGO = Instantiate(questLogItemPrefab, questListContent);
        QuestLogItem itemUI = itemGO.GetComponent<QuestLogItem>();
        itemUI.Setup(quest);

        questItemObjects.Add(quest, itemGO);
    }

    public void RemoveQuestFromList(ActiveQuest quest)
    {
        if (questItemObjects.TryGetValue(quest, out GameObject itemGO))
        {
            Destroy(itemGO);
            questItemObjects.Remove(quest);
        }
    }

    public void UpdateQuestStatus(ActiveQuest quest)
    {
        if (questItemObjects.TryGetValue(quest, out GameObject itemGO))
        {
            itemGO.GetComponent<QuestLogItem>().UpdateStatus();
        }
    }
}