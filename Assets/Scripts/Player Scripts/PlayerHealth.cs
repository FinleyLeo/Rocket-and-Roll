using JetBrains.Annotations;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 2;
    public NetworkVariable<int> health = new NetworkVariable<int>();

    [SerializeField] float iFramelength = 1f;
    NetworkVariable<bool> invincible = new NetworkVariable<bool>();
    NetworkVariable<bool> flashOn = new NetworkVariable<bool>();

    public NetworkVariable<bool> isAlive = new NetworkVariable<bool>();

    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator anim;
    PlayerMovement moveScript;

    [SerializeField] Sprite ghostSprite;
    [SerializeField] TrailRenderer ghostTrail;

    [SerializeField] GameObject rpgObj, eyesObj, emptyHandsObj;

    float alphaAmount;

    public override void OnNetworkSpawn()
    {
        flashOn.OnValueChanged += (bool prev, bool current) =>
        {
            SetAlpha(current ? 0f : 1f);
        };
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        moveScript = GetComponent<PlayerMovement>();

        ghostTrail.enabled = false;

        alphaAmount = 1f;
    }

    private void Start()
    {
        health.Value = maxHealth;
    }

    private void Update()
    {
        if (!isAlive.Value)
        {
            GhostVisual();
        }
        else if (!invincible.Value)
        {
            // Reset all ghost visuals
            if (sr.color.a != 1f)
            {
                SetAlpha(1);
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void TakeDamageRPC(int damage)
    {
        if (!invincible.Value)
        {
            health.Value -= damage;

            if (health.Value <= 0)
            {
                SendDieRPC();
            }
            else
            {
                StartCoroutine(InvincibilityEffect());
            }
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

    [Rpc(SendTo.Server)]
    public void SetHealthRPC(int value)
    {
        health.Value = value;
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

    IEnumerator InvincibilityEffect()
    {
        invincible.Value = true;

        float elapsedTime = 0f;
        float flashInterval = 0.1f;

        while (elapsedTime < iFramelength)
        {
            flashOn.Value = !flashOn.Value;

            yield return new WaitForSeconds(flashInterval);
            elapsedTime += flashInterval;
        }

        flashOn.Value = false;
        invincible.Value = false;
    }

    public void SetAlpha(float alpha)
    {
        SpriteRenderer[] children = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer child in children)
        {
            child.color = new Color(1, 1, 1, alpha);
        }

        // Set alpha values of ghost trail
        ghostTrail.startColor = new Color(1, 1, 1, alpha);
    }
}
