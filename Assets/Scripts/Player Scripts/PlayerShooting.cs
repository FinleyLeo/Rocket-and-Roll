using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] GameObject missilePrefab;

    InputAction attackAction;

    [SerializeField] float cooldown = 1f;
    [SerializeField] float cooldownTimer;

    [SerializeField] PlayerMovement playerScript;

    private void Start()
    {
        attackAction = InputSystem.actions.FindAction("Attack");
    }

    private void Update()
    {
        if (IsOwner && (PauseMenuScript.instance != null && !PauseMenuScript.instance.isPaused))
        {
            cooldownTimer -= Time.deltaTime;

            if (attackAction.WasPressedThisFrame() && cooldownTimer <= 0)
            {
                cooldownTimer = cooldown;

                if (IsHost)
                {
                    var missile = Instantiate(missilePrefab, transform.position, transform.rotation);

                    missile.GetComponent<MissileScript>().playerId = playerScript.playerId;
                    missile.GetComponent<NetworkObject>().Spawn(true);
                }
                else
                {
                    SpawnMissileRPC();
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnMissileRPC()
    {
        var missile = Instantiate(missilePrefab, transform.position, transform.rotation);

        missile.GetComponent<MissileScript>().playerId = playerScript.playerId;
        missile.GetComponent<NetworkObject>().Spawn(true);

        Debug.Log("If youre seeing this on the host pc, then spawning RPC works");
    }
}
