using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    // A coverage fringe one screen pixel wide keeps tilted paper edges smooth at any canvas density.
    internal sealed class UiPaperShape : BaseMeshEffect
    {
        private readonly Vector2[] _corners = new Vector2[4];
        private readonly Vector2[] _outer = new Vector2[4];
        private float _shear;
        private bool _straightLeft;
        private Shadow _shadow;

        internal static UiPaperShape Attach(Image image, float shear = 0f)
        {
            UiPaperShape shape = image.GetComponent<UiPaperShape>();
            if (shape == null)
            {
                image.overrideSprite = null;
                image.sprite = null;
                shape = image.gameObject.AddComponent<UiPaperShape>();
                shape._shadow = image.GetComponent<Shadow>();
                if (shape._shadow != null) shape._shadow.enabled = false;
            }
            if (shape._shear != shear)
            {
                shape._shear = shear;
                image.SetVerticesDirty();
            }
            return shape;
        }

        internal void StraightLeft()
        {
            if (_straightLeft) return;
            _straightLeft = true;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            mesh.Clear();
            Rect r = graphic.rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            float skew = Mathf.Clamp(_shear, -r.width * .95f, r.width * .95f);
            _corners[0] = new Vector2(r.xMin + (_straightLeft ? 0f : Mathf.Max(0f, -skew)), r.yMin);
            _corners[1] = new Vector2(r.xMax - Mathf.Max(0f, skew), r.yMin);
            _corners[2] = new Vector2(r.xMax - Mathf.Max(0f, -skew), r.yMax);
            _corners[3] = new Vector2(r.xMin + (_straightLeft ? 0f : Mathf.Max(0f, skew)), r.yMax);
            float pixel = 1f / Mathf.Max(0.1f, graphic.canvas == null ? 1f : graphic.canvas.scaleFactor);
            // Draw the shadow with its own coverage ring to avoid a jagged silhouette behind the face.
            if (_shadow != null)
            {
                Color tint = _shadow.effectColor;
                if (_shadow.useGraphicAlpha) tint.a *= graphic.color.a;
                Fill(mesh, tint, _shadow.effectDistance, pixel);
            }
            Fill(mesh, graphic.color, Vector2.zero, pixel);
        }

        private void Fill(VertexHelper mesh, Color32 color, Vector2 offset, float pixel)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = (_corners[i] - _corners[(i + 3) % 4]).normalized;
                Vector2 b = (_corners[(i + 1) % 4] - _corners[i]).normalized;
                Vector2 normal = new Vector2(a.y + b.y, -a.x - b.x) /
                    Mathf.Max(0.08f, 1f + Vector2.Dot(a, b));
                Vector2 point = _corners[i] + offset;
                mesh.AddVert(point - normal * (pixel * 0.5f), color, Vector2.zero);
                _outer[i] = point + normal * (pixel * 0.5f);
            }
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
            color.a = 0;
            for (int i = 0; i < 4; i++) mesh.AddVert(_outer[i], color, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                int a = start + i, b = start + (i + 1) % 4;
                mesh.AddTriangle(a, a + 4, b + 4);
                mesh.AddTriangle(a, b + 4, b);
            }
        }
    }
}
