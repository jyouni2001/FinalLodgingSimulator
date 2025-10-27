using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    //[Header("상호작용 시스템")]
    //[SerializeField] private ObjectInteractionUI interactionUI;

    [Header("컴포넌트")]
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private Camera cam;
    [SerializeField] private Grid grid;
    [SerializeField] private ChangeFloorSystem changeFloorSystem;
    
    private Vector3 lastPosition;   // 마지막 좌표 변수
    private Vector3 uiShowPosition; // BuildUI가 보이는 위치
    private Vector3 uiHidePosition; // BuildUI가 숨겨진 위치
    private Tween   uiTween; // 현재 실행 중인 트윈 저장


    private Vector3 uiShowPosition2; // buildModeUI가 보이는 위치
    private Vector3 uiHidePosition2; // buildModeUI가 숨겨진 위치

    [Header("UI 참조")]
    [Tooltip("건축/삭제 모드 토글 UI")]
    [SerializeField] private BuildModeToggle buildModeToggle;

    public GameObject buildModeUI;

    [Header("변수")]
    
    [SerializeField] private LayerMask placementLayermask;
    [SerializeField] private LayerMask batchedLayer;
    [SerializeField] private LayerMask objectLayer;
    public event Action OnClicked, OnExit;

    public GameObject BuildUI;
    public GameObject SettingUI;
    public GameObject SettingUI2;
    public GameObject targetObject;
    public GameObject QuestUI;    
    public GameObject HiringUI;
    public GameObject StatisticUI;
    public Button SettingBtn;
    public GameObject PreExitUI;

    [Header("언어별 설정 이미지")]
    public GameObject settingImageKO; // 한국어 이미지
    public GameObject settingImageEN; // 영어 이미지


    public RaycastHit   hit;
    public RaycastHit   hit2; 
    public bool         isBuildMode = false;
    public bool isDeleteMode = false;
    public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    [Header("DOTween Settings")]
    public float animationDuration = 0.3f; // 애니메이션 지속 시간
    public Ease openEase = Ease.OutBack;    // 열릴 때 적용할 Ease
    public Ease closeEase = Ease.InBack;     // 닫힐 때 적용할 Ease

    private void Start()
    {
        InitialBuildUI();
        SettingBtn.onClick.AddListener(OnOffSettingUI);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }
    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
    {
        // 설정 UI가 활성화 상태일 때만 이미지를 업데이트합니다.
        if (SettingUI.activeSelf)
        {
            UpdateSettingImages();
        }
    }

    private void Update()
    {
        // B키로 건설모드 시작
        if (Input.GetKeyDown(KeyCode.B)/* && !IsPointerOverUI()*/)
        {
            ChangeBuildMode();
            
        }

        // Q키로 퀘스트 UI 출력
        if (Input.GetKeyDown(KeyCode.Q)) QuestUI.SetActive(!QuestUI.activeSelf);

        // ESC키로 설정 UI 출력
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            OnOffSettingUI();
        }

        // 마우스 우클릭으로 건설 중지
        if (Input.GetMouseButtonDown(1) && isBuildMode && !IsPointerOverUI())
        {
            OnExit?.Invoke();
        }

        // 마우스 좌클릭으로 건설
        if (Input.GetMouseButtonDown(0) && isDeleteMode)
        {
            OnClicked?.Invoke();
        }

        // ESC 키로 건설 상태 종료
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isBuildMode)
            {
                isBuildMode = false;

                //placementSystem.ExitBuildMode();

                StartCoroutine(HideBuildUI());
            }
            OnExit?.Invoke();
        }

        // 삭제모드 시작
        if (Input.GetKeyDown(KeyCode.L) && isBuildMode)
        {
            if (buildModeToggle != null)
            {
                // BuildModeToggle UI에 토글 명령
                buildModeToggle.ToggleMode();
            }
            else
            {
                Debug.LogWarning("BuildModeToggle이 InputManager에 연결되지 않았습니다!");
            }

            //ChangeDeleteMode();
        }

        // 일반 모드에서 오브젝트 클릭 처리
        if (Input.GetMouseButtonDown(0) && !isBuildMode && !isDeleteMode && !IsPointerOverUI())
        {
            HandleObjectSelection();
        }

        if(Input.GetKeyDown(KeyCode.H))
        {
            HiringUI.gameObject.SetActive(!HiringUI.activeSelf);
        }
    }

    private void ChangeBuildMode()
    {
        isBuildMode = !isBuildMode;
        if (isBuildMode)
        {
            ShowBuildUI(); // BuildUI 애니메이션 실행
        }
        else
        {
            StartCoroutine(HideBuildUI());
            //HideBuildUI(); // BuildUI 애니메이션 실행
            //placementSystem.ExitBuildMode();
            OnExit?.Invoke();
        }

        ChangeFloorForBuildMode();
    }

    private void OnOffSettingUI()
    {
        if (StatisticUI.activeSelf) StatisticUI.SetActive(false);
        if (QuestUI.activeSelf) QuestUI.SetActive(false);
        if (HiringUI.activeSelf) HiringUI.SetActive(false);
        if (PreExitUI.activeSelf) PreExitUI.SetActive(false);

        // UI가 비활성화 상태일 때 -> 열기
        if (!SettingUI.activeSelf)
        {
            // SettingUI2가 켜져 있다면 먼저 끈다.
            if (SettingUI2 != null && SettingUI2.activeSelf)
            {
                SettingUI2.SetActive(false);
            }

            // UI를 활성화하고, 시작 크기를 설정한 후 애니메이션 실행
            SettingUI.SetActive(true);
            SettingUI.transform.localScale = Vector3.one * 0.1f;
            SettingUI.transform.DOScale(1f, animationDuration).SetEase(openEase).SetUpdate(true);

            UpdateSettingImages();
        }
        // UI가 활성화 상태일 때 -> 닫기
        else
        {
            // 크기 애니메이션을 먼저 실행하고, 애니메이션이 끝나면 비활성화
            SettingUI.transform.DOScale(0.1f, animationDuration)
                .SetEase(closeEase).SetUpdate(true)
                .OnComplete(() =>
                {
                    SettingUI.SetActive(false);
                    SettingUI2.SetActive(false);
                });
        }
    }

    private void UpdateSettingImages()
    {
        if (settingImageKO == null || settingImageEN == null) return;

        // 현재 선택된 언어의 코드를 확인합니다.
        if (LocalizationSettings.SelectedLocale.Identifier.Code == "ko-KR")
        {
            settingImageKO.SetActive(true);
            settingImageEN.SetActive(false);
        }
        else // 한국어가 아니면 영어 이미지를 보여줍니다.
        {
            settingImageKO.SetActive(false);
            settingImageEN.SetActive(true);
        }
    }

    private void HandleObjectSelection()
    {        
        GameObject clickedObject = GetClickedObject();
        
        if (clickedObject != null)
        {
            Debug.Log($"{clickedObject.name}을 클릭");

            // ObjectPlacer null 체크 추가
            ObjectPlacer objectPlacer = FindFirstObjectByType<ObjectPlacer>();
            if (objectPlacer == null)
            {
                Debug.LogError("ObjectPlacer를 찾을 수 없습니다!");
                return;
            }

            // 설치된 오브젝트인지 확인
            int objectIndex = objectPlacer.GetObjectIndex(clickedObject);
            Debug.Log($"{clickedObject.name}의 objectIndex는 {objectIndex}");
            if (objectIndex >= 0)
            {
                Vector3 objectPosition = clickedObject.transform.position;
                Debug.Log($"{clickedObject.name}는 {objectPosition}에 위치");

                // interactionUI null 체크 추가
                /*if (interactionUI != null)
                {
                    interactionUI.ShowInteractionUI(clickedObject, objectPosition);
                }
                else
                {
                    Debug.LogError("InteractionUI가 할당되지 않았습니다! Inspector에서 할당해주세요.");
                }*/
            }
        }
        else
        {
            // 빈 공간 클릭 시 UI 숨김
            //if (interactionUI != null && interactionUI.IsUIActive()) interactionUI.HideInteractionUI();
            
            //if(clickedObject != null)  interactionUI.HideInteractionUI();
        }
    }

    private void InitialBuildUI()
    {
        // BuildUI의 초기 위치 설정
        if (BuildUI is not null && buildModeUI is not null)
        {
            BuildUI.SetActive(true);
            buildModeUI.SetActive(true);

            // BuildUI의 RectTransform 사용
            RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
            if (uiRect is not null)
            {
                // 현재 위치를 보이는 위치로 설정
                uiShowPosition = uiRect.anchoredPosition;

                // 숨겨진 위치는 Y축을 아래로 이동 (화면 아래로)
                uiHidePosition = uiShowPosition + new Vector3(0, -Screen.height, 0); // 화면 높이만큼 아래로

                // 초기 상태: BuildUI 숨김
                uiRect.anchoredPosition = uiHidePosition;
                BuildUI.SetActive(false); // 비활성화 대신 위치로 제어
            }
            else
            {
                Debug.LogError("BuildUI에 RectTransform이 없습니다!");
            }

            RectTransform uiRect2 = buildModeUI.GetComponent<RectTransform>();
            if (uiRect2 is not null)
            {
                // 현재 위치를 보이는 위치로 설정
                uiShowPosition2 = uiRect2.anchoredPosition;

                // 숨겨진 위치는 Y축을 아래로 이동 (화면 아래로)
                uiHidePosition2 = uiShowPosition2 + new Vector3(0, -Screen.height, 0); // 화면 높이만큼 아래로

                // 초기 상태: BuildUI 숨김
                uiRect2.anchoredPosition = uiHidePosition2;
                buildModeUI.SetActive(false); // 비활성화 대신 위치로 제어
            }
            else
            {
                Debug.LogError("buildModeUI에 RectTransform이 없습니다!");
            }

            BuildUI.SetActive(false);
            buildModeUI.SetActive(false);
        }
        else
        {
            Debug.LogError("BuildUI 또는 buildModeUI 가 할당되지 않았습니다!");
        }

        if(SettingUI is not null) SettingUI.SetActive(false);
    }

    /// <summary>
    /// BuildUI를 위로 올리는 Dotween 애니메이션 코드
    /// </summary>
    private void ShowBuildUI()
    {
        if (BuildUI is null) return;
        if (buildModeUI is null) return;

        // 기존 트윈이 있으면 종료
        if (uiTween is not null)
        {
            uiTween.Kill();
        }

        RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
        if (uiRect is not null)
        {
            placementSystem.EnterBuildMode();
            // DOTween으로 Y축 이동 애니메이션
            uiTween = uiRect.DOAnchorPosY(uiShowPosition.y, 0.5f) // 0.5초 동안 이동
                .SetEase(Ease.OutQuad) // 부드러운 이징
                .SetUpdate(UpdateType.Normal, true) // 타임스케일 영향 X
                .OnComplete(() => uiTween = null); // 완료 시 트윈 변수 초기화
        }

        RectTransform uiRect2 = buildModeUI.GetComponent<RectTransform>();
        if (uiRect2 is not null)
        {
            // DOTween으로 Y축 이동 애니메이션
            uiTween = uiRect2.DOAnchorPosY(uiShowPosition2.y, 0.5f) // 0.5초 동안 이동
                .SetEase(Ease.OutQuad) // 부드러운 이징
                .SetUpdate(UpdateType.Normal, true) // 타임스케일 영향 X
                .OnComplete(() => uiTween = null); // 완료 시 트윈 변수 초기화
        }
    }

    /// <summary>
    /// BuildUI를 아래로 내리는 Dotween 애니메이션 코드
    /// </summary>
    private IEnumerator HideBuildUI()
    {
        if (BuildUI == null || buildModeUI == null) yield break;


        // buildModeToggle이 있다면 '건축' 모드로 리셋
        if (buildModeToggle != null)
        {
            buildModeToggle.ResetToBuildMode();
        }

        bool buildUI_AnimationFinished = false;
        bool buildModeUI_AnimationFinished = false;

        RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            uiRect.DOAnchorPosY(uiHidePosition.y, 0.5f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    BuildUI.SetActive(false); // 애니메이션 끝나면 비활성화
                    buildUI_AnimationFinished = true;
                });
        }
        else { buildUI_AnimationFinished = true; }

        RectTransform uiRect2 = buildModeUI.GetComponent<RectTransform>();
        if (uiRect2 != null)
        {
            uiRect2.DOAnchorPosY(uiHidePosition2.y, 0.5f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    buildModeUI.SetActive(false); // 애니메이션 끝나면 비활성화
                    buildModeUI_AnimationFinished = true;
                });
        }
        else { buildModeUI_AnimationFinished = true; }

        // 애니메이션이 완료될 때까지 대기
        yield return new WaitUntil(() => buildUI_AnimationFinished && buildModeUI_AnimationFinished);

        // 4. 모든 애니메이션이 끝난 후 건설 모드 종료
        if (placementSystem != null)
        {
            placementSystem.ExitBuildMode();
        }
    }

    /// <summary>
    /// 마우스를 통해 실시간으로 좌표를 반환한다.
    /// </summary>
    /// <returns></returns>
    public Vector3 GetSelectedMapPosition()
    {
        if (cam is null)
        {
            Debug.LogError("Camera가 할당되지 않았습니다!");
            return Vector3.zero;
        }
        
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.nearClipPlane;
        
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out hit, 90, placementLayermask))
        {
            lastPosition = hit.point;
        }
        
        return lastPosition;
    }

    public GameObject GetClickedObject()
    {
        if (cam is null)
        {
            Debug.LogError("Camera가 할당되지 않았습니다!");
            return null;
        }


        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, objectLayer);

        if (hits.Length == 0)
        {
            return null;
        }

        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        foreach (var hit in hits)
        {
            Transform currentTransform = hit.collider.transform;
            GameObject rootObject = null;

            // 최상위 부모 오브젝트(ObjectPlacer에 등록된 오브젝트)를 찾습니다.
            while (currentTransform != null)
            {
                if (ObjectPlacer.Instance.placedGameObjects.Contains(currentTransform.gameObject))
                {
                    rootObject = currentTransform.gameObject;
                    break;
                }
                currentTransform = currentTransform.parent;
            }

            if (rootObject != null)
            {
                // 삭제 모드가 아니라면, 가장 가까운 첫 번째 오브젝트를 바로 반환합니다.
                if (!isDeleteMode)
                {
                    return rootObject;
                }

                // 삭제 모드일 경우, 이 오브젝트가 현재 층에 있는지 확인합니다.
                int currentFloorLayer = LayerMask.NameToLayer($"{changeFloorSystem.currentFloor}F");

                // 자식 오브젝트들을 순회하며 레이어를 확인합니다.
                foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.layer == currentFloorLayer)
                    {
                        // 자식 중 하나라도 현재 층의 레이어와 일치하면, 이것이 바로 우리가 찾던 오브젝트입니다.
                        return rootObject;
                    }
                }
                // 현재 층의 오브젝트가 아니면, 다음으로 가까운 오브젝트를 계속 확인하기 위해 루프를 계속합니다.
            }
        }

        /*if (Physics.Raycast(ray, out RaycastHit hit, 100f, objectLayer))
        {
            Transform currentTransform = hit.collider.transform;
            // [핵심 수정]
            // 레이캐스트에 맞은 지점부터 시작해서 부모로 거슬러 올라가며,
            // ObjectPlacer가 관리하는 placedGameObjects 리스트에 포함된 오브젝트를 찾습니다.
            while (currentTransform != null)
            {
                if (ObjectPlacer.Instance.placedGameObjects.Contains(currentTransform.gameObject))
                {
                    // 리스트에서 일치하는 오브젝트를 찾으면 그것이 바로 우리가 삭제할 대상입니다.
                    return currentTransform.gameObject;
                }
                // 못 찾으면 한 단계 위 부모로 이동하여 다시 검사합니다.
                currentTransform = currentTransform.parent;
            }
        }*/

        return null;
    }

    private void ChangeFloorForBuildMode()
    {
        if(placementSystem.GetFloorLock()) changeFloorSystem.OnBuildModeChanged();
    }    
}
