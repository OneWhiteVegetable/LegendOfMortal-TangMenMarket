using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static class RuntimePreviewGlyphAtlas
    {
        [Serializable]
        private sealed class Sample
        {
            public string itemId, catalogName, binding, value, font;
            public RectInt pixels;
            public int differingPixels, maxChannelDifference, pixelsAboveOne, inkPixels;
        }

        [Serializable]
        private sealed class Evidence
        {
            public int atlasRebuilds;
            public int wholeFrameDifferingPixels, wholeFramePixelsAboveOne, wholeFrameMaxChannelDifference;
            public List<Sample> samples = new List<Sample>();
        }

        internal static void Check(RuntimePreviewAdapter adapter, Camera camera, RuntimePreviewCaseReport result,
            int firstNumber, Action<string> capture, HashSet<string> visited = null)
        {
            string dir = Path.GetDirectoryName(result.png);
            string stem = Path.GetFileNameWithoutExtension(result.png);
            int page = (firstNumber - 1) / 6;
            if (visited != null) stem += "-page-" + (page + 1).ToString("00");
            var labels = new List<Text>();
            var fonts = new HashSet<Font>();
            var evidence = new Evidence();
            for (int i = 0; i < 6; ++i)
            {
                RuntimePreviewItemBinding item = RuntimePreviewMarketInteractionProbe.Item(adapter, i);
                if (visited != null && !visited.Add(item.ItemId))
                    result.failures.Add("Repeated catalog item on page " + (page + 1) + ": " + item.ItemId);
                foreach (string key in new[] { "NameText", "CategoryText" })
                {
                    Text label = RuntimePreviewMarketInteractionProbe.CardText(adapter, i, key);
                    if (label == null || label.font == null || string.IsNullOrEmpty(label.text))
                        throw new InvalidDataException("Missing label on " + item.ItemId + "/" + key);
                    string expected = key == "NameText" ? item.Name : "行货 · " + (i + firstNumber).ToString("00");
                    if (label.text != expected)
                        result.failures.Add(item.ItemId + "/" + key + " bound " + label.text + " expected " + expected);
                    labels.Add(label);
                    fonts.Add(label.font);
                    evidence.samples.Add(new Sample { itemId = item.ItemId, catalogName = item.Name, binding = key,
                        value = label.text, font = label.font.name, pixels = ScreenArea(label.rectTransform, camera) });
                }
            }
            capture(Path.Combine(dir, stem + "-opening.png"));
            char[] chars = new char[512];
            Action<Font> count = font => { if (fonts.Contains(font)) ++evidence.atlasRebuilds; };
            Font.textureRebuilt += count;
            try
            {
                // A reused, already large atlas may accept several batches before its UVs move.
                for (int attempt = 0; attempt < 24 && evidence.atlasRebuilds == 0; ++attempt)
                {
                    for (int i = 0; i < chars.Length; ++i)
                        chars[i] = (char)(0x4e00 + (i + page * 1763 + attempt * chars.Length) % 18000);
                    string stress = new string(chars);
                    foreach (Font font in fonts)
                        font.RequestCharactersInTexture(stress, 49 + page * 2 + attempt, FontStyle.Normal);
                }
                Canvas.ForceUpdateCanvases();
            }
            finally { Font.textureRebuilt -= count; }
            if (evidence.atlasRebuilds == 0)
                result.failures.Add("No card font rebuilt its atlas during the stress test.");
            string stressed = Path.Combine(dir, stem + "-atlas-rebuilt.png");
            capture(stressed);
            // Repopulate the exact same strings at the same size against the *current* atlas.
            // Rebinding through a second string avoids depending on private generator internals.
            foreach (Text t in labels) t.text = t.text.Substring(0, t.text.Length - 1);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < labels.Count; ++i) labels[i].text = evidence.samples[i].value;
            Canvas.ForceUpdateCanvases();
            string fresh = Path.Combine(dir, stem + "-fresh-reference.png");
            capture(fresh);
            Color32[] before = LoadPixels(stressed);
            Color32[] reference = LoadPixels(fresh);
            // A nested Text rebuild can append quads outside the card's own rectangle.
            // Both captures are in the same frame and neither advances the market or its ticker.
            for (int i = 0; i < before.Length; i++)
            {
                int delta = ChannelDifference(before[i], reference[i]);
                if (delta == 0) continue;
                ++evidence.wholeFrameDifferingPixels;
                if (delta > 1) ++evidence.wholeFramePixelsAboveOne;
                evidence.wholeFrameMaxChannelDifference = Math.Max(evidence.wholeFrameMaxChannelDifference, delta);
            }
            if (evidence.wholeFramePixelsAboveOne > 0)
                result.failures.Add("Page " + (page + 1) + " has " + evidence.wholeFramePixelsAboveOne +
                    " viewport pixels outside 1/255 of the fresh redraw.");
            foreach (Sample sample in evidence.samples)
            {
                RectInt r = sample.pixels;
                for (int y = r.yMin; y < r.yMax; ++y)
                    for (int x = r.xMin; x < r.xMax; ++x)
                    {
                        int p = y * result.width + x;
                        Color32 actual = before[p], expected = reference[p];
                        if (!actual.Equals(expected))
                        {
                            ++sample.differingPixels;
                            int diff = ChannelDifference(actual, expected);
                            sample.maxChannelDifference = Math.Max(sample.maxChannelDifference, diff);
                            if (diff > 1) ++sample.pixelsAboveOne;
                        }
                        if (x < r.xMin + 3 || x >= r.xMax - 3 || y < r.yMin + 3 || y >= r.yMax - 3) continue;
                        Color32 pixel = reference[p];
                        if (sample.binding == "NameText" ? pixel.r < 90 && pixel.g < 90 && pixel.b < 90 :
                            pixel.r > 155 && pixel.g > 155 && pixel.b > 155)
                            ++sample.inkPixels;
                    }
                if (sample.inkPixels < sample.value.Length * 2)
                    result.failures.Add(sample.itemId + "/" + sample.binding + " has insufficient visible ink: " + sample.inkPixels);
                // Keep the exact counts. A one-level 8-bit rounding difference can occur when the
                // same filtered atlas glyph occupies a different texel address after a repack.
                if (sample.pixelsAboveOne > 0)
                    result.failures.Add(sample.itemId + "/" + sample.binding + " rendered " +
                        sample.pixelsAboveOne + " pixels outside 1/255 of a fresh mesh of the same label.");
            }
            VisualValidation.WriteJson(Path.Combine(dir, stem + "-pixels.json"), evidence);
        }

        private static int ChannelDifference(Color32 a, Color32 b)
        {
            return Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g),
                Math.Max(Math.Abs(a.b - b.b), Math.Abs(a.a - b.a))));
        }

        private static RectInt ScreenArea(RectTransform rect, Camera cam)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(Screen.width, Screen.height), max = Vector2.zero;
            foreach (Vector3 corner in corners)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            int x = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, Screen.width);
            int y = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, Screen.height);
            return new RectInt(x, y, Mathf.Clamp(Mathf.CeilToInt(max.x), x, Screen.width) - x,
                Mathf.Clamp(Mathf.CeilToInt(max.y), y, Screen.height) - y);
        }

        private static Color32[] LoadPixels(string path)
        {
            var image = new Texture2D(2, 2);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(path)))
                    throw new InvalidDataException(path);
                return image.GetPixels32();
            }
            finally { UnityEngine.Object.Destroy(image); }
        }
    }
}
