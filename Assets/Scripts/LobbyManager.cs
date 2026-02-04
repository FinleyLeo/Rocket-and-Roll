using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [HideInInspector] public Lobby currentLobby;
    [HideInInspector] public string playerId;

    string lobbyJoinCode;

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

    public void CreateLobby(bool isPrivate, int maxPlayers, string lobbyName)
    {
        try
        {
            if (lobbyName == "" || lobbyName == " " || lobbyName == "   " || lobbyName == null)
            {
                lobbyName = PlayerPrefs.GetString("Username", playerId) + "'s lobby";
            }

            RelayManager.Instance.CreateRelay(maxPlayers);

            WaitForRelay(isPrivate, maxPlayers, lobbyName);

            //NetworkManager.Singleton.SceneManager.LoadScene("Testing", LoadSceneMode.Single);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    async void LobbySetUp(bool isPrivate, int maxPlayers, string lobbyName)
    {
        string joinCode = RelayManager.Instance.GetJoinCode();
        Debug.Log("Join Code: " + joinCode);

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = isPrivate,
            Player = GetPlayer(),
            Data = new Dictionary<string, DataObject>
                {
                    {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") },
                    {"RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, RelayManager.Instance.GetJoinCode()) }
                }
        };

        currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

        Debug.Log("Data converted join Code: " + currentLobby.Data["RelayJoinCode"].Value);
    }

    IEnumerator WaitForRelay(bool isPrivate, int maxPlayers, string lobbyName)
    {
        yield return new WaitForSeconds(1);

        Debug.Log("Finished timer");

        //if (RelayManager.Instance.GetJoinCode() == null)
        //{
        //    yield return new WaitForSecondsRealtime(2);
        //}

        LobbySetUp(isPrivate, maxPlayers, lobbyName);
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

            if (currentLobby.AvailableSlots < 1)
            {
                Debug.Log("Lobby is full");
                LeaveLobby();
                return;
            }

            string joinCode = currentLobby.Data["RelayJoinCode"].Value;

            RelayManager.Instance.JoinRelay(RelayManager.Instance.GetJoinCode());
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

            if (currentLobby.AvailableSlots < 1)
            {
                Debug.Log("Lobby is full");
                LeaveLobby();
                return;
            }

            string joinCode = currentLobby.Data["RelayJoinCode"].Value;

            RelayManager.Instance.JoinRelay(joinCode);
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

            RelayManager.Instance.LeaveRelay();
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
            NetworkManager.Singleton.Shutdown();
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
