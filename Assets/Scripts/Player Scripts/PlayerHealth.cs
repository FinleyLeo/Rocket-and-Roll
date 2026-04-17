using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 2;
    public int health;
    public bool isAlive;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator anim;

    [SerializeField] Sprite ghostSprite;
    [SerializeField] TrailRenderer ghostTrail;

    [SerializeField] GameObject rpgObj, eyesObj, emptyHandsObj;

    float alphaAmount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        isAlive = true;
        ghostTrail.enabled = false;
    }

    private void Start()
    {
        health = maxHealth;

        alphaAmount = 1f;
    }

    private void Update()
    {
        // Debug death
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            isAlive = !isAlive;

            SwitchAliveState();
        }

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

                Debug.Log("Set alpha to max");
            }
        }

        Debug.Log(alphaAmount);
    }

    public void TakeDamage(int damage)
    {
        health -= damage;

        Debug.Log($"Took {damage} Damage, {health} health left");

        if (health <= 0)
        {
            isAlive = false;

            SwitchAliveState();
        }
    }

    void SwitchAliveState()
    {
        // specific to death scenario
        if (!isAlive)
        {
            Debug.Log("Died");
            rb.linearVelocity = Vector3.zero;
            sr.sprite = ghostSprite;
        }
        else
        {
            alphaAmount = 1f;
        }

        // Disable components
        rb.simulated = isAlive;
        GetComponent<Collider2D>().enabled = isAlive;
        anim.enabled = isAlive;

        // Switch visuals
        eyesObj.SetActive(isAlive);
        rpgObj.SetActive(isAlive);
        emptyHandsObj.SetActive(!isAlive);

        ghostTrail.enabled = !isAlive;
    }

    void GhostVisual()
    {
        // Add wispy ghost movement upwards
        Vector2 moveDir = new Vector2(Mathf.Sin(Time.time * 5) * 2f, 2f);
        transform.Translate(moveDir * Time.deltaTime);

        // Gradually fade out player sprite
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

        ghostTrail.startColor = new Color(1, 1, 1, alpha);
    }
}
