using UnityEngine;
using UnityEngine.SceneManagement;

public class MouseVisualsManager : MonoBehaviour
{
    [SerializeField] Texture2D crosshair;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
    }

    // Update is called once per frame
    void Update()
    {
        if (SceneManager.GetActiveScene().name == "Main Menu" && SceneManager.GetActiveScene().name == "Bootstrap")
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(crosshair, Vector2.zero, CursorMode.Auto);
        }
    }
}
