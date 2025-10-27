using DG.Tweening; // DOTween 사용
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class QuestUIManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject questPanel; // 퀘스트 UI 패널 전체
    public TextMeshProUGUI questDialogueText;
    public TextMeshProUGUI questConditionText;
    public RawImage characterImage;

    private Vector2 initialPosition;
    private Vector2 startDragPosition;
    private float swipeThreshold = 150f; // 스와이프 판정 거리
    private float maxDragDistance = 200f;

    private QuestData currentQuestData;

    private void Start()
    {
        initialPosition = questPanel.GetComponent<RectTransform>().anchoredPosition;
        questPanel.SetActive(false);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }
    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 이벤트 구독을 해제합니다. (메모리 누수 방지)
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }
    private void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
    {
        // 퀘스트 알림창이 활성화되어 있고, 표시할 퀘스트 데이터가 있을 때만 텍스트를 새로고침합니다.
        if (questPanel.activeSelf && currentQuestData != null)
        {
            RefreshQuestUI();
        }
    }


    // 퀘스트 UI를 화면에 표시
    public void ShowQuest(QuestData quest)
    {
        currentQuestData = quest; // 현재 퀘스트 데이터를 저장합니다.
        RefreshQuestUI(); // UI 내용을 채우는 역할을 RefreshQuestUI 함수에 맡깁니다.        

        //questDialogueText.text = quest.dialogue;
        //questConditionText.text = GetConditionString(quest);
        //characterImage.texture = quest.characterImage;

        questPanel.SetActive(true);
        RectTransform rect = questPanel.GetComponent<RectTransform>();

        // 화면 밖 왼쪽에서 시작
        rect.anchoredPosition = new Vector2(-rect.rect.width, initialPosition.y);

        // DOTween을 사용하여 슬라이드 인 애니메이션
        rect.DOAnchorPosX(initialPosition.x, 0.5f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // 퀘스트 UI를 화면 밖으로 사라지게 함
    public void HideQuest(bool accepted, System.Action onHideAnimationComplete)
    {
        RectTransform rect = questPanel.GetComponent<RectTransform>();
        float endPosX = accepted ? rect.rect.width * 2 : -rect.rect.width * 2; // 수락이면 오른쪽, 거절이면 왼쪽

        rect.DOAnchorPosX(endPosX, 0.5f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            questPanel.SetActive(false);
            rect.anchoredPosition = initialPosition; // 다음 퀘스트를 위해 위치 초기화

            currentQuestData = null;

            onHideAnimationComplete?.Invoke();
        });
    }

    // UI 텍스트를 현재 언어 설정에 맞게 새로고침하는 함수
    private void RefreshQuestUI()
    {
        if (currentQuestData == null) return;

        if (LocalizationSettings.SelectedLocale.Identifier.Code == "en")
        {
            questDialogueText.text = currentQuestData.dialogue_en;
        }
        else
        {
            questDialogueText.text = currentQuestData.dialogue;
        }

        questConditionText.text = GetConditionString(currentQuestData);
        characterImage.texture = currentQuestData.characterImage;
    }

    public string GetConditionString(QuestData quest)
    {
        string condition = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? "Condition: " : "조건: ";
        switch (quest.completionType)
        {
            case QuestCompletionType.Tutorial:
                return (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? "Condition: Pull to accept." : "조건: 당겨서 수락하세요.";
            case QuestCompletionType.BuildObject:
                string objectName = PlacementSystem.Instance.database.GetObjectData(quest.completionTargetID).LocalizedName;
                return condition + $"{objectName} " + ((LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Build {quest.completionAmount}" : $"{quest.completionAmount}개 건설");
            case QuestCompletionType.EarnMoney:
                return condition + ((LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Earn {quest.completionAmount} G" : $"{quest.completionAmount}원 벌기");
            case QuestCompletionType.ReachReputation:
                return condition + ((LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Reach {quest.completionAmount} reputation" : $"평판 {quest.completionAmount}점 달성");
            default:
                return "";
        }
    }

    /*// 완료 조건 텍스트 생성
    public string GetConditionString(QuestData quest)
    {
        switch (quest.completionType)
        {
            case QuestCompletionType.Tutorial:
                return $"조건 : 당겨서 수락하세요.";
            case QuestCompletionType.BuildObject:
                // ObjectDatabaseSO를 참조하여 오브젝트 이름을 가져옵니다.
                string objectName = PlacementSystem.Instance.database.GetObjectData(quest.completionTargetID).Name;
                return $"조건: {objectName} {quest.completionAmount}개 건설";
            case QuestCompletionType.EarnMoney:
                return $"조건: {quest.completionAmount}원 벌기";
            case QuestCompletionType.ReachReputation:
                return $"조건: 평판 {quest.completionAmount}점 달성";
            default:
                return "";
        }
    }*/

    public bool IsVisible()
    {
        return questPanel.activeSelf;
    }

    // --- 스와이프 처리 ---
    public void OnBeginDrag(PointerEventData eventData)
    {
        startDragPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 드래그 중 패널을 따라 움직이게 함
        float difference = eventData.position.x - startDragPosition.x;

        // 드래그 최대치 지정
        float clampedDifference = Mathf.Clamp(difference, -maxDragDistance, maxDragDistance);
        questPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialPosition.x + clampedDifference, initialPosition.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float swipeDistance = eventData.position.x - startDragPosition.x;

        if (swipeDistance > swipeThreshold) // 오른쪽 스와이프 (수락)
        {
            QuestManager.Instance.AcceptQuest();
        }
        else if (swipeDistance < -swipeThreshold) // 왼쪽 스와이프 (거절)
        {
            QuestManager.Instance.DeclineQuest();
        }
        else // 원래 위치로 복귀
        {
            questPanel.GetComponent<RectTransform>().DOAnchorPos(initialPosition, 0.2f).SetUpdate(true);
        }
    }
}