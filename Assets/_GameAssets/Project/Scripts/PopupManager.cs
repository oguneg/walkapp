using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoSingleton<PopupManager>
{
    [SerializeField] private Image popupBG;

    [SerializeField] private List<PopupBase> popups;
    private const float popupBGFadeAmount = 0.8f;
    private const float popupBGFadeTime = 0.2f;
    private const float popupSwapDelay = 0.25f;

    // One popup at a time; the rest wait here (e.g. an express offer arriving while "while you were away" is open).
    private readonly Queue<(PopupType type, Action<PopupBase> setup)> queue = new Queue<(PopupType, Action<PopupBase>)>();
    private PopupBase current;
    private bool opening;

    public bool IsShowing(PopupType type) => current != null && current.popupType == type;

    public void ShowBasePopup()
    {
        EnqueuePopup(PopupType.PopupBase);
    }

    /// <summary>Shows the popup now, or once the popups already on screen/queued are closed.</summary>
    public void EnqueuePopup(PopupType popupType, Action<PopupBase> setup = null)
    {
        if (current != null || opening)
        {
            queue.Enqueue((popupType, setup));
            return;
        }

        Open(popupType, setup);
    }

    /// <summary>Called by PopupBase when a popup closes.</summary>
    public void HidePopup()
    {
        AudioManager.instance.PlaySound(SoundType.Button);
        current = null;

        if (queue.Count == 0)
        {
            HideBG();
            return;
        }

        var (type, setup) = queue.Dequeue();
        opening = true;
        DOVirtual.DelayedCall(popupSwapDelay, () =>
        {
            opening = false;
            Open(type, setup);
        }).SetTarget(this);
    }

    private void Open(PopupType popupType, Action<PopupBase> setup)
    {
        var popup = popups.Find(x => x.popupType == popupType);
        if (popup == null)
        {
            Debug.LogError($"PopupManager: no popup registered for {popupType}");
            return;
        }

        current = popup;
        ShowBG();
        AudioManager.instance.PlaySound(SoundType.Swipe);
        popup.ShowPopup();
        setup?.Invoke(popup);
    }

    private void ShowBG()
    {
        popupBG.DOKill();
        popupBG.raycastTarget = true;
        popupBG.enabled = true;
        popupBG.DOFade(popupBGFadeAmount, popupBGFadeTime).SetEase(Ease.InOutSine);
    }

    private void HideBG()
    {
        popupBG.DOKill();
        popupBG.DOFade(0, popupBGFadeTime).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            popupBG.raycastTarget = false;
            popupBG.enabled = false;
        });
    }
}

public enum PopupType
{
    PopupBase,
    PopupJobComplete,
    PopupOfflineSteps,
    PopupExpressOffer,
    PopupRefuel,
    PopupStarEarned,
    PopupConfirm,
    PopupSettings
}
