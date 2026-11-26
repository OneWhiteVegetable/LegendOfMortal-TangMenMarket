using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Market
{
    // Tracking and shear for short, single-line labels. Layout and the glyph atlas remain owned by Text.
    internal sealed class MarketLetterSpacing : BaseMeshEffect
    {
        private float _tracking;
        private float _shear;

        internal static void Attach(Text text, float trackingEm, float shear = 0f)
        {
            MarketLetterSpacing spacing = text.GetComponent<MarketLetterSpacing>();
            if (spacing == null)
                spacing = text.gameObject.AddComponent<MarketLetterSpacing>();
            spacing._tracking = trackingEm;
            spacing._shear = shear;
            text.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            Text text = graphic as Text;
            int glyphs = mesh.currentVertCount / 4;
            if (!IsActive() || text == null || glyphs == 0)
                return;
            float advance = text.fontSize * _tracking;
            float origin = ((int)text.alignment % 3) * 0.5f * (glyphs - 1);
            float centerY = text.rectTransform.rect.center.y;
            UIVertex vertex = default(UIVertex);
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 at = vertex.position;
                at.x += (i / 4 - origin) * advance + (at.y - centerY) * _shear;
                vertex.position = at;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
