namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiWindowLayoutProfile
    {
        private const float WideInsetMinWidth = 1700f;
        private const float NarrowMaxWidth = 1450f;
        private const float CompactMaxHeight = 790f;

        private UiWindowLayoutProfile(
            float windowInset,
            float mastheadHeight,
            float brandWidth,
            float brandSealWidth,
            float brandSealHeight,
            float indexWidth,
            float indexChartWidth,
            float indexChartHeight,
            float statusHeight,
            float navWidth,
            float moneyWidth,
            bool narrow,
            bool compactHeight,
            bool compact)
        {
            WindowInset = windowInset;
            MastheadHeight = mastheadHeight;
            BrandWidth = brandWidth;
            BrandSealWidth = brandSealWidth;
            BrandSealHeight = brandSealHeight;
            IndexWidth = indexWidth;
            IndexChartWidth = indexChartWidth;
            IndexChartHeight = indexChartHeight;
            StatusHeight = statusHeight;
            NavWidth = navWidth;
            MoneyWidth = moneyWidth;
            Narrow = narrow;
            CompactHeight = compactHeight;
            Compact = compact;
        }

        internal const float TickerHeight = 34f;

        internal readonly float WindowInset;
        internal readonly float MastheadHeight;
        internal readonly float BrandWidth;
        internal readonly float BrandSealWidth;
        internal readonly float BrandSealHeight;
        internal readonly float IndexWidth;
        internal readonly float IndexChartWidth;
        internal readonly float IndexChartHeight;
        internal readonly float StatusHeight;
        internal readonly float NavWidth;
        internal readonly float MoneyWidth;
        internal readonly bool Narrow;
        internal readonly bool CompactHeight;
        internal readonly bool Compact;

        internal static UiWindowLayoutProfile For(float width, float height)
        {
            bool narrow = width <= NarrowMaxWidth;
            bool compactHeight = height <= CompactMaxHeight;
            bool compact = narrow || compactHeight;
            return new UiWindowLayoutProfile(
                width < WideInsetMinWidth ? 14f : 20f,
                compactHeight ? 70f : 82f,
                narrow ? 224f : 258f,
                narrow ? 38f : 45f,
                compactHeight ? 39f : 48f,
                narrow ? 305f : 355f,
                narrow ? 102f : 130f,
                compactHeight ? 39f : 45f,
                compactHeight ? 24f : 28f,
                compact ? 180f : 195f,
                compact ? 190f : 198f,
                narrow,
                compactHeight,
                compact);
        }
    }
}
