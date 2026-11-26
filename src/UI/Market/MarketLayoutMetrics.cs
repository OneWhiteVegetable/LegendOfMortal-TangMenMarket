using System;

namespace XingShangMod.UI.Market
{
    internal static class MarketLayoutMetrics
    {
        internal const float HighDensityScale = 1.5f;

        internal static float ContentScale(float width, float height)
        {
            return width >= 2304f && height >= 1296f ? HighDensityScale : 1f;
        }

        internal static float EffectiveSize(float physicalSize, float contentScale)
        {
            return contentScale > 0f ? physicalSize / contentScale : physicalSize;
        }

        internal static float CardBodyHeight(
            float cardHeight,
            float topPadding,
            float bottomPadding,
            float headerHeight,
            float actionHeight)
        {
            return Math.Max(0f, cardHeight - 2f - topPadding - bottomPadding - headerHeight - actionHeight);
        }

        internal static float CenteredTop(float outerTop, float outerHeight, float contentHeight)
        {
            return outerTop + (outerHeight - contentHeight) * 0.5f;
        }

        internal static float CartFooterHeight(bool compactHeight)
        {
            return compactHeight ? 153f : 193f;
        }

        internal static float CartListTop(bool compactHeight)
        {
            return compactHeight ? 85f : 110f;
        }

        internal static float CompactQuoteChangeHeight(float viewportWidth)
        {
            float fontSize = Math.Max(11f, Math.Min(15f, viewportWidth * 0.009f));
            return (float)Math.Ceiling(fontSize * 1.3f) + 4f;
        }

        internal static float CompactQuoteHeight(float priceHeight, float changeHeight)
        {
            return priceHeight + 32f + changeHeight;
        }
    }
}
