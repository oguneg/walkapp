using DG.Tweening;
using UnityEngine;

public class PopupBase : MonoBehaviour
{
    public PopupType popupType;
    private const float popupAppearTime = 0.2f;
    public virtual void ShowPopup()
    {
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);
        transform.DOScale(1f, popupAppearTime).SetEase(Ease.OutBack);
    }

    public virtual void HidePopup()
    {
        transform.DOScale(0f, popupAppearTime).SetEase(Ease.InBack).OnComplete((() => gameObject.SetActive(false)));
        PopupManager.instance.HidePopup();
    }
}

