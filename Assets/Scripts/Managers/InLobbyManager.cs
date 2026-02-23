using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class InLobbyManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI roomNameText;
    [SerializeField] TextMeshProUGUI roomCodeText;

    List<Player> players;
    List<NetworkClient> netPlayers;
    void Start()
    {
        players = new List<Player>();
        netPlayers = new List<NetworkClient>();

        UpdatePlayerLists();

        // Set up lobby visuals
        roomNameText.text = LobbyManager.Instance.currentLobby.Name;
        roomCodeText.text = LobbyManager.Instance.currentLobby.LobbyCode;

        // Sets up player positions when joining the scene
        for (int i = 0; i < LobbyManager.Instance.currentLobby.Players.Count; i++)
        {
            GameObject clientObj = netPlayers[i].PlayerObject.gameObject;

            clientObj.transform.position = new Vector2(Random.Range(-5f, 5f), -6);
        }

        UpdatePlayerInfo();

        NetworkManager.Singleton.OnClientConnectedCallback += (ulong clientId) => UpdatePlayerInfo();
    }

    void UpdatePlayerLists()
    {
        players = LobbyManager.Instance.currentLobby.Players;
        netPlayers = (List<NetworkClient>)NetworkManager.Singleton.ConnectedClientsList;
    }

    void UpdatePlayerInfo()
    {
        // Loads values of every player to get new joiner up to date and old joiners to track new player
        UpdatePlayerLists();

        for (int i = 0; i < players.Count; i++)
        {
            GameObject clientObj = netPlayers[i].PlayerObject.gameObject;

            // Assign player id
            clientObj.GetComponent<PlayerMovement>().playerId = players[i].Id;

            #region layerOrdering

            int clientOrder = 0;

            if (clientObj.GetComponent<PlayerMovement>().IsOwner)
            {
                clientOrder = 12;
            }
            else
            {
                // Set based on player join ranking
                clientOrder = ((int)clientObj.GetComponent<PlayerMovement>().NetworkObjectId - 1);
            }

            SetOrder(clientObj, clientOrder, 2);

            for (int j = 0; j < clientObj.transform.childCount; j++)
            {
                GameObject childObj = clientObj.transform.GetChild(j).gameObject;

                if (childObj.transform.childCount > 0)
                {
                    for (int k = 0; k < childObj.transform.childCount; k++)
                    {
                        GameObject _childObj = childObj.transform.GetChild(k).gameObject;

                        if (_childObj.transform.childCount > 0)
                        {
                            for (int l = 0; l < _childObj.transform.childCount; l++)
                            {
                                GameObject __childObj = _childObj.transform.GetChild(l).gameObject;

                                SetOrder(__childObj, clientOrder, 5);
                            }
                        }

                        SetOrder(_childObj, clientOrder, 5);
                    }
                }

                SetOrder(childObj, clientOrder, 5);
            }

            #endregion
        }
    }

    void SetOrder(GameObject obj, int orderIncrement, int maxOrder)
    {
        if (obj.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            SpriteRenderer _sr = sr;

            if (_sr.sortingOrder < maxOrder)
            {
                _sr.sortingOrder += orderIncrement;
            }
        }
        else if (obj.TryGetComponent<Canvas>(out Canvas canvas))
        {
            Canvas _canvas = canvas;

            if (_canvas.sortingOrder < maxOrder)
            {
                _canvas.sortingOrder += orderIncrement;
            }
        }
        else if (obj.TryGetComponent<ParticleSystemRenderer>(out ParticleSystemRenderer ps))
        {
            ParticleSystemRenderer _ps = ps.GetComponent<ParticleSystemRenderer>();

            if (_ps.sortingOrder < maxOrder)
            {
                _ps.sortingOrder += orderIncrement;
            }
        }
    }
}