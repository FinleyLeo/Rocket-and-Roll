using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RoundSceneManager : NetworkBehaviour
{
    public static RoundSceneManager Instance;

    HashSet<ulong> readyClients = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public override void OnNetworkSpawn()
    {
        MatchManager.Instance.OnMatchStateChanged += RoundStateLogic;

        TransitionManager.Instance.EndTransition();
    }

    private void Start()
    {
        TransitionManager.Instance.EndTransition();
    }

    private void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame && IsHost)
        {
            EndCurrentRound();
        }
    }

    void RoundStateLogic(MatchState state)
    {
        switch (state)
        {
            case MatchState.RoundEnding:
                StartCoroutine(RoundEndCountdown());
                break;
            case MatchState.RoundActive:
                // temp, may be needed later
                break;
        }
    }

    [Rpc(SendTo.Server)]
    public void ClientReadyRPC(ulong clientId)
    {
        Debug.Log("client " + clientId + "ready notification");
        readyClients.Add(clientId);

        if (readyClients.Count >= NetworkManager.Singleton.ConnectedClients.Count)
        {
            readyClients.Clear();

            if (MatchManager.Instance.matchState.Value == MatchState.RoundStarting)
            {
                StartCoroutine(RoundStartCountdown());
            }
        }
    }

    IEnumerator RoundStartCountdown()
    {
        yield return new WaitForSeconds(1f);
        // Show "READY"

        yield return new WaitForSeconds(1f);
        // Show "SET"

        yield return new WaitForSeconds(1f);
        // Show "GO"

        if (IsHost)
            MatchManager.Instance.matchState.Value = MatchState.RoundActive;
    }

    IEnumerator RoundEndCountdown()
    {
        // run when round state changed to round ending

        yield return new WaitForSeconds(2);

        // check if any player still alive after 2 second(s) of round ending, if not then its a draw
        bool isDraw = true;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            PlayerHealth healthScript = client.PlayerObject.GetComponent<PlayerHealth>();

            if (healthScript.isAlive.Value)
            {
                // Last player alive awarded one point
                isDraw = false;
                MatchManager.Instance.AwardPoint(healthScript.OwnerClientId);
            }
        }

        if (isDraw)
            Debug.Log("Is a draw, no points awarded");

        yield return new WaitForSeconds(2);

        if (MatchManager.Instance.matchState.Value != MatchState.MatchEnded)
        {
            EndCurrentRound();
        }
    }

    public void EndCurrentRound()
    {
        GameObject[] missiles = GameObject.FindGameObjectsWithTag("Missile");

        if (missiles.Length > 0)
        {
            foreach (GameObject missile in missiles)
            {
                DestroyImmediate(missile);
            }
        }

        MatchManager.Instance.matchState.Value = MatchState.RoundStarting;
        TilemapGen.Instance.GenerateAutoSmooth();
    }
}
