using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [SerializeField] Material transitionMat;
    public float fillAmount;

    [SerializeField] float transitionSpeed = 1;
    [SerializeField] bool loadingScene;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        fillAmount = 0;
    }

    private void Start()
    {
        SceneManager.LoadScene("Main Menu");
        loadingScene = true;
        fillAmount = 1;

        //SceneManager.activeSceneChanged += EndTransition;
    }

    private void Update()
    {
        int isLoading = loadingScene ? 1 : -1;

        fillAmount += Time.deltaTime * isLoading * transitionSpeed;
        fillAmount = Mathf.Clamp01(fillAmount);

        transitionMat.SetFloat("_FillAmount", fillAmount);
    }

    public void EndTransition()
    {
        if (fillAmount < 1)
        {
            StartCoroutine(EndTransDelay());
        }
        else
        {
            loadingScene = false;
        }
    }

    IEnumerator EndTransDelay()
    {
        while (fillAmount < 1)
        {
            yield return null;
        }

        loadingScene = false;
    }

    public void StartTransitionManually()
    {
        loadingScene = true;
    }

    public void LoadScene(string sceneIndex)
    {
        StartCoroutine(LoadSceneDelay(sceneIndex));
    }

    IEnumerator LoadSceneDelay(string sceneIndex)
    {
        StartTransitionManually();

        while (fillAmount < 1)
        {
            yield return null;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);
    }

    [Rpc(SendTo.NotServer)]
    public void ClientTransitionRPC()
    {
        StartTransitionManually();
    }
}