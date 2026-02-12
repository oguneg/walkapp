using UnityEngine;
using OgunWorks.UI;
using Random = System.Random;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] private TabButtonView[] tabButtons;
    [SerializeField] private TabView[] tabs;
    private TabButtonView activeTabButton;
    private TabView activeTab;
    private void Start()
    {
        foreach (TabButtonView tabButton in tabButtons)
        {
            tabButton.OnButtonClicked += OnTabButtonClicked;
        }
        OnTabButtonClicked(tabButtons[0]);
    }

    private void OnTabButtonClicked(TabButtonView tabButton)
    {
        if (activeTabButton && activeTabButton.tabType == tabButton.tabType) return;
        activeTabButton?.Deactivate();
        activeTabButton = tabButton;
        activeTabButton.Activate();
        ActivateTab(activeTabButton.tabType);
    }

    private void ActivateTab(TabType tabType)
    {
        activeTab?.Deactivate();
        activeTab = tabs[(int)tabType];
        activeTab.Activate();
    }
}
