using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventoryLoanSlip : MaskableGraphic
    {
        private Sprite _atlas;
        private string _extendedDigits;
        internal int RemainingWeeks { get; private set; } = -1;
        public override Texture mainTexture { get { return _atlas == null ? base.mainTexture : _atlas.texture; } }

        internal void Initialize(Sprite atlas)
        {
            _atlas = atlas;
            raycastTarget = false;
            SetAllDirty();
        }

        internal void SetRemaining(int weeks)
        {
            if (RemainingWeeks == weeks) return;
            RemainingWeeks = weeks;
            _extendedDigits = weeks > 24 ? weeks.ToString(CultureInfo.InvariantCulture) : null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_atlas == null || RemainingWeeks < 0) return;
            Rect r = rectTransform.rect;
            AddQuad(mesh, r, new Rect(432, 2, 76, 66), Color.white, 0f);
            var lettering = new Rect(r.xMin + 24f * r.width / 76f, r.yMax - 50f * r.height / 66f,
                45f * r.width / 76f, 29f * r.height / 66f);
            Color32 ink = RemainingWeeks <= 3 ? UiFormatting.Red : UiFormatting.Green;
            if (_extendedDigits == null)
            {
                // Every normal contract value uses one specially spaced composition.
                AddQuad(mesh, lettering, NumberCell(RemainingWeeks), ink, 5f);
                return;
            }

            // Keep the complete count for older contracts with an extended deadline.
            float advance = lettering.width / _extendedDigits.Length;
            float cellHeight = Mathf.Min(lettering.height, advance * 1.15f);
            float x = lettering.xMin;
            foreach (char digit in _extendedDigits)
            {
                Rect source = NumberCell(digit - '0');
                source.x += 20f;
                source.width = 44f;
                AddQuad(mesh, new Rect(x, lettering.center.y - cellHeight / 2f, advance, cellHeight), source, ink, 0f);
                x += advance;
            }
        }

        private static Rect NumberCell(int value)
        {
            return new Rect(value % 5 * 84f, value / 5 * 50f, 84f, 48f);
        }

        private static void AddQuad(VertexHelper mesh, Rect r, Rect source, Color32 color, float angle)
        {
            float left = source.xMin / 512f;
            float right = source.xMax / 512f;
            float top = 1f - source.yMin / 256f;
            float bottom = 1f - source.yMax / 256f;
            Vector3 center = r.center;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3 x = rotation * Vector3.right * (r.width / 2f);
            Vector3 y = rotation * Vector3.up * (r.height / 2f);
            int start = mesh.currentVertCount;
            mesh.AddVert(center-x-y, color, new Vector2(left, bottom));
            mesh.AddVert(center-x+y, color, new Vector2(left, top));
            mesh.AddVert(center+x+y, color, new Vector2(right, top));
            mesh.AddVert(center+x-y, color, new Vector2(right, bottom));
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
