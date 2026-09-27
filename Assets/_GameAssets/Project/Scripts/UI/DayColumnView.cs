using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>One day in the momentum strip: up to five stars stacked, and the day's name.</summary>
    public class DayColumnView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image[] stars; // bottom to top
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Color normalColor = new Color32(0x0B, 0x25, 0x45, 0x26);
        [SerializeField] private Color todayColor = new Color32(0xFF, 0xFF, 0xFF, 0x40);

        public void Show(int earned, string dayName, bool isToday, Color on, Color off)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < earned ? on : off;
            label.text = dayName;
            label.fontStyle = isToday ? FontStyles.Bold : FontStyles.Normal;
            background.color = isToday ? todayColor : normalColor;
        }
    }
}
