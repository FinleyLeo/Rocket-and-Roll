using Unity.Netcode;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;

enum PlayerMode
{
    Normal,
    Balled
}

public class PlayerMovement : NetworkBehaviour
{
    NetworkVariable<Vector2> position = new NetworkVariable<Vector2>(new Vector2(0, 5), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    InputAction moveAction;
    InputAction jumpAction;

    bool rbNotFound;
    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;

    PlayerMode currentMode;

    // Movement values
    [SerializeField] float jumpForce = 16f;
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] Vector2 axisMaxClamps;
    float moveDir;

    [SerializeField] LayerMask collideLayer;
    float rayLength = 1.1f;
    bool isGrounded;

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
                rbNotFound = true;
            }
        }
    }

    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        if (rbNotFound)
        {
            rb.simulated = false; // if rb isnt referenced in OnNetworkSpawn, try again in start
            rbNotFound = false;
        }

        // Set network animator to owner authoritative later so they can set animation states
    }

    void UpdateNetworkPosition()
    {
        transform.position = Vector3.Lerp(transform.position, position.Value, Time.deltaTime * 25); // Keeps player movement smooth on other clients
    }

    void Update()
    {
        if (!IsOwner)
        {
            UpdateNetworkPosition();
            return;
        }

        Move();
        ClampVelocity();
        JumpCheck();
        DoWallRay();

        AnimationChecks();
    }

    void Move()
    {
        moveDir = moveAction.ReadValue<Vector2>().x;

        rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocityY);
        position.Value = transform.position;
    }

    void ClampVelocity()
    {
        if (Mathf.Abs(rb.linearVelocityY) > axisMaxClamps.y)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, Mathf.Clamp(rb.linearVelocityY, -axisMaxClamps.y, axisMaxClamps.y));
        }

        if (Mathf.Abs(rb.linearVelocityX) > axisMaxClamps.x)
        {
            rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocityX, -axisMaxClamps.x, axisMaxClamps.x), rb.linearVelocityY);
        }
    }

    void JumpCheck()
    {
        DoGroundRay();

        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            anim.SetTrigger("Jump");
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
        if (Physics2D.Raycast(transform.position, Vector2.down, rayLength, collideLayer))
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

    // used to stop sticking to walls
    void DoWallRay()
    {
        bool facedDir = moveDir > 0.1f ? true : false;

        if (Physics2D.Raycast(transform.position, facedDir ? Vector2.right : Vector2.left, rayLength / 2, collideLayer))
        {
            Debug.DrawRay(transform.position, (facedDir ? Vector2.right : Vector2.left) * (rayLength / 2), Color.green);
            rb.linearVelocity = new Vector2(0, rb.linearVelocityY);
        }
        else
        {
            Debug.DrawRay(transform.position, (facedDir ? Vector2.right : Vector2.left) * (rayLength / 2), Color.red);
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

    void AnimationChecks()
    {
        sr.flipX = moveDir < 0.1f ? true : false;

        anim.SetBool("IsRunning", Mathf.Abs(moveDir) > 0 ? true : false);
        anim.SetBool("IsFalling", IsFalling(-3f));
    }
}
