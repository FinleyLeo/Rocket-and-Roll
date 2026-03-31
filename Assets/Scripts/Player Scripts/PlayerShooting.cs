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
            var missile = Instantiate(missilePrefab, transform.position, Quaternion.Euler(RotationAlignment(transform.rotation.eulerAngles)));

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
        //var missile = Instantiate(missilePrefab, transform.position, transform.rotation);
        var missile = Instantiate(missilePrefab, transform.position, Quaternion.Euler(RotationAlignment(transform.rotation.eulerAngles)));

        MissileScript missileScript = missile.GetComponent<MissileScript>();

        missileScript.playerId = playerScript.playerId;
        missileScript.startVelocity = velocity * 0.001f;

        missile.GetComponent<NetworkObject>().Spawn(true);
    }

    // used to reduce visual artifacts from pixel art upscaling as missiles rotate a lot
    Vector3 RotationAlignment(Vector3 rotation)
    {
        Vector3 modifiedRotation = rotation;

        //modifiedRotation.z = Mathf.CeilToInt(rotation.z / 45);
        //modifiedRotation.z *= 45;

        float absXAngle = Mathf.Abs(rotation.z);

        if (absXAngle < 10)
        {
            modifiedRotation.z = 0;
        }
        else if (absXAngle < 100 && absXAngle > 80)
        {
            modifiedRotation.z = 90;
        }
        else if (absXAngle < 190 && absXAngle > 170)
        {
            modifiedRotation.z = 180;
        }
        else if (absXAngle < 280 && absXAngle > 260)
        {
            modifiedRotation.z = 270;
        }

        return modifiedRotation;
    }
}
