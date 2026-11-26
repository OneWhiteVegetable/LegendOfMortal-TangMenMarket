using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal static class MarketVisualStyle
    {
        internal static void Apply(UiPrefabInstance window)
        {
            RectTransform rail = window.Rect("CartRail");
            Image background = rail.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = UiFormatting.Ink;
            }

            MarketCartCountBadge.Apply(rail);

            RectTransform tabs = rail.Find("CartTabs") as RectTransform;
            if (tabs != null)
            {
                MarketCartTabsLayout.Apply(tabs);
                if (tabs.GetComponent<MarketCartTabsLayout>() == null)
                    tabs.gameObject.AddComponent<MarketCartTabsLayout>();
            }

            Text[] texts = rail.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text.name == "CartCountText" && text.transform.parent != null &&
                    text.transform.parent.name == "CountBack")
                {
                    MarketTextInkEffect effect = text.GetComponent<MarketTextInkEffect>();
                    if (effect == null)
                        effect = text.gameObject.AddComponent<MarketTextInkEffect>();
                    effect.Configure(MarketTextInkKind.CartCount);
                    continue;
                }
                if (text.transform.parent == null || text.transform.parent.name != "CartEmpty")
                    continue;
                if (text.name == "TitleText")
                {
                    text.fontStyle = FontStyle.Bold;
                    MarketTextInkEffect effect = text.GetComponent<MarketTextInkEffect>();
                    if (effect == null)
                        effect = text.gameObject.AddComponent<MarketTextInkEffect>();
                    effect.Configure(MarketTextInkKind.EmptyTitle);
                }
                else if (text.name == "BodyText")
                {
                    text.lineSpacing = 1.9f;
                    MarketTextInkEffect effect = text.GetComponent<MarketTextInkEffect>();
                    if (effect == null)
                        effect = text.gameObject.AddComponent<MarketTextInkEffect>();
                    effect.Configure(MarketTextInkKind.EmptyBody);
                }
            }
        }

    }

    [DefaultExecutionOrder(1000)]
    internal sealed class MarketCartTabsLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            Apply(transform as RectTransform);
        }

        internal static void Apply(RectTransform tabs)
        {
            if (tabs == null)
                return;
            float contentScale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(Screen.width, contentScale);
            float viewportHeight = MarketLayoutMetrics.EffectiveSize(Screen.height, contentScale);
            bool compact = viewportWidth <= 1450f || viewportHeight <= 790f;
            float top = compact ? 49f : 65f;
            float height = compact ? 29f : 35f;
            Vector2 offsetMax = new Vector2(tabs.offsetMax.x, -top);
            Vector2 offsetMin = new Vector2(tabs.offsetMin.x, -top - height);
            if (tabs.offsetMax != offsetMax)
                tabs.offsetMax = offsetMax;
            if (tabs.offsetMin != offsetMin)
                tabs.offsetMin = offsetMin;
        }
    }

    internal enum MarketTextInkKind
    {
        GoodsTitle,
        GoodsCategory,
        PhysicalText,
        EmptyTitle,
        EmptyBody,
        CartCount
    }

    internal sealed class MarketTextInkEffect : BaseMeshEffect
    {
        private MarketTextInkKind _kind;
        private int _sourceFontSize;
        private int _rasterFontSize;
        private float _glyphDensity = 1f;
        private int _rasterGlyphFontSize;
        private int _physicalVertexCount;
        private int _meshVertexCount;
        private string _glyphGenerationFailure;
        private UiPrefabInstance _view;
        private string _bindingKey;
        private readonly Vector3[] _worldCorners = new Vector3[4];

        internal static void Attach(UiPrefabInstance view, string key, MarketTextInkKind kind)
        {
            Text text = view.Text(key);
            MarketTextInkEffect effect = text.GetComponent<MarketTextInkEffect>();
            if (effect == null)
                effect = text.gameObject.AddComponent<MarketTextInkEffect>();
            effect._view = view;
            effect._bindingKey = key;
            effect.Configure(kind);
        }

        internal void Configure(MarketTextInkKind kind)
        {
            _kind = kind;
            _sourceFontSize = GetComponent<Text>().fontSize;
            _rasterFontSize = _sourceFontSize;
            _rasterGlyphFontSize = _sourceFontSize;
            if (UsesPhysicalGlyphs(_kind))
                ApplyGlyphDensity();
            else
                ApplyRasterFontSize();
        }

        private void LateUpdate()
        {
            if (UsesPhysicalGlyphs(_kind))
                ApplyGlyphDensity();
            else
                ApplyRasterFontSize();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount == 0 || _sourceFontSize <= 0)
                return;
            Text text = GetComponent<Text>();
            Rect rect = text.rectTransform.rect;
            Vector2 anchor = AlignmentAnchor(text.alignment, rect);
            Vector2 scale = VisualScaleForKind();
            if (!UsesPhysicalGlyphs(_kind) && _rasterFontSize > 0)
                scale /= _rasterFontSize / (float)_sourceFontSize;

            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector2 position = vertex.position;
                vertex.position = anchor + Vector2.Scale(position - anchor, scale);
                vertexHelper.SetUIVertex(vertex, i);
            }
            if (!UsesPhysicalGlyphs(_kind))
                return;
            _meshVertexCount = vertexHelper.currentVertCount;
            MarketPhysicalText physical = text as MarketPhysicalText;
            if (physical == null)
            {
                _rasterGlyphFontSize = text.fontSize;
                _physicalVertexCount = 0;
                _glyphGenerationFailure = "missing-market-physical-text";
                return;
            }
            _rasterGlyphFontSize = physical.RasterFontSize;
            _physicalVertexCount = physical.LastGenerationSucceeded ? _meshVertexCount : 0;
            _glyphGenerationFailure = physical.LastGenerationSucceeded ? string.Empty : "populate-with-errors-failed";
        }

        private static bool UsesPhysicalGlyphs(MarketTextInkKind kind)
        {
            return kind == MarketTextInkKind.GoodsTitle || kind == MarketTextInkKind.GoodsCategory ||
                kind == MarketTextInkKind.PhysicalText;
        }

        private void ApplyGlyphDensity()
        {
            Text text = GetComponent<Text>();
            float contentScale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            if (contentScale <= 1f && !(text is MarketPhysicalText))
            {
                _glyphDensity = 1f;
                return;
            }
            MarketPhysicalText physical = text as MarketPhysicalText;
            if (physical == null)
                physical = MarketPhysicalText.Replace(_view, _bindingKey, text);

            float density = 1f;
            if (contentScale > 1f)
            {
                Vector2 visualScale = VisualScaleForKind();
                density = PhysicalPixelsPerUnit(physical) * Mathf.Max(visualScale.x, visualScale.y);
                if (density <= 0.001f)
                    density = contentScale;
            }
            density = Mathf.Max(1f, density);
            _glyphDensity = density;
            physical.SetDensity(density);
        }

        private float PhysicalPixelsPerUnit(Text text)
        {
            Rect rect = text.rectTransform.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return 0f;
            text.rectTransform.GetWorldCorners(_worldCorners);
            Canvas canvas = text.canvas;
            if (canvas != null && canvas.rootCanvas != null)
                canvas = canvas.rootCanvas;
            Camera camera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[3]);
            float horizontal = Vector2.Distance(bottomLeft, bottomRight) / rect.width;
            float vertical = Vector2.Distance(bottomLeft, topLeft) / rect.height;
            return Mathf.Max(horizontal, vertical);
        }

        private void ApplyRasterFontSize()
        {
            if (_sourceFontSize <= 0)
                return;
            Text text = GetComponent<Text>();
            float contentScale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            int target = Mathf.Max(_sourceFontSize, Mathf.RoundToInt(_sourceFontSize * contentScale / text.pixelsPerUnit));
            _rasterFontSize = target;
            if (text.fontSize != target)
                text.fontSize = target;
        }

        private Vector2 VisualScaleForKind()
        {
            if (_kind == MarketTextInkKind.EmptyTitle)
                return new Vector2(1.17f, 1.12f);
            if (_kind == MarketTextInkKind.EmptyBody || _kind == MarketTextInkKind.CartCount ||
                _kind == MarketTextInkKind.GoodsCategory || _kind == MarketTextInkKind.PhysicalText)
                return Vector2.one;

            float contentScale = MarketLayoutMetrics.ContentScale(Screen.width, Screen.height);
            if (contentScale > 1f)
                return new Vector2(1.174f, 1.136f);
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(Screen.width, contentScale);
            float namePixels = Mathf.Clamp(viewportWidth * 0.0102f, 14f, 19f);
            float amount = Mathf.Clamp01((namePixels - 14f) / 5f);
            return new Vector2(
                Mathf.Lerp(1.095f, 1.229f, amount),
                Mathf.Lerp(1f, 1.188f, amount));
        }

        private static Vector2 AlignmentAnchor(TextAnchor alignment, Rect rect)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return new Vector2(rect.xMin, rect.yMax);
                case TextAnchor.UpperCenter: return new Vector2(rect.center.x, rect.yMax);
                case TextAnchor.UpperRight: return new Vector2(rect.xMax, rect.yMax);
                case TextAnchor.MiddleLeft: return new Vector2(rect.xMin, rect.center.y);
                case TextAnchor.MiddleCenter: return rect.center;
                case TextAnchor.MiddleRight: return new Vector2(rect.xMax, rect.center.y);
                case TextAnchor.LowerLeft: return new Vector2(rect.xMin, rect.yMin);
                case TextAnchor.LowerCenter: return new Vector2(rect.center.x, rect.yMin);
                case TextAnchor.LowerRight: return new Vector2(rect.xMax, rect.yMin);
                default: return rect.center;
            }
        }
    }

    internal sealed class MarketPhysicalText : Text
    {
        private readonly UIVertex[] _tempVerts = new UIVertex[4];
        private float _density = 1f;
        private bool _lastGenerationSucceeded;

        internal int RasterFontSize
        {
            get { return Mathf.Max(fontSize, Mathf.CeilToInt(fontSize * _density)); }
        }

        internal bool LastGenerationSucceeded
        {
            get { return _lastGenerationSucceeded; }
        }

        private float GenerationScale
        {
            get { return RasterFontSize / (float)Mathf.Max(1, fontSize); }
        }

        internal void SetDensity(float density)
        {
            density = Mathf.Max(1f, density);
            if (Mathf.Approximately(_density, density))
                return;
            int previousRasterSize = RasterFontSize;
            _density = density;
            if (previousRasterSize != RasterFontSize)
                SetVerticesDirty();
        }

        internal static MarketPhysicalText Replace(UiPrefabInstance view, string key, Text source)
        {
            TextProperties properties = new TextProperties(source);
            MarketPhysicalText replacement = view.ReplaceTextComponent<MarketPhysicalText>(key, source);
            properties.Apply(replacement);
            return replacement;
        }

        private sealed class TextProperties
        {
            private bool _enabled;
            private Font _font;
            private int _fontSize;
            private FontStyle _fontStyle;
            private bool _resizeTextForBestFit;
            private int _resizeTextMinSize;
            private int _resizeTextMaxSize;
            private TextAnchor _alignment;
            private bool _alignByGeometry;
            private bool _supportRichText;
            private HorizontalWrapMode _horizontalOverflow;
            private VerticalWrapMode _verticalOverflow;
            private float _lineSpacing;
            private string _text;
            private Color _color;
            private Material _material;
            private bool _raycastTarget;
            private Vector4 _raycastPadding;
            private bool _maskable;

            internal TextProperties(Text source)
            {
                _enabled = source.enabled;
                _font = source.font;
                _fontSize = source.fontSize;
                _fontStyle = source.fontStyle;
                _resizeTextForBestFit = source.resizeTextForBestFit;
                _resizeTextMinSize = source.resizeTextMinSize;
                _resizeTextMaxSize = source.resizeTextMaxSize;
                _alignment = source.alignment;
                _alignByGeometry = source.alignByGeometry;
                _supportRichText = source.supportRichText;
                _horizontalOverflow = source.horizontalOverflow;
                _verticalOverflow = source.verticalOverflow;
                _lineSpacing = source.lineSpacing;
                _text = source.text;
                _color = source.color;
                _material = source.material;
                _raycastTarget = source.raycastTarget;
                _raycastPadding = source.raycastPadding;
                _maskable = source.maskable;
            }

            internal void Apply(MarketPhysicalText target)
            {
                target.font = _font;
                target.fontSize = _fontSize;
                target.fontStyle = _fontStyle;
                target.resizeTextForBestFit = _resizeTextForBestFit;
                target.resizeTextMinSize = _resizeTextMinSize;
                target.resizeTextMaxSize = _resizeTextMaxSize;
                target.alignment = _alignment;
                target.alignByGeometry = _alignByGeometry;
                target.supportRichText = _supportRichText;
                target.horizontalOverflow = _horizontalOverflow;
                target.verticalOverflow = _verticalOverflow;
                target.lineSpacing = _lineSpacing;
                target.text = _text;
                target.color = _color;
                target.material = _material;
                target.raycastTarget = _raycastTarget;
                target.raycastPadding = _raycastPadding;
                target.maskable = _maskable;
                target.enabled = _enabled;
            }
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (font == null)
            {
                toFill.Clear();
                _lastGenerationSucceeded = false;
                return;
            }

            float scale = GenerationScale;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                // Text.FontTextureChanged invalidates this generator when Unity repacks the atlas.
                // Layout already has its own native cachedTextGeneratorForLayout.
                TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
                settings.scaleFactor = scale;
                _lastGenerationSucceeded = cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
            }
            finally
            {
                m_DisableFontTextureRebuiltCallback = false;
            }

            IList<UIVertex> verts = cachedTextGenerator.verts;
            float unitsPerPixel = 1f / scale;
            int vertCount = verts.Count;
            // Populate may repack the atlas and synchronously render a different Text into
            // Graphic's shared VertexHelper. Clear here, as in Unity's Text.OnPopulateMesh.
            toFill.Clear();
            if (!_lastGenerationSucceeded || vertCount <= 0)
                return;
            Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
            roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
            for (int i = 0; i < vertCount; i++)
            {
                int tempIndex = i & 3;
                _tempVerts[tempIndex] = verts[i];
                _tempVerts[tempIndex].position *= unitsPerPixel;
                _tempVerts[tempIndex].position.x += roundingOffset.x;
                _tempVerts[tempIndex].position.y += roundingOffset.y;
                if (tempIndex == 3)
                    toFill.AddUIVertexQuad(_tempVerts);
            }
        }

    }
}
