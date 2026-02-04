using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] Button playButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button quitButton;
    [SerializeField] TMP_InputField playerNameInput;

    [Space(10)]
    [Header("Settings Menu")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Button exitSettingsButton;

    [Space(10)]
    [Header("Player Query")]
    [SerializeField] GameObject playQueryPanel;
    [SerializeField] Button openCreateLobbyButton;
    [SerializeField] Button exitPlayerQueryButton;
    [SerializeField] Button joinLobbyButton;

    [Space(10)]
    [Header("Lobby List")]
    [SerializeField] GameObject lobbyListPanel;
    [SerializeField] Button exitLobbyListButton;
    [SerializeField] GameObject lobbyInfoPrefab;
    [SerializeField] GameObject lobbyInfoContent;
    bool lobbyListOpen;

    [Space(10)]
    [Header("Create Room Panel")]
    [SerializeField] GameObject createRoomPanel;
    [SerializeField] TMP_InputField roomNameInput;
    [SerializeField] TMP_InputField maxPlayersInput;
    [SerializeField] Button exitCreateLobbyButton;
    [SerializeField] Button createRoomButton;
    [SerializeField] Toggle isPrivateToggle;

    [Space(10)]
    [Header("Room Panel")]
    [SerializeField] GameObject roomPanel;
    [SerializeField] Button exitLobbyButton;
    [SerializeField] Button startGameButton;
    [SerializeField] TextMeshProUGUI roomName;
    [SerializeField] TextMeshProUGUI roomCode;
    [SerializeField] GameObject playerInfoPrefab;
    [SerializeField] GameObject playerInfoContent;

    [Space(10)]
    [Header("Join Room With Code")]
    [SerializeField] GameObject roomCodePanel;
    [SerializeField] TMP_InputField roomCodeInput;
    [SerializeField] Button exitRoomCodePanel;
    [SerializeField] Button openRoomCodePanel;
    [SerializeField] Button joinLobbyWithCodeButton;

    void Start()
    {
        playButton.onClick.AddListener(OpenPlayOption);
        //exitPlayerQueryButton.onClick.AddListener();

        createRoomButton.onClick.AddListener(CreateLobby);

        joinLobbyButton.onClick.AddListener(OpenLobbyList);
        exitLobbyListButton.onClick.AddListener(CloseLobbyList);

        joinLobbyWithCodeButton.onClick.AddListener(JoinWithCode);
        openRoomCodePanel.onClick.AddListener(OpenJoinWithCode);
        exitRoomCodePanel.onClick.AddListener(CloseJoinWithCode);

        openCreateLobbyButton.onClick.AddListener(OpenCreateLobbyMenu);
        exitCreateLobbyButton.onClick.AddListener(CloseCreateLobbyMenu);

        //exitLobbyButton.onClick.AddListener(ExitRoom);

        playerNameInput.onValueChanged.AddListener(delegate
        {
            if (playerNameInput.text == "")
            {
                PlayerPrefs.SetString("Username", LobbyManager.Instance.playerId);
                return;
            }

            PlayerPrefs.SetString("Username", playerNameInput.text);
        });

        playerNameInput.text = PlayerPrefs.GetString("Username");
    }

    void Update()
    {
        if (lobbyListOpen)
        {
            HandleLobbiesListUpdate();
        }
    }

    void OpenPlayOption()
    {
        playQueryPanel.SetActive(true);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = false;
    }

    void ExitPlayOption()
    {
        playQueryPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    void OpenSettings()
    {
        settingsPanel.SetActive(true);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = false;
    }

    void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    void OpenLobbyList()
    {
        lobbyListOpen = true;

        lobbyListPanel.SetActive(true);
        playQueryPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = false;

        ListPublicLobbies();
    }

    void CloseLobbyList()
    {
        lobbyListOpen = false;

        lobbyListPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    void OpenCreateLobbyMenu()
    {
        playQueryPanel.SetActive(false);
        createRoomPanel.SetActive(true);
    }

    void CloseCreateLobbyMenu()
    {
        createRoomPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    void OpenJoinWithCode()
    {
        roomCodePanel.SetActive(true);
        lobbyListPanel.GetComponent<CanvasGroup>().interactable = false;
    }

    void CloseJoinWithCode()
    {
        roomCodePanel.SetActive(false);
        lobbyListPanel.GetComponent<CanvasGroup>().interactable = true;
    }

    void CreateLobby()
    {
        string lobbyName = roomNameInput.text;
        int.TryParse(maxPlayersInput.text, out int maxPlayers);
        LobbyManager.Instance.CreateLobby(isPrivateToggle.isOn, maxPlayers, lobbyName);
    }

    void JoinWithCode()
    {
        LobbyManager.Instance.JoinLobbyWithCode(roomCodeInput.text);
    }

    async void ListPublicLobbies()
    {
        try
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            VisualiseLobbyList(response.Results);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    float updateLobbiesListTimer = 2f;

    void HandleLobbiesListUpdate()
    {
        updateLobbiesListTimer -= Time.deltaTime;
        if (updateLobbiesListTimer <= 0)
        {
            ListPublicLobbies();
            updateLobbiesListTimer = 2f;
        }
    }

    void VisualiseLobbyList(List<Lobby> publicLobbies)
    {
        // We need to clear previous info
        if (lobbyInfoContent.transform.childCount > 0)
        {
            for (int i = 0; i < lobbyInfoContent.transform.childCount; i++)
            {
                Destroy(lobbyInfoContent.transform.GetChild(i).gameObject);
            }
        }

        foreach (Lobby lobby in publicLobbies)
        {
            GameObject newLobbyInfo = Instantiate(lobbyInfoPrefab, lobbyInfoContent.transform);
            var lobbyinfoTMPs = newLobbyInfo.GetComponentsInChildren<TextMeshProUGUI>();

            lobbyinfoTMPs[0].text = lobby.Name;
            lobbyinfoTMPs[1].text = (lobby.MaxPlayers - lobby.AvailableSlots) + "/" + lobby.MaxPlayers; // use maxplayers - available slots to find player count

            newLobbyInfo.GetComponent<Button>().onClick.AddListener(()=>LobbyManager.Instance.JoinLobby(lobby.Id)); // call join lobby
        }
    }
}