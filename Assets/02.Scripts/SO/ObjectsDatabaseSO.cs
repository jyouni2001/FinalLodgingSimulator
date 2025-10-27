using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// 건축물들의 데이터를 저장하는 스크립터블오브젝트 
/// </summary>
[CreateAssetMenu]
public class ObjectsDatabaseSO : ScriptableObject
{
    public List<ObjectData> objectsData;
    //최적화 시도
    private Dictionary<int, ObjectData> objectDataDict;
    
    public void InitializeDictionary()
    {
        if (objectDataDict != null) return;

        objectDataDict = new Dictionary<int, ObjectData>();
        foreach (var data in objectsData)
        {
            objectDataDict[data.ID] = data;
        }
    }
    
    public ObjectData GetObjectData(int id)
    {
        if (objectDataDict == null)
        {
            Debug.Log("ObjectsDatabaseSO: 딕셔너리가 초기화되지 않아 지금 초기화합니다.");
            InitializeDictionary();
        }

        return objectDataDict.TryGetValue(id, out var data) ? data : null;
    }
}

[Serializable]
public class ObjectData
{
    [field : SerializeField]
    public string Name { get;  set; }

    [field: SerializeField]
    public string Name_en { get; set; } // 영어 이름

    [field : SerializeField]
    public int ID { get;  set; }

    [field : SerializeField]
    public int kindIndex { get;  set; }

    [field: SerializeField]
    public bool IsWall; 

    [field: SerializeField] 
    public Vector2Int Size { get;  set; } = Vector2Int.one;
    
    [field : SerializeField]
    public GameObject Prefab { get;  set; }

    [field : SerializeField]
    public int BuildPrice { get;  set; }

    [field: SerializeField]
    public int BasePrice { get;  set; }
    
    [field: SerializeField]
    public int ReputationValue { get; set; }

    [field: SerializeField, TextArea(3, 5)]
    public string Description { get; set; }

    [field: SerializeField, TextArea(3, 5)]
    public string Description_en { get; set; } // 영어 설명

    public string LocalizedName
    {
        get
        {
            if (LocalizationSettings.SelectedLocale.Identifier.Code == "en")
            {
                return Name_en;
            }
            return Name;
        }
    }

    public string LocalizedDescription
    {
        get
        {
            if (LocalizationSettings.SelectedLocale.Identifier.Code == "en")
            {
                return Description_en;
            }
            return Description;
        }
    }
}
