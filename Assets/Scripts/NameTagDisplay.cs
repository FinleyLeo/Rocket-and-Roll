using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NameTagDisplay : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI nameTagUI;
    NetworkVariable<FixedString64Bytes> nameTag = new NetworkVariable<FixedString64Bytes>("Unknown", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public override void OnNetworkSpawn()
    {
        nameTag.OnValueChanged += (FixedString64Bytes previousPos, FixedString64Bytes nextPos) => UpdateTag();

        if (IsOwner)
        {
            nameTag.Value = PlayerPrefs.GetString("Username", "Player " + NetworkObjectId);
        }

        UpdateTag();
    }

    private void Update()
    {
        bool canDisplay = Keyboard.current.tabKey.isPressed;

        if (NetworkManager.Singleton != null)
        {
            DisplayTags(canDisplay);
        }
    }

    void UpdateTag()
    {
        nameTagUI.text = nameTag.Value.ToString();
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
