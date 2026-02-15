using System.Collections;
using UnityEngine;

public class MissileScript : MonoBehaviour
{
    float velocity;
    float lifeTime = 5;

    public string playerId;

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
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Make sure it cant collide with person who shot, give it player id value or something
        Debug.Log("Collided");

        // checks if collided with wall or player
        if (collision.gameObject.CompareTag("Wall") || (collision.gameObject.CompareTag("Player") && collision.GetComponent<PlayerMovement>().playerId != playerId)) // Collides if is wall/player without same ID
        {
            Debug.Log("Exploded");
            Explode();
        }
    }
}
