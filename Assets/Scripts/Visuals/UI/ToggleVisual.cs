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

    readonly string vSyncKey = "Vsync";

    private void Start()
    {
        toggle = GetComponent<Toggle>();
        toggleImage = GetComponent<Image>();

        SetValues();

        toggle.onValueChanged.AddListener(OnToggle);
        OnToggle(toggle.isOn);
    }

    void OnToggle(bool isOn)
    {
        if (isOn)
        {
            toggleImage.sprite = OnSprite;
        }
        else
        {
            toggleImage.sprite = OffSprite;
        }

        SetPlayerPrefs();
        SetValues();
    }

    void SetPlayerPrefs()
    {
        switch (toggleType)
        {
            case ToggleType.Vsync:
                PlayerPrefs.SetInt(vSyncKey, toggle.isOn ? 1 : 0);
                break;
        }
    }

    void SetValues()
    {
        switch (toggleType)
        {
            case ToggleType.Vsync:
                toggle.isOn = PlayerPrefs.GetInt(vSyncKey, toggle.isOn ? 1 : 0) == 1;
                QualitySettings.vSyncCount = toggle.isOn ? 1 : 0;
                break;
        }
    }
}
