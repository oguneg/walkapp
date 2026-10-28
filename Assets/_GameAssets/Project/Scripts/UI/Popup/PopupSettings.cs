using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings: sound and notifications on/off, the account (guest, or signed in with Google) and delete my data.
/// </summary>
public class PopupSettings : PopupBase
{
    [SerializeField] private Button soundButton, notificationsButton, closeButton;
    [SerializeField] private TextMeshProUGUI soundLabel, notificationsLabel, versionText;
    [Header("Account")]
    [SerializeField] private Button accountButton, deleteButton;
    [SerializeField] private TextMeshProUGUI accountButtonLabel, accountRowText, deleteButtonLabel;
    [SerializeField] private Color dangerColor = new Color32(0xC8, 0x43, 0x3A, 0xFF);
    [SerializeField] private Color accountColor = new Color32(0x2B, 0x6C, 0xC4, 0xFF);
    [SerializeField] private Color onColor = new Color32(0x3E, 0x9A, 0x4B, 0xFF);
    [SerializeField] private Color offColor = new Color32(0x8D, 0xA9, 0xC4, 0xFF);

    private void Awake()
    {
        soundButton.onClick.AddListener(() =>
        {
            GameSettings.SoundOn = !GameSettings.SoundOn;
            AudioManager.instance.PlaySound(SoundType.Button); // silent when just turned off
            Refresh();
        });
        notificationsButton.onClick.AddListener(() =>
        {
            GameSettings.NotificationsOn = !GameSettings.NotificationsOn;
            if (GameSettings.NotificationsOn) NotificationScheduler.instance.RequestPermission();
            AudioManager.instance.PlaySound(SoundType.Button);
            Refresh();
        });
        closeButton.onClick.AddListener(HidePopup);
        if (accountButton) accountButton.onClick.AddListener(OnAccount);
        if (deleteButton) deleteButton.onClick.AddListener(OnDelete);
        AuthService.instance.OnChanged += () => { if (gameObject.activeInHierarchy) Refresh(); };
    }

    public override void ShowPopup()
    {
        base.ShowPopup();
        Refresh();
    }

    private void Refresh()
    {
        SetToggle(soundButton, soundLabel, GameSettings.SoundOn);
        SetToggle(notificationsButton, notificationsLabel, GameSettings.NotificationsOn);
        if (versionText) versionText.text = $"Version {Application.version}";
        RefreshAccount();
    }

    private bool accountBusy;

    private void RefreshAccount()
    {
        if (!accountButton) return;
        var auth = AuthService.instance;
        bool google = auth.IsGoogle;
        accountRowText.text = google
            ? $"Account\n<size=62%>{auth.GoogleName}</size>"
            : "Account\n<size=62%>Guest: sign in to keep your convoy on a new phone</size>";
        accountButtonLabel.text = accountBusy ? "..." : google ? "SIGN OUT" : "GOOGLE";
        accountButton.image.color = google ? offColor : accountColor;
        accountButton.interactable = !accountBusy;
        deleteButton.image.color = dangerColor;
        deleteButtonLabel.text = "DELETE";
        deleteButtonLabel.color = Color.white;
        deleteButton.interactable = !accountBusy;
    }

    private void OnAccount()
    {
        AudioManager.instance.PlaySound(SoundType.Button);
        var auth = AuthService.instance;
        if (auth.IsGoogle)
        {
            HidePopup();
            UIManager.instance.ShowConfirm("SIGN OUT?",
                "This phone goes back to a guest account. Your Google account keeps its convoy; sign in again to get it back.",
                "SIGN OUT", "CANCEL", () => auth.SignOut());
            return;
        }

        accountBusy = true;
        RefreshAccount();
        auth.SignInWithGoogle((ok, message) =>
        {
            accountBusy = false;
            Refresh();
            if (ok)
            {
                ConvoyManager.instance.RefreshSoon();
                if (message == "switched")
                    UIManager.instance.ShowMessage("WELCOME BACK",
                        $"Signed in as <b>{auth.GoogleName}</b>. Your convoy from that account is back.", "OK");
            }
            else if (message != "cancelled")
            {
                UIManager.instance.ShowMessage("SIGN-IN FAILED", message, "OK");
            }
        });
    }

    // GDPR: everything about the player goes, online and on this phone, after a clear warning.
    private void OnDelete()
    {
        AudioManager.instance.PlaySound(SoundType.Button);
        HidePopup();
        UIManager.instance.ShowConfirm("DELETE MY DATA?",
            "This deletes your account, your convoy membership and your step totals on our server, and resets the game on this phone: coins, trucks, upgrades and levels.\n\nThis can't be undone.",
            "DELETE ALL", "CANCEL", () =>
            {
                ConvoyManager.instance.DeleteOnlineData(ok =>
                {
                    if (!ok)
                    {
                        UIManager.instance.ShowMessage("COULDN'T DELETE",
                            "We couldn't reach the server, so nothing was deleted. Check your connection and try again.", "OK");
                        return;
                    }
                    // The managers live for the whole session: the wipe happens at the next launch.
                    SaveSystem.ResetOnNextLaunch();
                    UIManager.instance.ShowConfirm("DATA DELETED",
                        "Everything is gone. The game closes now; open it again to start fresh.",
                        "CLOSE GAME", null, Application.Quit);
                });
            });
    }

    private void SetToggle(Button button, TextMeshProUGUI label, bool on)
    {
        button.image.color = on ? onColor : offColor;
        label.text = on ? "ON" : "OFF";
    }
}
