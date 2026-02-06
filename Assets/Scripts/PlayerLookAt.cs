using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLookAt : NetworkBehaviour
{
    Transform eyePivot;
    float clampDistance = 0.35f;

    private void Start()
    {
        eyePivot = transform.parent;
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
        Vector2 lookDir = GetMousePosition() - eyePivot.position;
        float lookAngle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

        transform.position = GetMousePosition();

        if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
        {
            //eyePivot.rotation = Quaternion.Euler(new Vector3(0, 0, lookAngle));
            transform.localPosition = lookDir.normalized * (clampDistance - 0.1f);
        }
    }
}
