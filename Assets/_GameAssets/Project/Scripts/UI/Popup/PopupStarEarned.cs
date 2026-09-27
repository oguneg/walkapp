using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Celebrates passing one of today's step milestones.</summary>
public class PopupStarEarned : PopupBase
{
    [SerializeField] private TextMeshProUGUI titleText, stepsText, momentumText;
    [SerializeField] private Image[] stars;
    [SerializeField] private Button okButton;
    [SerializeField] private Color starOn = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
    [SerializeField] private Color starOff = new Color32(0x0B, 0x25, 0x45, 0x30);

    private void Awake()
    {
        okButton.onClick.AddListener(HidePopup);
    }

    public void Initialize(int gained)
    {
        var daily = DailyBonusManager.instance;
        int today = daily.TodayStars;

        titleText.text = gained > 1 ? $"{gained} STARS EARNED!" : "STAR EARNED!";
        stepsText.text = $"<b>{daily.TodaySteps:N0}</b> steps today";
        momentumText.text = $"Momentum {daily.WindowStars}/{daily.MaxWindowStars}  ·  coin bonus <b>+{daily.BonusPercent * 100:0}%</b>";

        for (int i = 0; i < stars.Length; i++)
        {
            bool isNew = i >= today - gained && i < today;
            stars[i].color = i < today ? starOn : starOff;
            stars[i].transform.DOKill(true);
            stars[i].transform.localScale = Vector3.one;
            if (isNew)
            {
                stars[i].transform.localScale = Vector3.zero;
                stars[i].transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.25f + 0.15f * (i - (today - gained)));
            }
        }
    }
}
