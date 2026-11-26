using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Market
{
    // A one-screen-pixel coverage fringe on solid uGUI quads, including on a canvas without MSAA.
    internal sealed class MarketPaperEdge : BaseMeshEffect
    {
        internal float Slant;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount != 4 || graphic.canvas == null)
                return;
            RectTransform rect = graphic.rectTransform;
            Camera camera = graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : graphic.canvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            float xScale = Vector2.Distance(center, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.right)));
            float yScale = Vector2.Distance(center, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up)));
            if (xScale < 0.01f || yScale < 0.01f)
                return;
            Vector2 inset = new Vector2(Mathf.Min(rect.rect.width * 0.25f, 0.5f / xScale),
                Mathf.Min(rect.rect.height * 0.25f, 0.5f / yScale));
            Vector2 middle = rect.rect.center;
            for (int i = 0; i < 4; i++)
            {
                UIVertex vertex = default(UIVertex);
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 position = vertex.position;
                Vector3 direction = new Vector3(position.x < middle.x ? -inset.x : inset.x,
                    position.y < middle.y ? -inset.y : inset.y, 0f);
                position.x += (position.y - middle.y) * Slant;
                vertex.position = position - direction;
                mesh.SetUIVertex(vertex, i);
                vertex.position = position + direction;
                Color32 color = vertex.color;
                color.a = 0;
                vertex.color = color;
                mesh.AddVert(vertex);
            }
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                mesh.AddTriangle(i, 4 + i, 4 + next);
                mesh.AddTriangle(i, 4 + next, next);
            }
        }
    }
}
