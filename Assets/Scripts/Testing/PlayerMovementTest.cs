using UnityEngine;
using Unity.Netcode;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class PlayerMovementTest : NetworkBehaviour
{
    private NetworkVariable<Vector2> position = new NetworkVariable<Vector2>(new Vector2(0, 5), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    Rigidbody2D rb;

    InputAction moveAction;
    InputAction jumpAction;

    public override void OnNetworkSpawn()
    {
        position.OnValueChanged += (Vector2 previousPos, Vector2 nextPos) => Debug.Log($"Player {NetworkObjectId} moved to: {position.Value}");

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");

        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!IsOwner)
        {
            transform.position = position.Value;

            return;
        }

        float moveDir = moveAction.ReadValue<Vector2>().x;

        if (jumpAction.IsPressed())
        {
            rb.AddForce(Vector2.up * 2);
        }

        rb.linearVelocity = new Vector2(moveDir * 3, rb.linearVelocityY);

        position.Value = transform.position;
    }
}