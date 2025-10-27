using UnityEngine;
using System.Collections.Generic;
using ZLinq;

namespace JY
{
    /// <summary>
    /// 결제 시스템
    /// AI의 방 사용 요금 관리 및 결제 처리
    /// </summary>
    public class PaymentSystem : MonoBehaviour
    {
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("결제 처리 과정 로그 표시")]
        [SerializeField] private bool showPaymentLogs = true;
        
        /// <summary>
        /// 결제 정보 클래스
        /// </summary>
        [System.Serializable]
        public class PaymentInfo
        {
            public string aiName;
            public int amount;
            public string roomID;
            public int roomReputation;
            public bool isPaid;

            public PaymentInfo(string aiName, int amount, string roomID, int roomReputation = 0)
            {
                this.aiName = aiName;
                this.amount = amount;
                this.roomID = roomID;
                this.roomReputation = roomReputation;
                this.isPaid = false;
            }
        }

        [Header("결제 정보")]
        [SerializeField] public List<PaymentInfo> paymentQueue = new List<PaymentInfo>();
        
        // 명성도 시스템 참조
        private ReputationSystem reputationSystem;

        public static PaymentSystem Instance { get; set; }
        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // 명성도 시스템 참조 찾기
            reputationSystem = ReputationSystem.Instance;
            if (reputationSystem == null)
            {
                reputationSystem = FindFirstObjectByType<ReputationSystem>();
            }
            
            DebugLog("결제 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 결제 정보 추가 (기본)
        /// </summary>
        public void AddPayment(string aiName, int amount, string roomID)
        {
            paymentQueue.Add(new PaymentInfo(aiName, amount, roomID));
            DebugLog($"새로운 결제 등록: {aiName}, 방 {roomID}, {amount}원", showPaymentLogs);
        }
        
        /// <summary>
        /// 결제 정보 추가 (방 명성도 포함)
        /// </summary>
        public void AddPayment(string aiName, int amount, string roomID, int roomReputation)
        {
            Debug.Log($"[PaymentSystem.AddPayment] 호출됨 - AI: {aiName}, 금액: {amount}원, 방: {roomID}, 명성도: {roomReputation}");
            
            // ✅ 시설(Facility)은 여러 번 사용 가능 → 중복 체크 안 함
            // ✅ 방(Room)만 중복 체크 (한 AI가 동시에 두 방을 사용할 수 없음)
            bool isFacility = roomID.Contains("Facility") || 
                             roomID == "Sunbed" || 
                             roomID == "KITCHEN_ORDER" || 
                             roomID.Contains("Bathtub");
            
            if (!isFacility)
            {
                // 방(Room)만 중복 등록 방지
                bool isDuplicate = paymentQueue.Exists(p => p.aiName == aiName && p.roomID == roomID && !p.isPaid);
                if (isDuplicate)
                {
                    Debug.Log($"[PaymentSystem] 중복 결제 등록 방지: {aiName}, 방 {roomID} (이미 미결제 항목 존재)");
                    return;
                }
            }
            
            paymentQueue.Add(new PaymentInfo(aiName, amount, roomID, roomReputation));
            Debug.Log($"<color=lime>💰 [결제 등록 성공] AI: {aiName}, 항목: {roomID}, 금액: {amount}원, 명성도: {roomReputation}</color>");
            DebugLog($"새로운 결제 등록: {aiName}, 방 {roomID}, {amount}원, 명성도 {roomReputation}", showPaymentLogs);
        }
        
        /// <summary>
        /// 결제 처리
        /// </summary>
        public int ProcessPayment(string aiName)
        {
            Debug.Log($"[PaymentSystem.ProcessPayment] 호출됨 - AI: {aiName}, 현재 전체 큐 크기: {paymentQueue.Count}개");
            DebugLog($"결제 처리 시작 - AI: {aiName}", showPaymentLogs);
            
            int totalAmount = 0;
            List<PaymentInfo> aiPayments = paymentQueue.FindAll(p => p.aiName == aiName && !p.isPaid);
            
            Debug.Log($"[PaymentSystem.ProcessPayment] AI {aiName}의 미결제 항목: {aiPayments.Count}개");
            
            if (aiPayments.Count == 0)
            {
                int alreadyPaidCount = paymentQueue.FindAll(p => p.aiName == aiName && p.isPaid).Count;
                Debug.LogWarning($"[PaymentSystem] AI {aiName}의 미결제 항목이 0개입니다. 전체 큐: {paymentQueue.Count}개 (이미 결제됨: {alreadyPaidCount}개)");
                
                // 전체 큐 내용 출력
                Debug.Log($"[PaymentSystem] 전체 큐 내용:");
                foreach (var p in paymentQueue)
                {
                    Debug.Log($"  - AI: {p.aiName}, 방: {p.roomID}, 금액: {p.amount}원, 결제됨: {p.isPaid}");
                }
            }
            else
            {
                Debug.Log($"[PaymentSystem.ProcessPayment] AI {aiName}의 미결제 항목 {aiPayments.Count}개 발견");
                DebugLog($"{aiName}의 미결제 항목 {aiPayments.Count}개 발견", showPaymentLogs);
            }
            
            foreach (var payment in aiPayments)
            {
                totalAmount += payment.amount;
                payment.isPaid = true;
                Debug.Log($"[PaymentSystem.ProcessPayment] 결제 처리 중 - AI: {payment.aiName}, 방: {payment.roomID}, 금액: {payment.amount}원, 명성도: {payment.roomReputation}");
                DebugLog($"결제 처리: {payment.aiName}, 방 {payment.roomID}, {payment.amount}원, 명성도: {payment.roomReputation}", showPaymentLogs);
            }
            
            // 결제된 금액을 플레이어 소지금에 추가
            if (totalAmount > 0)
            {
                var playerWallet = PlayerWallet.Instance;
                if (playerWallet != null)
                {
                    int beforeMoney = playerWallet.money;
                    playerWallet.AddMoney(totalAmount);
                    int afterMoney = playerWallet.money;
                    Debug.Log($"<color=yellow>💰💰💰 [돈 획득!] AI: {aiName}, 획득 금액: {totalAmount}원 (이전: {beforeMoney}원 → 현재: {afterMoney}원)</color>");
                    DebugLog($"플레이어 소지금에 {totalAmount}원 추가", true);
                }
                else
                {
                    Debug.LogError("❌ PlayerWallet을 찾을 수 없습니다!");
                    DebugLog("PlayerWallet을 찾을 수 없습니다.", true);
                }
                
                // 명성도 증가 - 각 방의 명성도를 기반으로 명성도 증가
                if (reputationSystem != null)
                {
                    DebugLog($"명성도 시스템 발견, 명성도 증가 시작", showPaymentLogs);
                    int totalReputation = 0;
                    foreach (var payment in aiPayments)
                    {
                        totalReputation += payment.roomReputation;
                        DebugLog($"명성도 증가 호출 - AI: {payment.aiName}, 방: {payment.roomID}, 명성도: {payment.roomReputation}", showPaymentLogs);
                        // 방 명성도 기반으로 명성도 지급
                        reputationSystem.AddReputation(payment.roomReputation, $"방 {payment.roomID} 사용 완료");
                    }
                    Debug.Log($"<color=yellow>⭐⭐⭐ [명성도 획득!] AI: {aiName}, 획득 명성도: {totalReputation} (총 {aiPayments.Count}개 항목)</color>");
                }
                else
                {
                    Debug.LogError("❌ ReputationSystem을 찾을 수 없습니다!");
                    DebugLog("ReputationSystem을 찾을 수 없습니다!", true);
                }
            }
            
            // 처리된 결제 제거
            paymentQueue.RemoveAll(p => p.isPaid);
            
            if (totalAmount == 0)
            {
                Debug.LogWarning($"<color=red>❌ [결제 실패] AI: {aiName}, 결제 금액: 0원 (큐에 미결제 항목이 없음)</color>");
            }
            
            DebugLog($"결제 처리 완료 - 총 금액: {totalAmount}원", true);
            return totalAmount;
        }
        
        /// <summary>
        /// 미결제 항목 확인
        /// </summary>
        public bool HasUnpaidPayments(string aiName)
        {
            return paymentQueue.Exists(p => p.aiName == aiName && !p.isPaid);
        }
        
        /// <summary>
        /// 총 미결제 금액 반환
        /// </summary>
        public int GetTotalUnpaidAmount(string aiName)
        {
            return paymentQueue
                .AsValueEnumerable()
                .Where(p => p.aiName == aiName && !p.isPaid)
                .Sum(p => p.amount);
        }
        
        /// <summary>
        /// 특정 AI의 미결제 방 목록 반환
        /// </summary>
        public List<string> GetUnpaidRooms(string aiName)
        {
            return paymentQueue
                .AsValueEnumerable()
                .Where(p => p.aiName == aiName && !p.isPaid)
                .Select(p => p.roomID)
                .ToList();
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
        }
        
        #endregion
    }
}