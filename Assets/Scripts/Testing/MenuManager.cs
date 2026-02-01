using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

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

    Lobby currentLobby;

    string playerId;

    async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            playerId = AuthenticationService.Instance.PlayerId;
            Debug.Log("Signed in " + playerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        playButton.onClick.AddListener(OpenPlayOption);
        //exitPlayerQueryButton.onClick.AddListener();

        createRoomButton.onClick.AddListener(CreateLobby);

        joinLobbyButton.onClick.AddListener(OpenLobbyList);
        exitLobbyListButton.onClick.AddListener(CloseLobbyList);

        joinLobbyWithCodeButton.onClick.AddListener(JoinLobbyWithCode);
        openRoomCodePanel.onClick.AddListener(OpenJoinWithCode);
        exitRoomCodePanel.onClick.AddListener(CloseJoinWithCode);

        openCreateLobbyButton.onClick.AddListener(OpenCreateLobbyMenu);
        exitCreateLobbyButton.onClick.AddListener(CloseCreateLobbyMenu);

        exitLobbyButton.onClick.AddListener(LeaveRoom);

        playerNameInput.onValueChanged.AddListener(delegate
        {
            PlayerPrefs.SetString("Username", playerNameInput.text);
        });

        playerNameInput.text = PlayerPrefs.GetString("Username");
    }

    void Update()
    {
        HandleLobbyActivityCheck();
        HandleRoomUpdate();
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
        lobbyListPanel.SetActive(true);
        playQueryPanel.SetActive(false);
        mainMenuPanel.GetComponent<CanvasGroup>().interactable = false;

        ListPublicLobbies();
    }

    void CloseLobbyList()
    {
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

    async void CreateLobby()
    {
        try
        {
            string lobbyName = roomNameInput.text;
            int.TryParse(maxPlayersInput.text, out int maxPlayers);
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivateToggle.isOn,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            EnterRoom();
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    void EnterRoom()
    {
        mainMenuPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        lobbyListPanel.SetActive(false);
        playQueryPanel.SetActive(false);
        roomCodePanel.SetActive(false);

        roomPanel.SetActive(true);
        roomName.text = currentLobby.Name;
        roomCode.text = currentLobby.LobbyCode;

        VisualiseRoomDetails();
    }

    float roomUpdateTimer = 2f;
    async void HandleRoomUpdate()
    {
        if (currentLobby != null)
        {
            roomUpdateTimer -= Time.deltaTime;

            if (roomUpdateTimer <= 0)
            {
                roomUpdateTimer = 2f;
                try
                {
                    if (IsInLobby())
                    {
                        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                        VisualiseRoomDetails();
                    }
                }
                catch (LobbyServiceException e)
                {
                    Debug.Log(e);
                    if (currentLobby.IsPrivate && e.Reason == LobbyExceptionReason.Forbidden || e.Reason == LobbyExceptionReason.LobbyNotFound)
                    {
                        currentLobby = null;
                        ExitRoom();
                    }
                }
            }
        }
    }

    bool IsInLobby()
    {
        if (currentLobby == null) return false;
        
        foreach (Player player in currentLobby.Players)
        {
            if (player.Id == playerId)
            {
                return true;
            }
        }
        currentLobby = null;
        return false;
    }

    void VisualiseRoomDetails()
    {
        // Clear previous player info
        for (int i = 0; i < playerInfoContent.transform.childCount; i++)
        {
            Destroy(playerInfoContent.transform.GetChild(i).gameObject);
        }

        if (IsInLobby())
        {
            foreach (Player player in currentLobby.Players)
            {
                GameObject newPLayerInfo = Instantiate(playerInfoPrefab, playerInfoContent.transform);
                newPLayerInfo.GetComponentInChildren<TextMeshProUGUI>().text = player.Data["PlayerName"].Value;
                if (IsHost() && player.Id != playerId)
                {
                    Button kickBtn = newPLayerInfo.GetComponentInChildren<Button>(true); // passing in true makes it referable while being inactive
                    kickBtn.onClick.AddListener(() => KickPlayer(player.Id));
                    kickBtn.gameObject.SetActive(true);
                }
            }

            if (IsHost())
            {
                startGameButton.onClick.AddListener(StartGame);
                startGameButton.gameObject.SetActive(true);
            }
            else
            {
                if (IsGameStarted())
                {
                    startGameButton.onClick.AddListener(EnterGame);
                    startGameButton.gameObject.SetActive(true);
                    startGameButton.GetComponentInChildren<TextMeshProUGUI>().text = "Enter Game";
                }
                else
                {
                    startGameButton.onClick.RemoveAllListeners();
                    startGameButton.gameObject.SetActive(false);
                }
            }
        }

        else
        {
            ExitRoom();
        }
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

    void VisualiseLobbyList(List<Lobby> publicLobbies)
    {
        // We need to clear previous info
        for (int i = 0; i < lobbyInfoContent.transform.childCount; i++)
        {
            Destroy(lobbyInfoContent.transform.GetChild(i).gameObject);
        }

        foreach (Lobby lobby in publicLobbies)
        {
            GameObject newLobbyInfo = Instantiate(lobbyInfoPrefab, lobbyInfoContent.transform);
            var lobbyinfoTMPs = newLobbyInfo.GetComponentsInChildren<TextMeshProUGUI>();

            lobbyinfoTMPs[0].text = lobby.Name;
            lobbyinfoTMPs[1].text = (lobby.MaxPlayers - lobby.AvailableSlots) + "/" + lobby.MaxPlayers; // use maxplayers - available slots to find player count

            newLobbyInfo.GetComponent<Button>().onClick.AddListener(()=>JoinLobby(lobby.Id)); // call join lobby
        }
    }

    async void JoinLobby(string lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            EnterRoom();
            Debug.Log("Player in room: " + currentLobby.Players.Count);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    async void JoinLobbyWithCode()
    {
        string lobbyCode = roomCodeInput.text;
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            EnterRoom();
            Debug.Log("Player in room: " + currentLobby.Players.Count);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    float activityTimer = 15f;
    async void HandleLobbyActivityCheck()
    {
        if (currentLobby != null && IsHost())
        {
            activityTimer -= Time.deltaTime;

            if (activityTimer <= 0)
            {
                activityTimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    bool IsHost()
    {
        if (currentLobby != null && currentLobby.HostId == playerId)
        {
            return true;
        }
        return false;
    }

    private Player GetPlayer()
    {
        string playerName = PlayerPrefs.GetString("Username");

        if (playerName == null || playerName == "")
        {
            playerName = playerId;
        }

        Player player = new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
            }
        };

        return player;
    }

    async void LeaveRoom()
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
            ExitRoom();
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    void ExitRoom()
    {
        mainMenuPanel.SetActive(true);

        lobbyListPanel.SetActive(true);
        lobbyListPanel.GetComponent<CanvasGroup>().interactable = true;

        createRoomPanel.SetActive(false);
        roomPanel.SetActive(false);
        roomCodePanel.SetActive(false);
    }

    async void KickPlayer(string _playerId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, _playerId);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    async void StartGame()
    {
        if (currentLobby != null && IsHost())
        {
            try
            {
                UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                };

                currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, updateOptions);

                EnterGame();
            }
            catch(LobbyServiceException e)
            {
                Debug.Log(e);
            }
           
        }
    }

    bool IsGameStarted()
    {
        if (currentLobby != null)
        {
            if (currentLobby.Data["IsGameStarted"].Value == "true")
            {
                return true;
            }
        }

        return false;
    }

    void EnterGame()
    {
        SceneManager.LoadScene("Testing");
    }
}