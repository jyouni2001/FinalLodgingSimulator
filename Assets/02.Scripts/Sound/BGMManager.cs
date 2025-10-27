using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement; // AudioMixerGroup을 사용하기 위해 추가

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioClip startTheme; 
    [SerializeField] private AudioClip mainTheme; 
    [SerializeField] private AudioMixerGroup bgmMixerGroup; // BGM 믹서 그룹

    [SerializeField] private bool bgmChanged = false;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 BGM이 끊기지 않게 함
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // AudioSource의 Output을 BGM 믹서 그룹으로 설정
        if (bgmAudioSource != null && bgmMixerGroup != null)
        {
            bgmAudioSource.outputAudioMixerGroup = bgmMixerGroup;
        }

        if (SceneManager.GetActiveScene().name == "StartScene")
        {
            // BGM 재생
            PlayBGM(startTheme);
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "MainScene" && !bgmChanged)
        {
            bgmAudioSource.clip = null;
            PlayBGM(mainTheme);
            bgmChanged = true;
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmAudioSource == null) return;

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = true; // BGM은 보통 반복재생
        bgmAudioSource.Play();
    }
}