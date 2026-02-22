using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MissileScript : NetworkBehaviour
{
    NetworkVariable<Vector2> position = new NetworkVariable<Vector2>();
    NetworkVariable<bool> trailActive = new NetworkVariable<bool>(false);

    [SerializeField] float velocityMulti = 2f;
    float velocity;
    [SerializeField] float lifeTime = 5;

    public string playerId;

    [SerializeField] ParticleSystem explosion;
    [SerializeField] GameObject rocketTrail;

    public override void OnNetworkSpawn()
    {
        trailActive.OnValueChanged += (bool prev, bool next) => rocketTrail.SetActive(trailActive.Value);
    }

    void Start()
    {
        if (!IsServer) return;

        position.Value = transform.position;
        velocity = 4;

        StartCoroutine(TrailDelay());
    }

    IEnumerator TrailDelay()
    {
        yield return new WaitForSeconds(0.25f);

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
        velocity += Time.deltaTime * velocityMulti;
        velocity = Mathf.Clamp(velocity, 0, 30);

        transform.position += (Time.deltaTime * velocity * transform.right);
        position.Value = transform.position;
    }

    void Explode()
    {
        SendExplosionEffectRPC();

        // If host then despawn
        if (IsServer) GetComponent<NetworkObject>().Despawn();

        Debug.Log("Exploded");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendExplosionEffectRPC()
    {
        Debug.Log($"Explode called on {OwnerClientId} | Server: {IsServer}");

        GameObject particleObj = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0)).gameObject;
        Destroy(particleObj, explosion.main.startLifetime.constant);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        Debug.Log("Collided");

        // checks if collided with wall or player
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Collided with a player");

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
