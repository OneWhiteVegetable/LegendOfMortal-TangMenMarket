using System;
using System.Collections.Generic;
using DG.Tweening;

namespace XingShangMod.UI.Infrastructure
{
    public static class SafeTween
    {
        public static Action<string> LogWarning { get; set; }

        public static void Kill(Tween tween, bool complete = false)
        {
            if (tween == null)
            {
                return;
            }
            try
            {
                tween.Kill(complete);
            }
            catch (Exception exception)
            {
                Report(exception);
            }
        }

        public static int Kill(object target, bool complete = false)
        {
            if (target == null)
            {
                return 0;
            }
            try
            {
                return DOTween.Kill(target, complete);
            }
            catch (Exception exception)
            {
                Report(exception);
                return 0;
            }
        }

        public static void KillAll(IEnumerable<Tween> tweens, bool complete = false)
        {
            if (tweens == null)
            {
                return;
            }
            List<Tween> copy = new List<Tween>(tweens);
            foreach (Tween tween in copy)
            {
                Kill(tween, complete);
            }
        }

        private static void Report(Exception exception)
        {
            Action<string> logger = LogWarning;
            if (logger == null)
            {
                return;
            }
            try
            {
                logger("[XingShangMod.UI] DOTween cleanup failed: " + exception.Message);
            }
            catch
            {
            }
        }
    }
}
