using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum MatchState 
{ 
    None,
    Lobby, 
    RoundStarting, 
    RoundActive, 
    RoundEnding, 
    MatchEnded 
}

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance;

    public NetworkVariable<MatchState> matchState = new(MatchState.None);
    public NetworkList<PlayerScore> scores = new();
    public NetworkVariable<int> playersAlive = new();

    public event Action<MatchState> OnMatchStateChanged;

    private void Awake() // Singleton initialisation
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

    public override void OnNetworkSpawn()
    {
        // Temp points display for debugging
        scores.OnListChanged += OnScoresChanged;

        LogRankings();

        matchState.OnValueChanged += (prev, current) =>
        {
            OnMatchStateChanged?.Invoke(current);
            OnStateChanged(prev, current);
        };

        if (IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    #region Event Handling

    public override void OnDestroy()
    {
        // temp point display
        scores.OnListChanged -= OnScoresChanged;

        if (NetworkManager.Singleton != null && IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    void HandleClientConnected(ulong clientId)
    {
        // Add to scoreboard
        if (IsHost)
        {
            scores.Add(new PlayerScore { clientId = clientId, points = 0 });

            // Update alive count if round is still starting
            if (matchState.Value == MatchState.RoundStarting)
                playersAlive.Value = NetworkManager.Singleton.ConnectedClients.Count;
        }  
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (IsHost)
        {
            // Remove from scoreboard
            for (int i = scores.Count - 1; i >= 0; i--)
            {
                if (scores[i].clientId == clientId)
                {
                    scores.RemoveAt(i);
                    break;
                }
            }

            // Update alive count if round is still starting
            if (matchState.Value == MatchState.RoundStarting)
                playersAlive.Value = NetworkManager.Singleton.ConnectedClients.Count;
        }
    }


    void OnStateChanged(MatchState prev, MatchState current)
    {
        if (!IsHost) return; // only server drives logic

        switch (current)
        {
            case MatchState.Lobby:
                playersAlive.Value = 0;
                SetAllPlayerMovement(true);
                break;

            case MatchState.RoundStarting:
                playersAlive.Value = NetworkManager.Singleton.ConnectedClients.Count;
                SetAllPlayerMovement(false);
                break;

            case MatchState.RoundActive:
                SetAllPlayerMovement(true);
                break;

            case MatchState.RoundEnding:
                SetAllPlayerMovement(false);
                break;

            case MatchState.MatchEnded:
                SetAllPlayerMovement(false);
                EndMatch();
                break;
        }
    }

    #endregion

    public void EndMatch()
    {
        // resets all scores
        for (int i = 0; i < scores.Count; i++)
        {
            PlayerScore score = scores[i];
            score.points = 0;
            scores[i] = score;
        }

        TransitionManager.Instance.LoadScene("WinScreen");
    }

    void SetAllPlayerMovement(bool canMove)
    {
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            client.PlayerObject.GetComponent<PlayerMovement>().canMove.Value = canMove;
        }
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerDeathRPC()
    {
        playersAlive.Value--;

        if (playersAlive.Value <= 1 && matchState.Value == MatchState.RoundActive)
        {
            matchState.Value = MatchState.RoundEnding;
        }
    }

    public void AwardPoint(ulong clientId)
    {
        if (!IsHost) return;

        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i].clientId == clientId)
            {
                // Increase winning players score by 1
                PlayerScore score = scores[i];
                score.points++;
                scores[i] = score;

                if (score.points >= 3)
                {
                    matchState.Value = MatchState.MatchEnded;
                }
                break;
            }
        }
    }

    public PlayerScore GetWinner()
    {
        PlayerScore winner = default;
        int highestScore = -1;

        foreach (PlayerScore score in scores)
        {
            if (score.points > highestScore)
            {
                highestScore = score.points;
                winner = score;
            }
        }

        return winner;
    }

    #region debug points display

    public List<PlayerScore> GetRankedPlayers()
    {
        List<PlayerScore> ranked = new List<PlayerScore>();

        foreach (PlayerScore score in scores)
        {
            ranked.Add(score);
        }

        ranked.Sort((a, b) => b.points.CompareTo(a.points)); // descending
        return ranked;
    }

    void OnScoresChanged(NetworkListEvent<PlayerScore> changeEvent)
    {
        // Type tells you what happened
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerScore>.EventType.Add:
                Debug.Log($"Player {changeEvent.Value.clientId} joined the scoreboard");
                break;

            case NetworkListEvent<PlayerScore>.EventType.Value:
                Debug.Log($"Player {changeEvent.Value.clientId} now has {changeEvent.Value.points} points");
                break;

            case NetworkListEvent<PlayerScore>.EventType.Remove:
                Debug.Log($"Player {changeEvent.Value.clientId} removed from scoreboard");
                break;
        }

        // Log full rankings after any change
        LogRankings();
    }

    void LogRankings()
    {
        List<PlayerScore> ranked = GetRankedPlayers();

        for (int i = 0; i < ranked.Count; i++)
        {
            Debug.Log($"#{i + 1} - Client {ranked[i].clientId}: {ranked[i].points} pts");
        }
    }

    #endregion
}

[System.Serializable]
public struct PlayerScore : INetworkSerializeByMemcpy, System.IEquatable<PlayerScore>
{
    public ulong clientId;
    public int points;

    public bool Equals(PlayerScore other) => clientId == other.clientId && points == other.points;
}
