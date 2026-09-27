using UnityEngine;

/// <summary>One step of the fleet ladder. The player drives their best owned truck.</summary>
[CreateAssetMenu(fileName = "TruckData", menuName = "Scriptable Object/Truck Data")]
public class TruckData : ScriptableObject
{
    public string truckName;
    [TextArea] public string description;
    public long price;
    [Tooltip("Multiplies coin rewards of new job and express offers.")]
    public float rewardMultiplier = 1f;
    [Tooltip("Multiplies fuel cost of new job offers (lower is better).")]
    public float fuelMultiplier = 1f;
    [Tooltip("Extra fuel tank capacity, in displayed fuel units.")]
    public int fuelTankBonus;
    [Tooltip("Optional art. The Fleet tab shows a tier badge when empty.")]
    public Sprite sprite;
}
