using UnityEngine;
using OgunWorks.UI;
using TMPro;
using Random = System.Random;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] private TabButtonView[] tabButtons;
    [SerializeField] private TabView[] tabs;
    [SerializeField] private JobListView jobListView;
    [SerializeField] private ActiveJobView activeJobView;
    [SerializeField] private TextMeshProUGUI completedJobsText;
    
    private TabButtonView activeTabButton;
    private TabView activeTab;
    private void Start()
    {
        foreach (TabButtonView tabButton in tabButtons)
        {
            tabButton.OnButtonClicked += OnTabButtonClicked;
        }

        foreach (var tab in tabs)
        {
            tab.transform.localPosition = new Vector3(0,tab.transform.localPosition.y);
            tab.Deactivate();
        }
        
        OnTabButtonClicked(tabButtons[2]);
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

    public void ForceTab(TabType tabType)
    {
        OnTabButtonClicked(tabButtons[(int)tabType]);
    }

    public void AddJob(JobData jobData)
    {
        jobListView.AddJob(jobData);
    }

    public void DisplayActiveJob(ActiveJobSaveData job)
    {
        activeJobView.AssignJob(job);
        activeJobView.OnJobResponse = OnActiveJobResponse;
    }

    public void UpdateActiveJobStatus()
    {
        activeJobView.UpdateStatus();
    }

    public void OnActiveJobResponse(ActiveJobView jobView, bool response)
    {
        JobManager.instance.EndJob(response);
        jobView.ClearJobView();
    }

    public void UpdateCompletedJobCount(int i)
    {
        //completedJobsText.text = $"Completed Jobs: {i}";
    }
}
