using UnityEngine;

[CreateAssetMenu(fileName = "Currency", menuName = "ScriptableObjects/Currency")]
public class Currency : ScriptableObject
{
    public CurrencyType CurrencyType;
    public string name;
    public string SaveKey; 
    public bool hasCap = false;
    public int initialCap = 100;
}

public enum CurrencyType
{
    Coin, Fuel
}
