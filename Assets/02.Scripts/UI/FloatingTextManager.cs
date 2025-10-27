using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("UI 설정")]
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private Transform canvasTransform; // UI가 생성될 캔버스

    [Header("오브젝트 풀 설정")]
    [SerializeField] private int poolSize = 10;
    private Queue<FloatingText> floatingTextPool = new Queue<FloatingText>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(floatingTextPrefab, canvasTransform);
            obj.SetActive(false);
            floatingTextPool.Enqueue(obj.GetComponent<FloatingText>());
        }
    }

    public void Show(string message, Color color, Vector3 position)
    {
        FloatingText floatingText = GetPooledObject();
        floatingText.transform.position = position; // UI 요소의 월드 좌표 설정
        floatingText.gameObject.SetActive(true);
        floatingText.Show(message, color);
    }

    private FloatingText GetPooledObject()
    {
        if (floatingTextPool.Count > 0)
        {
            FloatingText obj = floatingTextPool.Dequeue();
            // 사용 후 다시 큐에 넣기 위해 Dequeue 후 Enqueue
            floatingTextPool.Enqueue(obj);
            return obj;
        }
        else
        {
            // 풀이 비어있으면 새로 생성 (확장성)
            GameObject obj = Instantiate(floatingTextPrefab, canvasTransform);
            FloatingText newFloatingText = obj.GetComponent<FloatingText>();
            floatingTextPool.Enqueue(newFloatingText);
            return newFloatingText;
        }
    }
}