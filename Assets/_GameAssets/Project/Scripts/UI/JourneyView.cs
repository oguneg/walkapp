using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// The road trip on the Stats tab: the current route, the next stop and how many steps away it is, and the
    /// postcard album (collected postcards with their facts, newest first).
    /// </summary>
    public class JourneyView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI routeText, nextStopText, detailText, albumTitleText, albumText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button albumButton;
        [SerializeField] private RectTransform albumCard;

        private JourneyManager manager;

        private void Awake()
        {
            if (albumButton) albumButton.onClick.AddListener(ScrollToAlbum);
        }

        private void OnEnable()
        {
            manager = JourneyManager.instance;
            manager.OnChanged += Refresh;
            manager.ShowPending(); // first visit after unlocking: the postcards collected so far
            Refresh();
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            var route = manager.Route;
            int collected = manager.Postcards.Count;
            int total = manager.TotalStops;

            if (route == null)
            {
                routeText.text = "ROAD TRIP";
                nextStopText.text = "Every route driven!";
                detailText.text = "New routes are on their way. Your steps keep counting.";
                progressFill.fillAmount = 1f;
            }
            else
            {
                var first = route.stops[0].name;
                var last = route.stops[route.stops.Length - 1].name;
                routeText.text = $"ROAD TRIP · {first.ToUpperInvariant()} TO {last.ToUpperInvariant()}";
                progressFill.fillAmount = manager.RouteProgress01;
                var next = manager.NextStop;
                nextStopText.text = next != null ? $"Next stop: {next.name}" : "Arriving...";
                long reward = next != null ? manager.StopReward(route, manager.StopsReached) : 0;
                detailText.text = $"<sprite=1>{manager.StepsToNextStop:N0} steps away · <sprite=0>{reward:N0}"
                                  + $" · stop {manager.StopsReached} of {route.stops.Length - 1}";
            }

            albumButton.GetComponentInChildren<TextMeshProUGUI>().text = $"POSTCARDS {collected}/{total}";
            albumTitleText.text = $"POSTCARDS · {collected} OF {total}";

            var sb = new StringBuilder();
            for (int i = manager.Postcards.Count - 1; i >= 0; i--)
            {
                var (r, index) = manager.Find(manager.Postcards[i]);
                if (r == null) continue;
                var stop = r.stops[index];
                sb.Append($"<b>{stop.name}</b>  <size=75%><color=#FFFFFFAA>{r.name}, km {stop.km:N0}</color></size>\n");
                sb.Append($"<size=85%>{stop.fact}</size>\n\n");
            }
            int ahead = total - collected;
            if (ahead > 0) sb.Append($"<size=85%><color=#FFFFFFAA>{ahead} more postcards on the road ahead.</color></size>");
            albumText.text = sb.ToString().TrimEnd();
        }

        private void ScrollToAlbum()
        {
            AudioManager.instance.PlaySound(SoundType.Button);
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll == null || albumCard == null) return;
            Canvas.ForceUpdateCanvases();
            float contentHeight = scroll.content.rect.height;
            float viewHeight = scroll.viewport ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;
            float fromTop = -albumCard.localPosition.y;
            float range = contentHeight - viewHeight;
            if (range > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - fromTop / range);
        }
    }
}
