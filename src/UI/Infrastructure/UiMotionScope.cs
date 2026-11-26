using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace XingShangMod.UI.Infrastructure
{
    /// <summary>
    /// Owns the tweens and MotionPivot targets of one View. All tracked tweens use unscaled time.
    /// Disabling the root cancels this scope only; destroying it disposes this scope only.
    /// </summary>
    internal sealed class UiMotionScope : IDisposable
    {
        private readonly GameObject _root;
        private readonly List<Tween> _tweens = new List<Tween>();
        private readonly List<UiMotionTarget> _targets = new List<UiMotionTarget>();
        private UiMotion _motion;
        private UiMotionScopeLifetime _lifetime;
        private bool _disposed;

        /// <summary>Creates an independent scope with a fixed initial reduced-motion value.</summary>
        internal UiMotionScope(GameObject root, bool reducedMotion = false)
        {
            if (root == null)
                throw new ArgumentNullException("root");
            _root = root;
            ReducedMotion = reducedMotion;
            _lifetime = root.AddComponent<UiMotionScopeLifetime>();
            _lifetime.Attach(this);
        }

        /// <summary>Creates an independent scope rooted at a RectTransform or another UI component.</summary>
        internal UiMotionScope(Component root, bool reducedMotion = false)
            : this(root == null ? null : root.gameObject, reducedMotion)
        {
        }

        /// <summary>Creates a scope that follows UiMotion.SetReducedMotion/Toggle until disposed.</summary>
        internal UiMotionScope(GameObject root, UiMotion motion)
            : this(root, motion == null ? false : motion.ReducedMotion)
        {
            if (motion == null)
                throw new ArgumentNullException("motion");
            _motion = motion;
            _motion.ReducedMotionChanged += SetReducedMotion;
        }

        /// <summary>Creates a root-component scope that follows the shared UiMotion setting.</summary>
        internal UiMotionScope(Component root, UiMotion motion)
            : this(root == null ? null : root.gameObject, motion)
        {
        }

        /// <summary>Whether new motion is suppressed and active motion is settled immediately.</summary>
        internal bool ReducedMotion { get; private set; }

        /// <summary>
        /// Current reuse generation. Capture this value before an asynchronous operation and check IsCurrent
        /// in its callback; ResetForReuse, root Disable and Dispose invalidate older generations.
        /// </summary>
        internal int Generation { get; private set; }

        /// <summary>Returns true only while this scope is alive and generation is its current generation.</summary>
        internal bool IsCurrent(int generation)
        {
            return !_disposed && _root != null && Generation == generation;
        }

        /// <summary>
        /// Attaches pointer/select behavior directly to motionPivot. The pivot must be a child of the scope root.
        /// Use the overload with eventSource when interaction is on LayoutRoot and only a child pivot should move.
        /// A CanvasGroup is optional; when supplied, rest/hover/press alpha is animated too.
        /// </summary>
        internal UiMotionTarget Attach(RectTransform motionPivot, CanvasGroup canvasGroup = null)
        {
            return Attach(motionPivot, motionPivot, canvasGroup);
        }

        /// <summary>
        /// Attaches pointer/select behavior to eventSource but animates only motionPivot, never its LayoutRoot.
        /// Multiple targets may share an eventSource when they drive different pivots.
        /// </summary>
        internal UiMotionTarget Attach(
            RectTransform motionPivot,
            Component eventSource,
            CanvasGroup canvasGroup = null)
        {
            EnsureUsable();
            if (motionPivot == null)
                throw new ArgumentNullException("motionPivot");
            if (eventSource == null)
                throw new ArgumentNullException("eventSource");
            EnsureInScope(motionPivot);
            EnsureInScope(eventSource);

            UiMotionTarget[] existing = eventSource.GetComponents<UiMotionTarget>();
            for (int i = 0; i < existing.Length; i++)
            {
                UiMotionTarget target = existing[i];
                if (target == null || target.IsDisposed || target.Target != motionPivot)
                    continue;
                if (!ReferenceEquals(target.Owner, this))
                    throw new InvalidOperationException("A MotionPivot target cannot be owned by two UiMotionScope instances.");
                return target;
            }

            UiMotionTarget created = eventSource.gameObject.AddComponent<UiMotionTarget>();
            created.Bind(this, motionPivot, canvasGroup);
            _targets.Add(created);
            return created;
        }

        /// <summary>
        /// Tracks a tween with unscaled time and scope-only cleanup. The scope owns OnKill for list maintenance;
        /// supply completion through Track(tween, callback) instead of replacing OnKill afterward.
        /// </summary>
        internal T Track<T>(T tween) where T : Tween
        {
            if (tween == null)
                return null;
            if (_disposed || _root == null)
            {
                SafeTween.Kill(tween);
                return tween;
            }
            if (ReducedMotion)
            {
                if (tween.IsActive() && tween.Loops() < 0)
                    SafeTween.Kill(tween);
                else
                    SafeTween.Kill(tween, true);
                return tween;
            }

            T tracked = tween;
            tween.OnKill(delegate { _tweens.Remove(tracked); });
            tween.SetUpdate(true);
            _tweens.Add(tween);
            return tween;
        }

        /// <summary>
        /// Tracks a tween and guards its completion against reuse and Dispose. This overwrites OnComplete.
        /// The callback runs on Unity's main thread only if the captured generation is still current.
        /// </summary>
        internal T Track<T>(T tween, TweenCallback complete) where T : Tween
        {
            if (tween == null)
                return null;
            if (complete != null)
            {
                int generation = Generation;
                tween.OnComplete(delegate
                {
                    if (IsCurrent(generation))
                        complete();
                });
            }
            return Track(tween);
        }

        /// <summary>
        /// Cancels this scope without completing callbacks, restores every attached target to its captured rest pose,
        /// clears hover/press/focus, and invalidates old callbacks. Call before binding pooled data or hiding a View.
        /// Returns the new generation.
        /// </summary>
        internal int ResetForReuse()
        {
            if (_disposed)
                return Generation;

            Generation = unchecked(Generation + 1);
            UiMotionTarget[] targets = _targets.ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].ResetForReuse();
            }
            CancelTrackedTweens();
            return Generation;
        }

        /// <summary>
        /// Changes reduced-motion behavior. Enabling it first snaps interactive targets to their current desired
        /// rest/hover/press pose, then completes finite tracked timelines and cancels infinite ones.
        /// Disabling it does not replay old motion.
        /// </summary>
        internal void SetReducedMotion(bool value)
        {
            if (_disposed || ReducedMotion == value)
                return;
            ReducedMotion = value;
            if (!value)
                return;

            UiMotionTarget[] targets = _targets.ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].SettleForReducedMotion();
            }
            CompleteTrackedTweens();
        }

        /// <summary>
        /// Kills only this scope's live tweens without completion, detaches its global subscription and removes
        /// components added by this scope. The root GameObject and all visual content remain intact.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        internal void DisposeFromRoot()
        {
            Dispose(false);
        }

        private void Dispose(bool destroyComponents)
        {
            if (_disposed)
                return;
            _disposed = true;
            Generation = unchecked(Generation + 1);

            if (_motion != null)
            {
                _motion.ReducedMotionChanged -= SetReducedMotion;
                _motion = null;
            }
            if (_lifetime != null)
            {
                UiMotionScopeLifetime lifetime = _lifetime;
                _lifetime = null;
                lifetime.Detach(this);
                if (destroyComponents)
                    UnityEngine.Object.Destroy(lifetime);
            }

            UiMotionTarget[] targets = _targets.ToArray();
            _targets.Clear();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].Dispose(destroyComponents);
            }
            CancelTrackedTweens();
        }

        internal void Forget(UiMotionTarget target)
        {
            _targets.Remove(target);
        }

        private void EnsureUsable()
        {
            if (_disposed)
                throw new ObjectDisposedException("UiMotionScope");
            if (_root == null)
                throw new ObjectDisposedException("UiMotionScope", "The scope root has been destroyed.");
        }

        private void EnsureInScope(Component component)
        {
            if (!component.transform.IsChildOf(_root.transform))
                throw new ArgumentException("Motion targets and event sources must belong to the scope root.");
        }

        private void CancelTrackedTweens()
        {
            if (_tweens.Count == 0)
                return;
            Tween[] tweens = _tweens.ToArray();
            _tweens.Clear();
            for (int i = 0; i < tweens.Length; i++)
                SafeTween.Kill(tweens[i]);
        }

        private void CompleteTrackedTweens()
        {
            if (_tweens.Count == 0)
                return;
            Tween[] tweens = _tweens.ToArray();
            _tweens.Clear();
            for (int i = 0; i < tweens.Length; i++)
            {
                Tween tween = tweens[i];
                if (tween != null && tween.IsActive() && tween.Loops() < 0)
                    SafeTween.Kill(tween);
                else
                    SafeTween.Kill(tween, true);
            }
        }
    }

    internal sealed class UiMotionScopeLifetime : MonoBehaviour
    {
        private UiMotionScope _owner;

        internal void Attach(UiMotionScope owner)
        {
            _owner = owner;
        }

        internal void Detach(UiMotionScope owner)
        {
            if (ReferenceEquals(_owner, owner))
                _owner = null;
        }

        private void OnDisable()
        {
            if (_owner != null)
                _owner.ResetForReuse();
        }

        private void OnDestroy()
        {
            UiMotionScope owner = _owner;
            _owner = null;
            if (owner != null)
                owner.DisposeFromRoot();
        }
    }
}
