using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
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

        // temp, move to seperate script for initializing players on join
        // Script will be used for loading values of every player to get new joiner up to date and old joiners to track new player
        List<Player> players = LobbyManager.Instance.currentLobby.Players;
        List<NetworkClient> netPlayers = (List<NetworkClient>)NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < LobbyManager.Instance.currentLobby.Players.Count; i++)
        {
            GameObject clientObj = netPlayers[i].PlayerObject.gameObject;

            // Sets up player positions when joining the scene
            clientObj.transform.position = new Vector2(Random.Range(-5f, 5f), -6);

            clientObj.GetComponent<PlayerMovement>().playerId = players[i].Id;
            Debug.Log("player ID: " + netPlayers[i].PlayerObject.gameObject + " player object ID: " + clientObj.GetComponent<PlayerMovement>().playerId);
        }
    }
}