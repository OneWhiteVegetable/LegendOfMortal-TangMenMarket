using System;

namespace XingShangMod.UI.Shared
{
    internal static class NewsTickerMotion
    {
        internal const float CycleDurationSeconds = 32f;

        internal static float Advance(float offset, float deltaTime, float groupWidth)
        {
            float current = Wrap(offset, groupWidth);
            if (groupWidth <= 0f || float.IsNaN(groupWidth) || float.IsInfinity(groupWidth) ||
                deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return current;
            double distance = (double)groupWidth * deltaTime / CycleDurationSeconds;
            return WrapDouble((double)current + distance, groupWidth);
        }

        internal static float Wrap(float offset, float groupWidth)
        {
            if (groupWidth <= 0f || float.IsNaN(groupWidth) || float.IsInfinity(groupWidth) ||
                float.IsNaN(offset) || float.IsInfinity(offset))
                return 0f;
            return WrapDouble(offset, groupWidth);
        }

        private static float WrapDouble(double offset, float groupWidth)
        {
            double result = offset % groupWidth;
            if (result < 0.0)
                result += groupWidth;
            return (float)result;
        }
    }
}
