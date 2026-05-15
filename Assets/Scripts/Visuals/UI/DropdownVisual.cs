using TMPro;
using UnityEngine;

enum DropdownType
{
    Resolution,
    ScreenMode
}

public class DropdownVisual : MonoBehaviour
{
    TMP_Dropdown dropdown;

    [SerializeField] DropdownType dropdownType;

    int defaultResIndex = 0;

    readonly string resolutionKey = "Resolution";
    readonly string screenModeKey = "ScreenMode";

    private void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        if (dropdownType == DropdownType.Resolution)
        {
            SetUpResolutions();
        }

        SetValues();

        dropdown.onValueChanged.AddListener(OnChanged);
        OnChanged(dropdown.value);
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
        SetPlayerPrefs();
        SetValues();
    }

    void SetPlayerPrefs()
    {
        switch (dropdownType)
        {
            case DropdownType.Resolution:
                PlayerPrefs.SetInt(resolutionKey, dropdown.value);
                break;

            case DropdownType.ScreenMode:
                PlayerPrefs.SetInt(screenModeKey, dropdown.value);
                break;
        }
    }

    void SetValues()
    {
        switch (dropdownType)
        {
            case DropdownType.Resolution:
                dropdown.value = PlayerPrefs.GetInt(resolutionKey, defaultResIndex);

                Resolution r = Screen.resolutions[dropdown.value];
                Screen.SetResolution(r.width, r.height, Screen.fullScreen);
                break;

            case DropdownType.ScreenMode:
                dropdown.value = PlayerPrefs.GetInt(screenModeKey, 0);

                switch (dropdown.value)
                {
                    case 0:
                        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                        break;
                    case 1:
                        Screen.fullScreenMode = FullScreenMode.Windowed;
                        break;
                    case 2:
                        Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
                        break;
                }
                break;
        }
    }
}
