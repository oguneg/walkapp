using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings: sound and notifications on/off. Account (sign in), delete my data and more go here later;
/// their rows are placeholders until then.
/// </summary>
public class PopupSettings : PopupBase
{
    [SerializeField] private Button soundButton, notificationsButton, closeButton;
    [SerializeField] private TextMeshProUGUI soundLabel, notificationsLabel, versionText;
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
    }

    private void SetToggle(Button button, TextMeshProUGUI label, bool on)
    {
        button.image.color = on ? onColor : offColor;
        label.text = on ? "ON" : "OFF";
    }
}
