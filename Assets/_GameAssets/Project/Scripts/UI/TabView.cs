using UnityEngine;

namespace OgunWorks.UI
{
    public class TabView : MonoBehaviour
    {
        public TabType tabType;

        public void Activate()
        {
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }
    }
}