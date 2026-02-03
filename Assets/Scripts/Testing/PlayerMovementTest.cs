using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

enum PlayerMode
{
    Normal,
    Balled
}

public class PlayerMovementTest : NetworkBehaviour
{
    NetworkVariable<Vector2> position = new NetworkVariable<Vector2>(new Vector2(0, 5), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    Rigidbody2D rb;
    bool rbNotFoundYet;

    InputAction moveAction;
    InputAction jumpAction;

    // Movement values
    [SerializeField] float jumpForce = 5;
    [SerializeField] float moveSpeed;
    [SerializeField] Vector2 axisClamps;

    // Jump stuff
    [SerializeField] LayerMask ground;
    float rayLength = 1.25f;
    bool isGrounded;

    PlayerMode currentMode;

    [SerializeField] Transform eyePivot;


    public override void OnNetworkSpawn()
    {
        //position.OnValueChanged += (Vector2 previousPos, Vector2 nextPos) => ;

        if (!IsOwner)
        {
            if (rb != null)
            {
                rb.simulated = false;
            }
            
            else
            {
                rbNotFoundYet = true;
            }
        }
    }

    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");

        rb = GetComponent<Rigidbody2D>();

        if (rbNotFoundYet)
        {
            rb.simulated = false; // if rb isnt referenced in OnNetworkSpawn, try again in start
            rbNotFoundYet = false;
        }
    }

    void UpdateNetworkPosition()
    {
        transform.position = Vector3.Lerp(transform.position, position.Value, Time.deltaTime * 25); // Keeps player movement smooth on other clients
    }

    private void Update()
    {
        if (!IsOwner)
        {
            UpdateNetworkPosition();
            return;
        }

        Move();
        JumpCheck();
        ClampVelocity();
        LookAtMouse();
    }

    void Move()
    {
        float moveDir = moveAction.ReadValue<Vector2>().x;

        rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocityY);
        position.Value = transform.position;
    }

    void ClampVelocity()
    {
        if (Mathf.Abs(rb.linearVelocityY) > axisClamps.y)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, Mathf.Clamp(rb.linearVelocityY, -axisClamps.y, axisClamps.y));
        }

        if (Mathf.Abs(rb.linearVelocityX) > axisClamps.x)
        {
            rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocityX, -axisClamps.x, axisClamps.x), rb.linearVelocityY);
        }
    }

    void JumpCheck()
    {
        DoGroundRay();

        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        // Stops jump early if not already falling
        if (jumpAction.WasReleasedThisFrame() && !isGrounded && !IsFalling(3f))
        {
            // stops y velocity and adds extra force to make it seem more like an arc rather than an instant stop
            rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
            rb.AddForce(Vector2.up * (jumpForce * 0.3f), ForceMode2D.Impulse);
        }
    }
    void DoGroundRay()
    {
        if (Physics2D.Raycast(transform.position, Vector2.down, rayLength, ground))
        {
            Debug.DrawRay(transform.position, Vector2.down * rayLength, Color.green);
            isGrounded = true;
        }
        else
        {
            Debug.DrawRay(transform.position, Vector2.down * rayLength, Color.red);
            isGrounded = false;
        }
    }

    bool IsFalling()
    {
        if (rb.linearVelocity.y < 0)
        {
            return true;
        }
        return false;
    }
    bool IsFalling(float offset)
    {
        if (rb.linearVelocity.y < offset)
        {
            return true;
        }
        return false;
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

        eyePivot.rotation = Quaternion.Euler(new Vector3(0, 0, lookAngle));
    }
}