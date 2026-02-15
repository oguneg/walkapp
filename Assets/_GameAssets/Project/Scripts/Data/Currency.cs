using UnityEngine;

[CreateAssetMenu(fileName = "Currency", menuName = "ScriptableObjects/Currency")]
public class Currency : ScriptableObject
{
    public CurrencyType CurrencyType;
    public string name;
    public string SaveKey; 
    public bool hasCap = false;
    public int initialCap = 100;
    
    [Header("Offline Regen")]
    public bool regenerateOffline;
    public long regenRate;
    public int regenIntervalInSeconds;
}

public enum CurrencyType
{
    Coin, Fuel
}
