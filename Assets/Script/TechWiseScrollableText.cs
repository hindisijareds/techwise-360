using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scrollable text within its existing rectangle, leaving sibling controls exposed.</summary>
internal static class TechWiseScrollableText
{
    internal static ScrollRect Wrap(TMP_Text text)
    {
        var original=text.rectTransform;
        var viewport=new GameObject(text.name+" scroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        var rect=viewport.GetComponent<RectTransform>(); rect.SetParent(original.parent,false);
        rect.anchorMin=original.anchorMin; rect.anchorMax=original.anchorMax;
        rect.pivot=original.pivot; rect.offsetMin=original.offsetMin; rect.offsetMax=original.offsetMax;
        viewport.GetComponent<Image>().color=new Color(0,0,0,.015f);
        original.SetParent(rect,false); original.anchorMin=new Vector2(0,1); original.anchorMax=Vector2.one;
        original.pivot=new Vector2(.5f,1); original.offsetMin=new Vector2(8,0); original.offsetMax=new Vector2(-12,0);
        text.enableAutoSizing=false; text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Overflow;
        text.raycastTarget=false;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.GetComponent<ScrollRect>(); scroll.viewport=rect; scroll.content=original;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=38;
        return scroll;
    }
}
