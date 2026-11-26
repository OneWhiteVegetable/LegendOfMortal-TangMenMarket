using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Charting
{
    internal static class WeeklyChartMesh
    {
        internal static void AddBrushSection(VertexHelper mesh, Vector2 top, Vector2 bottom,
            Color32 topInk, Color32 bottomInk, Vector2 uvOffset, float pixel, bool connect)
        {
            Vector2 span = top - bottom;
            float width = span.magnitude;
            Vector2 normal = width > 0.00001f ? span / width : Vector2.up;
            Vector2 middle = (top + bottom) * 0.5f;
            float core = Mathf.Max(0f, (width - pixel) * 0.5f);
            float outside = core + pixel;
            float coverage = Mathf.Min(1f, width / pixel);
            topInk.a = (byte)Mathf.RoundToInt(topInk.a * coverage);
            bottomInk.a = (byte)Mathf.RoundToInt(bottomInk.a * coverage);
            Color32 clearTop = topInk, clearBottom = bottomInk;
            clearTop.a = clearBottom.a = 0;
            int at = mesh.currentVertCount;
            AddVertex(mesh, middle + normal * outside, clearTop, true, uvOffset);
            AddVertex(mesh, middle + normal * core, topInk, true, uvOffset);
            AddVertex(mesh, middle - normal * core, bottomInk, true, uvOffset);
            AddVertex(mesh, middle - normal * outside, clearBottom, true, uvOffset);
            if (!connect) return;
            for (int j = 0; j < 3; j++)
            {
                int first = at - 4 + j;
                mesh.AddTriangle(first, first + 1, first + 5);
                mesh.AddTriangle(first, first + 5, first + 4);
            }
        }

        internal static void AddLine(
            VertexHelper vertices,
            Vector2 start,
            Vector2 end,
            float width,
            Color32 startColor,
            Color32 endColor,
            bool textured = false,
            Vector2 uvOffset = default(Vector2))
        {
            if (width <= 0f)
                return;
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.000001f)
                return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            AddQuad(
                vertices,
                start - normal,
                start + normal,
                end + normal,
                end - normal,
                startColor,
                startColor,
                endColor,
                endColor,
                textured,
                uvOffset);
        }

        internal static void AddQuad(
            VertexHelper vertices,
            Vector2 center,
            float size,
            Color32 color)
        {
            float half = size * 0.5f;
            AddQuad(
                vertices,
                center + new Vector2(-half, -half),
                center + new Vector2(-half, half),
                center + new Vector2(half, half),
                center + new Vector2(half, -half),
                color,
                color,
                color,
                color,
                false,
                Vector2.zero);
        }

        internal static void AddQuad(
            VertexHelper vertices,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color32 firstColor,
            Color32 secondColor,
            Color32 thirdColor,
            Color32 fourthColor,
            bool textured = false,
            Vector2 uvOffset = default(Vector2))
        {
            float winding =
                (second.x - first.x) * (third.y - first.y) -
                (second.y - first.y) * (third.x - first.x);
            if (winding > 0f)
            {
                Vector2 position = second;
                second = fourth;
                fourth = position;
                Color32 color = secondColor;
                secondColor = fourthColor;
                fourthColor = color;
            }
            int index = vertices.currentVertCount;
            AddVertex(vertices, first, firstColor, textured, uvOffset);
            AddVertex(vertices, second, secondColor, textured, uvOffset);
            AddVertex(vertices, third, thirdColor, textured, uvOffset);
            AddVertex(vertices, fourth, fourthColor, textured, uvOffset);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index + 2, index + 3, index);
        }

        internal static void AddCircle(
            VertexHelper vertices,
            Vector2 center,
            float radius,
            Color32 color,
            bool textured = false,
            Vector2 uvOffset = default(Vector2),
            int sides = 16)
        {
            if (radius <= 0f)
                return;
            sides = Mathf.Max(8, sides);
            int index = vertices.currentVertCount;
            AddVertex(vertices, center, color, textured, uvOffset);
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                AddVertex(
                    vertices,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color,
                    textured,
                    uvOffset);
            }
            for (int i = 0; i < sides; i++)
                vertices.AddTriangle(index, index + i + 2, index + i + 1);
        }

        private static void AddVertex(
            VertexHelper vertices,
            Vector2 position,
            Color32 color,
            bool textured,
            Vector2 uvOffset)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = textured ? (position + uvOffset) / 256f : Vector2.zero;
            vertices.AddVert(vertex);
        }
    }
}
