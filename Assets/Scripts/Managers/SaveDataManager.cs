using UnityEngine;

public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager instance;

    // Sliders
    public readonly string masterVolKey = "MasterVolume";
    public readonly string musicVolKey = "MusicVolume";
    public readonly string sfxVolKey = "SFXVolume";
    public readonly string screenShakeKey = "CameraShake";

    // Toggles
    public readonly string vSyncToggleKey = "VSyncToggle";
    public readonly string crowdToggleKey = "CrowdToggle";

    // Dropdowns
    public readonly string resolutionDropKey = "ResolutionDrop";
    public readonly string screenModeDropKey = "ScreenModeDrop";

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetPlayerPrefValues();

        // Left here for now, maybe move to its own dedicated script
        Cursor.lockState = CursorLockMode.Confined;
    }

    void SetPlayerPrefValues()
    {
        AudioManager.instance.SetMasterVolume(PlayerPrefs.GetFloat(masterVolKey, 0.75f));
        AudioManager.instance.SetSFXVolume(PlayerPrefs.GetFloat(sfxVolKey, 0.75f));
        AudioManager.instance.SetMusicVolume(PlayerPrefs.GetFloat(musicVolKey, 0.75f));
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(vSyncToggleKey, 0);

        // Apply screen mode first, then resolution using the saved mode.
        SetScreenMode();
        SetResolution();
    }

    void SetResolution()
    {
        int defaultResIndex = 0;

        if (!PlayerPrefs.HasKey(resolutionDropKey))
        {
            int i = 0;

            foreach (Resolution r in Screen.resolutions)
            {
                if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
                {
                    defaultResIndex = i;
                    break;
                }

                i++;
            }
        }

        int resIndex = Mathf.Clamp(PlayerPrefs.GetInt(resolutionDropKey, defaultResIndex), 0, Mathf.Max(0, Screen.resolutions.Length - 1));
        Resolution res = Screen.resolutions[resIndex];

        bool fullscreen = PlayerPrefs.GetInt(screenModeDropKey) == 0 || PlayerPrefs.GetInt(screenModeDropKey) == 2;
        Screen.SetResolution(res.width, res.height, fullscreen);
    }

    void SetScreenMode()
    {
        switch (PlayerPrefs.GetInt(screenModeDropKey, 0))
        {
            case 0: // Fullscreen
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 1: // Windowed
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 2: // Borderless
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
        }
    }
}