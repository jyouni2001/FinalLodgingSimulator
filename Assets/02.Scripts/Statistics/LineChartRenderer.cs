using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity LineRenderer를 사용하여 점선 차트를 렌더링하는 클래스
/// 3개의 차트(명성도, 골드, 방문객)를 동시에 렌더링하고 실시간 업데이트를 지원합니다.
/// </summary>
public class LineChartRenderer : MonoBehaviour
{
    [Header("차트 설정")]
    [Tooltip("차트 크기 (픽셀)")]
    [SerializeField] private Vector2 chartSize = new Vector2(300f, 200f);
    
    [Tooltip("차트 여백")]
    [SerializeField] private float chartMargin = 20f;
    
    [Tooltip("점선 패턴 (점의 개수)")]
    [SerializeField] private int dashPattern = 5;
    
    [Tooltip("점선 간격")]
    [SerializeField] private float dashSpacing = 2f;
    
    [Header("색상 설정")]
    [Tooltip("명성도 차트 색상")]
    [SerializeField] private Color reputationColor = Color.blue;
    
    [Tooltip("골드 차트 색상")]
    [SerializeField] private Color goldColor = Color.yellow;
    
    [Tooltip("방문객 차트 색상")]
    [SerializeField] private Color visitorColor = Color.green;
    
    [Tooltip("그리드 색상")]
    [SerializeField] private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    
    [Header("라인 설정")]
    [Tooltip("라인 두께")]
    [SerializeField] private float lineWidth = 2f;
    
    [Tooltip("그리드 라인 두께")]
    [SerializeField] private float gridLineWidth = 1f;
    
    [Header("점 도표 설정")]
    [Tooltip("점 크기")]
    [SerializeField] private float pointSize = 16f;
    
    [Tooltip("점 표시 여부")]
    [SerializeField] private bool showDataPoints = true;
    
    [Tooltip("라인 표시 여부")]
    [SerializeField] private bool showLines = true;
    
    [Header("비율 설정")]
    [Tooltip("고정 비율 사용 여부 (체크 시 차트가 왜곡되지 않음)")]
    [SerializeField] private bool maintainAspectRatio = false;
    
    [Tooltip("목표 가로세로 비율 (너비/높이)")]
    [SerializeField] private float targetAspectRatio = 1.5f; // 3:2 비율
    
    [Header("애니메이션 설정")]
    [Tooltip("차트 애니메이션 활성화")]
    [SerializeField] private bool enableAnimation = false; // 실시간 차트 불필요하므로 애니메이션 비활성화
    
    [Tooltip("애니메이션 속도")]
    [SerializeField] private float animationSpeed = 2f;
    
    [Tooltip("애니메이션 지연 시간")]
    [SerializeField] private float animationDelay = 0.1f;
    
    [Header("디버그 설정")]
    [Tooltip("디버그 로그 표시")]
    [SerializeField] private bool showDebugLogs = true;
    
    // LineRenderer 컴포넌트들
    private LineRenderer reputationLineRenderer;
    private LineRenderer goldLineRenderer;
    private LineRenderer visitorLineRenderer;
    private LineRenderer gridLineRenderer;
    
    // 점 도표용 GameObject들
    private List<GameObject> reputationPointObjects = new List<GameObject>();
    private List<GameObject> goldPointObjects = new List<GameObject>();
    private List<GameObject> visitorPointObjects = new List<GameObject>();
    
    // 차트 데이터 (위치 정보)
    private List<Vector3> reputationPositions = new List<Vector3>();
    private List<Vector3> goldPositions = new List<Vector3>();
    private List<Vector3> visitorPositions = new List<Vector3>();
    
    // 스케일링 정보
    private float maxReputation = 1000f;
    private float maxGold = 10000f;
    private float maxVisitors = 100f;
    
    // 애니메이션 관련
    private bool isAnimating = false;
    private float animationProgress = 0f;
    
    // 캐시된 값들
    private Vector3 lastChartPosition;
    private Vector2 lastChartSize;
    
    // Canvas 관련
    private Canvas parentCanvas;
    private float canvasScaleFactor = 1f;
    
    // RectTransform 관련
    private RectTransform rectTransform;
    
    /// <summary>
    /// 차트 크기 (읽기 전용) - 실제 RectTransform 크기 반환
    /// </summary>
    public Vector2 ChartSize => rectTransform != null ? rectTransform.rect.size : chartSize;
    
    /// <summary>
    /// 차트 여백 (읽기 전용)
    /// </summary>
    public float ChartMargin => chartMargin;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeRectTransform();
        InitializeCanvas();
        InitializeLineRenderers();
    }
    
    /// <summary>
    /// RectTransform 초기화
    /// </summary>
    private void InitializeRectTransform()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
            DebugLog("RectTransform이 없어서 추가했습니다.");
        }
        DebugLog($"RectTransform 크기: {rectTransform.rect.size}");
    }
    
    /// <summary>
    /// Canvas 초기화 및 스케일 팩터 가져오기
    /// </summary>
    private void InitializeCanvas()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasScaleFactor = parentCanvas.scaleFactor;
            // ✅ ScaleFactor가 0이 되는 것을 방지 (최소값 보장)
            canvasScaleFactor = Mathf.Max(0.1f, canvasScaleFactor);
            DebugLog($"Canvas ScaleFactor: {canvasScaleFactor}");
        }
        else
        {
            canvasScaleFactor = 1f;
            DebugLog("Canvas를 찾을 수 없습니다. ScaleFactor를 1로 설정합니다.");
        }
    }
    
    private void Start()
    {
        SetupLineRenderers();
        CreateGrid();
    }
    
    private void Update()
    {
        // Canvas 스케일 팩터 업데이트
        UpdateCanvasScaleFactor();
        
        // 차트 위치나 크기가 변경되었는지 확인
        if (HasChartChanged())
        {
            UpdateChartLayout();
        }
    }
    
    /// <summary>
    /// Canvas 스케일 팩터 업데이트
    /// </summary>
    private void UpdateCanvasScaleFactor()
    {
        if (parentCanvas != null)
        {
            float newScaleFactor = parentCanvas.scaleFactor;
            
            // ✅ ScaleFactor가 0이 되는 것을 방지 (최소값 보장)
            newScaleFactor = Mathf.Max(0.1f, newScaleFactor);
            
            if (Mathf.Abs(newScaleFactor - canvasScaleFactor) > 0.01f)
            {
                canvasScaleFactor = newScaleFactor;
                // ✅ Canvas 좌표계를 사용하므로 ScaleFactor 변경 시에도 라인 두께는 그대로 유지
                DebugLog($"Canvas ScaleFactor 변경: {canvasScaleFactor} (라인 두께는 고정)");
            }
        }
    }
    
    /// <summary>
    /// 라인 두께 업데이트 (Canvas 좌표계 사용)
    /// </summary>
    private void UpdateLineWidths()
    {
        // ✅ Canvas 좌표계를 사용하므로 두께를 그대로 사용
        float scaledLineWidth = lineWidth;
        float scaledGridLineWidth = gridLineWidth;
        
        if (reputationLineRenderer != null)
        {
            reputationLineRenderer.startWidth = scaledLineWidth;
            reputationLineRenderer.endWidth = scaledLineWidth;
        }
        
        if (goldLineRenderer != null)
        {
            goldLineRenderer.startWidth = scaledLineWidth;
            goldLineRenderer.endWidth = scaledLineWidth;
        }
        
        if (visitorLineRenderer != null)
        {
            visitorLineRenderer.startWidth = scaledLineWidth;
            visitorLineRenderer.endWidth = scaledLineWidth;
        }
        
        if (gridLineRenderer != null)
        {
            gridLineRenderer.startWidth = scaledGridLineWidth;
            gridLineRenderer.endWidth = scaledGridLineWidth;
        }
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// LineRenderer 컴포넌트들 초기화
    /// </summary>
    private void InitializeLineRenderers()
    {
        // 명성도 차트 LineRenderer
        GameObject reputationObj = new GameObject("ReputationChart");
        reputationObj.transform.SetParent(transform);
        reputationLineRenderer = reputationObj.AddComponent<LineRenderer>();
        
        // 골드 차트 LineRenderer
        GameObject goldObj = new GameObject("GoldChart");
        goldObj.transform.SetParent(transform);
        goldLineRenderer = goldObj.AddComponent<LineRenderer>();
        
        // 방문객 차트 LineRenderer
        GameObject visitorObj = new GameObject("VisitorChart");
        visitorObj.transform.SetParent(transform);
        visitorLineRenderer = visitorObj.AddComponent<LineRenderer>();
        
        // 그리드 LineRenderer
        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(transform);
        gridLineRenderer = gridObj.AddComponent<LineRenderer>();
        
        DebugLog("LineRenderer 컴포넌트들 초기화 완료");
    }
    
    /// <summary>
    /// LineRenderer 설정
    /// </summary>
    private void SetupLineRenderers()
    {
        SetupLineRenderer(reputationLineRenderer, reputationColor, "Reputation");
        SetupLineRenderer(goldLineRenderer, goldColor, "Gold");
        SetupLineRenderer(visitorLineRenderer, visitorColor, "Visitor");
        SetupGridRenderer();
        
        DebugLog("LineRenderer 설정 완료");
    }
    
    /// <summary>
    /// 개별 LineRenderer 설정
    /// </summary>
    private void SetupLineRenderer(LineRenderer lr, Color color, string name)
    {
        lr.material = new Material(Shader.Find("UI/Default"));
        lr.material.color = color;
        
        // ✅ Canvas 좌표계를 사용하므로 라인 두께를 그대로 설정
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        
        lr.useWorldSpace = false;
        lr.sortingOrder = 1;
        lr.name = name;
        
        // UI Canvas에서 작동하도록 설정
        lr.alignment = LineAlignment.TransformZ;
        lr.textureMode = LineTextureMode.Tile;
        
        // 점선 패턴 설정
        if (dashPattern > 0)
        {
            lr.material.SetFloat("_DashSize", dashPattern);
            lr.material.SetFloat("_DashSpacing", dashSpacing);
        }
    }
    
    /// <summary>
    /// 그리드 LineRenderer 설정
    /// </summary>
    private void SetupGridRenderer()
    {
        gridLineRenderer.material = new Material(Shader.Find("UI/Default"));
        gridLineRenderer.material.color = gridColor;
        
        // ✅ Canvas 좌표계를 사용하므로 라인 두께를 그대로 설정
        gridLineRenderer.startWidth = gridLineWidth;
        gridLineRenderer.endWidth = gridLineWidth;
        
        gridLineRenderer.useWorldSpace = false;
        gridLineRenderer.sortingOrder = 0;
        gridLineRenderer.name = "Grid";
        
        // UI Canvas에서 작동하도록 설정
        gridLineRenderer.alignment = LineAlignment.TransformZ;
        gridLineRenderer.textureMode = LineTextureMode.Tile;
    }
    
    #endregion
    
    #region Chart Rendering
    
    /// <summary>
    /// 차트 데이터 업데이트 (일차별 데이터)
    /// </summary>
    /// <param name="dailyData">일차별 데이터 리스트</param>
    public void UpdateChartData(List<DailyData> dailyData)
    {
        if (dailyData == null || dailyData.Count == 0)
        {
            ClearCharts();
            cachedDailyData = null;
            return;
        }
        
        // 데이터 정리 및 정렬
        var sortedData = new List<DailyData>(dailyData);
        sortedData.Sort((a, b) => a.day.CompareTo(b.day));
        
        // ✅ 데이터 캐시 (해상도 변경 시 재계산용)
        cachedDailyData = sortedData;
        
        // 최대값 계산
        CalculateMaxValues(sortedData);
        
        // 포인트 생성
        GenerateChartPoints(sortedData);
        
        // 차트 렌더링
        RenderCharts();
        
        DebugLog($"차트 데이터 업데이트: {sortedData.Count}개 일차 데이터 포인트");
    }
    
    /// <summary>
    /// 최대값 계산 (일차별 데이터)
    /// </summary>
    private void CalculateMaxValues(List<DailyData> data)
    {
        maxReputation = 100f; // 기본값을 낮게 설정
        maxGold = 1000f;      // 기본값을 낮게 설정
        maxVisitors = 10f;    // 기본값을 낮게 설정
        
        foreach (var item in data)
        {
            if (item.reputationGained > maxReputation)
                maxReputation = item.reputationGained;
            
            if (item.goldEarned > maxGold)
                maxGold = item.goldEarned;
            
            if (item.totalVisitors > maxVisitors)
                maxVisitors = item.totalVisitors;
        }
        
        // 0 값도 표시되도록 최소값 보장 (0 값이 있어도 차트에서 보이도록)
        maxReputation = Mathf.Max(maxReputation, 10f);
        maxGold = Mathf.Max(maxGold, 100f);
        maxVisitors = Mathf.Max(maxVisitors, 1f);
        
        DebugLog($"최대값 계산: Rep={maxReputation}, Gold={maxGold}, Visitors={maxVisitors}");
    }
    
    /// <summary>
    /// 차트 포인트 생성 (일차별 데이터)
    /// </summary>
    private void GenerateChartPoints(List<DailyData> data)
    {
        reputationPositions.Clear();
        goldPositions.Clear();
        visitorPositions.Clear();
        
        // 기존 점 도표 오브젝트들 정리
        ClearPointObjects();
        
        // ✅ 실제 RectTransform 크기 사용 (해상도 대응)
        Vector2 actualSize = rectTransform != null ? rectTransform.rect.size : chartSize;
        
        // ✅ 비율 유지 옵션이 활성화되어 있으면 크기 조정
        if (maintainAspectRatio)
        {
            actualSize = AdjustSizeForAspectRatio(actualSize, targetAspectRatio);
        }
        
        // ✅ RectTransform의 rect 좌표계 사용 (pivot 무관)
        Rect rect = rectTransform != null ? rectTransform.rect : new Rect(0, 0, actualSize.x, actualSize.y);
        
        float chartWidth = Mathf.Max(10f, rect.width - (chartMargin * 2));
        float chartHeight = Mathf.Max(10f, rect.height - (chartMargin * 2));
        
        // rect.min: 좌하단 좌표, rect.max: 우상단 좌표 (pivot 기준)
        float startX = rect.xMin + chartMargin;
        float startY = rect.yMin + chartMargin;
        
        DebugLog($"차트 포인트 생성 - Rect: {rect}, 시작점: ({startX}, {startY}), 차트 영역: {chartWidth}x{chartHeight}");
        
        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            
            // ✅ rect 좌표계 기준으로 계산
            float x = startX + (i / (float)Math.Max(1, data.Count - 1)) * chartWidth;
            
            // Y 좌표 계산 (값 기반, 0-1 정규화)
            float reputationY = startY + Mathf.Max(0.05f, (item.reputationGained / maxReputation)) * chartHeight;
            float goldY = startY + Mathf.Max(0.05f, (item.goldEarned / maxGold)) * chartHeight;
            float visitorY = startY + Mathf.Max(0.05f, (item.totalVisitors / maxVisitors)) * chartHeight;
            
            // 위치 정보 저장
            reputationPositions.Add(new Vector3(x, reputationY, 0));
            goldPositions.Add(new Vector3(x, goldY, 0));
            visitorPositions.Add(new Vector3(x, visitorY, 0));
            
            // 점 도표 오브젝트 생성
            if (showDataPoints)
            {
                DebugLog($"점 생성 중: Day {item.day}, Rep={item.reputationGained}, Gold={item.goldEarned}, Visitors={item.totalVisitors}");
                CreateDataPoint(reputationPositions[i], reputationColor, $"ReputationPoint_{i}", reputationPointObjects);
                CreateDataPoint(goldPositions[i], goldColor, $"GoldPoint_{i}", goldPointObjects);
                CreateDataPoint(visitorPositions[i], visitorColor, $"VisitorPoint_{i}", visitorPointObjects);
            }
        }
    }
    
    /// <summary>
    /// 데이터 포인트 생성
    /// </summary>
    private void CreateDataPoint(Vector3 position, Color color, string name, List<GameObject> pointList)
    {
        GameObject pointObj = new GameObject(name);
        pointObj.transform.SetParent(transform);
        
        // UI Image 컴포넌트 추가
        RectTransform rectTransform = pointObj.AddComponent<RectTransform>();
        
        // Canvas 스케일 팩터를 고려한 점 크기 설정
        float scaledPointSize = pointSize;  // UI 요소는 이미 Canvas ScaleFactor의 영향을 받으므로 그대로 사용
        rectTransform.sizeDelta = new Vector2(scaledPointSize, scaledPointSize);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        
        // ✅ position은 이미 Canvas 좌표이므로 그대로 사용
        rectTransform.anchoredPosition = new Vector2(position.x, position.y);
        
        Image pointImage = pointObj.AddComponent<Image>();
        pointImage.color = color;
        
        // 기본 UI 스프라이트 사용 (흰색 텍스처로 사각형 점 생성)
        Texture2D whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        pointImage.sprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        
        pointList.Add(pointObj);
        
        DebugLog($"데이터 포인트 생성: {name} at ({position.x:F1}, {position.y:F1})");
    }
    
    /// <summary>
    /// 원형 스프라이트 생성
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                if (distance <= radius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    /// <summary>
    /// 점 도표 오브젝트들 정리
    /// </summary>
    private void ClearPointObjects()
    {
        foreach (var obj in reputationPointObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        foreach (var obj in goldPointObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        foreach (var obj in visitorPointObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        
        reputationPointObjects.Clear();
        goldPointObjects.Clear();
        visitorPointObjects.Clear();
    }
    
    /// <summary>
    /// 차트 렌더링
    /// </summary>
    private void RenderCharts()
    {
        if (enableAnimation)
        {
            StartAnimation();
        }
        else
        {
            RenderChartImmediate();
        }
    }
    
    /// <summary>
    /// 즉시 차트 렌더링
    /// </summary>
    private void RenderChartImmediate()
    {
        if (showLines)
        {
            RenderLine(reputationLineRenderer, reputationPositions);
            RenderLine(goldLineRenderer, goldPositions);
            RenderLine(visitorLineRenderer, visitorPositions);
        }
        else
        {
            // 라인을 표시하지 않을 때는 포인트만 표시
            reputationLineRenderer.positionCount = 0;
            goldLineRenderer.positionCount = 0;
            visitorLineRenderer.positionCount = 0;
        }
    }
    
    /// <summary>
    /// 개별 라인 렌더링
    /// </summary>
    private void RenderLine(LineRenderer lr, List<Vector3> points)
    {
        if (points.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }
        
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }
    
    #endregion
    
    #region Animation
    
    /// <summary>
    /// 애니메이션 시작
    /// </summary>
    private void StartAnimation()
    {
        if (isAnimating) return;
        
        StartCoroutine(AnimateCharts());
    }
    
    /// <summary>
    /// 차트 애니메이션 코루틴
    /// </summary>
    private System.Collections.IEnumerator AnimateCharts()
    {
        isAnimating = true;
        animationProgress = 0f;
        
        while (animationProgress < 1f)
        {
            animationProgress += Time.deltaTime * animationSpeed;
            animationProgress = Mathf.Clamp01(animationProgress);
            
            RenderAnimatedCharts();
            
            yield return new WaitForSeconds(animationDelay);
        }
        
        // 최종 렌더링
        RenderChartImmediate();
        isAnimating = false;
    }
    
    /// <summary>
    /// 애니메이션된 차트 렌더링
    /// </summary>
    private void RenderAnimatedCharts()
    {
        if (showLines)
        {
            RenderAnimatedLine(reputationLineRenderer, reputationPositions);
            RenderAnimatedLine(goldLineRenderer, goldPositions);
            RenderAnimatedLine(visitorLineRenderer, visitorPositions);
        }
    }
    
    /// <summary>
    /// 애니메이션된 라인 렌더링
    /// </summary>
    private void RenderAnimatedLine(LineRenderer lr, List<Vector3> points)
    {
        if (points.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }
        
        int visiblePoints = Mathf.RoundToInt(points.Count * animationProgress);
        visiblePoints = Mathf.Max(1, visiblePoints);
        
        lr.positionCount = visiblePoints;
        
        for (int i = 0; i < visiblePoints; i++)
        {
            lr.SetPosition(i, points[i]);
        }
    }
    
    #endregion
    
    #region Grid System
    
    /// <summary>
    /// 그리드 생성
    /// </summary>
    private void CreateGrid()
    {
        var gridPoints = new List<Vector3>();
        
        // ✅ 실제 RectTransform 크기 사용 (해상도 대응)
        Vector2 actualSize = rectTransform != null ? rectTransform.rect.size : chartSize;
        
        // ✅ 비율 유지 옵션이 활성화되어 있으면 크기 조정
        if (maintainAspectRatio)
        {
            actualSize = AdjustSizeForAspectRatio(actualSize, targetAspectRatio);
        }
        
        // ✅ RectTransform의 rect 좌표계 사용 (pivot 무관)
        Rect rect = rectTransform != null ? rectTransform.rect : new Rect(0, 0, actualSize.x, actualSize.y);
        
        float chartWidth = Mathf.Max(10f, rect.width - (chartMargin * 2));
        float chartHeight = Mathf.Max(10f, rect.height - (chartMargin * 2));
        
        float startX = rect.xMin + chartMargin;
        float startY = rect.yMin + chartMargin;
        float endX = rect.xMax - chartMargin;
        float endY = rect.yMax - chartMargin;
        
        DebugLog($"그리드 생성 - Rect: {rect}, 시작: ({startX}, {startY}), 끝: ({endX}, {endY})");
        
        // 수직 그리드 라인 (일차축)
        for (int i = 0; i <= 4; i++)
        {
            float x = startX + (i / 4f) * chartWidth;
            gridPoints.Add(new Vector3(x, startY, 0));
            gridPoints.Add(new Vector3(x, endY, 0));
        }
        
        // 수평 그리드 라인 (값축)
        for (int i = 0; i <= 4; i++)
        {
            float y = startY + (i / 4f) * chartHeight;
            gridPoints.Add(new Vector3(startX, y, 0));
            gridPoints.Add(new Vector3(endX, y, 0));
        }
        
        gridLineRenderer.positionCount = gridPoints.Count;
        gridLineRenderer.SetPositions(gridPoints.ToArray());
    }
    
    #endregion
    
    #region Aspect Ratio Management
    
    /// <summary>
    /// 크기를 목표 비율에 맞게 조정
    /// </summary>
    /// <param name="originalSize">원본 크기</param>
    /// <param name="targetRatio">목표 비율 (width/height)</param>
    /// <returns>조정된 크기</returns>
    private Vector2 AdjustSizeForAspectRatio(Vector2 originalSize, float targetRatio)
    {
        if (originalSize.x <= 0 || originalSize.y <= 0 || targetRatio <= 0)
        {
            return originalSize;
        }
        
        float currentRatio = originalSize.x / originalSize.y;
        
        // 현재 비율이 목표 비율보다 넓으면 (더 가로로 긴 경우)
        if (currentRatio > targetRatio)
        {
            // 높이를 기준으로 너비를 줄임
            float newWidth = originalSize.y * targetRatio;
            DebugLog($"비율 조정 (너비 줄임): {originalSize.x}x{originalSize.y} -> {newWidth}x{originalSize.y}");
            return new Vector2(newWidth, originalSize.y);
        }
        // 현재 비율이 목표 비율보다 좁으면 (더 세로로 긴 경우)
        else if (currentRatio < targetRatio)
        {
            // 너비를 기준으로 높이를 줄임
            float newHeight = originalSize.x / targetRatio;
            DebugLog($"비율 조정 (높이 줄임): {originalSize.x}x{originalSize.y} -> {originalSize.x}x{newHeight}");
            return new Vector2(originalSize.x, newHeight);
        }
        
        // 비율이 이미 일치하면 그대로 반환
        return originalSize;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// 차트가 변경되었는지 확인
    /// </summary>
    private bool HasChartChanged()
    {
        bool positionChanged = transform.position != lastChartPosition;
        
        // ✅ 실제 RectTransform 크기로 변경 감지 (해상도 대응)
        Vector2 currentSize = rectTransform != null ? rectTransform.rect.size : chartSize;
        bool sizeChanged = currentSize != lastChartSize;
        
        if (positionChanged || sizeChanged)
        {
            lastChartPosition = transform.position;
            lastChartSize = currentSize;
            
            if (sizeChanged)
            {
                DebugLog($"차트 크기 변경 감지: {lastChartSize} -> {currentSize}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 차트 레이아웃 업데이트
    /// </summary>
    private void UpdateChartLayout()
    {
        CreateGrid();
        
        // ✅ 캐시된 데이터가 있으면 새 크기로 다시 계산하여 렌더링
        if (cachedDailyData != null && cachedDailyData.Count > 0)
        {
            DebugLog("차트 레이아웃 업데이트 - 데이터 재계산 중");
            CalculateMaxValues(cachedDailyData);
            GenerateChartPoints(cachedDailyData);
            RenderChartImmediate();
        }
    }
    
    // 데이터 캐시 (해상도 변경 시 재계산용)
    private List<DailyData> cachedDailyData;
    
    /// <summary>
    /// 차트 초기화
    /// </summary>
    public void ClearCharts()
    {
        reputationLineRenderer.positionCount = 0;
        goldLineRenderer.positionCount = 0;
        visitorLineRenderer.positionCount = 0;
        
        reputationPositions.Clear();
        goldPositions.Clear();
        visitorPositions.Clear();
        
        // 점 도표 오브젝트들도 정리
        ClearPointObjects();
        
        // ✅ 캐시된 데이터도 정리
        cachedDailyData = null;
        
        DebugLog("차트 초기화 완료");
    }
    
    /// <summary>
    /// 차트 크기 설정
    /// </summary>
    public void SetChartSize(Vector2 newSize)
    {
        chartSize = newSize;
        UpdateChartLayout();
    }
    
    /// <summary>
    /// 차트 색상 설정
    /// </summary>
    public void SetChartColors(Color reputation, Color gold, Color visitor)
    {
        reputationColor = reputation;
        goldColor = gold;
        visitorColor = visitor;
        
        if (reputationLineRenderer != null)
            reputationLineRenderer.material.color = reputationColor;
        if (goldLineRenderer != null)
            goldLineRenderer.material.color = goldColor;
        if (visitorLineRenderer != null)
            visitorLineRenderer.material.color = visitorColor;
    }
    
    /// <summary>
    /// 테스트용 점 생성 (디버그용)
    /// </summary>
    public void CreateTestPoints()
    {
        DebugLog("테스트 점 생성 시작");
        
        // 테스트 데이터로 점 생성
        var testData = new List<DailyData>
        {
            new DailyData(1, 10, 100, 5, 0, 0, 10, 100),
            new DailyData(2, 20, 200, 8, 10, 100, 30, 300),
            new DailyData(3, 0, 50, 3, 30, 300, 30, 350)
        };
        
        UpdateChartData(testData);
        
        DebugLog("테스트 점 생성 완료");
    }
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void DebugLog(string message)
    {
#if UNITY_EDITOR
        if (!showDebugLogs) return;
        
        Debug.Log($"[LineChartRenderer] {message}");
#endif
    }
    
    #endregion
}
