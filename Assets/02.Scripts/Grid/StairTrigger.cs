using UnityEngine;

public class StairTrigger : MonoBehaviour
{
    [Tooltip("층 이동 트리거로 사용될 콜라이더")]
    [SerializeField] private Collider triggerCollider;

    private void Awake()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- 수정된 부분 시작 ---
        Debug.Log("*****************계단에 무언가가 닿음*****************");
        // 계단이 놓인 층과 충돌한 오브젝트의 현재 층을 가져옵니다.
        int stairFloor = GetFloorFromLayer(gameObject.layer);
        int objectFloor = GetFloorFromLayer(other.gameObject.layer);

        Debug.Log($"stairFloor = {stairFloor}, objectFloor = {objectFloor}");

        // 계단 또는 충돌한 오브젝트가 유효한 층 레이어("1F", "2F" 등)가 아니면 함수를 종료합니다.
        if (stairFloor == -1 || objectFloor == -1)
        {
            Debug.Log("*******************************************대체 뭐고");
            return;
        }

        // --- 수정된 부분 끝 ---

        int destinationFloor;

        // 캐릭터와 계단이 같은 층에 있으면 -> 올라가는 상황
        if (objectFloor == stairFloor)
        {
            destinationFloor = stairFloor + 1;
            Debug.Log("*******************************************여기서 바뀜");
        }
        // 캐릭터가 계단보다 한 층 위에 있으면 -> 내려가는 상황
        else if (objectFloor == stairFloor + 1)
        {
            destinationFloor = stairFloor;
            Debug.Log("*******************************************여기서도 바뀜");
        }
        else
        {
            // 예외 상황 (예: 2층 계단에 5층 캐릭터가 닿는 경우), 무시
            Debug.Log("*******************************************예외상황");
            return;
        }

        // 레이어 변경
        ChangeLayerOfAllChildren(other.gameObject, destinationFloor);
    }

    /// <summary>
    /// 대상 오브젝트와 모든 자식의 레이어를 변경합니다.
    /// </summary>
    private void ChangeLayerOfAllChildren(GameObject target, int floor)
    {
        if (floor < 1 || floor > 6) return;

        string layerName = $"{floor}F";
        int newLayer = LayerMask.NameToLayer(layerName);

        if (newLayer == -1)
        {
            Debug.LogError($"[StairTrigger] 레이어를 찾을 수 없습니다: {layerName}");
            return;
        }

        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = newLayer;
        }
        target.layer = newLayer;

        Debug.Log($"[StairTrigger] '{target.name}'의 레이어를 '{layerName}'(으)로 변경했습니다.");
    }

    /// <summary>
    /// 레이어 이름("1F", "2F" 등)에서 층 번호를 숫자로 추출합니다.
    /// </summary>
    private int GetFloorFromLayer(int layer)
    {
        string layerName = LayerMask.LayerToName(layer);
        if (layerName.EndsWith("F") && int.TryParse(layerName.Substring(0, layerName.Length - 1), out int floor))
        {
            return floor;
        }

        if(layerName.Equals("Employee") || layerName.Equals("Customer"))
        {
            return 1;
        }
        return -1; // 유효하지 않은 층
    }
}