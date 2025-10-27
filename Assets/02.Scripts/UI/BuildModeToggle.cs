using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BuildModeToggle : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("움직일 이미지 UI의 RectTransform")]
    [SerializeField] private RectTransform movingImageRect;

    [Header("위치 값")]
    [Tooltip("왼쪽 (건축 모드)일 때의 X 좌표")]
    [SerializeField] private float buildModePosX = -55;

    [Tooltip("오른쪽 (삭제 모드)일 때의 X 좌표")]
    [SerializeField] private float deleteModePosX = 55;

    [Header("애니메이션 설정")]
    [Tooltip("애니메이션 총 시간")]
    [SerializeField] private float duration = 0.4f;

    [Tooltip("이동에 적용할 Ease 함수")]
    [SerializeField] private Ease easeType = Ease.OutBack;

    [Header("'샤샤삭' 효과 설정")]
    [Tooltip("X축으로 늘어날 최대 스케일")]
    [SerializeField] private float stretchScaleX = 1.8f;

    [Tooltip("Y축으로 줄어들 최소 스케일")]
    [SerializeField] private float squashScaleY = 0.6f;

    // 내부 상태 변수
    private enum Mode { Build, Delete }
    private Mode currentMode = Mode.Build;
    private Sequence currentSequence; // 현재 실행 중인 DOTween 시퀀스를 저장

    /// <summary>
    /// 스크립트 시작 시 호출됩니다.
    /// 건축 모드로 초기화합니다.
    /// </summary>
    void Start()
    {
        // 시작은 항상 '건축 모드'로 설정
        SetMode(Mode.Build, false); // 애니메이션 없이 즉시 설정
    }

    /// <summary>
    /// 모드를 토글하는 공개 메소드. InputManager에서 호출됩니다.
    /// </summary>
    public void ToggleMode()
    {
        Mode newMode = (currentMode == Mode.Build) ? Mode.Delete : Mode.Build;
        SetMode(newMode, true); // 애니메이션과 함께 설정
    }
    public void ResetToBuildMode()
    {
        // 애니메이션 없이 즉시 '건축' 모드로 설정
        currentSequence?.Kill();

        if (movingImageRect != null)
        {
            movingImageRect.localScale = Vector3.one;
        }

        SetMode(Mode.Build, false);
    }

    /// <summary>
    /// 지정된 모드로 상태를 변경하고 UI 애니메이션을 실행합니다.
    /// </summary>
    /// <param name="mode">변경할 모드 (Build 또는 Delete)</param>
    /// <param name="animate">애니메이션 실행 여부</param>
    private void SetMode(Mode mode, bool animate)
    {
        currentMode = mode;

        // 목표 위치 결정
        float targetX = (currentMode == Mode.Build) ? buildModePosX : deleteModePosX;

        // PlacementSystem의 상태를 즉시 변경
        if (PlacementSystem.Instance != null)
        {
            if (currentMode == Mode.Build)
            {
                PlacementSystem.Instance.StopDeleteMode();
            }
            else
            {
                PlacementSystem.Instance.StartDeleteMode();
            }
        }
        else
        {
            Debug.LogError("[BuildModeToggle] PlacementSystem.Instance를 찾을 수 없습니다!");
        }


        // 애니메이션이 필요 없으면 즉시 위치 설정 후 종료
        if (!animate)
        {
            movingImageRect.anchoredPosition = new Vector2(targetX, movingImageRect.anchoredPosition.y);
            return;
        }

        currentSequence?.Kill();
        currentSequence = DOTween.Sequence();

        // 위치 이동 트윈
        Tween moveTween = movingImageRect.DOAnchorPosX(targetX, duration)
                                    .SetEase(easeType);

        // 스케일 변경 트윈 (Squash & Stretch)
        Sequence scaleSequence = DOTween.Sequence();
        float halfDuration = duration / 2f;

        scaleSequence.Append(movingImageRect.DOScale(new Vector3(stretchScaleX, squashScaleY, 1f), halfDuration))
                     .Append(movingImageRect.DOScale(Vector3.one, halfDuration));

        // 메인 시퀀스에 두 트윈을 동시에(Join) 실행하도록 추가
        // [GC 최적화] OnComplete 콜백에 람다 대신 메소드 참조를 사용하여 불필요한 할당 방지
        currentSequence.Append(moveTween)
                       .Join(scaleSequence)
                       .SetUpdate(true) // Time.timeScale에 영향받지 않도록 설정
                       .OnComplete(OnAnimationComplete);
    }

    /// <summary>
    /// 애니메이션 완료 시 호출될 콜백 함수입니다.
    /// </summary>
    private void OnAnimationComplete()
    {
        // 현재 시퀀스가 완료되었으므로 참조를 null로 설정합니다.
        currentSequence = null;
    }

    /// <summary>
    /// 이 오브젝트가 파괴될 때 호출됩니다.
    /// 실행 중인 모든 DOTween 시퀀스를 파괴하여 메모리 누수를 방지합니다.
    /// </summary>
    void OnDestroy()
    {
        // currentSequence?.Kill(); 와 동일한 동작
        if (currentSequence != null)
        {
            currentSequence.Kill();
        }
    }
}