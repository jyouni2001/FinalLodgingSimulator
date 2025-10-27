// TutorialAnimationManager.cs
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic; // List 사용을 위해 추가

[System.Serializable]
public class GuideSet // Inspector에서 설정할 가이드 UI 세트
{
    public string guideName; // TutorialStep의 guideAnimationName과 일치시킬 이름
    public Image highlightBackground;
    public Image arrow;
    public Image normalMouse;
    public Image clickedMouse;
}

public class TutorialAnimationManager : MonoBehaviour
{
    [SerializeField]
    private List<GuideSet> guideSets; // 여러 가이드 세트를 관리할 리스트

    private Coroutine currentAnimationCoroutine;
    private Sequence arrowSequence;

    // 모든 가이드 UI 요소들을 비활성화하고 시작
    private void Start()
    {
        HideAllGuides();
    }

    // 이름에 맞는 가이드 애니메이션을 찾아서 재생
    public void ShowGuideAnimation(string guideName)
    {
        if (string.IsNullOrEmpty(guideName)) return;

        GuideSet targetGuide = guideSets.Find(g => g.guideName == guideName);
        if (targetGuide == null)
        {
            Debug.LogError($"'{guideName}'에 해당하는 가이드 세트를 찾을 수 없습니다.");
            return;
        }

        // 이전에 실행 중인 애니메이션이 있다면 중지하고 모든 UI를 정리합니다.
        HideAllGuides();

        currentAnimationCoroutine = StartCoroutine(AnimateGuideSequence(targetGuide));
    }

    private IEnumerator AnimateGuideSequence(GuideSet guide)
    {
        // 1. 하이라이트 배경 페이드 인
        if (guide.highlightBackground != null)
        {
            guide.highlightBackground.gameObject.SetActive(true);
            guide.highlightBackground.color = new Color(1, 1, 1, 0); // 투명하게 시작
            Tween fadeTween = guide.highlightBackground.DOFade(0.7f, 0.5f).SetUpdate(true);
            yield return fadeTween.WaitForCompletion(); // 페이드 인이 끝날 때까지 대기
        }

        // 2. 화살표와 마우스 이미지 활성화
        if (guide.arrow != null) guide.arrow.gameObject.SetActive(true);
        if (guide.normalMouse != null) guide.normalMouse.gameObject.SetActive(true);
        if (guide.clickedMouse != null) guide.clickedMouse.gameObject.SetActive(false);

        // 3. 화살표 애니메이션 시작 (미리 배치된 위치에서 상대적으로 이동)
        if (guide.arrow != null)
        {
            arrowSequence = DOTween.Sequence().SetUpdate(true);
            arrowSequence.Append(guide.arrow.rectTransform.DOAnchorPos(guide.arrow.rectTransform.anchoredPosition + new Vector2(20, 20), 0.5f).SetEase(Ease.InOutSine))
                       .Append(guide.arrow.rectTransform.DOAnchorPos(guide.arrow.rectTransform.anchoredPosition, 0.5f).SetEase(Ease.InOutSine))
                       .SetLoops(-1);
        }

        // 4. 마우스 클릭 애니메이션 시작 (두 이미지 활성화/비활성화 반복)
        if (guide.normalMouse != null && guide.clickedMouse != null)
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                guide.normalMouse.gameObject.SetActive(false);
                guide.clickedMouse.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(0.3f);
                guide.normalMouse.gameObject.SetActive(true);
                guide.clickedMouse.gameObject.SetActive(false);
            }
        }
    }

    // 모든 가이드 UI 숨기기
    public void HideAllGuides()
    {
        arrowSequence?.Kill();

        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        foreach (var guide in guideSets)
        {
            if (guide.highlightBackground != null) guide.highlightBackground.gameObject.SetActive(false);
            if (guide.arrow != null) guide.arrow.gameObject.SetActive(false);
            if (guide.normalMouse != null) guide.normalMouse.gameObject.SetActive(false);
            if (guide.clickedMouse != null) guide.clickedMouse.gameObject.SetActive(false);
        }
    }
}