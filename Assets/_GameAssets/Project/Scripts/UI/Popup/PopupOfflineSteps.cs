using DG.Tweening;
using TMPro;
using UnityEngine;

public class PopupOfflineSteps : PopupBase
{
    [SerializeField] private TextMeshProUGUI offlineStepsText, activeStepsText, bankedStepsText, wastedStepsText;
    public override void ShowPopup()
    {
        base.ShowPopup();
    }

    public void Initialize(int offlineSteps, int activeSteps, int bankedSteps, int wastedSteps)
    {
        var seq =  DOTween.Sequence();

        offlineStepsText.text = $"<mspace=48>0";
        activeStepsText.text = $"<mspace=44>0";
        bankedStepsText.text = $"<mspace=44>0";
        wastedStepsText.text = $"<mspace=44>0";
        
        
        seq.AppendInterval(0.3f);
        seq.AppendCallback(() => AudioManager.instance.PlayCount(offlineSteps / 100, 2));
        seq.Append(DOVirtual.Int(0, offlineSteps, 2f, value => { offlineStepsText.text = $"<mspace=48>{value:N0}"; }));
        seq.AppendInterval(0.2f);
        seq.AppendCallback(() => AudioManager.instance.PlayCount(activeSteps / 100,1));
        seq.Append(DOVirtual.Int(0, activeSteps, 1f, value => { activeStepsText.text = $"<mspace=44>{value:N0}"; }));
        seq.AppendInterval(0.2f);
        seq.AppendCallback(() => AudioManager.instance.PlayCount(bankedSteps / 100,1));
        seq.Append(DOVirtual.Int(0, bankedSteps, 1f, value => { bankedStepsText.text = $"<mspace=44>{value:N0}"; }));
        seq.AppendInterval(0.2f);
        seq.AppendCallback(() => AudioManager.instance.PlayCount(wastedSteps / 100,1));
        seq.Append(DOVirtual.Int(0, wastedSteps, 1f, value => { wastedStepsText.text = $"<mspace=44>{value:N0}"; }));
    }

    public override void HidePopup()
    {
        base.HidePopup();
    }
}

