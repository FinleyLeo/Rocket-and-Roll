using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class WinSceneManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI winScreenText;
    [SerializeField] Transform[] podiumSpawns;

    public NetworkList<PlayerScore> scores = new();

    private void Start()
    {
        SortScoreList();

        if (IsHost)
        {
            StartCoroutine(ReturnToLobby());

            // Revive all connected players on the host (server-authoritative)
            ReviveAllPlayers();

            // Send each player their target podium (only top N get podium positions)
            for (int i = 0; i < scores.Count; i++)
            {
                var score = scores[i];
                Vector3 targetPos;

                if (i < podiumSpawns.Length)
                {
                    // top positions use podium transforms
                    targetPos = podiumSpawns[i].position;
                }
                else
                {
                    // non-top players: send a floor position (example: y = 0).
                    // User said they will implement custom floor handling — adjust as needed.
                    targetPos = new Vector3(0f, 0f, 0f);
                }

                // Send position directly to the client that owns this score
                SpawnPlayersOnPodiumsRPC(targetPos, RpcTarget.Single(score.clientId, RpcTargetUse.Temp));
            }
        }

        TransitionManager.Instance.EndTransition();

        SetWinText();
    }

    IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(5);

        MatchManager.Instance.ResetScores();
        TransitionManager.Instance.LoadScene("Lobby");
    }

    void SetWinText()
    {
        var winner = MatchManager.Instance.GetWinner();
        if (winner.Equals(default(PlayerScore)))
        {
            winScreenText.text = "No winner";
            return;
        }

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (winner.clientId == client.ClientId)
            {
                var pv = client.PlayerObject?.GetComponent<PlayerVisuals>();
                if (pv != null)
                {
                    string playerName = pv.usernameText.text;
                    winScreenText.text = playerName + " WINS!";
                }
                else
                {
                    winScreenText.text = "Player WINS!";
                }
                break;
            }
        }
    }

    // Server-side helper to revive everyone
    void ReviveAllPlayers()
    {
        if (!IsHost) return;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.SendRespawnRPC();
            }
        }
    }

    // Send position to a specific client. The host calls this targeting each client.
    [Rpc(SendTo.SpecifiedInParams)]
    void SpawnPlayersOnPodiumsRPC(Vector3 position, RpcParams rpcParams = default)
    {
        // This RPC runs on the target client (and host if included). Target client should set its own player object position.
        var local = NetworkManager.Singleton.LocalClient;
        if (local == null)
        {
            Debug.LogWarning("SpawnPlayersOnPodiumsRPC: LocalClient is null");
            return;
        }

        var playerObj = local.PlayerObject;

        if (playerObj == null)
        {
            Debug.LogWarning("SpawnPlayersOnPodiumsRPC: Local player's NetworkObject is null");
            return;
        }

        // Ensure we only move the local player's object (owner client).
        // local.PlayerObject is the client's own player object, so safe to move here.
        playerObj.transform.position = position;
    }

    void SortScoreList()
    {
        scores = new();
        List<PlayerScore> tempScores = new();

        foreach (PlayerScore score in MatchManager.Instance.scores)
        {
            tempScores.Add(score);
        }

        tempScores.Sort((a, b) => b.points.CompareTo(a.points));

        foreach (PlayerScore score in tempScores)
        {
            Debug.Log($"Added {score.clientId} with {score.points} points to the sorted list.");
            scores.Add(score);
        }
    }
}
