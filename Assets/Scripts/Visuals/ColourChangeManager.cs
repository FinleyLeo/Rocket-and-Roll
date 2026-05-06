using UnityEngine;

public class ColourChangeManager : MonoBehaviour
{
    public static ColourChangeManager Instance;

    [SerializeField] PaletteSO[] palettes;
    public PaletteSO selectedPalette;
    public Color selectedPlayerColour, selectedRPGColour;
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
        selectedPlayerColour =  playerColours[PlayerPrefs.GetInt("SelectedPlayerColour", Random.Range(0, playerColours.Length))];
        selectedRPGColour =  playerColours[PlayerPrefs.GetInt("SelectedPlayerColour", Random.Range(0, playerColours.Length))];
        selectedPalette = palettes[PlayerPrefs.GetInt("SelectedPalette", 0)];
    }
}
