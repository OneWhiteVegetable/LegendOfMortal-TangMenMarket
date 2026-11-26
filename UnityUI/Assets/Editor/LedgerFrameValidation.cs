using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace XingShang.UI.Authoring
{
    internal static class LedgerFrameValidation
    {
        internal struct PaintedButton
        {
            internal RectInt Bounds;
            internal Color32 Color;
        }

        internal static void UseFixturePointer()
        {
            // Opening a production modal activates its input module again. Keep driving this
            // case with the explicit pointer; a second desktop mouse can otherwise open unrelated tooltips.
            foreach (BaseInputModule module in UnityEngine.Object.FindObjectsOfType<BaseInputModule>())
                module.enabled = false;
        }

        internal static RectTransform[] Frames(RuntimePreviewInteractionContext context)
        {
            var frames = new RectTransform[8];
            for (int i = 0; i < 6; i++)
                frames[i] = context.Adapter.CardActionButton(i, "QuantityIncrement").transform.parent as RectTransform;
            for (int i = 0; i < 2; i++)
                frames[6 + i] = context.Adapter.CartRowButton(i, "QuantityIncrement").transform.parent as RectTransform;
            return frames;
        }

        internal static Vector2 SubpixelOffset(RectTransform rect, int phase)
        {
            Rect screen = ScreenRect(rect);
            return new Vector2(.25f * phase * rect.rect.width / screen.width,
                .25f * (4 - phase) * rect.rect.height / screen.height);
        }

        internal static PaintedButton Read(RuntimePreviewInteractionContext context, Button button,
            RuntimePreviewInteractionSample sample, RectTransform[] frames = null)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(sample.png), false))
                    throw new InvalidDataException("Cannot read the actual captured ledger PNG.");
                Color32[] pixels = image.GetPixels32();
                if (frames != null)
                {
                    for (int i = 0; i < frames.Length; i++)
                    {
                        Rect r = ScreenRect(frames[i]);
                        int left = Mathf.RoundToInt(r.xMin), bottom = Mathf.RoundToInt(r.yMin);
                        int right = Mathf.RoundToInt(r.xMax) - 1, top = Mathf.RoundToInt(r.yMax) - 1;
                        CheckEdge(context, sample, i, "left", pixels, image.width, left, bottom + 3, 0, 1, top - bottom - 5);
                        CheckEdge(context, sample, i, "right", pixels, image.width, right, bottom + 3, 0, 1, top - bottom - 5);
                        CheckEdge(context, sample, i, "bottom", pixels, image.width, left + 3, bottom, 1, 0, right - left - 5);
                        CheckEdge(context, sample, i, "top", pixels, image.width, left + 3, top, 1, 0, right - left - 5);
                    }
                }
                Rect hit = ScreenRect(button.GetComponent<RectTransform>());
                int probeX = Mathf.RoundToInt(hit.xMin + hit.width * .17f);
                int probeY = Mathf.RoundToInt(hit.center.y);
                int xMin = int.MaxValue, yMin = int.MaxValue, xMax = -1, yMax = -1;
                for (int x = (int)hit.xMin - 8; x < (int)hit.xMax + 8; x++)
                {
                    if (!Red(pixels[probeY * image.width + x])) continue;
                    xMin = Math.Min(xMin, x);
                    xMax = Math.Max(xMax, x);
                }
                for (int y = (int)hit.yMin - 8; y < (int)hit.yMax + 8; y++)
                {
                    if (!Red(pixels[y * image.width + probeX])) continue;
                    yMin = Math.Min(yMin, y);
                    yMax = Math.Max(yMax, y);
                }
                if (xMax < xMin || yMax < yMin) throw new InvalidDataException("Checkout has no visible red face.");
                return new PaintedButton
                {
                    Bounds = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1),
                    Color = pixels[probeY * image.width + probeX]
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void CheckEdge(RuntimePreviewInteractionContext context, RuntimePreviewInteractionSample sample,
            int frame, string side, Color32[] pixels, int width, int x, int y, int dx, int dy, int length)
        {
            for (int i = 0; i < length; i++, x += dx, y += dy)
            {
                int dark = 0;
                for (int cross = -2; cross <= 2; cross++)
                {
                    Color32 value = pixels[(y + cross * dx) * width + x + cross * dy];
                    if (value.r < 185 && value.g < 185 && value.b < 185) dark++;
                }
                if (dark == 1) continue;
                context.Fail(sample.label + " " + (frame < 6 ? "card " + (frame + 1) : "ledger " + (frame - 5)) +
                    " " + side + " has " + dark + " dark pixels across the edge at " + x + "," + y + "; expected one.");
                return;
            }
        }

        internal static void CheckPose(RuntimePreviewInteractionContext context, Button button,
            PaintedButton rest, PaintedButton painted, float dx, float dy, bool bright, string label)
        {
            float density = button.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            context.Check(Math.Abs(painted.Bounds.width - rest.Bounds.width) <= 1 &&
                Math.Abs(painted.Bounds.height - rest.Bounds.height) <= 1,
                label + " changed the size of the visible checkout face.");
            context.Check(Math.Abs(painted.Bounds.xMin - rest.Bounds.xMin - dx * density) <= 1.1f &&
                Math.Abs(painted.Bounds.yMin - rest.Bounds.yMin - dy * density) <= 1.1f,
                label + " did not move the visible checkout face to the CSS position.");
            context.Check(bright ? painted.Color.r >= rest.Color.r + 11 : Math.Abs(painted.Color.r - rest.Color.r) <= 2,
                label + " did not show the expected checkout face color.");
        }

        private static bool Red(Color32 c) { return c.r > 120 && c.r - c.g > 61; }

        private static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
