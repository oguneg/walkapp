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

    // Tabs revealed by a level-up keep a dot until they're first opened.
    private readonly System.Collections.Generic.HashSet<TabType> newTabs = new System.Collections.Generic.HashSet<TabType>();
    private bool fleetAttention, jobListAttention, activeAttention;

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

        var progression = ProgressionManager.instance;
        RefreshTabUnlocks(announce: false);
        progression.OnUnlocksChanged += () => RefreshTabUnlocks(announce: true);
        OnTabButtonClicked(tabButtons[(int)(progression.IsUnlocked(Feature.DailyStars) ? TabType.Stats : TabType.JobList)]);

        var express = ExpressJobManager.instance;
        express.OnOfferCreated += _ => ShowExpressOfferPopup();
        express.OnChanged += RefreshExpress;
        if (express.HasOffer && !express.OfferSeen) ShowExpressOfferPopup();
        RefreshExpress();

        DailyBonusManager.instance.OnStarsEarnedToday += (gained, _) =>
        {
            if (!ProgressionManager.instance.IsUnlocked(Feature.DailyStars)) return;
            PopupManager.instance.EnqueuePopup(PopupType.PopupStarEarned, p => ((PopupStarEarned)p).Initialize(gained));
        };

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
        fleetAttention = FleetManager.instance.CanAffordNextTruck;
        UpdateDot(TabType.Fleet);
    }

    private void UpdateDot(TabType type)
    {
        bool attention = newTabs.Contains(type) ||
                         (type == TabType.Fleet && fleetAttention) ||
                         (type == TabType.JobList && jobListAttention) ||
                         (type == TabType.ActiveJobs && activeAttention);
        tabButtons[(int)type].SetNotificationDotStatus(attention);
    }

    // Tabs appear with the player's level. A tab revealed by a level-up pops in and gets a dot until opened.
    private void RefreshTabUnlocks(bool announce)
    {
        var progression = ProgressionManager.instance;
        SetTabUnlocked(TabType.Upgrades, progression.IsUnlocked(Feature.UpgradesTab), announce);
        SetTabUnlocked(TabType.Stats, progression.IsUnlocked(Feature.DailyStars), announce);
        SetTabUnlocked(TabType.Fleet, progression.IsUnlocked(Feature.FleetTab), announce);
        RefreshExpress();
    }

    private void SetTabUnlocked(TabType type, bool unlocked, bool announce)
    {
        var button = tabButtons[(int)type];
        bool wasVisible = button.gameObject.activeSelf;
        button.gameObject.SetActive(unlocked);
        if (!unlocked)
        {
            newTabs.Remove(type);
            return;
        }

        if (announce && !wasVisible)
        {
            newTabs.Add(type);
            button.transform.DOKill(true);
            button.transform.localScale = Vector3.zero;
            button.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.2f);
        }

        UpdateDot(type);
    }

    /// <summary>One-button message (level ups).</summary>
    public void ShowMessage(string title, string message, string buttonLabel)
    {
        ShowConfirm(title, message, buttonLabel, null, null);
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
        if (newTabs.Remove(tabButton.tabType)) UpdateDot(tabButton.tabType);
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

    /// <summary>featured: put it on top, replacing the bottom offer when the list is full.</summary>
    public void AddJob(JobData jobData, bool featured = false)
    {
        if (featured) jobListView.AddFeaturedJob(jobData);
        else jobListView.AddJob(jobData);
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

    /// <summary>
    /// One job at a time, regular or express. Frees the slot, then runs <paramref name="take"/>:
    /// a missed job is just cleared, a delivered one is claimed and a running one dropped, both after asking.
    /// </summary>
    public void RequestJobSlot(string newJob, System.Action take)
    {
        var express = ExpressJobManager.instance;
        var ex = express.Job;
        if (ex != null)
        {
            switch (ex.status)
            {
                case ExpressStatus.Failed:
                    express.Dismiss();
                    take();
                    return;
                case ExpressStatus.Completed:
                    ShowConfirm("CLAIM FIRST",
                        $"Your <b>{ex.offer.cargoType}</b> express delivery is waiting to be claimed.\n\nClaim <sprite=0>{DailyBonusManager.instance.ApplyBonus(ex.offer.reward):N0} and take <b>{newJob}</b>?",
                        "CLAIM & TAKE", "NOT NOW",
                        () =>
                        {
                            if (express.Job != ex) return;
                            AudioManager.instance.PlaySound(SoundType.Success);
                            express.Claim();
                            take();
                        });
                    return;
                default:
                    ShowConfirm("EXPRESS IN PROGRESS",
                        $"You're on a rush delivery: <b>{ex.offer.cargoType}</b>, {ex.bestProgress:N0} / {ex.offer.targetSteps:N0} steps.\n\nDrop it and take <b>{newJob}</b>? The express job and its pay will be lost.",
                        "REPLACE", "KEEP EXPRESS",
                        () =>
                        {
                            if (express.Job != ex) return;
                            express.Abandon();
                            take();
                        });
                    return;
            }
        }

        var current = JobManager.instance.activeJob;
        if (current == null)
        {
            take();
            return;
        }

        switch (current.state)
        {
            case JobState.Failed:
                // Missed its deadline: nothing left to lose, just clear it.
                AbandonActiveJob();
                take();
                return;
            case JobState.Claimable:
                ShowConfirm("CLAIM FIRST",
                    $"Your <b>{current.jobData.cargoType}</b> delivery is waiting to be claimed.\n\nClaim <sprite=0>{DailyBonusManager.instance.ApplyBonus(current.jobData.reward):N0} and take <b>{newJob}</b>?",
                    "CLAIM & TAKE", "NOT NOW",
                    () =>
                    {
                        if (JobManager.instance.activeJob != current) return;
                        ClaimActiveJob();
                        take();
                    });
                return;
            default:
                ShowConfirm("JOB IN PROGRESS",
                    $"You're already hauling <b>{current.jobData.cargoType}</b>. It's at {current.jobData.steps - current.stepsLeft:N0} / {current.jobData.steps:N0} steps.\n\nReplace it with <b>{newJob}</b>? The current job and its progress will be lost.",
                    "REPLACE", "KEEP CURRENT",
                    () =>
                    {
                        if (JobManager.instance.activeJob != current) return;
                        AbandonActiveJob();
                        take();
                    });
                return;
        }
    }

    /// <summary>Accept the express offer (from the Job List slot or the popup), freeing the job slot first.</summary>
    public void AcceptExpressOffer()
    {
        var express = ExpressJobManager.instance;
        var offer = express.Offer;
        if (offer == null) return;

        RequestJobSlot($"{offer.cargoType} (express)", () =>
        {
            if (express.Offer != offer) return;
            if (express.AcceptOffer()) ForceTab(TabType.ActiveJobs);
            else AudioManager.instance.PlaySound(SoundType.Fail);
        });
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
        activeAttention = regularDone || expressDone;
        UpdateDot(TabType.ActiveJobs);
    }

    // An accepted express job is a job: it leaves the Job List for the Active tab until it's claimed or dismissed.
    private void RefreshExpress()
    {
        var express = ExpressJobManager.instance;
        bool hasJob = express.HasJob;
        bool unlocked = ProgressionManager.instance.IsUnlocked(Feature.Express);
        if (expressBoardView) expressBoardView.gameObject.SetActive(!hasJob && unlocked);
        if (expressActiveView) expressActiveView.gameObject.SetActive(hasJob);
        activeJobView.SetExpressRunning(hasJob);

        jobListAttention = express.HasOffer;
        UpdateDot(TabType.JobList);
        UpdateActiveJobsDot();
    }

    public void UpdateCompletedJobCount(int i)
    {
        //completedJobsText.text = $"Completed Jobs: {i}";
    }
}
