using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitioner : MonoBehaviour
{
    public static SceneTransitioner Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        fadeCanvasGroup.DOFade(0f, fadeDuration)
            .OnComplete(() => fadeCanvasGroup.blocksRaycasts = false);
    }

    public void LoadScene(string sceneName)
    {
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.DOFade(1f, fadeDuration)
            .OnComplete(() => SceneManager.LoadScene(sceneName));
    }

    public static void Load(string sceneName)
    {
        if (Instance != null) Instance.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }
}