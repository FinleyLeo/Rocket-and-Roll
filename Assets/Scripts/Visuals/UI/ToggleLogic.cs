using UnityEngine;
using UnityEngine.UI;

enum ToggleType
{
    Vsync,
    Crowd
}

public class ToggleLogic : MonoBehaviour
{
    [SerializeField] Sprite OnSprite, OffSprite;
    Image toggleImage;
    Toggle toggle;

    [SerializeField] ToggleType toggleType;

    bool isInitializing;

    private void Start()
    {
        toggle = GetComponent<Toggle>();
        toggleImage = GetComponent<Image>();

        isInitializing = true;
        GetPlayerPrefs();
        toggle.onValueChanged.AddListener(OnToggle);
        isInitializing = false;

        UpdateSprite(toggle.isOn);
    }

    void OnToggle(bool isOn)
    {
        UpdateSprite(isOn);

        if (isInitializing) return;

        SetPlayerPrefs();

        switch (toggleType)
        {
            case ToggleType.Vsync:
                QualitySettings.vSyncCount = isOn ? 1 : 0;
                break;
        }
    }

    void UpdateSprite(bool isOn)
    {
        toggleImage.sprite = isOn ? OnSprite : OffSprite;
    }

    void SetPlayerPrefs()
    {
        switch (toggleType)
        {
            case ToggleType.Vsync:
                PlayerPrefs.SetInt(SaveDataManager.instance.vSyncToggleKey, toggle.isOn ? 1 : 0);
                break;
            case ToggleType.Crowd:
                PlayerPrefs.SetInt(SaveDataManager.instance.crowdToggleKey, toggle.isOn ? 1 : 0);
                break;
        }
    }

    void GetPlayerPrefs()
    {
        switch (toggleType)
        {
            case ToggleType.Vsync:
                toggle.isOn = PlayerPrefs.GetInt(SaveDataManager.instance.vSyncToggleKey, 1) == 1;
                break;
            case ToggleType.Crowd:
                toggle.isOn = PlayerPrefs.GetInt(SaveDataManager.instance.crowdToggleKey, 1) == 1;
                break;
        }
    }
}
