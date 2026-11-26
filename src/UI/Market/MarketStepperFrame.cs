using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketStepperFrame : BaseMeshEffect
    {
        private static readonly Color32 Ink = new Color32(32, 35, 33, 115);
        private Color32 _paper;
        private Rect _bounds;
        private Vector2 _pixel;

        internal static void Attach(RectTransform rect, bool ledger)
        {
            Image image = rect.GetComponent<Image>();
            Outline outline = rect.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            image.overrideSprite = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            var frame = rect.gameObject.AddComponent<MarketStepperFrame>();
            frame._paper = ledger ? new Color32(255, 255, 255, 97) : (Color32)UiFormatting.Paper;
            image.SetVerticesDirty();
        }

        private void LateUpdate()
        {
            Rect bounds;
            Vector2 pixel;
            Measure(out bounds, out pixel);
            if (bounds != _bounds || pixel != _pixel)
                graphic.SetVerticesDirty();
        }

        private void Measure(out Rect bounds, out Vector2 pixel)
        {
            RectTransform rect = graphic.rectTransform;
            bounds = rect.rect;
            pixel = Vector2.one;
            Canvas canvas = graphic.canvas;
            if (canvas == null || bounds.width < 1f || bounds.height < 1f) return;
            Canvas root = canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(bounds.min));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(new Vector2(bounds.xMax, bounds.yMin)));
            Vector2 top = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(new Vector2(bounds.xMin, bounds.yMax)));
            float width = Vector2.Distance(origin, right);
            float height = Vector2.Distance(origin, top);
            if (width < 1f || height < 1f) return;
            pixel = new Vector2(bounds.width / width, bounds.height / height);
            if (Mathf.Abs(right.y - origin.y) > .01f || Mathf.Abs(top.x - origin.x) > .01f) return;
            // Align the inside and outside to the same physical grid after all parent scaling.
            bounds = Rect.MinMaxRect(bounds.xMin + (Mathf.Round(origin.x) - origin.x) * pixel.x,
                bounds.yMin + (Mathf.Round(origin.y) - origin.y) * pixel.y,
                bounds.xMax + (Mathf.Round(right.x) - right.x) * pixel.x,
                bounds.yMax + (Mathf.Round(top.y) - top.y) * pixel.y);
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Measure(out _bounds, out _pixel);
            mesh.Clear();
            if (_bounds.width < 2f * _pixel.x || _bounds.height < 2f * _pixel.y) return;
            float left = _bounds.xMin, right = _bounds.xMax, top = _bounds.yMax, bottom = _bounds.yMin;
            Quad(mesh, left, bottom, right, top, _paper);
            Quad(mesh, left, bottom, left + _pixel.x, top, Ink);
            Quad(mesh, right - _pixel.x, bottom, right, top, Ink);
            Quad(mesh, left + _pixel.x, bottom, right - _pixel.x, bottom + _pixel.y, Ink);
            Quad(mesh, left + _pixel.x, top - _pixel.y, right - _pixel.x, top, Ink);
        }

        private static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, bottom), color, Vector2.zero);
            mesh.AddVert(new Vector2(left, top), color, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), color, Vector2.zero);
            mesh.AddVert(new Vector2(right, bottom), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
