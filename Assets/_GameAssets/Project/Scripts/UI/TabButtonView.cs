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
        
        public void OnButton()
        {
            OnButtonClicked?.Invoke(this);
        }

        public void Activate()
        {
            textField.fontStyle = FontStyles.Bold;
        }

        public void Deactivate()
        {
            textField.fontStyle = FontStyles.Normal;
        }
    }

    public enum TabType
    {
        JobList, ActiveJobs, Stats, Upgrades, Fleet
    }
}