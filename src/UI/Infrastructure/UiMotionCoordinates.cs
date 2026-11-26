using UnityEngine;

namespace XingShangMod.UI.Infrastructure
{
    internal static class UiMotionCoordinates
    {
        internal static Vector2 CartTokenSize(RectTransform root, RectTransform cartTarget)
        {
            const float cssSize = 55f;
            if (root == null || cartTarget == null)
                return new Vector2(cssSize, cssSize);
            Vector3 rootScale = root.lossyScale;
            Vector3 targetScale = cartTarget.lossyScale;
            return new Vector2(
                cssSize * ScaleRatio(targetScale.x, rootScale.x),
                cssSize * ScaleRatio(targetScale.y, rootScale.y));
        }

        internal static bool TryGetCartFlight(
            RectTransform root,
            RectTransform source,
            RectTransform cartTarget,
            Vector2 untransformedTokenSize,
            out Vector2 start,
            out Vector2 end)
        {
            start = Vector2.zero;
            end = Vector2.zero;
            if (root == null || source == null || cartTarget == null)
                return false;

            Camera camera = ResolveCamera(root);
            Rect sourceRect = source.rect;
            Vector3 sourceWorld = source.TransformPoint(sourceRect.center);
            Rect targetRect = cartTarget.rect;
            Vector3 targetWorld = cartTarget.TransformPoint(new Vector3(
                targetRect.xMin + targetRect.width * UiMotionKeyframes.CartTargetXFraction,
                targetRect.yMax - UiMotionKeyframes.CartTargetFromTop(targetRect.height),
                0f));
            bool converted = TryWorldToCenteredAnchor(root, sourceWorld, camera, out start) &&
                TryWorldToCenteredAnchor(root, targetWorld, camera, out end);
            if (converted)
            {
                end += new Vector2(
                    untransformedTokenSize.x * 0.5f,
                    -untransformedTokenSize.y * 0.5f);
            }
            return converted;
        }

        private static float ScaleRatio(float target, float root)
        {
            target = Mathf.Abs(target);
            root = Mathf.Abs(root);
            if (float.IsNaN(target) || float.IsInfinity(target) ||
                float.IsNaN(root) || float.IsInfinity(root) || root <= 0.0001f)
                return 1f;
            return target / root;
        }

        private static bool TryWorldToCenteredAnchor(
            RectTransform root,
            Vector3 world,
            Camera camera,
            out Vector2 anchored)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, camera, out local))
            {
                anchored = Vector2.zero;
                return false;
            }
            anchored = local - root.rect.center;
            return !float.IsNaN(anchored.x) && !float.IsNaN(anchored.y) &&
                !float.IsInfinity(anchored.x) && !float.IsInfinity(anchored.y);
        }

        private static Camera ResolveCamera(RectTransform root)
        {
            Canvas canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;
            if (canvas.rootCanvas != null)
                canvas = canvas.rootCanvas;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
        }
    }
}
