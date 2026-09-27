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
    private bool regularJobClaimable;

    private void Start()
    {
        foreach (TabButtonView tabButton in tabButtons)
        {
            tabButton.OnButtonClicked += OnTabButtonClicked;
            tabButton.Deactivate();
        }

        foreach (var tab in tabs)
        {
            tab.transform.localPosition = tabPos.localPosition;
            tab.Deactivate();
            tab.gameObject.SetActive(false);
        }

        OnTabButtonClicked(tabButtons[2]);

        var express = ExpressJobManager.instance;
        express.OnOfferCreated += _ => ShowExpressOfferPopup();
        express.OnChanged += UpdateActiveJobsDot;
        if (express.HasOffer && !express.OfferSeen) ShowExpressOfferPopup();
        UpdateActiveJobsDot();

        FleetManager.instance.OnChanged += UpdateFleetDot;
        CurrencyManager.instance.OnCurrencyAmountChanged += (type, _) =>
        {
            if (type == CurrencyType.Coin) UpdateFleetDot();
        };
        UpdateFleetDot();
    }

    /// <summary>Fuel station popup. neededFuel = what a job needs (0 to just fill up); onRefueled runs after paying.</summary>
    public void ShowRefuel(long neededFuel, System.Action onRefueled)
    {
        if (PopupManager.instance.IsShowing(PopupType.PopupRefuel)) return;
        PopupManager.instance.EnqueuePopup(PopupType.PopupRefuel,
            popup => ((PopupRefuel)popup).Initialize(neededFuel, onRefueled));
    }

    // Fleet dot: the next truck is affordable.
    private void UpdateFleetDot()
    {
        tabButtons[(int)TabType.Fleet].SetNotificationDotStatus(FleetManager.instance.CanAffordNextTruck);
    }

    private void ShowExpressOfferPopup()
    {
        if (PopupManager.instance.IsShowing(PopupType.PopupExpressOffer)) return;
        ExpressJobManager.instance.MarkOfferSeen();
        PopupManager.instance.EnqueuePopup(PopupType.PopupExpressOffer);
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
        AudioManager.instance.PlaySound(SoundType.Swipe);
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
        AudioManager.instance.PlaySound(response?SoundType.Success:SoundType.Fail);
        JobManager.instance.EndJob(response);
        jobView.ClearJobView();
        SetActiveJobTabButtonNotificationLight(false);
    }

    public void SetActiveJobTabButtonNotificationLight(bool isActive)
    {
        regularJobClaimable = isActive;
        UpdateActiveJobsDot();
    }

    // The Active Jobs dot means "something to do there": a regular job to claim, an express offer to answer,
    // or an express result to collect.
    private void UpdateActiveJobsDot()
    {
        var express = ExpressJobManager.instance;
        var job = express.Job;
        bool expressNeedsAttention = express.HasOffer ||
                                     (job != null && (job.status == ExpressStatus.Completed || job.status == ExpressStatus.Failed));
        tabButtons[(int)TabType.ActiveJobs].SetNotificationDotStatus(regularJobClaimable || expressNeedsAttention);
    }

    public void UpdateCompletedJobCount(int i)
    {
        //completedJobsText.text = $"Completed Jobs: {i}";
    }
}
