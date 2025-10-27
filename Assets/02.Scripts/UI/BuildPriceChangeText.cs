using System.Text;
using TMPro;
using UnityEngine;

public class BuildPriceChangeText : MonoBehaviour
{
    [Header("가격 변경할 데이터")]
    [SerializeField] private int objectID; // 이 버튼이 어떤 건물을 나타내는지 ID로 연결
    [SerializeField] private ObjectsDatabaseSO database; // 건물 데이터베이스 참조
    [SerializeField] private TextMeshProUGUI priceText;

    private StringBuilder sb;

    void Start()
    {
       sb = new StringBuilder();
       priceText = GetComponent<TextMeshProUGUI>();
       ChangeText();
    }

    void ChangeText()
    {
        if (database == null) return;

        ObjectData data = database.GetObjectData(objectID);
        if (data != null)
        {
            sb.Clear();
            sb.Append(data.BuildPrice);
            priceText.text = sb.ToString();
        }
        else
        {
            Debug.LogError("data가 없습니다."); 
        }
    }
}
