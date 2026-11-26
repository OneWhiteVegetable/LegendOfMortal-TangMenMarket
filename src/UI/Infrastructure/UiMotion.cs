using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiMotion
    {
        private readonly Dictionary<CanvasGroup, FadeState> _fades =
            new Dictionary<CanvasGroup, FadeState>();

        /// <summary>Raised after the shared reduced-motion setting changes; scopes unsubscribe on Dispose.</summary>
        internal event Action<bool> ReducedMotionChanged;

        internal bool ReducedMotion { get; private set; }

        internal void Toggle()
        {
            SetReducedMotion(!ReducedMotion);
        }

        /// <summary>Sets the shared preference and settles registered fades and every scope using this instance.</summary>
        internal void SetReducedMotion(bool value)
        {
            if (ReducedMotion == value)
                return;
            ReducedMotion = value;
            if (value)
                SettleFadesForReducedMotion();
            Action<bool> changed = ReducedMotionChanged;
            if (changed != null)
                changed(value);
        }

        /// <summary>Creates a View-owned scope that follows this shared setting and uses unscaled time.</summary>
        internal UiMotionScope CreateScope(GameObject root)
        {
            return new UiMotionScope(root, this);
        }

        /// <summary>Creates a View-owned scope rooted at a RectTransform or another UI component.</summary>
        internal UiMotionScope CreateScope(Component root)
        {
            return new UiMotionScope(root, this);
        }

        internal void FadeIn(CanvasGroup group, float duration = 0.16f)
        {
            if (group == null)
                return;
            CancelFade(group);
            group.blocksRaycasts = true;
            group.interactable = true;
            if (ReducedMotion || duration <= 0f)
            {
                group.alpha = 1f;
                return;
            }
            group.alpha = 0f;
            try
            {
                Tween tween = group.DOFade(1f, duration);
                if (tween == null)
                {
                    group.alpha = 1f;
                    return;
                }
                TrackFade(group, tween, false, null);
            }
            catch (Exception)
            {
                CancelFade(group);
                group.alpha = 1f;
            }
        }

        internal void FadeOut(CanvasGroup group, float duration, TweenCallback complete)
        {
            if (group == null)
            {
                if (complete != null)
                    complete();
                return;
            }
            CancelFade(group);
            group.blocksRaycasts = false;
            group.interactable = false;
            if (ReducedMotion || duration <= 0f)
            {
                group.alpha = 0f;
                if (complete != null)
                    complete();
                return;
            }
            try
            {
                Tween tween = group.DOFade(0f, duration);
                if (tween == null)
                {
                    group.alpha = 0f;
                    if (complete != null)
                        complete();
                    return;
                }
                TrackFade(group, tween, true, complete);
            }
            catch (Exception)
            {
                CancelFade(group);
                group.alpha = 0f;
                if (complete != null)
                    complete();
            }
        }

        internal void Kill(GameObject target)
        {
            if (target == null)
                return;
            List<CanvasGroup> fadingGroups = null;
            foreach (CanvasGroup group in _fades.Keys)
            {
                if (group != null && group.gameObject == target)
                {
                    if (fadingGroups == null)
                        fadingGroups = new List<CanvasGroup>();
                    fadingGroups.Add(group);
                }
            }
            if (fadingGroups != null)
            {
                for (int i = 0; i < fadingGroups.Count; i++)
                    CancelFade(fadingGroups[i]);
            }
            SafeTween.Kill(target);
        }

        private void TrackFade(CanvasGroup group, Tween tween, bool fadingOut, TweenCallback complete)
        {
            FadeState state = new FadeState
            {
                Group = group,
                Tween = tween,
                FadingOut = fadingOut,
                Complete = complete
            };
            _fades[group] = state;
            tween.SetUpdate(true).OnKill(delegate { ForgetFade(state); });
            if (fadingOut)
                tween.OnComplete(delegate { CompleteFade(state); });
        }

        private void CompleteFade(FadeState state)
        {
            if (!IsCurrentFade(state))
                return;
            _fades.Remove(state.Group);
            if (state.Group != null)
                state.Group.alpha = state.FadingOut ? 0f : 1f;
            TweenCallback complete = state.Complete;
            state.Complete = null;
            if (complete != null)
                complete();
        }

        private void CancelFade(CanvasGroup group)
        {
            FadeState state;
            if (!_fades.TryGetValue(group, out state))
                return;
            _fades.Remove(group);
            state.Complete = null;
            SafeTween.Kill(state.Tween);
        }

        private void SettleFadesForReducedMotion()
        {
            if (_fades.Count == 0)
                return;
            List<FadeState> states = new List<FadeState>(_fades.Values);
            for (int i = 0; i < states.Count; i++)
            {
                FadeState state = states[i];
                if (!IsCurrentFade(state))
                    continue;
                _fades.Remove(state.Group);
                SafeTween.Kill(state.Tween);
                if (state.Group != null)
                    state.Group.alpha = state.FadingOut ? 0f : 1f;
                TweenCallback complete = state.Complete;
                state.Complete = null;
                if (complete != null)
                    complete();
            }
        }

        private void ForgetFade(FadeState state)
        {
            if (IsCurrentFade(state))
                _fades.Remove(state.Group);
        }

        private bool IsCurrentFade(FadeState state)
        {
            FadeState current;
            return state != null && state.Group != null &&
                _fades.TryGetValue(state.Group, out current) && ReferenceEquals(current, state);
        }

        private sealed class FadeState
        {
            internal CanvasGroup Group;
            internal Tween Tween;
            internal bool FadingOut;
            internal TweenCallback Complete;
        }
    }
}
