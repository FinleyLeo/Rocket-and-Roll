using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuScript : NetworkBehaviour
{
    public static PauseMenuScript instance;

    [SerializeField] GameObject pauseMenu;
    [SerializeField] GameObject pauseMenuPanel;
    [SerializeField] Button closePausebutton;

    [SerializeField] Button quitToMenuButton;
    [SerializeField] Button quitGameButton;


    [SerializeField] Button openCustomisationButton;
    [SerializeField] Button closeCustomisationButton;

    [SerializeField] GameObject playerInfoContent;
    [SerializeField] GameObject playerInfoPrefab;

    [SerializeField] Material buttonBannerMat;
    [SerializeField] Material buttonHighlightMat;

    [Space(10)]
    [Header("Settings Menu")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Button openSettingsButton;
    [SerializeField] Button exitSettingsButton;
    [SerializeField] GameObject accessibilityPanel, audioPanel, videoPanel, controlPanel;
    [SerializeField] Button accessibilityTab, audioTab, videoTab, controlTab;

    public bool isPaused;
    float fillAmount;

    private void Start()
    {
        instance = this;

        closePausebutton.onClick.AddListener(ResumeGame);

        openSettingsButton.onClick.AddListener(OpenSettings);
        exitSettingsButton.onClick.AddListener(CloseSettings);

        quitToMenuButton.onClick.AddListener(QuitToMenu);
        quitGameButton.onClick.AddListener(QuitGame);


        accessibilityTab.onClick.AddListener(delegate { SwitchTab(0); });
        audioTab.onClick.AddListener(delegate { SwitchTab(1); });
        videoTab.onClick.AddListener(delegate { SwitchTab(2); });
        controlTab.onClick.AddListener(delegate { SwitchTab(3); });
    }

    private void Update()
    {
        if (!settingsPanel.activeSelf)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                isPaused = !isPaused;

                pauseMenu.SetActive(isPaused);
            }
        }
        FadeInOutBanner();
        //HandlePlayerListUpdate();
    }

    void FadeInOutBanner()
    {
        fillAmount = buttonBannerMat.GetFloat("_FillAmount");

        if (isPaused)
        {
            fillAmount += Time.deltaTime * 1.5f;
        }
        else
        {
            fillAmount -= Time.deltaTime * 1f;
        }

        fillAmount = Mathf.Clamp(fillAmount, -0.35f, 1.05f);
        buttonBannerMat.SetFloat("_FillAmount", fillAmount);
        buttonHighlightMat.SetFloat("_FillAmount", fillAmount);
    }
    void ResumeGame()
    {
        isPaused = false;
        settingsPanel.SetActive(false);
        pauseMenu.SetActive(false);
    }

    void QuitToMenu()
    {
        LobbyManager.Instance.LeaveLobby();

        // Used to keep pause menu active until scene loaded
        StartCoroutine(DelayLeave());
    }

    void QuitGame()
    {
        Application.Quit();
    }

    IEnumerator DelayLeave()
    {
        TransitionManager.Instance.StartTransitionManually();

        while (TransitionManager.Instance.fillAmount < 1)
        {
            yield return null;
        }

        SceneManager.LoadScene("Main Menu");
    }

    void OpenSettings()
    {
        pauseMenuPanel.GetComponent<CanvasGroup>().interactable = false;
        settingsPanel.SetActive(true);
    }
    void CloseSettings()
    {
        settingsPanel.SetActive(false);
        pauseMenuPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    float updateTimer = 2f;
    void HandlePlayerListUpdate()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0)
        {
            VisualisePlayerList();
            updateTimer = 2f;
        }
    }
    void VisualisePlayerList()
    {
        // Clear previous player list before adding new
        if (playerInfoContent != null && playerInfoContent.transform.childCount > 0)
        {
            for (int i = 0; i < playerInfoContent.transform.childCount; i++)
            {
                Destroy(playerInfoContent.transform.GetChild(i).gameObject);
            }
        }

        foreach (Player player in LobbyManager.Instance.currentLobby.Players)
        {
            GameObject newPlayerInfo = Instantiate(playerInfoPrefab, playerInfoContent.transform);
            var playerinfoTMPs = newPlayerInfo.GetComponentsInChildren<TextMeshProUGUI>();

            playerinfoTMPs[0].text = player.Data["Username"].Value;

            if (LobbyManager.Instance.currentLobby.HostId != LobbyManager.Instance.playerId)
            {
                newPlayerInfo.GetComponent<Button>().onClick.AddListener(() =>
                {
                    LobbyManager.Instance.KickPlayer(player.Id);
                });
            }
        }
    }

    void SwitchTab(int tabIndex)
    {
        switch (tabIndex)
        {
            case 0: // accessobility
                accessibilityPanel.SetActive(true);
                audioPanel.SetActive(false);
                videoPanel.SetActive(false);
                controlPanel.SetActive(false);
                break;
            case 1: // audio
                videoPanel.SetActive(false);
                audioPanel.SetActive(true);
                accessibilityPanel.SetActive(false);
                controlPanel.SetActive(false);
                break;
            case 2: // video
                videoPanel.SetActive(true);
                accessibilityPanel.SetActive(false);
                audioPanel.SetActive(false);
                controlPanel.SetActive(false);
                break;
            case 3: // control
                controlPanel.SetActive(true);
                accessibilityPanel.SetActive(false);
                audioPanel.SetActive(false);
                videoPanel.SetActive(false);
                break;
        }
    }
}
