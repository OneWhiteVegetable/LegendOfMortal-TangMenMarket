using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Shared
{
    // back_money is a Simple sprite without nine-slice metadata. Stretch only its center.
    internal sealed class MoneyFrameSlicing : BaseMeshEffect
    {
        private float _originalWidth;

        internal void SetOriginalWidth(float value)
        {
            _originalWidth = value;
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            Rect rect = graphic.rectTransform.rect;
            if (!IsActive() || mesh.currentVertCount != 4 || _originalWidth <= 0f ||
                rect.width <= _originalWidth + .01f)
                return;
            UIVertex bl = default(UIVertex), tl = default(UIVertex), tr = default(UIVertex), br = default(UIVertex);
            mesh.PopulateUIVertex(ref bl, 0);
            mesh.PopulateUIVertex(ref tl, 1);
            mesh.PopulateUIVertex(ref tr, 2);
            mesh.PopulateUIVertex(ref br, 3);
            mesh.Clear();
            float left = bl.position.x, right = br.position.x;
            float endWidth = _originalWidth * .25f;
            AddColumn(mesh, bl, tl, br, tr, left, 0f);
            AddColumn(mesh, bl, tl, br, tr, left + endWidth, .25f);
            AddColumn(mesh, bl, tl, br, tr, right - endWidth, .75f);
            AddColumn(mesh, bl, tl, br, tr, right, 1f);
            for (int i = 0; i < 6; i += 2)
            {
                mesh.AddTriangle(i, i + 1, i + 3);
                mesh.AddTriangle(i, i + 3, i + 2);
            }
        }

        private static void AddColumn(VertexHelper mesh, UIVertex lower, UIVertex upper,
            UIVertex lowerRight, UIVertex upperRight, float x, float t)
        {
            lower.position.x = upper.position.x = x;
            lower.uv0 = Vector4.Lerp(lower.uv0, lowerRight.uv0, t);
            upper.uv0 = Vector4.Lerp(upper.uv0, upperRight.uv0, t);
            mesh.AddVert(lower);
            mesh.AddVert(upper);
        }
    }
}
