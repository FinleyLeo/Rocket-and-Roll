using System.Collections;
using UnityEngine;

public class ShakerScript : MonoBehaviour
{
    readonly float defaultStrength = 1;
    readonly float defaultDuration = 0.5f;

    Vector3 rootPos;
    Coroutine shake;

    private void Start()
    {
        rootPos = transform.position;

        if (TryGetComponent<Camera>(out Camera cam))
        {
            // Makes sure the root position is always -10 if its a camera
            rootPos.z = -10;
        }
    }

    public void Shake()
    {
        if (shake != null)
        {
            StopCoroutine(shake);
        }

        shake = StartCoroutine(ShakeEffect(defaultStrength, defaultDuration));
    }
    public void Shake(float strength, float duration)
    {
        if (rootPos != null) // only run of the root position is set
        {
            if (shake != null)
            {
                StopCoroutine(shake);
            }

            shake = StartCoroutine(ShakeEffect(strength, duration));
        }   
    }

    public IEnumerator ShakeEffect(float strength, float duration)
    {
        float elapsedTime = 0;
        float interval = 0.05f;

        while (elapsedTime < duration)
        {
            transform.position = rootPos + (Vector3)Random.insideUnitCircle * strength;

            elapsedTime += interval;
            yield return new WaitForSeconds(interval);
        }

        transform.position = rootPos;
    }
}
