using DG.Tweening;
using UnityEngine;

public class PopupBase : MonoBehaviour
{
    public PopupType popupType;
    private const float popupAppearTime = 0.2f;
    private bool isVisible;

    public virtual void ShowPopup()
    {
        isVisible = true;
        transform.DOKill();
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);
        transform.DOScale(1f, popupAppearTime).SetEase(Ease.OutBack);
    }

    public virtual void HidePopup()
    {
        if (!isVisible) return; // double taps on the close button must not close the next queued popup too
        isVisible = false;

        transform.DOKill();
        transform.DOScale(0f, popupAppearTime).SetEase(Ease.InBack).OnComplete((() => gameObject.SetActive(false)));
        PopupManager.instance.HidePopup();
    }
}
