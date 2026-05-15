using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LogoVisuals : MonoBehaviour
{
    bool canStartGame, gameStarting;
    bool visualsStarted;

    [SerializeField] Transform logoCircle;
    Image circleSr;

    Animator logoAnim;
    [SerializeField] ParticleSystem startParticles;
    [SerializeField] TextMeshProUGUI startText;

    Vector3 rootCircleScale;

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

        StartCoroutine(StartDelay());

        TransitionManager.Instance.EndTransition();
    }

    private void Update()
    {
        if (canStartGame && !gameStarting)
        {
            if (Keyboard.current.anyKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
            {
                gameStarting = true;

                TransitionManager.Instance.LoadScene("Main Menu");
            }
        }

        CircleVisualiser();
    }

    void CircleVisualiser()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.musicSource.clip.GetData(clipSampleData, AudioManager.instance.musicSource.timeSamples); //I read 1024 samples, which is about 80 ms on a 44khz stereo clip, beginning at the current sample position of the clip.

            clipLoudness = 0f;

            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }

            clipLoudness /= sampleDataLength;

            logoCircle.localScale = Vector3.Lerp(logoCircle.localScale, new Vector3(clipLoudness + rootCircleScale.x, clipLoudness + rootCircleScale.x), Time.deltaTime * 5);
        }
    }

    IEnumerator StartDelay()
    {
        AudioManager.instance.PlayMusic("TitleStart");

        yield return new WaitForSeconds(0.1f);

        circleSr = logoCircle.GetComponent<Image>();
        Color palette = ColourChangeManager.Instance.selectedPalette.backgroundPrimary;
        Color.RGBToHSV(palette, out float h, out float s, out _);

        Color newCircleColour = Color.HSVToRGB(h, s, 1);

        circleSr.color = newCircleColour;

        startText.color = ColourChangeManager.Instance.selectedPalette.foregroundPrimary;

        yield return new WaitForSeconds(2f);

        startParticles.Play();

        yield return new WaitForSeconds(0.8f);

        logoAnim.Play("StartLogo");
        canStartGame = true;
    }
}
