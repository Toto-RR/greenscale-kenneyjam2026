using UnityEngine;
using UnityEngine.Audio;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;

    private const string VolumeKey = "MusicVolume";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetVolume(PlayerPrefs.GetFloat(VolumeKey, 0.75f));
    }

    public float GetVolume() => musicSource.volume;

    public void SetVolume(float value)
    {
        musicSource.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }
}