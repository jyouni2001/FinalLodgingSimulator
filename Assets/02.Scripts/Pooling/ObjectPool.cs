using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    // 프리팹 ID를 키로 사용하여 각 오브젝트 풀을 관리하는 딕셔너리
    private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();
    // 모든 풀의 부모가 될 트랜스폼
    private Transform poolContainer;

    /// <summary>
    /// 지정된 프리팹으로 오브젝트 풀을 생성합니다.
    /// </summary>
    /// <param name="prefab">풀링할 프리팹</param>
    /// <param name="poolSize">초기 풀 크기</param>
    public void CreatePool(GameObject prefab, int poolSize)
    {
        int prefabId = prefab.GetInstanceID();

        if (poolDictionary.ContainsKey(prefabId))
        {
            return; // 이미 풀이 존재하면 생성하지 않음
        }

        // 풀 컨테이너가 없으면 생성
        if (poolContainer == null)
        {
            poolContainer = new GameObject("ObjectPools").transform;
        }

        // 개별 프리팹을 담을 컨테이너 생성
        GameObject container = new GameObject(prefab.name + " Pool");
        container.transform.SetParent(poolContainer);

        poolDictionary[prefabId] = new Queue<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab, container.transform);
            obj.SetActive(false);
            poolDictionary[prefabId].Enqueue(obj);
        }
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져옵니다.
    /// </summary>
    /// <param name="prefab">가져올 프리팹</param>
    /// <param name="position">오브젝트 위치</param>
    /// <param name="rotation">오브젝트 회전</param>
    /// <returns>활성화된 오브젝트</returns>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int prefabId = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(prefabId))
        {
            // 풀이 없으면 동적으로 생성
            CreatePool(prefab, 100); // 기본 사이즈 5로 생성
        }

        // 풀에 사용 가능한 오브젝트가 없으면 새로 생성 (확장성)
        if (poolDictionary[prefabId].Count == 0)
        {
            Transform container = poolContainer.Find(prefab.name + " Pool");
            GameObject newObj = Instantiate(prefab, container);
            poolDictionary[prefabId].Enqueue(newObj);
        }

        GameObject obj = poolDictionary[prefabId].Dequeue();
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    /// <summary>
    /// 오브젝트를 풀로 반환합니다.
    /// </summary>
    /// <param name="prefab">반환할 오브젝트의 원본 프리팹</param>
    /// <param name="obj">반환할 오브젝트</param>
    public void Return(GameObject prefab, GameObject obj)
    {
        int prefabId = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(prefabId))
        {
            Debug.LogWarning($"풀에 '{prefab.name}'이(가) 존재하지 않습니다.");
            Destroy(obj); // 풀이 없으면 그냥 파괴
            return;
        }

        obj.SetActive(false);
        poolDictionary[prefabId].Enqueue(obj);
    }
}