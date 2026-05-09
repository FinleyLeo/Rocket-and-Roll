using Unity.Netcode;
using UnityEngine;

public class ColourChangeManager : NetworkBehaviour
{
    public static ColourChangeManager Instance;

    public PaletteSO[] palettes;
    public PaletteSO selectedPalette;
    PaletteSO selectedPaletteBuffer;

    public Color selectedPlayerColour, selectedRPGColour;
    public Color[] playerColours;

    [SerializeField] Material tmMat;
    [SerializeField] Material backgroundMat;

    public NetworkVariable<int> selectedPaletteIndex = new();

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

        selectedPaletteIndex.OnValueChanged += (int prev, int current) =>
        {
            selectedPalette = palettes[current];
        };
    }

    private void Start()
    {
        selectedPaletteIndex.Value = Random.Range(0, palettes.Length);

        selectedPlayerColour =  playerColours[PlayerPrefs.GetInt("SelectedPlayerColour", Random.Range(0, playerColours.Length))];
        selectedRPGColour =  playerColours[PlayerPrefs.GetInt("SelectedPlayerColour", Random.Range(0, playerColours.Length))];

        selectedPalette = palettes[PlayerPrefs.GetInt("SelectedPalette", selectedPaletteIndex.Value)];
    }

    private void Update()
    {
        if (selectedPaletteBuffer != selectedPalette)
        {
            selectedPaletteBuffer = selectedPalette;
            SetMapColours(selectedPalette);

            Debug.Log("Palette updated to: " + selectedPalette.name);
        }
    }

    public void SetMapColours(PaletteSO palette)
    {
        tmMat.SetColor("_Base", palette.foregroundPrimary);
        tmMat.SetColor("_Outline", palette.foregroundSecondary);

        backgroundMat.SetColor("_Color", palette.backgroundPrimary);
        backgroundMat.SetColor("_Background_Color", palette.backgroundSecondary);
    }
}
