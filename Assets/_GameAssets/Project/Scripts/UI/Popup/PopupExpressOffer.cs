using OgunWorks.UI;
using TMPro;
using UnityEngine;

/// <summary>"Express delivery!" popup shown when an offer arrives. Accept starts the clock right away.</summary>
public class PopupExpressOffer : PopupBase
{
    [SerializeField] private TextMeshProUGUI goalText, detailText, rewardText, timerText;
    [SerializeField] private UnityEngine.UI.Button acceptButton, laterButton;

    private ExpressJobManager manager;
    private bool subscribed;

    private void Awake()
    {
        acceptButton.onClick.AddListener(OnAccept);
        laterButton.onClick.AddListener(HidePopup);
    }

    public override void ShowPopup()
    {
        base.ShowPopup();
        manager = ExpressJobManager.instance;
        if (!subscribed)
        {
            manager.OnTick += Refresh;
            manager.OnChanged += Refresh;
            subscribed = true;
        }

        Refresh();
    }

    public override void HidePopup()
    {
        Unsubscribe();
        base.HidePopup();
    }

    private void OnDisable() => Unsubscribe();

    private void Unsubscribe()
    {
        if (!subscribed || manager == null) return;
        manager.OnTick -= Refresh;
        manager.OnChanged -= Refresh;
        subscribed = false;
    }

    private void Refresh()
    {
        ExpressOffer offer = manager.Offer;
        if (offer == null)
        {
            // Expired while queued/open, or accepted from the Active Jobs tab.
            HidePopup();
            return;
        }

        goalText.text = $"Walk <b>{offer.targetSteps:N0}</b> steps\nin <b>{offer.durationMinutes} minutes</b>";
        detailText.text = $"Rush delivery: {offer.cargoType}. The clock starts when you accept.";
        rewardText.text = $"<sprite=0>{offer.reward:N0}    <sprite=3>{offer.experience:N0}";
        timerText.text = $"Offer ends in {GameClock.FormatClock(GameClock.FromUnix(offer.expiresUnix) - GameClock.UtcNow)}";
    }

    private void OnAccept()
    {
        if (!manager.AcceptOffer())
        {
            Refresh();
            return;
        }

        HidePopup();
        UIManager.instance.ForceTab(TabType.Express);
    }
}
