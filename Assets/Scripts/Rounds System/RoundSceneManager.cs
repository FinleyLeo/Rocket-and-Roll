using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class RoundSceneManager : NetworkBehaviour
{
    public static RoundSceneManager Instance;

    HashSet<ulong> readyClients = new();

    [SerializeField] Transform pointsParent;
    [SerializeField] GameObject pointsPrefab;

    //List<TextMeshProUGUI> pointDisplay = new();

    [SerializeField] Animator crowdAnim;

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

    public override void OnDestroy()
    {
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.OnMatchStateChanged -= RoundStateLogic;
        }
    }

    private void Start()
    {
        TransitionManager.Instance.EndTransition();

        ColourChangeManager instanceTemp = ColourChangeManager.instance;
        instanceTemp.selectedPalette = instanceTemp.palettes[instanceTemp.selectedPaletteIndex.Value];
        instanceTemp.SetBackgroundPattern(instanceTemp.selectedPatternIndex.Value);

        //InitialisePointDisplay();
    }

    #region Round System

    void RoundStateLogic(MatchState state)
    {
        switch (state)
        {
            case MatchState.RoundEnding:
                if (IsServer)
                {
                    StartCoroutine(RoundEndCountdown());
                }
                break;
            case MatchState.RoundActive:
                // temp, may be needed later
                break;
        }
    }

    [Rpc(SendTo.Server)]
    public void ClientReadyRPC(ulong clientId)
    {
        // This RPC is executed on the server. Be defensive: guard against missing singletons.
        Debug.Log($"ClientReadyRPC received for client {clientId} on server.");

        if (!IsServer)
        {
            Debug.LogWarning("ClientReadyRPC was invoked but this instance is not the server. Ignoring.");
            return;
        }

        if (readyClients == null)
            readyClients = new HashSet<ulong>();

        // Add client to ready set
        readyClients.Add(clientId);

        // Defensive check for NetworkManager
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton is null in ClientReadyRPC.");
            return;
        }

        int connectedCount = NetworkManager.Singleton.ConnectedClientsList?.Count ?? NetworkManager.Singleton.ConnectedClients.Count;

        // If every connected client has connected, proceed to start round countdown.
        if (readyClients.Count >= connectedCount)
        {
            readyClients.Clear();

            if (MatchManager.Instance == null)
            {
                Debug.LogWarning("MatchManager.Instance is null in ClientReadyRPC; cannot check match state.");
                return;
            }

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

        if (IsServer)
            MatchManager.Instance.matchState.Value = MatchState.RoundActive;
    }

    IEnumerator RoundEndCountdown()
    {
        bool crowdOn = PlayerPrefs.GetInt(SaveDataManager.instance.crowdToggleKey, 0) == 1;

        MatchManager.Instance.SendEndTimeSlowRPC(0.5f);

        yield return new WaitForSecondsRealtime(1.5f);

        if (crowdOn)
        {
            SetCrowdBoolRPC(true);
        }

        MatchManager.Instance.SendEndTimeSlowRPC(1f);

        yield return new WaitForSecondsRealtime(0.5f);

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

        if (crowdOn)
        {
            SetCrowdBoolRPC(false);
        }

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

    #endregion

    [Rpc(SendTo.ClientsAndHost)]
    void SetCrowdBoolRPC(bool value1)
    {
        crowdAnim.SetBool("Cheering", value1);
    }

    //void InitialisePointDisplay()
    //{
    //    foreach (PlayerScore score in MatchManager.Instance.scores)
    //    {
    //        TextMeshProUGUI pointShadowText = Instantiate(pointsPrefab, pointsParent).GetComponent<TextMeshProUGUI>();
    //        TextMeshProUGUI pointText = pointShadowText.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

    //        pointText.text = score.points.ToString();
    //        pointShadowText.text = score.points.ToString();

    //        pointDisplay.Add(pointShadowText);
    //    }
    //}

    // When players first load in, THEN add all currently joined players
    // When a new player joins, add onto the list
    // New players should get all currently joined players
    // When a player leaves, remove their point prefab from the list
}