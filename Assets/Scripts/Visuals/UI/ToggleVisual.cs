using UnityEngine;
using UnityEngine.UI;

enum ToggleType
{
    Vsync,
}

public class ToggleVisual : MonoBehaviour
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
                PlayerPrefs.SetInt(SaveDataManager.instance.vSyncKey, toggle.isOn ? 1 : 0);
                break;
        }
    }

    void GetPlayerPrefs()
    {
        switch (toggleType)
        {
            case ToggleType.Vsync:
                toggle.isOn = PlayerPrefs.GetInt(SaveDataManager.instance.vSyncKey, 0) == 1;
                break;
        }
    }
}
