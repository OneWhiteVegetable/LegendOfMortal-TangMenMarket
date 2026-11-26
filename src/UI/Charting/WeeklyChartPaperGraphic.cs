using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Charting
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class WeeklyChartPaperGraphic : MaskableGraphic
    {
        internal Color32 PaperColor = new Color32(246, 238, 216, 242);
        internal Color32 BorderColor = new Color32(108, 88, 57, 64);
        internal Color32 AccentColor = new Color32(116, 103, 84, 255);
        internal Color32 ShadowColor = new Color32(66, 49, 29, 0);
        internal float AccentWidth = 2f;
        internal Vector2 ShadowOffset = Vector2.zero;

        protected override void Awake()
        {
            base.Awake();
            color = Color.white;
            raycastTarget = false;
        }

        internal void RefreshStyle()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            if (ShadowColor.a > 0)
            {
                AddRect(vertexHelper, new Rect(
                    rect.xMin + ShadowOffset.x,
                    rect.yMin + ShadowOffset.y,
                    rect.width,
                    rect.height), ShadowColor);
            }
            AddRect(vertexHelper, rect, PaperColor);
            WeeklyChartMesh.AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), 1f, BorderColor, BorderColor);
            WeeklyChartMesh.AddLine(vertexHelper, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), 1f, BorderColor, BorderColor);
            WeeklyChartMesh.AddLine(vertexHelper, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), 1f, BorderColor, BorderColor);
            WeeklyChartMesh.AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), 1f, BorderColor, BorderColor);
            if (AccentWidth > 0f && AccentColor.a > 0)
            {
                WeeklyChartMesh.AddLine(
                    vertexHelper,
                    new Vector2(rect.xMin + AccentWidth * 0.5f, rect.yMin),
                    new Vector2(rect.xMin + AccentWidth * 0.5f, rect.yMax),
                    AccentWidth,
                    AccentColor,
                    AccentColor);
            }
        }

        private static void AddRect(VertexHelper vertices, Rect rect, Color32 color)
        {
            WeeklyChartMesh.AddQuad(
                vertices,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMax, rect.yMin),
                color,
                color,
                color,
                color);
        }
    }
}
