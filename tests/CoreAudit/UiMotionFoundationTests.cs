#if UI_MOTION_AUDIT
using System;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;
using static AuditAssert;

internal sealed class UiMotionFoundationTests
{
    public void Register(TestRunner runner)
    {
        runner.Run("motion-css-easing-matches-independent-bezier-solve", TestCssEasing);
        runner.Run("motion-token-follows-flight-keyframes", TestTokenKeyframes);
        runner.Run("motion-stamp-transform-spans-42-to-100-percent", TestStampKeyframes);
        runner.Run("motion-cart-target-uses-prototype-anchor", TestCartTarget);
        runner.Run("motion-ticker-completes-one-group-in-32-seconds", TestTicker);
    }

    private static void TestCssEasing()
    {
        float[] samples = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f };
        for (int i = 0; i < samples.Length; i++)
        {
            float time = samples[i];
            NearMotion(
                ReferenceBezier(time, 0.16f, 1f, 0.3f, 1f),
                UiMotionEasing.PaperOut(time, 1f, 0f, 0f),
                "PaperOut at " + time);
            NearMotion(
                ReferenceBezier(time, 0.25f, 0.1f, 0.25f, 1f),
                UiMotionEasing.CssEase(time, 1f, 0f, 0f),
                "CSS ease at " + time);
            NearMotion(
                ReferenceBezier(time, 0f, 0f, 0.58f, 1f),
                UiMotionEasing.CssEaseOut(time, 1f, 0f, 0f),
                "CSS ease-out at " + time);
        }
    }

    private static void TestTokenKeyframes()
    {
        float progress;
        float rotation;
        float scale;
        float alpha;
        UiMotionKeyframes.EvaluateToken(0f, out progress, out rotation, out scale, out alpha);
        NearMotion(0f, progress, "token initial progress");
        NearMotion(1f, scale, "token initial scale");
        NearMotion(1f, alpha, "token initial alpha");

        UiMotionKeyframes.EvaluateToken(0.5f, out progress, out rotation, out scale, out alpha);
        float expectedProgress = ReferenceBezier(0.5f, 0.5f, 0f, 0.7f, 0.2f);
        NearMotion(expectedProgress, progress, "token midpoint progress");
        NearMotion(-9f * expectedProgress, rotation, "token midpoint rotation");
        NearMotion(1f - 0.75f * expectedProgress, scale, "token midpoint scale");
        NearMotion(1f, alpha, "token remains opaque through 60 percent");

        UiMotionKeyframes.EvaluateToken(0.8f, out progress, out rotation, out scale, out alpha);
        float expectedFade = ReferenceBezier(0.5f, 0.5f, 0f, 0.7f, 0.2f);
        NearMotion(1f - expectedFade, alpha, "token opacity must use the flight easing after 60 percent");

        UiMotionKeyframes.EvaluateToken(1f, out progress, out rotation, out scale, out alpha);
        NearMotion(1f, progress, "token final progress");
        NearMotion(-9f, rotation, "token final rotation maps CSS positive to Unity negative");
        NearMotion(0.25f, scale, "token final scale");
        NearMotion(0f, alpha, "token final alpha");
    }

    private static void TestStampKeyframes()
    {
        float scale;
        float rotation;
        float alpha;
        UiMotionKeyframes.EvaluateStamp(0f, out scale, out rotation, out alpha);
        NearMotion(2f, scale, "stamp initial scale");
        NearMotion(18f, rotation, "stamp initial rotation maps CSS -18 to Unity +18");
        NearMotion(0f, alpha, "stamp initial alpha");

        UiMotionKeyframes.EvaluateStamp(0.25f, out scale, out rotation, out alpha);
        NearMotion(0.93f, scale, "stamp 25 percent scale");
        NearMotion(8f, rotation, "stamp 25 percent rotation");
        NearMotion(1f, alpha, "stamp 25 percent alpha");

        UiMotionKeyframes.EvaluateStamp(0.42f, out scale, out rotation, out alpha);
        NearMotion(1f, scale, "stamp 42 percent scale");
        NearMotion(10f, rotation, "stamp 42 percent rotation");

        UiMotionKeyframes.EvaluateStamp(0.5f, out scale, out rotation, out alpha);
        float transformEase = ReferenceBezier((0.5f - 0.42f) / 0.58f, 0.16f, 1f, 0.3f, 1f);
        NearMotion(1f + 0.03f * transformEase, scale, "stamp transform must not hold from 42 to 78 percent");
        NearMotion(10f - transformEase, rotation, "stamp rotation must continue after 42 percent");
        NearMotion(1f, alpha, "stamp stays opaque before 78 percent");

        UiMotionKeyframes.EvaluateStamp(0.78f, out scale, out rotation, out alpha);
        transformEase = ReferenceBezier((0.78f - 0.42f) / 0.58f, 0.16f, 1f, 0.3f, 1f);
        NearMotion(1f + 0.03f * transformEase, scale, "stamp 78 percent transform");
        NearMotion(1f, alpha, "stamp 78 percent alpha");

        UiMotionKeyframes.EvaluateStamp(1f, out scale, out rotation, out alpha);
        NearMotion(1.03f, scale, "stamp final scale");
        NearMotion(9f, rotation, "stamp final rotation");
        NearMotion(0f, alpha, "stamp final alpha");
    }

    private static void TestCartTarget()
    {
        NearMotion(0.55f, UiMotionKeyframes.CartTargetXFraction, "cart horizontal target");
        NearMotion(45f, UiMotionKeyframes.CartTargetFromTop(100f), "short cart target from top");
        NearMotion(260f, UiMotionKeyframes.CartTargetFromTop(1000f), "cart target vertical cap");
        NearMotion(0f, UiMotionKeyframes.CartTargetFromTop(-1f), "invalid cart height");
    }

    private static void TestTicker()
    {
        NearMotion(500f, NewsTickerMotion.Advance(0f, 16f, 1000f), "ticker half cycle");
        NearMotion(250f, NewsTickerMotion.Advance(250f, 32f, 1000f), "ticker full cycle wraps one group");
        NearMotion(160f, NewsTickerMotion.Advance(0f, 8f, 640f), "ticker speed follows measured group width");
        NearMotion(90f, NewsTickerMotion.Wrap(-10f, 100f), "negative manual scroll wraps");
        NearMotion(0f, NewsTickerMotion.Advance(0f, float.NaN, 100f), "invalid delta is ignored");
        NearMotion(0f, NewsTickerMotion.Advance(0f, 1f, 0f), "empty ticker stays at rest");
    }

    private static float ReferenceBezier(float time, float x1, float y1, float x2, float y2)
    {
        if (time <= 0f)
            return 0f;
        if (time >= 1f)
            return 1f;
        double lower = 0.0;
        double upper = 1.0;
        double parameter = time;
        for (int i = 0; i < 64; i++)
        {
            double x = Sample(parameter, x1, x2);
            if (x < time)
                lower = parameter;
            else
                upper = parameter;
            parameter = (lower + upper) * 0.5;
        }
        return (float)Sample(parameter, y1, y2);
    }

    private static double Sample(double time, double first, double second)
    {
        double inverse = 1.0 - time;
        return 3.0 * inverse * inverse * time * first +
            3.0 * inverse * time * time * second +
            time * time * time;
    }

    private static void NearMotion(double expected, double actual, string message)
    {
        True(Math.Abs(expected - actual) <= 0.00001,
            message + ": expected " + expected + ", actual " + actual);
    }
}
#endif
