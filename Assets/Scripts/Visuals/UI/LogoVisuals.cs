using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LogoVisuals : MonoBehaviour
{
    bool canStartGame, gameStarting;

    [SerializeField] Transform logoCircle;
    [SerializeField] Transform logoTitle;
    Image circleSr;

    Animator logoAnim;
    [SerializeField] ParticleSystem startParticles;
    [SerializeField] TextMeshProUGUI startText;

    Vector3 rootCircleScale;
    Vector3 rootTitleScale;

    public int sampleDataLength = 1024;
    private float clipLoudness;
    private float[] clipSampleData;

    void Awake()
    {
        clipSampleData = new float[sampleDataLength];
        logoAnim = GetComponent<Animator>();
    }

    private void Start()
    {
        rootCircleScale = logoCircle.localScale;
        rootTitleScale = logoTitle.localScale;

        StartCoroutine(StartDelay());

        TransitionManager.Instance.EndTransition();
    }

    private void Update()
    {
        if (canStartGame)
        {
            if (!gameStarting)
            {
                if (Keyboard.current.anyKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
                {
                    gameStarting = true;

                    TransitionManager.Instance.LoadScene("Main Menu");
                }
            }

            TitleVisuals();
        }

        CircleVisuals();
    }

    IEnumerator StartDelay()
    {
        AudioManager.instance.PlayMusic("TitleStart");

        yield return new WaitForSeconds(0.2f);

        circleSr = logoCircle.GetComponent<Image>();
        Color palette = ColourChangeManager.Instance.selectedPalette.backgroundPrimary;
        Color.RGBToHSV(palette, out float h, out float s, out float v);

        Color newCircleColour = Color.HSVToRGB(h, s, v + 0.2f);

        circleSr.color = newCircleColour;

        startText.color = ColourChangeManager.Instance.selectedPalette.foregroundSecondary;

        yield return new WaitForSeconds(3f);

        startParticles.Play();

        yield return new WaitForSeconds(1f);

        logoAnim.Play("StartLogo");
        canStartGame = true;
    }

    void CircleVisuals()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.musicSource.clip.GetData(clipSampleData, AudioManager.instance.musicSource.timeSamples);

            clipLoudness = 0f;

            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }

            clipLoudness /= sampleDataLength;

            logoCircle.localScale = Vector3.Lerp(logoCircle.localScale, new Vector3(clipLoudness + rootCircleScale.x, clipLoudness + rootCircleScale.x), Time.deltaTime * 5);
        }
    }

    void TitleVisuals()
    {
        float offset = -0.4f;
        float sinValue = Mathf.Sin(Time.time * 12.25f) + offset;

        if (sinValue > rootTitleScale.x)
        {
            logoTitle.localScale = new Vector3(sinValue, sinValue);
        }
    }
}