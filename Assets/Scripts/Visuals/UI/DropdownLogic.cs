using TMPro;
using UnityEngine;

enum DropdownType
{
    Resolution,
    ScreenMode
}

public class DropdownLogic : MonoBehaviour
{
    TMP_Dropdown dropdown;

    [SerializeField] DropdownType dropdownType;

    int defaultResIndex = 0;
    bool isInitializing;

    private void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        isInitializing = true;

        if (dropdownType == DropdownType.Resolution)
        {
            SetUpResolutions();
        }

        GetPlayerPrefs();
        dropdown.onValueChanged.AddListener(OnChanged);

        isInitializing = false;
    }

    void SetUpResolutions()
    {
        dropdown.options.Clear();

        int i = 0;

        foreach (Resolution r in Screen.resolutions)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData($"{r.width} x {r.height}"));

            if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
            {
                defaultResIndex = i;
            }

            i++;
        }
    }

    void OnChanged(int value)
    {
        if (isInitializing) return;

        SetPlayerPrefs();

        switch (dropdownType)
        {
            case DropdownType.Resolution:
                int resIndex = value;
                var res = Screen.resolutions[Mathf.Clamp(resIndex, 0, Screen.resolutions.Length - 1)];
                bool fullscreen = PlayerPrefs.GetInt(SaveDataManager.instance.screenModeDropKey, 0) == 0;
                Screen.SetResolution(res.width, res.height, fullscreen);
                break;

            case DropdownType.ScreenMode:
                int mode = value;
                switch (mode)
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
                break;
        }

        GetPlayerPrefs();
    }

    void SetPlayerPrefs()
    {
        switch (dropdownType)
        {
            case DropdownType.Resolution:
                PlayerPrefs.SetInt(SaveDataManager.instance.resolutionDropKey, dropdown.value);
                break;

            case DropdownType.ScreenMode:
                PlayerPrefs.SetInt(SaveDataManager.instance.screenModeDropKey, dropdown.value);
                break;
        }
    }

    void GetPlayerPrefs()
    {
        switch (dropdownType)
        {
            case DropdownType.Resolution:
                dropdown.value = PlayerPrefs.GetInt(SaveDataManager.instance.resolutionDropKey, defaultResIndex);
                break;

            case DropdownType.ScreenMode:
                dropdown.value = PlayerPrefs.GetInt(SaveDataManager.instance.screenModeDropKey, 0);
                break;
        }
    }
}
