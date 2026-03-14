using DG.Tweening;
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
    [SerializeField] private TextMeshProUGUI bankedStepsText;
    [SerializeField] private RectTransform tabPos, tabLeftPos, tabRightPos;

    private const float tabMoveSpeed = 0.2f;
    
    private int activeTabIndex;
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
            tab.transform.localPosition = tabPos.localPosition;
            tab.Deactivate();
            tab.gameObject.SetActive(false);
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
        this.DOKill();
        var isComingFromRight = (int)tabType >= activeTabIndex;
        ShowTab(tabs[(int)tabType], isComingFromRight);
        HideTab(activeTab, !isComingFromRight);
        activeTab = tabs[(int)tabType];
        activeTab.Activate();
        activeTabIndex = (int)tabType;
    }

    private void ShowTab(TabView tab, bool isComingFromRight)
    {
        tab.transform.position = (isComingFromRight ? tabRightPos : tabLeftPos).position;
        tab.gameObject.SetActive(true);
        tab.transform.DOMove(tabPos.position, tabMoveSpeed).SetEase(Ease.InOutSine).SetTarget(this);
    }

    private void HideTab(TabView tab, bool isGoingRight)
    {
        tab?.transform.DOMove((isGoingRight ? tabRightPos : tabLeftPos).position, tabMoveSpeed).SetEase(Ease.InOutSine).SetTarget(this).OnComplete(()=>tab.gameObject.SetActive(false));
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
