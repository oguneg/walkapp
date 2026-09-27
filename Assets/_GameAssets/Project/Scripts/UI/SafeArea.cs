using UnityEngine;

/// <summary>
/// Fits this RectTransform to Screen.safeArea so HUD and tab bar stay clear of notches, camera holes
/// and the home indicator. Put it on a full-screen container; backgrounds that should bleed to the edges stay outside.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect appliedSafeArea;
    private Vector2Int appliedScreenSize;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)
            Apply();
    }

    private void Apply()
    {
        appliedSafeArea = Screen.safeArea;
        appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = appliedSafeArea.position;
        Vector2 max = appliedSafeArea.position + appliedSafeArea.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
