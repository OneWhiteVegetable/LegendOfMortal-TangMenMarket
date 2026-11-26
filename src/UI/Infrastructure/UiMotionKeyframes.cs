namespace XingShangMod.UI.Infrastructure
{
    internal static class UiMotionKeyframes
    {
        internal const float CartTargetXFraction = 0.55f;

        internal static float CartTargetFromTop(float height)
        {
            if (float.IsNaN(height) || float.IsInfinity(height) || height <= 0f)
                return 0f;
            float distance = height * 0.45f;
            return distance < 260f ? distance : 260f;
        }

        internal static void EvaluateToken(
            float value,
            out float positionProgress,
            out float rotation,
            out float scale,
            out float alpha)
        {
            float normalized = Clamp01(value);
            positionProgress = UiMotionEasing.CubicBezier(normalized, 0.5f, 0f, 0.7f, 0.2f);
            rotation = -9f * positionProgress;
            scale = 1f - 0.75f * positionProgress;
            if (normalized <= 0.6f)
            {
                alpha = 1f;
                return;
            }
            float fade = UiMotionEasing.CubicBezier((normalized - 0.6f) / 0.4f, 0.5f, 0f, 0.7f, 0.2f);
            alpha = 1f - fade;
        }

        internal static void EvaluateStamp(
            float value,
            out float scale,
            out float rotation,
            out float alpha)
        {
            float normalized = Clamp01(value);
            if (normalized < 0.25f)
            {
                float eased = PaperOut(normalized / 0.25f);
                scale = Lerp(2f, 0.93f, eased);
                rotation = Lerp(18f, 8f, eased);
                alpha = eased;
                return;
            }
            if (normalized < 0.42f)
            {
                float eased = PaperOut((normalized - 0.25f) / 0.17f);
                scale = Lerp(0.93f, 1f, eased);
                rotation = Lerp(8f, 10f, eased);
                alpha = 1f;
                return;
            }

            float transformEase = PaperOut((normalized - 0.42f) / 0.58f);
            scale = Lerp(1f, 1.03f, transformEase);
            rotation = Lerp(10f, 9f, transformEase);
            alpha = normalized <= 0.78f
                ? 1f
                : 1f - PaperOut((normalized - 0.78f) / 0.22f);
        }

        private static float PaperOut(float value)
        {
            return UiMotionEasing.CubicBezier(value, 0.16f, 1f, 0.3f, 1f);
        }

        private static float Lerp(float from, float to, float value)
        {
            return from + (to - from) * value;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}
