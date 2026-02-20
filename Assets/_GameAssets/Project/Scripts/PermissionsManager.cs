using System;
using UnityEngine;

public class PermissionsManager : MonoSingleton<PermissionsManager>
{
    private const string IsPermissionsGivenKey =  "isPermissionsGiven";
    [SerializeField] private GameObject permissionsManagerPanel;
    private void Awake()
    {
        return;
        if (PlayerPrefsX.GetBool(IsPermissionsGivenKey, false))
        {
            OnPermissionsComplete();
            return;
        }
        permissionsManagerPanel.SetActive(true);
    }

    private void OnPermissionsComplete()
    {
        permissionsManagerPanel.SetActive(false);
    }

    public void OnPermissionsGiven()
    {
        PlayerPrefsX.SetBool(IsPermissionsGivenKey, true);
        OnPermissionsComplete();
    }
}
