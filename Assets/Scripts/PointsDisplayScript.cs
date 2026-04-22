using TMPro;
using UnityEngine;

public class PointsDisplayScript : MonoBehaviour
{
    // player id used to keep track of who owns the display, and what colour to set it to
    public string playerId;
    public int points;

    [SerializeField] TextMeshProUGUI pointDisplay;

    public void UpdatePointCount(int pointAmount)
    {
        pointDisplay.text = pointAmount.ToString();
        points = pointAmount;
    }
}
