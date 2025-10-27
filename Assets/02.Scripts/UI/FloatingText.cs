using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private float moveDistance = 100f;
    [SerializeField] private float fadeDuration = 1.5f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Show(string message, Color color)
    {
        text.text = message;
        text.color = color;

        // 애니메이션 실행
        Animate();
    }

    private void Animate()
    {
        // 초기 상태 설정 (위치 및 알파 값)
        //rectTransform.anchoredPosition = Vector2.zero;
        text.alpha = 1f;

        // DOTween을 사용하여 애니메이션 시퀀스 생성
        // 1. 위로 이동하면서 동시에 페이드 아웃
        rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + moveDistance, fadeDuration).SetUpdate(true);
        text.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            // 애니메이션이 끝나면 오브젝트 풀로 반환
            gameObject.SetActive(false);
        });
    }
}