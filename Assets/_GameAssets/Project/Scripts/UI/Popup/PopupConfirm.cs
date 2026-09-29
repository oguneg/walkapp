using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Generic yes/no prompt (e.g. replacing a job that's in progress), or a one-button message.</summary>
public class PopupConfirm : PopupBase
{
    [SerializeField] private TextMeshProUGUI titleText, messageText, confirmText, cancelText;
    [SerializeField] private Button confirmButton, cancelButton;
    [Tooltip("Optional third button above the other two (e.g. \"GET JOB QUEUE\"). Hidden unless a label is given.")]
    [SerializeField] private Button extraButton;
    [SerializeField] private TextMeshProUGUI extraText;
    [SerializeField] private float extraButtonSpace = 136f;

    [Tooltip("Button colours for a plain message (one button), instead of the prompt's warning colour.")]
    [SerializeField] private Color messageButtonColor = new Color32(0xFF, 0xB4, 0x01, 0xFF);
    [SerializeField] private Color messageLabelColor = new Color32(0x0B, 0x25, 0x45, 0xFF);

    private Action onConfirm, onExtra;
    private bool hasExtra;
    private Vector2 confirmPosition;
    private Color promptButtonColor, promptLabelColor;
    private float baseHeight, baseMessageHeight;

    private void Awake()
    {
        confirmPosition = ((RectTransform)confirmButton.transform).anchoredPosition;
        promptButtonColor = confirmButton.image.color;
        promptLabelColor = confirmText.color;
        baseHeight = ((RectTransform)transform).sizeDelta.y;
        baseMessageHeight = messageText.rectTransform.sizeDelta.y;
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
            onExtra = null;
            HidePopup();
        });
        if (extraButton)
            extraButton.onClick.AddListener(() =>
            {
                var action = onExtra;
                onConfirm = null;
                onExtra = null;
                HidePopup();
                action?.Invoke();
            });
    }

    /// <summary>Same as Initialize, plus a third button above the other two.</summary>
    public void Initialize(string title, string message, string confirmLabel, string cancelLabel, Action confirm,
        string extraLabel, Action extra)
    {
        hasExtra = extraButton && !string.IsNullOrEmpty(extraLabel);
        onExtra = hasExtra ? extra : null;
        if (extraButton) extraButton.gameObject.SetActive(hasExtra);
        if (hasExtra) extraText.text = extraLabel;
        Initialize(title, message, confirmLabel, cancelLabel, confirm, keepExtra: true);
    }

    public void Initialize(string title, string message, string confirmLabel, string cancelLabel, Action confirm) =>
        Initialize(title, message, confirmLabel, cancelLabel, confirm, keepExtra: false);

    private void Initialize(string title, string message, string confirmLabel, string cancelLabel, Action confirm, bool keepExtra)
    {
        if (!keepExtra)
        {
            hasExtra = false;
            onExtra = null;
            if (extraButton) extraButton.gameObject.SetActive(false);
        }

        titleText.text = title;
        messageText.text = message;
        confirmText.text = confirmLabel;
        onConfirm = confirm;

        // No cancel label: a single centred button (plain message).
        bool hasCancel = !string.IsNullOrEmpty(cancelLabel);
        cancelButton.gameObject.SetActive(hasCancel);
        if (hasCancel) cancelText.text = cancelLabel;
        ((RectTransform)confirmButton.transform).anchoredPosition =
            hasCancel ? confirmPosition : new Vector2(0f, confirmPosition.y);
        confirmButton.image.color = hasCancel ? promptButtonColor : messageButtonColor;
        confirmText.color = hasCancel ? promptLabelColor : messageLabelColor;

        FitMessage();
    }

    // Grow the popup for long messages (a level-up that unlocks several things at once).
    // Runs again when the width changes (rotation, resolution), since that changes the line count.
    private bool fitting;

    private void FitMessage()
    {
        if (fitting || baseHeight <= 0f) return;
        fitting = true;
        var messageRect = messageText.rectTransform;
        float needed = messageText.GetPreferredValues(messageText.text, messageRect.rect.width, 0f).y + 12f;
        float messageHeight = Mathf.Max(baseMessageHeight, needed);
        messageRect.sizeDelta = new Vector2(messageRect.sizeDelta.x, messageHeight);
        var root = (RectTransform)transform;
        root.sizeDelta = new Vector2(root.sizeDelta.x,
            baseHeight + messageHeight - baseMessageHeight + (hasExtra ? extraButtonSpace : 0f));
        fitting = false;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) FitMessage();
    }
}
