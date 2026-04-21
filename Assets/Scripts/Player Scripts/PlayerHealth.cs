using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 1;
    public int health;
    //public bool isAlive;

    public NetworkVariable<bool> isAlive = new NetworkVariable<bool>();
    public NetworkVariable<int> points = new NetworkVariable<int>();

    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator anim;
    PlayerMovement moveScript;

    [SerializeField] Sprite ghostSprite;
    [SerializeField] TrailRenderer ghostTrail;

    [SerializeField] GameObject rpgObj, eyesObj, emptyHandsObj;

    float alphaAmount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        moveScript = GetComponent<PlayerMovement>();

        ghostTrail.enabled = false;
    }

    private void Start()
    {
        health = maxHealth;

        alphaAmount = 1f;
    }

    private void Update()
    {
        if (!isAlive.Value)
        {
            GhostVisual();
        }
        else
        {
            // Reset all ghost visuals
            if (sr.color.a != 1f)
            {
                SetAlpha(1);
            }
        }

        if (Keyboard.current.rKey.wasPressedThisFrame && IsOwner)
        {
            if (!isAlive.Value)
            {
                SendRespawnRPC();
            }
            else
            {
                SendDieRPC();
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void TakeDamageRPC(int damage)
    {
        health -= damage;

        if (health <= 0)
        {
            SendDieRPC();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendDieRPC()
    {
        if (IsHost)
        {
            ModifyAliveStateRPC(false);
        }

        rb.linearVelocity = Vector3.zero;
        sr.sprite = ghostSprite;

        // Disable components
        rb.simulated = false;
        GetComponent<Collider2D>().enabled = false;
        anim.enabled = false;

        // Switch to death visuals
        eyesObj.SetActive(false);
        rpgObj.SetActive(false);
        emptyHandsObj.SetActive(true);

        if (IsOwner)
        {
            moveScript.knockBacked.Value = false;

            if (InGameManager.Instance.playersAlive.Value > 0)
            {
                InGameManager.Instance.ModifyPlayersAliveRPC(-1);
            }
        }

        ghostTrail.enabled = true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SendRespawnRPC()
    {
        if (IsHost)
        {
            ModifyAliveStateRPC(true);
        }

        health = maxHealth;

        // Enable components
        rb.simulated = true;
        GetComponent<Collider2D>().enabled = true;
        anim.enabled = true;

        // Revert visuals
        alphaAmount = 1f;
        eyesObj.SetActive(true);
        rpgObj.SetActive(true);
        emptyHandsObj.SetActive(false);

        ghostTrail.enabled = false;
    }

    [Rpc(SendTo.Server)]
    public void ModifyAliveStateRPC(bool state)
    {
        isAlive.Value = state;
    }

    void GhostVisual()
    {
        // Adds wispy ghost movement upwards
        if (IsOwner)
        {
            Vector2 moveDir = new Vector2(Mathf.Sin(Time.time * 5) * 2f, 2f);
            transform.Translate(moveDir * Time.deltaTime);
        }

        // Gradually fades out the players sprite
        alphaAmount -= Time.deltaTime * 0.75f;
        alphaAmount = Mathf.Clamp01(alphaAmount);

        SetAlpha(alphaAmount);
    }

    public void SetAlpha(float alpha)
    {
        SpriteRenderer[] children = GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer child in children)
        {
            child.color = new Color(1, 1, 1, alpha);
        }

        // Set alpha values of ghost trail
        ghostTrail.startColor = new Color(1, 1, 1, alpha);
    }
}
