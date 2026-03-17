using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MissileScript : NetworkBehaviour
{
    NetworkVariable<Vector2> position = new NetworkVariable<Vector2>();
    NetworkVariable<bool> trailActive = new NetworkVariable<bool>(false);
    NetworkVariable<bool> srActive = new NetworkVariable<bool>(false);
    NetworkVariable<bool> colActive = new NetworkVariable<bool>(false);

    [SerializeField] float velocityMulti = 8f;
    [SerializeField] float constantVelocity;
    [SerializeField] float explosionForce = 5f;
    [HideInInspector] public Vector2 startVelocity;
    float lifeTime = 5;

    public string playerId;

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

        position.Value = transform.position;
        constantVelocity = 10;

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
        if (!IsServer)
        {
            transform.position = position.Value;
            return;
        }

        // If still alive after x seconds, destroy
        lifeTime -= Time.deltaTime;

        if (lifeTime <= 0)
        {
            Explode();
        }

        MissileMovement();
    }

    void MissileMovement()
    {
        constantVelocity += Time.deltaTime * velocityMulti;
        constantVelocity = Mathf.Clamp(constantVelocity, 0, 40);

        transform.position += (Time.deltaTime * constantVelocity * transform.right);
        transform.position += (Vector3)startVelocity;

        startVelocity *= 0.98f;

        position.Value = transform.position;
    }

    void Explode()
    {
        // If host then explode and despawn
        if (IsServer)
        {
            // Give explosion knockback to every player within radius
            foreach (Collider2D playerCol in Physics2D.OverlapCircleAll(transform.position, 4f, playerLayer))
            {
                PlayerMovement playerScript = playerCol.GetComponent<PlayerMovement>();
                Rigidbody2D playerRB = playerCol.attachedRigidbody;

                Vector2 knockDir = (playerCol.transform.position - transform.position);
                //Vector2 modifiedKnockDir = explosionForce - knockDir.magnitude;

                playerScript.canStopEarly = false;
                // set timer based on distance from explosion, closer means longer time for further knockback distance
                playerScript.airDecayTimer = 0.75f;

                playerRB.linearVelocity = knockDir.normalized * explosionForce;

                Debug.Log(playerCol.gameObject.name);
            }

            SendExplosionRPC();
            GetComponent<NetworkObject>().Despawn();
        }
    }

        [Rpc(SendTo.ClientsAndHost)]
    void SendExplosionRPC()
    {
        GameObject particleObj = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0)).gameObject;
        Destroy(particleObj, explosion.main.startLifetime.constant * 2);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        // checks if collided with wall or player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Only collides if hitting someone other than the shooter and if the player id is set
            if (playerId != collision.gameObject.GetComponent<PlayerMovement>().playerId && !string.IsNullOrEmpty(playerId))
            {
                Explode();
            }
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            Explode();
        }
    }
}
