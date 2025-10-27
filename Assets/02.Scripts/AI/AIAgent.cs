using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JY;
using UnityEngine.Animations;

namespace JY
{
public interface IRoomDetector
{
    GameObject[] GetDetectedRooms();
    void DetectRooms();
}

public class AIAgent : MonoBehaviour
{
    #region 비공개 변수
    private NavMeshAgent agent;                    // AI 이동 제어를 위한 네비메시 에이전트
    private RoomManager roomManager;               // 룸 매니저 참조
    private Transform counterPosition;             // 카운터 위치
    private static List<RoomInfo> roomList = new List<RoomInfo>();  // 동적 룸 정보 리스트
    private Transform spawnPoint;                  // AI 생성/소멸 지점
    private int currentRoomIndex = -1;            // 현재 사용 중인 방 인덱스 (-1은 미사용)
    private AISpawner spawner;                    // AI 스포너 참조
    private float arrivalDistance = 0.5f;         // 도착 판정 거리

    private bool isInQueue = false;               // 대기열에 있는지 여부
    private Vector3 targetQueuePosition;          // 대기열 목표 위치
    private Quaternion targetQueueRotation;       // 대기열 목표 방향
    private bool isWaitingForService = false;     // 서비스 대기 중인지 여부

    private AIState currentState = AIState.MovingToQueue;  // 현재 AI 상태
    private string currentDestination = "대기열로 이동 중";  // 현재 목적지 (UI 표시용)

    private static readonly object lockObject = new object();  // 스레드 동기화용 잠금 객체
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조

    private Coroutine useWanderingCoroutine;  // 방 외부 배회 코루틴 참조
    private Coroutine queueCoroutine;         // 대기열 코루틴 참조
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    private TimeSystem timeSystem;                // 시간 시스템 참조
    private int lastBehaviorUpdateHour = -1;      // 마지막 행동 업데이트 시간
    private bool isScheduledForDespawn = false;   // 11시 디스폰 예정인지 여부
    
    // 침대 관련 변수들
    private Transform currentBedTransform;        // 현재 사용 중인 침대 Transform
    private Transform currentBedPoint;            // 현재 사용 중인 BedPoint Transform
    private Vector3 preSleepPosition;            // 수면 전 위치 저장
    private Quaternion preSleepRotation;         // 수면 전 회전값 저장
    private bool isSleeping = false;             // 수면 중인지 여부
    private bool isNapping = false;              // 낮잠 중인지 여부 (짧은 수면)
    private Coroutine sleepingCoroutine;         // 수면 코루틴 참조
    private Coroutine nappingCoroutine;          // 낮잠 타이머 코루틴 참조
    private Animator animator;                   // 애니메이터 컴포넌트
    private float sleepStartTime = 0f;           // 수면 시작 시간
    
    // 선베드 관련 변수들
    private Transform currentSunbedTransform;    // 현재 사용 중인 선베드 Transform
    private Vector3 preSunbedPosition;          // 선베드 사용 전 위치 저장
    private Quaternion preSunbedRotation;       // 선베드 사용 전 회전값 저장
    private bool isUsingSunbed = false;         // 선베드 사용 중인지 여부
    // ✅ isSunbedInRoom 제거 - 결제 시점에 방 안/밖 확인
    private Coroutine sunbedCoroutine;          // 선베드 관련 코루틴 참조
    
    // ✅ occupiedSunbeds 제거 - 시설 레벨 점유는 불필요, SunbedPoint만 점유 관리
    
    // 선베드 포인트 점유 관리를 위한 static 딕셔너리 (다른 시설과 동일한 패턴)
    private static Dictionary<Transform, AIAgent> occupiedSunbedPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object sunbedPointLock = new object();
    private Transform currentSunbedPoint; // 현재 사용 중인 SunbedPoint
    
    // 욕조 관련 변수들
    private Transform currentBathtubTransform;   // 현재 사용 중인 욕조 Transform
    private Vector3 preBathtubPosition;          // 욕조 사용 전 위치 저장
    private Quaternion preBathtubRotation;       // 욕조 사용 전 회전값 저장
    private bool isUsingBathtub = false;         // 욕조 사용 중인지 여부
    private Coroutine bathtubCoroutine;          // 욕조 관련 코루틴 참조
    
    // 운동 시설 관련 변수들
    private Transform currentHealthTransform;    // 현재 사용 중인 운동 시설 Transform
    private Vector3 preHealthPosition;           // 운동 시설 사용 전 위치 저장
    private Quaternion preHealthRotation;        // 운동 시설 사용 전 회전값 저장
    private bool isUsingHealth = false;          // 운동 시설 사용 중인지 여부
    private Coroutine healthCoroutine;           // 운동 시설 관련 코루틴 참조
    
    // 헬스장 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedHealthFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object healthLock = new object();
    
    // 예식장 관련 변수들
    private Transform currentWeddingTransform;   // 현재 사용 중인 예식장 Transform
    private Vector3 preWeddingPosition;          // 예식장 사용 전 위치 저장
    private Quaternion preWeddingRotation;       // 예식장 사용 전 회전값 저장
    private bool isUsingWedding = false;         // 예식장 사용 중인지 여부
    private Coroutine weddingCoroutine;          // 예식장 관련 코루틴 참조
    
    // 예식장 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedWeddingFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object weddingLock = new object();
    
    // 라운지 관련 변수들
    private Transform currentLoungeTransform;    // 현재 사용 중인 라운지 Transform
    private Vector3 preLoungePosition;           // 라운지 사용 전 위치 저장
    private Quaternion preLoungeRotation;        // 라운지 사용 전 회전값 저장
    private bool isUsingLounge = false;          // 라운지 사용 중인지 여부
    private Coroutine loungeCoroutine;           // 라운지 관련 코루틴 참조
    
    // 라운지 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedLoungeFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object loungeLock = new object();
    
    // 연회장 관련 변수들
    private Transform currentHallTransform;      // 현재 사용 중인 연회장 Transform
    private Vector3 preHallPosition;             // 연회장 사용 전 위치 저장
    private Quaternion preHallRotation;          // 연회장 사용 전 회전값 저장
    private bool isUsingHall = false;            // 연회장 사용 중인지 여부
    private Coroutine hallCoroutine;             // 연회장 관련 코루틴 참조
    
    // 연회장 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedHallFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object hallLock = new object();
    
    // Point 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedHealthPoints = new Dictionary<Transform, AIAgent>();
    private static Dictionary<Transform, AIAgent> occupiedWeddingPoints = new Dictionary<Transform, AIAgent>();
    private static Dictionary<Transform, AIAgent> occupiedLoungePoints = new Dictionary<Transform, AIAgent>();
    private static Dictionary<Transform, AIAgent> occupiedHallPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object healthPointLock = new object();
    private static readonly object weddingPointLock = new object();
    private static readonly object loungePointLock = new object();
    private static readonly object hallPointLock = new object();
    
    // 현재 사용 중인 Point Transform
    private Transform currentHealthPoint;
    private Transform currentWeddingPoint;
    private Transform currentLoungePoint;
    private Transform currentHallPoint;
    
    // 사우나 관련
    private Transform currentSaunaTransform;
    private Vector3 preSaunaPosition;
    private Quaternion preSaunaRotation;
    private bool isUsingSauna = false;
    private Coroutine saunaCoroutine;
    private Transform currentSaunaPoint; // SaunaSitPoint 또는 SaunaDownPoint
    private bool isSaunaSitting = false; // Sitting 애니메이션 사용 여부
    private readonly Dictionary<Transform, AIAgent> occupiedSaunaFacilities = new Dictionary<Transform, AIAgent>();
    private readonly object saunaLock = new object();
    
    // 사우나 포인트 점유 관리 (static)
    private static Dictionary<Transform, AIAgent> occupiedSaunaSitPoints = new Dictionary<Transform, AIAgent>();
    private static Dictionary<Transform, AIAgent> occupiedSaunaDownPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object saunaSitPointLock = new object();
    private static readonly object saunaDownPointLock = new object();
    
    // 카페 관련 변수들
    private Transform currentCafeTransform;      // 현재 사용 중인 카페 Transform
    private Vector3 preCafePosition;             // 카페 사용 전 위치 저장
    private Quaternion preCafeRotation;          // 카페 사용 전 회전값 저장
    private bool isUsingCafe = false;            // 카페 사용 중인지 여부
    private Coroutine cafeCoroutine;             // 카페 관련 코루틴 참조
    
    // 카페 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedCafeFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object cafeLock = new object();
    
    // 카페 포인트 점유 관리 (static)
    private static Dictionary<Transform, AIAgent> occupiedCafePoints = new Dictionary<Transform, AIAgent>();
    private static readonly object cafePointLock = new object();
    
    // 현재 사용 중인 카페 Point Transform
    private Transform currentCafePoint;
    
    // Bath 관련 변수들
    private Transform currentBathTransform;      // 현재 사용 중인 Bath Transform
    private Vector3 preBathPosition;             // Bath 사용 전 위치 저장
    private Quaternion preBathRotation;          // Bath 사용 전 회전값 저장
    private bool isUsingBath = false;            // Bath 사용 중인지 여부
    private Coroutine bathCoroutine;             // Bath 관련 코루틴 참조
    private Transform currentBathPoint;          // BathSitPoint 또는 BathDownPoint
    private bool isBathSitting = false;          // Sitting 애니메이션 사용 여부 (false면 BedTime)
    
    // Bath 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedBathFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object bathLock = new object();
    
    // Bath 포인트 점유 관리 (static)
    private static Dictionary<Transform, AIAgent> occupiedBathSitPoints = new Dictionary<Transform, AIAgent>();
    private static Dictionary<Transform, AIAgent> occupiedBathDownPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object bathSitPointLock = new object();
    private static readonly object bathDownPointLock = new object();
    
    // Hos(고급식당) 관련 변수들
    private Transform currentHosTransform;       // 현재 사용 중인 Hos Transform
    private Vector3 preHosPosition;              // Hos 사용 전 위치 저장
    private Quaternion preHosRotation;           // Hos 사용 전 회전값 저장
    private bool isUsingHos = false;             // Hos 사용 중인지 여부
    private Coroutine hosCoroutine;              // Hos 관련 코루틴 참조
    private Transform currentHosPoint;           // 현재 사용 중인 HosPoint Transform
    private ChairPoint currentHosChairPoint;     // 현재 사용 중인 ChairPoint (테이블 관리)
    private GameObject currentHosUtensil;        // 현재 사용 중인 식사 도구 (Fork 또는 Spoon)
    
    // Hos 점유 관리를 위한 static 딕셔너리
    private static Dictionary<Transform, AIAgent> occupiedHosFacilities = new Dictionary<Transform, AIAgent>();
    private static readonly object hosLock = new object();
    
    // Hos 포인트 점유 관리 (static)
    private static Dictionary<Transform, AIAgent> occupiedHosPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object hosPointLock = new object();
    
    // 침대 포인트 점유 관리 (static)
    private static Dictionary<Transform, AIAgent> occupiedBedPoints = new Dictionary<Transform, AIAgent>();
    private static readonly object bedPointLock = new object();
    
    // 복원 플래그
    private bool isBeingRestored = false;
    
    // 식당 관련 변수들
    private Transform currentKitchenTransform;  // 현재 식당 Transform
    private Transform currentChairTransform;    // 현재 사용 중인 의자 Transform
    private ChairPoint currentChairPoint;       // 현재 사용 중인 ChairPoint 컴포넌트
    private Vector3 preChairPosition;           // 의자 사용 전 위치 저장
    private Quaternion preChairRotation;       // 의자 사용 전 회전값 저장
    private bool isEating = false;              // 식사 중인지 여부
    private Coroutine eatingCoroutine;         // 식사 관련 코루틴 참조
    private float eatingStartTime = 0f;         // 식사 시작 시간
    
    // 주방 카운터 관련 변수들
    private KitchenCounter currentKitchenCounter; // 현재 사용 중인 주방 카운터
    private bool isWaitingAtKitchenCounter = false; // 주방 카운터에서 대기 중인지 여부
    private GameObject currentEatingUtensil = null; // 현재 사용 중인 식사 도구 (Fork 또는 Spoon)
    
    // 주방 카운터 관련 공개 프로퍼티
    public bool IsWaitingAtKitchenCounter => isWaitingAtKitchenCounter;
    
    [Header("UI 디버그")]
    [Tooltip("모든 AI 머리 위에 행동 상태 텍스트 표시")]
    [SerializeField] private bool debugUIEnabled = true;
    
    // 모든 AI가 공유하는 static 변수
    private static bool globalShowDebugUI = true;
    
    [Header("애니메이션 소품")]
    [Tooltip("왼손 덤벨 (헬스장 애니메이션용)")]
    [SerializeField] private GameObject leftDumbbell;
    
    [Tooltip("오른손 덤벨 (헬스장 애니메이션용)")]
    [SerializeField] private GameObject rightDumbbell;
    
    [Tooltip("파스타 오브젝트 (식사 애니메이션용)")]
    [SerializeField] private GameObject pastaObject;
    
    [Tooltip("포크 오브젝트 (식사 애니메이션용)")]
    [SerializeField] private GameObject forkObject;
    
    [Tooltip("스푼 오브젝트 (식사 애니메이션용)")]
    [SerializeField] private GameObject spoonObject;
    #endregion

    #region 룸 정보 클래스
    private class RoomInfo
    {
        public Transform transform;               // 룸의 Transform
        public bool isOccupied;                   // 룸 사용 여부
        public bool isBeingCleaned;               // 룸 청소 중 여부 (집사 AI용)
        public float size;                        // 룸 크기
        public GameObject gameObject;             // 룸 게임 오브젝트
        public string roomId;                     // 룸 고유 ID
        public Bounds bounds;                     // 룸의 Bounds
        public Transform bedTransform;            // 침대 Transform (있는 경우)
        // ✅ isSunbedRoom 제거 - 선베드는 더 이상 방이 아님

        public RoomInfo(GameObject roomObj)
        {
            gameObject = roomObj;
            transform = roomObj.transform;
            isOccupied = false;
            isBeingCleaned = false;

            var collider = roomObj.GetComponent<Collider>();
            size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
            var roomContents = roomObj.GetComponent<RoomContents>();
            bounds = roomContents != null ? roomContents.roomBounds : (collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one * 2f));

            // ✅ isSunbedRoom 제거 - 모든 방은 일반 방으로 처리

            // 침대 탐지
            bedTransform = FindBedInRoom(roomObj);

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
        }

        private Transform FindBedInRoom(GameObject roomObj)
        {
            // 방 내부에서 "Bed" 태그를 가진 오브젝트 찾기
            var allBeds = GameObject.FindGameObjectsWithTag("Bed");
            foreach (var bed in allBeds)
            {
                if (bed != null && bounds.Contains(bed.transform.position))
                {
                    return bed.transform;
                }
            }
            return null;
        }
    }
    #endregion

    #region AI 상태 열거형
    private enum AIState
    {
        Wandering,           // 외부 배회
        MovingToQueue,       // 대기열로 이동
        WaitingInQueue,      // 대기열에서 대기
        MovingToRoom,        // 배정된 방으로 이동
        UsingRoom,           // 방 사용
        UseWandering,        // 방 사용 중 배회
        ReportingRoom,       // 방 사용 완료 보고
        ReturningToSpawn,    // 스폰 지점으로 복귀 (디스폰)
        RoomWandering,       // 방 내부 배회
        ReportingRoomQueue,  // 방 사용 완료 보고를 위해 대기열로 이동
        MovingToBed,         // 침대로 이동
        Sleeping,            // 침대에서 수면
        MovingToSunbed,      // 선베드로 이동
        UsingSunbed,         // 선베드 사용 중
        MovingToKitchenCounter, // 주방 카운터로 이동
        WaitingAtKitchenCounter, // 주방 카운터에서 대기
        MovingToChair,       // 의자로 이동
        Eating,              // 식사 중
        MovingToBathtub,     // 욕조로 이동
        UsingBathtub,        // 욕조 사용 중
        MovingToHealth,      // 운동 시설로 이동
        UsingHealth,         // 운동 시설 사용 중
        MovingToWedding,     // 예식장으로 이동
        UsingWedding,        // 예식장 사용 중
        MovingToLounge,      // 라운지로 이동
        UsingLounge,         // 라운지 사용 중
        MovingToHall,        // 연회장으로 이동
        UsingHall,           // 연회장 사용 중
        MovingToSauna,       // 사우나로 이동
        UsingSauna,          // 사우나 사용 중
        MovingToCafe,        // 카페로 이동
        UsingCafe,           // 카페 사용 중
        MovingToBath,        // Bath로 이동
        UsingBath,           // Bath 사용 중
        MovingToHos,         // 고급식당으로 이동
        UsingHos             // 고급식당 사용 중
    }
    #endregion

    #region 이벤트
    public delegate void RoomsUpdatedHandler(GameObject[] rooms);
    private static event RoomsUpdatedHandler OnRoomsUpdated;
    #endregion

    #region 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeStatics()
    {
        roomList = new List<RoomInfo>();
        OnRoomsUpdated -= UpdateRoomList;
        OnRoomsUpdated = null;
        globalShowDebugUI = true;
        // ✅ occupiedSunbeds 초기화 제거 - 시설 레벨 점유 불필요
        occupiedSunbedPoints = new Dictionary<Transform, AIAgent>(); // SunbedPoint만 점유 관리
        occupiedHealthFacilities = new Dictionary<Transform, AIAgent>();
        occupiedWeddingFacilities = new Dictionary<Transform, AIAgent>();
        occupiedLoungeFacilities = new Dictionary<Transform, AIAgent>();
        occupiedHallFacilities = new Dictionary<Transform, AIAgent>();
        occupiedHealthPoints = new Dictionary<Transform, AIAgent>();
        occupiedWeddingPoints = new Dictionary<Transform, AIAgent>();
        occupiedLoungePoints = new Dictionary<Transform, AIAgent>();
        occupiedHallPoints = new Dictionary<Transform, AIAgent>();
        occupiedSaunaSitPoints = new Dictionary<Transform, AIAgent>();
        occupiedSaunaDownPoints = new Dictionary<Transform, AIAgent>();
        occupiedCafeFacilities = new Dictionary<Transform, AIAgent>();
        occupiedCafePoints = new Dictionary<Transform, AIAgent>();
        occupiedBathFacilities = new Dictionary<Transform, AIAgent>();
        occupiedBathSitPoints = new Dictionary<Transform, AIAgent>();
        occupiedBathDownPoints = new Dictionary<Transform, AIAgent>();
        occupiedHosFacilities = new Dictionary<Transform, AIAgent>();
        occupiedHosPoints = new Dictionary<Transform, AIAgent>();
        occupiedBedPoints = new Dictionary<Transform, AIAgent>();
    }

    void Start()
    {
        if (!InitializeComponents()) return;
        InitializeRoomsIfEmpty();
        InitializeAnimationProps();
        timeSystem = TimeSystem.Instance;
        
        // Inspector 설정을 전역 설정에 반영
        globalShowDebugUI = debugUIEnabled;
        
        // 복원 중이 아닐 때만 초기 행동 결정
        if (!isBeingRestored)
        {
            DetermineInitialBehavior();
        }
    }
    
    /// <summary>
    /// 애니메이션 소품 초기화 (모두 비활성화)
    /// </summary>
    private void InitializeAnimationProps()
    {
        if (leftDumbbell != null) leftDumbbell.SetActive(false);
        if (rightDumbbell != null) rightDumbbell.SetActive(false);
        if (pastaObject != null) pastaObject.SetActive(false);
        if (forkObject != null) forkObject.SetActive(false);
        if (spoonObject != null) spoonObject.SetActive(false);
    }

    private bool InitializeComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent == null)
        {
            Destroy(gameObject);
            return false;
        }

        // NavMeshAgent 정확한 위치/회전 설정 (반동 제거)
        agent.acceleration = 100f;        // 가속도 증가 (빠르게 가속)
        agent.angularSpeed = 360f;        // 이동 중 자동 회전 (자연스러운 이동)
        agent.stoppingDistance = 0.05f;   // 정지 거리 최소화
        agent.autoBraking = true;         // 자동 브레이킹 활성화

        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Destroy(gameObject);
            return false;
        }

        roomManager = FindFirstObjectByType<RoomManager>();
        spawnPoint = spawn.transform;

        GameObject counter = GameObject.FindGameObjectWithTag("Counter");
        counterPosition = counter != null ? counter.transform : null;

        if (counterManager == null)
        {
            counterManager = FindFirstObjectByType<CounterManager>();
        }

        return true;
    }

    private void InitializeRoomsIfEmpty()
    {
        lock (lockObject)
        {
            if (roomList.Count == 0)
            {
                InitializeRooms();
                if (OnRoomsUpdated == null)
                {
                    OnRoomsUpdated += UpdateRoomList;
                }
            }
        }
    }

    private void DetermineInitialBehavior()
    {
        DetermineBehaviorByTime();
    }
    #endregion

    #region 룸 관리
    private void InitializeRooms()
    {
        roomList.Clear();

        var roomDetectors = GameObject.FindObjectsByType<RoomDetector>(FindObjectsSortMode.None);
        
        if (roomDetectors.Length > 0)
        {
            foreach (var detector in roomDetectors)
            {
                detector.ScanForRooms();
                detector.OnRoomsUpdated += rooms =>
                {
                    if (rooms != null && rooms.Length > 0)
                    {
                        UpdateRoomList(rooms);
                    }
                };
            }
        }
        else
        {
            GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
            
            foreach (GameObject room in taggedRooms)
            {
                if (!roomList.Any(r => r.gameObject == room))
                {
                    var roomInfo = new RoomInfo(room);
                    roomList.Add(roomInfo);
                }
            }
        }

        if (roomList.Count == 0)
        {
        }
    }

    public static void UpdateRoomList(GameObject[] newRooms)
    {
        if (newRooms == null || newRooms.Length == 0) return;

        lock (lockObject)
        {
            HashSet<string> processedRoomIds = new HashSet<string>();
            List<RoomInfo> updatedRoomList = new List<RoomInfo>();

            foreach (GameObject room in newRooms)
            {
                if (room != null)
                {
                    RoomInfo newRoom = new RoomInfo(room);
                    if (!processedRoomIds.Contains(newRoom.roomId))
                    {
                        processedRoomIds.Add(newRoom.roomId);
                        var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                        if (existingRoom != null)
                        {
                            newRoom.isOccupied = existingRoom.isOccupied;
                            updatedRoomList.Add(newRoom);
                        }
                        else
                        {
                            updatedRoomList.Add(newRoom);
                        }
                    }
                }
            }

            if (updatedRoomList.Count > 0)
            {
                roomList = updatedRoomList;
            }
        }
    }

    public static void NotifyRoomsUpdated(GameObject[] rooms)
    {
        OnRoomsUpdated?.Invoke(rooms);
    }
    #endregion

    #region 시간 기반 행동 결정
    private void DetermineBehaviorByTime()
    {
        if (timeSystem == null)
        {
            FallbackBehavior();
            return;
        }

        int hour = timeSystem.CurrentHour;
        int minute = timeSystem.CurrentMinute;

        // 정각마다 이전 행동 정리 (애니메이션, 오브젝트, 점유 해제 등)
        CleanupCurrentActivity();

        // 17:00 이후 방 없는 AI는 강제 디스폰
        // currentRoomIndex == -1이면 방 없는 AI
        if (hour >= 17 && currentRoomIndex == -1)
        {
            Handle17OClockForcedDespawn();
            return;
        }

        if (hour >= 0 && hour < 9)
        {
            // 0:00 ~ 9:00 - 무조건 침대에서 자야 함
            if (currentRoomIndex != -1)
            {
                // 0시에 낮잠 중이면 깨우고 야간 수면으로 전환
                if (hour == 0 && isNapping && isSleeping)
                {
                    // 낮잠 타이머 중단
                    if (nappingCoroutine != null)
                    {
                        StopCoroutine(nappingCoroutine);
                        nappingCoroutine = null;
                    }
                    
                    // 애니메이션 끄기
                    if (animator != null)
                    {
                        animator.SetBool("BedTime", false);
                    }
                    
                    // NavMeshAgent 다시 활성화
                    if (agent != null)
                    {
                        agent.enabled = true;
                    }
                    
                    // 수면 상태 해제
                    isSleeping = false;
                    isNapping = false;
                    currentBedTransform = null;
                    
                    // 이제 야간 수면 시작
                    if (FindBedInCurrentRoom(out Transform bedTransform))
                    {
                        currentBedTransform = bedTransform;
                        preSleepPosition = transform.position;
                        preSleepRotation = transform.rotation;
                        TransitionToState(AIState.MovingToBed);
                    }
                    else
                    {
                        TransitionToState(AIState.RoomWandering);
                    }
                }
                // 자고 있지 않으면 무조건 침대로 이동 (0~8시 모두 적용)
                else if (!isSleeping && currentState != AIState.MovingToBed && currentState != AIState.Sleeping)
                {
                    // 0시 정각에는 랜덤 딜레이로 분산 처리 (성능 최적화)
                    if (hour == 0 && delayedBedMovementCoroutine == null)
                    {
                        Debug.Log($"<color=yellow>[0시 침대 이동] {gameObject.name}: 랜덤 딜레이 코루틴 시작 (방: {currentRoomIndex})</color>");
                        delayedBedMovementCoroutine = StartCoroutine(DelayedBedMovement());
                    }
                    // 1~8시는 즉시 침대로 이동
                    else if (hour >= 1)
                {
                    if (FindBedInCurrentRoom(out Transform bedTransform))
                    {
                        currentBedTransform = bedTransform;
                        preSleepPosition = transform.position;
                        preSleepRotation = transform.rotation;
                        isNapping = false; // 야간 수면 (낮잠 아님)
                            Debug.Log($"<color=cyan>[{hour}시 침대 이동] {gameObject.name}: 즉시 침대로 이동 시작</color>");
                        TransitionToState(AIState.MovingToBed);
                    }
                    else
                    {
                            // 침대가 없으면 방 배회 (fallback)
                            Debug.LogWarning($"[{hour}시 침대 없음] {gameObject.name}: 방에 침대가 없어 배회");
                        TransitionToState(AIState.RoomWandering);
                    }
                }
                }
                // 이미 자고 있거나 침대로 이동 중이면 아무것도 안 함
            }
            else
            {
                // 0~9시에 방 없는 AI는 존재하면 안 됨 (17시에 이미 퇴장했어야 함)
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00
            if (currentRoomIndex != -1)
            {
                // 9시에 야간 수면 중인 AI를 깨움 (0~9시는 야간 수면만 존재)
                if (hour == 9 && isSleeping)
                {
                    WakeUp();
                    // WakeUp()에서 이미 ReportingRoomQueue로 전환하므로 여기서 return
                    return;
                }
                
                // 수면이 아닌 경우 방 사용 완료 보고
                // 단, 이미 보고 중이거나 대기열에 있으면 전환하지 않음
                if (!isSleeping && currentState != AIState.ReportingRoomQueue && 
                    currentState != AIState.ReportingRoom && !isInQueue && !isWaitingForService)
                {
                    // 9시에 AI들이 동시에 몰리지 않도록 랜덤 딜레이 추가
                    StartCoroutine(DelayedReportingRoomQueue());
                }
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 11 && hour < 24)
        {
            // 11:00 ~ 24:00
            if (currentRoomIndex == -1)
            {
                // ========== 방 없는 AI (11:00~17:00만 존재) ==========
                if (hour >= 11 && hour <= 16)
                {
                    // 시설 사용 중이면 다음 정각에 종료
                    if (isUsingHealth)
                    {
                        FinishUsingHealth();
                        return; // 이번 정각은 종료하는 것으로 처리
                    }
                    if (isUsingWedding)
                    {
                        FinishUsingWedding();
                        return; // 이번 정각은 종료하는 것으로 처리
                    }
                    if (isUsingLounge)
                    {
                        FinishUsingLounge();
                        return;
                    }
                    if (isUsingHall)
                    {
                        FinishUsingHall();
                        return;
                    }
                    if (isUsingSauna)
                    {
                        FinishUsingSauna();
                        return;
                    }
                    if (isUsingCafe)
                    {
                        FinishUsingCafe();
                        return;
                    }
                    if (isUsingBath)
                    {
                        FinishUsingBath();
                        return;
                    }
                    if (isUsingHos)
                    {
                        FinishUsingHos();
                        return;
                    }
                    
                    // 확률 행동
                    float randomValue = Random.value;
                        
                        // 25% 식사
                        if (randomValue < 0.25f)
                        {
                            if (!TryFindAvailableKitchen())
                            {
                                TransitionToState(AIState.Wandering);
                            }
                        }
                        // 45% 시설 이용 (동적 분배)
                        else if (randomValue < 0.70f) // 0.25 + 0.45 = 0.70
                        {
                            // 시설 존재 여부 확인
                            bool counterAvailable = counterManager != null; // 카운터 (방 배정 시도)
                            bool sunbedAvailable = hour >= 11 && hour <= 15; // 선베드는 11~15시만
                            bool healthExists = GameObject.FindGameObjectsWithTag("Health").Length > 0;
                            bool weddingExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Wedding").Length > 0;
                            bool loungeExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Lounge").Length > 0;
                            bool hallExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Hall").Length > 0;
                            bool saunaExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Sauna").Length > 0;
                            bool cafeExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Cafe").Length > 0;
                            bool bathExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Bath").Length > 0;
                            bool hosExists = hour >= 11 && hour <= 22 && GameObject.FindGameObjectsWithTag("Hos").Length > 0;
                            
                            // 시설 개수 카운트
                            int facilityCount = 0;
                            if (counterAvailable) facilityCount++; // 카운터 추가
                            if (sunbedAvailable) facilityCount++;
                            if (healthExists) facilityCount++;
                            if (weddingExists) facilityCount++;
                            if (loungeExists) facilityCount++;
                            if (hallExists) facilityCount++;
                            if (saunaExists) facilityCount++;
                            if (cafeExists) facilityCount++;
                            if (bathExists) facilityCount++;
                            if (hosExists) facilityCount++;
                            
                            // 시설이 하나도 없으면 배회
                            if (facilityCount == 0)
                            {
                                TransitionToState(AIState.Wandering);
                            }
                            else
                            {
                                // 45%를 시설 개수로 동적 분배
                                float facilityRandom = Random.value;
                                float probabilityPerFacility = 1.0f / facilityCount;
                                int currentIndex = 0;
                                
                                // 카운터 (방 배정 시도)
                                if (counterAvailable && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    TransitionToState(AIState.MovingToQueue);
                                }
                                // 선베드 (11~15시만)
                                else if (sunbedAvailable && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableSunbed())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 운동 시설 (유료)
                                else if (healthExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableHealth())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 예식장 (11~22시, 유료)
                                else if (weddingExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableWedding())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 라운지 (11~22시, 유료)
                                else if (loungeExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableLounge())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 연회장 (11~22시, 유료)
                                else if (hallExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableHall())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 사우나 (11~22시, 유료)
                                else if (saunaExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableSauna())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 카페 (11~22시, 유료)
                                else if (cafeExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableCafe())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // Bath (11~22시, 유료)
                                else if (bathExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableBath())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // Hos 고급식당 (11~22시, 유료)
                                else if (hosExists && facilityRandom < probabilityPerFacility * ++currentIndex)
                                {
                                    if (!TryFindAvailableHos())
                                    {
                                        TransitionToState(AIState.Wandering);
                                    }
                                }
                                // 나머지는 배회
                                else
                                {
                                    TransitionToState(AIState.Wandering);
                                }
                            }
                        }
                    // 5% 퇴장
                    else if (randomValue < 0.75f) // 0.70 + 0.05 = 0.75
                    {
                        TransitionToState(AIState.ReturningToSpawn);
                    }
                    // 25% 배회
                    else // 0.75 ~ 1.00 = 25%
                    {
                        TransitionToState(AIState.Wandering);
                    }
                }
                else
                {
                    // 17시 이후는 강제 퇴장
                    FallbackBehavior();
                }
            }
            else
            {
                // ========== 투숙객 (11:00~24:00) ==========
                
                // 확률 행동
                float randomValue = Random.value;
                
                // 낮잠 중이면 다음 정각에 자연스럽게 깨우기
                if (isNapping && isSleeping)
                {
                    WakeUpFromNap();
                    return; // 이번 정각은 깨어나는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 운동 시설 사용 중이면 다음 정각에 종료
                if (isUsingHealth)
                {
                    FinishUsingHealth();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 예식장 사용 중이면 다음 정각에 종료
                if (isUsingWedding)
                {
                    FinishUsingWedding();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 라운지 사용 중이면 다음 정각에 종료
                if (isUsingLounge)
                {
                    FinishUsingLounge();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 연회장 사용 중이면 다음 정각에 종료
                if (isUsingHall)
                {
                    FinishUsingHall();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 사우나 사용 중이면 다음 정각에 종료
                if (isUsingSauna)
                {
                    FinishUsingSauna();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 카페 사용 중이면 다음 정각에 종료
                if (isUsingCafe)
                {
                    FinishUsingCafe();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // Bath 사용 중이면 다음 정각에 종료
                if (isUsingBath)
                {
                    FinishUsingBath();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // Hos 사용 중이면 다음 정각에 종료
                if (isUsingHos)
                {
                    FinishUsingHos();
                    return; // 이번 정각은 종료하는 것으로 처리, 다음 정각에 다시 행동 결정
                }
                
                // 5% 낮잠 (짧은 수면)
                if (randomValue < 0.05f)
                {
                    if (FindBedInCurrentRoom(out Transform bedTransform))
                    {
                        currentBedTransform = bedTransform;
                        preSleepPosition = transform.position;
                        preSleepRotation = transform.rotation;
                        isNapping = true; // 낮잠 플래그 설정
                        TransitionToState(AIState.MovingToBed);
                    }
                    else
                    {
                        TransitionToState(AIState.RoomWandering);
                    }
                }
                // 30% 식사
                else if (randomValue < 0.35f)
                {
                    if (!TryFindAvailableKitchen())
                    {
                        // 식당이 없으면 배회
                        if (Random.value < 0.5f)
                        {
                            TransitionToState(AIState.UseWandering);
                        }
                        else
                        {
                            TransitionToState(AIState.RoomWandering);
                        }
                    }
                }
                // 35% 시설 이용 (욕조 + 선베드 + 운동 시설 + 예식장 + 라운지 + 연회장 + 사우나)
                else if (randomValue < 0.70f)
                {
                    // 라운지/연회장/사우나/카페/Bath/Hos 존재 여부 확인 (11~22시만)
                    bool loungeExists = false;
                    bool hallExists = false;
                    bool saunaExists = false;
                    bool cafeExists = false;
                    bool bathExists = false;
                    bool hosExists = false;
                    
                    if (hour >= 11 && hour <= 22)
                    {
                        loungeExists = GameObject.FindGameObjectsWithTag("Lounge").Length > 0;
                        hallExists = GameObject.FindGameObjectsWithTag("Hall").Length > 0;
                        saunaExists = GameObject.FindGameObjectsWithTag("Sauna").Length > 0;
                        cafeExists = GameObject.FindGameObjectsWithTag("Cafe").Length > 0;
                        bathExists = GameObject.FindGameObjectsWithTag("Bath").Length > 0;
                        hosExists = GameObject.FindGameObjectsWithTag("Hos").Length > 0;
                    }
                    
                    // 시설 개수에 따라 확률 조정
                    int facilityCount = 4; // 기본: 욕조, 선베드, 운동 시설, 예식장
                    if (loungeExists) facilityCount++;
                    if (hallExists) facilityCount++;
                    if (saunaExists) facilityCount++;
                    if (cafeExists) facilityCount++;
                    if (bathExists) facilityCount++;
                    if (hosExists) facilityCount++;
                    
                    float facilityRandom = Random.value;
                    float probabilityPerFacility = 1.0f / facilityCount;
                    
                    // 욕조
                    if (facilityRandom < probabilityPerFacility)
                    {
                        if (FindBathtubInCurrentRoom(out Transform bathtubTransform))
                        {
                            currentBathtubTransform = bathtubTransform;
                            preBathtubPosition = transform.position;
                            preBathtubRotation = transform.rotation;
                            TransitionToState(AIState.MovingToBathtub);
                        }
                        else
                        {
                            // 욕조 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                    }
                    // 선베드 (11~22시, 방 주인만)
                    else if (facilityRandom < probabilityPerFacility * 2)
                    {
                        if (hour >= 11 && hour <= 22)
                        {
                            // ✅ TryFindAvailableSunbed()는 방 주인이면 방 안 선베드만 찾음
                            if (!TryFindAvailableSunbed())
                            {
                                // 선베드 없으면 배회
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            // 선베드 시간 아니면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                    }
                    // 운동 시설
                    else if (facilityRandom < probabilityPerFacility * 3)
                    {
                        if (FindHealthInCurrentRoom(out Transform healthTransform))
                        {
                            currentHealthTransform = healthTransform;
                            preHealthPosition = transform.position;
                            preHealthRotation = transform.rotation;
                            TransitionToState(AIState.MovingToHealth);
                        }
                        else
                        {
                            // 운동 시설 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                    }
                    // 예식장 (11~22시만)
                    else if (facilityRandom < probabilityPerFacility * 4)
                    {
                        if (hour >= 11 && hour <= 22)
                        {
                            if (!FindWeddingInCurrentRoom(out Transform weddingTransform))
                            {
                                // 예식장 없으면 배회
                                if (Random.value < 0.5f)
                                {
                                    TransitionToState(AIState.UseWandering);
                                }
                                else
                                {
                                    TransitionToState(AIState.RoomWandering);
                                }
                            }
                            else
                            {
                                currentWeddingTransform = weddingTransform;
                                preWeddingPosition = transform.position;
                                preWeddingRotation = transform.rotation;
                                TransitionToState(AIState.MovingToWedding);
                            }
                        }
                        else
                        {
                            // 예식장 시간 아니면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                    }
                    // 라운지 (11~22시만, 존재할 때만)
                    else if (loungeExists && facilityRandom < probabilityPerFacility * 5)
                    {
                        if (!FindLoungeInCurrentRoom(out Transform loungeTransform))
                        {
                            // 라운지 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentLoungeTransform = loungeTransform;
                            preLoungePosition = transform.position;
                            preLoungeRotation = transform.rotation;
                            TransitionToState(AIState.MovingToLounge);
                        }
                    }
                    // 연회장 (11~22시만, 존재할 때만)
                    else if (hallExists && facilityRandom < probabilityPerFacility * 6)
                    {
                        if (!FindHallInCurrentRoom(out Transform hallTransform))
                        {
                            // 연회장 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentHallTransform = hallTransform;
                            preHallPosition = transform.position;
                            preHallRotation = transform.rotation;
                            TransitionToState(AIState.MovingToHall);
                        }
                    }
                    // 사우나 (11~22시만, 존재할 때만)
                    else if (saunaExists && facilityRandom < probabilityPerFacility * 7)
                    {
                        if (!FindSaunaInCurrentRoom(out Transform saunaTransform))
                        {
                            // 사우나 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentSaunaTransform = saunaTransform;
                            preSaunaPosition = transform.position;
                            preSaunaRotation = transform.rotation;
                            TransitionToState(AIState.MovingToSauna);
                        }
                    }
                    // 카페 (11~22시만, 존재할 때만)
                    else if (cafeExists && facilityRandom < probabilityPerFacility * 8)
                    {
                        if (!FindCafeInCurrentRoom(out Transform cafeTransform))
                        {
                            // 카페 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentCafeTransform = cafeTransform;
                            preCafePosition = transform.position;
                            preCafeRotation = transform.rotation;
                            TransitionToState(AIState.MovingToCafe);
                        }
                    }
                    // Bath (11~22시만, 존재할 때만)
                    else if (bathExists && facilityRandom < probabilityPerFacility * 9)
                    {
                        if (!FindBathInCurrentRoom(out Transform bathTransform))
                        {
                            // Bath 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentBathTransform = bathTransform;
                            preBathPosition = transform.position;
                            preBathRotation = transform.rotation;
                            TransitionToState(AIState.MovingToBath);
                        }
                    }
                    // Hos 고급식당 (11~22시만, 존재할 때만)
                    else if (hosExists && facilityRandom < probabilityPerFacility * 10)
                    {
                        if (!FindHosInCurrentRoom(out Transform hosTransform))
                        {
                            // Hos 없으면 배회
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.UseWandering);
                            }
                            else
                            {
                                TransitionToState(AIState.RoomWandering);
                            }
                        }
                        else
                        {
                            currentHosTransform = hosTransform;
                            preHosPosition = transform.position;
                            preHosRotation = transform.rotation;
                            TransitionToState(AIState.MovingToHos);
                        }
                    }
                    // 나머지는 배회
                    else
                    {
                        if (Random.value < 0.5f)
                        {
                            TransitionToState(AIState.UseWandering);
                        }
                        else
                        {
                            TransitionToState(AIState.RoomWandering);
                        }
                    }
                }
                // 30% 배회
                else
                {
                    // 30% 중 절반씩 분배
                    if (Random.value < 0.5f)
                    {
                        TransitionToState(AIState.UseWandering);
                    }
                    else
                    {
                        TransitionToState(AIState.RoomWandering);
                    }
                }
            }
        }

        lastBehaviorUpdateHour = hour;
    }

    /// <summary>
    /// 17:00에 방 없는 AI를 강제로 디스폰시킵니다.
    /// </summary>
    private void Handle17OClockForcedDespawn()
    {
        // 투숙객(실제 방이 할당된 AI)만 체크
        // currentRoomIndex != -1이면 투숙객
        if (currentRoomIndex != -1)
        {
            return;
        }
        
        // 선베드 사용 중이면 강제 종료
        if (isUsingSunbed || currentState == AIState.MovingToSunbed || currentState == AIState.UsingSunbed)
        {
            if (sunbedCoroutine != null)
            {
                StopCoroutine(sunbedCoroutine);
                sunbedCoroutine = null;
            }
            
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            isUsingSunbed = false;
            // ✅ isSunbedInRoom 제거
            currentSunbedTransform = null;
        }
        
        // 헬스장 사용 중이면 강제 종료 및 점유 해제
        if (isUsingHealth || currentState == AIState.MovingToHealth || currentState == AIState.UsingHealth)
        {
            // HealthPoint 점유 해제
            if (currentHealthPoint != null)
            {
                lock (healthPointLock)
                {
                    if (occupiedHealthPoints.ContainsKey(currentHealthPoint) && occupiedHealthPoints[currentHealthPoint] == this)
                    {
                        occupiedHealthPoints.Remove(currentHealthPoint);
                    }
                }
                currentHealthPoint = null;
            }
            
            if (healthCoroutine != null)
            {
                StopCoroutine(healthCoroutine);
                healthCoroutine = null;
            }
            
            // 덤벨 비활성화 (강제 종료 시)
            if (leftDumbbell != null) leftDumbbell.SetActive(false);
            if (rightDumbbell != null) rightDumbbell.SetActive(false);
            
            if (animator != null)
            {
                animator.SetBool("Exercise", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            isUsingHealth = false;
            currentHealthTransform = null;
        }
        
        // 예식장 사용 중이면 강제 종료 및 점유 해제
        if (isUsingWedding || currentState == AIState.MovingToWedding || currentState == AIState.UsingWedding)
        {
            // WeddingPoint 점유 해제
            if (currentWeddingPoint != null)
            {
                lock (weddingPointLock)
                {
                    if (occupiedWeddingPoints.ContainsKey(currentWeddingPoint) && occupiedWeddingPoints[currentWeddingPoint] == this)
                    {
                        occupiedWeddingPoints.Remove(currentWeddingPoint);
                    }
                }
                currentWeddingPoint = null;
            }
            
            if (weddingCoroutine != null)
            {
                StopCoroutine(weddingCoroutine);
                weddingCoroutine = null;
            }
            
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            isUsingWedding = false;
            currentWeddingTransform = null;
        }
        
        // 라운지 사용 중이면 강제 종료 및 점유 해제
        if (isUsingLounge || currentState == AIState.MovingToLounge || currentState == AIState.UsingLounge)
        {
            // LoungePoint 점유 해제
            if (currentLoungePoint != null)
            {
                lock (loungePointLock)
                {
                    if (occupiedLoungePoints.ContainsKey(currentLoungePoint) && occupiedLoungePoints[currentLoungePoint] == this)
                    {
                        occupiedLoungePoints.Remove(currentLoungePoint);
                    }
                }
                currentLoungePoint = null;
            }
            
            if (loungeCoroutine != null)
            {
                StopCoroutine(loungeCoroutine);
                loungeCoroutine = null;
            }
            
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            isUsingLounge = false;
            currentLoungeTransform = null;
        }
        
        // 연회장 사용 중이면 강제 종료 및 점유 해제
        if (isUsingHall || currentState == AIState.MovingToHall || currentState == AIState.UsingHall)
        {
            // HallPoint 점유 해제
            if (currentHallPoint != null)
            {
                lock (hallPointLock)
                {
                    if (occupiedHallPoints.ContainsKey(currentHallPoint) && occupiedHallPoints[currentHallPoint] == this)
                    {
                        occupiedHallPoints.Remove(currentHallPoint);
                    }
                }
                currentHallPoint = null;
            }
            
            if (hallCoroutine != null)
            {
                StopCoroutine(hallCoroutine);
                hallCoroutine = null;
            }
            
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            isUsingHall = false;
            currentHallTransform = null;
        }

        // 사우나 사용 중이면 강제 종료
        if (isUsingSauna)
        {
            if (saunaCoroutine != null)
            {
                StopCoroutine(saunaCoroutine);
                saunaCoroutine = null;
            }
            
            if (animator != null)
            {
                if (isSaunaSitting)
                {
                    animator.SetBool("Sitting", false);
                }
                else
                {
                    animator.SetBool("BedTime", false);
                }
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            // 사우나 포인트 점유 해제
            if (currentSaunaPoint != null)
            {
                if (isSaunaSitting)
                {
                    lock (saunaSitPointLock)
                    {
                        if (occupiedSaunaSitPoints.ContainsKey(currentSaunaPoint) && occupiedSaunaSitPoints[currentSaunaPoint] == this)
                        {
                            occupiedSaunaSitPoints.Remove(currentSaunaPoint);
                        }
                    }
                }
                else
                {
                    lock (saunaDownPointLock)
                    {
                        if (occupiedSaunaDownPoints.ContainsKey(currentSaunaPoint) && occupiedSaunaDownPoints[currentSaunaPoint] == this)
                        {
                            occupiedSaunaDownPoints.Remove(currentSaunaPoint);
                        }
                    }
                }
                currentSaunaPoint = null;
            }
            
            isUsingSauna = false;
            isSaunaSitting = false;
            currentSaunaTransform = null;
        }

        // 카페 사용 중이면 강제 종료
        if (isUsingCafe)
        {
            if (cafeCoroutine != null)
            {
                StopCoroutine(cafeCoroutine);
                cafeCoroutine = null;
            }
            
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            // CafePoint 점유 해제
            if (currentCafePoint != null)
            {
                lock (cafePointLock)
                {
                    if (occupiedCafePoints.ContainsKey(currentCafePoint) && occupiedCafePoints[currentCafePoint] == this)
                    {
                        occupiedCafePoints.Remove(currentCafePoint);
                    }
                }
                currentCafePoint = null;
            }
            
            isUsingCafe = false;
            currentCafeTransform = null;
        }

        // Bath 사용 중이면 강제 종료
        if (isUsingBath)
        {
            if (bathCoroutine != null)
            {
                StopCoroutine(bathCoroutine);
                bathCoroutine = null;
            }
            
            if (animator != null)
            {
                if (isBathSitting)
                {
                    animator.SetBool("Sitting", false);
                }
                else
                {
                    animator.SetBool("BedTime", false);
                }
            }
            
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            // BathPoint 점유 해제
            if (currentBathPoint != null)
            {
                if (isBathSitting)
                {
                    lock (bathSitPointLock)
                    {
                        if (occupiedBathSitPoints.ContainsKey(currentBathPoint) && occupiedBathSitPoints[currentBathPoint] == this)
                        {
                            occupiedBathSitPoints.Remove(currentBathPoint);
                        }
                    }
                }
                else
                {
                    lock (bathDownPointLock)
                    {
                        if (occupiedBathDownPoints.ContainsKey(currentBathPoint) && occupiedBathDownPoints[currentBathPoint] == this)
                        {
                            occupiedBathDownPoints.Remove(currentBathPoint);
                        }
                    }
                }
                currentBathPoint = null;
            }
            
            isUsingBath = false;
            isBathSitting = false;
            currentBathTransform = null;
        }

        // 주방 카운터 대기 중이거나 식사 중인 AI는 강제로 종료 후 퇴장
        if (isWaitingAtKitchenCounter || currentState == AIState.MovingToKitchenCounter || currentState == AIState.WaitingAtKitchenCounter)
        {
            ForceFinishKitchenActivity();
            return;  // ForceFinishKitchenActivity()에서 이미 디스폰 처리됨
        }
        else if (isEating || currentState == AIState.Eating || currentState == AIState.MovingToChair)
        {
            // 식사 관련 모든 상태를 확실하게 정리
            ForceFinishEating();
            CleanupKitchenVariables(); // 추가 안전장치: 모든 식사 관련 변수 정리
        }

        // 모든 코루틴 강제 종료
        CleanupCoroutines();

        // 대기열에서 강제 제거
        if (isInQueue && counterManager != null)
        {
            counterManager.LeaveQueue(this);
            isInQueue = false;
            isWaitingForService = false;
            counterManager.ForceCleanupQueue();
        }

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
    }

    /// <summary>
    /// 방 관련 상태인지 확인합니다.
    /// </summary>
    private bool IsInRoomRelatedState()
    {
        return (currentState == AIState.UsingRoom || 
                currentState == AIState.UseWandering || 
                currentState == AIState.RoomWandering ||
                currentState == AIState.MovingToRoom ||
                currentState == AIState.MovingToBed ||
                currentState == AIState.Sleeping) && 
                currentRoomIndex != -1;
    }

    /// <summary>
    /// 11시 체크아웃 완료 후 디스폰을 처리합니다. (더 이상 사용 안 함 - 17시 디스폰으로 통합)
    /// </summary>
    private void HandleCheckoutDespawn()
    {
        // 11시에 강제 디스폰하지 않음
        // 17시에 방 없는 AI만 디스폰됨
    }

    /// <summary>
    /// 중요한 상태인지 확인합니다 (행동 재결정을 방해하면 안 되는 상태).
    /// </summary>
    private bool IsInCriticalState()
    {
        bool isCritical = currentState == AIState.WaitingInQueue || 
               currentState == AIState.MovingToQueue || 
               currentState == AIState.MovingToRoom || 
               currentState == AIState.MovingToBed ||
               currentState == AIState.Sleeping ||
               currentState == AIState.MovingToSunbed ||
               currentState == AIState.UsingSunbed ||
               currentState == AIState.ReportingRoom ||
               currentState == AIState.ReportingRoomQueue ||
               currentState == AIState.ReturningToSpawn ||
               currentState == AIState.MovingToKitchenCounter ||  // ✅ 주방 카운터로 이동 중
               currentState == AIState.WaitingAtKitchenCounter || // ✅ 주방 카운터 대기 중
               isInQueue || isWaitingForService || isWaitingAtKitchenCounter; // ✅ 주방 카운터 대기 플래그
               // isScheduledForDespawn 플래그 제거: 11시에 강제 디스폰하지 않음
        
        return isCritical;
    }

    /// <summary>
    /// 방 사용 중인 AI의 시간별 내부/외부 배회를 재결정합니다.
    /// </summary>
    private void RedetermineRoomBehavior()
    {
        if (!IsInRoomRelatedState()) return;

        int hour = timeSystem.CurrentHour;
        
        // 0시에는 무조건 내부 배회
        if (hour == 0)
        {
            if (currentState == AIState.UseWandering)
            {
                TransitionToState(AIState.RoomWandering);
            }
        }
        // 11-17시, 17-24시에는 50/50 확률로 재결정
        else if ((hour >= 11 && hour < 17) || (hour >= 17 && hour < 24))
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                if (currentState != AIState.UseWandering)
                {
                    TransitionToState(AIState.UseWandering);
                }
            }
            else
            {
                if (currentState != AIState.RoomWandering)
                {
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
    }

    private void FallbackBehavior()
    {
        if (counterPosition == null || counterManager == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
            }
        }
        else
        {
            float randomValue = Random.value;
            if (randomValue < 0.4f)
            {
                TransitionToState(AIState.Wandering);
            }
            else
            {
                TransitionToState(AIState.MovingToQueue);
            }
        }
    }
    #endregion

    #region 업데이트 및 상태 머신
    void Update()
    {
        // NavMeshAgent 꺼진 상태에서는 NavMesh 체크 건너뛰기
        // (선베드, 수면, 식사, 라운지, 연회장, 사우나, 헬스장, 웨딩, 카페, 욕실, Hos, 욕조 사용 중)
        if (!isUsingSunbed && !isSleeping && !isEating && !isUsingLounge && !isUsingHall && !isUsingSauna 
            && !isUsingHealth && !isUsingWedding && !isUsingCafe && !isUsingBath && !isUsingHos && !isUsingBathtub)
        {
            if (!agent.isOnNavMesh)
            {
                ReturnToPool();
                return;
            }
        }
        
        // Inspector에서 설정이 변경되면 전역 설정 업데이트
        if (globalShowDebugUI != debugUIEnabled)
        {
            globalShowDebugUI = debugUIEnabled;
        }

        // 주기적으로 오브젝트 유효성 검사 (3초마다)
        CheckObjectValidity();

        // 애니메이션 상태 타임아웃 체크
        CheckAnimationStateTimeout();

        // 애니메이션 파라미터 제어
        if (animator != null)
        {
            // ✅ 시설 사용 중에는 애니메이션 파라미터를 건드리지 않음
            bool isUsingFacility = isUsingHealth || isUsingWedding || isUsingLounge || isUsingHall || 
                                   isUsingSauna || isUsingCafe || isUsingBath || isUsingHos || isUsingBathtub;
            
            if (!isUsingFacility)
            {
                // Moving 파라미터: 이동 중일 때 true (수면 중이나 선베드 사용 중, 식사 중, 대기열 대기 중이 아닐 때)
                // 대기열에 있거나 서비스 대기 중일 때는 Moving 애니메이션 비활성화
                bool isMoving = agent.velocity.magnitude > 0.1f && 
                               !isSleeping && 
                               !isUsingSunbed && 
                               !isEating && 
                               !isInQueue && 
                               !isWaitingForService &&
                               !isWaitingAtKitchenCounter;
            animator.SetBool("Moving", isMoving);
            
            // BedTime 파라미터: 수면 중이거나 선베드 사용 중일 때 true
            animator.SetBool("BedTime", isSleeping || isUsingSunbed);
            
            // Eating 파라미터: 식사 중일 때 true
            animator.SetBool("Eating", isEating);
            }
        }

        // 시간 기반 행동 갱신
        if (timeSystem != null)
        {
            int hour = timeSystem.CurrentHour;
            int minute = timeSystem.CurrentMinute;

            // 16시 선베드 강제 종료 체크 (15시까지만 사용 가능)
            if (hour == 16 && minute == 0 && lastBehaviorUpdateHour != hour)
            {
                if (isUsingSunbed || currentState == AIState.MovingToSunbed || currentState == AIState.UsingSunbed)
                {
                    ForceFinishUsingSunbed();
                    lastBehaviorUpdateHour = hour;
                    // 선베드 종료 후 다음 행동 결정
                    DetermineBehaviorByTime();
                    return;
                }
            }
            
            // 17:00~20:00 방 없는 AI 지속적으로 퇴장 체크 (배속 대응)
            if (hour >= 17 && hour <= 20 && lastBehaviorUpdateHour != hour)
            {
                // 방이 없는 AI는 무조건 퇴장!
                if (currentRoomIndex == -1)
                {
                    Handle17OClockForcedDespawn();
                    lastBehaviorUpdateHour = hour;
                    return;
                }
            }

            // 11시 체크아웃 완료 후 디스폰 체크 - 비활성화됨 (17시 디스폰으로 통합)
            // 11시에는 더 이상 강제 디스폰하지 않음
            /*if (hour == 11 && minute == 0 && lastBehaviorUpdateHour != hour)
            {
                HandleCheckoutDespawn();
                lastBehaviorUpdateHour = hour;
                return;
            }*/
            
            // 0시가 되면 모든 AI의 lastBehaviorUpdateHour 리셋 (날짜 변경)
            if (hour == 0 && lastBehaviorUpdateHour == 23)
            {
                lastBehaviorUpdateHour = -1;
            }

            // 9시에 수면 중인 AI 특별 처리 (다른 체크보다 먼저)
            if (hour == 9 && minute == 0 && isSleeping && lastBehaviorUpdateHour != hour)
            {
                WakeUp();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 매시간 행동 재결정 (모든 AI 포함)
            if (minute == 0 && hour != lastBehaviorUpdateHour)
            {
                // 중요한 상태가 아닌 경우에만 행동 재결정
                if (!IsInCriticalState())
                {
                    DetermineBehaviorByTime();
                    lastBehaviorUpdateHour = hour;
                }
                // 방 사용 중인 AI도 매시간 내부/외부 배회 재결정
                else if (IsInRoomRelatedState())
                {
                    RedetermineRoomBehavior();
                    lastBehaviorUpdateHour = hour;
                }
                else
                {
                    lastBehaviorUpdateHour = hour;
                }
            }
        }

        switch (currentState)
        {
            case AIState.Wandering:
                break;
            case AIState.MovingToQueue:
            case AIState.WaitingInQueue:
            case AIState.ReportingRoomQueue:
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1 && currentRoomIndex < roomList.Count)
                {
                    Bounds roomBounds = roomList[currentRoomIndex].bounds;
                    if (agent != null && agent.enabled && agent.isOnNavMesh && 
                        !agent.pathPending && agent.remainingDistance < arrivalDistance && roomBounds.Contains(transform.position))
                    {
                        StartCoroutine(UseRoom());
                    }
                }
                break;
            case AIState.MovingToBed:
                // 침대로 이동 중 - MoveToBedBehavior 코루틴에서 처리
                break;
            case AIState.Sleeping:
                // 수면 중 - 별도 처리 불필요
                break;
            case AIState.MovingToSunbed:
                // 선베드로 이동 중 - MoveToSunbedBehavior 코루틴에서 처리
                break;
            case AIState.UsingSunbed:
                // 선베드 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToBathtub:
                // 욕조로 이동 중 - MoveToBathtubBehavior 코루틴에서 처리
                break;
            case AIState.UsingBathtub:
                // 욕조 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToHealth:
                // 운동 시설로 이동 중 - MoveToHealthBehavior 코루틴에서 처리
                break;
            case AIState.UsingHealth:
                // 운동 시설 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToWedding:
                // 예식장으로 이동 중 - MoveToWeddingBehavior 코루틴에서 처리
                break;
            case AIState.UsingWedding:
                // 예식장 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToLounge:
                // 라운지로 이동 중 - MoveToLoungeBehavior 코루틴에서 처리
                break;
            case AIState.UsingLounge:
                // 라운지 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToHall:
                // 연회장으로 이동 중 - MoveToHallBehavior 코루틴에서 처리
                break;
            case AIState.UsingHall:
                // 연회장 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToSauna:
                // 사우나로 이동 중 - MoveToSaunaBehavior 코루틴에서 처리
                break;
            case AIState.UsingSauna:
                // 사우나 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToCafe:
                // 카페로 이동 중 - MoveToCafeBehavior 코루틴에서 처리
                break;
            case AIState.UsingCafe:
                // 카페 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToBath:
                // Bath로 이동 중 - MoveToBathBehavior 코루틴에서 처리
                break;
            case AIState.UsingBath:
                // Bath 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToHos:
                // Hos로 이동 중 - MoveToHosBehavior 코루틴에서 처리
                break;
            case AIState.UsingHos:
                // Hos 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToKitchenCounter:
                // 주방 카운터로 이동 중 - MoveToKitchenCounterBehavior 코루틴에서 처리
                break;
            case AIState.WaitingAtKitchenCounter:
                // 주방 카운터에서 대기 중 - 별도 처리 불필요
                break;
            case AIState.MovingToChair:
                // 의자로 이동 중 - MoveToChairBehavior 코루틴에서 처리
                break;
            case AIState.Eating:
                // 식사 중 - 별도 처리 불필요
                break;
            case AIState.UsingRoom:
            case AIState.RoomWandering:
                break;
            case AIState.ReportingRoom:
                break;
            case AIState.ReturningToSpawn:
                if (agent != null && agent.enabled && agent.isOnNavMesh && 
                    !agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private int queueRetryCount = 0; // 대기열 재시도 횟수
    private const int maxQueueRetries = 5; // 최대 재시도 횟수
    private Coroutine delayedBedMovementCoroutine; // 지연된 침대 이동 코루틴
    
    /// <summary>
    /// 9시에 AI들이 동시에 몰리지 않도록 랜덤 딜레이 후 ReportingRoomQueue로 전환
    /// </summary>
    private IEnumerator DelayedReportingRoomQueue()
    {
        // 0-10초 사이의 랜덤 딜레이
        float delay = Random.Range(0f, 10f);
        
        yield return new WaitForSeconds(delay);
        
        // 딜레이 중에 상태가 변경되지 않았는지 확인
        if (currentRoomIndex != -1 && currentState != AIState.ReportingRoomQueue && 
            currentState != AIState.ReportingRoom && !isInQueue && !isWaitingForService)
        {
            TransitionToState(AIState.ReportingRoomQueue);
        }
    }
    
    /// <summary>
    /// 0시에 AI들이 동시에 침대로 이동하지 않도록 랜덤 딜레이 추가 (성능 최적화)
    /// </summary>
    private IEnumerator DelayedBedMovement()
    {
        // 0-5초 사이의 랜덤 딜레이
        float delay = Random.Range(0f, 5f);
        
        Debug.Log($"<color=yellow>[딜레이 대기] {gameObject.name}: {delay:F1}초 후 침대로 이동 예정</color>");
        yield return new WaitForSeconds(delay);
        
        // 딜레이 후에도 여전히 0~9시 사이인지 확인
        if (timeSystem != null && timeSystem.CurrentHour >= 0 && timeSystem.CurrentHour < 9)
        {
            // 딜레이 중에 상태가 변경되지 않았는지 확인
            if (currentRoomIndex != -1 && !isSleeping && 
                currentState != AIState.MovingToBed && currentState != AIState.Sleeping)
            {
                // 직접 침대로 이동 (DetermineBehaviorByTime 재호출하지 않음)
                if (FindBedInCurrentRoom(out Transform bedTransform))
                {
                    currentBedTransform = bedTransform;
                    preSleepPosition = transform.position;
                    preSleepRotation = transform.rotation;
                    isNapping = false;
                    Debug.Log($"<color=lime>[딜레이 완료] {gameObject.name}: 침대로 이동 시작! (대기시간: {delay:F1}초)</color>");
                    TransitionToState(AIState.MovingToBed);
                }
                else
                {
                    // 침대가 없으면 방 배회
                    Debug.LogWarning($"[딜레이 후 침대 없음] {gameObject.name}: 침대를 찾지 못함");
                    TransitionToState(AIState.RoomWandering);
                }
            }
            else
            {
                Debug.Log($"<color=gray>[딜레이 스킵] {gameObject.name}: 이미 침대로 이동 중이거나 자고 있음 (isSleeping: {isSleeping}, state: {currentState})</color>");
            }
        }
        else
        {
            int currentHour = timeSystem != null ? timeSystem.CurrentHour : -1;
            Debug.LogWarning($"[딜레이 취소] {gameObject.name}: 시간이 지나서 취소됨 (현재: {currentHour}시)");
        }
        
        delayedBedMovementCoroutine = null;
    }
    
    private IEnumerator QueueBehavior()
    {
        if (counterManager == null || counterPosition == null)
        {
            queueRetryCount = 0; // 재시도 횟수 초기화
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
            }
            yield break;
        }

        if (!counterManager.TryJoinQueue(this))
        {
            queueRetryCount++;
            
            // 최대 재시도 횟수 초과 시
            if (queueRetryCount >= maxQueueRetries)
            {
                queueRetryCount = 0;
                
                // ReportingRoomQueue 상태였다면 방을 반납하고 배회
                if (currentState == AIState.ReportingRoomQueue)
                {
                    if (currentRoomIndex != -1)
                    {
                        lock (lockObject)
                        {
                            if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                            {
                                roomList[currentRoomIndex].isOccupied = false;
                            }
                            currentRoomIndex = -1;
                        }
                    }
                }
                
                DetermineBehaviorByTime();
                yield break;
            }
            
            // ReportingRoomQueue 상태인 경우 재시도 (점진적으로 대기 시간 증가)
            if (currentState == AIState.ReportingRoomQueue)
            {
                float waitTime = Random.Range(3f, 7f) * queueRetryCount; // 재시도 횟수에 비례하여 대기 시간 증가
                yield return new WaitForSeconds(waitTime);
                queueCoroutine = StartCoroutine(QueueBehavior());
                yield break;
            }
            
            if (currentRoomIndex == -1)
            {
                queueRetryCount = 0;
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    wanderingCoroutine = StartCoroutine(WanderingBehavior());
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                }
            }
            else
            {
                float waitTime = Random.Range(2f, 4f) * queueRetryCount;
                yield return new WaitForSeconds(waitTime);
                queueCoroutine = StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        // 대기열 진입 성공 시 재시도 횟수 초기화
        queueRetryCount = 0;

        isInQueue = true;
        
        // 여기서 TransitionToState를 호출하면 안됨! 현재 코루틴이 중지되고 재시작되는 무한 루프 발생!
        // 대신 상태만 직접 변경
        if (currentState != AIState.ReportingRoomQueue && currentState != AIState.WaitingInQueue)
        {
            currentState = AIState.WaitingInQueue;
            currentDestination = GetStateDescription(AIState.WaitingInQueue);
        }

        bool hasArrivedAtQueue = false; // 대기열 위치 도착 플래그

        while (isInQueue)
        {
            // 17시 체크 - 방 없는 AI만 디스폰 (투숙객은 체크아웃 진행)
            if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
            {
                Handle17OClockForcedDespawn();
                yield break;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && 
                !agent.pathPending && agent.remainingDistance < arrivalDistance)
            {
                // ✅ 대기열 위치에 처음 도착했을 때만 위치/회전 설정
                if (!hasArrivedAtQueue)
                {
                    hasArrivedAtQueue = true;
                    
                    // 회전만 설정 (Y값 문제 방지를 위해 위치는 NavMesh에 맡김)
                if (targetQueueRotation != Quaternion.identity)
                {
                    transform.rotation = targetQueueRotation;
                }
                
                    // NavMeshAgent 완전 정지
                agent.isStopped = true;
                agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }
                
                if (counterManager.CanReceiveService(this))
                {
                    counterManager.StartService(this);
                    isWaitingForService = true;

                    while (isWaitingForService)
                    {
                        // 서비스 대기 중에도 17시 체크 - 방 없는 AI만 디스폰
                        if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
                        {
                            Handle17OClockForcedDespawn();
                            yield break;
                        }
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        // ReportRoomVacancy 코루틴이 완전히 끝날 때까지 대기 (결제 처리 보장)
                        yield return StartCoroutine(ReportRoomVacancy());
                    }
                    else if (currentRoomIndex != -1)
                    {
                        roomList[currentRoomIndex].isOccupied = false;
                        currentRoomIndex = -1;
                        TransitionToState(AIState.ReturningToSpawn);
                        agent.SetDestination(spawnPoint.position);
                    }
                    else
                    {
                        if (TryAssignRoom())
                        {
                            // 방 배정 후 즉시 결제 정보 등록 (방 사용 전에 0시가 되어도 결제되도록)
                            var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
                            var roomManager = FindFirstObjectByType<RoomManager>();
                            if (roomManager != null && room != null)
                            {
                                roomManager.ReportRoomUsage(gameObject.name, room);
                            }
                            
                            TransitionToState(AIState.MovingToRoom);
                            agent.SetDestination(roomList[currentRoomIndex].transform.position);
                        }
                        else
                        {
                            float randomValue = Random.value;
                            if (randomValue < 0.5f)
                            {
                                TransitionToState(AIState.Wandering);
                                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                            }
                            else
                            {
                                TransitionToState(AIState.ReturningToSpawn);
                                agent.SetDestination(spawnPoint.position);
                            }
                        }
                    }
                    
                    // 서비스 완료 후 대기열에서 나가기
                    isInQueue = false;
                    if (counterManager != null)
                    {
                        counterManager.LeaveQueue(this);
                    }
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private bool TryAssignRoom()
    {
        lock (lockObject)
        {
            // ✅ isSunbedRoom 필터링 제거 - 모든 방은 일반 방으로 처리
            var availableRooms = roomList.Select((room, index) => new { room, index })
                                         .Where(r => !r.room.isOccupied && 
                                                    !r.room.isBeingCleaned)
                                         .Select(r => r.index)
                                         .ToList();

            if (availableRooms.Count == 0)
            {
                return false;
            }

            int selectedRoomIndex = availableRooms[Random.Range(0, availableRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied && 
                !roomList[selectedRoomIndex].isBeingCleaned)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                return true;
            }

            return false;
        }
    }
    #endregion

    #region 상태 전환
    private void TransitionToState(AIState newState)
    {
        AIState oldState = currentState;
        
        CleanupCoroutines();

        // 식사 관련 상태에서 다른 상태로 전환 시 확실하게 정리
        if ((oldState == AIState.Eating || oldState == AIState.MovingToChair || oldState == AIState.MovingToKitchenCounter || oldState == AIState.WaitingAtKitchenCounter) &&
            (newState != AIState.Eating && newState != AIState.MovingToChair && newState != AIState.MovingToKitchenCounter && newState != AIState.WaitingAtKitchenCounter))
        {
            // Pasta 오브젝트 강제 비활성화
            if (pastaObject != null && pastaObject.activeSelf)
            {
                pastaObject.SetActive(false);
            }
            
            // 식사 도구 강제 비활성화
            if (currentEatingUtensil != null && currentEatingUtensil.activeSelf)
            {
                currentEatingUtensil.SetActive(false);
                currentEatingUtensil = null;
            }
            
            // 애니메이션 강제 종료
            if (animator != null)
            {
                animator.SetBool("PickUp", false);
                animator.SetBool("Eating", false);
                animator.SetBool("Sitting", false);
            }
        }

        // 이동이 필요한 상태로 전환될 때 NavMeshAgent 활성화 확인
        if (newState == AIState.Wandering || 
            newState == AIState.UseWandering || 
            newState == AIState.RoomWandering ||
            newState == AIState.MovingToQueue ||
            newState == AIState.MovingToRoom ||
            newState == AIState.ReturningToSpawn)
        {
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
        }

        currentState = newState;
        currentDestination = GetStateDescription(newState);

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                queueCoroutine = StartCoroutine(QueueBehavior());
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1)
                {
                    agent.SetDestination(roomList[currentRoomIndex].transform.position);
                }
                break;
            case AIState.MovingToBed:
                if (currentBedTransform != null)
                {
                    sleepingCoroutine = StartCoroutine(MoveToBedBehavior());
                }
                break;
            case AIState.Sleeping:
                // 수면 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToSunbed:
                if (currentSunbedTransform != null)
                {
                    sunbedCoroutine = StartCoroutine(MoveToSunbedBehavior());
                }
                break;
            case AIState.UsingSunbed:
                // 선베드 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToBathtub:
                if (currentBathtubTransform != null)
                {
                    bathtubCoroutine = StartCoroutine(MoveToBathtubBehavior());
                }
                break;
            case AIState.UsingBathtub:
                // 욕조 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToHealth:
                if (currentHealthTransform != null)
                {
                    healthCoroutine = StartCoroutine(MoveToHealthBehavior());
                }
                break;
            case AIState.UsingHealth:
                // 운동 시설 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToWedding:
                if (currentWeddingTransform != null)
                {
                    weddingCoroutine = StartCoroutine(MoveToWeddingBehavior());
                }
                break;
            case AIState.UsingWedding:
                // 예식장 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToLounge:
                if (currentLoungeTransform != null)
                {
                    loungeCoroutine = StartCoroutine(MoveToLoungeBehavior());
                }
                break;
            case AIState.UsingLounge:
                // 라운지 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToHall:
                if (currentHallTransform != null)
                {
                    hallCoroutine = StartCoroutine(MoveToHallBehavior());
                }
                break;
            case AIState.UsingHall:
                // 연회장 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToSauna:
                if (currentSaunaTransform != null)
                {
                    saunaCoroutine = StartCoroutine(MoveToSaunaBehavior());
                }
                break;
            case AIState.UsingSauna:
                // 사우나 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToCafe:
                if (currentCafeTransform != null)
                {
                    cafeCoroutine = StartCoroutine(MoveToCafeBehavior());
                }
                break;
            case AIState.UsingCafe:
                // 카페 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToBath:
                if (currentBathTransform != null)
                {
                    bathCoroutine = StartCoroutine(MoveToBathBehavior());
                }
                break;
            case AIState.UsingBath:
                // Bath 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToHos:
                if (currentHosTransform != null)
                {
                    hosCoroutine = StartCoroutine(MoveToHosBehavior());
                }
                break;
            case AIState.UsingHos:
                // Hos 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToKitchenCounter:
                if (currentKitchenCounter != null)
                {
                    eatingCoroutine = StartCoroutine(MoveToKitchenCounterBehavior());
                }
                break;
            case AIState.WaitingAtKitchenCounter:
                // 주방 카운터에서 대기 - 별도 처리 불필요
                break;
            case AIState.MovingToChair:
                if (currentChairTransform != null)
                {
                    eatingCoroutine = StartCoroutine(MoveToChairBehavior());
                }
                break;
            case AIState.Eating:
                // 식사 상태 - 별도의 코루틴 불필요
                break;
            case AIState.ReturningToSpawn:
                if (agent != null && spawnPoint != null)
                {
                    agent.SetDestination(spawnPoint.position);
                }
                break;
            case AIState.RoomWandering:
                roomWanderingCoroutine = StartCoroutine(RoomWanderingBehavior());
                break;
            case AIState.UseWandering:
                useWanderingCoroutine = StartCoroutine(UseWanderingBehavior());
                break;
        }
    }

    private string GetStateDescription(AIState state)
    {
        return state switch
        {
            AIState.Wandering => "배회 중",
            AIState.MovingToQueue => "대기열로 이동 중",
            AIState.WaitingInQueue => "대기열에서 대기 중",
            AIState.MovingToRoom => $"룸 {currentRoomIndex + 1}번으로 이동 중",
            AIState.UsingRoom => "룸 사용 중",
            AIState.ReportingRoom => "룸 사용 완료 보고 중",
            AIState.ReturningToSpawn => "퇴장 중",
            AIState.RoomWandering => $"룸 {currentRoomIndex + 1}번 내부 배회 중",
            AIState.ReportingRoomQueue => "사용 완료 보고 대기열로 이동 중",
            AIState.UseWandering => $"룸 {currentRoomIndex + 1}번 사용 중 외부 배회",
            AIState.MovingToBed => $"룸 {currentRoomIndex + 1}번 침대로 이동 중",
            AIState.Sleeping => $"룸 {currentRoomIndex + 1}번 침대에서 수면 중",
            AIState.MovingToSunbed => $"선베드로 이동 중 (방 없는 AI)",
            AIState.UsingSunbed => $"선베드 사용 중 (방 없는 AI)",
            AIState.MovingToBathtub => $"룸 {currentRoomIndex + 1}번 욕조로 이동 중",
            AIState.UsingBathtub => $"룸 {currentRoomIndex + 1}번 욕조 사용 중",
            AIState.MovingToHealth => $"룸 {currentRoomIndex + 1}번 운동 시설로 이동 중",
            AIState.UsingHealth => $"룸 {currentRoomIndex + 1}번 운동 시설 사용 중",
            AIState.MovingToWedding => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 예식장으로 이동 중" : "예식장으로 이동 중 (방 없는 AI)",
            AIState.UsingWedding => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 예식장 사용 중" : "예식장 사용 중 (방 없는 AI)",
            AIState.MovingToLounge => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 라운지로 이동 중" : "라운지로 이동 중 (방 없는 AI)",
            AIState.UsingLounge => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 라운지 사용 중" : "라운지 사용 중 (방 없는 AI)",
            AIState.MovingToHall => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 연회장으로 이동 중" : "연회장으로 이동 중 (방 없는 AI)",
            AIState.UsingHall => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 연회장 사용 중" : "연회장 사용 중 (방 없는 AI)",
            AIState.MovingToSauna => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 사우나로 이동 중" : "사우나로 이동 중 (방 없는 AI)",
            AIState.UsingSauna => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 사우나 사용 중" : "사우나 사용 중 (방 없는 AI)",
            AIState.MovingToCafe => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 카페로 이동 중" : "카페로 이동 중 (방 없는 AI)",
            AIState.UsingCafe => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 카페 사용 중" : "카페 사용 중 (방 없는 AI)",
            AIState.MovingToBath => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 Bath로 이동 중" : "Bath로 이동 중 (방 없는 AI)",
            AIState.UsingBath => currentRoomIndex != -1 ? $"룸 {currentRoomIndex + 1}번 Bath 사용 중" : "Bath 사용 중 (방 없는 AI)",
            AIState.MovingToKitchenCounter => "주방 카운터로 이동 중",
            AIState.WaitingAtKitchenCounter => "주방 카운터에서 대기 중",
            AIState.MovingToChair => "의자로 이동 중",
            AIState.Eating => "식사 중",
            _ => "알 수 없는 상태"
        };
    }
    #endregion

    #region 룸 사용
    private IEnumerator UseRoom()
    {
        float roomUseTime = Random.Range(25f, 35f);
        float elapsedTime = 0f;

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        // ReportRoomUsage는 방 배정 시 이미 호출됨 (중복 방지)
        // 방 배정 받자마자 결제 정보가 등록되므로 여기서는 호출하지 않음

        TransitionToState(AIState.UseWandering);

        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        DetermineBehaviorByTime();
    }

    private IEnumerator ReportRoomVacancy()
    {
        TransitionToState(AIState.ReportingRoom);
        int reportingRoomIndex = currentRoomIndex;

        // 1. 먼저 결제 처리 (방 해제 전에 결제 처리해야 함)
        var roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager != null)
        {
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
        }

        // 2. 결제 처리 후 방 해제
        lock (lockObject)
        {
            if (reportingRoomIndex >= 0 && reportingRoomIndex < roomList.Count)
            {
                roomList[reportingRoomIndex].isOccupied = false;
                currentRoomIndex = -1;
            }
        }

        // 집사 AI 생성 요청 (컴파일 순서 문제로 인해 문자열로 찾기)
        GameObject butlerSpawnerObj = GameObject.Find("ButlerSpawner");
        if (butlerSpawnerObj != null)
        {
            var butlerSpawner = butlerSpawnerObj.GetComponent<MonoBehaviour>();
            if (butlerSpawner != null)
            {
                // 리플렉션을 사용하여 SpawnButlerForRoom 메서드 호출
                var method = butlerSpawner.GetType().GetMethod("SpawnButlerForRoom");
                if (method != null)
                {
                    method.Invoke(butlerSpawner, new object[] { reportingRoomIndex });
                }
            }
        }

        // 체크아웃 후 시간에 따라 정상적으로 행동 (다른 NPC처럼)
        // 11시에 디스폰하지 않고, 17시에 방 없으면 디스폰됨
            DetermineBehaviorByTime();

        yield break;
    }
    #endregion

    #region 배회 동작
    private IEnumerator WanderingBehavior()
    {
        // 배회 시작 시 모든 애니메이션 초기화 및 걷기 애니메이션 활성화
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetBool("BedTime", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
            animator.SetBool("Exercise", false);
            animator.SetBool("PickUp", false);
        }
        
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            yield return null; // 한 프레임 대기
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            yield break;
        }

        float wanderingTime = Random.Range(20f, 40f);
        float elapsedTime = 0f;

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            // 17시 체크 - 방 없는 AI만 디스폰 (투숙객은 계속 배회)
            if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
            {
                Handle17OClockForcedDespawn();
                yield break;
            }

            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(25f, 50f);
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            bool foundValidPosition = false;
            
            // 방을 피해서 배회 위치 찾기
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 15f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                foundValidPosition = true;
            }
            else
            {
                WanderOnGround();
                foundValidPosition = true;
            }
            
            if (foundValidPosition)
            {
                // 목적지까지 이동 대기 (타임아웃 추가)
                float moveTimeout = 15f;
                float moveTimer = 0f;
                
                while (agent != null && agent.enabled && agent.isOnNavMesh && 
                       (agent.pathPending || agent.remainingDistance > arrivalDistance))
                {
                    if (moveTimer >= moveTimeout)
                    {
                        break;
                    }
                    
                    // 17시 체크 - 방 없는 AI만 디스폰
                    if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
                    {
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                    moveTimer += 0.5f;
                }
                
                // 도착 후 대기
                float waitTime = Random.Range(5f, 12f);
                
                // 대기 시간을 쪼개서 17시 체크를 더 자주 함
                float remainingWait = waitTime;
                while (remainingWait > 0 && currentState == AIState.Wandering)
                {
                    yield return new WaitForSeconds(1f);
                    remainingWait -= 1f;
                    
                    // 대기 중에도 17시 체크 - 방 없는 AI만 디스폰
                    if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
                    {
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                }
                
                elapsedTime += waitTime + moveTimer;
            }
            else
            {
                yield return new WaitForSeconds(2f);
                elapsedTime += 2f;
            }
        }

        // 배회 완료 후에도 17시 체크 - 방 없는 AI만 디스폰
        if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
        {
            Handle17OClockForcedDespawn();
            yield break;
        }

        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 9-11시 체크아웃 후 배회하다가 11시에 디스폰하는 특별한 배회
    /// </summary>
    private IEnumerator WanderingBehaviorWithDespawn()
    {
        // 배회 시작 시 모든 애니메이션 초기화 및 걷기 애니메이션 활성화
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetBool("BedTime", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
            animator.SetBool("Exercise", false);
            animator.SetBool("PickUp", false);
        }
        
        while (currentState == AIState.Wandering)
        {
            // 11시가 되면 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 11)
            {
                isScheduledForDespawn = false;
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                yield break;
            }

            // 17시 체크도 유지 - 방 없는 AI만 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
            {
                isScheduledForDespawn = false;
                Handle17OClockForcedDespawn();
                yield break;
            }

            WanderOnGround();
            float waitTime = Random.Range(3f, 7f);
            
            // 대기 시간을 쪼개서 11시와 17시 체크를 더 자주 함
            float remainingWait = waitTime;
            while (remainingWait > 0 && currentState == AIState.Wandering)
            {
                yield return new WaitForSeconds(1f);
                remainingWait -= 1f;
                
                // 11시 체크
                if (timeSystem != null && timeSystem.CurrentHour >= 11)
                {
                    isScheduledForDespawn = false;
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                    yield break;
                }
                
                // 17시 체크 - 방 없는 AI만 디스폰
                if (timeSystem != null && timeSystem.CurrentHour >= 17 && currentRoomIndex == -1)
                {
                    isScheduledForDespawn = false;
                    Handle17OClockForcedDespawn();
                    yield break;
                }
            }
        }
    }

    private IEnumerator UseWanderingBehavior()
    {
        // 배회 시작 시 모든 애니메이션 초기화 및 걷기 애니메이션 활성화
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetBool("BedTime", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
            animator.SetBool("Exercise", false);
            animator.SetBool("PickUp", false);
        }
        
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            yield return null; // 한 프레임 대기
        }

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        while (currentState == AIState.UseWandering && agent.isOnNavMesh)
        {
            // 투숙객의 방 외부 배회 - 17시에도 계속 진행

            // 방을 피해서 외부 배회
            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(15f, 25f);
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 10f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                WanderOnGround();
            }

            float waitTime = Random.Range(4f, 10f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator RoomWanderingBehavior()
    {
        // 배회 시작 시 모든 애니메이션 초기화 및 걷기 애니메이션 활성화
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetBool("BedTime", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
            animator.SetBool("Exercise", false);
            animator.SetBool("PickUp", false);
        }
        
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            yield return null; // 한 프레임 대기
        }
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            // 투숙객의 방 내부 배회 - 17시에도 계속 진행

            // 방 내부에서만 배회
            if (TryGetRoomWanderingPosition(currentRoomIndex, out Vector3 roomPosition))
            {
                agent.SetDestination(roomPosition);
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 내부 위치를 찾지 못한 경우 기존 방식 사용
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                float roomSize = roomList[currentRoomIndex].size * 0.5f;
                if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 fallbackPos))
                {
                    agent.SetDestination(fallbackPos);
                }
            }

            float waitTime = Random.Range(3f, 6f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        DetermineBehaviorByTime();
    }

    private void WanderOnGround()
    {
        float wanderDistance = Random.Range(20f, 40f);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0;
        randomDirection.Normalize();
        
        Vector3 randomPoint = transform.position + randomDirection * wanderDistance;
        
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            return;
        }

        // 방을 피해서 배회하도록 수정
        if (TryGetWanderingPositionAvoidingRooms(randomPoint, 15f, groundMask, out Vector3 validPosition))
        {
            agent.SetDestination(validPosition);
        }
        else
        {
            // 방을 피하지 못한 경우 기본 방식으로 시도
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 15f, groundMask))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
    
    /// <summary>
    /// 방들을 피해서 배회 위치를 찾는 메서드
    /// </summary>
    private bool TryGetWanderingPositionAvoidingRooms(Vector3 targetPoint, float searchRadius, int layerMask, out Vector3 result)
    {
        result = targetPoint;
        
        for (int i = 0; i < maxRetries * 2; i++) // 더 많이 시도
        {
            Vector3 testPoint = targetPoint + Random.insideUnitSphere * searchRadius;
            testPoint.y = targetPoint.y; // Y축 고정
            
            if (NavMesh.SamplePosition(testPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                // 방과의 거리 체크
                bool tooCloseToRoom = false;
                foreach (var room in roomList)
                {
                    if (room != null && room.gameObject != null)
                    {
                        float distanceToRoom = Vector3.Distance(hit.position, room.transform.position);
                        // 방 크기의 1.5배 이상 떨어져 있어야 함
                        if (distanceToRoom < room.size * 1.5f)
                        {
                            tooCloseToRoom = true;
                            break;
                        }
                    }
                }
                
                if (!tooCloseToRoom)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 방 내부에서만 배회하는 위치를 찾는 메서드
    /// </summary>
    private bool TryGetRoomWanderingPosition(int roomIndex, out Vector3 result)
    {
        result = Vector3.zero;
        
        if (roomIndex < 0 || roomIndex >= roomList.Count)
            return false;
            
        var room = roomList[roomIndex];
        if (room == null || room.gameObject == null)
            return false;
            
        Bounds roomBounds = room.bounds;
        Vector3 roomCenter = roomBounds.center;
        
        for (int i = 0; i < maxRetries * 3; i++)
        {
            // 방 내부의 랜덤한 위치 생성
            Vector3 randomPoint = new Vector3(
                Random.Range(roomBounds.min.x + 1f, roomBounds.max.x - 1f),
                roomCenter.y,
                Random.Range(roomBounds.min.z + 1f, roomBounds.max.z - 1f)
            );
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                // 방 경계 내부인지 확인
                if (roomBounds.Contains(hit.position))
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    #endregion

    #region 침대 관련 메서드
    /// <summary>
    /// 현재 방에서 침대를 찾습니다.
    /// </summary>
    private bool FindBedInCurrentRoom(out Transform bedTransform)
    {
        bedTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.bedTransform == null)
        {
            return false;
        }

        bedTransform = room.bedTransform;
        return true;
    }

    /// <summary>
    /// 현재 방 안에 Bathtub 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindBathtubInCurrentRoom(out Transform bathtubTransform)
    {
        bathtubTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 자식 오브젝트에서 "Bathtub" 태그를 가진 오브젝트 찾기
        GameObject[] allBathtubs = GameObject.FindGameObjectsWithTag("Bathtub");
        foreach (var bathtub in allBathtubs)
        {
            // 방의 bounds 안에 있는지 체크
            if (room.bounds.Contains(bathtub.transform.position))
            {
                bathtubTransform = bathtub.transform;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Health 태그를 가진 오브젝트 찾기 (AI 점유 관리)
    /// </summary>
    private bool FindHealthInCurrentRoom(out Transform healthTransform)
    {
        healthTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Health" 태그를 가진 오브젝트 찾기
        GameObject[] allHealths = GameObject.FindGameObjectsWithTag("Health");
        
        Transform sameFloorHealth = null;
        Transform otherFloorHealth = null;
        
        foreach (var health in allHealths)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(health.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(health.transform.position.y))
                {
                    sameFloorHealth = health.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorHealth == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorHealth = health.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedHealth = sameFloorHealth ?? otherFloorHealth;
        
        if (selectedHealth != null)
        {
            healthTransform = selectedHealth;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Wedding 태그를 가진 오브젝트 찾기 (AI 점유 관리)
    /// </summary>
    private bool FindWeddingInCurrentRoom(out Transform weddingTransform)
    {
        weddingTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Wedding" 태그를 가진 오브젝트 찾기
        GameObject[] allWeddings = GameObject.FindGameObjectsWithTag("Wedding");
        
        Transform sameFloorWedding = null;
        Transform otherFloorWedding = null;
        
        foreach (var wedding in allWeddings)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(wedding.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(wedding.transform.position.y))
                {
                    sameFloorWedding = wedding.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorWedding == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorWedding = wedding.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedWedding = sameFloorWedding ?? otherFloorWedding;
        
        if (selectedWedding != null)
        {
            weddingTransform = selectedWedding;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 손님용: 맵에서 Health 태그를 가진 오브젝트 찾기 (AI 점유 관리)
    /// </summary>
    private bool TryFindAvailableHealth()
    {
        GameObject[] allHealths = GameObject.FindGameObjectsWithTag("Health");
        
        if (allHealths.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 운동 시설 우선 찾기
        Transform nearestSameFloorHealth = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 운동 시설 (백업)
        Transform nearestOtherFloorHealth = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var health in allHealths)
        {
            if (health != null)
            {
                float distance = Vector3.Distance(transform.position, health.transform.position);
                
                if (IsSameFloor(health.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorHealth = health.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorHealth = health.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedHealth = nearestSameFloorHealth ?? nearestOtherFloorHealth;

        if (selectedHealth != null)
        {
            currentHealthTransform = selectedHealth;
            preHealthPosition = transform.position;
            preHealthRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToHealth);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 손님용: 맵에서 Wedding 태그를 가진 오브젝트 찾기 (AI 점유 관리)
    /// </summary>
    private bool TryFindAvailableWedding()
    {
        GameObject[] allWeddings = GameObject.FindGameObjectsWithTag("Wedding");
        
        if (allWeddings.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 예식장 우선 찾기
        Transform nearestSameFloorWedding = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 예식장 (백업)
        Transform nearestOtherFloorWedding = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var wedding in allWeddings)
        {
            if (wedding != null)
            {
                float distance = Vector3.Distance(transform.position, wedding.transform.position);
                
                if (IsSameFloor(wedding.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorWedding = wedding.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorWedding = wedding.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedWedding = nearestSameFloorWedding ?? nearestOtherFloorWedding;

        if (selectedWedding != null)
        {
            currentWeddingTransform = selectedWedding;
            preWeddingPosition = transform.position;
            preWeddingRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToWedding);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 손님용: 맵에서 Lounge 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool TryFindAvailableLounge()
    {
        GameObject[] allLounges = GameObject.FindGameObjectsWithTag("Lounge");
        
        if (allLounges.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 라운지 우선 찾기
        Transform nearestSameFloorLounge = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 라운지 (백업)
        Transform nearestOtherFloorLounge = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var lounge in allLounges)
        {
            if (lounge != null)
            {
                float distance = Vector3.Distance(transform.position, lounge.transform.position);
                
                if (IsSameFloor(lounge.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorLounge = lounge.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorLounge = lounge.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedLounge = nearestSameFloorLounge ?? nearestOtherFloorLounge;

        if (selectedLounge != null)
        {
            currentLoungeTransform = selectedLounge;
            preLoungePosition = transform.position;
            preLoungeRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToLounge);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 손님용: 맵에서 Hall 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool TryFindAvailableHall()
    {
        GameObject[] allHalls = GameObject.FindGameObjectsWithTag("Hall");
        
        if (allHalls.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 연회장 우선 찾기
        Transform nearestSameFloorHall = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 연회장 (백업)
        Transform nearestOtherFloorHall = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var hall in allHalls)
        {
            if (hall != null)
            {
                float distance = Vector3.Distance(transform.position, hall.transform.position);
                
                if (IsSameFloor(hall.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorHall = hall.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorHall = hall.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedHall = nearestSameFloorHall ?? nearestOtherFloorHall;

        if (selectedHall != null)
        {
            currentHallTransform = selectedHall;
            preHallPosition = transform.position;
            preHallRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToHall);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 손님용: 맵에서 Sauna 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool TryFindAvailableSauna()
    {
        GameObject[] allSaunas = GameObject.FindGameObjectsWithTag("Sauna");
        
        if (allSaunas.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 사우나 우선 찾기
        Transform nearestSameFloorSauna = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 사우나 (백업)
        Transform nearestOtherFloorSauna = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var sauna in allSaunas)
        {
            if (sauna != null)
            {
                float distance = Vector3.Distance(transform.position, sauna.transform.position);
                
                if (IsSameFloor(sauna.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorSauna = sauna.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorSauna = sauna.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedSauna = nearestSameFloorSauna ?? nearestOtherFloorSauna;

        if (selectedSauna != null)
        {
            currentSaunaTransform = selectedSauna;
            preSaunaPosition = transform.position;
            preSaunaRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToSauna);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Lounge 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindLoungeInCurrentRoom(out Transform loungeTransform)
    {
        loungeTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Lounge" 태그를 가진 오브젝트 찾기
        GameObject[] allLounges = GameObject.FindGameObjectsWithTag("Lounge");
        
        Transform sameFloorLounge = null;
        Transform otherFloorLounge = null;
        
        foreach (var lounge in allLounges)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(lounge.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(lounge.transform.position.y))
                {
                    sameFloorLounge = lounge.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorLounge == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorLounge = lounge.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedLounge = sameFloorLounge ?? otherFloorLounge;
        
        if (selectedLounge != null)
        {
            loungeTransform = selectedLounge;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Hall 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindHallInCurrentRoom(out Transform hallTransform)
    {
        hallTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Hall" 태그를 가진 오브젝트 찾기
        GameObject[] allHalls = GameObject.FindGameObjectsWithTag("Hall");
        
        Transform sameFloorHall = null;
        Transform otherFloorHall = null;
        
        foreach (var hall in allHalls)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(hall.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(hall.transform.position.y))
                {
                    sameFloorHall = hall.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorHall == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorHall = hall.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedHall = sameFloorHall ?? otherFloorHall;
        
        if (selectedHall != null)
        {
            hallTransform = selectedHall;
            Debug.Log($"[Hall 찾기] {gameObject.name}: 방 안에서 연회장 찾음 (층: {GetFloorLevel(selectedHall.position.y)})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Sauna 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindSaunaInCurrentRoom(out Transform saunaTransform)
    {
        saunaTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Sauna" 태그를 가진 오브젝트 찾기
        GameObject[] allSaunas = GameObject.FindGameObjectsWithTag("Sauna");
        
        Transform sameFloorSauna = null;
        Transform otherFloorSauna = null;
        
        foreach (var sauna in allSaunas)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(sauna.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(sauna.transform.position.y))
                {
                    sameFloorSauna = sauna.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorSauna == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorSauna = sauna.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedSauna = sameFloorSauna ?? otherFloorSauna;
        
        if (selectedSauna != null)
        {
            saunaTransform = selectedSauna;
            Debug.Log($"[Sauna 찾기] {gameObject.name}: 방 안에서 사우나 찾음 (층: {GetFloorLevel(selectedSauna.position.y)})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 침대로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToBedBehavior()
    {
        if (currentBedTransform == null || currentBedTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: 침대 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 bedFacilityPos = currentBedTransform.position;
        bool pathSet = agent.SetDestination(bedFacilityPos);
        
        if (!pathSet || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"[침대 1단계 실패] {gameObject.name}: 침대로 경로 설정 불가");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"<color=cyan>[침대 1단계] {gameObject.name}: 침대 근처로 이동 시작 (목표: {bedFacilityPos})</color>");

        // 시설 근처 도착 대기 (침대가 크므로 범위를 넓게)
        float facilityArrivalRange = 7f; // 3m → 7m로 증가
        float yTolerance = 1f;
        float timeout = 20f;
        float timer = 0f;
        
        while (true)
        {
            if (currentBedTransform == null || currentBedTransform.gameObject == null)
            {
                Debug.LogWarning($"[침대 1단계 중단] {gameObject.name}: 침대 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }

            if (!agent.pathPending && agent.isOnNavMesh)
            {
                Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 targetPosXZ = new Vector3(bedFacilityPos.x, 0, bedFacilityPos.z);
                float xzDistance = Vector3.Distance(currentPosXZ, targetPosXZ);
                float yDistance = Mathf.Abs(transform.position.y - bedFacilityPos.y);
                
                // XZ 거리 또는 remainingDistance가 작으면 도착으로 판정
                if ((xzDistance <= facilityArrivalRange && yDistance <= yTolerance) || 
                    agent.remainingDistance <= 2f)
                {
                    Debug.Log($"<color=lime>[침대 1단계 완료] {gameObject.name}: 침대 근처 도착! XZ:{xzDistance:F1}m, Y:{yDistance:F1}m, remain:{agent.remainingDistance:F1}m</color>");
                    break;
                }
            }
            
            if (timer >= timeout)
            {
                Debug.LogError($"[침대 1단계 타임아웃] {gameObject.name}: 20초 초과, 그래도 다음 단계 진행");
                break; // 타임아웃이어도 다음 단계로 진행
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // ===== 2단계: BedPoint 찾기 =====
        Transform bedPoint = FindNearestBedPoint(currentBedTransform);
        
        if (bedPoint == null)
        {
            Debug.LogWarning($"[침대 2단계] {gameObject.name}: BedPoint 없음, 침대 위치로 직접 수면");
            StartSleeping(null);
            yield break;
        }

        Debug.Log($"<color=cyan>[침대 2단계] {gameObject.name}: BedPoint 발견 ({bedPoint.name}), 이동 시작</color>");

        // ===== 3단계: BedPoint 근처로 이동 =====
        Vector3 targetPos = bedPoint.position;
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"[침대 3단계 스킵] {gameObject.name}: BedPoint로 경로 불가, 바로 수면");
            StartSleeping(bedPoint);
            yield break;
        }

        // BedPoint 근처 도착 대기 (더 짧은 시간으로 체크)
        float pointArrivalRange = 2.5f; // 1.5m → 2.5m로 증가
        timer = 0f;
        
        while (true)
        {
            if (currentBedTransform == null || currentBedTransform.gameObject == null)
            {
                Debug.LogWarning($"[침대 3단계 중단] {gameObject.name}: 침대 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            // pathPending이 끝나고, remainingDistance가 작으면 도착
            if (!agent.pathPending && agent.remainingDistance <= pointArrivalRange)
            {
                Debug.Log($"<color=lime>[침대 3단계 완료] {gameObject.name}: BedPoint 근처 도착! remain:{agent.remainingDistance:F1}m</color>");
                break;
            }
            
            if (timer >= timeout)
            {
                Debug.LogError($"[침대 3단계 타임아웃] {gameObject.name}: 20초 초과, 그래도 수면 시작");
                break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // ===== 4단계: 순간이동 및 수면 시작 =====
        Debug.Log($"<color=yellow>[침대 4단계] {gameObject.name}: 수면 시작!</color>");
        StartSleeping(bedPoint);
    }
    
    /// <summary>
    /// 침대 주변에서 가장 가까운 BedPoint 찾기 (자신의 방 안에 있고 점유되지 않은 포인트만)
    /// </summary>
    private Transform FindNearestBedPoint(Transform bed)
    {
        GameObject[] allBedPoints = GameObject.FindGameObjectsWithTag("BedPoint");
        
        if (allBedPoints == null || allBedPoints.Length == 0)
        {
            return null;
        }
        
        // ✅ 자신의 방 정보 가져오기
        Bounds? myRoomBounds = null;
        if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
        {
            var room = roomList[currentRoomIndex];
            if (room != null && room.gameObject != null)
            {
                myRoomBounds = room.bounds;
            }
        }
        
        Transform nearestPoint = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var point in allBedPoints)
        {
            if (point == null) continue;
            
            // ✅ 자신의 방 안에 있는 BedPoint만 고려
            if (myRoomBounds.HasValue)
            {
                if (!myRoomBounds.Value.Contains(point.transform.position))
                {
                    continue; // 다른 방의 BedPoint는 스킵
                }
            }
            
            // 점유 체크
            lock (bedPointLock)
            {
                // 이미 다른 AI가 점유 중이면 스킵
                if (occupiedBedPoints.ContainsKey(point.transform) && occupiedBedPoints[point.transform] != this)
                {
                    continue;
                }
            }
            
            float distance = Vector3.Distance(bed.position, point.transform.position);
            
            // 침대 주변 5m 이내의 BedPoint만 고려
            if (distance < 5f && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPoint = point.transform;
            }
        }
        
        // 찾은 포인트를 점유
        if (nearestPoint != null)
        {
            lock (bedPointLock)
            {
                occupiedBedPoints[nearestPoint] = this;
            }
            Debug.Log($"<color=cyan>[BedPoint 찾기] {gameObject.name}: 자신의 방 안에서 BedPoint 찾음 (거리: {nearestDistance:F1}m)</color>");
        }
        else
        {
            Debug.LogWarning($"[BedPoint 찾기] {gameObject.name}: 자신의 방 안에서 사용 가능한 BedPoint를 찾을 수 없음");
        }
        
        return nearestPoint;
    }

    /// <summary>
    /// 수면을 시작합니다.
    /// </summary>
    /// <param name="bedPoint">BedPoint Transform (없으면 null)</param>
    private void StartSleeping(Transform bedPoint)
    {
        if (currentBedTransform == null || currentBedTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preSleepPosition = transform.position;
        preSleepRotation = transform.rotation;

        // BedPoint가 있으면 해당 위치로, 없으면 침대 위치로
        Vector3 targetPosition;
        Quaternion targetRotation;
        
        if (bedPoint != null)
        {
            targetPosition = bedPoint.position;
            targetRotation = bedPoint.rotation;
            currentBedPoint = bedPoint;
        }
        else
        {
            targetPosition = currentBedTransform.position;
            targetRotation = currentBedTransform.rotation;
            currentBedPoint = null;
        }
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        // 3단계: Point로 정확히 순간이동
            transform.position = targetPosition;
        transform.rotation = targetRotation;

        // 4단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false);
            animator.SetBool("BedTime", true);
        }

        // 수면 상태 설정
        isSleeping = true;
        sleepStartTime = Time.time;
        TransitionToState(AIState.Sleeping);
        
        // 낮잠인 경우 타이머 시작
        if (isNapping)
        {
            nappingCoroutine = StartCoroutine(NappingTimer(30f));
        }
    }

    /// <summary>
    /// 수면에서 깨어납니다.
    /// </summary>
    private void WakeUp()
    {
        if (!isSleeping)
        {
            return;
        }

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 저장된 위치로 복귀
        transform.position = preSleepPosition;
        transform.rotation = preSleepRotation;

        // BedPoint 점유 해제
        if (currentBedPoint != null)
        {
            lock (bedPointLock)
            {
                if (occupiedBedPoints.ContainsKey(currentBedPoint) && occupiedBedPoints[currentBedPoint] == this)
                {
                    occupiedBedPoints.Remove(currentBedPoint);
                }
            }
            currentBedPoint = null;
        }

        // 수면 상태 해제
        isSleeping = false;
        currentBedTransform = null;

        // 9시 기상 시 결제 정보 확인 및 등록 (방 배정 시 등록되지 않은 경우 대비)
        if (currentRoomIndex != -1)
        {
            var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
            var roomManager = FindFirstObjectByType<RoomManager>();
            if (roomManager != null && room != null)
            {
                roomManager.ReportRoomUsage(gameObject.name, room);
            }
        }

        // 방 사용 완료 보고로 전환
        TransitionToState(AIState.ReportingRoomQueue);
    }

    /// <summary>
    /// 낮잠 타이머 (30초 후 자동으로 깨어남)
    /// </summary>
    private IEnumerator NappingTimer(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // 낮잠 중 침대 삭제 감지
            if (currentBedTransform == null || currentBedTransform.gameObject == null)
            {
                HandleBedDestroyed();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 낮잠 시간 종료 - 자연스럽게 깨어나기
        WakeUpFromNap();
    }

    /// <summary>
    /// 낮잠에서 깨어납니다 (야간 수면과 다르게 처리)
    /// </summary>
    private void WakeUpFromNap()
    {
        if (!isSleeping || !isNapping)
        {
            return;
        }

        // 낮잠 타이머 중단 (정각에 깨어날 때)
        if (nappingCoroutine != null)
        {
            StopCoroutine(nappingCoroutine);
            nappingCoroutine = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 저장된 위치로 복귀
        if (NavMesh.SamplePosition(preSleepPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            transform.position = preSleepPosition;
        }
        transform.rotation = preSleepRotation;

        // BedPoint 점유 해제
        if (currentBedPoint != null)
        {
            lock (bedPointLock)
            {
                if (occupiedBedPoints.ContainsKey(currentBedPoint) && occupiedBedPoints[currentBedPoint] == this)
                {
                    occupiedBedPoints.Remove(currentBedPoint);
                }
            }
            currentBedPoint = null;
        }

        // 수면 상태 해제
        isSleeping = false;
        isNapping = false;
        currentBedTransform = null;

        Debug.Log($"[낮잠 종료] {gameObject.name}: 정각이 되어 자연스럽게 깨어남");

        // 낮잠 후에는 다음 행동 결정 (퇴실 보고 아님!)
        DetermineBehaviorByTime();
    }
    #endregion

    #region 선베드 관련 메서드
    // ✅ TryFindSunbedInMyRoom 제거 - TryFindAvailableSunbed가 방 안/밖 모두 처리
    
    /// <summary>
    /// 선베드 찾기 (헬스장과 동일한 패턴)
    /// </summary>
    private bool TryFindAvailableSunbed()
    {
        GameObject[] allSunbeds = GameObject.FindGameObjectsWithTag("Sunbed");

        if (allSunbeds.Length == 0)
        {
            return false;
        }

        // ✅ 방 주인인 경우: 방 안 선베드만 찾기
        if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
        {
            Bounds myRoomBounds = roomList[currentRoomIndex].bounds;
            Transform nearestRoomSunbed = null;
            float minDistance = float.MaxValue;

            foreach (var sunbed in allSunbeds)
            {
                if (sunbed != null && myRoomBounds.Contains(sunbed.transform.position))
                {
                    float distance = Vector3.Distance(transform.position, sunbed.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestRoomSunbed = sunbed.transform;
                    }
                }
            }

            if (nearestRoomSunbed != null)
            {
                currentSunbedTransform = nearestRoomSunbed;
                    preSunbedPosition = transform.position;
                    preSunbedRotation = transform.rotation;
                    
                    TransitionToState(AIState.MovingToSunbed);
                Debug.Log($"[선베드] {gameObject.name}: 방 안 선베드 선택 (무료) - {currentSunbedTransform.name}");
                    return true;
        }
        
            // 방 안에 선베드가 없으면 실패
            Debug.Log($"[선베드] {gameObject.name}: 방 안에 선베드가 없습니다");
        return false;
    }
    
        // ✅ 방 없는 AI: 방 밖 선베드 찾기 (같은 층 우선)
        Transform nearestSameFloorSunbed = null;
        float minSameFloorDistance = float.MaxValue;

        Transform nearestOtherFloorSunbed = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var sunbed in allSunbeds)
        {
            if (sunbed != null)
            {
                // 방 안에 있는 선베드는 제외
                bool isInsideRoom = false;
                foreach (var room in roomList)
                {
                    if (room != null && room.bounds.Contains(sunbed.transform.position))
                    {
                        isInsideRoom = true;
                        break;
                    }
                }
                
                if (isInsideRoom) continue;

                float distance = Vector3.Distance(transform.position, sunbed.transform.position);
                
                if (IsSameFloor(sunbed.transform.position.y))
                {
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorSunbed = sunbed.transform;
                    }
                }
                else
                {
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorSunbed = sunbed.transform;
                    }
                }
            }
        }

        Transform selectedSunbed = nearestSameFloorSunbed ?? nearestOtherFloorSunbed;

        if (selectedSunbed != null)
        {
            currentSunbedTransform = selectedSunbed;
        preSunbedPosition = transform.position;
        preSunbedRotation = transform.rotation;
        
        TransitionToState(AIState.MovingToSunbed);
            Debug.Log($"[선베드] {gameObject.name}: 방 밖 선베드 선택 (유료) - {currentSunbedTransform.name}");
        return true;
        }

        return false;
    }

    /// <summary>
    /// 선베드로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToSunbedBehavior()
    {
        if (currentSunbedTransform == null || currentSunbedTransform.gameObject == null)
        {
            CleanupSunbedMovement();
            yield break;
        }

        // NavMesh 상태 확인
        if (!agent.isOnNavMesh)
        {
            CleanupSunbedMovement();
            yield break;
        }

        Debug.Log($"[Sunbed] {gameObject.name}: 선베드 시설로 이동 시작 - {currentSunbedTransform.position}");

        // ===== 1단계: 선베드 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 sunbedFacilityPos = currentSunbedTransform.position;
        bool pathSet = agent.SetDestination(sunbedFacilityPos);
        
        if (!pathSet || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError($"[Sunbed 1단계 실패] {gameObject.name}: 선베드 시설로 경로 설정 실패");
            CleanupSunbedMovement();
            yield break;
        }

        // 시설 근처 도착 대기 (XZ 거리 + Y 오차 체크)
        float facilityArrivalRange = 5f; // 시설 근처 5m 이내
        float yTolerance = 1f; // Y축 오차 1m 이내
        float timeout = 30f;
        float timer = 0f;
        
        while (true)
        {
            // 선베드 삭제 체크
            if (currentSunbedTransform == null || currentSunbedTransform.gameObject == null)
            {
                Debug.LogWarning($"[Sunbed 1단계 중단] {gameObject.name}: 선베드가 삭제됨");
                CleanupSunbedMovement();
                yield break;
            }
            
            if (!agent.pathPending && agent.isOnNavMesh)
            {
                // XZ 평면 거리 계산
                Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 targetPosXZ = new Vector3(sunbedFacilityPos.x, 0, sunbedFacilityPos.z);
                float xzDistance = Vector3.Distance(currentPosXZ, targetPosXZ);
                
                // Y축 차이 계산
                float yDistance = Mathf.Abs(transform.position.y - sunbedFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Sunbed 1단계 완료] {gameObject.name}: 선베드 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer >= timeout)
            {
                Debug.LogError($"[Sunbed 1단계 타임아웃] {gameObject.name}: 선베드 시설로 이동 실패");
                CleanupSunbedMovement();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // ===== 2단계: SunbedPoint 찾기 =====
        Transform sunbedPoint = FindNearestSunbedPoint(currentSunbedTransform);
        
        if (sunbedPoint == null)
        {
            Debug.LogError($"[Sunbed] {gameObject.name}: SunbedPoint를 찾을 수 없습니다!");
            CleanupSunbedMovement();
            yield break;
        }

        Debug.Log($"[Sunbed 2단계 시작] {gameObject.name}: SunbedPoint로 이동 - {sunbedPoint.position}");

        // ===== 3단계: SunbedPoint로 이동 =====
        Vector3 targetPos = sunbedPoint.position;
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError($"[Sunbed 2단계 실패] {gameObject.name}: SunbedPoint로 경로 설정 실패");
            CleanupSunbedMovement();
            yield break;
        }

        // SunbedPoint 도착 대기
        timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            // 선베드 삭제 체크
            if (currentSunbedTransform == null || currentSunbedTransform.gameObject == null)
            {
                Debug.LogWarning($"[Sunbed 2단계 중단] {gameObject.name}: 선베드가 삭제됨");
                CleanupSunbedMovement();
                yield break;
            }
            
            if (timer >= timeout)
            {
                Debug.LogError($"[Sunbed 2단계 타임아웃] {gameObject.name}: SunbedPoint로 이동 실패");
                CleanupSunbedMovement();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[Sunbed 3단계 완료] {gameObject.name}: SunbedPoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingSunbed(sunbedPoint);
    }

    /// <summary>
    /// 선베드 주변에서 가장 가까운 사용 가능한 SunbedPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestSunbedPoint(Transform sunbed)
    {
        GameObject[] allSunbedPoints = GameObject.FindGameObjectsWithTag("SunbedPoint");
        
        if (allSunbedPoints == null || allSunbedPoints.Length == 0)
        {
            return null;
        }
        
        lock (sunbedPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allSunbedPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedSunbedPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedSunbedPoints[point.transform] == null)
                    {
                        occupiedSunbedPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(sunbed.position, point.transform.position);
                
                // 선베드 주변 10m 이내의 SunbedPoint만 고려
                if (distance < 10f && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 가장 가까운 포인트를 찾았으면 점유 등록
            if (nearestPoint != null)
            {
                occupiedSunbedPoints[nearestPoint] = this;
                currentSunbedPoint = nearestPoint;
                Debug.Log($"[Sunbed] {gameObject.name}: SunbedPoint 점유 완료 (거리: {nearestDistance:F2}m, 점유 중: {occupiedSunbedPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 선베드로 이동 실패 시 리소스 정리
    /// </summary>
    private void CleanupSunbedMovement()
    {
        // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요
        
        // SunbedPoint 점유 해제
        if (currentSunbedPoint != null)
        {
            lock (sunbedPointLock)
            {
                if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint) && occupiedSunbedPoints[currentSunbedPoint] == this)
                {
                    occupiedSunbedPoints.Remove(currentSunbedPoint);
                    Debug.Log($"[SunbedPoint 점유 해제] {gameObject.name}: 이동 실패로 점유 해제");
                }
            }
        }
        
        // 선베드 관련 변수 정리
        currentSunbedTransform = null;
        currentSunbedPoint = null;
        // ✅ isSunbedInRoom 제거 - 결제 시점에 방 안/밖 확인
        preSunbedPosition = Vector3.zero;
        preSunbedRotation = Quaternion.identity;
        
        // 다른 행동으로 전환
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 선베드 사용을 시작합니다.
    /// </summary>
    /// <param name="sunbedPoint">SunbedPoint Transform (null이면 자동으로 찾음)</param>
    private void StartUsingSunbed(Transform sunbedPoint = null)
    {
        if (currentSunbedTransform == null || currentSunbedTransform.gameObject == null)
        {
            CleanupSunbedMovement();
            return;
        }

        // sunbedPoint가 null이면 자동으로 찾기
        if (sunbedPoint == null)
        {
            sunbedPoint = FindNearestSunbedPoint(currentSunbedTransform);
        }

        if (sunbedPoint == null)
        {
            Debug.LogError($"[Sunbed] {gameObject.name}: SunbedPoint를 찾을 수 없습니다!");
            CleanupSunbedMovement();
            return;
        }

        // 이미 선베드 사용 중인지 확인
        if (isUsingSunbed)
        {
            return;
        }

        // 1단계: 현재 위치 저장 (NavMesh 위의 위치 - Point 바로 앞)
        preSunbedPosition = transform.position;
        preSunbedRotation = transform.rotation;
        Debug.Log($"[Sunbed] {gameObject.name}: 이전 위치 저장 완료 - {preSunbedPosition}");
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
            Debug.Log($"[Sunbed] {gameObject.name}: NavMeshAgent 비활성화");
        }

        // 3단계: SunbedPoint 위치로 정확히 순간이동
        transform.position = sunbedPoint.position;
        transform.rotation = sunbedPoint.rotation;
        Debug.Log($"[Sunbed 순간이동] {gameObject.name}: 정확한 SunbedPoint 위치로 이동 완료 - {sunbedPoint.position}");

        // 4단계: 선베드 사용 상태 설정
        isUsingSunbed = true;
        TransitionToState(AIState.UsingSunbed);
        
        // 타이머가 없다면 다시 시작 (안전장치)
        if (sunbedCoroutine == null)
        {
            sunbedCoroutine = StartCoroutine(SunbedUsageTimer());
        }
        
        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("BedTime", true);
            Debug.Log($"[Sunbed 애니메이션] {gameObject.name}: Moving=false, BedTime=true");
        }

        int aiFloor = GetFloorLevel(transform.position.y);
        int sunbedFloor = GetFloorLevel(currentSunbedTransform.position.y);
        Debug.Log($"[Sunbed 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 선베드 층: {sunbedFloor}");
    }

    /// <summary>
    /// 선베드 사용 시간 타이머 (게임 시간 50분)
    /// </summary>
    private IEnumerator SunbedUsageTimer()
    {
        // TimeSystem 안전성 체크
        if (timeSystem == null)
        {
            timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                yield return new WaitForSeconds(50f);
                if (isUsingSunbed) FinishUsingSunbed();
                sunbedCoroutine = null;
                yield break;
            }
        }

        // 게임 시간으로 50분 대기
        int startHour = timeSystem.CurrentHour;
        int startMinute = timeSystem.CurrentMinute;
        int startTotalMinutes = startHour * 60 + startMinute;
        
        // 50분 후의 시간 계산
        int targetTotalMinutes = startTotalMinutes + 50;
        int targetHour = (targetTotalMinutes / 60) % 24;
        int targetMinute = targetTotalMinutes % 60;
        
        // 안전장치: 최대 대기 시간 (실제 시간 10분 = Time.unscaledTime 기준)
        float maxWaitTime = 600f;
        float startRealTime = Time.unscaledTime;
        float checkInterval = 0.5f; // 0.5초마다 체크 (빠른 감지)
        
        // 게임 시간이 목표 시간에 도달할 때까지 대기
        while (isUsingSunbed && (Time.unscaledTime - startRealTime) < maxWaitTime)
        {
            // TimeSystem 재확인
            if (timeSystem == null)
            {
                timeSystem = TimeSystem.Instance;
                if (timeSystem == null)
                {
                    break;
                }
            }
            
            int currentHour = timeSystem.CurrentHour;
            int currentMinute = timeSystem.CurrentMinute;
            int currentTotalMinutes = currentHour * 60 + currentMinute;
            
            // 16시 이후 강제 종료 (15시까지만 선베드 사용 가능)
            if (currentHour >= 16)
            {
                break;
            }
            
            // 목표 시간 도달 체크
            bool timeReached = false;
            
            // 같은 날인 경우 (자정을 넘지 않음)
            if (targetTotalMinutes < 1440) // 1440분 = 24시간
            {
                if (currentTotalMinutes >= targetTotalMinutes)
                {
                    timeReached = true;
                }
            }
            else
            {
                // 다음 날로 넘어가는 경우
                int nextDayTargetMinutes = targetTotalMinutes - 1440;
                if (currentTotalMinutes >= nextDayTargetMinutes || currentTotalMinutes < startTotalMinutes)
                {
                    timeReached = true;
                }
            }
            
            // 시간이 크게 점프한 경우 감지 (게임 시간 빨리 감기)
            int timeDifference = currentTotalMinutes - startTotalMinutes;
            if (timeDifference < 0) timeDifference += 1440; // 하루 넘어간 경우 보정
            
            if (timeDifference >= 50) // 50분 이상 지났으면
            {
                timeReached = true;
            }
            
            if (timeReached)
            {
                break;
            }
            
            yield return new WaitForSeconds(checkInterval);
        }
        
        // 선베드 사용 종료
        if (isUsingSunbed)
        {
            FinishUsingSunbed();
        }
        
        // 코루틴 참조 정리
        sunbedCoroutine = null;
    }

    /// <summary>
    /// 선베드 사용을 종료합니다.
    /// </summary>
    private void FinishUsingSunbed()
    {
        if (!isUsingSunbed)
        {
            return;
        }

        // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요

        // 0. ✅ SunbedPoint 점유 해제
        if (currentSunbedPoint != null)
        {
            lock (sunbedPointLock)
            {
                if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint) && occupiedSunbedPoints[currentSunbedPoint] == this)
                {
                    occupiedSunbedPoints.Remove(currentSunbedPoint);
                    Debug.Log($"[SunbedPoint 점유 해제] {gameObject.name}: 사용 종료로 점유 해제");
                }
            }
        }

        // 1. BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // 2. NavMeshAgent 다시 활성화 (침대와 동일하게)
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 3. 이전 위치로 복귀 (이미 NavMesh 위의 위치)
        if (preSunbedPosition != Vector3.zero)
        {
            transform.position = preSunbedPosition;
                transform.rotation = preSunbedRotation;
            Debug.Log($"[Sunbed] {gameObject.name}: 저장된 위치로 복귀 완료 - {preSunbedPosition}");
        }

        // 4. 선베드 사용 상태 해제 (안전하게)
        isUsingSunbed = false;
        
        // 5. ✅ 선베드 결제 처리 (방 안/밖 동적 확인)
        if (roomManager != null && currentSunbedTransform != null)
        {
            // 방 안/밖 확인: 선베드가 내 방 범위 안에 있는지 체크
            bool isInMyRoom = false;
            if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
            {
                isInMyRoom = roomList[currentRoomIndex].bounds.Contains(currentSunbedTransform.position);
            }
            
            roomManager.ProcessSunbedPayment(gameObject.name, isInMyRoom);
            Debug.Log($"[선베드 결제] {gameObject.name}: 방 안={isInMyRoom}");
        }
        
        // 6. 선베드 관련 변수 정리
        currentSunbedTransform = null;
        currentSunbedPoint = null;
        // ✅ isSunbedInRoom 제거 - 결제에서 동적으로 확인
        
        // 7. 다음 행동 결정 (다른 시설들과 동일)
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// GameObject 비활성화 시 코루틴 없이 선베드 결제를 직접 처리합니다 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessSunbedPaymentDirectly()
    {
        // RoomManager를 통한 결제 처리 (FacilityPriceConfig에서 가격 자동 로드)
        if (roomManager != null && currentSunbedTransform != null)
        {
            // ✅ 방 안/밖 동적 확인
            bool isInMyRoom = false;
            if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
            {
                isInMyRoom = roomList[currentRoomIndex].bounds.Contains(currentSunbedTransform.position);
            }
            
            roomManager.ProcessSunbedPayment(gameObject.name, isInMyRoom);
        }
        else
        {
            Debug.LogError($"[선베드 결제 실패 (비활성화)] {gameObject.name}: RoomManager 또는 선베드가 null입니다.");
        }
    }
    
    /// <summary>
    /// 17시 강제 디스폰 시 선베드 사용을 강제로 종료합니다.
    /// </summary>
    private void ForceFinishUsingSunbed()
    {
        if (!isUsingSunbed)
        {
            return;
        }

        // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요

        // 0. ✅ SunbedPoint 점유 해제
        if (currentSunbedPoint != null)
        {
            lock (sunbedPointLock)
            {
                if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint) && occupiedSunbedPoints[currentSunbedPoint] == this)
                {
                    occupiedSunbedPoints.Remove(currentSunbedPoint);
                    Debug.Log($"[SunbedPoint 점유 해제] {gameObject.name}: 강제 종료로 점유 해제");
                }
            }
        }

        // 1. BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // 2. NavMeshAgent 다시 활성화 (침대와 동일하게)
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 3. ✅ NavMesh 위의 안전한 위치로 복귀
        if (preSunbedPosition != Vector3.zero)
        {
            // NavMesh 위의 유효한 위치 찾기
            if (NavMesh.SamplePosition(preSunbedPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                transform.rotation = preSunbedRotation;
                Debug.Log($"[선베드 강제 종료] {gameObject.name}: NavMesh 위치로 복귀 완료");
            }
            else
            {
                // NavMesh를 찾지 못한 경우 현재 위치 유지
                Debug.LogWarning($"[선베드 강제 종료] {gameObject.name}: NavMesh 위치를 찾지 못함, 현재 위치 유지");
            }
        }

        // 4. 선베드 사용 상태 해제 (안전하게)
        isUsingSunbed = false;
        
        // 5. ✅ 선베드 결제 처리 (강제 종료 시에도 결제, 방 안/밖 동적 확인)
        if (roomManager != null && currentSunbedTransform != null)
        {
            bool isInMyRoom = false;
            if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
            {
                isInMyRoom = roomList[currentRoomIndex].bounds.Contains(currentSunbedTransform.position);
            }
            
            roomManager.ProcessSunbedPayment(gameObject.name, isInMyRoom);
            Debug.Log($"[선베드 강제 종료 결제] {gameObject.name}: 방 안={isInMyRoom}");
        }
        
        // 6. 선베드 관련 변수 정리
        currentSunbedTransform = null;
        currentSunbedPoint = null;
        // ✅ isSunbedInRoom 제거 - 결제에서 동적으로 확인
    }

    /// <summary>
    /// 선베드 결제를 처리합니다 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessSunbedPayment(bool isForcedDespawn = false)
    {
        // ✅ 방 안/밖 동적 확인
        bool isInMyRoom = false;
        if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count && currentSunbedTransform != null)
        {
            isInMyRoom = roomList[currentRoomIndex].bounds.Contains(currentSunbedTransform.position);
        }
        
        // RoomManager를 통한 결제 처리 (FacilityPriceConfig에서 가격 자동 로드)
        if (roomManager != null)
        {
            roomManager.ProcessSunbedPayment(gameObject.name, isInMyRoom);
        }
        else
        {
            Debug.LogError($"[선베드 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }

        // 결제 완료 후 상태 전환
        if (isForcedDespawn)
        {
            // 17시 강제 디스폰인 경우 디스폰 상태로 전환
            TransitionToState(AIState.ReturningToSpawn);
            agent.SetDestination(spawnPoint.position);
        }
        else
        {
            // 일반적인 경우 배회로 전환
            if (currentRoomIndex != -1)
            {
                // 투숙객은 방 배회
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                // 방 없는 AI는 일반 배회
                TransitionToState(AIState.Wandering);
            }
        }
    }
    #endregion

    #region 식당 관련 메서드
    /// <summary>
    /// 사용 가능한 주방 카운터를 찾아서 대기열에 진입합니다.
    /// </summary>
    private bool TryFindAvailableKitchen()
    {
        // 1단계: KitchenDetector가 주방을 인식했는지 먼저 확인
        if (KitchenDetector.Instance == null)
        {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ KitchenDetector가 없습니다!");
            return false;
        }
        
        var detectedKitchens = KitchenDetector.Instance.GetDetectedKitchens();
        if (detectedKitchens == null || detectedKitchens.Count == 0)
        {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ 인식된 주방이 없습니다! 주방을 설치하세요 (카운터+인덕션+테이블 필요)");
            return false;
        }
        
        Debug.Log($"[식당 단계1] {gameObject.name}: ✅ 인식된 주방 {detectedKitchens.Count}개 발견");
        
        // 2단계: KitchenCounter 컴포넌트를 가진 오브젝트들 찾기
        KitchenCounter[] kitchenCounters = FindObjectsByType<KitchenCounter>(FindObjectsSortMode.None);
        
        if (kitchenCounters.Length == 0)
        {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ KitchenCounter 컴포넌트가 없습니다! 주방 카운터에 KitchenCounter 스크립트를 추가하세요.");
            return false;
        }
        
        Debug.Log($"[식당 단계2] {gameObject.name}: ✅ KitchenCounter {kitchenCounters.Length}개 발견");

        // 3단계: 가장 가까운 주방 카운터 찾기 (인식된 주방 영역 내에 있는 것만)
        KitchenCounter closestCounter = null;
        float closestDistance = float.MaxValue;
        int validCounters = 0;

        foreach (KitchenCounter counter in kitchenCounters)
        {
            if (counter == null) continue;
            
            // 카운터가 인식된 주방 영역 내에 있는지 확인
            bool isInKitchenArea = KitchenDetector.Instance.IsInKitchenArea(counter.transform.position);
            if (!isInKitchenArea)
            {
                Debug.LogWarning($"[식당 실패] {gameObject.name}: ⚠️ 카운터 {counter.name}는 인식된 주방 영역 밖에 있습니다 (카운터를 주방 근처에 배치하세요)");
                continue;
            }

            validCounters++;
            float distance = Vector3.Distance(transform.position, counter.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCounter = counter;
            }
        }

        Debug.Log($"[식당 단계3] {gameObject.name}: 주방 영역 내 유효한 카운터 {validCounters}개 / 총 {kitchenCounters.Length}개");

        if (closestCounter == null)
        {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ 주방 영역 내에 유효한 카운터가 없습니다!");
            return false;
        }
        
        Debug.Log($"[식당 단계4] {gameObject.name}: ✅ 가장 가까운 카운터 발견: {closestCounter.name} (거리: {closestDistance:F2}m)");

            // 주방 카운터 대기열에 진입 시도
        if (!closestCounter.TryJoinQueue(this))
            {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ 카운터 대기열에 진입 실패! 대기열이 가득 찼거나 직원이 없을 수 있습니다.");
            return false;
        }
        
        Debug.Log($"<color=green>[식당 단계5] {gameObject.name}: ✅✅✅ 카운터 대기열 진입 성공!</color>");
                currentKitchenCounter = closestCounter;
                
                // 해당 주방에서 사용 가능한 의자 미리 선택
        if (!FindAvailableChairInKitchen(closestCounter, out Transform selectedChair, out Transform kitchen))
        {
            Debug.LogWarning($"[식당 실패] {gameObject.name}: ❌ 사용 가능한 의자를 찾을 수 없습니다! 주방에 ChairPoint가 있는지 확인하세요.");
            closestCounter.LeaveQueue(this);
            return false;
        }
        
        Debug.Log($"[식당 단계6] {gameObject.name}: ✅ 사용 가능한 의자 발견: {selectedChair.name}");
        
                    currentKitchenTransform = kitchen;
                    currentChairTransform = selectedChair;
                    
                    // ChairPoint 컴포넌트 가져오기 및 즉시 점유 (예약만, 테이블은 아직 off)
                    currentChairPoint = selectedChair.GetComponent<ChairPoint>();
                    if (currentChairPoint != null)
                    {
                        currentChairPoint.OccupyChair(this, activateTable: false);  // 예약만!
            Debug.Log($"[식당 단계7] {gameObject.name}: ✅ 의자 예약 완료 (테이블은 아직 비활성) - {selectedChair.name}");
                    }
                    
                    // 위치 저장은 의자로 이동할 때 하도록 변경 (MoveToChairBehavior에서 처리)
                    
                    TransitionToState(AIState.MovingToKitchenCounter);
        Debug.Log($"[식당 성공] {gameObject.name}: ✅✅✅ 식당 이용 시작! 주방 카운터로 이동 중...");
                    return true;
    }

    /// <summary>
    /// 주방에서 사용 가능한 의자 찾기 (ChairPoint 사용)
    /// </summary>
    private bool FindAvailableChairInKitchen(KitchenCounter counter, out Transform selectedChair, out Transform kitchen)
    {
        selectedChair = null;
        kitchen = null;

        // 모든 KitchenChair 태그 오브젝트 직접 찾기
        GameObject[] allChairs = GameObject.FindGameObjectsWithTag("KitchenChair");
        
        if (allChairs.Length == 0)
        {
            return false;
        }

        List<ChairPoint> availableChairPoints = new List<ChairPoint>();

        foreach (GameObject chairObj in allChairs)
        {
            if (chairObj == null) continue;

            // 카운터와 의자가 너무 멀지 않은지 확인 (50미터 이내)
            float distance = Vector3.Distance(counter.transform.position, chairObj.transform.position);
            if (distance > 50f) 
            {
                continue;
            }

            // ChairPoint 컴포넌트 확인
            ChairPoint chairPoint = chairObj.GetComponent<ChairPoint>();
            if (chairPoint != null && !chairPoint.IsOccupied)
            {
                availableChairPoints.Add(chairPoint);
            }
            else if (chairPoint == null)
            {
                // ChairPoint가 없는 경우 기존 방식으로 확인 (하위 호환성)
                if (!IsChairOccupied(chairObj.transform))
                {
                    // ChairPoint가 없지만 사용 가능한 의자 (경고 로그 출력)
                    Debug.LogWarning($"[식당] 의자에 ChairPoint 컴포넌트가 없습니다: {chairObj.name}");
                }
            }
        }

        if (availableChairPoints.Count > 0)
        {
            // 랜덤하게 ChairPoint 선택
            ChairPoint selectedChairPoint = availableChairPoints[Random.Range(0, availableChairPoints.Count)];
            selectedChair = selectedChairPoint.transform;
            kitchen = selectedChair; // 의자 자체를 kitchen으로 설정 (단순화)
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// 의자가 사용 중인지 확인합니다. (하위 호환성을 위해 유지)
    /// </summary>
    private bool IsChairOccupied(Transform chair)
    {
        // ChairPoint가 있으면 ChairPoint로 확인
        ChairPoint chairPoint = chair.GetComponent<ChairPoint>();
        if (chairPoint != null)
        {
            return chairPoint.IsOccupied;
        }
        
        // ChairPoint가 없으면 기존 방식으로 확인 (하위 호환성)
        AIAgent[] allAgents = FindObjectsByType<AIAgent>(FindObjectsSortMode.None);
        foreach (AIAgent agent in allAgents)
        {
            if (agent != this && agent.currentChairTransform == chair)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 주방 카운터로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToKitchenCounterBehavior()
    {
        if (currentKitchenCounter == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 대기열 위치에 실제로 도착할 때까지 대기
        float timeout = 15f;
        float timer = 0f;
        
        // NavMeshAgent가 대기열 위치를 설정받을 때까지 잠시 대기
        yield return new WaitForSeconds(0.1f);
        
        while (true)
        {
            // 타임아웃 체크
            if (timer >= timeout)
            {
                // 대기열에서 나가기
                if (currentKitchenCounter != null)
                {
                    currentKitchenCounter.LeaveQueue(this);
                }
                CleanupKitchenVariables();
                DetermineBehaviorByTime();
                yield break;
            }
            
            // NavMeshAgent가 유효하고 경로가 설정되었는지 확인
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // 경로가 계산 중이 아니고, 목적지에 도착했는지 확인
                if (!agent.pathPending && agent.remainingDistance <= arrivalDistance)
                {
                    break;
                }
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 주방 카운터 대기열 위치 도착 - 정확한 위치와 회전 설정 (반동 제거)
        transform.position = targetQueuePosition;
        if (targetQueueRotation != Quaternion.identity)
        {
            transform.rotation = targetQueueRotation;
        }
        
        // NavMeshAgent 완전 정지 (반동 제거)
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero; // velocity 강제로 0으로 설정 (애니메이션 깜빡임 방지)

        // 대기열 위치에 실제로 도착한 후 대기 상태로 전환
        isWaitingAtKitchenCounter = true;
        
        // 주방 카운터에 도착했음을 알림 (서비스 시작 가능)
        if (currentKitchenCounter != null)
        {
            // 카운터의 ProcessCustomerQueue가 자동으로 호출되어 서비스 시작 확인
        }
        
        TransitionToState(AIState.WaitingAtKitchenCounter);
    }

    /// <summary>
    /// 주방 카운터 서비스 완료 시 호출 (KitchenCounter에서 호출)
    /// </summary>
    public void OnKitchenServiceComplete()
    {
        isWaitingAtKitchenCounter = false;
        
        // 주문 결제 처리 (100원, 명성도 50)
        ProcessKitchenOrderPayment();
        
        // 의자로 이동
        if (currentChairTransform != null)
        {
            TransitionToState(AIState.MovingToChair);
        }
        else
        {
            CleanupKitchenVariables();
            DetermineBehaviorByTime();
        }
    }
    
    /// <summary>
    /// 주방 주문 결제 처리 (FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessKitchenOrderPayment()
    {
        // RoomManager를 통한 결제 처리 (FacilityPriceConfig에서 가격 자동 로드)
        if (roomManager != null)
        {
            roomManager.ProcessRestaurantPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[식당 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }

    /// <summary>
    /// 주방 관련 변수들 정리
    /// </summary>
    private void CleanupKitchenVariables()
    {
        // 🍴 식사 도구 비활성화
        if (currentEatingUtensil != null)
        {
            currentEatingUtensil.SetActive(false);
            currentEatingUtensil = null;
        }
        
        // 🍝 Pasta 오브젝트 비활성화
        if (pastaObject != null && pastaObject.activeSelf)
        {
            pastaObject.SetActive(false);
        }
        
        // 🍽️ 식사 관련 애니메이션 정리
        if (animator != null)
        {
            animator.SetBool("PickUp", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
        }
        
        // 🪑 ChairPoint 해제 (테이블 비활성화)
        if (currentChairPoint != null)
        {
            currentChairPoint.ReleaseChair(this);
            currentChairPoint = null;
        }
        
        currentKitchenCounter = null;
        currentKitchenTransform = null;
        currentChairTransform = null;
        isWaitingAtKitchenCounter = false;
        isEating = false;
        preChairPosition = Vector3.zero;
        preChairRotation = Quaternion.identity;
    }

    /// <summary>
    /// 식당으로 이동하는 코루틴 (사용하지 않음 - 호환성을 위해 유지)
    /// </summary>
    private IEnumerator MoveToKitchenBehavior()
    {
        if (currentKitchenTransform == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 식당 위치로 이동
        agent.SetDestination(currentKitchenTransform.position);

        // 식당에 도착할 때까지 대기
        float timeout = 15f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 식당에 도착했으므로 의자로 이동
        TransitionToState(AIState.MovingToChair);
    }

    /// <summary>
    /// 의자로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToChairBehavior()
    {
        if (currentChairTransform == null || currentChairTransform.gameObject == null)
        {
            CleanupKitchenVariables();
            DetermineBehaviorByTime();
            yield break;
        }

        // 🍝 의자로 이동 시작 - Pasta 오브젝트 활성화 & PickUp 애니메이션
        if (pastaObject != null)
        {
            pastaObject.SetActive(true);
            Debug.Log($"[식사] {gameObject.name}: Pasta 오브젝트 활성화");
        }
        
        if (animator != null)
        {
            animator.SetBool("PickUp", true);
            Debug.Log($"[식사] {gameObject.name}: PickUp 애니메이션 시작");
        }

        // 층간 이동 대응: XZ만 사용하고 Y는 AI의 현재 높이 유지
        Vector3 targetPos = new Vector3(
            currentChairTransform.position.x,
            transform.position.y, // 현재 AI의 Y 높이 유지
            currentChairTransform.position.z
        );
        
        agent.SetDestination(targetPos);

        // 의자에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            // 이동 중 의자 삭제 감지
            if (currentChairTransform == null || currentChairTransform.gameObject == null)
            {
                // 의자가 삭제되면 Pasta와 PickUp 정리
                if (pastaObject != null) pastaObject.SetActive(false);
                if (animator != null) animator.SetBool("PickUp", false);
                
                CleanupKitchenVariables();
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer >= timeout)
            {
                // 타임아웃 시 Pasta와 PickUp 정리
                if (pastaObject != null) pastaObject.SetActive(false);
                if (animator != null) animator.SetBool("PickUp", false);
                
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 의자 바로 앞에 도착 - 이 위치를 저장! (NavAgent가 아직 켜져 있는 상태)
        preChairPosition = transform.position;
        preChairRotation = transform.rotation;
        Debug.Log($"[식사] {gameObject.name}: 의자 앞 도착 - 현재 위치 저장 완료");

        // 의자에 도착했으므로 식사 시작
        StartEating();
    }

    /// <summary>
    /// 식사를 시작합니다.
    /// </summary>
    private void StartEating()
    {
        if (currentChairTransform == null || currentChairTransform.gameObject == null)
        {
            CleanupKitchenVariables();
            DetermineBehaviorByTime();
            return;
        }

        // 🍝 의자 도착 - Pasta 비활성화 & PickUp 애니메이션 종료
        if (pastaObject != null)
        {
            pastaObject.SetActive(false);
            Debug.Log($"[식사] {gameObject.name}: Pasta 오브젝트 비활성화");
        }
        
        if (animator != null)
        {
            animator.SetBool("PickUp", false);
            Debug.Log($"[식사] {gameObject.name}: PickUp 애니메이션 종료");
        }

        // 1단계: 현재 위치 저장
        preChairPosition = transform.position;
        preChairRotation = transform.rotation;

        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
            Debug.Log($"[식사] {gameObject.name}: NavMeshAgent 비활성화");
        }

        // 3단계: 의자 위치로 정확히 순간이동 (NavMesh.SamplePosition 사용 안 함)
        Vector3 chairPosition = currentChairTransform.position;
            transform.position = chairPosition;
        transform.rotation = currentChairTransform.rotation; // 에디터 회전값 그대로 사용
        Debug.Log($"[식사 순간이동] {gameObject.name}: 정확한 의자 위치로 이동 완료 - {chairPosition}");

        // ChairPoint 테이블 활성화 (이미 점유는 카운터 대기 시 완료)
        if (currentChairPoint != null)
        {
            currentChairPoint.OccupyChair(this, activateTable: true);
            Debug.Log($"[식사] {gameObject.name}: 테이블 활성화");
        }
        
        // Sitting 애니메이션 시작 (앉기)
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[식사] {gameObject.name}: Moving=false, Sitting=true");
        }

        // Fork 또는 Spoon 랜덤 선택 및 활성화
        bool useFork = Random.value > 0.5f;
        
        if (useFork && forkObject != null)
        {
            forkObject.SetActive(true);
            currentEatingUtensil = forkObject;
            Debug.Log($"[식사] {gameObject.name}: Fork 활성화");
        }
        else if (!useFork && spoonObject != null)
        {
            spoonObject.SetActive(true);
            currentEatingUtensil = spoonObject;
            Debug.Log($"[식사] {gameObject.name}: Spoon 활성화");
        }

        // Eating 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Eating", true);
            Debug.Log($"[식사] {gameObject.name}: Eating 애니메이션 시작");
        }

        // 식사 상태 설정
        isEating = true;
        eatingStartTime = Time.time; // 식사 시작 시간 기록
        TransitionToState(AIState.Eating);
        
        // 10초 후 자동으로 식사 종료
        eatingCoroutine = StartCoroutine(EatingTimer());
    }

    /// <summary>
    /// 식사 시간 타이머 (10초)
    /// </summary>
    private IEnumerator EatingTimer()
    {
        // 10초 대기
        yield return new WaitForSeconds(10f);
        
        // 10초 후 식사 종료
        FinishEating();
    }

    /// <summary>
    /// 식사를 종료합니다.
    /// </summary>
    private void FinishEating()
    {
        if (!isEating)
            return;

        // 식사 도구 (Fork/Spoon) 비활성화
        if (currentEatingUtensil != null)
        {
            currentEatingUtensil.SetActive(false);
            Debug.Log($"[식사 종료] {gameObject.name}: {currentEatingUtensil.name} 비활성화");
            currentEatingUtensil = null;
        }

        // ChairPoint에서 일어나기 (테이블 비활성화 + 의자 해제)
        if (currentChairPoint != null)
        {
            currentChairPoint.ReleaseChair(this);
            Debug.Log($"[식사 종료] {gameObject.name}: ChairPoint 해제 완료 - 테이블 비활성화");
        }

        // Eating 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Eating", false);
            Debug.Log($"[식사 종료] {gameObject.name}: Eating 애니메이션 종료");
        }

        // Sitting 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Sitting", false);
            Debug.Log($"[식사 종료] {gameObject.name}: Sitting 애니메이션 종료");
        }

        // NavMeshAgent 다시 활성화 (의자에서 일어나기 전에)
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[식사 종료] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 저장된 위치로 이동 (워프 아님, 실제 이동)
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(preChairPosition);
            Debug.Log($"[식사 종료] {gameObject.name}: 원래 위치로 이동 시작");
        }

        // 저장된 회전값 복원
        transform.rotation = preChairRotation;

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
        currentChairPoint = null;

        // 방이 있는 사람은 방 밖 배회, 방 없는 사람은 배회
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.UseWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }

    /// <summary>
    /// 17시 강제 디스폰 시 식사를 강제로 종료합니다.
    /// </summary>
    /// <summary>
    /// 주방 카운터 관련 활동 강제 종료
    /// </summary>
    private void ForceFinishKitchenActivity()
    {
        // 주방 카운터 대기열에서 제거
        if (currentKitchenCounter != null)
        {
            currentKitchenCounter.LeaveQueue(this);
        }

        // NavMeshAgent 상태 복원
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 주방 관련 변수들 정리
        CleanupKitchenVariables();

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
    }

    private void ForceFinishEating()
    {
        if (!isEating)
            return;

        // 식사 도구 (Fork/Spoon) 비활성화
        if (currentEatingUtensil != null)
        {
            currentEatingUtensil.SetActive(false);
            currentEatingUtensil = null;
        }

        // 🍝 Pasta 오브젝트 비활성화 (만약 활성화 상태라면)
        if (pastaObject != null && pastaObject.activeSelf)
        {
            pastaObject.SetActive(false);
        }

        // 🪑 ChairPoint에서 일어나기 (테이블 비활성화) - 강제 종료
        if (currentChairPoint != null)
        {
            currentChairPoint.ReleaseChair(this);
            Debug.Log($"[강제 식사 종료] {gameObject.name}: ChairPoint 해제 완료 - 테이블 비활성화");
        }

        // 🍽️ 모든 식사 관련 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("PickUp", false);
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
        }

        // NavMeshAgent 다시 활성화 (의자에서 일어나기 전에)
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[강제 식사 종료] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 저장된 위치로 이동 (워프 아님, 실제 이동)
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(preChairPosition);
            Debug.Log($"[강제 식사 종료] {gameObject.name}: 원래 위치로 이동 시작");
        }

        // 저장된 회전값 복원
        transform.rotation = preChairRotation;

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
        currentChairPoint = null;
    }
    #endregion

    #region 유틸리티 메서드
    private bool TryGetValidPosition(Vector3 center, float radius, int layerMask, out Vector3 result)
    {
        result = center;
        float searchRadius = radius * 0.8f;

        for (int i = 0; i < maxRetries; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                if (Vector3.Distance(hit.position, center) <= searchRadius)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void SetSpawner(AISpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    private void ReturnToPool()
    {
        CleanupCoroutines();
        CleanupResources();

        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region 오브젝트 유효성 및 타임아웃 검사
    
    private float lastObjectCheckTime = 0f;
    
    /// <summary>
    /// 애니메이션 상태 타임아웃 체크 (너무 오래 특정 상태에 있으면 배회로 전환)
    /// </summary>
    private void CheckAnimationStateTimeout()
    {
        float currentTime = Time.time;
        
        // 수면 상태 타임아웃 (최대 12시간 = 43200초 게임 시간, 실제로는 9시에 WakeUp으로 처리됨)
        // 9시가 넘었는데도 수면 중이면 강제로 깨우기
        if (isSleeping && timeSystem != null && timeSystem.CurrentHour >= 9)
        {
            WakeUp();
            return;
        }
        
        // 식사 상태 타임아웃 (최대 30초)
        if (isEating && currentTime - eatingStartTime > 30f)
        {
            // NavMeshAgent 다시 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }
            
            // 식사 상태 해제하고 배회로 전환
            isEating = false;
            if (eatingCoroutine != null)
            {
                StopCoroutine(eatingCoroutine);
                eatingCoroutine = null;
            }
            
            CleanupKitchenVariables();
            
            // 방 유무에 따라 배회 전환
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.UseWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            return;
        }
        
        // 주방 카운터 대기 상태 타임아웃 (최대 60초)
        if (isWaitingAtKitchenCounter && currentState == AIState.WaitingAtKitchenCounter)
        {
            // 주방 카운터에서 60초 이상 대기 중이면 강제로 나가기
            if (currentKitchenCounter != null)
            {
                float waitTime = currentKitchenCounter.GetWaitingTime(this);
                if (waitTime > 60f)
                {
                    currentKitchenCounter.LeaveQueue(this);
                    isWaitingAtKitchenCounter = false;
                    CleanupKitchenVariables();
                    
                    // 방 유무에 따라 배회 전환
                    if (currentRoomIndex != -1)
                    {
                        TransitionToState(AIState.UseWandering);
                    }
                    else
                    {
                        TransitionToState(AIState.Wandering);
                    }
                    return;
                }
            }
        }
    }
    
    /// <summary>
    /// 주기적으로 오브젝트 유효성 검사 (3초마다)
    /// </summary>
    private void CheckObjectValidity()
    {
        // 3초마다만 검사 (성능 최적화)
        if (Time.time - lastObjectCheckTime < 3f)
            return;
            
        lastObjectCheckTime = Time.time;
        
        // 카운터 매니저 체크
        if (counterManager != null && counterManager.gameObject == null)
        {
            HandleCounterDestroyed();
        }
        
        // 카운터 위치 체크
        if (counterPosition != null && counterPosition.gameObject == null)
        {
            HandleCounterDestroyed();
        }
        
        // 주방 카운터 체크
        if (currentKitchenCounter != null && currentKitchenCounter.gameObject == null)
        {
            HandleKitchenCounterDestroyed();
        }
        
        // 의자 체크 (식사 중일 때)
        if (isEating && currentChairTransform != null && currentChairTransform.gameObject == null)
        {
            HandleChairDestroyed();
        }
        
        // 선베드 체크 (선베드 사용 중일 때)
        if (isUsingSunbed && currentSunbedTransform != null && currentSunbedTransform.gameObject == null)
        {
            HandleSunbedDestroyed();
        }
        
        // 욕조 체크 (욕조 사용 중일 때)
        if (isUsingBathtub && currentBathtubTransform != null && currentBathtubTransform.gameObject == null)
        {
            HandleBathtubDestroyed();
        }
        
        // 운동 시설 체크 (운동 시설 사용 중일 때)
        if (isUsingHealth && currentHealthTransform != null && currentHealthTransform.gameObject == null)
        {
            HandleHealthDestroyed();
        }
        
        // 침대 체크 (수면 중일 때)
        if (isSleeping && currentBedTransform != null && currentBedTransform.gameObject == null)
        {
            HandleBedDestroyed();
        }
    }
    
    /// <summary>
    /// 카운터가 삭제되었을 때 처리
    /// </summary>
    private void HandleCounterDestroyed()
    {
        // 대기열에서 나가기
        if (isInQueue || isWaitingForService)
        {
            isInQueue = false;
            isWaitingForService = false;
        }
        
        // 코루틴 정리
        CleanupCoroutines();
        
        // 배회로 전환
        if (currentState == AIState.MovingToQueue || 
            currentState == AIState.WaitingInQueue || 
            currentState == AIState.ReportingRoomQueue)
        {
            TransitionToState(AIState.Wandering);
        }
    }
    
    /// <summary>
    /// 주방 카운터가 삭제되었을 때 처리
    /// </summary>
    private void HandleKitchenCounterDestroyed()
    {
        // NavMeshAgent 재활성화 (식사 중이었을 경우)
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // 주방 관련 변수 정리
        CleanupKitchenVariables();
        
        // 코루틴 정리
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
        
        // 방 유무에 따라 배회로 전환
        if (currentState == AIState.MovingToKitchenCounter || 
            currentState == AIState.WaitingAtKitchenCounter ||
            currentState == AIState.MovingToChair ||
            currentState == AIState.Eating)
        {
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.UseWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
        }
    }
    
    /// <summary>
    /// 의자가 삭제되었을 때 처리 (결제 처리 없음)
    /// </summary>
    private void HandleChairDestroyed()
    {
        Debug.Log($"[의자 삭제 감지] {gameObject.name}: 식사 강제 종료 (결제 처리 없음)");
        
        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Eating", false);
        }
        
        // NavMeshAgent 다시 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // 이전 위치로 복귀 (가능한 경우)
        if (preChairPosition != Vector3.zero)
        {
            if (NavMesh.SamplePosition(preChairPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }
        
        // 식사 관련 변수 정리
        isEating = false;
        currentChairTransform = null;
        currentKitchenTransform = null;
        
        // 코루틴 정리
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
        
        // 결제 처리 하지 않음 - 삭제로 인한 강제 종료
        
        // 방 유무에 따라 배회로 전환
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.UseWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }
    
    /// <summary>
    /// 선베드가 삭제되었을 때 처리 (결제 처리 없음)
    /// </summary>
    private void HandleSunbedDestroyed()
    {
        Debug.Log($"[선베드 삭제 감지] {gameObject.name}: 선베드 사용 강제 종료 (결제 처리 없음)");
        
        // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요
        
        // SunbedPoint 점유 해제
        if (currentSunbedPoint != null)
        {
            lock (sunbedPointLock)
            {
                if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint) && occupiedSunbedPoints[currentSunbedPoint] == this)
                {
                    occupiedSunbedPoints.Remove(currentSunbedPoint);
                    Debug.Log($"[SunbedPoint 점유 해제] {gameObject.name}: 선베드 삭제로 점유 해제");
                }
            }
        }
        
        // 선베드 코루틴 정리
        if (sunbedCoroutine != null)
        {
            StopCoroutine(sunbedCoroutine);
            sunbedCoroutine = null;
        }
        
        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }
        
        // NavMeshAgent 다시 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // 이전 위치로 복귀 (가능한 경우)
        if (preSunbedPosition != Vector3.zero)
        {
            if (NavMesh.SamplePosition(preSunbedPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }
        
        // 선베드 사용 상태 해제
        isUsingSunbed = false;
        // ✅ isSunbedInRoom 제거
        currentSunbedTransform = null;
        currentSunbedPoint = null;
        
        // 결제 처리 하지 않음 - 삭제로 인한 강제 종료
        
        // 배회 상태로 전환
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.RoomWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }
    
    /// <summary>
    /// 침대가 삭제되었을 때 처리 (결제 처리 없음)
    /// </summary>
    private void HandleBedDestroyed()
    {
        Debug.Log($"[침대 삭제 감지] {gameObject.name}: 수면 강제 종료 (결제 처리 없음)");
        
        // 수면 코루틴 정리
        if (sleepingCoroutine != null)
        {
            StopCoroutine(sleepingCoroutine);
            sleepingCoroutine = null;
        }
        
        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }
        
        // NavMeshAgent 다시 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // 이전 위치로 복귀 (가능한 경우)
        if (preSleepPosition != Vector3.zero)
        {
            if (NavMesh.SamplePosition(preSleepPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }
        
        // BedPoint 점유 해제
        if (currentBedPoint != null)
        {
            lock (bedPointLock)
            {
                if (occupiedBedPoints.ContainsKey(currentBedPoint) && occupiedBedPoints[currentBedPoint] == this)
                {
                    occupiedBedPoints.Remove(currentBedPoint);
                }
            }
            currentBedPoint = null;
        }
        
        // 수면 상태 해제
        isSleeping = false;
        currentBedTransform = null;
        
        // 결제 처리 하지 않음 - 삭제로 인한 강제 종료
        
        // 방 내부 배회로 전환 (방이 있는 AI만 수면 가능)
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.RoomWandering);
        }
        else
        {
            // 방이 없으면 배회
            TransitionToState(AIState.Wandering);
        }
    }
    #endregion

    #region 욕조 관련 메서드
    /// <summary>
    /// 욕조로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToBathtubBehavior()
    {
        if (currentBathtubTransform == null || currentBathtubTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 먼저 BathtubPoint 찾기
        Transform bathtubPoint = FindNearestBathtubPoint(currentBathtubTransform);
        
        // BathtubPoint가 있으면 해당 위치로, 없으면 욕조로 이동
        Vector3 targetPos = bathtubPoint != null ? bathtubPoint.position : currentBathtubTransform.position;
        Debug.Log($"[Bathtub] {gameObject.name}: {(bathtubPoint != null ? "BathtubPoint" : "욕조")}로 이동 - 목표: {targetPos}");
        
        agent.SetDestination(targetPos);

        // 목표 지점까지 이동 대기
        float timeout = 30f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentBathtubTransform == null || currentBathtubTransform.gameObject == null)
            {
                Debug.LogWarning($"[Bathtub] {gameObject.name}: 이동 중 욕조가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer >= timeout)
            {
                Debug.LogError($"[Bathtub] {gameObject.name}: 욕조 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[Bathtub] {gameObject.name}: {(bathtubPoint != null ? "BathtubPoint" : "욕조")} 바로 앞 도착! 현재 위치: {transform.position}");
        
        // 욕조 사용 시작
            StartUsingBathtub(bathtubPoint);
    }
    
    /// <summary>
    /// 욕조 주변에서 가장 가까운 BathtubPoint 찾기
    /// </summary>
    private Transform FindNearestBathtubPoint(Transform bathtub)
    {
        GameObject[] allBathtubPoints = GameObject.FindGameObjectsWithTag("BathtubPoint");
        
        if (allBathtubPoints == null || allBathtubPoints.Length == 0)
        {
            return null;
        }
        
        Transform nearestPoint = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var point in allBathtubPoints)
        {
            if (point == null) continue;
            
            float distance = Vector3.Distance(bathtub.position, point.transform.position);
            
            // 욕조 주변 5m 이내의 BathtubPoint만 고려
            if (distance < 5f && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPoint = point.transform;
            }
        }
        
        return nearestPoint;
    }

    /// <summary>
    /// 욕조 사용을 시작합니다.
    /// </summary>
    /// <param name="bathtubPoint">BathtubPoint Transform (없으면 null)</param>
    private void StartUsingBathtub(Transform bathtubPoint)
    {
        if (currentBathtubTransform == null || currentBathtubTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preBathtubPosition = transform.position;
        preBathtubRotation = transform.rotation;

        // BathtubPoint가 있으면 해당 위치로, 없으면 욕조 위치로
        Vector3 targetPosition;
        Quaternion targetRotation;
        
        if (bathtubPoint != null)
        {
            targetPosition = bathtubPoint.position;
            targetRotation = bathtubPoint.rotation;
            Debug.Log($"[욕조 포인트 이동] {gameObject.name}: BathtubPoint로 이동");
        }
        else
        {
            targetPosition = currentBathtubTransform.position;
            targetRotation = currentBathtubTransform.rotation;
            Debug.Log($"[욕조 직접 이동] {gameObject.name}: 욕조 위치로 이동");
        }
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: 욕조 위치로 정확히 순간이동 (NavMesh.SamplePosition 사용 안 함)
            transform.position = targetPosition;
        transform.rotation = targetRotation;
        Debug.Log($"[욕조 순간이동] {gameObject.name}: 정확한 욕조 위치로 이동 완료 - {targetPosition}");

        // 4단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("BedTime", true);
            Debug.Log($"[욕조 애니메이션] {gameObject.name}: Moving=false, BedTime=true");
        }

        // 상태 업데이트
        isUsingBathtub = true;
        TransitionToState(AIState.UsingBathtub);

        // 타이머 시작 (30초 사용)
        bathtubCoroutine = StartCoroutine(BathtubUsageTimer(30f));
    }

    /// <summary>
    /// 욕조 사용 타이머 코루틴
    /// </summary>
    private IEnumerator BathtubUsageTimer(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // 사용 중 욕조 삭제 감지
            if (currentBathtubTransform == null || currentBathtubTransform.gameObject == null)
            {
                HandleBathtubDestroyed();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 정상 종료
        FinishUsingBathtub();
    }

    /// <summary>
    /// 욕조 사용을 정상적으로 종료합니다.
    /// </summary>
    private void FinishUsingBathtub()
    {
        // BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // NavMeshAgent 다시 활성화
        agent.enabled = true;

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preBathtubPosition;
        transform.rotation = preBathtubRotation;
        Debug.Log($"[Bathtub] {gameObject.name}: 저장된 위치로 복귀 완료 - {preBathtubPosition}");

        // 상태 초기화
        isUsingBathtub = false;
        currentBathtubTransform = null;
        bathtubCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 욕조가 삭제되었을 때 강제 종료합니다.
    /// </summary>
    private void HandleBathtubDestroyed()
    {
        Debug.Log($"[욕조 삭제 감지] {gameObject.name}: 욕조 사용 강제 종료 (결제 처리 없음)");
        
        // 욕조 코루틴 정리
        if (bathtubCoroutine != null)
        {
            StopCoroutine(bathtubCoroutine);
            bathtubCoroutine = null;
        }
        
        // BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }
        
        // NavMeshAgent 다시 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
        
        // 이전 위치로 복귀 (가능한 경우)
        if (preBathtubPosition != Vector3.zero)
        {
            if (NavMesh.SamplePosition(preBathtubPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }
        
        // 욕조 상태 해제
        isUsingBathtub = false;
        currentBathtubTransform = null;
        
        // 결제 처리 하지 않음 - 삭제로 인한 강제 종료
        
        // 방 내부 배회로 전환 (방이 있는 AI만 욕조 사용 가능)
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.RoomWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }
    #endregion

    #region 운동 시설 관련 메서드
    /// <summary>
    /// Y 좌표를 기반으로 층 계산 (층당 4m 기준)
    /// </summary>
    private int GetFloorLevel(float yPosition)
    {
        const float floorHeight = 4f; // 층당 높이 (필요시 조정)
        return Mathf.FloorToInt(yPosition / floorHeight);
    }

    /// <summary>
    /// AI와 같은 층에 있는지 확인
    /// </summary>
    private bool IsSameFloor(float yPosition)
    {
        return GetFloorLevel(transform.position.y) == GetFloorLevel(yPosition);
    }

    /// <summary>
    /// 헬스장 바운더리 내에서 랜덤한 NavMesh 위치를 찾습니다.
    /// </summary>
    private Vector3 GetRandomPositionInHealthBounds(GameObject healthObject)
    {
        // Collider 또는 Renderer를 통해 Bounds 가져오기
        Bounds bounds;
        Collider healthCollider = healthObject.GetComponent<Collider>();
        if (healthCollider != null)
        {
            bounds = healthCollider.bounds;
        }
        else
        {
            Renderer healthRenderer = healthObject.GetComponent<Renderer>();
            if (healthRenderer != null)
            {
                bounds = healthRenderer.bounds;
            }
            else
            {
                // Bounds를 찾지 못하면 오브젝트 위치 반환
                Debug.LogWarning($"[Health] {healthObject.name}에 Collider나 Renderer가 없습니다.");
                return healthObject.transform.position;
            }
        }

        // 헬스장의 층 높이를 기준으로 Y 좌표 설정 (AI가 다른 층의 헬스장으로 갈 수 있음)
        float healthFloorY = healthObject.transform.position.y;
        int healthFloorLevel = GetFloorLevel(healthFloorY);

        // Bounds 내에서 랜덤 위치 생성 (최대 10번 시도)
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                healthFloorY, // 헬스장의 층 높이 사용
                Random.Range(bounds.min.z, bounds.max.z)
            );

            // NavMesh 위에서 유효한 위치 찾기 (헬스장 층의 NavMesh)
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                // 찾은 위치가 헬스장과 같은 층인지 확인
                if (GetFloorLevel(hit.position.y) == healthFloorLevel)
                {
                    Debug.Log($"[Health] 랜덤 위치 찾음: {hit.position} (층: {GetFloorLevel(hit.position.y)})");
                    return hit.position;
                }
            }
        }

        // 랜덤 위치를 찾지 못하면 헬스장 층의 중심 반환
        Debug.LogWarning($"[Health] 랜덤 위치를 찾지 못해 중심 위치 사용");
        Vector3 centerAtHealthFloor = new Vector3(bounds.center.x, healthFloorY, bounds.center.z);
        if (NavMesh.SamplePosition(centerAtHealthFloor, out NavMeshHit centerHit, 5f, NavMesh.AllAreas))
        {
            if (GetFloorLevel(centerHit.position.y) == healthFloorLevel)
            {
                return centerHit.position;
            }
        }
        
        return centerAtHealthFloor;
    }

    /// <summary>
    /// 운동 시설로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToHealthBehavior()
    {
        if (currentHealthTransform == null || currentHealthTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Health 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 healthFacilityPos = currentHealthTransform.position;
        Debug.Log($"[Health 1단계] {gameObject.name}: Health 시설 근처로 이동 시작 - 목표: {healthFacilityPos}");
        
        bool pathSet = agent.SetDestination(healthFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Health 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        // Health 시설 근처까지 도착 대기 (XZ 거리 + Y 높이 모두 체크)
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f; // Y 좌표 허용 오차 1m
        
        while (true)
        {
            if (currentHealthTransform == null || currentHealthTransform.gameObject == null)
            {
                Debug.LogWarning($"[Health 1단계] {gameObject.name}: 이동 중 헬스장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            // NavMesh 경로 계산 완료 후 체크
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(healthFacilityPos.x, healthFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - healthFacilityPos.y);
                
                // XZ 거리가 5m 이내이고, Y 높이 차이가 1m 이내면 도착
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Health 1단계 완료] {gameObject.name}: Health 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m, 현재 Y: {transform.position.y:F2}, 목표 Y: {healthFacilityPos.y:F2}");
                    break;
                }
                
                // 디버그: 진행 상황 출력 (0.5초마다)
                if (timer1 % 0.5f < Time.deltaTime)
                {
                    Debug.Log($"[Health 1단계 진행중] {gameObject.name}: XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m, remainingDistance: {agent.remainingDistance:F2}m");
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Health 1단계] {gameObject.name}: Health 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: HealthPoint 찾기 =====
        Transform healthPoint = FindNearestHealthPoint(currentHealthTransform);
        
        if (healthPoint == null)
        {
            Debug.Log($"[Health 2단계] {gameObject.name}: 사용 가능한 HealthPoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Health 2단계] {gameObject.name}: HealthPoint 발견 - {healthPoint.name}, 위치: {healthPoint.position}");
        
        // ===== 3단계: HealthPoint로 이동 =====
        Vector3 targetPos = healthPoint.position;
        Debug.Log($"[Health 3단계] {gameObject.name}: HealthPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Health 3단계] {gameObject.name}: HealthPoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        // HealthPoint 바로 앞까지 도착 대기
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentHealthTransform == null || currentHealthTransform.gameObject == null)
            {
                Debug.LogWarning($"[Health 3단계] {gameObject.name}: 이동 중 헬스장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Health 3단계] {gameObject.name}: HealthPoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Health 3단계 완료] {gameObject.name}: HealthPoint 바로 앞 도착! 현재 위치: {transform.position}, Y: {transform.position.y}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingHealth(healthPoint);
    }
    
    /// <summary>
    /// 헬스장 주변에서 가장 가까운 사용 가능한 HealthPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestHealthPoint(Transform health)
    {
        GameObject[] allHealthPoints = GameObject.FindGameObjectsWithTag("HealthPoint");
        
        if (allHealthPoints == null || allHealthPoints.Length == 0)
        {
            return null;
        }
        
        lock (healthPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allHealthPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedHealthPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedHealthPoints[point.transform] == null)
                    {
                        occupiedHealthPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(health.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 HealthPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedHealthPoints[nearestPoint] = this;
                currentHealthPoint = nearestPoint;
                Debug.Log($"[HealthPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedHealthPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 운동 시설 사용을 시작합니다.
    /// </summary>
    /// <param name="healthPoint">HealthPoint Transform (없으면 null)</param>
    private void StartUsingHealth(Transform healthPoint = null)
    {
        if (currentHealthTransform == null || currentHealthTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            return;
        }

        if (healthPoint == null)
        {
            Debug.LogError($"[Health] {gameObject.name}: HealthPoint가 null입니다!");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장 (NavMesh 위의 위치 - Point 바로 앞)
        preHealthPosition = transform.position;
        preHealthRotation = transform.rotation;
        Debug.Log($"[Health] {gameObject.name}: 이전 위치 저장 완료 - {preHealthPosition}");
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero; // velocity도 0으로 초기화
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
            Debug.Log($"[Health] {gameObject.name}: NavMeshAgent 비활성화");
        }

        // 3단계: HealthPoint 위치로 정확히 순간이동
        transform.position = healthPoint.position;
        transform.rotation = healthPoint.rotation;
        Debug.Log($"[Health 순간이동] {gameObject.name}: 정확한 HealthPoint 위치로 이동 완료 - {healthPoint.position}");

        // 덤벨 활성화 (애니메이션 시작 직전)
        if (leftDumbbell != null) 
        {
            leftDumbbell.SetActive(true);
            Debug.Log($"[Health Props] {gameObject.name}: 왼손 덤벨 활성화");
        }
        if (rightDumbbell != null) 
        {
            rightDumbbell.SetActive(true);
            Debug.Log($"[Health Props] {gameObject.name}: 오른손 덤벨 활성화");
        }

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Exercise", true);
            Debug.Log($"[Health 애니메이션] {gameObject.name}: Moving=false, Exercise=true");
        }

        // 상태 업데이트
        isUsingHealth = true;
        TransitionToState(AIState.UsingHealth);

        int aiFloor = GetFloorLevel(transform.position.y);
        int healthFloor = GetFloorLevel(currentHealthTransform.position.y);
        Debug.Log($"[운동 시설 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 헬스장 층: {healthFloor}");
    }

    /// <summary>
    /// 운동 시설 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingHealth()
    {
        if (!isUsingHealth)
        {
            return;
        }

        Debug.Log($"[운동 시설 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // HealthPoint 점유 해제
        if (currentHealthPoint != null)
        {
            lock (healthPointLock)
            {
                if (occupiedHealthPoints.ContainsKey(currentHealthPoint) && occupiedHealthPoints[currentHealthPoint] == this)
                {
                    occupiedHealthPoints.Remove(currentHealthPoint);
                    Debug.Log($"[HealthPoint 점유 해제] {gameObject.name}: {currentHealthPoint.name} 점유 해제 (점유 중: {occupiedHealthPoints.Count}개)");
                }
            }
            currentHealthPoint = null;
        }

        // 덤벨 비활성화 (애니메이션 종료 직전)
        if (leftDumbbell != null) 
        {
            leftDumbbell.SetActive(false);
            Debug.Log($"[Health Props] {gameObject.name}: 왼손 덤벨 비활성화");
        }
        if (rightDumbbell != null) 
        {
            rightDumbbell.SetActive(false);
            Debug.Log($"[Health Props] {gameObject.name}: 오른손 덤벨 비활성화");
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Exercise", false);
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preHealthPosition;
        transform.rotation = preHealthRotation;
        Debug.Log($"[Health] {gameObject.name}: 저장된 위치로 복귀 완료 - {preHealthPosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Health] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 운동 시설 결제 처리 (방 안 시설이면 무료, 방 밖이면 유료)
        ProcessHealthFacilityPayment();

        // 상태 초기화
        isUsingHealth = false;
        currentHealthTransform = null;
        healthCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 운동 시설 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessHealthFacilityPayment()
    {
        // RoomManager를 통한 결제 처리 (방 유무 상관없이 유료)
        if (roomManager != null)
        {
            roomManager.ProcessHealthFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[운동 시설 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }

    #endregion

    #region 예식장 관련 메서드
    /// <summary>
    /// 예식장으로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToWeddingBehavior()
    {
        if (currentWeddingTransform == null || currentWeddingTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Wedding 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 weddingFacilityPos = currentWeddingTransform.position;
        Debug.Log($"[Wedding 1단계] {gameObject.name}: Wedding 시설 근처로 이동 시작 - 목표: {weddingFacilityPos}");
        
        bool pathSet = agent.SetDestination(weddingFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Wedding 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        // Wedding 시설 근처까지 도착 대기 (XZ 거리 + Y 높이 모두 체크)
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentWeddingTransform == null || currentWeddingTransform.gameObject == null)
            {
                Debug.LogWarning($"[Wedding 1단계] {gameObject.name}: 이동 중 예식장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(weddingFacilityPos.x, weddingFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - weddingFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Wedding 1단계 완료] {gameObject.name}: Wedding 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Wedding 1단계] {gameObject.name}: Wedding 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: WeddingPoint 찾기 =====
        Transform weddingPoint = FindNearestWeddingPoint(currentWeddingTransform);
        
        if (weddingPoint == null)
        {
            Debug.Log($"[Wedding 2단계] {gameObject.name}: 사용 가능한 WeddingPoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Wedding 2단계] {gameObject.name}: WeddingPoint 발견 - {weddingPoint.name}, 위치: {weddingPoint.position}");
        
        // ===== 3단계: WeddingPoint로 이동 =====
        Vector3 targetPos = weddingPoint.position;
        Debug.Log($"[Wedding 3단계] {gameObject.name}: WeddingPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Wedding 3단계] {gameObject.name}: WeddingPoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentWeddingTransform == null || currentWeddingTransform.gameObject == null)
            {
                Debug.LogWarning($"[Wedding 3단계] {gameObject.name}: 이동 중 예식장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Wedding 3단계] {gameObject.name}: WeddingPoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Wedding 3단계 완료] {gameObject.name}: WeddingPoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingWedding(weddingPoint);
    }
    
    /// <summary>
    /// 예식장 주변에서 가장 가까운 사용 가능한 WeddingPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestWeddingPoint(Transform wedding)
    {
        GameObject[] allWeddingPoints = GameObject.FindGameObjectsWithTag("WeddingPoint");
        
        if (allWeddingPoints == null || allWeddingPoints.Length == 0)
        {
            return null;
        }
        
        lock (weddingPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allWeddingPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedWeddingPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedWeddingPoints[point.transform] == null)
                    {
                        occupiedWeddingPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(wedding.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 WeddingPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedWeddingPoints[nearestPoint] = this;
                currentWeddingPoint = nearestPoint;
                Debug.Log($"[WeddingPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedWeddingPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 예식장 바운더리 내에서 랜덤한 NavMesh 위치를 찾습니다.
    /// </summary>
    private Vector3 GetRandomPositionInWeddingBounds(GameObject weddingObject)
    {
        // Collider 또는 Renderer를 통해 Bounds 가져오기
        Bounds bounds;
        Collider weddingCollider = weddingObject.GetComponent<Collider>();
        if (weddingCollider != null)
        {
            bounds = weddingCollider.bounds;
        }
        else
        {
            Renderer weddingRenderer = weddingObject.GetComponent<Renderer>();
            if (weddingRenderer != null)
            {
                bounds = weddingRenderer.bounds;
            }
            else
            {
                // Bounds를 찾지 못하면 오브젝트 위치 반환
                Debug.LogWarning($"[Wedding] {weddingObject.name}에 Collider나 Renderer가 없습니다.");
                return weddingObject.transform.position;
            }
        }

        // 예식장의 층 높이를 기준으로 Y 좌표 설정
        float weddingFloorY = weddingObject.transform.position.y;
        int weddingFloorLevel = GetFloorLevel(weddingFloorY);

        // Bounds 내에서 랜덤 위치 생성 (최대 10번 시도)
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                weddingFloorY, // 예식장의 층 높이 사용
                Random.Range(bounds.min.z, bounds.max.z)
            );

            // NavMesh 위에서 유효한 위치 찾기 (예식장 층의 NavMesh)
            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Debug.Log($"[Wedding] {gameObject.name}: 예식장 내 랜덤 NavMesh 위치 찾음: {hit.position} (층: {weddingFloorLevel})");
                return hit.position;
            }
        }

        // 10번 시도 후에도 실패하면 예식장 위치 반환
        Debug.LogWarning($"[Wedding] {gameObject.name}: NavMesh 위치를 찾지 못해 예식장 위치로 대체 (층: {weddingFloorLevel})");
        return weddingObject.transform.position;
    }

    /// <summary>
    /// 예식장 사용을 시작합니다.
    /// </summary>
    /// <param name="weddingPoint">WeddingPoint Transform (없으면 null)</param>
    private void StartUsingWedding(Transform weddingPoint = null)
    {
        if (currentWeddingTransform == null || currentWeddingTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            return;
        }

        if (weddingPoint == null)
        {
            Debug.LogError($"[Wedding] {gameObject.name}: WeddingPoint가 null입니다!");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장 (NavMesh 위의 위치 - Point 바로 앞)
        preWeddingPosition = transform.position;
        preWeddingRotation = transform.rotation;
        Debug.Log($"[Wedding] {gameObject.name}: 이전 위치 저장 완료 - {preWeddingPosition}");
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
            Debug.Log($"[Wedding] {gameObject.name}: NavMeshAgent 비활성화");
        }

        // 3단계: WeddingPoint 위치로 정확히 순간이동
        transform.position = weddingPoint.position;
        transform.rotation = weddingPoint.rotation;
        Debug.Log($"[Wedding 순간이동] {gameObject.name}: 정확한 WeddingPoint 위치로 이동 완료 - {weddingPoint.position}");

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[Wedding 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
        }
        else
        {
            Debug.LogError($"[Wedding 애니메이션] {gameObject.name}: Animator가 null입니다!");
        }

        // 상태 업데이트
        isUsingWedding = true;
        TransitionToState(AIState.UsingWedding);

        int aiFloor = GetFloorLevel(transform.position.y);
        int weddingFloor = GetFloorLevel(currentWeddingTransform.position.y);
        Debug.Log($"[예식장 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 예식장 층: {weddingFloor}");
    }

    /// <summary>
    /// 예식장 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingWedding()
    {
        if (!isUsingWedding)
        {
            return;
        }

        Debug.Log($"[예식장 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // WeddingPoint 점유 해제
        if (currentWeddingPoint != null)
        {
            lock (weddingPointLock)
            {
                if (occupiedWeddingPoints.ContainsKey(currentWeddingPoint) && occupiedWeddingPoints[currentWeddingPoint] == this)
                {
                    occupiedWeddingPoints.Remove(currentWeddingPoint);
                    Debug.Log($"[WeddingPoint 점유 해제] {gameObject.name}: {currentWeddingPoint.name} 점유 해제 (점유 중: {occupiedWeddingPoints.Count}개)");
                }
            }
            currentWeddingPoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Sitting", false);
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preWeddingPosition;
        transform.rotation = preWeddingRotation;
        Debug.Log($"[Wedding] {gameObject.name}: 저장된 위치로 복귀 완료 - {preWeddingPosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Wedding] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 예식장 결제 처리 (방 안 시설이면 무료, 방 밖이면 유료)
        ProcessWeddingFacilityPayment();

        // 상태 초기화
        isUsingWedding = false;
        currentWeddingTransform = null;
        weddingCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 예식장 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessWeddingFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessWeddingFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[예식장 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region 라운지 관련 메서드
    /// <summary>
    /// 라운지로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToLoungeBehavior()
    {
        if (currentLoungeTransform == null || currentLoungeTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Lounge 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 loungeFacilityPos = currentLoungeTransform.position;
        Debug.Log($"[Lounge 1단계] {gameObject.name}: Lounge 시설 근처로 이동 시작 - 목표: {loungeFacilityPos}");
        
        bool pathSet = agent.SetDestination(loungeFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Lounge 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentLoungeTransform == null || currentLoungeTransform.gameObject == null)
            {
                Debug.LogWarning($"[Lounge 1단계] {gameObject.name}: 이동 중 라운지가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(loungeFacilityPos.x, loungeFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - loungeFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Lounge 1단계 완료] {gameObject.name}: Lounge 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Lounge 1단계] {gameObject.name}: Lounge 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: LoungePoint 찾기 =====
        Transform loungePoint = FindNearestLoungePoint(currentLoungeTransform);
        
        if (loungePoint == null)
        {
            Debug.Log($"[Lounge 2단계] {gameObject.name}: 사용 가능한 LoungePoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Lounge 2단계] {gameObject.name}: LoungePoint 발견 - {loungePoint.name}, 위치: {loungePoint.position}");
        
        // ===== 3단계: LoungePoint로 이동 =====
        Vector3 targetPos = loungePoint.position;
        Debug.Log($"[Lounge 3단계] {gameObject.name}: LoungePoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Lounge 3단계] {gameObject.name}: LoungePoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentLoungeTransform == null || currentLoungeTransform.gameObject == null)
            {
                Debug.LogWarning($"[Lounge 3단계] {gameObject.name}: 이동 중 라운지가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Lounge 3단계] {gameObject.name}: LoungePoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Lounge 3단계 완료] {gameObject.name}: LoungePoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingLounge(loungePoint);
    }
    
    /// <summary>
    /// 라운지 주변에서 가장 가까운 사용 가능한 LoungePoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestLoungePoint(Transform lounge)
    {
        GameObject[] allLoungePoints = GameObject.FindGameObjectsWithTag("LoungePoint");
        
        if (allLoungePoints == null || allLoungePoints.Length == 0)
        {
            return null;
        }
        
        lock (loungePointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allLoungePoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedLoungePoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedLoungePoints[point.transform] == null)
                    {
                        occupiedLoungePoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(lounge.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 LoungePoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedLoungePoints[nearestPoint] = this;
                currentLoungePoint = nearestPoint;
                Debug.Log($"[LoungePoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedLoungePoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 라운지 사용을 시작합니다.
    /// </summary>
    /// <param name="loungePoint">LoungePoint Transform (null이면 자동으로 찾음)</param>
    private void StartUsingLounge(Transform loungePoint)
    {
        // loungePoint가 null이면 자동으로 찾기
        if (loungePoint == null && currentLoungeTransform != null)
        {
            loungePoint = FindNearestLoungePoint(currentLoungeTransform);
        }
        
        if (loungePoint == null)
        {
            Debug.LogWarning($"[라운지 사용 실패] {gameObject.name}: LoungePoint를 찾을 수 없습니다.");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preLoungePosition = transform.position;
        preLoungeRotation = transform.rotation;

        currentLoungePoint = loungePoint;
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: LoungePoint로 정확히 순간이동
        transform.position = loungePoint.position;
        transform.rotation = loungePoint.rotation;
        
        Debug.Log($"[Lounge 순간이동] {gameObject.name}: 정확한 LoungePoint 위치로 이동 완료 - {loungePoint.position}");

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[Lounge 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
        }
        else
        {
            Debug.LogError($"[Lounge 애니메이션] {gameObject.name}: Animator가 null입니다!");
        }

        // 상태 플래그 설정
        isUsingLounge = true;
        TransitionToState(AIState.UsingLounge);

        int aiFloor = GetFloorLevel(transform.position.y);
        int loungeFloor = GetFloorLevel(currentLoungeTransform.position.y);
        Debug.Log($"[라운지 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 라운지 층: {loungeFloor}");
    }

    /// <summary>
    /// 라운지 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingLounge()
    {
        if (!isUsingLounge)
        {
            return;
        }

        Debug.Log($"[라운지 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // LoungePoint 점유 해제
        if (currentLoungePoint != null)
        {
            lock (loungePointLock)
            {
                if (occupiedLoungePoints.ContainsKey(currentLoungePoint) && occupiedLoungePoints[currentLoungePoint] == this)
                {
                    occupiedLoungePoints.Remove(currentLoungePoint);
                    Debug.Log($"[LoungePoint 점유 해제] {gameObject.name}: {currentLoungePoint.name} 점유 해제 (점유 중: {occupiedLoungePoints.Count}개)");
                }
            }
            currentLoungePoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Sitting", false);
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preLoungePosition;
        transform.rotation = preLoungeRotation;
        Debug.Log($"[Lounge] {gameObject.name}: 저장된 위치로 복귀 완료 - {preLoungePosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Lounge] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 라운지 결제 처리
        ProcessLoungeFacilityPayment();

        // 상태 초기화
        isUsingLounge = false;
        currentLoungeTransform = null;
        loungeCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 라운지 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessLoungeFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessLoungeFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[라운지 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region 연회장 관련 메서드
    /// <summary>
    /// 연회장으로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToHallBehavior()
    {
        if (currentHallTransform == null || currentHallTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Hall 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 hallFacilityPos = currentHallTransform.position;
        Debug.Log($"[Hall 1단계] {gameObject.name}: Hall 시설 근처로 이동 시작 - 목표: {hallFacilityPos}");
        
        bool pathSet = agent.SetDestination(hallFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Hall 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentHallTransform == null || currentHallTransform.gameObject == null)
            {
                Debug.LogWarning($"[Hall 1단계] {gameObject.name}: 이동 중 연회장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(hallFacilityPos.x, hallFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - hallFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Hall 1단계 완료] {gameObject.name}: Hall 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Hall 1단계] {gameObject.name}: Hall 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: HallPoint 찾기 =====
        Transform hallPoint = FindNearestHallPoint(currentHallTransform);
        
        if (hallPoint == null)
        {
            Debug.Log($"[Hall 2단계] {gameObject.name}: 사용 가능한 HallPoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Hall 2단계] {gameObject.name}: HallPoint 발견 - {hallPoint.name}, 위치: {hallPoint.position}");
        
        // ===== 3단계: HallPoint로 이동 =====
        Vector3 targetPos = hallPoint.position;
        Debug.Log($"[Hall 3단계] {gameObject.name}: HallPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Hall 3단계] {gameObject.name}: HallPoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentHallTransform == null || currentHallTransform.gameObject == null)
            {
                Debug.LogWarning($"[Hall 3단계] {gameObject.name}: 이동 중 연회장이 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Hall 3단계] {gameObject.name}: HallPoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Hall 3단계 완료] {gameObject.name}: HallPoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingHall(hallPoint);
    }
    
    /// <summary>
    /// 연회장 주변에서 가장 가까운 사용 가능한 HallPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestHallPoint(Transform hall)
    {
        GameObject[] allHallPoints = GameObject.FindGameObjectsWithTag("HallPoint");
        
        if (allHallPoints == null || allHallPoints.Length == 0)
        {
            return null;
        }
        
        lock (hallPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allHallPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedHallPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedHallPoints[point.transform] == null)
                    {
                        occupiedHallPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(hall.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 HallPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedHallPoints[nearestPoint] = this;
                currentHallPoint = nearestPoint;
                Debug.Log($"[HallPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedHallPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 연회장 사용을 시작합니다.
    /// </summary>
    /// <param name="hallPoint">HallPoint Transform (null이면 자동으로 찾음)</param>
    private void StartUsingHall(Transform hallPoint)
    {
        // hallPoint가 null이면 자동으로 찾기
        if (hallPoint == null && currentHallTransform != null)
        {
            hallPoint = FindNearestHallPoint(currentHallTransform);
        }
        
        if (hallPoint == null)
        {
            Debug.LogWarning($"[연회장 사용 실패] {gameObject.name}: HallPoint를 찾을 수 없습니다.");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preHallPosition = transform.position;
        preHallRotation = transform.rotation;

        currentHallPoint = hallPoint;
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: HallPoint로 정확히 순간이동
        transform.position = hallPoint.position;
        transform.rotation = hallPoint.rotation;
        
        Debug.Log($"[Hall 순간이동] {gameObject.name}: 정확한 HallPoint 위치로 이동 완료 - {hallPoint.position}");

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[Hall 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
        }
        else
        {
            Debug.LogError($"[Hall 애니메이션] {gameObject.name}: Animator가 null입니다!");
        }

        // 상태 플래그 설정
        isUsingHall = true;
        TransitionToState(AIState.UsingHall);

        int aiFloor = GetFloorLevel(transform.position.y);
        int hallFloor = GetFloorLevel(currentHallTransform.position.y);
        Debug.Log($"[연회장 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 연회장 층: {hallFloor}");
    }

    /// <summary>
    /// 연회장 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingHall()
    {
        if (!isUsingHall)
        {
            return;
        }

        Debug.Log($"[연회장 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // HallPoint 점유 해제
        if (currentHallPoint != null)
        {
            lock (hallPointLock)
            {
                if (occupiedHallPoints.ContainsKey(currentHallPoint) && occupiedHallPoints[currentHallPoint] == this)
                {
                    occupiedHallPoints.Remove(currentHallPoint);
                    Debug.Log($"[HallPoint 점유 해제] {gameObject.name}: {currentHallPoint.name} 점유 해제 (점유 중: {occupiedHallPoints.Count}개)");
                }
            }
            currentHallPoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Sitting", false);
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preHallPosition;
        transform.rotation = preHallRotation;
        Debug.Log($"[Hall] {gameObject.name}: 저장된 위치로 복귀 완료 - {preHallPosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Hall] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 연회장 결제 처리
        ProcessHallFacilityPayment();

        // 상태 초기화
        isUsingHall = false;
        currentHallTransform = null;
        hallCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 연회장 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessHallFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessHallFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[연회장 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region 사우나 관련 메서드
    /// <summary>
    /// 사우나로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToSaunaBehavior()
    {
        if (currentSaunaTransform == null || currentSaunaTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Sauna 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 saunaFacilityPos = currentSaunaTransform.position;
        Debug.Log($"[Sauna 1단계] {gameObject.name}: Sauna 시설 근처로 이동 시작 - 목표: {saunaFacilityPos}");
        
        bool pathSet = agent.SetDestination(saunaFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Sauna 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentSaunaTransform == null || currentSaunaTransform.gameObject == null)
            {
                Debug.LogWarning($"[Sauna 1단계] {gameObject.name}: 이동 중 사우나가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(saunaFacilityPos.x, saunaFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - saunaFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Sauna 1단계 완료] {gameObject.name}: Sauna 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Sauna 1단계] {gameObject.name}: Sauna 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: SaunaPoint 찾기 (랜덤 선택) =====
        bool useSitPoint = Random.value < 0.5f;
        Transform saunaPoint = null;
        
        if (useSitPoint)
        {
            saunaPoint = FindNearestSaunaSitPoint(currentSaunaTransform);
            if (saunaPoint != null)
            {
                isSaunaSitting = true;
                Debug.Log($"[Sauna 2단계] {gameObject.name}: SaunaSitPoint 선택 (앉기)");
            }
        }
        else
        {
            saunaPoint = FindNearestSaunaDownPoint(currentSaunaTransform);
            if (saunaPoint != null)
            {
                isSaunaSitting = false;
                Debug.Log($"[Sauna 2단계] {gameObject.name}: SaunaDownPoint 선택 (눕기)");
            }
        }
        
        if (saunaPoint == null)
        {
            if (useSitPoint)
            {
                saunaPoint = FindNearestSaunaDownPoint(currentSaunaTransform);
                if (saunaPoint != null)
                {
                    isSaunaSitting = false;
                    Debug.Log($"[Sauna 2단계] {gameObject.name}: SaunaSitPoint 없음 - SaunaDownPoint로 대체");
                }
            }
            else
            {
                saunaPoint = FindNearestSaunaSitPoint(currentSaunaTransform);
                if (saunaPoint != null)
                {
                    isSaunaSitting = true;
                    Debug.Log($"[Sauna 2단계] {gameObject.name}: SaunaDownPoint 없음 - SaunaSitPoint로 대체");
                }
            }
        }
        
        if (saunaPoint == null)
        {
            Debug.Log($"[Sauna 2단계] {gameObject.name}: 사용 가능한 사우나 포인트 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Sauna 2단계] {gameObject.name}: SaunaPoint 발견 - {saunaPoint.name}, 위치: {saunaPoint.position}");
        
        // ===== 3단계: SaunaPoint로 이동 =====
        Vector3 targetPos = saunaPoint.position;
        Debug.Log($"[Sauna 3단계] {gameObject.name}: SaunaPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Sauna 3단계] {gameObject.name}: SaunaPoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentSaunaTransform == null || currentSaunaTransform.gameObject == null)
            {
                Debug.LogWarning($"[Sauna 3단계] {gameObject.name}: 이동 중 사우나가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Sauna 3단계] {gameObject.name}: SaunaPoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Sauna 3단계 완료] {gameObject.name}: SaunaPoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingSauna(saunaPoint);
    }
    
    /// <summary>
    /// 사우나 주변에서 가장 가까운 사용 가능한 SaunaSitPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestSaunaSitPoint(Transform sauna)
    {
        GameObject[] allSaunaSitPoints = GameObject.FindGameObjectsWithTag("SaunaSitPoint");
        
        if (allSaunaSitPoints == null || allSaunaSitPoints.Length == 0)
        {
            return null;
        }
        
        lock (saunaSitPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allSaunaSitPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedSaunaSitPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedSaunaSitPoints[point.transform] == null)
                    {
                        occupiedSaunaSitPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(sauna.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 SaunaSitPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedSaunaSitPoints[nearestPoint] = this;
                currentSaunaPoint = nearestPoint;
                Debug.Log($"[SaunaSitPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedSaunaSitPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }
    
    /// <summary>
    /// 사우나 주변에서 가장 가까운 사용 가능한 SaunaDownPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestSaunaDownPoint(Transform sauna)
    {
        GameObject[] allSaunaDownPoints = GameObject.FindGameObjectsWithTag("SaunaDownPoint");
        
        if (allSaunaDownPoints == null || allSaunaDownPoints.Length == 0)
        {
            return null;
        }
        
        lock (saunaDownPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allSaunaDownPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedSaunaDownPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedSaunaDownPoints[point.transform] == null)
                    {
                        occupiedSaunaDownPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(sauna.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 SaunaDownPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedSaunaDownPoints[nearestPoint] = this;
                currentSaunaPoint = nearestPoint;
                Debug.Log($"[SaunaDownPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedSaunaDownPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 사우나 사용을 시작합니다.
    /// </summary>
    /// <param name="saunaPoint">사우나 포인트 Transform (null이면 자동으로 찾음)</param>
    private void StartUsingSauna(Transform saunaPoint)
    {
        // saunaPoint가 null이면 자동으로 찾기
        if (saunaPoint == null && currentSaunaTransform != null)
        {
            // 랜덤으로 다시 선택
            bool useSitPoint = Random.value < 0.5f;
            if (useSitPoint)
            {
                saunaPoint = FindNearestSaunaSitPoint(currentSaunaTransform);
                if (saunaPoint != null) isSaunaSitting = true;
            }
            else
            {
                saunaPoint = FindNearestSaunaDownPoint(currentSaunaTransform);
                if (saunaPoint != null) isSaunaSitting = false;
            }
            
            // 선택한 타입이 없으면 다른 타입 시도
            if (saunaPoint == null)
            {
                if (useSitPoint)
                {
                    saunaPoint = FindNearestSaunaDownPoint(currentSaunaTransform);
                    if (saunaPoint != null) isSaunaSitting = false;
                }
                else
                {
                    saunaPoint = FindNearestSaunaSitPoint(currentSaunaTransform);
                    if (saunaPoint != null) isSaunaSitting = true;
                }
            }
        }
        
        if (saunaPoint == null)
        {
            Debug.LogWarning($"[사우나 사용 실패] {gameObject.name}: 사우나 포인트를 찾을 수 없습니다.");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preSaunaPosition = transform.position;
        preSaunaRotation = transform.rotation;

        currentSaunaPoint = saunaPoint;
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: SaunaPoint로 정확히 순간이동
        transform.position = saunaPoint.position;
        transform.rotation = saunaPoint.rotation;
        
        Debug.Log($"[Sauna 순간이동] {gameObject.name}: 정확한 SaunaPoint 위치로 이동 완료 - {saunaPoint.position}");

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            if (isSaunaSitting)
            {
                animator.SetBool("Sitting", true);
                Debug.Log($"[사우나 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
            }
            else
            {
                animator.SetBool("BedTime", true);
                Debug.Log($"[사우나 애니메이션] {gameObject.name}: Moving=false, BedTime=true");
            }
        }

        // 상태 플래그 설정
        isUsingSauna = true;
        TransitionToState(AIState.UsingSauna);

        int aiFloor = GetFloorLevel(transform.position.y);
        int saunaFloor = GetFloorLevel(currentSaunaTransform.position.y);
        Debug.Log($"[사우나 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 사우나 층: {saunaFloor}, 타입: {(isSaunaSitting ? "앉기" : "눕기")}");
    }

    /// <summary>
    /// 사우나 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingSauna()
    {
        if (!isUsingSauna)
        {
            return;
        }

        Debug.Log($"[사우나 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // 사우나 포인트 점유 해제
        if (currentSaunaPoint != null)
        {
            if (isSaunaSitting)
            {
                lock (saunaSitPointLock)
                {
                    if (occupiedSaunaSitPoints.ContainsKey(currentSaunaPoint) && occupiedSaunaSitPoints[currentSaunaPoint] == this)
                    {
                        occupiedSaunaSitPoints.Remove(currentSaunaPoint);
                        Debug.Log($"[SaunaSitPoint 점유 해제] {gameObject.name}: {currentSaunaPoint.name} 점유 해제 (점유 중: {occupiedSaunaSitPoints.Count}개)");
                    }
                }
            }
            else
            {
                lock (saunaDownPointLock)
                {
                    if (occupiedSaunaDownPoints.ContainsKey(currentSaunaPoint) && occupiedSaunaDownPoints[currentSaunaPoint] == this)
                    {
                        occupiedSaunaDownPoints.Remove(currentSaunaPoint);
                        Debug.Log($"[SaunaDownPoint 점유 해제] {gameObject.name}: {currentSaunaPoint.name} 점유 해제 (점유 중: {occupiedSaunaDownPoints.Count}개)");
                    }
                }
            }
            currentSaunaPoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            if (isSaunaSitting)
            {
                animator.SetBool("Sitting", false);
            }
            else
            {
                animator.SetBool("BedTime", false);
            }
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preSaunaPosition;
        transform.rotation = preSaunaRotation;
        Debug.Log($"[Sauna] {gameObject.name}: 저장된 위치로 복귀 완료 - {preSaunaPosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Sauna] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 사우나 결제 처리
        ProcessSaunaFacilityPayment();

        // 상태 초기화
        isUsingSauna = false;
        isSaunaSitting = false;
        currentSaunaTransform = null;
        saunaCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 사우나 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessSaunaFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessSaunaFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[사우나 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region 카페 관련 메서드
    /// <summary>
    /// 일반 손님용: 맵에서 Cafe 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool TryFindAvailableCafe()
    {
        GameObject[] allCafes = GameObject.FindGameObjectsWithTag("Cafe");
        
        if (allCafes.Length == 0)
        {
            return false;
        }

        // 같은 층에서 가장 가까운 카페 우선 찾기
        Transform nearestSameFloorCafe = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 카페 (백업)
        Transform nearestOtherFloorCafe = null;
        float minOtherFloorDistance = float.MaxValue;

        foreach (var cafe in allCafes)
        {
            if (cafe != null)
            {
                float distance = Vector3.Distance(transform.position, cafe.transform.position);
                
                if (IsSameFloor(cafe.transform.position.y))
                {
                    // 같은 층
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorCafe = cafe.transform;
                    }
                }
                else
                {
                    // 다른 층
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorCafe = cafe.transform;
                    }
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층
        Transform selectedCafe = nearestSameFloorCafe ?? nearestOtherFloorCafe;

        if (selectedCafe != null)
        {
            currentCafeTransform = selectedCafe;
            preCafePosition = transform.position;
            preCafeRotation = transform.rotation;
            
            Debug.Log($"[Cafe 찾기] {gameObject.name}: 카페 찾음 (층: {GetFloorLevel(selectedCafe.position.y)})");
            
            TransitionToState(AIState.MovingToCafe);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Cafe 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindCafeInCurrentRoom(out Transform cafeTransform)
    {
        cafeTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Cafe" 태그를 가진 오브젝트 찾기
        GameObject[] allCafes = GameObject.FindGameObjectsWithTag("Cafe");
        
        Transform sameFloorCafe = null;
        Transform otherFloorCafe = null;
        
        foreach (var cafe in allCafes)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(cafe.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(cafe.transform.position.y))
                {
                    sameFloorCafe = cafe.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorCafe == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorCafe = cafe.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedCafe = sameFloorCafe ?? otherFloorCafe;
        
        if (selectedCafe != null)
        {
            cafeTransform = selectedCafe;
            Debug.Log($"[Cafe 찾기] {gameObject.name}: 방 안에서 카페 찾음 (층: {GetFloorLevel(selectedCafe.position.y)})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 카페로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToCafeBehavior()
    {
        if (currentCafeTransform == null || currentCafeTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Cafe 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 cafeFacilityPos = currentCafeTransform.position;
        Debug.Log($"[Cafe 1단계] {gameObject.name}: Cafe 시설 근처로 이동 시작 - 목표: {cafeFacilityPos}");
        
        bool pathSet = agent.SetDestination(cafeFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Cafe 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentCafeTransform == null || currentCafeTransform.gameObject == null)
            {
                Debug.LogWarning($"[Cafe 1단계] {gameObject.name}: 이동 중 카페가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(cafeFacilityPos.x, cafeFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - cafeFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Cafe 1단계 완료] {gameObject.name}: Cafe 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Cafe 1단계] {gameObject.name}: Cafe 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: CafePoint 찾기 =====
        Transform cafePoint = FindNearestCafePoint(currentCafeTransform);
        
        if (cafePoint == null)
        {
            Debug.Log($"[Cafe 2단계] {gameObject.name}: 사용 가능한 CafePoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Cafe 2단계] {gameObject.name}: CafePoint 발견 - {cafePoint.name}, 위치: {cafePoint.position}");
        
        // ===== 3단계: CafePoint로 이동 =====
        Vector3 targetPos = cafePoint.position;
        Debug.Log($"[Cafe 3단계] {gameObject.name}: CafePoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Cafe 3단계] {gameObject.name}: CafePoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentCafeTransform == null || currentCafeTransform.gameObject == null)
            {
                Debug.LogWarning($"[Cafe 3단계] {gameObject.name}: 이동 중 카페가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Cafe 3단계] {gameObject.name}: CafePoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Cafe 3단계 완료] {gameObject.name}: CafePoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingCafe(cafePoint);
    }
    
    /// <summary>
    /// 카페 주변에서 가장 가까운 사용 가능한 CafePoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestCafePoint(Transform cafe)
    {
        GameObject[] allCafePoints = GameObject.FindGameObjectsWithTag("CafePoint");
        
        if (allCafePoints == null || allCafePoints.Length == 0)
        {
            return null;
        }
        
        lock (cafePointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allCafePoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedCafePoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedCafePoints[point.transform] == null)
                    {
                        occupiedCafePoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(cafe.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 CafePoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedCafePoints[nearestPoint] = this;
                currentCafePoint = nearestPoint;
                Debug.Log($"[CafePoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedCafePoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// 카페 사용을 시작합니다.
    /// </summary>
    /// <param name="cafePoint">CafePoint Transform</param>
    private void StartUsingCafe(Transform cafePoint)
    {
        // cafePoint가 null이면 자동으로 찾기
        if (cafePoint == null && currentCafeTransform != null)
        {
            cafePoint = FindNearestCafePoint(currentCafeTransform);
        }
        
        if (cafePoint == null)
        {
            Debug.LogWarning($"[카페 사용 실패] {gameObject.name}: CafePoint를 찾을 수 없습니다.");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preCafePosition = transform.position;
        preCafeRotation = transform.rotation;

        currentCafePoint = cafePoint;
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: CafePoint로 정확히 순간이동
        transform.position = cafePoint.position;
        transform.rotation = cafePoint.rotation;
        
        Debug.Log($"[Cafe 순간이동] {gameObject.name}: 정확한 CafePoint 위치로 이동 완료 - {cafePoint.position}");

        // 5단계: 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[카페 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
        }

        // 상태 플래그 설정
        isUsingCafe = true;
        TransitionToState(AIState.UsingCafe);

        int aiFloor = GetFloorLevel(transform.position.y);
        int cafeFloor = GetFloorLevel(currentCafeTransform.position.y);
        Debug.Log($"[카페 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, 카페 층: {cafeFloor}");
    }

    /// <summary>
    /// 카페 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingCafe()
    {
        if (!isUsingCafe)
        {
            return;
        }

        Debug.Log($"[카페 사용 종료] {gameObject.name}: 다음 정각이 되어 종료");
        
        // CafePoint 점유 해제
        if (currentCafePoint != null)
        {
            lock (cafePointLock)
            {
                if (occupiedCafePoints.ContainsKey(currentCafePoint) && occupiedCafePoints[currentCafePoint] == this)
                {
                    occupiedCafePoints.Remove(currentCafePoint);
                    Debug.Log($"[CafePoint 점유 해제] {gameObject.name}: {currentCafePoint.name} 점유 해제 (점유 중: {occupiedCafePoints.Count}개)");
                }
            }
            currentCafePoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Sitting", false);
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preCafePosition;
        transform.rotation = preCafeRotation;
        Debug.Log($"[Cafe] {gameObject.name}: 저장된 위치로 복귀 완료 - {preCafePosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Cafe] {gameObject.name}: NavMeshAgent 활성화");
        }

        // 카페 결제 처리
        ProcessCafeFacilityPayment();

        // 상태 초기화
        isUsingCafe = false;
        currentCafeTransform = null;
        cafeCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 카페 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessCafeFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessCafeFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[카페 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region Bath 관련 메서드
    /// <summary>
    /// 일반 손님용: 맵에서 Bath 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool TryFindAvailableBath()
    {
        // 현재 11~22시인지 확인
        if (timeSystem == null || timeSystem.CurrentHour < 11 || timeSystem.CurrentHour > 22)
        {
            Debug.Log($"[Bath] {gameObject.name}: Bath 운영 시간이 아닙니다.");
            return false;
        }
        
        GameObject[] allBaths = GameObject.FindGameObjectsWithTag("Bath");
        
        if (allBaths.Length == 0)
        {
            Debug.Log($"[Bath] {gameObject.name}: Bath가 없습니다.");
            return false;
        }

        // 같은 층에서 가장 가까운 Bath 우선 찾기
        Transform nearestSameFloorBath = null;
        float minSameFloorDistance = float.MaxValue;

        // 다른 층에서 가장 가까운 Bath (백업)
        Transform nearestOtherFloorBath = null;
        float minOtherFloorDistance = float.MaxValue;

        int noPointsCount = 0;
        int candidateCount = 0;

        foreach (var bath in allBaths)
        {
            if (bath != null)
            {
                    bool hasAvailablePoint = HasAvailableBathPoint(bath.transform);
                    if (!hasAvailablePoint)
                    {
                        noPointsCount++;
                        continue;
                    }
                    
                candidateCount++;
                float distance = Vector3.Distance(transform.position, bath.transform.position);
                
                if (IsSameFloor(bath.transform.position.y))
                {
                    if (distance < minSameFloorDistance)
                    {
                        minSameFloorDistance = distance;
                        nearestSameFloorBath = bath.transform;
                    }
                }
                else
                {
                    if (distance < minOtherFloorDistance)
                    {
                        minOtherFloorDistance = distance;
                        nearestOtherFloorBath = bath.transform;
                    }
                }
            }
        }

        Transform selectedBath = nearestSameFloorBath ?? nearestOtherFloorBath;

        if (selectedBath != null)
        {
            currentBathTransform = selectedBath;
            preBathPosition = transform.position;
            preBathRotation = transform.rotation;
            
            TransitionToState(AIState.MovingToBath);
            return true;
        }

        Debug.LogWarning($"[Bath 찾기 실패] {gameObject.name}: 총 {allBaths.Length}개 Bath / 포인트없음: {noPointsCount}개 / 후보: {candidateCount}개");
        return false;
    }
    
    /// <summary>
    /// Bath에 사용 가능한 포인트가 있는지 확인
    /// </summary>
    private bool HasAvailableBathPoint(Transform bath)
    {
        int totalSitPoints = 0;
        int occupiedSitPoints = 0;
        int nearbySitPoints = 0;
        int totalDownPoints = 0;
        int occupiedDownPoints = 0;
        int nearbyDownPoints = 0;
        
        // BathSitPoint 확인
        GameObject[] sitPoints = GameObject.FindGameObjectsWithTag("BathSitPoint");
        totalSitPoints = sitPoints.Length;
        
        foreach (var point in sitPoints)
        {
            if (point != null)
            {
                float distance = Vector3.Distance(bath.position, point.transform.position);
                if (distance < 50f)
                {
                    nearbySitPoints++;
                    lock (bathSitPointLock)
                    {
                        if (occupiedBathSitPoints.ContainsKey(point.transform))
                        {
                            occupiedSitPoints++;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
        }
        
        // BathDownPoint 확인
        GameObject[] downPoints = GameObject.FindGameObjectsWithTag("BathDownPoint");
        totalDownPoints = downPoints.Length;
        
        foreach (var point in downPoints)
        {
            if (point != null)
            {
                float distance = Vector3.Distance(bath.position, point.transform.position);
                if (distance < 50f)
                {
                    nearbyDownPoints++;
                    lock (bathDownPointLock)
                    {
                        if (occupiedBathDownPoints.ContainsKey(point.transform))
                        {
                            occupiedDownPoints++;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 방 안에 Bath 태그를 가진 오브젝트 찾기
    /// </summary>
    private bool FindBathInCurrentRoom(out Transform bathTransform)
    {
        bathTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 안의 모든 "Bath" 태그를 가진 오브젝트 찾기
        GameObject[] allBaths = GameObject.FindGameObjectsWithTag("Bath");
        
        Transform sameFloorBath = null;
        Transform otherFloorBath = null;
        
        foreach (var bath in allBaths)
        {
            // 방의 bounds 안에 있는지 확인
            if (room.bounds.Contains(bath.transform.position))
            {
                // 같은 층이면 우선순위
                if (IsSameFloor(bath.transform.position.y))
                {
                    sameFloorBath = bath.transform;
                    break; // 같은 층 찾으면 바로 사용
                }
                else if (otherFloorBath == null)
                {
                    // 다른 층은 백업으로 저장
                    otherFloorBath = bath.transform;
                }
            }
        }

        // 같은 층 우선, 없으면 다른 층 사용
        Transform selectedBath = sameFloorBath ?? otherFloorBath;
        
        if (selectedBath != null)
        {
            bathTransform = selectedBath;
            Debug.Log($"[Bath 찾기] {gameObject.name}: 방 안에서 Bath 찾음 (층: {GetFloorLevel(selectedBath.position.y)})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Bath로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToBathBehavior()
    {
        if (currentBathTransform == null || currentBathTransform.gameObject == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // ===== 1단계: Bath 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 bathFacilityPos = currentBathTransform.position;
        Debug.Log($"[Bath 1단계] {gameObject.name}: Bath 시설 근처로 이동 시작 - 목표: {bathFacilityPos}");
        
        bool pathSet = agent.SetDestination(bathFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Bath 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentBathTransform == null || currentBathTransform.gameObject == null)
            {
                Debug.LogWarning($"[Bath 1단계] {gameObject.name}: 이동 중 Bath가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(bathFacilityPos.x, bathFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - bathFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Bath 1단계 완료] {gameObject.name}: Bath 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Bath 1단계] {gameObject.name}: Bath 시설 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: BathPoint 찾기 (랜덤 선택) =====
        bool useSitPoint = Random.value < 0.5f;
        Transform bathPoint = useSitPoint ? FindNearestBathSitPoint(currentBathTransform) : FindNearestBathDownPoint(currentBathTransform);
        isBathSitting = useSitPoint;
        
        if (bathPoint == null)
        {
            Debug.Log($"[Bath 2단계] {gameObject.name}: 사용 가능한 BathPoint 없음 - 배회로 전환");
            
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
            yield break;
        }
        
        Debug.Log($"[Bath 2단계] {gameObject.name}: BathPoint 발견 - {bathPoint.name}, 타입: {(isBathSitting ? "Sitting" : "Down")}");
        
        // ===== 3단계: BathPoint로 이동 =====
        Vector3 targetPos = bathPoint.position;
        Debug.Log($"[Bath 3단계] {gameObject.name}: BathPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Bath 3단계] {gameObject.name}: BathPoint 경로 설정 실패!");
            DetermineBehaviorByTime();
            yield break;
        }
        
        float timeout2 = 30f;
        float timer2 = 0f;

        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentBathTransform == null || currentBathTransform.gameObject == null)
            {
                Debug.LogWarning($"[Bath 3단계] {gameObject.name}: 이동 중 Bath가 삭제됨");
                DetermineBehaviorByTime();
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Bath 3단계] {gameObject.name}: BathPoint 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[Bath 3단계 완료] {gameObject.name}: BathPoint 바로 앞 도착! 현재 위치: {transform.position}");

        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingBath(bathPoint);
    }
    
    /// <summary>
    /// Bath 주변에서 가장 가까운 사용 가능한 BathSitPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestBathSitPoint(Transform bath)
    {
        GameObject[] allBathSitPoints = GameObject.FindGameObjectsWithTag("BathSitPoint");
        
        if (allBathSitPoints == null || allBathSitPoints.Length == 0)
        {
            return null;
        }
        
        lock (bathSitPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allBathSitPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedBathSitPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedBathSitPoints[point.transform] == null)
                    {
                        occupiedBathSitPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(bath.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 BathSitPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedBathSitPoints[nearestPoint] = this;
                currentBathPoint = nearestPoint;
                Debug.Log($"[BathSitPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedBathSitPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }
    
    /// <summary>
    /// Bath 주변에서 가장 가까운 사용 가능한 BathDownPoint 찾기 (점유 관리)
    /// </summary>
    private Transform FindNearestBathDownPoint(Transform bath)
    {
        GameObject[] allBathDownPoints = GameObject.FindGameObjectsWithTag("BathDownPoint");
        
        if (allBathDownPoints == null || allBathDownPoints.Length == 0)
        {
            return null;
        }
        
        lock (bathDownPointLock)
        {
            Transform nearestPoint = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var point in allBathDownPoints)
            {
                if (point == null) continue;
                
                // 이미 다른 AI가 점유 중인지 확인
                if (occupiedBathDownPoints.ContainsKey(point.transform))
                {
                    // 점유한 AI가 null이거나 삭제되었으면 점유 해제
                    if (occupiedBathDownPoints[point.transform] == null)
                    {
                        occupiedBathDownPoints.Remove(point.transform);
                    }
                    else
                    {
                        // 다른 AI가 사용 중이면 스킵
                        continue;
                    }
                }
                
                float distance = Vector3.Distance(bath.position, point.transform.position);
                
                // 거리 제한 없이 가장 가까운 BathDownPoint 찾기
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point.transform;
                }
            }
            
            // 찾은 Point를 점유 등록
            if (nearestPoint != null)
            {
                occupiedBathDownPoints[nearestPoint] = this;
                currentBathPoint = nearestPoint;
                Debug.Log($"[BathDownPoint 점유] {gameObject.name}: {nearestPoint.name} 점유 (점유 중: {occupiedBathDownPoints.Count}개)");
            }
            
            return nearestPoint;
        }
    }

    /// <summary>
    /// Bath 사용을 시작합니다.
    /// </summary>
    /// <param name="bathPoint">BathSitPoint 또는 BathDownPoint Transform</param>
    private void StartUsingBath(Transform bathPoint)
    {
        if (bathPoint == null)
        {
            Debug.LogWarning($"[Bath 사용 실패] {gameObject.name}: BathPoint를 찾을 수 없습니다.");
            DetermineBehaviorByTime();
            return;
        }

        // 1단계: 현재 위치 저장
        preBathPosition = transform.position;
        preBathRotation = transform.rotation;

        currentBathPoint = bathPoint;
        
        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: BathPoint로 정확히 순간이동
        transform.position = bathPoint.position;
        transform.rotation = bathPoint.rotation;
        
        Debug.Log($"[Bath 순간이동] {gameObject.name}: 정확한 BathPoint 위치로 이동 완료 - {bathPoint.position}, 타입: {(isBathSitting ? "Sitting" : "Down")}");

        // 5단계: 애니메이션 시작 (Sitting 또는 BedTime)
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            if (isBathSitting)
            {
                animator.SetBool("Sitting", true);
                Debug.Log($"[Bath 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
            }
            else
            {
                animator.SetBool("BedTime", true);
                Debug.Log($"[Bath 애니메이션] {gameObject.name}: Moving=false, BedTime=true");
            }
        }

        // 상태 플래그 설정
        isUsingBath = true;
        TransitionToState(AIState.UsingBath);

        int aiFloor = GetFloorLevel(transform.position.y);
        int bathFloor = GetFloorLevel(currentBathTransform.position.y);
        Debug.Log($"[Bath 사용 시작] {gameObject.name}: 위치({transform.position}), AI 층: {aiFloor}, Bath 층: {bathFloor}");
    }

    /// <summary>
    /// Bath 사용을 종료합니다. (다음 정각에 호출)
    /// </summary>
    private void FinishUsingBath()
    {
        if (!isUsingBath)
        {
            return;
        }

        Debug.Log($"[Bath 사용 종료] {gameObject.name}: 다음 정각이 되어 종료 (타입: {(isBathSitting ? "Sitting" : "Down")})");
        
        // BathPoint 점유 해제
        if (currentBathPoint != null)
        {
            if (isBathSitting)
            {
                lock (bathSitPointLock)
                {
                    if (occupiedBathSitPoints.ContainsKey(currentBathPoint) && occupiedBathSitPoints[currentBathPoint] == this)
                    {
                        occupiedBathSitPoints.Remove(currentBathPoint);
                        Debug.Log($"[BathSitPoint 점유 해제] {gameObject.name}: {currentBathPoint.name} 점유 해제 (점유 중: {occupiedBathSitPoints.Count}개)");
                    }
                }
            }
            else
            {
                lock (bathDownPointLock)
                {
                    if (occupiedBathDownPoints.ContainsKey(currentBathPoint) && occupiedBathDownPoints[currentBathPoint] == this)
                    {
                        occupiedBathDownPoints.Remove(currentBathPoint);
                        Debug.Log($"[BathDownPoint 점유 해제] {gameObject.name}: {currentBathPoint.name} 점유 해제 (점유 중: {occupiedBathDownPoints.Count}개)");
                    }
                }
            }
            currentBathPoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            if (isBathSitting)
            {
                animator.SetBool("Sitting", false);
            }
            else
            {
                animator.SetBool("BedTime", false);
            }
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preBathPosition;
        transform.rotation = preBathRotation;
        Debug.Log($"[Bath] {gameObject.name}: 저장된 위치로 복귀 완료 - {preBathPosition}");
        
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[Bath] {gameObject.name}: NavMeshAgent 활성화");
        }

        // Bath 결제 처리
        ProcessBathFacilityPayment();

        // 상태 초기화
        isUsingBath = false;
        isBathSitting = false;
        currentBathTransform = null;
        bathCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// Bath 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessBathFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessBathFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[Bath 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region Hos(고급식당) 관련 메서드
    /// <summary>
    /// 사용 가능한 Hos를 찾습니다 (방 밖)
    /// </summary>
    private bool TryFindAvailableHos()
    {
        // 현재 11~22시인지 확인
        if (timeSystem == null || timeSystem.CurrentHour < 11 || timeSystem.CurrentHour > 22)
        {
            Debug.Log($"[Hos] {gameObject.name}: Hos 운영 시간이 아닙니다.");
            return false;
        }

        GameObject[] hosObjects = GameObject.FindGameObjectsWithTag("Hos");
        if (hosObjects.Length == 0)
        {
            Debug.Log($"[Hos] {gameObject.name}: Hos가 없습니다.");
            return false;
        }

        // 가장 가까운 사용 가능한 Hos 찾기
        Transform nearestHos = null;
        float nearestDistance = float.MaxValue;

        foreach (GameObject hosObj in hosObjects)
        {
            Transform hosTransform = hosObj.transform;

            // 이미 점유된 Hos는 건너뛰기
            lock (hosLock)
            {
                if (occupiedHosFacilities.ContainsKey(hosTransform))
                {
                    continue;
                }
            }

            // 사용 가능한 포인트가 있는지 확인
            Transform[] hosPoints = hosTransform.GetComponentsInChildren<Transform>();
            bool hasAvailablePoint = false;

            foreach (Transform point in hosPoints)
            {
                if (point.CompareTag("HosPoint"))
                {
                    lock (hosPointLock)
                    {
                        if (!occupiedHosPoints.ContainsKey(point))
                        {
                            hasAvailablePoint = true;
                            break;
                        }
                    }
                }
            }

            if (!hasAvailablePoint)
            {
                continue;
            }

            // 거리 계산
            float distance = Vector3.Distance(transform.position, hosTransform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestHos = hosTransform;
            }
        }

        if (nearestHos != null)
        {
            currentHosTransform = nearestHos;
            preHosPosition = transform.position;
            preHosRotation = transform.rotation;
            TransitionToState(AIState.MovingToHos);
            return true;
        }

        Debug.Log($"[Hos] {gameObject.name}: 사용 가능한 Hos가 없습니다.");
        return false;
    }

    /// <summary>
    /// 현재 방에서 Hos를 찾습니다
    /// </summary>
    private bool FindHosInCurrentRoom(out Transform hosTransform)
    {
        hosTransform = null;

        // 현재 11~22시인지 확인
        if (timeSystem == null || timeSystem.CurrentHour < 11 || timeSystem.CurrentHour > 22)
        {
            Debug.Log($"[Hos] {gameObject.name}: Hos 운영 시간이 아닙니다.");
            return false;
        }

        if (currentRoomIndex == -1 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        RoomInfo currentRoom = roomList[currentRoomIndex];
        Bounds roomBounds = currentRoom.bounds;

        GameObject[] hosObjects = GameObject.FindGameObjectsWithTag("Hos");
        foreach (GameObject hosObj in hosObjects)
        {
            if (roomBounds.Contains(hosObj.transform.position))
            {
                // 이미 점유된 Hos는 건너뛰기
                lock (hosLock)
                {
                    if (occupiedHosFacilities.ContainsKey(hosObj.transform))
                    {
                        continue;
                    }
                }

                // 사용 가능한 포인트가 있는지 확인
                Transform[] hosPoints = hosObj.transform.GetComponentsInChildren<Transform>();
                bool hasAvailablePoint = false;

                foreach (Transform point in hosPoints)
                {
                    if (point.CompareTag("HosPoint"))
                    {
                        lock (hosPointLock)
                        {
                            if (!occupiedHosPoints.ContainsKey(point))
                            {
                                hasAvailablePoint = true;
                                break;
                            }
                        }
                    }
                }

                if (hasAvailablePoint)
                {
                    hosTransform = hosObj.transform;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Hos로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToHosBehavior()
    {
        if (currentHosTransform == null)
        {
            Debug.LogError($"[Hos] {gameObject.name}: currentHosTransform이 null입니다!");
            TransitionToState(AIState.Wandering);
            yield break;
        }

        // Hos 점유
        lock (hosLock)
        {
            if (occupiedHosFacilities.ContainsKey(currentHosTransform))
            {
                Debug.Log($"[Hos] {gameObject.name}: Hos가 이미 점유되었습니다.");
                currentHosTransform = null;
                TransitionToState(AIState.Wandering);
                yield break;
            }
            occupiedHosFacilities[currentHosTransform] = this;
        }

        // ===== 1단계: Hos 시설 근처 NavMesh 위치로 먼저 이동 =====
        Vector3 hosFacilityPos = currentHosTransform.position;
        Debug.Log($"[Hos 1단계] {gameObject.name}: Hos 시설 근처로 이동 시작 - 목표: {hosFacilityPos}");
        
        bool pathSet = agent.SetDestination(hosFacilityPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Hos 1단계] {gameObject.name}: 경로 설정 실패! NavMesh 위 여부: {agent.isOnNavMesh}");
            
            // 점유 해제
            lock (hosLock)
            {
                if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                {
                    occupiedHosFacilities.Remove(currentHosTransform);
                }
            }
            
            TransitionToState(AIState.Wandering);
            yield break;
        }
        
        float timeout1 = 30f;
        float timer1 = 0f;
        float facilityArrivalRange = 5f;
        float yTolerance = 1f;
        
        while (true)
        {
            if (currentHosTransform == null)
            {
                Debug.LogWarning($"[Hos 1단계] {gameObject.name}: 이동 중 Hos가 삭제됨");
                
                lock (hosLock)
                {
                    if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                    {
                        occupiedHosFacilities.Remove(currentHosTransform);
                    }
                }
                
                TransitionToState(AIState.Wandering);
                yield break;
            }
            
            if (!agent.pathPending)
            {
                float xzDistance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(hosFacilityPos.x, hosFacilityPos.z)
                );
                float yDistance = Mathf.Abs(transform.position.y - hosFacilityPos.y);
                
                if (xzDistance <= facilityArrivalRange && yDistance <= yTolerance)
                {
                    Debug.Log($"[Hos 1단계 완료] {gameObject.name}: Hos 시설 근처 도착! XZ거리: {xzDistance:F2}m, Y차이: {yDistance:F2}m");
                    break;
                }
            }
            
            if (timer1 >= timeout1)
            {
                Debug.LogError($"[Hos 1단계] {gameObject.name}: Hos 시설 이동 타임아웃");
                
                lock (hosLock)
                {
                    if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                    {
                        occupiedHosFacilities.Remove(currentHosTransform);
                    }
                }
                
                TransitionToState(AIState.Wandering);
                yield break;
            }
            
            timer1 += Time.deltaTime;
            yield return null;
        }
        
        // ===== 2단계: HosPoint 찾기 =====
        Transform hosPoint = FindNearestHosPoint(currentHosTransform);
        
        if (hosPoint == null)
        {
            Debug.LogError($"[Hos 2단계] {gameObject.name}: HosPoint를 찾을 수 없습니다!");
            
            lock (hosLock)
            {
                if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                {
                    occupiedHosFacilities.Remove(currentHosTransform);
                }
            }
            
            currentHosTransform = null;
            TransitionToState(AIState.Wandering);
            yield break;
        }

        currentHosPoint = hosPoint;
        Debug.Log($"[Hos 2단계] {gameObject.name}: HosPoint 발견 - {hosPoint.name}, 위치: {hosPoint.position}");

        // ===== 3단계: HosPoint로 이동 =====
        Vector3 targetPos = hosPoint.position;
        Debug.Log($"[Hos 3단계] {gameObject.name}: HosPoint로 이동 시작 - 목표: {targetPos}");
        
        pathSet = agent.SetDestination(targetPos);
        
        if (!pathSet)
        {
            Debug.LogError($"[Hos 3단계] {gameObject.name}: HosPoint 경로 설정 실패!");
            TransitionToState(AIState.Wandering);
            yield break;
        }

        float timeout2 = 30f;
        float timer2 = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (currentHosTransform == null)
            {
                Debug.LogWarning($"[Hos 3단계] {gameObject.name}: 이동 중 Hos가 삭제됨");
                TransitionToState(AIState.Wandering);
                yield break;
            }
            
            if (timer2 >= timeout2)
            {
                Debug.LogError($"[Hos 3단계] {gameObject.name}: HosPoint 이동 타임아웃");
                TransitionToState(AIState.Wandering);
                yield break;
            }
            
            timer2 += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[Hos 3단계 완료] {gameObject.name}: HosPoint 바로 앞 도착! 현재 위치: {transform.position}");
        
        // ===== 4단계: 순간이동 및 사용 시작 =====
        StartUsingHos(hosPoint);
    }
    
    /// <summary>
    /// 가장 가까운 HosPoint를 찾습니다
    /// </summary>
    private Transform FindNearestHosPoint(Transform hos)
    {
        Transform nearestPoint = null;
        float nearestDistance = float.MaxValue;

        Transform[] points = hos.GetComponentsInChildren<Transform>();

        foreach (Transform point in points)
        {
            if (point.CompareTag("HosPoint"))
            {
                // 이미 점유된 포인트는 건너뛰기
                lock (hosPointLock)
                {
                    if (occupiedHosPoints.ContainsKey(point))
                    {
                        continue;
                    }
                }

                float distance = Vector3.Distance(transform.position, point.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPoint = point;
                }
            }
        }

        if (nearestPoint != null)
        {
            // 포인트 점유
            lock (hosPointLock)
            {
                occupiedHosPoints[nearestPoint] = this;
            }
        }

        return nearestPoint;
    }

    /// <summary>
    /// Hos 사용 시작
    /// </summary>
    private void StartUsingHos(Transform hosPoint)
    {
        if (hosPoint == null)
        {
            Debug.LogError($"[Hos] {gameObject.name}: hosPoint가 null입니다!");
            return;
        }

        // 1단계: 현재 위치 저장
        preHosPosition = transform.position;
        preHosRotation = transform.rotation;

        // 2단계: NavMeshAgent 비활성화 (순간이동 전에 먼저!)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false; // 먼저 비활성화해야 NavMesh 간섭 없음
        }

        // 3단계: HosPoint로 정확히 순간이동
        transform.position = hosPoint.position;
        transform.rotation = hosPoint.rotation;

        Debug.Log($"[Hos 순간이동] {gameObject.name}: 정확한 HosPoint 위치로 이동 완료 - {hosPoint.position}");

        // ChairPoint 컴포넌트 가져오기
        ChairPoint chairPoint = hosPoint.GetComponent<ChairPoint>();
        if (chairPoint != null)
        {
            currentHosChairPoint = chairPoint;
            chairPoint.OccupyChair(this);
            Debug.Log($"[Hos 사용 시작] {gameObject.name}: ChairPoint 점유 완료 (테이블 활성화)");
        }
        else
        {
            Debug.LogWarning($"[Hos 사용 시작] {gameObject.name}: ChairPoint 컴포넌트를 찾을 수 없습니다!");
        }

        // 5단계: 애니메이션 시작
        // Sitting 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Moving", false); // ✅ Moving 애니메이션 끄기
            animator.SetBool("Sitting", true);
            Debug.Log($"[Hos 애니메이션] {gameObject.name}: Moving=false, Sitting=true");
        }

        // Fork 또는 Spoon 랜덤 활성화
        if (forkObject != null && spoonObject != null)
        {
            bool useFork = Random.value > 0.5f;
            if (useFork)
            {
                forkObject.SetActive(true);
                currentHosUtensil = forkObject;
                Debug.Log($"[Hos 도구] {gameObject.name}: Fork 활성화");
            }
            else
            {
                spoonObject.SetActive(true);
                currentHosUtensil = spoonObject;
                Debug.Log($"[Hos 도구] {gameObject.name}: Spoon 활성화");
            }
        }
        else
        {
            Debug.LogWarning($"[Hos 도구] {gameObject.name}: Fork 또는 Spoon 오브젝트가 null입니다!");
        }

        // Eating 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("Eating", true);
            Debug.Log($"[Hos 애니메이션] {gameObject.name}: Eating 애니메이션 시작");
        }

        isUsingHos = true;
        TransitionToState(AIState.UsingHos);

        Debug.Log($"[Hos 사용 시작] {gameObject.name}: 위치({hosPoint.position}), AI 층: {GetFloorLevel(transform.position.y)}, Hos 층: {GetFloorLevel(currentHosTransform.position.y)}");
    }

    /// <summary>
    /// Hos 사용 종료
    /// </summary>
    private void FinishUsingHos()
    {
        if (!isUsingHos)
            return;

        Debug.Log($"[Hos 사용 종료] {gameObject.name}: Hos 사용 종료 시작");

        // 식사 도구 비활성화
        if (currentHosUtensil != null)
        {
            currentHosUtensil.SetActive(false);
            Debug.Log($"[Hos 사용 종료] {gameObject.name}: 식사 도구 비활성화");
            currentHosUtensil = null;
        }

        // ChairPoint 해제 (테이블 비활성화)
        if (currentHosChairPoint != null)
        {
            currentHosChairPoint.ReleaseChair(this);
            Debug.Log($"[Hos 사용 종료] {gameObject.name}: ChairPoint 해제 완료 - 테이블 비활성화");
            currentHosChairPoint = null;
        }

        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Eating", false);
            animator.SetBool("Sitting", false);
            Debug.Log($"[Hos 사용 종료] {gameObject.name}: 애니메이션 종료");
        }

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
        }

        // Point 점유 해제
        if (currentHosPoint != null)
        {
            lock (hosPointLock)
            {
                if (occupiedHosPoints.ContainsKey(currentHosPoint))
                {
                    occupiedHosPoints.Remove(currentHosPoint);
                    Debug.Log($"[Hos 점유 해제] {gameObject.name}: HosPoint 점유 해제");
                }
            }
            currentHosPoint = null;
        }

        // 시설 점유 해제
        if (currentHosTransform != null)
        {
            lock (hosLock)
            {
                if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                {
                    occupiedHosFacilities.Remove(currentHosTransform);
                    Debug.Log($"[Hos 점유 해제] {gameObject.name}: Hos 시설 점유 해제");
                }
            }
        }

        // 이전 위치로 복귀 (이미 NavMesh 위의 위치)
            transform.position = preHosPosition;
        transform.rotation = preHosRotation;
        Debug.Log($"[Hos] {gameObject.name}: 저장된 위치로 복귀 완료 - {preHosPosition}");

        // Hos 결제 처리
        ProcessHosFacilityPayment();

        // 상태 초기화
        isUsingHos = false;
        currentHosTransform = null;
        hosCoroutine = null;

        // 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// Hos 결제 처리 (RoomManager를 통해 - FacilityPriceConfig 사용)
    /// </summary>
    private void ProcessHosFacilityPayment()
    {
        // RoomManager를 통한 결제 처리
        if (roomManager != null)
        {
            roomManager.ProcessHosFacilityPayment(gameObject.name);
        }
        else
        {
            Debug.LogError($"[Hos 결제 실패] {gameObject.name}: RoomManager가 null입니다.");
        }
    }
    #endregion

    #region 운동 시설 (헬스장) 추가 메서드
    /// <summary>
    /// 운동 시설이 삭제되었을 때 강제 종료합니다.
    /// </summary>
    private void HandleHealthDestroyed()
    {
        Debug.Log($"[운동 시설 삭제 감지] {gameObject.name}: 운동 시설 사용 강제 종료");
        
        // 운동 시설 코루틴 정리
        if (healthCoroutine != null)
        {
            StopCoroutine(healthCoroutine);
            healthCoroutine = null;
        }
        
        // 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("Exercise", false);
        }
        
        // 이전 위치로 복귀 (가능한 경우)
        if (preHealthPosition != Vector3.zero)
        {
            if (NavMesh.SamplePosition(preHealthPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }
        
        // 운동 시설 상태 해제
        isUsingHealth = false;
        currentHealthTransform = null;
        
        // 방 내부 배회로 전환 (방이 있는 AI만 운동 시설 사용 가능)
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.RoomWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }
    #endregion

    #region 정리
    void OnDisable()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    void OnDestroy()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    /// <summary>
    /// 정각마다 호출되어 현재 진행 중인 활동을 정리합니다 (애니메이션, 오브젝트, 점유 해제).
    /// 상태 전환은 하지 않고 정리만 수행합니다.
    /// </summary>
    private void CleanupCurrentActivity()
    {
        // 1. 식사 중이면 정리 (상태 전환 없이)
        if (isEating)
        {
            // 🍝 Pasta 오브젝트 비활성화 (중요!)
            if (pastaObject != null && pastaObject.activeSelf)
            {
                pastaObject.SetActive(false);
                Debug.Log($"[활동 정리] {gameObject.name}: Pasta 오브젝트 비활성화");
            }

            // 식사 도구 비활성화
            if (currentEatingUtensil != null)
            {
                currentEatingUtensil.SetActive(false);
                currentEatingUtensil = null;
            }

            // ChairPoint 해제 (테이블 비활성화)
            if (currentChairPoint != null)
            {
                currentChairPoint.ReleaseChair(this);
                currentChairPoint = null;
            }

            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("PickUp", false);  // PickUp 애니메이션도 끄기
                animator.SetBool("Eating", false);
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // 플래그 및 변수 정리
            isEating = false;
            currentKitchenTransform = null;
            currentChairTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 식사 활동 정리 완료 (Pasta 포함)");
        }

        // 2. 주방 카운터 대기 중이면 정리
        if (isWaitingAtKitchenCounter)
        {
            if (currentKitchenCounter != null)
            {
                currentKitchenCounter.LeaveQueue(this);
            }

            CleanupKitchenVariables();
            
            if (agent != null)
            {
                agent.isStopped = false;
            }
            
            Debug.Log($"[활동 정리] {gameObject.name}: 주방 카운터 대기 정리 완료");
        }

        // 3. 헬스장 사용 중이면 정리
        if (isUsingHealth)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Health", false);
            }

            // 덤벨 비활성화
            if (leftDumbbell != null) leftDumbbell.SetActive(false);
            if (rightDumbbell != null) rightDumbbell.SetActive(false);

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentHealthPoint != null)
            {
                lock (healthPointLock)
                {
                    if (occupiedHealthPoints.ContainsKey(currentHealthPoint))
                    {
                        occupiedHealthPoints.Remove(currentHealthPoint);
                    }
                }
                currentHealthPoint = null;
            }

            // 시설 점유 해제
            if (currentHealthTransform != null)
            {
                lock (healthLock)
                {
                    if (occupiedHealthFacilities.ContainsKey(currentHealthTransform))
                    {
                        occupiedHealthFacilities.Remove(currentHealthTransform);
                    }
                }
            }

            // 결제 처리
            ProcessHealthFacilityPayment();

            // 플래그 정리
            isUsingHealth = false;
            currentHealthTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 헬스장 활동 정리 완료");
        }

        // 4. 예식장 사용 중이면 정리
        if (isUsingWedding)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentWeddingPoint != null)
            {
                lock (weddingPointLock)
                {
                    if (occupiedWeddingPoints.ContainsKey(currentWeddingPoint))
                    {
                        occupiedWeddingPoints.Remove(currentWeddingPoint);
                    }
                }
                currentWeddingPoint = null;
            }

            // 시설 점유 해제
            if (currentWeddingTransform != null)
            {
                lock (weddingLock)
                {
                    if (occupiedWeddingFacilities.ContainsKey(currentWeddingTransform))
                    {
                        occupiedWeddingFacilities.Remove(currentWeddingTransform);
                    }
                }
            }

            // 결제 처리
            ProcessWeddingFacilityPayment();

            // 플래그 정리
            isUsingWedding = false;
            currentWeddingTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 예식장 활동 정리 완료");
        }

        // 5. 라운지 사용 중이면 정리
        if (isUsingLounge)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentLoungePoint != null)
            {
                lock (loungePointLock)
                {
                    if (occupiedLoungePoints.ContainsKey(currentLoungePoint))
                    {
                        occupiedLoungePoints.Remove(currentLoungePoint);
                    }
                }
                currentLoungePoint = null;
            }

            // 시설 점유 해제
            if (currentLoungeTransform != null)
            {
                lock (loungeLock)
                {
                    if (occupiedLoungeFacilities.ContainsKey(currentLoungeTransform))
                    {
                        occupiedLoungeFacilities.Remove(currentLoungeTransform);
                    }
                }
            }

            // 결제 처리
            ProcessLoungeFacilityPayment();

            // 플래그 정리
            isUsingLounge = false;
            currentLoungeTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 라운지 활동 정리 완료");
        }

        // 6. 연회장 사용 중이면 정리
        if (isUsingHall)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentHallPoint != null)
            {
                lock (hallPointLock)
                {
                    if (occupiedHallPoints.ContainsKey(currentHallPoint))
                    {
                        occupiedHallPoints.Remove(currentHallPoint);
                    }
                }
                currentHallPoint = null;
            }

            // 시설 점유 해제
            if (currentHallTransform != null)
            {
                lock (hallLock)
                {
                    if (occupiedHallFacilities.ContainsKey(currentHallTransform))
                    {
                        occupiedHallFacilities.Remove(currentHallTransform);
                    }
                }
            }

            // 결제 처리
            ProcessHallFacilityPayment();

            // 플래그 정리
            isUsingHall = false;
            currentHallTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 연회장 활동 정리 완료");
        }

        // 7. 사우나 사용 중이면 정리
        if (isUsingSauna)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                if (isSaunaSitting)
                {
                    animator.SetBool("Sitting", false);
                }
                else
                {
                    animator.SetBool("BedTime", false);
                }
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentSaunaPoint != null)
            {
                if (isSaunaSitting)
                {
                    lock (saunaSitPointLock)
                    {
                        if (occupiedSaunaSitPoints.ContainsKey(currentSaunaPoint))
                        {
                            occupiedSaunaSitPoints.Remove(currentSaunaPoint);
                        }
                    }
                }
                else
                {
                    lock (saunaDownPointLock)
                    {
                        if (occupiedSaunaDownPoints.ContainsKey(currentSaunaPoint))
                        {
                            occupiedSaunaDownPoints.Remove(currentSaunaPoint);
                        }
                    }
                }
                currentSaunaPoint = null;
            }

            // 결제 처리
            ProcessSaunaFacilityPayment();

            // 플래그 정리
            isUsingSauna = false;
            currentSaunaTransform = null;
            isSaunaSitting = false;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 사우나 활동 정리 완료");
        }

        // 8. 카페 사용 중이면 정리
        if (isUsingCafe)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentCafePoint != null)
            {
                lock (cafePointLock)
                {
                    if (occupiedCafePoints.ContainsKey(currentCafePoint))
                    {
                        occupiedCafePoints.Remove(currentCafePoint);
                    }
                }
                currentCafePoint = null;
            }

            // 시설 점유 해제
            if (currentCafeTransform != null)
            {
                lock (cafeLock)
                {
                    if (occupiedCafeFacilities.ContainsKey(currentCafeTransform))
                    {
                        occupiedCafeFacilities.Remove(currentCafeTransform);
                    }
                }
            }

            // 결제 처리
            ProcessCafeFacilityPayment();

            // 플래그 정리
            isUsingCafe = false;
            currentCafeTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 카페 활동 정리 완료");
        }

        // 9. Bath 사용 중이면 정리
        if (isUsingBath)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                if (isBathSitting)
                {
                    animator.SetBool("Sitting", false);
                }
                else
                {
                    animator.SetBool("BedTime", false);
                }
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentBathPoint != null)
            {
                if (isBathSitting)
                {
                    lock (bathSitPointLock)
                    {
                        if (occupiedBathSitPoints.ContainsKey(currentBathPoint))
                        {
                            occupiedBathSitPoints.Remove(currentBathPoint);
                        }
                    }
                }
                else
                {
                    lock (bathDownPointLock)
                    {
                        if (occupiedBathDownPoints.ContainsKey(currentBathPoint))
                        {
                            occupiedBathDownPoints.Remove(currentBathPoint);
                        }
                    }
                }
                currentBathPoint = null;
            }

            // 결제 처리
            ProcessBathFacilityPayment();

            // 플래그 정리
            isUsingBath = false;
            currentBathTransform = null;
            isBathSitting = false;
            
            Debug.Log($"[활동 정리] {gameObject.name}: Bath 활동 정리 완료");
        }

        // 10. 선베드 사용 중이면 정리
        if (isUsingSunbed)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요

            // SunbedPoint 점유 해제
            if (currentSunbedPoint != null)
            {
                lock (sunbedPointLock)
                {
                    if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint))
                    {
                        occupiedSunbedPoints.Remove(currentSunbedPoint);
                    }
                }
            }

            // ✅ 결제 처리 (방 안/밖 동적 확인)
                ProcessSunbedPaymentDirectly();

            // 플래그 정리
            isUsingSunbed = false;
            currentSunbedTransform = null;
            currentSunbedPoint = null;
            // ✅ isSunbedInRoom 제거
            
            Debug.Log($"[활동 정리] {gameObject.name}: 선베드 활동 정리 완료");
        }

        // 11. 욕조 사용 중이면 정리
        if (isUsingBathtub)
        {
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // 플래그 정리
            isUsingBathtub = false;
            currentBathtubTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: 욕조 활동 정리 완료");
        }

        // 12. Hos(고급식당) 사용 중이면 정리
        if (isUsingHos)
        {
            // 식사 도구 비활성화
            if (currentHosUtensil != null)
            {
                currentHosUtensil.SetActive(false);
                currentHosUtensil = null;
            }

            // ChairPoint 해제 (테이블 비활성화)
            if (currentHosChairPoint != null)
            {
                currentHosChairPoint.ReleaseChair(this);
                currentHosChairPoint = null;
            }

            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
                animator.SetBool("Eating", false);
            }

            // NavMeshAgent 활성화
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // Point 점유 해제
            if (currentHosPoint != null)
            {
                lock (hosPointLock)
                {
                    if (occupiedHosPoints.ContainsKey(currentHosPoint))
                    {
                        occupiedHosPoints.Remove(currentHosPoint);
                    }
                }
                currentHosPoint = null;
            }

            // 시설 점유 해제
            if (currentHosTransform != null)
            {
                lock (hosLock)
                {
                    if (occupiedHosFacilities.ContainsKey(currentHosTransform))
                    {
                        occupiedHosFacilities.Remove(currentHosTransform);
                    }
                }
            }

            // 결제 처리
            ProcessHosFacilityPayment();

            // 플래그 정리
            isUsingHos = false;
            currentHosTransform = null;
            
            Debug.Log($"[활동 정리] {gameObject.name}: Hos(고급식당) 활동 정리 완료");
        }
    }

    private void CleanupCoroutines()
    {
        if (wanderingCoroutine != null)
        {
            StopCoroutine(wanderingCoroutine);
            wanderingCoroutine = null;
        }
        if (queueCoroutine != null)
        {
            StopCoroutine(queueCoroutine);
            queueCoroutine = null;
        }
        if (roomUseCoroutine != null)
        {
            StopCoroutine(roomUseCoroutine);
            roomUseCoroutine = null;
        }
        if (roomWanderingCoroutine != null)
        {
            StopCoroutine(roomWanderingCoroutine);
            roomWanderingCoroutine = null;
        }
        if (useWanderingCoroutine != null)
        {
            StopCoroutine(useWanderingCoroutine);
            useWanderingCoroutine = null;
        }
        if (delayedBedMovementCoroutine != null)
        {
            StopCoroutine(delayedBedMovementCoroutine);
            delayedBedMovementCoroutine = null;
        }
        if (sleepingCoroutine != null)
        {
            StopCoroutine(sleepingCoroutine);
            sleepingCoroutine = null;
        }
        // nappingCoroutine은 Sleeping 상태에서도 유지되어야 함
        if (nappingCoroutine != null && currentState != AIState.Sleeping)
        {
            StopCoroutine(nappingCoroutine);
            nappingCoroutine = null;
        }
        // sunbedCoroutine은 UsingSunbed 상태에서도 유지되어야 함
        if (sunbedCoroutine != null && currentState != AIState.UsingSunbed)
        {
            StopCoroutine(sunbedCoroutine);
            sunbedCoroutine = null;
        }
        // bathtubCoroutine은 UsingBathtub 상태에서도 유지되어야 함
        if (bathtubCoroutine != null && currentState != AIState.UsingBathtub)
        {
            StopCoroutine(bathtubCoroutine);
            bathtubCoroutine = null;
        }
        // healthCoroutine은 UsingHealth 상태에서도 유지되어야 함
        if (healthCoroutine != null && currentState != AIState.UsingHealth)
        {
            StopCoroutine(healthCoroutine);
            healthCoroutine = null;
        }
        // cafeCoroutine은 UsingCafe 상태에서도 유지되어야 함
        if (cafeCoroutine != null && currentState != AIState.UsingCafe)
        {
            StopCoroutine(cafeCoroutine);
            cafeCoroutine = null;
        }
        // bathCoroutine은 UsingBath 상태에서도 유지되어야 함
        if (bathCoroutine != null && currentState != AIState.UsingBath)
        {
            StopCoroutine(bathCoroutine);
            bathCoroutine = null;
        }
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
    }

    private void CleanupResources()
    {
        // ✅ occupiedSunbeds 제거 - 시설 레벨 점유 불필요
        
        // SunbedPoint 점유 해제
        if (currentSunbedPoint != null)
        {
            lock (sunbedPointLock)
            {
                if (occupiedSunbedPoints.ContainsKey(currentSunbedPoint) && occupiedSunbedPoints[currentSunbedPoint] == this)
                {
                    occupiedSunbedPoints.Remove(currentSunbedPoint);
                    Debug.Log($"[SunbedPoint 점유 해제] {gameObject.name}: AI 삭제로 점유 해제");
                }
            }
        }
        
        // 수면 상태 정리
        if (isSleeping)
        {
            // BedPoint 점유 해제
            if (currentBedPoint != null)
            {
                lock (bedPointLock)
                {
                    if (occupiedBedPoints.ContainsKey(currentBedPoint) && occupiedBedPoints[currentBedPoint] == this)
                    {
                        occupiedBedPoints.Remove(currentBedPoint);
                        Debug.Log($"[BedPoint 점유 해제] {gameObject.name}: AI 삭제로 점유 해제");
                    }
                }
                currentBedPoint = null;
            }
            
            if (isNapping)
            {
                // 낮잠 중이면 낮잠 정리
                isNapping = false;
                isSleeping = false;
                if (animator != null)
                {
                    animator.SetBool("BedTime", false);
                }
                if (agent != null)
                {
                    agent.enabled = true;
                }
            }
            else
            {
                // 야간 수면이면 WakeUp 호출
                WakeUp();
            }
        }
        
        // 선베드 사용 상태 정리 (애니메이션 지연 없이 직접 정리)
        if (isUsingSunbed)
        {
            // 1. BedTime 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }

            // 2. NavMeshAgent 다시 활성화 (침대와 동일하게)
            if (agent != null)
            {
                agent.enabled = true;
            }

            // 3. 저장된 위치로 복귀
            transform.position = preSunbedPosition;
            transform.rotation = preSunbedRotation;

            // 4. 선베드 사용 상태 해제
            isUsingSunbed = false;
            currentSunbedTransform = null;
            currentSunbedPoint = null; // SunbedPoint도 정리

            // 5. 선베드 결제 처리 (GameObject 비활성화 시에는 코루틴 시작 없이)
            ProcessSunbedPaymentDirectly();
            
            // 6. ✅ isSunbedInRoom 제거
        }
        
        // 욕조 사용 상태 정리
        if (isUsingBathtub)
        {
            // 1. ✅ BedTime 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }

            // 2. NavMeshAgent 다시 활성화
            if (agent != null)
            {
                agent.enabled = true;
            }

            // 3. 저장된 위치로 복귀
            transform.position = preBathtubPosition;
            transform.rotation = preBathtubRotation;

            // 4. 욕조 사용 상태 해제
            isUsingBathtub = false;
            currentBathtubTransform = null;
            
            // 욕조는 방 시설이므로 결제 처리 없음
        }
        
        // 운동 시설 사용 상태 정리
        if (isUsingHealth)
        {
            // 1. Exercise 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Exercise", false);
            }

            // 2. 저장된 위치로 복귀
            transform.position = preHealthPosition;
            transform.rotation = preHealthRotation;

            // 3. 운동 시설 사용 상태 해제
            isUsingHealth = false;
            currentHealthTransform = null;
            
            // 운동 시설은 방 시설이므로 결제 처리 없음
        }
        
        // 카페 사용 상태 정리
        if (isUsingCafe)
        {
            // 1. Sitting 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("Sitting", false);
            }

            // 2. 저장된 위치로 복귀
            transform.position = preCafePosition;
            transform.rotation = preCafeRotation;

            // 3. CafePoint 점유 해제
            if (currentCafePoint != null)
            {
                lock (cafePointLock)
                {
                    if (occupiedCafePoints.ContainsKey(currentCafePoint) && occupiedCafePoints[currentCafePoint] == this)
                    {
                        occupiedCafePoints.Remove(currentCafePoint);
                        Debug.Log($"[CafePoint 점유 해제] {gameObject.name}: AI 삭제로 점유 해제 (점유 중: {occupiedCafePoints.Count}개)");
                    }
                }
                currentCafePoint = null;
            }

            // 4. 카페 사용 상태 해제
            isUsingCafe = false;
            currentCafeTransform = null;
        }
        
        // Bath 사용 상태 정리
        if (isUsingBath)
        {
            // 1. 애니메이션 종료
            if (animator != null)
            {
                if (isBathSitting)
                {
                    animator.SetBool("Sitting", false);
                }
                else
                {
                    animator.SetBool("BedTime", false);
                }
            }

            // 2. 저장된 위치로 복귀
            transform.position = preBathPosition;
            transform.rotation = preBathRotation;

            // 3. BathPoint 점유 해제
            if (currentBathPoint != null)
            {
                if (isBathSitting)
                {
                    lock (bathSitPointLock)
                    {
                        if (occupiedBathSitPoints.ContainsKey(currentBathPoint) && occupiedBathSitPoints[currentBathPoint] == this)
                        {
                            occupiedBathSitPoints.Remove(currentBathPoint);
                            Debug.Log($"[BathSitPoint 점유 해제] {gameObject.name}: AI 삭제로 점유 해제 (점유 중: {occupiedBathSitPoints.Count}개)");
                        }
                    }
                }
                else
                {
                    lock (bathDownPointLock)
                    {
                        if (occupiedBathDownPoints.ContainsKey(currentBathPoint) && occupiedBathDownPoints[currentBathPoint] == this)
                        {
                            occupiedBathDownPoints.Remove(currentBathPoint);
                            Debug.Log($"[BathDownPoint 점유 해제] {gameObject.name}: AI 삭제로 점유 해제 (점유 중: {occupiedBathDownPoints.Count}개)");
                        }
                    }
                }
                currentBathPoint = null;
            }

            // 4. Bath 사용 상태 해제
            isUsingBath = false;
            isBathSitting = false;
            currentBathTransform = null;
        }

        // 식사 상태 정리 (GameObject 비활성화 시에는 코루틴 시작 없이)
        // 주방 카운터 관련 상태 정리
        if (isWaitingAtKitchenCounter || currentKitchenCounter != null)
        {
            // 주방 카운터 대기열에서 제거
            if (currentKitchenCounter != null)
            {
                currentKitchenCounter.LeaveQueue(this);
            }
            
            // NavMeshAgent 다시 활성화 (침대와 동일하게)
            if (agent != null)
            {
                agent.enabled = true;
            }
            
            // 주방 관련 변수들 정리
            CleanupKitchenVariables();
        }
        
        if (isEating)
        {
            // 🍝 Pasta 오브젝트 비활성화 (중요!)
            if (pastaObject != null && pastaObject.activeSelf)
            {
                pastaObject.SetActive(false);
            }
            
            // 식사 도구 비활성화
            if (currentEatingUtensil != null && currentEatingUtensil.activeSelf)
            {
                currentEatingUtensil.SetActive(false);
                currentEatingUtensil = null;
            }
            
            // ChairPoint 해제
            if (currentChairPoint != null)
            {
                currentChairPoint.ReleaseChair(this);
                currentChairPoint = null;
            }
            
            // 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("PickUp", false);
                animator.SetBool("Eating", false);
                animator.SetBool("Sitting", false);
            }

            // NavMeshAgent 다시 활성화 (침대와 동일하게)
            if (agent != null)
            {
                agent.enabled = true;
            }

            // 저장된 위치로 복귀
            transform.position = preChairPosition;
            transform.rotation = preChairRotation;

            // 식사 상태 해제
            isEating = false;
            currentKitchenTransform = null;
            currentChairTransform = null;
            
            Debug.Log($"[리소스 정리] {gameObject.name}: 식사 관련 모든 오브젝트 정리 완료 (Pasta 포함)");
        }
        
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                }
                currentRoomIndex = -1;
            }
        }

        isInQueue = false;
        isWaitingForService = false;
        isScheduledForDespawn = false; // 디스폰 예정 플래그 리셋
        queueRetryCount = 0; // 대기열 재시도 횟수 리셋

        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion

    #region UI
    void OnGUI()
    {
        // 디버그 UI가 활성화된 경우에만 표시
        if (!globalShowDebugUI) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            Vector2 guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            GUI.Label(new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 40), currentDestination);
        }
    }
    #endregion

    #region 공개 메서드
    public void InitializeAI()
    {
        currentState = AIState.MovingToQueue;
        currentDestination = "대기열로 이동 중";
        isInQueue = false;
        isWaitingForService = false;
        currentRoomIndex = -1;
        lastBehaviorUpdateHour = -1;
        queueRetryCount = 0; // 대기열 재시도 횟수 초기화
        
        // 수면 관련 초기화
        isSleeping = false;
        currentBedTransform = null;
        preSleepPosition = Vector3.zero;
        preSleepRotation = Quaternion.identity;
        
        // 선베드 관련 초기화
        isUsingSunbed = false;
        // ✅ isSunbedInRoom 제거
        currentSunbedTransform = null;
        preSunbedPosition = Vector3.zero;
        preSunbedRotation = Quaternion.identity;
        
        // 식사 관련 초기화
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
        preChairPosition = Vector3.zero;
        preChairRotation = Quaternion.identity;
        
        // 주방 카운터 관련 초기화
        currentKitchenCounter = null;
        isWaitingAtKitchenCounter = false;

        if (agent != null)
        {
            agent.ResetPath();
            DetermineInitialBehavior();
        }
    }

    void OnEnable()
    {
        // 복원 중이 아닐 때만 초기화
        if (!isBeingRestored)
        {
            InitializeAI();
        }
    }

    public void SetQueueDestination(Vector3 position)
    {
        targetQueuePosition = position;
        if (agent != null)
        {
            agent.SetDestination(position);
        }
    }

    public void SetQueueDestination(Vector3 position, Quaternion rotation)
    {
        targetQueuePosition = position;
        targetQueueRotation = rotation;
        if (agent != null)
        {
            // NavMeshAgent 재시작 (이전에 정지되었을 수 있음)
            agent.isStopped = false;
            agent.SetDestination(position);
        }
    }

    public void OnServiceComplete()
    {
        isWaitingForService = false;
        // isInQueue는 여기서 false로 설정하지 않음
        // QueueBehavior 코루틴에서 서비스 완료 후 처리가 끝나면 자동으로 break로 빠져나감
    }
    #endregion

    #region 저장/로드 시스템
    /// <summary>
    /// AI의 현재 상태를 저장 가능한 데이터로 변환
    /// </summary>
    public AISaveData GetSaveData()
    {
        AISaveData saveData = new AISaveData
        {
            aiName = gameObject.name,
            position = transform.position,
            rotation = transform.rotation,
            currentRoomIndex = currentRoomIndex,
            currentState = currentState.ToString(),
            
            // 상태 플래그들
            isUsingHealth = isUsingHealth,
            isUsingWedding = isUsingWedding,
            isUsingLounge = isUsingLounge,
            isUsingHall = isUsingHall,
            isUsingSauna = isUsingSauna,
            isSaunaSitting = isSaunaSitting,
            isUsingSunbed = isUsingSunbed,
            isEating = isEating,
            isUsingBathtub = isUsingBathtub,
            isSleeping = isSleeping,
            isNapping = isNapping,
            
            // 시설 위치 저장
            hasHealthPosition = currentHealthTransform != null,
            hasWeddingPosition = currentWeddingTransform != null,
            hasLoungePosition = currentLoungeTransform != null,
            hasHallPosition = currentHallTransform != null,
            hasSaunaPosition = currentSaunaTransform != null,
            hasSunbedPosition = currentSunbedTransform != null
        };
        
        // 시설 위치가 있으면 저장
        if (currentHealthTransform != null)
        {
            saveData.currentHealthPosition = currentHealthTransform.position;
        }
        if (currentWeddingTransform != null)
        {
            saveData.currentWeddingPosition = currentWeddingTransform.position;
        }
        if (currentLoungeTransform != null)
        {
            saveData.currentLoungePosition = currentLoungeTransform.position;
        }
        if (currentHallTransform != null)
        {
            saveData.currentHallPosition = currentHallTransform.position;
        }
        if (currentSaunaTransform != null)
        {
            saveData.currentSaunaPosition = currentSaunaTransform.position;
        }
        if (currentSunbedTransform != null)
        {
            saveData.currentSunbedPosition = currentSunbedTransform.position;
        }
        
        return saveData;
    }

    /// <summary>
    /// 저장된 데이터에서 AI 상태 복원
    /// </summary>
    public void LoadFromSaveData(AISaveData saveData)
    {
        // 복원 플래그 설정 (DetermineInitialBehavior 방지)
        isBeingRestored = true;
        
        // 컴포넌트 초기화 확인 (Start()가 아직 실행 안 됐을 수 있음)
        if (agent == null || animator == null)
        {
            InitializeComponents();
        }
        
        // 위치 복원 시 NavMesh 위의 유효한 위치 찾기
        Vector3 targetPosition = saveData.position;
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Debug.Log($"[AI Load] {gameObject.name}: NavMesh 위 유효한 위치 찾음 (거리: {Vector3.Distance(targetPosition, hit.position):F2}m)");
        }
        else
        {
            // NavMesh를 찾지 못하면 Spawn 위치로 이동
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                Debug.LogWarning($"[AI Load] {gameObject.name}: NavMesh 위치를 찾지 못해 Spawn 위치로 이동");
            }
            else
            {
                transform.position = targetPosition;
                Debug.LogWarning($"[AI Load] {gameObject.name}: NavMesh를 찾지 못했지만 원래 위치로 설정");
            }
        }
        
        transform.rotation = saveData.rotation;
        
        // 방 번호 복원
        currentRoomIndex = saveData.currentRoomIndex;
        
        // 상태 플래그 복원
        isUsingHealth = saveData.isUsingHealth;
        isUsingWedding = saveData.isUsingWedding;
        isUsingLounge = saveData.isUsingLounge;
        isUsingHall = saveData.isUsingHall;
        isUsingSauna = saveData.isUsingSauna;
        isSaunaSitting = saveData.isSaunaSitting;
        isUsingSunbed = saveData.isUsingSunbed;
        isEating = saveData.isEating;
        isUsingBathtub = saveData.isUsingBathtub;
        isSleeping = saveData.isSleeping;
        isNapping = saveData.isNapping;
        
        // 시설 Transform 재탐색 (상태 복원 전에 먼저 해야 함)
        if (saveData.hasHealthPosition)
        {
            FindHealthFacilityByPosition(saveData.currentHealthPosition);
        }
        if (saveData.hasWeddingPosition)
        {
            FindWeddingFacilityByPosition(saveData.currentWeddingPosition);
        }
        if (saveData.hasLoungePosition)
        {
            FindLoungeFacilityByPosition(saveData.currentLoungePosition);
        }
        if (saveData.hasHallPosition)
        {
            FindHallFacilityByPosition(saveData.currentHallPosition);
        }
        if (saveData.hasSaunaPosition)
        {
            FindSaunaFacilityByPosition(saveData.currentSaunaPosition);
        }
        if (saveData.hasSunbedPosition)
        {
            FindSunbedByPosition(saveData.currentSunbedPosition);
        }
        
        // 코루틴으로 한 프레임 대기 후 상태 복원 (Start() 실행 보장)
        StartCoroutine(RestoreStateAfterFrame(saveData.currentState));
        
        Debug.Log($"[AI Load] {gameObject.name}: 위치={saveData.position}, 방={currentRoomIndex}, 상태 예약={saveData.currentState}");
    }

    /// <summary>
    /// 한 프레임 대기 후 상태 복원 (컴포넌트 초기화 보장)
    /// </summary>
    private System.Collections.IEnumerator RestoreStateAfterFrame(string savedState)
    {
        // 한 프레임 대기 (Start() 실행 보장)
        yield return null;
        
        Debug.Log($"[AI Load Coroutine] {gameObject.name}: 상태 복원 코루틴 시작 - 저장된 상태: {savedState}");
        
        // TimeSystem 초기화 확인
        if (timeSystem == null)
        {
            timeSystem = TimeSystem.Instance;
            Debug.Log($"[AI Load Coroutine] {gameObject.name}: TimeSystem 초기화됨");
        }
        
        // NavMeshAgent 상태 확인
        if (agent != null)
        {
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[AI Load Coroutine] {gameObject.name}: NavMesh 위에 없음! 위치: {transform.position}");
                // 배회 상태로 전환
                TransitionToState(currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering);
                yield break;
            }
            Debug.Log($"[AI Load Coroutine] {gameObject.name}: NavMeshAgent 정상 (NavMesh 위에 있음)");
        }
        else
        {
            Debug.LogError($"[AI Load Coroutine] {gameObject.name}: NavMeshAgent가 null!");
            yield break;
        }
        
        // 상태 복원 (enum 변환)
        if (System.Enum.TryParse(savedState, out AIState loadedState))
        {
            // "Using" 상태는 특별 처리 필요
            bool needsSpecialHandling = false;
            
            switch (loadedState)
            {
                case AIState.UsingHealth:
                    if (currentHealthTransform != null)
                    {
                        // 헬스장 사용 중이었으면 해당 상태로 바로 복원
                        StartUsingHealth(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 헬스장 시설을 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingWedding:
                    if (currentWeddingTransform != null)
                    {
                        StartUsingWedding(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 예식장 시설을 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingLounge:
                    if (currentLoungeTransform != null)
                    {
                        StartUsingLounge(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 라운지 시설을 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingHall:
                    if (currentHallTransform != null)
                    {
                        StartUsingHall(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 연회장 시설을 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingSauna:
                    if (currentSaunaTransform != null)
                    {
                        StartUsingSauna(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 사우나 시설을 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingSunbed:
                    if (currentSunbedTransform != null)
                    {
                        StartUsingSunbed();
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 선베드를 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
                    
                case AIState.UsingBathtub:
                    if (currentBathtubTransform != null)
                    {
                        StartUsingBathtub(null); // Point는 다시 찾음
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 욕조를 찾을 수 없어 배회로 전환");
                        loadedState = AIState.RoomWandering;
                    }
                    break;
                    
                case AIState.Sleeping:
                    if (currentBedTransform != null)
                    {
                        // BedPoint 찾기
                        Transform bedPoint = FindNearestBedPoint(currentBedTransform);
                        StartSleeping(bedPoint);
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 침대를 찾을 수 없어 배회로 전환");
                        loadedState = AIState.RoomWandering;
                    }
                    break;
                    
                case AIState.Eating:
                    if (currentChairTransform != null)
                    {
                        StartEating();
                        needsSpecialHandling = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI Load] {gameObject.name}: 의자를 찾을 수 없어 배회로 전환");
                        loadedState = currentRoomIndex != -1 ? AIState.RoomWandering : AIState.Wandering;
                    }
                    break;
            }
            
            // 특별 처리가 필요 없는 상태는 TransitionToState 사용
            if (!needsSpecialHandling)
            {
                TransitionToState(loadedState);
            }
            
            Debug.Log($"[AI Load] {gameObject.name}: 상태 복원 완료 = {currentState}");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 상태 파싱 실패 = {savedState}");
        }
        
        // 복원 플래그 해제
        isBeingRestored = false;
        Debug.Log($"[AI Load] {gameObject.name}: 복원 완료 - 정상 동작 시작");
    }

    /// <summary>
    /// 저장된 위치를 기반으로 헬스장 시설 재탐색
    /// </summary>
    private void FindHealthFacilityByPosition(Vector3 position)
    {
        GameObject[] allHealths = GameObject.FindGameObjectsWithTag("Health");
        float closestDistance = float.MaxValue;
        Transform closestHealth = null;
        
        foreach (var health in allHealths)
        {
            float distance = Vector3.Distance(position, health.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestHealth = health.transform;
            }
        }
        
        if (closestHealth != null)
        {
            currentHealthTransform = closestHealth;
            Debug.Log($"[AI Load] {gameObject.name}: 헬스장 시설 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 헬스장 시설 재탐색 실패");
        }
    }

    /// <summary>
    /// 저장된 위치를 기반으로 예식장 시설 재탐색
    /// </summary>
    private void FindWeddingFacilityByPosition(Vector3 position)
    {
        GameObject[] allWeddings = GameObject.FindGameObjectsWithTag("Wedding");
        float closestDistance = float.MaxValue;
        Transform closestWedding = null;
        
        foreach (var wedding in allWeddings)
        {
            float distance = Vector3.Distance(position, wedding.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestWedding = wedding.transform;
            }
        }
        
        if (closestWedding != null)
        {
            currentWeddingTransform = closestWedding;
            Debug.Log($"[AI Load] {gameObject.name}: 예식장 시설 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 예식장 시설 재탐색 실패");
        }
    }

    /// <summary>
    /// 저장된 위치를 기반으로 라운지 시설 재탐색
    /// </summary>
    private void FindLoungeFacilityByPosition(Vector3 position)
    {
        GameObject[] allLounges = GameObject.FindGameObjectsWithTag("Lounge");
        float closestDistance = float.MaxValue;
        Transform closestLounge = null;
        
        foreach (var lounge in allLounges)
        {
            float distance = Vector3.Distance(position, lounge.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestLounge = lounge.transform;
            }
        }
        
        if (closestLounge != null)
        {
            currentLoungeTransform = closestLounge;
            Debug.Log($"[AI Load] {gameObject.name}: 라운지 시설 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 라운지 시설 재탐색 실패");
        }
    }

    /// <summary>
    /// 저장된 위치를 기반으로 연회장 시설 재탐색
    /// </summary>
    private void FindHallFacilityByPosition(Vector3 position)
    {
        GameObject[] allHalls = GameObject.FindGameObjectsWithTag("Hall");
        float closestDistance = float.MaxValue;
        Transform closestHall = null;
        
        foreach (var hall in allHalls)
        {
            float distance = Vector3.Distance(position, hall.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestHall = hall.transform;
            }
        }
        
        if (closestHall != null)
        {
            currentHallTransform = closestHall;
            Debug.Log($"[AI Load] {gameObject.name}: 연회장 시설 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 연회장 시설 재탐색 실패");
        }
    }

    /// <summary>
    /// 저장된 위치를 기반으로 사우나 시설 재탐색
    /// </summary>
    private void FindSaunaFacilityByPosition(Vector3 position)
    {
        GameObject[] allSaunas = GameObject.FindGameObjectsWithTag("Sauna");
        float closestDistance = float.MaxValue;
        Transform closestSauna = null;
        
        foreach (var sauna in allSaunas)
        {
            float distance = Vector3.Distance(position, sauna.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestSauna = sauna.transform;
            }
        }
        
        if (closestSauna != null)
        {
            currentSaunaTransform = closestSauna;
            Debug.Log($"[AI Load] {gameObject.name}: 사우나 시설 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 사우나 시설 재탐색 실패");
        }
    }

    /// <summary>
    /// 저장된 위치를 기반으로 선베드 재탐색
    /// </summary>
    private void FindSunbedByPosition(Vector3 position)
    {
        GameObject[] allSunbeds = GameObject.FindGameObjectsWithTag("Sunbed");
        float closestDistance = float.MaxValue;
        Transform closestSunbed = null;
        
        foreach (var sunbed in allSunbeds)
        {
            float distance = Vector3.Distance(position, sunbed.transform.position);
            if (distance < closestDistance && distance < 1f) // 1m 이내
            {
                closestDistance = distance;
                closestSunbed = sunbed.transform;
            }
        }
        
        if (closestSunbed != null)
        {
            currentSunbedTransform = closestSunbed;
            Debug.Log($"[AI Load] {gameObject.name}: 선베드 재탐색 성공 ({closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"[AI Load] {gameObject.name}: 선베드 재탐색 실패");
        }
    }
    #endregion
}
}