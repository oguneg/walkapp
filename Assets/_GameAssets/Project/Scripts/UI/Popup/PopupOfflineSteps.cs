using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class PopupOfflineSteps : PopupBase
{
    [SerializeField] private TextMeshProUGUI offlineStepsText, activeStepsText, bankedStepsText, wastedStepsText;
    [SerializeField] private TextMeshProUGUI headerText, detailText;
    private Sequence sequence;

    public void Initialize(OfflineStepReport report)
    {
        StepAllocation a = report.allocation;

        // A popup can be re-opened before its previous count-up finished; never let two sequences fight over the labels.
        sequence?.Kill();
        AudioManager.instance.StopCount();

        if (headerText)
        {
            headerText.text = report.awayFor.HasValue
                ? $"Away for <b>{GameClock.FormatDuration(report.awayFor.Value)}</b>"
                : "";
        }

        if (detailText) detailText.text = BuildDetail(a);

        offlineStepsText.text = "<mspace=0.56em>0";
        activeStepsText.text = "<mspace=0.56em>0";
        bankedStepsText.text = "<mspace=0.56em>0";
        wastedStepsText.text = "<mspace=0.56em>0";

        sequence = DOTween.Sequence().SetTarget(this);
        sequence.AppendInterval(0.3f);
        AppendCount(offlineStepsText, a.total, 2f);
        AppendCount(activeStepsText, a.toJob, 1f);
        AppendCount(bankedStepsText, a.toBank, 1f);
        AppendCount(wastedStepsText, a.overflow, 1f);
    }

    private void AppendCount(TextMeshProUGUI label, long value, float duration)
    {
        if (value <= 0)
        {
            sequence.AppendInterval(0.1f);
            return;
        }

        sequence.AppendInterval(0.2f);
        sequence.AppendCallback(() => AudioManager.instance.PlayCount((int)(value / 100), duration));
        sequence.Append(DOVirtual.Int(0, (int)value, duration, v => label.text = $"<mspace=0.56em>{v:N0}"));
    }

    private static string BuildDetail(StepAllocation a)
    {
        var lines = new List<string>();
        if (a.bankBurned > 0)
            lines.Add(a.bankCost > a.bankBurned
                ? $"Your step bank added <b>{a.bankBurned:N0}</b> steps to the job (cost {a.bankCost:N0})."
                : $"Your step bank added <b>{a.bankBurned:N0}</b> steps to the job.");
        if (a.overflow > 0)
            lines.Add("Your step depot was full. Upgrade it to keep more steps.");
        return string.Join("\n", lines);
    }

    public override void HidePopup()
    {
        sequence?.Complete();
        AudioManager.instance.StopCount();
        base.HidePopup();
    }
}
