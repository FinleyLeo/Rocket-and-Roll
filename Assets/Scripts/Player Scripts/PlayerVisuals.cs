using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerVisuals : NetworkBehaviour
{
    public bool layerUpdated;
    public NetworkVariable<bool> isFlipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Space(10)]
    [Header("Nametag Variables")]
    public TextMeshProUGUI usernameText;
    NetworkVariable<FixedString64Bytes> nameTag = new();
    [SerializeField] Vector3 offset = new Vector3(0, 1);
    Camera cam;

    [Space(10)]
    [Header("Look-At Variables")]
    [SerializeField] Transform eyePivot;
    [SerializeField] Transform rpgPivot;
    Transform rpg;
    Transform eyeTransform;

    InputAction lookAction;

    bool eyesLocked;
    readonly float clampDistance = 0.35f;
    public float rotationSpeed;
    Quaternion eyeStoredRotation;

    [Space(10)]
    [Header("References")]
    [SerializeField] ParticleSystem smokeTrail;

    PlayerMovement moveScript;
    PlayerShooting shootScript;
    PlayerHealth healthScript;
    Animator anim;
    SpriteRenderer sr;
    Rigidbody2D rb;

    MaterialPropertyBlock mpb;
    [SerializeField] Material material;

    public override void OnNetworkSpawn()
    {
        moveScript = GetComponent<PlayerMovement>();
        healthScript = GetComponent<PlayerHealth>();
        shootScript = GetComponentInChildren<PlayerShooting>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        lookAction = InputSystem.actions.FindAction("Look");

        eyeTransform = eyePivot.GetChild(0);
        rpg = rpgPivot.GetChild(0);

        mpb = new MaterialPropertyBlock();

        moveScript.knockBacked.OnValueChanged += (bool prev, bool next) => UpdateSmokeTrail();
        nameTag.OnValueChanged += (FixedString64Bytes before, FixedString64Bytes after) => usernameText.text = nameTag.Value.ToString();
        usernameText.text = nameTag.Value.ToString();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        if (IsOwner)
        {
            UpdateNameTagRPC(PlayerPrefs.GetString("Username", "Player " + NetworkObjectId));
        }

        UpdateLayerOrder();
        SetColour();
    }

    void OnClientConnected(ulong clientId)
    {
        UpdateLayerOrder();
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void Update()
    {
        NameTagDisplayLogic();

        if (IsOwner)
        {
            LookLogic();

            if (moveScript.canMove.Value)
            {
                AnimationChecks();
            }
            else
            {
                anim.SetBool("IsRunning", false);
                anim.SetBool("IsFalling", false);
                anim.SetBool("IsRolling", false);
                anim.SetBool("IsGrounded", true);
            }
        }
        else
        {
            SyncNetworkVisuals();
        }
    }

    void SyncNetworkVisuals()
    {
        if (Vector2.Distance(transform.position, moveScript.position.Value) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, moveScript.position.Value, Time.deltaTime * 40); // Keeps player movement smooth on other clients
        }

        sr.flipX = isFlipped.Value;
    }

    #region Nametag Visuals

    [Rpc(SendTo.Server)]
    void UpdateNameTagRPC(string value)
    {
        nameTag.Value = value;
    }

    void NameTagDisplayLogic()
    {
        bool canDisplay = Keyboard.current.tabKey.isPressed;

        if (NetworkManager.Singleton != null)
        {
            DisplayTags(canDisplay);
        }

        if (cam != null)
        {
            usernameText.transform.position = cam.WorldToScreenPoint(transform.position + offset);
        }
        else
        {
            cam = Camera.main;
        }
    }

    void DisplayTags(bool canDisplay)
    {
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            GameObject clientTagText = client.PlayerObject.GetComponentInChildren<TextMeshProUGUI>(true).gameObject;

            clientTagText.SetActive(canDisplay);
        }
    }

    #endregion

    #region Look-At Visuals

    void LookLogic()
    {
        if (healthScript.isAlive.Value)
        {
            if (PauseMenuScript.instance != null)
            {
                if (!PauseMenuScript.instance.isPaused)
                {
                    LookAtMouse();
                }
            }

            if (moveScript.inFullRoll)
            {
                eyeTransform.localPosition = eyePivot.localPosition;
                eyesLocked = true;

                eyeTransform.rotation = eyeStoredRotation;
                eyeTransform.rotation = Quaternion.Euler(0, 0, eyeTransform.rotation.eulerAngles.z + rotationSpeed);
                eyeStoredRotation = eyeTransform.rotation;
            }
            else
            {
                eyesLocked = false;

                rotationSpeed = 0;
                eyeStoredRotation = eyeTransform.rotation;
                eyeTransform.rotation = Quaternion.Euler(0, 0, 0);
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

        // Only move eyes if not lcoked in place
        if (!eyesLocked)
        {
            // manages eye position
            eyeTransform.position = GetMousePosition();

            // clamps to certain distance, rotating around the edge of the head
            if (Vector3.Distance(eyePivot.position, GetMousePosition()) > clampDistance)
            {
                eyeTransform.localPosition = eyelookDir.normalized * (clampDistance - 0.1f);
            }
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

        // Visualises cooldown through rpg recoil
        rpg.localPosition = rpglookDir * (-shootScript.cooldownTimer * 0.45f);
    }

    #endregion

    bool IsFalling(float offset)
    {
        if (rb.linearVelocity.y < offset)
        {
            return true;
        }
        return false;
    }

    void AnimationChecks()
    {
        if (Mathf.Abs(moveScript.moveDir) > 0.1f) // only update when moving
        {
            sr.flipX = moveScript.moveDir < 0.1f;
            isFlipped.Value = sr.flipX;
        }

        rotationSpeed = -(rb.linearVelocityX * (Mathf.PI * 10) * Time.deltaTime);

        anim.SetBool("IsRunning", moveScript.canMove.Value && Mathf.Abs(moveScript.moveDir) > 0);
        anim.SetBool("IsFalling", moveScript.canMove.Value && IsFalling(-3));
        anim.SetBool("IsGrounded", moveScript.canMove.Value && moveScript.isGrounded);
    }

    void UpdateSmokeTrail()
    {
        if (moveScript.knockBacked.Value)
        {
            smokeTrail.Play();
        }
        else
        {
            smokeTrail.Stop();
        }
    }

    void UpdateLayerOrder()
    {
        int index = 0;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            index++;

            if (client.PlayerObject == null) return;

            PlayerVisuals visualScript = client.PlayerObject.GetComponent<PlayerVisuals>();

            if (visualScript.layerUpdated) continue; // skip early

            int clientOrder = visualScript.IsOwner ? (NetworkManager.Singleton.ConnectedClientsList.Count + 5) * 5 : index * 5;

            Renderer[] children = visualScript.GetComponentsInChildren<Renderer>();

            foreach (Renderer childRend in children)
            {
                if (childRend.gameObject.name != "Shadow")
                {
                    childRend.sortingOrder += clientOrder;
                }
            }

            visualScript.layerUpdated = true;
        }
    }

    void SetColour()
    {
        material.SetColor("_Outline", ColourChangeManager.Instance.selectedPlayerColour);
    }
}
