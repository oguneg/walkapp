using System;
using UnityEngine;

/// <summary>
/// The road trips: real routes walked one after another. Each route takes about a month of normal walking
/// (stepsToComplete), and its stops sit at their real road distance, so the first postcard comes within days and a
/// whole route is a month-long goal. New routes are new content: add one here (or a later asset) and players who
/// finished the last route continue onto it.
/// </summary>
[CreateAssetMenu(fileName = "Journey", menuName = "Walkapp/Journey")]
public class JourneyData : ScriptableObject
{
    [Serializable]
    public class Stop
    {
        public string name;
        [Tooltip("Road distance from the route's start, km (approximate).")]
        public float km;
        [TextArea] public string fact;
    }

    [Serializable]
    public class Route
    {
        public string id;
        public string name;
        [Tooltip("Walked steps from the start of this route to its last stop.")]
        public long stepsToComplete = 200000;
        [Tooltip("Coins when the last stop is reached, on top of that stop's own reward.")]
        public long completionReward = 10000;
        public Stop[] stops;

        public float TotalKm => stops.Length > 0 ? stops[stops.Length - 1].km : 1f;

        /// <summary>Walked steps from the route's start to reach stop i.</summary>
        public long StepsTo(int i) => (long)Math.Round(stepsToComplete * (stops[i].km / TotalKm));
    }

    [Tooltip("Coins per walked step of each leg (15,000 steps to the next stop = 1,500 coins at 0.1).")]
    public float coinsPerLegStep = 0.1f;
    public Route[] routes;
}
