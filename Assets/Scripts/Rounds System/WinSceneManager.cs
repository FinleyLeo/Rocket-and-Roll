using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class WinSceneManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI winScreenText;

    private void Start()
    {
        if (IsHost)
        {
            StartCoroutine(ReturnToLobby());
        }

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (MatchManager.Instance.GetWinner().clientId == client.ClientId)
            {
                string playerName = client.PlayerObject.GetComponent<PlayerVisuals>().usernameText.text;

                winScreenText.text = playerName + " WINS!";
            }
        }

        TransitionManager.Instance.EndTransition();
    }

    IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(5);

        TransitionManager.Instance.LoadScene("Lobby");
    }
}
