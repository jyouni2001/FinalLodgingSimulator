using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("툴팁에 표시할 데이터")]
    [SerializeField] private int objectID; // 이 버튼이 어떤 건물을 나타내는지 ID로 연결
    [SerializeField] private ObjectsDatabaseSO database; // 건물 데이터베이스 참조

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (database == null) return;

        ObjectData data = database.GetObjectData(objectID);
        if (data != null)
        {
            // 툴팁에 표시할 내용을 조합
            string content = $"<b>{data.LocalizedName}</b>\n";
            string content2 = $"{data.LocalizedDescription}";
            TooltipManager.instance.ShowTooltip(content, content2);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.instance.HideTooltip();
        Debug.Log("꺼지는중");
    }
}