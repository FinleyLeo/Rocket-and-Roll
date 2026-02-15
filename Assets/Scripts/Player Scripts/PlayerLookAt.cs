using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLookAt : NetworkBehaviour
{
    Transform eyePivot;
    Transform rpgPivot;
    Transform eyeTransform;
    PlayerMovement playerScript;

    readonly float clampDistance = 0.35f;

    private void Start()
    {
        playerScript = GetComponent<PlayerMovement>();

        if (playerScript == null)
        {
            Debug.Log("Player script not found");
        }

        eyePivot = transform.GetChild(1);
        eyeTransform = eyePivot.GetChild(0);

        rpgPivot = transform.GetChild(2);
    }

    void Update()
    {
        // Makes sure rotation is always the same
        eyePivot.rotation = Quaternion.Euler(0, 0, 0);

        if (!IsOwner)
        {
            return;
        }

        if (PauseMenuScript.instance != null)
        {
            if (!PauseMenuScript.instance.isPaused)
            {
                LookAtMouse();
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
        Vector2 eyelookDir = GetMousePosition() - eyePivot.position;
        Vector2 rpglookDir = GetMousePosition() - rpgPivot.position;
        float rpglookAngle = Mathf.Atan2(rpglookDir.y, rpglookDir.x) * Mathf.Rad2Deg;

        // manages eye position
        eyeTransform.position = GetMousePosition();

        // clamps to certain distance, rotating around the edge of the head
        if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
        {
            eyeTransform.localPosition = eyelookDir.normalized * (clampDistance - 0.1f);
        }

        // manages rpg rotation
        //rpgPivot.rotation = Quaternion.Euler(0, 0, rpglookAngle);

        if (GetMousePosition().x < rpgPivot.position.x)
        {
            rpgPivot.rotation = Quaternion.Euler(180, 0, -rpglookAngle);
        }
        else
        {
            rpgPivot.rotation = Quaternion.Euler(0, 0, rpglookAngle);
        }
    }
}
