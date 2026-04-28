using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum Direction
{
    Down,
    Up,
    Left, 
    Right
}

public class BorderDetection : NetworkBehaviour
{
    PlayerHealth healthScript;
    PlayerMovement moveScript;
    Rigidbody2D rb;

    [SerializeField] ParticleSystem borderDeathEffect;

    float borderOffset = 0.5f;

    void Start()
    {
        healthScript = GetComponent<PlayerHealth>();
        moveScript = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (IsHost)
        {
            if (TilemapGen.Instance != null && InGameManager.Instance != null)
            {
                if (SceneManager.GetActiveScene().name == "RanGen")
                {
                    // checks if the round is properly started and the player is alive
                    if (healthScript.isAlive.Value && InGameManager.Instance.clientsReady && moveScript.canMove.Value)
                    {
                        if (transform.position.y < -borderOffset)
                            BorderHit(Direction.Down);
                        else if (transform.position.y > TilemapGen.Instance.height + borderOffset)
                            BorderHit(Direction.Up);
                        else if (transform.position.x < -borderOffset)
                            BorderHit(Direction.Left);
                        else if (transform.position.x > TilemapGen.Instance.width + borderOffset)
                            BorderHit(Direction.Right);
                    }
                }
            }
        }
    }

    void BorderHit(Direction dir)
    {
        Vector2 knockDir = Vector2.zero;

        float angle = 0;
        Vector2 particlePosition = Vector2.zero;

        switch (dir)
        {
            case Direction.Down:
                angle = 180;
                particlePosition = new Vector2(transform.position.x, -borderOffset);
                knockDir = Vector2.up;
                break;
            case Direction.Up:
                angle = 0;
                particlePosition = new Vector2(transform.position.x, TilemapGen.Instance.height + borderOffset);
                knockDir = Vector2.down;
                break;
            case Direction.Left:
                angle = 90;
                particlePosition = new Vector2(-borderOffset, transform.position.y);
                knockDir = Vector2.right;
                break;
            case Direction.Right:
                angle = 270;
                particlePosition = new Vector2(TilemapGen.Instance.width + borderOffset, transform.position.y);
                knockDir = Vector2.left;
                break;
        }

        healthScript.TakeDamageRPC(1);

        // if still alive after taking damage
        if (healthScript.health.Value > 0)
        {
            // Add knockback away from border
            SendRBForceRPC(knockDir * 50);
        }
        else
        {
            // play death effect away from border
            SendBorderParticleRPC(angle, particlePosition);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendBorderParticleRPC(float angle, Vector3 pos)
    {
        ParticleSystem particle = Instantiate(borderDeathEffect, pos, Quaternion.identity);

        particle.transform.eulerAngles = new Vector3(0, 0, angle);

        particle.Play();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendRBForceRPC(Vector3 force)
    {
        if (IsOwner)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(force, ForceMode2D.Impulse);
        }
    }
}
