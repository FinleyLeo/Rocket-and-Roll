using System.Collections;
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
    public NetworkVariable<int> selectedPatternIndex = new();

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
        selectedPaletteIndex.OnValueChanged += (int prev, int current) =>
        {
            selectedPalette = palettes[current];
        };

        selectedPatternIndex.OnValueChanged += (int prev, int current) =>
        {
            backgroundMat.SetFloat("_Type", current);
        };

        StartCoroutine(RandomisePalette());
        StartCoroutine(RandomisePattern());

        selectedPlayerColour = playerColours[Random.Range(0, playerColours.Length)];
        selectedRPGColour = playerColours[Random.Range(0, playerColours.Length)];

        selectedPalette = palettes[selectedPaletteIndex.Value];
        SetBackgroundPattern(selectedPatternIndex.Value);
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

    // Both randomisation coroutines make sure the same pattern and palette arent chose twice
    public IEnumerator RandomisePattern()
    {
        int patternBuffer = selectedPatternIndex.Value;

        while (patternBuffer == selectedPatternIndex.Value)
        {
            selectedPatternIndex.Value = Random.Range(0, 2);

            yield return null;
        }
    }
    public IEnumerator RandomisePalette()
    {
        int paletteBuffer = selectedPaletteIndex.Value;

        while (paletteBuffer == selectedPaletteIndex.Value)
        {
            selectedPaletteIndex.Value = Random.Range(0, palettes.Length);

            yield return null;
        }
    }

    public void SetMapColours(PaletteSO palette)
    {
        tmMat.SetColor("_Base", palette.foregroundPrimary);
        tmMat.SetColor("_Outline", palette.foregroundSecondary);

        backgroundMat.SetColor("_Color", palette.backgroundPrimary);
        backgroundMat.SetColor("_Background_Color", palette.backgroundSecondary);
    }
    public void SetBackgroundPattern(int index)
    {
        backgroundMat.SetFloat("_Type", index);
    }
}
