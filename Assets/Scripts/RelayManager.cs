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
    string joinCode;

    [HideInInspector] public Allocation allocation;
    [HideInInspector] public JoinAllocation joinAllocation;

    [HideInInspector] public bool relayCreated;

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
        //AuthenticatePlayer();
    }

    public async void AuthenticatePlayer()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }


    public async void CreateRelay(int maxPlayers)
    {
        try
        {
            relayCreated = false;

            allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();

            relayCreated = true;

            Debug.Log("Join code:" + joinCode);
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void JoinRelay(string joinCode)
    {
        try
        {
            if (joinCode == null)
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

    public string GetJoinCode()
    {
        return joinCode;
    }
}
