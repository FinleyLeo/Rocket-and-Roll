using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLookAt : NetworkBehaviour
{
    Transform eyePivot;
    Transform eyeTransform;
    PlayerMovement playerScript;
    float clampDistance = 0.35f;

    private void Start()
    {
        playerScript = GetComponent<PlayerMovement>();

        if (playerScript == null)
        {
            Debug.Log("Player script not found");
        }

        eyePivot = transform.GetChild(1);
        eyeTransform = eyePivot.GetChild(0);
    }

    void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        LookAtMouse();
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
        eyePivot.rotation = Quaternion.Euler(0, 0, 0); // Makes sure rotation is always the same
        Vector2 lookDir = GetMousePosition() - eyePivot.position;
        //float lookAngle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        //lookDir = playerScript.isFlipped.Value ? -lookDir : lookDir;

        eyeTransform.position = GetMousePosition();

        if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
        {
            //eyePivot.rotation = Quaternion.Euler(new Vector3(0, 0, lookAngle));
            eyeTransform.localPosition = lookDir.normalized * (clampDistance - 0.1f);
        }
    }
}
