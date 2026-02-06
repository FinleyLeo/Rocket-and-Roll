using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbySetup : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI roomNameText;
    [SerializeField] TextMeshProUGUI roomCodeText;

    void Start()
    {
        // Set up lobby visuals
        roomNameText.text = LobbyManager.Instance.currentLobby.Name;
        roomCodeText.text = LobbyManager.Instance.currentLobby.LobbyCode;

        // Sets up player positions when joining the scene

        
        //client.PlayerObject.transform.position = new Vector2(Random.Range(-5f, 5f), -6);
        //Debug.Log(client.ClientId);
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            client.PlayerObject.transform.position = new Vector2(Random.Range(-5f, 5f), -6);
            Debug.Log(client.ClientId);
        }
    }
}