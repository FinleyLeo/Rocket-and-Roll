using System.Globalization;
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

    [SerializeField] GameObject pauseMenuPanel;
    [SerializeField] GameObject settingsMenuPanel;

    [SerializeField] Button closePausebutton;

    [SerializeField] Button quitToMenuButton;
    [SerializeField] Button quitGameButton;

    [SerializeField] Button openSettingsButton;
    [SerializeField] Button closeSettingsButton;

    [SerializeField] Button openCustomisationButton;
    [SerializeField] Button closeCustomisationButton;

    [SerializeField] GameObject playerInfoContent;
    [SerializeField] GameObject playerInfoPrefab;

    [SerializeField] Material buttonBannerMat;
    [SerializeField] Material buttonHighlightMat;

    Animator anim;

    string playerName;

    float fillAmount;

    public bool isPaused;
    bool animEnded;

    private void Start()
    {
        if (IsOwner)
        {
            playerName = PlayerPrefs.GetString("Username", "Player " + NetworkObjectId);
        }

        instance = this;

        anim = GetComponent<Animator>();

        closePausebutton.onClick.AddListener(ResumeGame);
        quitToMenuButton.onClick.AddListener(QuitToMenu);
        //openSettingsButton.onClick.AddListener(OpenSettings);
        //closeSettingsButton.onClick.AddListener(CloseSettings);

        buttonBannerMat.SetFloat("_FillAmount", 0);
        buttonHighlightMat.SetFloat("_FillAmount", 0);
        anim.SetBool("isOpen", isPaused);
    }

    private void Update()
    {
        if (animEnded)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                animEnded = false;
                isPaused = !isPaused;

                anim.SetBool("isOpen", isPaused);
            }
        }

        FadeInOutBanner();
        //HandlePlayerListUpdate();

        Debug.Log("isOpen bool:" + anim.GetBool("isOpen"));
        Debug.Log(isPaused);
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

        fillAmount = Mathf.Clamp(fillAmount, -0.3f, 1.05f);
        buttonBannerMat.SetFloat("_FillAmount", fillAmount);
        buttonHighlightMat.SetFloat("_FillAmount", fillAmount);
    }

    // Tells script when animation has finished updating through event to respond accordingly
    public void NotifyEnd()
    {
        animEnded = true;
    }

    void ResumeGame()
    {
        if (animEnded)
        {
            animEnded = false;
            isPaused = false;

            anim.SetBool("isOpen", isPaused);
        }
    }

    void QuitToMenu()
    {
        LobbyManager.Instance.LeaveLobby();
        SceneManager.LoadScene("Main Menu");
    }

    void OpenSettings()
    {
        pauseMenuPanel.GetComponent<CanvasGroup>().interactable = false;
        settingsMenuPanel.SetActive(true);
    }
    void CloseSettings()
    {
        settingsMenuPanel.SetActive(false);
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
}
