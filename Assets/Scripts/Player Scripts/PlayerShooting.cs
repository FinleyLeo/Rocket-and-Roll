using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] GameObject missilePrefab;

    InputAction attackAction;

    [SerializeField] float cooldown = 1;
    public float cooldownTimer;

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

            if (playerScript.canMove.Value)
            {
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
    }

    void Shoot()
    {
        cooldownTimer = cooldown;

        SpriteRenderer rpgSR = GetComponentInParent<SpriteRenderer>();

        SpawnMissileRPC(playerRB.linearVelocity, playerScript.playerId.ToString(), rpgSR.material.GetColor("_Outline"));
    }

    [Rpc(SendTo.Server)]
    void SpawnMissileRPC(Vector2 velocity, string playerId, Color color)
    {
        //var missile = Instantiate(missilePrefab, transform.position, transform.rotation);
        var missile = Instantiate(missilePrefab, transform.position, Quaternion.Euler(RotationAlignment(transform.rotation.eulerAngles)));

        MissileScript missileScript = missile.GetComponent<MissileScript>();

        missileScript.moveDirection = transform.right;
        missileScript.startVelocity = velocity * 0.002f;

        missile.GetComponent<NetworkObject>().Spawn(true);
        missileScript.missileColor.Value = color;
        missileScript.playerId.Value = playerId;
    }

    // used to reduce visual artifacts from pixel art upscaling as missiles rotate a lot
    Vector3 RotationAlignment(Vector3 rotation)
    {
        Vector3 modifiedRotation = rotation;

        float absXAngle = Mathf.Abs(rotation.z);

        if (absXAngle < 5)
        {
            modifiedRotation.z = 0;
        }
        else if (absXAngle < 95 && absXAngle > 85)
        {
            modifiedRotation.z = 90;
        }
        else if (absXAngle < 185 && absXAngle > 175)
        {
            modifiedRotation.z = 180;
        }
        else if (absXAngle < 275 && absXAngle > 265)
        {
            modifiedRotation.z = 270;
        }

        return modifiedRotation;
    }
}
