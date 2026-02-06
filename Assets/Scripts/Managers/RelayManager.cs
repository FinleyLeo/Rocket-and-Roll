using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance;

    [HideInInspector] public Allocation allocation;
    [HideInInspector] public JoinAllocation joinAllocation;

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
    private void Start()
    {
        //NetworkManager.Singleton.OnServerStarted += () =>
        //{
        //    NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        //};
    }

    public async Task<string> CreateRelay(int maxPlayers)
    {
        try
        {
            if (!LobbyManager.Instance.servicesReady)
            {
                Debug.Log("Services not ready yet");
                return null;
            }

            allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return null;
        }
    }

    public async void JoinRelay(string joinCode)
    {
        try
        {
            if (!LobbyManager.Instance.servicesReady)
            {
                Debug.Log("Services not ready yet");
                return;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.Log($"No lobby with join code {joinCode} found");
                return;
            }

            joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    public void LeaveRelay()
    {
        try
        {
            // only run code if an allocation is being used
            if (allocation == null)
            {
                if (joinAllocation == null)
                {
                    return;
                }
            }

            NetworkManager.Singleton.Shutdown();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }
}
