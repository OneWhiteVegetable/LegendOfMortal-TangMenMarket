using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Market;

namespace XingShangMod.UI.Infrastructure
{
    internal static class UiCanvasDensity
    {
        internal static float Apply(RectTransform root)
        {
            float density = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.pixelPerfect = true;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = density;
            // Layout and text may be measured before CanvasScaler receives its next Update.
            if (!Mathf.Approximately(canvas.scaleFactor, density))
                canvas.scaleFactor = density;
            return density;
        }
    }
}
