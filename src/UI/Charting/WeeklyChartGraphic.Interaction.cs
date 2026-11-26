using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XingShangMod.UI.Charting
{
    public sealed partial class WeeklyChartGraphic
    {
        private static WeeklyChartGraphic _cursorOwner;

        private Vector2 _lastHoverScreenPoint;
        private Camera _lastHoverScreenCamera;
        private bool _hasHoverScreenPoint;
        private WeeklyChartCursors _cursors;
        private WeeklyChartCursorKind _cursorKind;

        private enum ChartZone
        {
            None,
            Plot,
            Time,
            Price
        }

        private void Update()
        {
            if (!_hasGraphicColor || _lastGraphicColor != color)
                UpdateVisuals();
            if (!_interactionEnabled || !_pointerInside || _drag != null || _timeline.Count == 0)
                return;
            Vector2 screenPoint = Input.mousePosition;
            if (_hasHoverScreenPoint && _lastHoverScreenPoint == screenPoint &&
                ReferenceEquals(_lastHoverScreenCamera, _pointerCamera))
                return;
            _hasHoverScreenPoint = true;
            _lastHoverScreenPoint = screenPoint;
            _lastHoverScreenCamera = _pointerCamera;
            UpdatePointerCursor(screenPoint, _pointerCamera);
            UpdateHover(screenPoint, _pointerCamera);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactionEnabled)
                return;
            _pointerInside = true;
            _pointerCamera = eventData != null ? eventData.pressEventCamera : null;
            if (eventData != null)
            {
                UpdatePointerCursor(eventData.position, _pointerCamera);
                UpdateHover(eventData.position, _pointerCamera);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _hasPointerLocal = false;
            _hasHoverScreenPoint = false;
            if (_drag == null)
            {
                ReleasePointerCursor();
                _pointerCamera = null;
                SetHoverIndex(-1);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData == null ||
                eventData.button != PointerEventData.InputButton.Left ||
                _timeline.Count == 0 || _drag != null)
                return;
            Vector2 local;
            if (!TryGetLocalPoint(eventData.position, eventData.pressEventCamera, out local))
                return;
            _pointerInside = true;
            _pointerCamera = eventData.pressEventCamera;
            _lastPointerLocal = local;
            _hasPointerLocal = true;
            Rect plot = GetPlotRect();
            ChartZone zone = InteractionZone(local, plot);
            if (zone == ChartZone.None || (zone != ChartZone.Price && _timeline.Count < 2))
                return;
            _drag = new DragState
            {
                Kind = zone == ChartZone.Price ? DragKind.Price : DragKind.Time,
                PointerId = eventData.pointerId,
                StartLocal = local,
                StartView = _viewStart,
                StartYFactor = _yZoomFactor,
                StartYCenter = _yZoomCenterRatio,
                AnchorRatio = plot.height <= 0f
                    ? 0.5f
                    : Mathf.Clamp01((local.y - plot.yMin) / plot.height),
                Count = _viewCount,
                Moved = false
            };
            UpdatePointerCursor(eventData.position, _pointerCamera);
            SetHoverIndex(-1);
            HideHoverTag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData == null || _drag == null ||
                eventData.pointerId != _drag.PointerId)
                return;
            Vector2 local;
            if (!TryGetLocalPoint(eventData.position, eventData.pressEventCamera, out local))
                return;
            Vector2 delta = local - _drag.StartLocal;
            Rect plot = GetPlotRect();
            if (_drag.Kind == DragKind.Price)
            {
                if (!_drag.Moved && Math.Abs(delta.y) < 3f)
                    return;
                _drag.Moved = true;
                eventData.Use();
                float factor = _drag.StartYFactor * (float)Math.Exp(delta.y * 0.008f);
                float nextFactor;
                float nextCenter;
                ChangedYZoom(
                    _drag.StartYFactor,
                    _drag.StartYCenter,
                    factor,
                    _drag.AnchorRatio,
                    out nextFactor,
                    out nextCenter);
                if (Math.Abs(nextFactor - _yZoomFactor) > 0.000001f ||
                    Math.Abs(nextCenter - _yZoomCenterRatio) > 0.000001f)
                {
                    _yZoomFactor = nextFactor;
                    _yZoomCenterRatio = nextCenter;
                    UpdateVisuals();
                    NotifyViewChanged();
                }
                return;
            }

            if (!_drag.Moved && Math.Abs(delta.x) < 4f)
                return;
            _drag.Moved = true;
            eventData.Use();
            if (plot.width <= 0f)
                return;
            float clamped = WeeklyChartData.Pan(
                _drag.StartView,
                _drag.Count,
                _timeline.Count,
                delta.x,
                plot.width);
            if (Math.Abs(clamped - _viewStart) > 0.000001f)
            {
                _viewStart = clamped;
                UpdateVisuals();
                NotifyViewChanged();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            FinishDrag(eventData, false);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_drag != null)
                FinishDrag(eventData, false);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_interactionEnabled || eventData == null || _timeline.Count == 0 || _drag != null)
                return;
            Vector2 local;
            if (!TryGetLocalPoint(eventData.position, eventData.pressEventCamera, out local))
                return;
            Rect plot = GetPlotRect();
            ChartZone zone = InteractionZone(local, plot);
            if (zone == ChartZone.None || (zone != ChartZone.Price && _timeline.Count < 2))
                return;
            UpdatePointerCursor(eventData.position, eventData.pressEventCamera);
            float scroll = eventData.scrollDelta.y;
            if (Math.Abs(scroll) < 0.0001f)
                return;
            eventData.Use();
            float factor = WeeklyChartData.ScrollRangeFactor(scroll);
            if (zone == ChartZone.Price)
            {
                float anchor = plot.height <= 0f
                    ? 0.5f
                    : Mathf.Clamp01((local.y - plot.yMin) / plot.height);
                ChangePriceZoom(_yZoomFactor / factor, anchor);
                SetHoverIndex(-1);
                return;
            }
            float ratio = plot.width <= 0f ? 0f : Mathf.Clamp01((local.x - plot.xMin) / plot.width);
            int nextCount = Mathf.Clamp(Mathf.RoundToInt(_viewCount * factor), 1, _timeline.Count);
            if (nextCount != _viewCount)
            {
                _hasCustomRange = true;
                ChangeRange(nextCount, ratio);
            }
            _lastPointerLocal = local;
            _hasPointerLocal = true;
            UpdateHover(eventData.position, eventData.pressEventCamera);
        }

        private void FinishDrag(PointerEventData eventData, bool cancelled)
        {
            if (_drag == null || eventData == null || eventData.pointerId != _drag.PointerId)
                return;
            DragState current = _drag;
            _drag = null;
            if (current.Moved)
                eventData.Use();
            if (cancelled)
            {
                ReleasePointerCursor();
                SetHoverIndex(-1);
                return;
            }
            _pointerCamera = eventData.pressEventCamera;
            UpdatePointerCursor(eventData.position, _pointerCamera);
            UpdateHover(eventData.position, _pointerCamera);
        }

        private void UpdateHover(Vector2 screenPoint, Camera eventCamera)
        {
            if (!_interactionEnabled || _drag != null || _timeline.Count == 0)
                return;
            Vector2 local;
            if (!TryGetLocalPoint(screenPoint, eventCamera, out local))
                return;
            Rect plot = GetPlotRect();
            if (InteractionZone(local, plot) != ChartZone.Plot)
            {
                SetHoverIndex(-1);
                return;
            }
            float ratio = plot.width <= 0f ? 0f : Mathf.Clamp01((local.x - plot.xMin) / plot.width);
            float approximate = _viewStart + ratio * Math.Max(0, _viewCount - 1);
            int first;
            int last;
            VisibleBounds(false, out first, out last);
            int index = Mathf.Clamp(Mathf.RoundToInt(approximate), first, last);
            _lastPointerLocal = local;
            _hasPointerLocal = true;
            if (_selectedIndex == index)
                RefreshHoverLabel();
            else
                SetHoverIndex(index);
        }

        private bool TryGetLocalPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out local);
        }

        private ChartZone InteractionZone(Vector2 position, Rect plot)
        {
            Rect bounds = rectTransform.rect;
            bool inPrice = position.x >= bounds.xMin &&
                           (_mode == WeeklyChartMode.Assets
                               ? position.x < plot.xMin || (position.x > plot.xMax && position.x <= bounds.xMax)
                               : position.x <= plot.xMin + 7f) &&
                           position.y >= plot.yMin - 7f &&
                           position.y <= Math.Min(bounds.yMax, plot.yMax + 8f);
            if (inPrice)
                return ChartZone.Price;
            bool inTime = position.x >= plot.xMin - 6f &&
                          position.x <= plot.xMax + 6f &&
                          position.y >= bounds.yMin &&
                          position.y <= plot.yMin + 6f;
            if (inTime)
                return ChartZone.Time;
            bool inPlot = position.x >= plot.xMin && position.x <= plot.xMax &&
                          position.y >= plot.yMin && position.y <= plot.yMax;
            return inPlot ? ChartZone.Plot : ChartZone.None;
        }

        private static void ChangedYZoom(
            float startFactor,
            float startCenter,
            float nextFactor,
            float anchorRatio,
            out float factor,
            out float center)
        {
            WeeklyChartData.ChangePriceZoom(
                startFactor,
                startCenter,
                nextFactor,
                anchorRatio,
                out factor,
                out center);
        }

        private void ChangePriceZoom(float nextFactor, float anchorRatio)
        {
            float factor;
            float center;
            ChangedYZoom(
                _yZoomFactor,
                _yZoomCenterRatio,
                nextFactor,
                anchorRatio,
                out factor,
                out center);
            if (Math.Abs(factor - _yZoomFactor) < 0.000001f &&
                Math.Abs(center - _yZoomCenterRatio) < 0.000001f)
                return;
            _yZoomFactor = factor;
            _yZoomCenterRatio = center;
            UpdateVisuals();
            NotifyViewChanged();
        }

        private void CancelInteraction()
        {
            _pointerInside = false;
            _pointerCamera = null;
            _hasPointerLocal = false;
            _hasHoverScreenPoint = false;
            _lastHoverScreenCamera = null;
            _drag = null;
            _selectedIndex = -1;
            if (!_isDestroying && _overlayGraphic != null)
                _overlayGraphic.SetVerticesDirty();
            ReleasePointerCursor();
            HideHoverTag();
        }

        internal void ReleaseInteractionResources()
        {
            CancelLayoutRefresh();
            CancelInteraction();
            DisposeCursorResources();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelInteraction();
        }

        private void UpdatePointerCursor(Vector2 screenPoint, Camera eventCamera)
        {
            WeeklyChartCursorKind next = WeeklyChartCursorKind.Default;
            if (_interactionEnabled && _timeline.Count > 0)
            {
                if (_drag != null)
                {
                    next = _drag.Kind == DragKind.Price
                        ? WeeklyChartCursorKind.VerticalResize
                        : WeeklyChartCursorKind.Grabbing;
                }
                else
                {
                    Vector2 local;
                    if (TryGetLocalPoint(screenPoint, eventCamera, out local))
                    {
                        ChartZone zone = InteractionZone(local, GetPlotRect());
                        if (zone == ChartZone.Price)
                            next = WeeklyChartCursorKind.VerticalResize;
                        else if (zone != ChartZone.None)
                            next = WeeklyChartCursorKind.Grab;
                    }
                }
            }
            SetPointerCursor(next);
        }

        private void SetPointerCursor(WeeklyChartCursorKind kind)
        {
            if (kind == WeeklyChartCursorKind.Default)
            {
                ReleasePointerCursor();
                return;
            }
            if (_cursors == null)
                _cursors = new WeeklyChartCursors();
            if (ReferenceEquals(_cursorOwner, this) && _cursorKind == kind)
                return;
            if (_cursorOwner != null && !ReferenceEquals(_cursorOwner, this))
                _cursorOwner._cursorKind = WeeklyChartCursorKind.Default;
            _cursorOwner = this;
            _cursorKind = kind;
            _cursors.Apply(kind);
        }

        private void ReleasePointerCursor()
        {
            _cursorKind = WeeklyChartCursorKind.Default;
            if (!ReferenceEquals(_cursorOwner, this))
                return;
            _cursorOwner = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void DisposeCursorResources()
        {
            ReleasePointerCursor();
            if (_cursors != null)
            {
                _cursors.Dispose();
                _cursors = null;
            }
        }
    }
}
