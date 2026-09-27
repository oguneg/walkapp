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
    [Tooltip("Express slot on top of the Job List: next offer, dispatcher call, offers to accept.")]
    [SerializeField] private ExpressJobView expressBoardView;
    [Tooltip("Express card on the Active tab: an accepted express job until it's claimed or dismissed.")]
    [SerializeField] private ExpressJobView expressActiveView;
    [SerializeField] private TextMeshProUGUI completedJobsText;
    [SerializeField] private TextMeshProUGUI bankedStepsText;
    [SerializeField] private RectTransform tabPos, tabLeftPos, tabRightPos;
    [Tooltip("Pedometer readouts and log buttons, shown by the small DEBUG button on the Stats tab.")]
    [SerializeField] private GameObject statsDebugPanel;
    [SerializeField] private UnityEngine.UI.Button statsDebugButton;
    
    private const float tabMoveSpeed = 0.2f;
    
    private int activeTabIndex;
    private TabButtonView activeTabButton;
    private TabView activeTab;

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
        express.OnChanged += RefreshExpress;
        if (express.HasOffer && !express.OfferSeen) ShowExpressOfferPopup();
        RefreshExpress();

        DailyBonusManager.instance.OnStarsEarnedToday += (gained, _) =>
            PopupManager.instance.EnqueuePopup(PopupType.PopupStarEarned, p => ((PopupStarEarned)p).Initialize(gained));

        if (statsDebugButton)
            statsDebugButton.onClick.AddListener(() => statsDebugPanel.SetActive(!statsDebugPanel.activeSelf));

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

    /// <summary>Yes/no prompt. onConfirm runs only on the confirm button.</summary>
    public void ShowConfirm(string title, string message, string confirmLabel, string cancelLabel, System.Action onConfirm)
    {
        PopupManager.instance.EnqueuePopup(PopupType.PopupConfirm,
            popup => ((PopupConfirm)popup).Initialize(title, message, confirmLabel, cancelLabel, onConfirm));
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
        // Slide direction follows the buttons' order on screen, not the enum.
        int index = tabButtons[(int)tabType].transform.GetSiblingIndex();
        var isComingFromRight = index >= activeTabIndex;
        ShowTab(tabs[(int)tabType], isComingFromRight);
        HideTab(activeTab, !isComingFromRight);
        activeTab = tabs[(int)tabType];
        activeTab.Activate();
        activeTabIndex = index;
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

    // For buttons wired in the inspector (UnityEvents can't pass enums).
    public void OpenJobList() => ForceTab(TabType.JobList);

    public void AddJob(JobData jobData)
    {
        jobListView.AddJob(jobData);
    }

    public void DisplayActiveJob(ActiveJobSaveData job)
    {
        activeJobView.AssignJob(job);
        activeJobView.OnJobResponse = OnActiveJobResponse;
        UpdateActiveJobsDot();
    }

    public void UpdateActiveJobStatus()
    {
        activeJobView.UpdateStatus();
        UpdateActiveJobsDot();
    }

    /// <summary>No regular job: hide the panel, show the empty state.</summary>
    public void ClearActiveJob()
    {
        activeJobView.ClearJobView();
        UpdateActiveJobsDot();
    }

    /// <summary>Claim (true) or dismiss/abandon (false) the regular job.</summary>
    public void OnActiveJobResponse(ActiveJobView jobView, bool response)
    {
        var job = JobManager.instance.activeJob;
        bool acknowledgingMiss = !response && job != null && job.state == JobState.Failed;
        AudioManager.instance.PlaySound(response ? SoundType.Success : acknowledgingMiss ? SoundType.Button : SoundType.Fail);
        JobManager.instance.EndJob(response);
        jobView.ClearJobView();
        UpdateActiveJobsDot();
    }

    public void AbandonActiveJob()
    {
        if (JobManager.instance.activeJob == null) return;
        OnActiveJobResponse(activeJobView, false);
    }

    public void ClaimActiveJob()
    {
        var job = JobManager.instance.activeJob;
        if (job == null || job.state != JobState.Claimable) return;
        OnActiveJobResponse(activeJobView, true);
    }

    // Active tab dot: a job (regular or express) was delivered or missed its deadline and waits for a tap.
    private void UpdateActiveJobsDot()
    {
        var regular = JobManager.instance.activeJob;
        bool regularDone = regular != null && (regular.state == JobState.Claimable || regular.state == JobState.Failed);
        var express = ExpressJobManager.instance.Job;
        bool expressDone = express != null &&
                           (express.status == ExpressStatus.Completed || express.status == ExpressStatus.Failed);
        tabButtons[(int)TabType.ActiveJobs].SetNotificationDotStatus(regularDone || expressDone);
    }

    // An accepted express job is a job: it leaves the Job List for the Active tab until it's claimed or dismissed.
    private void RefreshExpress()
    {
        var express = ExpressJobManager.instance;
        bool hasJob = express.HasJob;
        if (expressBoardView) expressBoardView.gameObject.SetActive(!hasJob);
        if (expressActiveView) expressActiveView.gameObject.SetActive(hasJob);
        activeJobView.SetExpressRunning(hasJob);

        tabButtons[(int)TabType.JobList].SetNotificationDotStatus(express.HasOffer);
        UpdateActiveJobsDot();
    }

    public void UpdateCompletedJobCount(int i)
    {
        //completedJobsText.text = $"Completed Jobs: {i}";
    }
}
