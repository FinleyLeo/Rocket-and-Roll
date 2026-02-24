using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLookAt : NetworkBehaviour
{
    Transform eyePivot;
    Transform rpgPivot;
    Transform rpg;
    Transform eyeTransform;
    PlayerMovement playerScript;
    PlayerShooting playerShootScript;

    readonly float clampDistance = 0.35f;

    bool eyesLocked;

    private void Start()
    {
        playerScript = GetComponent<PlayerMovement>();
        playerShootScript = GetComponentInChildren<PlayerShooting>();

        if (playerScript == null)
        {
            Debug.Log("Player script not found");
        }

        eyePivot = transform.GetChild(1);
        eyeTransform = eyePivot.GetChild(0);

        rpgPivot = transform.GetChild(2);
        rpg = rpgPivot.GetChild(0);

    }

    void Update()
    {
        // Makes sure rotation is always the same
        //eyePivot.rotation = Quaternion.Euler(0, 0, 0);

        if (!IsOwner) return;

        if (PauseMenuScript.instance != null)
        {
            if (!PauseMenuScript.instance.isPaused)
            {
                LookAtMouse();
            }
        }

        if (playerScript.inFullRoll)
        {
            if (Mathf.Abs(playerScript.moveDir) > 0) // if moving
            {
                eyeTransform.localPosition = eyePivot.localPosition;
                eyesLocked = true;
            }
            else
            {
                eyesLocked = false;
            }
        }
        else
        {
            eyesLocked = false;
        }
    }

    Vector3 GetMousePosition()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Vector3 convertedMousePos = Camera.main.ScreenToWorldPoint(mousePos);
        convertedMousePos.z = Camera.main.nearClipPlane;
        return convertedMousePos;
    }

    void LookAtMouse()
    {
        Vector2 eyelookDir = (GetMousePosition() - eyePivot.position).normalized;
        Vector2 rpglookDir = (GetMousePosition() - rpgPivot.position).normalized;
        float rpglookAngle = Mathf.Atan2(rpglookDir.y, rpglookDir.x) * Mathf.Rad2Deg;

        // Only move eyes if not lcoked in place
        if (!eyesLocked)
        {
            // manages eye position
            eyeTransform.position = GetMousePosition();

            // clamps to certain distance, rotating around the edge of the head
            if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
            {
                eyeTransform.localPosition = eyelookDir.normalized * (clampDistance - 0.1f);
            }
        }

        // manages rpg rotation
        if (GetMousePosition().x < rpgPivot.position.x)
        {
            rpg.rotation = Quaternion.Euler(180, 0, -rpglookAngle);
        }
        else
        {
            rpg.rotation = Quaternion.Euler(0, 0, rpglookAngle);
        }

        // Visualises cooldown through rpg recoil
        rpg.localPosition = rpglookDir * (-playerShootScript.cooldownTimer * 0.45f);
    }
}
