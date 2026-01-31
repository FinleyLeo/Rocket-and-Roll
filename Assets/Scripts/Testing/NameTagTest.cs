using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NameTagTest : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI nameTagUI;

    public override void OnNetworkSpawn()
    {
        nameTagUI.text = "Player " + NetworkObjectId;
    }
}
