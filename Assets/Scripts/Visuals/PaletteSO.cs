using UnityEngine;

[CreateAssetMenu(fileName = "PaletteSO", menuName = "Scriptable Objects/PaletteSO")]
public class PaletteSO : ScriptableObject
{
    public Color backgroundPrimary;
    public Color backgroundSecondary;
    public Color foregroundPrimary;
    public Color foregroundSecondary;
}
