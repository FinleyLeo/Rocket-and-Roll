using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbySceneManager : NetworkBehaviour
{
    //[SerializeField] TextMeshProUGUI roomNameText;
    //[SerializeField] TextMeshProUGUI roomCodeText;
    [SerializeField] Button startGameButton;

    bool canStartGame;

    private void Start()
    {
        startGameButton.onClick.AddListener(StartGame);
        TransitionManager.Instance.EndTransition();

        if (IsHost)
        {
            MatchManager.Instance.matchState.Value = MatchState.Lobby;
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            if (IsHost && !canStartGame && NetworkManager.Singleton.ConnectedClientsList.Count > 1)
            {
                canStartGame = true;

                startGameButton.interactable = true;
            }
        }
    }

    void StartGame()
    {
        TransitionManager.Instance.LoadScene("RanGen");
    }
}
