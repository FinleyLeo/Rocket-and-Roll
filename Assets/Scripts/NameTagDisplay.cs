using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NameTagDisplay : NetworkBehaviour
{
    NetworkVariable<FixedString64Bytes> nameTag = new NetworkVariable<FixedString64Bytes>("Unknown", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    TextMeshProUGUI usernameText;

    Camera cam;

    [SerializeField] Vector3 offset;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            nameTag.Value = PlayerPrefs.GetString("Username", "Player " + NetworkObjectId);
        }

        usernameText = transform.GetComponentInChildren<TextMeshProUGUI>();

        nameTag.OnValueChanged += (FixedString64Bytes before, FixedString64Bytes after) => usernameText.text = nameTag.Value.ToString();

        usernameText.text = nameTag.Value.ToString();
    }
    private void Update()
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
}
