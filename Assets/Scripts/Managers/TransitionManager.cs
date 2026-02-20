using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [SerializeField] Material transitionMat;
    [HideInInspector] public float fillAmount;

    [SerializeField] float transitionSpeed = 1;
    bool loadingScene;

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
        //SceneManager.sceneLoaded += Test();
        SceneManager.activeSceneChanged += EndTransition;
    }

    private void Update()
    {
        int isLoading = loadingScene ? 1 : -1;

        fillAmount += Time.deltaTime * isLoading * transitionSpeed;
        fillAmount = Mathf.Clamp01(fillAmount);

        transitionMat.SetFloat("_FillAmount", fillAmount);

        //if (loadingScene && fillAmount >= 1)
        //{
        //    LoadScene();
        //}
    }

    void EndTransition(Scene scene, Scene _scene)
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
        //yield return new WaitForSeconds(transitionSpeed / 2);

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

    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneDelay(sceneIndex));
    }
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneDelay(sceneName));
    }

    IEnumerator LoadSceneDelay(int sceneIndex)
    {
        loadingScene = true;

        yield return new WaitForSeconds(transitionSpeed / 2);

        NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetSceneByBuildIndex(sceneIndex).name, LoadSceneMode.Single);
    }
    IEnumerator LoadSceneDelay(string sceneName)
    {
        loadingScene = true;

        yield return new WaitForSeconds(transitionSpeed / 2);

        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
