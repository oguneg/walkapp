using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Generic yes/no prompt (e.g. replacing a job that's in progress).</summary>
public class PopupConfirm : PopupBase
{
    [SerializeField] private TextMeshProUGUI titleText, messageText, confirmText, cancelText;
    [SerializeField] private Button confirmButton, cancelButton;

    private Action onConfirm;

    private void Awake()
    {
        confirmButton.onClick.AddListener(() =>
        {
            var action = onConfirm;
            onConfirm = null;
            HidePopup();
            action?.Invoke();
        });
        cancelButton.onClick.AddListener(() =>
        {
            onConfirm = null;
            HidePopup();
        });
    }

    public void Initialize(string title, string message, string confirmLabel, string cancelLabel, Action confirm)
    {
        titleText.text = title;
        messageText.text = message;
        confirmText.text = confirmLabel;
        cancelText.text = cancelLabel;
        onConfirm = confirm;
    }
}
