using TMPro;
using UnityEngine;
using UnityEngine.UI;

enum SliderType
{
    MusicVolume,
    SFXVolume,
    MasterVolume,
    ScreenShake,
}

public class SliderLogic : MonoBehaviour
{
    // copy format of dropdown and toggle
    [SerializeField] SliderType sliderType;
    [SerializeField] TextMeshProUGUI percentText;

    Slider slider;
    bool isInitializing;

    private void Start()
    {
        slider = GetComponent<Slider>();

        isInitializing = true;
        GetPlayerPrefs();
        slider.onValueChanged.AddListener(OnChanged);
        isInitializing = false;

        // update percent text for initial visual
        UpdatePercent(slider.value);
    }

    void OnChanged(float value)
    {        
        UpdatePercent(value);

        if (isInitializing) return;

        SetPlayerPrefs();

        switch (sliderType)
        {
            case SliderType.MasterVolume:
                AudioManager.instance.SetMasterVolume(slider.value);
                break;
            case SliderType.MusicVolume:
                AudioManager.instance.SetMusicVolume(slider.value);
                break;
            case SliderType.SFXVolume:
                AudioManager.instance.SetSFXVolume(slider.value);
                break;
        }
    }

    void UpdatePercent(float value)
    {
        percentText.text = Mathf.RoundToInt(value * 100) + "%";
    }

    void SetPlayerPrefs()
    {
        switch (sliderType)
        {
            case SliderType.MasterVolume:
                PlayerPrefs.SetFloat(SaveDataManager.instance.masterVolKey, slider.value);
                break;
            case SliderType.MusicVolume:
                PlayerPrefs.SetFloat(SaveDataManager.instance.musicVolKey, slider.value);
                break;
            case SliderType.SFXVolume:
                PlayerPrefs.SetFloat(SaveDataManager.instance.sfxVolKey, slider.value);
                break;
            case SliderType.ScreenShake:
                PlayerPrefs.SetFloat(SaveDataManager.instance.screenShakeKey, slider.value);
                break;
        }
    }

    void GetPlayerPrefs()
    {
        switch (sliderType)
        {
            case SliderType.MasterVolume:
                slider.value = PlayerPrefs.GetFloat(SaveDataManager.instance.masterVolKey, slider.value);
                break;

            case SliderType.SFXVolume:
                slider.value = PlayerPrefs.GetFloat(SaveDataManager.instance.sfxVolKey, slider.value);
                break;

            case SliderType.MusicVolume:
                slider.value = PlayerPrefs.GetFloat(SaveDataManager.instance.musicVolKey, slider.value);
                break;

            case SliderType.ScreenShake:
                slider.value = PlayerPrefs.GetFloat(SaveDataManager.instance.screenShakeKey, slider.value);
                break;
        }
    }
}
