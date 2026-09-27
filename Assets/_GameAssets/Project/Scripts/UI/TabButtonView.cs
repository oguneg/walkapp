using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
namespace OgunWorks.UI
{
    public class TabButtonView : MonoBehaviour
    {
        public TabType tabType;
        public UnityAction<TabButtonView> OnButtonClicked;
        [SerializeField] private TextMeshProUGUI textField;
        [SerializeField] private GameObject notificationDot;
        [SerializeField] private Image background;
        [SerializeField] private Color activeColor = new Color32(0x2B, 0x6C, 0xC4, 0xFF);
        [SerializeField] private Color inactiveColor = new Color32(0x13, 0x40, 0x74, 0xFF);
        [SerializeField] private Color activeTextColor = Color.white;
        [SerializeField] private Color inactiveTextColor = new Color32(0xAF, 0xC3, 0xDC, 0xFF);

        public void OnButton()
        {
            OnButtonClicked?.Invoke(this);
        }

        public void Activate()
        {
            textField.fontStyle = FontStyles.Bold;
            textField.color = activeTextColor;
            if (background) background.color = activeColor;
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.06f, 0.25f, 5, 0.5f);
        }

        public void Deactivate()
        {
            textField.fontStyle = FontStyles.Normal;
            textField.color = inactiveTextColor;
            if (background) background.color = inactiveColor;
        }

        public void SetNotificationDotStatus(bool isActive)
        {
            notificationDot.SetActive(isActive);
        }
    }

    public enum TabType
    {
        JobList, ActiveJobs, Stats, Upgrades, Fleet
    }
}
