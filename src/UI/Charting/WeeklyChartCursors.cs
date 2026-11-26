using System;
using UnityEngine;

namespace XingShangMod.UI.Charting
{
    internal enum WeeklyChartCursorKind
    {
        Default,
        Grab,
        Grabbing,
        VerticalResize
    }

    internal sealed class WeeklyChartCursors : IDisposable
    {
        private const int Size = 24;
        private readonly Texture2D _grab;
        private readonly Texture2D _grabbing;
        private readonly Texture2D _verticalResize;
        private bool _disposed;

        internal WeeklyChartCursors()
        {
            _grab = CreateTexture("XingShangMod.Chart.Grab", GrabMask());
            _grabbing = CreateTexture("XingShangMod.Chart.Grabbing", GrabbingMask());
            _verticalResize = CreateTexture("XingShangMod.Chart.VerticalResize", VerticalResizeMask());
        }

        internal void Apply(WeeklyChartCursorKind kind)
        {
            if (_disposed)
                return;
            Texture2D texture = kind == WeeklyChartCursorKind.Grab
                ? _grab
                : kind == WeeklyChartCursorKind.Grabbing
                    ? _grabbing
                    : kind == WeeklyChartCursorKind.VerticalResize ? _verticalResize : null;
            Cursor.SetCursor(texture, new Vector2(Size * 0.5f, Size * 0.5f), CursorMode.Auto);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Destroy(_grab);
            Destroy(_grabbing);
            Destroy(_verticalResize);
        }

        private static void Destroy(Texture2D texture)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        private static Texture2D CreateTexture(string name, bool[] shape)
        {
            var pixels = new Color32[Size * Size];
            Color32 outline = new Color32(32, 35, 33, 245);
            Color32 paper = new Color32(245, 240, 228, 250);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int index = y * Size + x;
                    if (shape[index])
                    {
                        pixels[index] = paper;
                        continue;
                    }
                    bool border = false;
                    for (int offsetY = -1; offsetY <= 1 && !border; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int sampleX = x + offsetX;
                            int sampleY = y + offsetY;
                            if (sampleX >= 0 && sampleX < Size && sampleY >= 0 && sampleY < Size &&
                                shape[sampleY * Size + sampleX])
                            {
                                border = true;
                                break;
                            }
                        }
                    }
                    if (border)
                        pixels[index] = outline;
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool[] GrabMask()
        {
            var mask = new bool[Size * Size];
            Rectangle(mask, 7, 5, 11, 11);
            Rectangle(mask, 7, 13, 3, 7);
            Rectangle(mask, 10, 14, 3, 8);
            Rectangle(mask, 13, 13, 3, 8);
            Rectangle(mask, 16, 11, 3, 7);
            Line(mask, 8, 11, 4, 14, 2);
            Line(mask, 4, 14, 8, 17, 2);
            return mask;
        }

        private static bool[] GrabbingMask()
        {
            var mask = new bool[Size * Size];
            Rectangle(mask, 6, 5, 12, 11);
            Rectangle(mask, 7, 13, 11, 5);
            Rectangle(mask, 8, 17, 9, 3);
            Line(mask, 8, 10, 4, 13, 3);
            return mask;
        }

        private static bool[] VerticalResizeMask()
        {
            var mask = new bool[Size * Size];
            Rectangle(mask, 11, 5, 2, 14);
            for (int row = 0; row < 6; row++)
            {
                Rectangle(mask, 11 - row, 3 + row, row * 2 + 2, 1);
                Rectangle(mask, 11 - row, 20 - row, row * 2 + 2, 1);
            }
            return mask;
        }

        private static void Rectangle(bool[] mask, int x, int y, int width, int height)
        {
            for (int offsetY = 0; offsetY < height; offsetY++)
            {
                for (int offsetX = 0; offsetX < width; offsetX++)
                    Set(mask, x + offsetX, y + offsetY);
            }
        }

        private static void Line(bool[] mask, int startX, int startY, int endX, int endY, int thickness)
        {
            int steps = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
            for (int i = 0; i <= steps; i++)
            {
                float amount = steps == 0 ? 0f : i / (float)steps;
                int x = Mathf.RoundToInt(startX + (endX - startX) * amount);
                int y = Mathf.RoundToInt(startY + (endY - startY) * amount);
                Rectangle(mask, x - thickness / 2, y - thickness / 2, thickness, thickness);
            }
        }

        private static void Set(bool[] mask, int x, int y)
        {
            if (x >= 0 && x < Size && y >= 0 && y < Size)
                mask[y * Size + x] = true;
        }
    }
}
