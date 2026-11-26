using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiLedgerWatermark : MaskableGraphic
    {
        internal static void Create(RectTransform window)
        {
            var go = new GameObject("PaperWatermark", typeof(RectTransform), typeof(CanvasRenderer));
            var mark = go.AddComponent<UiLedgerWatermark>();
            mark.raycastTarget = false;
            mark.color = new Color32(74, 87, 77, 10);
            RectTransform rect = mark.rectTransform;
            rect.SetParent(window, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-120f, 125f);
            rect.sizeDelta = new Vector2(310f, 310f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float half = rectTransform.rect.width * .5f;
            float unit = 1f / Mathf.Max(1f, canvas == null ? 1f : canvas.scaleFactor);
            AddBorder(mesh, half, unit);
            AddBorder(mesh, half - 64f / 3f, unit);
        }

        private void AddBorder(VertexHelper mesh, float radius, float unit)
        {
            int start = mesh.currentVertCount;
            Color32 ink = color;
            Color32 clear = ink;
            clear.a = 0;
            AddSquare(mesh, radius + unit * .5f, clear);
            AddSquare(mesh, radius - unit * .5f, ink);
            AddSquare(mesh, radius - 32f / 3f + unit * .5f, ink);
            AddSquare(mesh, radius - 32f / 3f - unit * .5f, clear);
            for (int band = 0; band < 3; band++)
                for (int i = 0; i < 4; i++)
                {
                    int a = start + band * 4 + i, b = start + band * 4 + (i + 1) % 4;
                    mesh.AddTriangle(a, b, b + 4);
                    mesh.AddTriangle(a, b + 4, a + 4);
                }
        }

        private static void AddSquare(VertexHelper mesh, float r, Color32 ink)
        {
            mesh.AddVert(new Vector2(-r,-r), ink, Vector2.zero);
            mesh.AddVert(new Vector2(r,-r), ink, Vector2.zero);
            mesh.AddVert(new Vector2(r,r), ink, Vector2.zero);
            mesh.AddVert(new Vector2(-r,r), ink, Vector2.zero);
        }
    }
}
