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

    public void ShowBasePopup()
    {
        ShowPopup(PopupType.PopupBase);
    }

    public PopupBase ShowPopup(PopupType popupType)
    {
        var popup = popups.Find(x => x.popupType == popupType);
        ShowBG();
        AudioManager.instance.PlaySound(SoundType.Swipe);
        popup.ShowPopup();
        return popup;
    }

    public void HidePopup()
    {
        AudioManager.instance.PlaySound(SoundType.Button);
        HideBG();
    }

    private void ShowBG()
    {
        popupBG.raycastTarget = true;
        popupBG.DOFade(popupBGFadeAmount, popupBGFadeTime).SetEase(Ease.InOutSine);
        popupBG.enabled = true;
    }

    private void HideBG()
    {
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
    PopupOfflineSteps
}