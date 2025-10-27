using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSystem : MonoBehaviour
{
    public static LoadingSystem Instance { get; private set; }

    [SerializeField] private GameObject circleObject;
    private Material circleMaterialInstance;

    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 이미 인스턴스가 있다면 이 오브젝트는 파괴
            return;
        }

        if (circleObject != null)
        {
            Image circleImage = circleObject.GetComponent<Image>();

            if (circleImage != null)
            {
                circleMaterialInstance = circleImage.material;
            }

            circleObject.SetActive(false);
        }

        if(fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
        }
    }

    public void ShowLoadingScreen()
    {
        if (circleObject != null)
        {
            circleObject.SetActive(true);
        }
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f; // 페이드 아웃(검게 변함) 시작을 위해 투명하게 설정
        }
    }

    public void LoadScene(string sceneName)
    {
        StopAllCoroutines();
        ShowLoadingScreen(); // UI 먼저 표시
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    public IEnumerator LoadSceneAsync(string sceneName)
    {
        // [추가] 코루틴 시작 시 UI가 켜져 있는지 다시 한번 확인
        ShowLoadingScreen();

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // 로딩이 90% 완료될 때까지 대기
        while (op.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (circleMaterialInstance != null)
            {
                circleMaterialInstance.SetFloat("_Progressing", progress);
            }
            yield return null;
        }

        // 로딩 완료 직전: 100%
        if (circleMaterialInstance != null)
        {
            circleMaterialInstance.SetFloat("_Progressing", 1f);
        }

        // 페이드 아웃 효과 (알파 0 -> 1, 화면이 검게 변함)
        if (fadeCanvasGroup != null)
        {
            float fadeDuration = 1.0f;
            float elapsedTime = 0f;
            fadeCanvasGroup.alpha = 0f; // 시작은 투명하게

            while (elapsedTime < fadeDuration)
            {
                // [수정] Time.timeScale 영향 안 받도록 unscaledDeltaTime 사용 (GC 최소화)
                elapsedTime += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        // [수정] WaitForSeconds -> WaitForSecondsRealtime (Time.timeScale 영향 방지)
        yield return new WaitForSecondsRealtime(1.0f);
        op.allowSceneActivation = true;

        // 씬 활성화가 완전히 끝날 때까지 대기
        yield return op;

        // MainScene의 IngameFadeSystem이 자동으로 페이드 인을 시작할 것입니다.
        // 이 LoadingSystem 오브젝트는 역할을 다했으므로 스스로 파괴합니다.
        // (IngameFadeSystem.cs의 Start()에 FadeIn() 로직이 이미 있습니다)
        Destroy(gameObject);
    }
}

