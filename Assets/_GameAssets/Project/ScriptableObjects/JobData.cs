using UnityEngine;
 
[System.Serializable]
public class JobData
{
    public JobType jobType;
    public CargoType cargoType;
    public int distance;
    public int steps;
    public int timeInMinutes;
}

public enum JobType
{
    Short, Medium, Long
}

public enum CargoType
{
    Grain, Oil, Petroleum, Electronics, Cars, Tractor, Machinery, Cattle
}

public enum JobState
{
    None, Active, Claimable, TimeOver, Failed
}