using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NameTagTest : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI nameTagUI;
    private NetworkVariable<FixedString64Bytes> nameTag = new NetworkVariable<FixedString64Bytes>("Null", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            nameTag.Value = PlayerPrefs.GetString("Username", "Player " + NetworkObjectId);
        }

        nameTagUI.text = nameTag.Value.ToString();
    }
}
