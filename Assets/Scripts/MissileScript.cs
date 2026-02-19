using System.Collections;
using UnityEngine;

public class MissileScript : MonoBehaviour
{
    float velocity;
    float lifeTime = 5;

    public string playerId;

    [SerializeField] ParticleSystem explosion;

    void Start()
    {
        velocity = 4;
    }

    void Update()
    {
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
        velocity += Time.deltaTime * 10f;
        velocity = Mathf.Clamp(velocity, 0, 30);

        transform.position += (Time.deltaTime * velocity * transform.right);
    }

    void Explode()
    {
        GameObject particleObj = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0)).gameObject;
        Destroy(particleObj, explosion.main.startLifetime.constant);

        Destroy(gameObject);

        Debug.Log("Exploded");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Collided");

        // checks if collided with wall or player
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Collided with a player");

            // Only collides if hitting someone other than the shooter
            if (playerId != collision.gameObject.GetComponent<PlayerMovement>().playerId)
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
