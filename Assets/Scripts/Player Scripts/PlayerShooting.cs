using Unity.Netcode;
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
        if (IsOwner)
        {
            cooldownTimer -= Time.deltaTime;

            if (attackAction.WasPressedThisFrame() && cooldownTimer <= 0)
            {
                cooldownTimer = cooldown;

                MissileScript missile = Instantiate(missilePrefab, transform.position, transform.rotation).GetComponent<MissileScript>();
                missile.playerId = playerScript.playerId; 
            }
        }
    }
}
