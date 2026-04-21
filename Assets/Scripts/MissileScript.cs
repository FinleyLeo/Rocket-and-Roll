using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissileScript : NetworkBehaviour
{
    //NetworkVariable<Vector2> position = new NetworkVariable<Vector2>();
    NetworkVariable<bool> trailActive = new NetworkVariable<bool>(false);
    NetworkVariable<bool> srActive = new NetworkVariable<bool>(false);
    NetworkVariable<bool> colActive = new NetworkVariable<bool>(false);

    [SerializeField] float velocityMulti = 8f;
    float constantVelocity;
    [SerializeField] float explosionForce = 5f;
    [HideInInspector] public Vector2 startVelocity;
    float lifeTime = 5;

    //public string playerId;
    public NetworkVariable<FixedString64Bytes> playerId = new();

    [SerializeField] ParticleSystem explosion;
    [SerializeField] GameObject rocketTrail;
    [SerializeField] SpriteRenderer sr;
    [SerializeField] BoxCollider2D col;

    [SerializeField] LayerMask playerLayer;

    public override void OnNetworkSpawn()
    {
        trailActive.OnValueChanged += (bool prev, bool next) => rocketTrail.SetActive(trailActive.Value);
        srActive.OnValueChanged += (bool prev, bool next) => sr.enabled = srActive.Value;
        colActive.OnValueChanged += (bool prev, bool next) => col.enabled = colActive.Value;
    }

    void Start()
    {
        if (!IsServer) return;

        constantVelocity = 0.15f;
        startVelocity *= 3;

        StartCoroutine(SetupDelay());
    }

    IEnumerator SetupDelay()
    {
        yield return new WaitForSeconds(0.03f);

        srActive.Value = true;
        colActive.Value = true;

        yield return new WaitForSeconds(0.07f);

        trailActive.Value = true;
    }

    void Update()
    {
        if (!IsServer) return;

        // If still alive after x seconds, destroy
        lifeTime -= Time.deltaTime;

        if (lifeTime <= 0)
        {
            Explode();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        MissileMovement();
    }

    void MissileMovement()
    {
        constantVelocity += velocityMulti;
        constantVelocity = Mathf.Clamp(constantVelocity, 0, 3);

        transform.position += (constantVelocity * transform.right);

        if (startVelocity.magnitude > 0.001f)
        {
            transform.position += (Vector3)startVelocity;
            startVelocity *= 0.95f;
        }
    }

    void Explode()
    {
        // If host then explode and despawn
        if (IsServer)
        {
            SendExplosionRPC();
            SendExplosionKnockbackRPC();
            Destroy(gameObject);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendExplosionRPC()
    {
        GameObject particleObj = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0)).gameObject;
        Destroy(particleObj, explosion.main.startLifetime.constant * 2);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendExplosionKnockbackRPC()
    {
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            // checks if the object is owned by the client
            if (client.PlayerObject.IsOwner)
            {
                // component references
                NetworkObject player = client.PlayerObject;
                PlayerMovement playerScript = player.GetComponent<PlayerMovement>();
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                Rigidbody2D playerRB = player.GetComponent<Rigidbody2D>();

                // calculations for knockback angle and strength based on distance from explosion
                Vector2 knockDir = (player.transform.position - transform.position);
                float distanceFromExp = Vector2.Distance(transform.position, player.transform.position);
                float reversedDistance = explosionForce - (distanceFromExp * 6f);
                reversedDistance = Mathf.Clamp(reversedDistance, 0, explosionForce);

                // only adds knockback effects if the knockback strength is above the threshold
                if (reversedDistance > 1.5f)
                {
                    playerRB.linearVelocity = Vector2.zero;

                    playerScript.airDecayTimer = 0.5f;
                    playerScript.canStopEarly = false;

                    playerRB.linearVelocity = (knockDir * reversedDistance);

                    // only take damage if force is higher than a certain threshold aka within a range
                    if (reversedDistance > 3f)
                    {
                        if (playerId.Value != playerScript.playerId && SceneManager.GetActiveScene().name != "Lobby")
                        {
                            playerHealth.TakeDamageRPC(1);
                        }
                    }
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        // checks if collided with wall or player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Only collides if hitting someone other than the shooter and if the player id is set
            if (playerId.Value != collision.gameObject.GetComponent<PlayerMovement>().playerId && !string.IsNullOrEmpty(playerId.ToString()))
            {
                Explode();
            }
        }

        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Missile"))
        {
            Explode();
        }
    }
}
