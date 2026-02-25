using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RollState
{
    Normal,
    Balled
}

public class PlayerMovement : NetworkBehaviour
{
    NetworkVariable<Vector2> position = new NetworkVariable<Vector2>(new Vector2(0, 5), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector] public NetworkVariable<bool> isFlipped = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    InputAction moveAction;
    InputAction jumpAction;
    InputAction rollAction;

    bool rbNotFound;
    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    PlayerLookAt playerVisualScript;

    [HideInInspector] public RollState rollState;
    public bool inFullRoll;

    // Movement values
    [SerializeField] float jumpForce = 16f;
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] Vector2 axisMaxClamps;
    [HideInInspector] public float moveDir;

    [SerializeField] LayerMask collideLayer;
    [SerializeField] float groundRayLength = 0.8f;
    [SerializeField] float wallRayLength = 0.6f;
    [SerializeField] float[] wallRayChecks;
    [SerializeField] float[] ballModeWallRayChecks;
    [SerializeField] bool isGrounded;
    bool canBallHop;
    [SerializeField] bool canStopEarly;

    [SerializeField] float bufferTime;
    [SerializeField] float bufferTimer;

    public string playerId;

    [SerializeField] PhysicsMaterial2D physMat;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (rb != null)
            {
                // stops rb from fighting with movement updates while keeping collisions
                rb.gravityScale = 0f;
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
        rollAction = InputSystem.actions.FindAction("Roll");

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        playerVisualScript = GetComponent<PlayerLookAt>();

        if (rbNotFound)
        {
            // stops rb from fighting with movement updates while keeping collisions
            rb.gravityScale = 0f;
            rbNotFound = false;
        }
    }

    void UpdateNetworkValues()
    {
        if (Vector2.Distance(transform.position, position.Value) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, position.Value, Time.deltaTime * 40); // Keeps player movement smooth on other clients
        }

        sr.flipX = isFlipped.Value;
    }

    void Update()
    {
        if (!IsOwner)
        {
            UpdateNetworkValues();
            return;
        }

        moveDir = moveAction.ReadValue<Vector2>().x;

        if (PauseMenuScript.instance != null)
        {
            if (!PauseMenuScript.instance.isPaused)
            {
                JumpPreRegister();
            }
        }

        RollCheck();
        AnimationChecks();

        position.Value = transform.position;
    }

    private void FixedUpdate()
    {
        if (PauseMenuScript.instance != null)
        {
            if (!PauseMenuScript.instance.isPaused)
            {
                Move();
            }
            else
            {
                moveDir = 0;
                bufferTimer = 0;
            }
        }

        JumpCheck();
        DoWallRay();
        ClampVelocity();
    }

    void Move()
    {
        if (rollState == RollState.Normal && Mathf.Abs(rb.linearVelocityX) < (moveSpeed * 1.2f))
        {
            rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            float modifiedSpeed = moveSpeed;

            if (Mathf.Sign(moveDir) != Mathf.Sign(rb.linearVelocityX))
            {
                modifiedSpeed *= 3f;
            }

            rb.AddForce(moveDir * modifiedSpeed * Vector3.right);

            playerVisualScript.rotationSpeed = -(rb.linearVelocityX / 2);
        }
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

        if (bufferTimer > 0 && isGrounded)
        {
            Jump();
        }

        // Stops jump early if not already falling
        if (!jumpAction.IsPressed() && canStopEarly && !isGrounded && !IsFalling(3f))
        {
            canStopEarly = false;

            // stops y velocity and adds extra force to make it seem more like an arc rather than an instant stop
            rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
            rb.AddForce(Vector2.up * (jumpForce * 0.3f), ForceMode2D.Impulse);
        }
    }
    void Jump()
    {
        float jumpForceMulti = inFullRoll ? 0.8f : 1f;

        canStopEarly = true;
        bufferTimer = -1;

        rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
        rb.AddForce(jumpForce * jumpForceMulti * Vector2.up, ForceMode2D.Impulse);

        anim.SetTrigger("Jump");
    }
    void JumpPreRegister()
    {
        if (jumpAction.WasPressedThisFrame())
        {
            bufferTimer = bufferTime;
        }
        else
        {
            bufferTimer -= Time.deltaTime;
        }
    }

    void DoGroundRay()
    {
        groundRayLength = inFullRoll ? .7f : 2.35f;
        float ballTransRayLength = 1.8f;

        // Ground check ray
        if (Physics2D.Raycast(transform.position, Vector2.down, groundRayLength, collideLayer))
        {
            Debug.DrawRay(transform.position, Vector2.down * groundRayLength, Color.green);
            isGrounded = true;
        }
        else
        {
            Debug.DrawRay(transform.position, Vector2.down * groundRayLength, Color.red);
            isGrounded = false;
        }

        // Ball transition check
        if (Physics2D.Raycast(transform.position, Vector2.down, ballTransRayLength, collideLayer))
        {
            canBallHop = true;
        }
        else
        {
            canBallHop = false;
        }
    }

    void RollCheck()
    {
        if (rollAction.IsPressed()) // enter roll state
        {
            if (rollState != RollState.Balled)
            {
                Debug.Log("Switched to balled");
                rollState = RollState.Balled;

                anim.SetBool("IsRolling", true);
            }
        }
        else if (!rollAction.IsPressed()) // enter normal state
        {
            if (rollState != RollState.Normal)
            {
                Debug.Log("Switched to normal");
                rollState = RollState.Normal;

                // if grounded or in an out transition state
                if (isGrounded || canBallHop)
                {
                    // Small force added when switching back on ground to make transition smoother
                    rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
                    rb.AddForce(jumpForce * 0.9f * Vector2.up, ForceMode2D.Impulse);
                }

                anim.SetBool("IsRolling", false);
            }
        }
    }

    public void SetRollTrue()
    {
        inFullRoll = true;
    }
    public void SetRollFalse()
    {
        inFullRoll = false;
    }

    // used to stop sticking to walls
    void DoWallRay()
    {
        bool facedDir = moveDir > 0.1f ? true : false;
        wallRayLength = inFullRoll ? 0.65f : 0.55f;
        float[] rayChecks = rollState == RollState.Balled ? ballModeWallRayChecks : wallRayChecks;

        for (int i = 0; i < rayChecks.Length; i++)
        {
            if (Physics2D.Raycast(transform.position + new Vector3(0, rayChecks[i], 0), facedDir ? Vector2.right : Vector2.left, wallRayLength, collideLayer))
            {
                Debug.DrawRay(transform.position + new Vector3(0, rayChecks[i], 0), (facedDir ? Vector2.right : Vector2.left) * wallRayLength, Color.green);

                if (Mathf.Sign(moveDir) == Mathf.Sign(rb.linearVelocity.x))
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                }
            }
            else
            {
                Debug.DrawRay(transform.position + new Vector3(0, rayChecks[i], 0), (facedDir ? Vector2.right : Vector2.left) * wallRayLength, Color.red);
            }
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
        if (Mathf.Abs(moveDir) > 0.1f) // only update when moving
        {
            sr.flipX = moveDir < 0.1f;
            isFlipped.Value = sr.flipX;
        }

        anim.SetBool("IsRunning", Mathf.Abs(moveDir) > 0);
        anim.SetBool("IsFalling", IsFalling(-3));
        anim.SetBool("IsGrounded", isGrounded);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // if hitting a wall and in ball mode then bounce
        if (collision.gameObject.layer == collideLayer && inFullRoll)
        {
            if (Mathf.Abs(rb.linearVelocityX) > 1 || Mathf.Abs(rb.linearVelocityY) > 1)
            {
                rb.linearVelocity = -(rb.linearVelocity * 0.5f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        bool facedDir = moveDir > 0.1f;

        for (int i = 0; i < ballModeWallRayChecks.Length; i++)
        {
            Gizmos.DrawLine(transform.position + new Vector3(0, ballModeWallRayChecks[i], 0), transform.position + new Vector3(0, ballModeWallRayChecks[i], 0) + (facedDir ? Vector3.right : Vector3.left) * wallRayLength);
        }

        Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * groundRayLength));
    }
}
