using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] GameObject missilePrefab;

    InputAction attackAction;

    [SerializeField] float cooldown = 1.5f;
    public float cooldownTimer;

    [SerializeField] float recoilStrength;

    PlayerMovement playerScript;
    Rigidbody2D playerRB;


    private void Start()
    {
        playerScript = GetComponentInParent<PlayerMovement>();
        playerRB = GetComponentInParent<Rigidbody2D>();

        attackAction = InputSystem.actions.FindAction("Shoot");
    }

    private void Update()
    {
        if (IsOwner)
        {
            cooldownTimer -= Time.deltaTime;
            cooldownTimer = Mathf.Clamp(cooldownTimer, 0, cooldown);

            if ((PauseMenuScript.instance != null && !PauseMenuScript.instance.isPaused))
            {
                if (playerScript.rollState == RollState.Normal)
                {
                    if (attackAction.WasPressedThisFrame() && cooldownTimer <= 0)
                    {
                        Shoot();
                    }
                }
            }
        }
    }

    void Shoot()
    {
        cooldownTimer = cooldown;

        if (IsHost)
        {
            var missile = Instantiate(missilePrefab, transform.position, transform.rotation);

            MissileScript missileScript = missile.GetComponent<MissileScript>();

            missileScript.playerId = playerScript.playerId;
            missileScript.startVelocity = playerRB.linearVelocity * 0.002f;

            missile.GetComponent<NetworkObject>().Spawn(true);
        }
        else
        {
            SpawnMissileRPC(playerRB.linearVelocity);
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnMissileRPC(Vector2 velocity)
    {
        var missile = Instantiate(missilePrefab, transform.position, transform.rotation);

        MissileScript missileScript = missile.GetComponent<MissileScript>();

        missileScript.playerId = playerScript.playerId;
        missileScript.startVelocity = velocity * 0.001f;

        missile.GetComponent<NetworkObject>().Spawn(true);
    }

    void ShootRecoil(Vector3 recoilDir)
    {
        playerScript.canStopEarly = false;
        playerScript.airDecayTimer = 0.5f;

        playerRB.linearVelocity = (Vector2)recoilDir * recoilStrength;
    }

    Vector3 GetMousePosition()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Vector3 convertedMousePos = Camera.main.ScreenToWorldPoint(mousePos);
        convertedMousePos.z = Camera.main.nearClipPlane;
        return convertedMousePos;
    }
}
