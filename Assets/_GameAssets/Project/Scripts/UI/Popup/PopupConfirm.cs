using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Generic yes/no prompt (e.g. replacing a job that's in progress), or a one-button message.</summary>
public class PopupConfirm : PopupBase
{
    [SerializeField] private TextMeshProUGUI titleText, messageText, confirmText, cancelText;
    [SerializeField] private Button confirmButton, cancelButton;

    [Tooltip("Button colours for a plain message (one button), instead of the prompt's warning colour.")]
    [SerializeField] private Color messageButtonColor = new Color32(0xFF, 0xB4, 0x01, 0xFF);
    [SerializeField] private Color messageLabelColor = new Color32(0x0B, 0x25, 0x45, 0xFF);

    private Action onConfirm;
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
            HidePopup();
        });
    }

    public void Initialize(string title, string message, string confirmLabel, string cancelLabel, Action confirm)
    {
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
        root.sizeDelta = new Vector2(root.sizeDelta.x, baseHeight + messageHeight - baseMessageHeight);
        fitting = false;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) FitMessage();
    }
}
