using System;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    private bool isPermissionsGiven = false;
    public bool IsPermissionsGiven => isPermissionsGiven;

    private void Start()
    {
        Application.targetFrameRate = 60;
    }
}
