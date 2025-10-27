using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [SerializeField] private ObjectsDatabaseSO database; // 모든 건축 정보가 담긴 SO
    [SerializeField] private int defaultPoolSize = 100;  // 기본 풀 크기

    private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();
    private Dictionary<int, GameObject> prefabDictionary = new Dictionary<int, GameObject>();
    private Transform poolContainer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 데이터베이스의 모든 프리팹을 미리 풀링합니다.
    /// </summary>
    private void InitializePools()
    {
        poolContainer = new GameObject("ObjectPools").transform;
        DontDestroyOnLoad(poolContainer.gameObject);

        foreach (var data in database.objectsData)
        {
            if (data.Prefab == null) continue;

            int prefabId = data.Prefab.GetInstanceID();
            prefabDictionary[prefabId] = data.Prefab;

            if (poolDictionary.ContainsKey(prefabId)) continue;

            GameObject container = new GameObject(data.Prefab.name + " Pool");
            container.transform.SetParent(poolContainer);

            poolDictionary[prefabId] = new Queue<GameObject>();

            for (int i = 0; i < defaultPoolSize; i++)
            {
                GameObject obj = Instantiate(data.Prefab, container.transform);
                obj.AddComponent<PoolableObject>().prefabId = prefabId; // 풀링 정보 저장
                obj.SetActive(false);
                poolDictionary[prefabId].Enqueue(obj);
            }
        }
        Debug.Log($"오브젝트 풀 초기화 완료: {database.objectsData.Count} 종류의 프리팹, 각 {defaultPoolSize}개씩 생성.");
    }

    /// <summary>
    /// 풀에서 오브젝트를 가져옵니다.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int prefabId = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(prefabId) || poolDictionary[prefabId].Count == 0)
        {
            Debug.LogWarning($"'{prefab.name}' 풀이 비어있거나 존재하지 않습니다. 동적으로 확장합니다.");
            // 비상 시 동적 생성
            GameObject container = poolContainer.Find(prefab.name + " Pool")?.gameObject ?? new GameObject(prefab.name + " Pool");
            GameObject newObj = Instantiate(prefab, container.transform);
            newObj.AddComponent<PoolableObject>().prefabId = prefabId;
            return newObj;
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
    public void Return(GameObject obj)
    {
        PoolableObject poolable = obj.GetComponent<PoolableObject>();
        if (poolable == null)
        {
            Debug.LogError($"'{obj.name}'에 PoolableObject 컴포넌트가 없습니다. 파괴합니다.");
            Destroy(obj);
            return;
        }

        int prefabId = poolable.prefabId;
        if (poolDictionary.ContainsKey(prefabId))
        {
            obj.SetActive(false);
            poolDictionary[prefabId].Enqueue(obj);
        }
        else
        {
            Debug.LogWarning($"'{obj.name}'에 해당하는 풀이 없습니다. 파괴합니다.");
            Destroy(obj);
        }
    }
}

// 오브젝트에 부착하여 원본 프리팹 정보를 저장하는 도우미 클래스
public class PoolableObject : MonoBehaviour
{
    public int prefabId;
}