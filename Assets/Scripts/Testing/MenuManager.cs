using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class MenuManager : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] Button playButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button quitButton;

    [Space(10)]
    [Header("Settings Menu")]
    [SerializeField] GameObject settingsPanel;

    [Space(10)]
    [Header("Player Query")]
    [SerializeField] GameObject playQueryPanel;
    [SerializeField] Button openCreateLobbyButton;
    [SerializeField] Button joinLobbyButton;

    [Space(10)]
    [Header("Create Room Panel")]
    [SerializeField] GameObject createRoomPanel;
    [SerializeField] TMP_InputField roomNameInput;
    [SerializeField] TMP_InputField maxPlayersInput;
    [SerializeField] Button createRoomButton;

    [Space(10)]
    [Header("Room Panel")]
    [SerializeField] GameObject roomPanel;
    [SerializeField] TextMeshProUGUI roomName;
    [SerializeField] TextMeshProUGUI roomCode;

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

        createRoomButton.onClick.AddListener(CreateLobby);
        joinLobbyButton.onClick.AddListener(ListPublicLobbies);
        playButton.onClick.AddListener(OpenPlayOption);
        openCreateLobbyButton.onClick.AddListener(OpenCreateLobbyMenu);
    }

    void Update()
    {
        HandleLobbyActivityCheck();
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

    void OpenCreateLobbyMenu()
    {
        playQueryPanel.SetActive(false);
        createRoomPanel.SetActive(true);
    }

    async void CreateLobby()
    {
        try
        {
            string lobbyName = roomNameInput.text;
            int.TryParse(maxPlayersInput.text, out int maxPlayers);
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);
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

        roomPanel.SetActive(true);
        roomName.text = currentLobby.Name;
        roomCode.text = currentLobby.LobbyCode;
    }

    async void ListPublicLobbies()
    {
        try
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log("Available public lobbies: " + response.Results.Count);

            foreach (Lobby lobby in response.Results)
            {
                Debug.Log("Lobby Name:" + lobby.Name + ", Lobby ID: " + lobby.Id);
            }
        }
        catch(LobbyServiceException e)
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
}
