using System;
using DG.Tweening;

namespace XingShangMod.UI.Infrastructure
{
    /// <summary>
    /// Shared DOTween easing functions. Time and duration use the same unit; the returned value is normalized.
    /// </summary>
    internal static class UiMotionEasing
    {
        /// <summary>
        /// CSS cubic-bezier(.16, 1, .3, 1), suitable for Tween.SetEase. It is not an approximation by Ease.OutCubic.
        /// </summary>
        internal static readonly EaseFunction PaperOutEase = PaperOut;
        internal static readonly EaseFunction CssEase = EvaluateCssEase;
        internal static readonly EaseFunction CssEaseOut = EvaluateCssEaseOut;

        /// <summary>Evaluates PaperOut with DOTween's EaseFunction signature.</summary>
        internal static float PaperOut(float time, float duration, float overshootOrAmplitude, float period)
        {
            float normalizedTime = duration <= 0f ? 1f : time / duration;
            return CubicBezier(normalizedTime, 0.16f, 1f, 0.3f, 1f);
        }

        /// <summary>
        /// Evaluates a CSS-style cubic Bezier at normalizedTime. x1 and x2 must be in [0, 1];
        /// y values may overshoot. Both endpoints are pinned exactly.
        /// </summary>
        internal static float CubicBezier(float normalizedTime, float x1, float y1, float x2, float y2)
        {
            if (normalizedTime <= 0f)
                return 0f;
            if (normalizedTime >= 1f)
                return 1f;
            if (x1 < 0f || x1 > 1f)
                throw new ArgumentOutOfRangeException("x1");
            if (x2 < 0f || x2 > 1f)
                throw new ArgumentOutOfRangeException("x2");

            float lower = 0f;
            float upper = 1f;
            float parameter = normalizedTime;
            for (int i = 0; i < 16; i++)
            {
                float x = Sample(parameter, x1, x2) - normalizedTime;
                if (Math.Abs(x) < 0.000001f)
                    return Sample(parameter, y1, y2);

                if (x > 0f)
                    upper = parameter;
                else
                    lower = parameter;

                float slope = SampleDerivative(parameter, x1, x2);
                if (Math.Abs(slope) > 0.000001f)
                {
                    float candidate = parameter - x / slope;
                    if (candidate > lower && candidate < upper)
                    {
                        parameter = candidate;
                        continue;
                    }
                }
                parameter = (lower + upper) * 0.5f;
            }
            return Sample(parameter, y1, y2);
        }

        private static float EvaluateCssEase(float time, float duration, float overshootOrAmplitude, float period)
        {
            float normalizedTime = duration <= 0f ? 1f : time / duration;
            return CubicBezier(normalizedTime, 0.25f, 0.1f, 0.25f, 1f);
        }

        private static float EvaluateCssEaseOut(float time, float duration, float overshootOrAmplitude, float period)
        {
            float normalizedTime = duration <= 0f ? 1f : time / duration;
            return CubicBezier(normalizedTime, 0f, 0f, 0.58f, 1f);
        }

        private static float Sample(float t, float first, float second)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * t * first +
                3f * inverse * t * t * second +
                t * t * t;
        }

        private static float SampleDerivative(float t, float first, float second)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * first +
                6f * inverse * t * (second - first) +
                3f * t * t * (1f - second);
        }
    }
}
