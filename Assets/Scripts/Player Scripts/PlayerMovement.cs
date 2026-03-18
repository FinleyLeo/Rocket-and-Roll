using System;
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

    public string playerId;

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
    [HideInInspector] public bool isGrounded;

    [SerializeField] Vector3 groundCastOffset;
    [SerializeField] Vector3 ballGroundCastOffset;

    [SerializeField] Vector2 wallCastOffset;
    [SerializeField] Vector2 wallCastScale;
    [SerializeField] Vector2 ballWallCastScale;

    bool canBallHop;
    [SerializeField] public bool canStopEarly;
    Vector2 storedBallVelocity;

    [SerializeField] float bufferTime;
    float bufferTimer;

    [SerializeField] bool airVelocityDecay;
    public float airDecayTimer;

    [SerializeField] ParticleSystem smokeTrail;

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
        VelocityDecay();
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
        ClampVelocity();
    }

    void Move()
    {
        if (!inFullRoll)
        {
            if (Mathf.Abs(moveDir) > 0)
            {
                float modifiedSpeed = !airVelocityDecay ? 2 : moveSpeed;

                if (Mathf.Abs(rb.linearVelocityX) < moveSpeed)
                {
                    rb.linearVelocity += new Vector2(moveDir * modifiedSpeed, 0);
                }
                else
                {
                    // if moving in same direction as input then decrease more gradually
                    if (Mathf.Sign(moveDir) == Mathf.Sign(rb.linearVelocityX))
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocityX - (moveDir * 0.25f), rb.linearVelocity.y);
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocityX + (moveDir), rb.linearVelocity.y);
                    }
                }
            }
        }
        else
        {
            storedBallVelocity = rb.linearVelocity;

            // Increase max X axis speed when in ball mode
            axisMaxClamps.x = 20f;

            float modifiedSpeed = moveSpeed;

            if (Mathf.Sign(moveDir) != Mathf.Sign(rb.linearVelocityX))
            {
                modifiedSpeed *= 3f;
            }

            float acceleration = modifiedSpeed * 0.06f;

            if (Mathf.Abs(moveDir) > 0)
            {
                rb.linearVelocity = new Vector2(storedBallVelocity.x + (moveDir * acceleration), rb.linearVelocityY);
            }
        }
    }

    void VelocityDecay()
    {
        if (airDecayTimer <= 0)
        {
            if (!airVelocityDecay)
            {
                smokeTrail.Stop();
                airVelocityDecay = true;
            }
        }
        else
        {
            if (airVelocityDecay)
            {
                smokeTrail.Play();
                airVelocityDecay = false;
            }

            airDecayTimer -= Time.deltaTime;
        }

        if (Mathf.Abs(moveDir) == 0)
        {
            if (inFullRoll)
            {
                if (isGrounded)
                {
                    // Decays when on ground
                    rb.linearVelocity = new Vector2(rb.linearVelocityX * 0.99f, rb.linearVelocityY);
                }
            }
            else
            {
                if (isGrounded)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocityX * 0.95f, rb.linearVelocityY);
                }
                else
                {
                    if (airVelocityDecay)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocityX * 0.975f, rb.linearVelocityY);
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocityX * Mathf.Clamp(0.985f + airDecayTimer, 0.5f, 1f), rb.linearVelocityY);
                    }
                }
            }
        }

        // When dropping back into regular amount, set max clamp back to normal
        if (Mathf.Abs(rb.linearVelocityX) < 15)
        {
            axisMaxClamps.x = 15f;
        }
    }

    void ClampVelocity()
    {
        float modifiedXClamp = !airVelocityDecay ? 10 : axisMaxClamps.x;

        if (Mathf.Abs(rb.linearVelocityY) > axisMaxClamps.y)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocityX, Mathf.Clamp(rb.linearVelocityY, -axisMaxClamps.y, axisMaxClamps.y));
        }

        if (Mathf.Abs(rb.linearVelocityX) > modifiedXClamp)
        {
            rb.linearVelocity = new Vector2(Mathf.Clamp(rb.linearVelocityX, -modifiedXClamp, modifiedXClamp), rb.linearVelocityY);
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
        float jumpForceMulti = rollState == RollState.Balled ? 0.8f : 1f;

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
        CapsuleCollider2D playerCol = GetComponent<CapsuleCollider2D>();

        // Ground check ray
        if (Physics2D.BoxCast(transform.position + (!inFullRoll ? groundCastOffset : ballGroundCastOffset), new Vector3(playerCol.bounds.size.x - 0.1f, 0.2f), 0, Vector2.down, 0.1f, collideLayer))
        {
            if (!isGrounded)
            {
                isGrounded = true;

                if (inFullRoll)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocityX, -(rb.linearVelocityY * 0.6f));
                }
            }
        }
        else
        {
            if (isGrounded)
            {
                isGrounded = false;
            }
        }

        float ballTransRayLength = 1.8f;

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
                rollState = RollState.Balled;

                anim.SetBool("IsRolling", true);

                if (MathF.Abs(moveDir) > 0)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocityX + (moveDir * moveSpeed), rb.linearVelocityY);
                }
            }
        }
        else if (!rollAction.IsPressed()) // enter normal state
        {
            if (rollState != RollState.Normal)
            {
                rollState = RollState.Normal;

                // if grounded or in an out transition state
                if (isGrounded || canBallHop)
                {
                    // Small force added when switching back on ground to make transition smoother
                    rb.linearVelocity = new Vector2(rb.linearVelocityX, 0);
                    rb.AddForce(jumpForce * 0.9f * Vector2.up + moveDir * moveSpeed * Vector2.right, ForceMode2D.Impulse);
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

        playerVisualScript.rotationSpeed = -(rb.linearVelocityX * (Mathf.PI * 10) * Time.deltaTime);

        anim.SetBool("IsRunning", Mathf.Abs(moveDir) > 0);
        anim.SetBool("IsFalling", IsFalling(-3));
        anim.SetBool("IsGrounded", isGrounded);
    }

    private void OnDrawGizmos()
    {
        CapsuleCollider2D playerCol = GetComponent<CapsuleCollider2D>();

        Gizmos.DrawCube(transform.position + (!inFullRoll ? groundCastOffset : ballGroundCastOffset), new Vector3(playerCol.bounds.size.x - 0.1f, 0.2f));
        //Gizmos.DrawCube(transform.position + new Vector3(wallCastOffset.x, !inFullRoll ? wallCastOffset.y : 0), !inFullRoll ? wallCastScale : ballWallCastScale);
    }
}
