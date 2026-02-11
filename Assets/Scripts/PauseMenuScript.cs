using System.Globalization;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuScript : NetworkBehaviour
{
    [SerializeField] GameObject pauseMenuPanel;
    [SerializeField] GameObject settingsMenuPanel;
    [SerializeField] Button quitButton;
    [SerializeField] Button closePausebutton;
    [SerializeField] Button openSettingsButton;
    [SerializeField] Button closeSettingsButton;
    [SerializeField] GameObject playerInfoContent;
    [SerializeField] GameObject playerInfoPrefab;

    string playerName;

    private void Start()
    {
        if (IsOwner)
        {
            playerName = PlayerPrefs.GetString("Username", "Player " + NetworkObjectId);
        }

        closePausebutton.onClick.AddListener(ExitPauseMenu);
        quitButton.onClick.AddListener(QuitToMenu);
        openSettingsButton.onClick.AddListener(OpenSettings);
        closeSettingsButton.onClick.AddListener(CloseSettings);
    }

    private void Update()
    {
        HandlePlayerListUpdate();
    }

    void ExitPauseMenu()
    {
        pauseMenuPanel.SetActive(false);
    }

    void QuitToMenu()
    {
        LobbyManager.Instance.LeaveLobby();
        Debug.Log("Left lobby");
        // switch scenes if needed, test first
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
