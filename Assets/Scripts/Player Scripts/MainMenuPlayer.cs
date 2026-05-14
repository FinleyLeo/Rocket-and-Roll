using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuPlayer : MonoBehaviour
{
    public bool inMenu;

    [SerializeField] Material playerMat;
    [SerializeField] Material rpgMat;

    [SerializeField] Transform eyePivot;
    [SerializeField] Transform rpgPivot;
    Transform rpg;
    SpriteRenderer rpgSR;
    SpriteRenderer sr;
    Transform eyeTransform;

    readonly float clampDistance = 0.35f;

    [SerializeField] Sprite[] eyeSprites;

    private void Start()
    {
        eyeTransform = eyePivot.GetChild(0);
        sr = GetComponent<SpriteRenderer>();
        rpg = rpgPivot.GetChild(0);
        rpgSR = rpg.GetComponent<SpriteRenderer>();

        SetMaterialColour();
    }

    private void Update()
    {
        if (!inMenu)
        {
            LookAtMouse();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.value);
                mousePos.z = 0;

                if (Vector3.Distance(transform.position, mousePos) < 0.5f)
                {
                    SwitchEyes(eyeSprites[Random.Range(0, eyeSprites.Length)]);
                }
            }
        }
    }

    Vector3 GetMousePosition()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Vector3 convertedMousePos = Camera.main.ScreenToWorldPoint(mousePos);
        convertedMousePos.z = Camera.main.nearClipPlane;
        return convertedMousePos;
    }
    void LookAtMouse()
    {
        Vector2 eyelookDir = (GetMousePosition() - eyePivot.position).normalized;
        Vector2 rpglookDir = (GetMousePosition() - rpgPivot.position).normalized;
        float rpglookAngle = Mathf.Atan2(rpglookDir.y, rpglookDir.x) * Mathf.Rad2Deg;

        // manages eye position
        eyeTransform.position = GetMousePosition();

        // clamps to certain distance, rotating around the edge of the head
        if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
        {
            eyeTransform.localPosition = eyelookDir.normalized * (clampDistance - 0.1f);
        }

        // manages rpg rotation
        if (GetMousePosition().x < rpgPivot.position.x)
        {
            rpg.rotation = Quaternion.Euler(180, 0, -rpglookAngle);
        }
        else
        {
            rpg.rotation = Quaternion.Euler(0, 0, rpglookAngle);
        }
    }

    void SetMaterialColour()
    {
        sr.material.SetColor("_Outline", ColourChangeManager.Instance.selectedPlayerColour);
        rpgSR.material.SetColor("_Outline", ColourChangeManager.Instance.selectedRPGColour);

        SpriteRenderer[] children = sr.GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer childRend in children)
        {
            if (childRend.gameObject.name != "Shadow" && childRend.gameObject.name != "RPG")
            {
                childRend.material = sr.material;
            }
        }
    }

    public void SwitchEyes(Sprite eyeSprite)
    {
        StopCoroutine(EyeSwitchDelay(eyeSprite));
        StartCoroutine(EyeSwitchDelay(eyeSprite));
    }

    IEnumerator EyeSwitchDelay(Sprite eyeSprite)
    {
        Animator switchAnim = eyePivot.GetComponent<Animator>();

        switchAnim.Play("Player-EyeIdle"); // makes sure eye switch effects dont overlap
        switchAnim.SetTrigger("EyeSwitch");

        yield return new WaitForSeconds(0.1f);

        eyePivot.GetChild(0).GetComponent<SpriteRenderer>().sprite = eyeSprite;
    }
}
