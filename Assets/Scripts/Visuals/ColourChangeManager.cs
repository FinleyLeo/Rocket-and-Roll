using UnityEngine;

public class ColourChangeManager : MonoBehaviour
{
    public static ColourChangeManager Instance;

    [SerializeField] PaletteSO[] palettes;
    public PaletteSO selectedPalette;
    public Color selectedPlayerColour;
    public Color[] playerColours;

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
    }

    private void Start()
    {
        selectedPlayerColour =  playerColours[PlayerPrefs.GetInt("SelectedPlayerColour", 0)];
        selectedPalette = palettes[PlayerPrefs.GetInt("SelectedPalette", 0)];
    }
}
