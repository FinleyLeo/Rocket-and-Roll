using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [SerializeField] AudioSource SFXSource, musicSource;
    [SerializeField] AudioMixer SFXMixer, musicMixer;
    [SerializeField] Clip[] SFX, music;

    public bool musicMuted, SFXMuted;

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
        // Initialize mute states using player prefs
        musicMuted = PlayerPrefs.GetInt("musicMuted", 0) == 1;
        SFXMuted = PlayerPrefs.GetInt("sfxMuted", 0) == 1;

        musicSource.mute = musicMuted;
        SFXSource.mute = SFXMuted;
    }

    #region sound-methods

    public void PlaySFX(string clipName, float volume)
    {
        AudioClip clip = null;

        for (int i = 0; i < SFX.Length; i++)
        {
            if (SFX[i].name == clipName)
            {
                if (SFX[i].clip == null)
                {
                    Debug.LogWarning("UI sound effect " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = SFX[i]?.clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Sound effect " + clipName + " does not exist");
            return;
        }

        SFXSource.pitch = 1;
        SFXSource.PlayOneShot(clip, volume);
    }
    public void PlaySFX(string clipName)
    {
        AudioClip clip = null;

        for (int i = 0; i < SFX.Length; i++)
        {
            if (SFX[i].name == clipName)
            {
                if (SFX[i].clip == null)
                {
                    Debug.LogWarning("UI sound effect " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = SFX[i]?.clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Sound effect " + clipName + " does not exist");
            return;
        }

        SFXSource.pitch = 1;
        SFXSource.PlayOneShot(clip);
    }
    public void PlayPitchedSFX(string clipName, float pitch)
    {
        AudioClip clip = null;

        for (int i = 0; i < SFX.Length; i++)
        {
            if (SFX[i].name == clipName)
            {
                if (SFX[i].clip == null)
                {
                    Debug.LogWarning("UI sound effect " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = SFX[i]?.clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Sound effect " + clipName + " does not exist");
            return;
        }

        SFXSource.pitch = 1 + pitch;
        SFXSource.PlayOneShot(clip);
    }
    public void PlayPitchedSFX(string clipName, float volume, float pitch)
    {

        AudioClip clip = null;

        for (int i = 0; i < SFX.Length; i++)
        {
            if (SFX[i].name == clipName)
            {
                if (SFX[i].clip == null)
                {
                    Debug.LogWarning("UI sound effect " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = SFX[i]?.clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Sound effect " + clipName + " does not exist");
            return;
        }

        SFXSource.pitch = 1 + pitch;

        SFXSource.PlayOneShot(clip, volume);
    }
    public void PlayMusic(string clipName, float volume)
    {
        AudioClip clip = null;

        for (int i = 0; i < music.Length; i++)
        {
            if (music[i].name == clipName)
            {
                if (music[i].clip == null)
                {
                    Debug.LogWarning("Music track " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = music[i]?.clip;

                if (clip == musicSource.clip) return;

                musicSource.clip = clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Music track " + clipName + " does not exist");
            return;
        }

        musicSource.volume = volume;
        musicSource.Play();
    }
    public void PlayMusic(string clipName)
    {
        AudioClip clip = null;

        for (int i = 0; i < music.Length; i++)
        {
            if (music[i].name == clipName)
            {
                if (music[i].clip == null)
                {
                    Debug.LogWarning("Music track " + clipName + " does not have an audioClip attached");
                    return;
                }

                clip = music[i]?.clip;

                if (clip == musicSource.clip) return;

                musicSource.clip = clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning("Music track " + clipName + " does not exist");
            return;
        }

        musicSource.Play();
    }

    public void SetMusicMuffle(float muffleStrength)
    {
        musicMixer.SetFloat("muffleAmount", muffleStrength);
    }

    #endregion

    public void ToggleMusic()
    {
        musicMuted = !musicMuted;
        musicSource.mute = musicMuted;

        PlayerPrefs.SetInt("musicMuted", musicMuted ? 1 : 0);
    }

    public void ToggleSFX()
    {
        SFXMuted = !SFXMuted;
        SFXSource.mute = SFXMuted;

        PlayerPrefs.SetInt("sfxMuted", SFXMuted ? 1 : 0);
    }
}