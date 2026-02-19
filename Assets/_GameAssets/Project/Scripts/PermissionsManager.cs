using System;
using UnityEngine;

public class PermissionsManager : MonoSingleton<PermissionsManager>
{
    private const string IsPermissionsGivenKey =  "isPermissionsGiven";
    [SerializeField] private GameObject permissionsManagerPanel;
    private void Awake()
    {
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
        GameManager.instance.OnPermissionsGiven();
    }

    public void OnPermissionsGiven()
    {
        PlayerPrefsX.SetBool(IsPermissionsGivenKey, true);
        OnPermissionsComplete();
    }
}
