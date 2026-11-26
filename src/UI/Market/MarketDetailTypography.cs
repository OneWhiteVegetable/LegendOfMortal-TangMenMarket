using System;
using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketDetailTypography : IDisposable
    {
        private static readonly string[] SerifNames =
        {
            "Noto Serif SC",
            "Noto Serif CJK SC",
            "Source Han Serif SC",
            "Source Han Serif CN",
            "SimSun",
            "宋体"
        };

        private Font _serif;

        internal void Apply(params Text[] texts)
        {
            bool needsFallback = false;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && !IsSerif(texts[i].font))
                    needsFallback = true;
            }
            if (!needsFallback)
                return;
            if (_serif == null)
                _serif = CreateSerifFont();
            if (_serif == null)
                return;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && !IsSerif(texts[i].font))
                    texts[i].font = _serif;
            }
        }

        internal void ApplyDisplay(Font displayFont, params Text[] texts)
        {
            if (displayFont == null)
                return;
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                    continue;
                text.font = displayFont;
                text.fontStyle = FontStyle.Italic;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private static bool IsSerif(Font font)
        {
            return font != null && !string.IsNullOrEmpty(font.name) &&
                (font.name.IndexOf("serif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 font.name.IndexOf("宋体", StringComparison.Ordinal) >= 0);
        }

        public void Dispose()
        {
            if (_serif != null)
            {
                UnityEngine.Object.Destroy(_serif);
                _serif = null;
            }
        }

        private static Font CreateSerifFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < SerifNames.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (string.Equals(SerifNames[i], installed[j], StringComparison.OrdinalIgnoreCase))
                    {
                        Font font = Font.CreateDynamicFontFromOSFont(installed[j], 12);
                        if (font != null)
                            font.hideFlags = HideFlags.HideAndDontSave;
                        return font;
                    }
                }
            }
            return null;
        }
    }
}
