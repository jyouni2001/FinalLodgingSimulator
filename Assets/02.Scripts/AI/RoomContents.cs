using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 내용물 관리 클래스
    /// 방 안의 가구들을 관리하고 가격, 명성도를 계산
    /// </summary>
    public class RoomContents : MonoBehaviour
    {
        [Header("방 정보")]
        [Tooltip("방 고유 ID")]
        public string roomID;
        
        [Header("방 상태")]
        [Tooltip("방 사용 중 여부")]
        [SerializeField] private bool isRoomUsed = false;
        
        [Header("방 범위")]
        [Tooltip("방의 3D 경계")]
        public Bounds roomBounds;
        
        // ✅ 선베드 방 설정 제거 - 선베드는 더 이상 방이 아님
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("가구 스캔 과정 로그 표시")]
        [SerializeField] private bool showFurnitureLogs = false;
        
        [Header("가구 목록")]
        private List<FurnitureID> furnitureList = new List<FurnitureID>();
        
        // 공개 속성
        public bool IsRoomUsed => isRoomUsed;
        public int TotalRoomPrice { get; private set; }
        public int TotalRoomReputation { get; private set; }
        
        private void Start()
        {
            if (string.IsNullOrEmpty(roomID))
            {
                roomID = gameObject.name;
            }
            UpdateRoomContents();
            DebugLog("방 내용물 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 방 범위 설정
        /// </summary>
        public void SetRoomBounds(Bounds bounds)
        {
            // 전달받은 bounds를 그대로 사용 (RoomDetector에서 이미 올바르게 계산됨)
            roomBounds = bounds;

            UpdateRoomContents();
            DebugLog($"방 {roomID}의 범위가 업데이트되었습니다. 중심: {bounds.center}, 크기: {bounds.size}", true);
            DebugLog($"🎯 RoomContents 바운더리: Min({roomBounds.min}) Max({roomBounds.max})", true);
        }

        // ✅ SetAsSunbedRoom 메서드 제거 - 선베드는 더 이상 방이 아님
        
        /// <summary>
        /// 방 내용물 업데이트
        /// </summary>
        public void UpdateRoomContents()
        {
            // ✅ sunbed 방 로직 제거 - 모든 방은 일반 방으로 처리
            
            furnitureList.Clear();
            
            // 씬의 모든 FurnitureID 컴포넌트 찾기
            var allFurniture = GameObject.FindObjectsByType<FurnitureID>(FindObjectsSortMode.None);
            
            // roomBounds 안에 있는 가구만 필터링
            foreach (var furniture in allFurniture)
            {
                if (roomBounds.Contains(furniture.transform.position))
                {
                    furnitureList.Add(furniture);
                    DebugLog($"방 {roomID}에서 가구 발견: {furniture.gameObject.name}, 위치: {furniture.transform.position}", showFurnitureLogs);
                }
            }
            
            // 총 가격 계산
            CalculateTotalPrice();
            
            // 총 명성도 계산
            CalculateTotalReputation();
            
            DebugLog($"방 {roomID} 업데이트: 가구 {furnitureList.Count}개, 총 가격 {TotalRoomPrice}원, 총 명성도 {TotalRoomReputation}", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 총 가격 계산
        /// </summary>
        private void CalculateTotalPrice()
        {
            // ✅ sunbed 방 로직 제거 - 모든 방은 일반 방으로 처리
            
            TotalRoomPrice = 0;
            foreach (var furniture in furnitureList)
            {
                if (furniture != null && furniture.Data != null)
                {
                    TotalRoomPrice += furniture.Data.BasePrice;
                    DebugLog($"가구 가격 추가: {furniture.gameObject.name}, 가격: {furniture.Data.BasePrice}원", showFurnitureLogs);
                }
            }
        }
        
        /// <summary>
        /// 방 내 모든 가구의 명성도 합계 계산
        /// </summary>
        private void CalculateTotalReputation()
        {
            // ✅ sunbed 방 로직 제거 - 모든 방은 일반 방으로 처리
            
            TotalRoomReputation = 0;
            foreach (var furniture in furnitureList)
            {
                if (furniture != null && furniture.Data != null)
                {
                    TotalRoomReputation += furniture.Data.ReputationValue;
                    DebugLog($"가구 명성도 추가: {furniture.gameObject.name}, 명성도: {furniture.Data.ReputationValue}", showFurnitureLogs);
                }
            }
        }
        
        /// <summary>
        /// 방 사용 시작
        /// </summary>
        public int UseRoom()
        {
            if (isRoomUsed)
            {
                DebugLog($"방 {roomID}는 이미 사용 중입니다.", true);
                return 0;
            }
            
            isRoomUsed = true;
            DebugLog($"방 {roomID} 사용 시작", true);
            return TotalRoomPrice;
        }
        
        /// <summary>
        /// 방 사용 완료
        /// </summary>
        public void ReleaseRoom()
        {
            isRoomUsed = false;
            DebugLog($"방 {roomID} 사용 완료", true);
        }

        /// <summary>
        /// 기즈모 그리기 (방 범위 시각화)
        /// </summary>
        private void OnDrawGizmos()
        {
            // 방의 범위를 시각적으로 표시
            Gizmos.color = isRoomUsed ? Color.red : Color.yellow;
            
            // ✅ sunbed 방 로직 제거 - 모든 방은 일반 방으로 처리
            
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
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