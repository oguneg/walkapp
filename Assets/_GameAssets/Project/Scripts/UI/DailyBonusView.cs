using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// Stats tab: today's steps against the five milestones, and the 7-day momentum strip with the coin bonus.
    /// </summary>
    public class DailyBonusView : MonoBehaviour
    {
        [Header("Today")]
        [SerializeField] private TextMeshProUGUI todayStepsText, todayHintText;
        [SerializeField] private Image[] todayStars;
        [SerializeField] private RectTransform trackFill;
        [SerializeField] private RectTransform[] trackMarkers;
        [SerializeField] private Image[] trackStars;
        [SerializeField] private TextMeshProUGUI[] trackLabels;

        [Header("Momentum")]
        [SerializeField] private TextMeshProUGUI windowStarsText, bonusText, capText, dropText;
        [SerializeField] private DayColumnView[] dayColumns; // oldest (left) to today (right)

        [SerializeField] private Color starOn = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
        [SerializeField] private Color starOff = new Color32(0x0B, 0x25, 0x45, 0x40);

        private DailyBonusManager daily;

        private void OnEnable()
        {
            daily = DailyBonusManager.instance;
            daily.OnChanged += Refresh;
            PlaceMarkers();
            Refresh();
        }

        private void OnDisable()
        {
            if (daily != null) daily.OnChanged -= Refresh;
        }

        // Markers sit at milestone / last milestone along the track, so retuned milestones still line up.
        private void PlaceMarkers()
        {
            var ms = daily.Milestones;
            float max = ms[ms.Count - 1];
            for (int i = 0; i < trackMarkers.Length && i < ms.Count; i++)
            {
                float x = ms[i] / max;
                trackMarkers[i].anchorMin = new Vector2(x, trackMarkers[i].anchorMin.y);
                trackMarkers[i].anchorMax = new Vector2(x, trackMarkers[i].anchorMax.y);
                trackMarkers[i].anchoredPosition = new Vector2(0, trackMarkers[i].anchoredPosition.y);
                trackLabels[i].text = ms[i] % 1000 == 0 ? $"{ms[i] / 1000}K" : $"{ms[i] / 1000f:0.#}K";
            }
        }

        private void Refresh()
        {
            var ms = daily.Milestones;
            long steps = daily.TodaySteps;
            int stars = daily.TodayStars;

            todayStepsText.text = $"{steps:N0} <size=45%>steps</size>";
            for (int i = 0; i < todayStars.Length; i++) todayStars[i].color = i < stars ? starOn : starOff;
            for (int i = 0; i < trackStars.Length && i < ms.Count; i++) trackStars[i].color = steps >= ms[i] ? starOn : starOff;
            trackFill.anchorMax = new Vector2(Mathf.Clamp01(steps / (float)ms[ms.Count - 1]), trackFill.anchorMax.y);

            long next = daily.NextMilestone;
            todayHintText.text = next < 0
                ? "All five stars today. Legend."
                : $"<b>{next - steps:N0}</b> more steps for your next star";

            int window = daily.WindowStars;
            windowStarsText.text = $"{window} <size=60%>/ {daily.MaxWindowStars}</size>";
            bonusText.text = $"Coin bonus <b>+{daily.BonusPercent * 100:0}%</b>";
            capText.text = window >= daily.StarCap
                ? $"Maxed. {window - daily.StarCap} spare stars are your safety net."
                : $"+{daily.BonusPerStar * 100:0}% per star, max +{daily.MaxBonusPercent * 100:0}% at {daily.StarCap} stars.";

            int dropping = daily.StarsDroppingTomorrow;
            dropText.text = dropping > 0
                ? $"Tomorrow your oldest day ({dropping} stars) drops off. Walk today to keep your momentum."
                : "Your oldest day has no stars, so tomorrow costs you nothing.";

            DateTime today = daily.TodayLocal;
            int days = dayColumns.Length;
            for (int i = 0; i < days; i++)
            {
                DateTime day = today.AddDays(i - (days - 1));
                bool isToday = i == days - 1;
                dayColumns[i].Show(daily.StarsFor(daily.StepsOn(day)),
                    isToday ? "Today" : day.ToString("ddd"), isToday, starOn, starOff);
            }
        }
    }
}
