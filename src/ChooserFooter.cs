using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal static class ChooserFooter
    {
        internal static Action Fit(Button button, TMP_Text label)
        {
            var plate = (Image)button.targetGraphic;
            var rect = (RectTransform)button.transform;
            var anchorsMin = rect.anchorMin;
            var anchorsMax = rect.anchorMax;
            var position = rect.anchoredPosition;
            var size = rect.sizeDelta;
            float pixelsPerUnit = plate.pixelsPerUnitMultiplier;
            bool autoSize = label.enableAutoSizing;
            float minFont = label.fontSizeMin;
            float maxFont = label.fontSizeMax;

            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(230f, 36f);
            rect.anchoredPosition = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            // Preserve the chooser's footer geometry and use the Button's own
            // Cancel sprites for its normal, hovered, selected and pressed states.
            float canvasPixels = plate.canvas != null ? plate.canvas.referencePixelsPerUnit : 100f;
            plate.pixelsPerUnitMultiplier = pixelsPerUnit * 3f * (canvasPixels / 100f);
            // UI_23's hover and pressed sprites supply the native fills but only
            // Selected supplies the white edge. Draw just that nine-sliced border.
            var edge = ChoiceEdge.Add(button, button.spriteState.selectedSprite, Vector2.zero, plate);

            // The chest picker is reused by the game for ordinary rewards and shops.
            return () =>
            {
                if (button == null) return;
                edge.Remove();
                rect.anchorMin = anchorsMin;
                rect.anchorMax = anchorsMax;
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
                label.enableAutoSizing = autoSize;
                label.fontSizeMin = minFont;
                label.fontSizeMax = maxFont;
                plate.pixelsPerUnitMultiplier = pixelsPerUnit;
            };
        }
    }
}
