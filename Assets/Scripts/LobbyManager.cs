using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [HideInInspector] public Lobby currentLobby;
    [HideInInspector] public string playerId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        AuthenticatePlayer();
    }

    void Update()
    {
        HandleLobbyActivityCheck();
        HandleRoomUpdate();
    }

    public async void AuthenticatePlayer()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            playerId = AuthenticationService.Instance.PlayerId;
            Debug.Log("Signed in " + playerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public Player GetPlayer()
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

    public async void CreateLobby(bool isPrivate, int maxPlayers, string lobbyName)
    {
        try
        {
            //string lobbyName = roomNameInput.text;
            //int.TryParse(maxPlayersInput.text, out int maxPlayers);
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            NetworkManager.Singleton.StartHost();
            //EnterRoom();
            OpenLobbyScene();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public bool IsInLobby()
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

    public bool IsLobbyHost()
    {
        if (currentLobby != null && currentLobby.HostId == playerId)
        {
            return true;
        }
        return false;
    }

    public bool IsGameStarted()
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

    public async void JoinLobby(string lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            //EnterRoom();
            NetworkManager.Singleton.StartClient();
            Debug.Log("Player in room: " + currentLobby.Players.Count);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void JoinLobbyWithCode(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            //EnterRoom();
            NetworkManager.Singleton.StartClient();
            Debug.Log("Player in room: " + currentLobby.Players.Count);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void LeaveLobby()
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void KickPlayer(string _playerId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, _playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    float activityTimer = 15f;
    async void HandleLobbyActivityCheck()
    {
        if (currentLobby != null && IsLobbyHost())
        {
            activityTimer -= Time.deltaTime;

            if (activityTimer <= 0)
            {
                activityTimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
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
                        //VisualiseRoomDetails();
                    }
                }
                catch (LobbyServiceException e)
                {
                    Debug.Log(e);
                    if (e.Reason == LobbyExceptionReason.Forbidden || e.Reason == LobbyExceptionReason.LobbyNotFound)
                    {
                        currentLobby = null;
                        LeaveLobby();
                    }
                }
            }
        }
    }

    public void OpenLobbyScene()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("Testing", LoadSceneMode.Single);
    }

    public async void StartGame()
    {
        if (currentLobby != null && IsLobbyHost())
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
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }

        }
    }

    public void EnterGame()
    {
    }

    // NOT NEEDED YET, REWORK FOR INGAME LOBBY LATER
    //void VisualiseRoomDetails()
    //{
    //    roomName.text = LobbyManager.Instance.currentLobby.Name;
    //    roomCode.text = currentLobby.LobbyCode;
    //    // Clear previous player info
    //    for (int i = 0; i < playerInfoContent.transform.childCount; i++)
    //    {
    //        Destroy(playerInfoContent.transform.GetChild(i).gameObject);
    //    }

    //    if (IsInLobby())
    //    {
    //        foreach (Player player in LobbyManager.Instance.currentLobby.Players)
    //        {
    //            GameObject newPLayerInfo = Instantiate(playerInfoPrefab, playerInfoContent.transform);
    //            newPLayerInfo.GetComponentInChildren<TextMeshProUGUI>().text = player.Data["PlayerName"].Value;
    //            if (IsLobbyHost() && player.Id != playerId)
    //            {
    //                Button kickBtn = newPLayerInfo.GetComponentInChildren<Button>(true); // passing in true makes it referable while being inactive
    //                kickBtn.onClick.AddListener(() => LobbyManager.Instance.KickPlayer(player.Id));
    //                kickBtn.gameObject.SetActive(true);
    //            }
    //        }

    //        if (IsLobbyHost())
    //        {
    //            startGameButton.onClick.AddListener(StartGame);
    //            startGameButton.gameObject.SetActive(true);
    //        }
    //    }

    //    else
    //    {
    //        ExitRoom();
    //    }
    //}
}
