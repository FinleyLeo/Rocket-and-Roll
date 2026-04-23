using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLookAt : NetworkBehaviour
{
    Transform eyePivot;
    Transform rpgPivot;
    Transform rpg;
    Transform eyeTransform;

    PlayerMovement moveScript;
    PlayerShooting shootScript;
    PlayerHealth healthScript;

    readonly float clampDistance = 0.35f;

    bool eyesLocked;

    Quaternion eyeStoredRotation;

    [HideInInspector] public float rotationSpeed;

    InputAction lookAction;

    private void Start()
    {
        moveScript = GetComponent<PlayerMovement>();
        healthScript = GetComponent<PlayerHealth>();
        shootScript = GetComponentInChildren<PlayerShooting>();

        lookAction = InputSystem.actions.FindAction("Look");

        eyePivot = transform.GetChild(1);
        eyeTransform = eyePivot.GetChild(0);

        rpgPivot = transform.GetChild(2);
        rpg = rpgPivot.GetChild(0);
    }

    void Update()
    {
        if (!IsOwner) return;

        if (healthScript.isAlive.Value)
        {
            if (PauseMenuScript.instance != null)
            {
                if (!PauseMenuScript.instance.isPaused)
                {
                    LookAtMouse();
                }
            }

            if (moveScript.inFullRoll)
            {
                eyeTransform.localPosition = eyePivot.localPosition;
                eyesLocked = true;

                eyeTransform.rotation = eyeStoredRotation;
                eyeTransform.rotation = Quaternion.Euler(0, 0, eyeTransform.rotation.eulerAngles.z + rotationSpeed);
                eyeStoredRotation = eyeTransform.rotation;
            }
            else
            {
                eyesLocked = false;

                rotationSpeed = 0;
                eyeStoredRotation = eyeTransform.rotation;
                eyeTransform.rotation = Quaternion.Euler(0, 0, 0);
            }
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
        rpg.localPosition = rpglookDir * (-shootScript.cooldownTimer * 0.45f);
    }
}
