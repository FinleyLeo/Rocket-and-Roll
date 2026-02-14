using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    [SerializeField] GameObject missilePrefab;

    InputAction attackAction;

    [SerializeField] float cooldown;
    float cooldownTimer;

    private void Start()
    {
        attackAction = InputSystem.actions.FindAction("Attack");
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        if (attackAction.WasPressedThisFrame() && cooldown <= 0)
        {
            cooldownTimer = cooldown;
            Instantiate(missilePrefab, transform.position, transform.rotation);
        }
    }
}
