using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventoryActionEdge : BaseMeshEffect
    {
        internal bool Left;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount != 4) return;
            Rect r = graphic.rectTransform.rect;
            for (int i = 0; i < 4; i++)
            {
                UIVertex v = UIVertex.simpleVert;
                mesh.PopulateUIVertex(ref v, i);
                bool inner = Left ? v.position.x > r.center.x : v.position.x < r.center.x;
                if (!inner) continue;
                if (v.position.y > r.center.y)
                {
                    v.position.y -= r.height * 0.05f;
                    if (!Left) v.position.x += r.width * 0.06f;
                }
                else if (Left) v.position.x -= r.width * 0.06f;
                mesh.SetUIVertex(v, i);
            }
        }
    }

    internal sealed class InventorySection : IDisposable
    {
        private readonly UiFrontendServices _services;
        private readonly GridLayoutGroup _grid;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _viewport;
        private readonly Text _emptyState;
        private readonly LayoutElement _sectionLayout;
        private readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        private bool _compact;
        private int _cellCount;

        internal InventorySection(UiFrontendServices services, UiPrefabInstance window, bool isLong)
        {
            _services = services;
            string prefix = isLong ? "Long" : "Short";
            _grid = window.Get<GridLayoutGroup>(prefix + "GridContent");
            _scroll = window.Scroll(prefix + "GridScroll");
            // Keep the overhanging top lip inside the scroll viewport on the first row.
            _grid.padding = new RectOffset(4, 0, 9, 0);
            _viewport = _scroll.viewport == null
                ? _scroll.GetComponent<RectTransform>()
                : _scroll.viewport;
            _emptyState = window.Text(prefix + "EmptyStateText");
            _sectionLayout = window.Get<LayoutElement>(prefix + "Section");
            RectTransform section = _sectionLayout.GetComponent<RectTransform>();
            Color accent = isLong ? UiFormatting.Red : UiFormatting.Green;
            InventoryPaper.Face(section.GetComponent<Image>(), InventoryPaper.Section);
            InventoryPaper.Frame(section, InventoryPaper.Border, 5, accent);
            var header = (RectTransform)section.Find("Header");
            var sectionName = header.Find("Title").GetComponent<Text>();
            sectionName.fontSize = 15;
            InventoryPaper.Box(sectionName.rectTransform, 0, 0, isLong ? 98 : 116, 29);
            var sectionSummary = window.Text(prefix+"SummaryText");
            sectionSummary.fontSize = 10;
            InventoryPaper.Box(sectionSummary.rectTransform,Mathf.Ceil(sectionName.preferredWidth)+11,0,360,29);
            var profit = window.Text(prefix+"ProfitText");
            profit.fontSize = 14;
            InventoryPaper.Right(profit.rectTransform,0,0,107,29);
            InventoryCloseAllStyle.Attach(window.Button("CloseAll"+prefix+"Button"),services,isLong);
            _emptyState.text = isLong ? "暂无持货 · 空格由两区共享" : "暂无借货 · 空格由两区共享";
            Transform content = _grid.transform;
            for (int i = 0; i < content.childCount; i++)
                _slots.Add(CreateSlot(services.Assets.Adopt("InventorySlot", content.GetChild(i).gameObject)));
        }

        internal event Action<InventorySlotModel> ViewRequested;
        internal event Action<InventorySlotModel> CloseRequested;

        internal void Refresh(IList<InventoryCellModel> cells, bool interactive)
        {
            _cellCount = cells == null ? 0 : cells.Count;
            EnsureSlots(_cellCount);
            for (int i = 0; i < _slots.Count; i++)
            {
                bool active = i < _cellCount;
                _slots[i].GameObject.SetActive(active);
                if (active)
                    _slots[i].Bind(cells[i], interactive, _compact);
            }
            _emptyState.gameObject.SetActive(!InventorySectionLayout.HasOccupiedCells(cells));
            UpdateScrolling();
        }

        internal void SetLayout(int columns, bool compact, float availableHeight)
        {
            _compact = compact;
            columns = Math.Max(1, columns);
            if (_grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
                _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            if (_grid.constraintCount != columns)
                _grid.constraintCount = columns;

            float viewportWidth = _viewport.rect.width;
            float cellWidth = viewportWidth > 1f
                ? Mathf.Max(1f,Mathf.Floor((viewportWidth - _grid.padding.horizontal - _grid.spacing.x * (columns - 1)) / columns))
                : _grid.cellSize.x;
            Vector2 cellSize = new Vector2(cellWidth, compact ? 132f : 142f);
            if (_grid.cellSize != cellSize)
                _grid.cellSize = cellSize;
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetLayoutSize(cellSize, compact);
            UpdateSectionHeight(availableHeight);
            UpdateScrolling();
        }

        private void UpdateSectionHeight(float availableHeight)
        {
            if (_sectionLayout == null || availableHeight <= 1f)
                return;
            float height = InventorySectionLayout.SectionHeight(availableHeight, _compact);
            bool changed = Mathf.Abs(_sectionLayout.preferredHeight - height) >= 0.01f ||
                _sectionLayout.flexibleHeight < 1f;
            if (!changed)
                return;
            _sectionLayout.preferredHeight = height;
            _sectionLayout.flexibleHeight = 1f;
            RectTransform parent = _sectionLayout.transform.parent as RectTransform;
            LayoutRebuilder.MarkLayoutForRebuild(parent == null ? _sectionLayout.GetComponent<RectTransform>() : parent);
        }

        private void UpdateScrolling()
        {
            _scroll.vertical = InventorySectionLayout.NeedsScroll(
                _cellCount,
                _grid.constraintCount,
                _viewport.rect.height - _grid.padding.vertical,
                _grid.cellSize.y);
        }

        private void EnsureSlots(int count)
        {
            while (_slots.Count < count)
            {
                InventorySlotView slot = CreateSlot(_services.Assets.Create("InventorySlot", _grid.transform));
                slot.SetLayoutSize(_grid.cellSize, _compact);
                _slots.Add(slot);
            }
        }

        private InventorySlotView CreateSlot(UiPrefabInstance view)
        {
            InventorySlotView slot = new InventorySlotView(_services, view);
            slot.ViewRequested += delegate (InventorySlotModel model)
            {
                if (ViewRequested != null)
                    ViewRequested(model);
            };
            slot.CloseRequested += delegate (InventorySlotModel model)
            {
                if (CloseRequested != null)
                    CloseRequested(model);
            };
            return slot;
        }

        public void Dispose()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].Dispose();
            _slots.Clear();
            ViewRequested = null;
            CloseRequested = null;
        }

        private sealed class InventorySlotView : IDisposable
        {
            private readonly UiFrontendServices _services;
            private readonly UiPrefabInstance _view;
            private readonly InventorySlotMotion _motion;
            private readonly Image _edge, _shadow, _emptyBorder;
            private readonly InventoryLoanSlip _slip;
            private readonly Text _marketCost, _basisLabel, _marketLabel, _profitRate;
            private readonly GameObject[] _rules;
            private InventoryCellModel _cell;
            private InventorySlotModel _model;
            private bool _interactive;
            private bool _compact;

            internal InventorySlotView(UiFrontendServices services, UiPrefabInstance view)
            {
                _services = services;
                _view = view;
                var pivot = view.Rect("MotionPivot");
                var face = view.Image("Background");
                var clip = (RectTransform)face.transform.parent;
                RankingLayout.Stretch(clip,1,1,4,6);
                _shadow = InventoryPaper.Solid("ContractShadow",pivot,new Color32(32,35,33,125));
                _shadow.rectTransform.SetAsFirstSibling();
                RankingLayout.Stretch(_shadow.rectTransform,3,4,2,3);
                InventoryPaper.Frame(clip,InventoryPaper.InkBorder,4,UiFormatting.Red);
                _edge = clip.Find("LeftRule").GetComponent<Image>();
                _rules = new[] { _edge.gameObject, clip.Find("RightRule").gameObject,
                    clip.Find("TopRule").gameObject,clip.Find("BottomRule").gameObject };
                foreach (GameObject rule in _rules) UiPaperShape.Attach(rule.GetComponent<Image>());
                UiPaperShape.Attach(_shadow);
                RankingLayout.Stretch(view.Rect("FocusFrame"),6,4,8,9);
                _emptyBorder = InventoryPaper.Solid("EmptyBorder",clip,Color.clear);
                _emptyBorder.transform.SetAsFirstSibling();
                RankingLayout.Stretch(_emptyBorder.rectTransform);
                InventoryPaper.Frame(_emptyBorder.transform,InventoryPaper.Border,1,InventoryPaper.Border);
                var empty = view.Text("EmptyMark");
                empty.font = services.Assets.SerifFont ?? services.Assets.RegularFont;
                RankingLayout.Stretch(empty.rectTransform);
                var filled = view.Rect("FilledContent");
                RankingLayout.Stretch(filled,8,0,8,0);
                _profitRate = RankingLayout.Text("ProfitRate",filled,"",10,UiFormatting.Muted,services.Assets.BoldFont);
                _basisLabel = RankingLayout.Text("BasisCaption",filled,"购入",9,UiFormatting.Muted,services.Assets.RegularFont);
                _marketLabel = RankingLayout.Text("MarketCaption",filled,"现价",9,UiFormatting.Muted,services.Assets.RegularFont);
                _marketCost = RankingLayout.Text("MarketValue",filled,"",12,UiFormatting.Muted,services.Assets.BoldFont,TextAnchor.MiddleRight);
                // The ink crosses the card edge, while the grid viewport still clips scrolling rows.
                RectTransform slip = RankingLayout.Rect("LoanMaturity", pivot);
                InventoryPaper.Box(slip, -2f, -7f, 58f, 50f);
                slip.gameObject.AddComponent<CanvasRenderer>();
                _slip = slip.gameObject.AddComponent<InventoryLoanSlip>();
                _slip.Initialize(services.Assets.GetSprite("loan_maturity_atlas"));
                view.Text("MetaText").gameObject.SetActive(false);
                view.Rect("Actions").SetAsLastSibling();
                foreach (GameObject rule in _rules) rule.transform.SetAsLastSibling();
                _view.Button("OpenButton").onClick.AddListener(Open);
                _view.Button("ViewButton").onClick.AddListener(Open);
                _view.Button("ClosePositionButton").onClick.AddListener(Close);
                Button viewButton = _view.Button("ViewButton");
                ConfigureAction(viewButton, viewButton.transform.Find("ViewIcon").GetComponent<Image>(), true);
                ConfigureAction(_view.Button("ClosePositionButton"), _view.Image("CloseIcon"), false);
                _view.Rect("Actions").Find("ActionDivider").gameObject.SetActive(false);
                _motion = _view.GameObject.GetComponent<InventorySlotMotion>();
                if (_motion == null)
                    _motion = _view.GameObject.AddComponent<InventorySlotMotion>();
                RectTransform actions = _view.Rect("Actions");
                _motion.Configure(
                    services.Motion,
                    _view.Rect("MotionPivot"),
                    actions,
                    actions.GetComponent<CanvasGroup>(),
                    _view.Rect("FocusFrame").gameObject);
                services.Overlays.AttachTooltip(
                    _view.GameObject,
                    TooltipTitle,
                    TooltipBody,
                    TooltipHint);
            }

            internal event Action<InventorySlotModel> ViewRequested;
            internal event Action<InventorySlotModel> CloseRequested;
            internal GameObject GameObject { get { return _view.GameObject; } }

            private static void ConfigureAction(Button button, Image icon, bool left)
            {
                Image face = button.image;
                face.sprite = null;
                face.type = Image.Type.Simple;
                face.color = Color.white;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = UiFormatting.Ink;
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                button.gameObject.AddComponent<InventoryActionEdge>().Left = left;
                button.gameObject.AddComponent<MarketPaperEdge>();
                HorizontalLayoutGroup layout = button.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 6f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                LayoutElement size = icon.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = size.minHeight = size.preferredHeight = 17f;
                icon.transform.SetAsFirstSibling();
            }

            internal void SetLayoutSize(Vector2 size, bool compact)
            {
                bool compactChanged = _compact != compact;
                bool sizeChanged = _view.Root.rect.size != size;
                LayoutElement layout = _view.GameObject.GetComponent<LayoutElement>();
                if (layout == null)
                    layout = _view.GameObject.AddComponent<LayoutElement>();
                layout.minWidth = size.x;
                layout.minHeight = size.y;
                layout.preferredWidth = size.x;
                layout.preferredHeight = size.y;
                layout.flexibleWidth = 0f;
                layout.flexibleHeight = 0f;
                if (_view.Root.rect.size != size)
                {
                    _view.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                    _view.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                }
                if (compactChanged || sizeChanged)
                {
                    SetCompact(compact);
                    RefreshTextLayout();
                }
            }

            internal void SetCompact(bool compact)
            {
                _compact = compact;
                RectTransform filled = _view.Rect("FilledContent");
                float width = Mathf.Max(80,filled.rect.width);
                float start = Mathf.Round(width * 0.425f);
                float info = Mathf.Round(width * 0.69f);
                float contentWidth = Mathf.Max(1,width-info);
                float top = compact ? 10 : 15;
                RectTransform art = _view.Image("ItemImage").rectTransform;
                InventoryPaper.Box(art,1,compact ? 21 : 28,start-4,compact ? 79 : 96);
                _view.Text("NameText").fontSize = compact ? 11 : 12;
                _view.Text("QuantityText").fontSize = compact ? 27 : 30;
                Text profit = _view.Text("ProfitText");
                profit.horizontalOverflow = HorizontalWrapMode.Overflow;
                profit.alignment = TextAnchor.UpperLeft;
                InventoryPaper.Box(profit.rectTransform,start,top+3,Mathf.Max(1,info-start-2),23);
                InventoryPaper.Box(_profitRate.rectTransform,start,top+26,Mathf.Max(1,info-start-2),16);
                InventoryPaper.Box(_view.Text("NameText").rectTransform,info,top,contentWidth,18);
                InventoryPaper.Box(_view.Text("QuantityText").rectTransform,info,top+19,contentWidth,36);
                float costTop = top + (compact ? 57 : 59);
                float costX = Mathf.Round(width * 0.64f);
                float costWidth = width-costX;
                InventoryPaper.Box(_basisLabel.rectTransform,costX,costTop,24,20);
                InventoryPaper.Box(_marketLabel.rectTransform,costX,costTop+21,24,20);
                InventoryPaper.Box(_view.Text("CostText").rectTransform,costX+19,costTop,costWidth-19,20);
                InventoryPaper.Box(_marketCost.rectTransform,costX+19,costTop+21,costWidth-19,20);
            }

            private void RefreshTextLayout()
            {
                if (_model == null)
                    return;
                BindName();
                FitTextWidth(_view.Text("ProfitText"), _compact ? 14 : 16, _compact ? 10 : 11);
                FitTextWidth(_view.Text("CostText"), _compact ? 11 : 12, 9);
                FitTextWidth(_marketCost, _compact ? 11 : 12, 9);
                FitTextWidth(
                    _view.Text("QuantityText"),
                    _cell != null && _cell.IsMixed ? (_compact ? 18 : 20) : (_compact ? 27 : 30),
                    _compact ? 14 : 16);
            }

            private void BindName()
            {
                Text text = _view.Text("NameText");
                string fullName = (_model.Delisted ? "退市 · " : string.Empty) + _model.Item.Name;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.text = fullName;
                float width = text.rectTransform.rect.width;
                if (width <= 1f || text.preferredWidth <= width)
                    return;
                int low = 0;
                int high = fullName.Length;
                while (low < high)
                {
                    int length = (low + high + 1) / 2;
                    text.text = fullName.Substring(0, length) + "…";
                    if (text.preferredWidth <= width)
                        low = length;
                    else
                        high = length - 1;
                }
                text.text = fullName.Substring(0, low) + "…";
            }

            private static void FitTextWidth(Text text, int maximum, int minimum)
            {
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.fontSize = maximum;
                float width = text.rectTransform.rect.width;
                if (width <= 1f)
                    return;
                while (text.fontSize > minimum && text.preferredWidth > width)
                    text.fontSize--;
            }

            internal void Bind(InventoryCellModel cell, bool interactive, bool compact)
            {
                SetCompact(compact);
                _cell = cell;
                _model = cell == null ? null : cell.Position;
                _interactive = interactive;
                bool filled = _model != null;
                _shadow.gameObject.SetActive(filled);
                bool hasDeadline = filled && cell.CellShortQuantity > 0 && cell.ShortSliceRemainingWeeks >= 0;
                _slip.gameObject.SetActive(hasDeadline);
                if (hasDeadline) _slip.SetRemaining(cell.ShortSliceRemainingWeeks);
                _emptyBorder.gameObject.SetActive(!filled);
                foreach (GameObject rule in _rules) rule.SetActive(filled);
                if (!filled)
                {
                    InventoryPaper.Face(_view.Image("Background"),new Color32(234,227,210,125));
                    RankingLayout.Stretch(_view.Image("Background").rectTransform,1,1,1,1);
                    _view.Rect("FilledContent").gameObject.SetActive(false);
                    _view.Rect("Actions").gameObject.SetActive(false);
                    _view.Text("EmptyMark").gameObject.SetActive(true);
                    _view.Button("OpenButton").transition = Selectable.Transition.None;
                    _view.Button("OpenButton").interactable = false;
                    _motion.SetState(false, false);
                    return;
                }

                bool isLong = _model.IsLong;
                _edge.color = isLong ? UiFormatting.Red : UiFormatting.Green;
                RankingLayout.Stretch(_view.Image("Background").rectTransform);
                InventoryPaper.Face(_view.Image("Background"),new Color32(247,242,229,245));
                _view.Rect("FilledContent").gameObject.SetActive(true);
                _view.Text("EmptyMark").gameObject.SetActive(false);
                _view.Image("ItemImage").sprite = _services.Assets.GetSprite(MarketQuery.IconKey(_model.Item));
                _view.Image("ItemImage").color = _model.Delisted
                    ? new Color(0.45f, 0.47f, 0.45f, 0.65f)
                    : Color.white;
                BindName();
                _view.Text("NameText").color = _model.Delisted ? UiFormatting.Muted : UiFormatting.Ink;
                Text quantity = _view.Text("QuantityText");
                quantity.fontSize = cell.IsMixed ? (_compact ? 18 : 20) : (_compact ? 27 : 30);
                quantity.text = cell.IsMixed
                    ? "多" + cell.Quantity + " / 空" + cell.SecondaryQuantity
                    : "×" + cell.Quantity;
                BindCost(cell);
                _view.Text("ProfitText").text = cell.PriceAvailable ? UiFormatting.SignedMoney(cell.Floating) : "—";
                _profitRate.text = cell.PriceAvailable ? UiFormatting.Percent(cell.FloatingRate) : "缺现价";
                _view.Text("ProfitText").color = _model.Delisted || !cell.PriceAvailable
                    ? UiFormatting.Muted
                    : UiFormatting.Profit(cell.Floating);
                _profitRate.color = _view.Text("ProfitText").color;
                _profitRate.horizontalOverflow = HorizontalWrapMode.Overflow;
                RefreshTextLayout();

                _view.Rect("Actions").gameObject.SetActive(true);
                var inspect = _view.Button("ViewButton");
                inspect.gameObject.SetActive(true);
                inspect.image.rectTransform.anchorMax = new Vector2(interactive ? 0.5f : 1f,0);
                inspect.image.rectTransform.offsetMax = new Vector2(interactive ? -2 : 0,38);
                _view.Button("ClosePositionButton").gameObject.SetActive(interactive);
                _view.Button("ViewButton").interactable = true;
                _view.Button("ClosePositionButton").interactable = interactive && !_model.Delisted && !cell.IsMixed;
                UiView.SetLabel(_view.Button("ClosePositionButton"), cell.IsMixed ? "分仓" : cell.SlotCount > 1 ? "整仓平仓" : "平仓");
                _view.Image("CloseIcon").sprite = _services.Assets.GetSprite(isLong ? "icon_trade" : "icon_return");
                ColorBlock closeColors = _view.Button("ClosePositionButton").colors;
                closeColors.normalColor = UiFormatting.Ink;
                closeColors.highlightedColor = isLong ? UiFormatting.Red : UiFormatting.Green;
                closeColors.selectedColor = closeColors.highlightedColor;
                closeColors.pressedColor = isLong ? UiFormatting.RedDark : UiFormatting.GreenDark;
                _view.Button("ClosePositionButton").colors = closeColors;
                _view.Button("OpenButton").transition = Selectable.Transition.None;
                _view.Button("OpenButton").interactable = true;
                _motion.SetState(true, true);
            }

            private void BindCost(InventoryCellModel cell)
            {
                Text costText = _view.Text("CostText");
                costText.alignment = TextAnchor.MiddleRight;
                costText.font = _services.Assets.BoldFont;
                double average = cell.Quantity > 0 ? (double)cell.Basis / cell.Quantity : 0d;
                _basisLabel.text = _model.IsLong ? "购入" : "实收";
                _marketLabel.text = _model.IsLong ? "现价" : "还价";
                costText.text = UiFormatting.Money(average) + " 文";
                _marketCost.text = cell.PriceAvailable ? UiFormatting.Money(_model.CurrentPrice) + " 文" : "—";
                _marketCost.color = cell.PriceAvailable ? UiFormatting.Profit(cell.Floating) : UiFormatting.Muted;
            }

            private string TooltipTitle()
            {
                if (_model == null)
                    return "共享空格";
                return (_model.Delisted ? "退市 · " : string.Empty) + _model.Item.Name;
            }

            private string TooltipBody()
            {
                if (_model == null || _cell == null)
                    return "空格由做多与做空共同使用；这里只展示容量，不会创建仓位。";
                string description = _model.Item.Description ?? "";
                if (_cell.CellShortQuantity > 0)
                {
                    InventorySlotModel shortPosition = _cell.IsMixed ? _cell.SecondaryPosition : _model;
                    foreach (var due in _cell.DueQuantities)
                    {
                        string date = due.Key < 0 ? "到期待核" : "市周 " + due.Key + " 到期";
                        if (due.Key >= 0 && shortPosition.CurrentWeek >= 0)
                        {
                            int left = Math.Max(0, due.Key - shortPosition.CurrentWeek);
                            date += left == 0 ? "（本周）" : "（余 " + left + " 周）";
                        }
                        description += "\n" + date + " · " + due.Value + " 件";
                    }
                    if (_cell.DueQuantities.Count <= 1 && _cell.ShortSliceOtherQuantity > 0)
                        description += "\n另有 " + _cell.ShortSliceOtherQuantity + " 件较晚到期";
                }
                if (_cell.IsMixed) description += "\n旧仓合并陈列 · 多 " + _cell.Quantity + " 件，空 " + _cell.SecondaryQuantity + " 件";
                return description;
            }

            private string TooltipHint()
            {
                if (_model == null)
                    return "空格数量以顶部真实容量为准";
                string key = "mod:" + _model.Item.ItemId;
                if (!_interactive)
                    return key + " · 点击查看持仓行情";
                if (_model.Delisted || (_cell != null && _cell.IsMixed))
                    return key + " · 此仓不可直接平仓";
                return _cell != null && _cell.SlotCount > 1
                    ? key + " · 整仓平仓将结算此货全部 " + _model.Quantity + " 件"
                    : key + " · 平仓需预览确认";
            }

            private void Open()
            {
                if (_model != null && ViewRequested != null)
                    ViewRequested(_model);
            }

            private void Close()
            {
                if (_interactive && _model != null && !_model.Delisted &&
                    (_cell == null || !_cell.IsMixed) && CloseRequested != null)
                    CloseRequested(_model);
            }

            public void Dispose()
            {
                if (_view.IsAlive)
                {
                    _view.Button("OpenButton").onClick.RemoveListener(Open);
                    _view.Button("ViewButton").onClick.RemoveListener(Open);
                    _view.Button("ClosePositionButton").onClick.RemoveListener(Close);
                }
                _cell = null;
                _model = null;
                ViewRequested = null;
                CloseRequested = null;
                _view.Dispose();
            }
        }
    }
}
