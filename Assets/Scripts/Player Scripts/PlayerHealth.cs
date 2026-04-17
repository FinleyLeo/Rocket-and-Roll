using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 1;
    public int health;
    public bool isAlive;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator anim;

    [SerializeField] Sprite ghostSprite;
    [SerializeField] ParticleSystem ghostTrail, outlineTrail;
    [SerializeField] ParticleSystem smokeTrail;

    [SerializeField] GameObject rpgObj, eyesObj, emptyHandsObj;

    float alphaAmount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        isAlive = true;

        outlineTrail = ghostTrail.transform.GetChild(0).GetComponent<ParticleSystem>();
        ghostTrail.Stop();
    }

    private void Start()
    {
        health = maxHealth;

        alphaAmount = 1f;
    }

    private void Update()
    {
        if (!isAlive)
        {
            GhostVisual();
        }
        else
        {
            // Reset all ghost visuals
            if (sr.color.a != 1f)
            {
                SetAlpha(alphaAmount);
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
        isAlive = false;

        Debug.Log("Died");
        rb.linearVelocity = Vector3.zero;
        sr.sprite = ghostSprite;

        // Disable components
        rb.simulated = false;
        GetComponent<Collider2D>().enabled = false;
        anim.enabled = false;

        // Switch visuals
        eyesObj.SetActive(false);
        rpgObj.SetActive(false);
        emptyHandsObj.SetActive(true);

        smokeTrail.Stop();
        ghostTrail.Play();
    }

    void GhostVisual()
    {
        // Adds wispy ghost movement upwards
        Vector2 moveDir = new Vector2(Mathf.Sin(Time.time * 5) * 2f, 2f);
        transform.Translate(moveDir * Time.deltaTime);

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
        ParticleSystem.MainModule ma = ghostTrail.main;
        ParticleSystem.MainModule _ma = outlineTrail.main;

        ma.startColor = new Color(1, 1, 1, Mathf.Clamp01((alpha * 0.5f) - 0.3f));
        _ma.startColor = new Color(1, 1, 1, Mathf.Clamp01((alpha * 0.5f) - 0.5f));
    }
}
