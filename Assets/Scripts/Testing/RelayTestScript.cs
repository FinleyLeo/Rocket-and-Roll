using TMPro;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class RelayTestScript : MonoBehaviour
{
    [SerializeField] Button StartRelayButton;
    [SerializeField] Button JoinRelayButton;
    [SerializeField] Button StopRelayButton;
    [SerializeField] TMP_InputField joinCode;
    [SerializeField] TextMeshProUGUI lobbyCode;

    private void Start()
    {
        StartRelayButton.onClick.AddListener(CreateRelay);
        JoinRelayButton.onClick.AddListener(JoinRelay);
        StopRelayButton.onClick.AddListener(LeaveRelay);

    }

    private void Update()
    {
        UpdateLobbyCode();
    }

    async void CreateRelay()
    {
        await RelayManager.Instance.CreateRelay(4);
        //lobbyCode.text = RelayManager.Instance.GetJoinCode().ToString();
    }

    void JoinRelay()
    {
        RelayManager.Instance.JoinRelay(joinCode.text);
        lobbyCode.text = "";
    }

    void LeaveRelay()
    {
        RelayManager.Instance.LeaveRelay();
    }

    void UpdateLobbyCode()
    {
        //lobbyCode.text = RelayManager.Instance.GetJoinCode().ToString();
    }
}
