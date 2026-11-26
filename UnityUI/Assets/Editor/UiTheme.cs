using System;
using UnityEngine;

namespace XingShang.UI.Authoring
{
    internal static class UiTheme
    {
        public static readonly Color Paper = Hex("EAE3D2");
        public static readonly Color PaperLight = Hex("F5F0E4");


        public static readonly Color PaperDeep = Hex("D8CFBA");
        public static readonly Color CardPaper = Hex("F7F2E5");
        public static readonly Color Ink = Hex("202321");
        public static readonly Color InkSoft = Hex("363B37");
        public static readonly Color Muted = Hex("6B726B");
        public static readonly Color Celadon = Hex("65706A");
        public static readonly Color Gold = Hex("B49358");
        public static readonly Color GoldLight = Hex("D8BC7B");
        public static readonly Color Red = Hex("B64036");
        public static readonly Color RedDark = Hex("81332E");
        public static readonly Color RedWash = Hex("EAD2C9");
        public static readonly Color Green = Hex("3F6B58");
        public static readonly Color GreenDark = Hex("2E5143");
        public static readonly Color GreenWash = Hex("D2DED2");
        public static readonly Color Scene = Hex("171C1A");
        public static readonly Color Backdrop = Hex("0C0F0D");
        public static readonly Color DarkCaption = Hex("AEB5AA");
        public static readonly Color DarkMuted = Hex("9DA69A");
        public static readonly Color DarkBody = Hex("D0D2C7");
        public static readonly Color Line = Alpha(Ink, 0.22f);
        public static readonly Color HardShadow = Alpha(Hex("141715"), 0.88f);
        public static readonly Color WindowShadow = Alpha(Color.black, 0.35f);

        public static class TypeScale
        {
            public const int Micro = 8;
            public const int Caption = 9;
            public const int Small = 10;
            public const int Label = 11;
            public const int Body = 12;
            public const int BodyLarge = 14;
            public const int Change = 15;
            public const int Numeric = 16;
            public const int CardTitle = 19;
            public const int SectionTitle = 19;
            public const int CartTitle = 20;
            public const int HeaderPrice = 23;
            public const int Money = 22;
            public const int CartNet = 23;
            public const int AssetValue = 26;
            public const int PageNumber = 28;
            public const int RankNumber = 29;
            public const int CardPrice = 37;
            public const int DetailTitle = 43;
            public const int DetailPrice = 58;
            public const int GhostNumber = 91;
        }

        public static class LineSpacing
        {
            public const float Body = 1.12f;
            public const float Serif = 1.75f;
            public const float Display = 1f;
        }

        public static class Space
        {
            public const int Hairline = 1;
            public const int Xs = 3;
            public const int S = 5;
            public const int M = 8;
            public const int L = 12;
            public const int Xl = 18;
            public const int Xxl = 24;
        }

        public static class Border
        {
            public const int Thin = 1;
            public const int Focus = 3;
            public const int Active = 4;
            public const int Rail = 5;
            public const int ModalAccent = 13;
            public const int WindowChamfer = 24;
            public const int WindowLowerChamfer = 20;
            public const int CardChamfer = 14;
            public const int ModalChamfer = 22;
        }

        public static class Shadow
        {
            public const int CardX = 4;
            public const int CardY = 5;
            public const int CardHoverX = 8;
            public const int CardHoverY = 10;
            public const int WindowX = 16;
            public const int WindowY = 18;
            public const int ModalX = 10;
            public const int ModalY = 12;
        }

        public static class Layer
        {
            public const int Page = 1;
            public const int Status = 3;
            public const int Ticker = 4;
            public const int Masthead = 5;
            public const int PaperGrain = 20;
            public const int Tooltip = 1000;
            public const int FlyToken = 1050;
            public const int Modal = 1100;
            public const int Toast = 1200;
            public const int DealStamp = 1300;
        }

        public static class Opacity
        {
            public const float PaperGrain = 0.075f;
            public const float CardChart = 0.54f;
            public const float CardChartHover = 0.72f;
            public const float GhostNumber = 0.045f;
            public const float ModalBackdrop = 0.76f;
        }

        public static class Duration
        {
            public const float Button = 0.12f;
            public const float CardHover = 0.15f;
            public const float ItemHover = 0.17f;
            public const float GoldLine = 0.18f;
            public const float WindowOpen = 0.46f;
            public const float WindowClose = 0.24f;
            public const float CartEntry = 0.22f;
            public const float FlyToken = 0.36f;
            public const float DealStamp = 0.65f;
            public const float ModalBackdrop = 0.18f;
            public const float ModalBody = 0.28f;
            public const float ToastIn = 0.22f;
            public const float ToastOut = 0.2f;
            public const float TooltipDelay = 0.15f;
            public const float TooltipIn = 0.12f;
            public const float InventoryHover = 0.13f;
            public const float InventoryActions = 0.16f;
            public const float TickerLoop = 32f;
            public const float TrendHit = 0.44f;
        }

        public static class Easing
        {
            public const float PaperOutX1 = 0.16f;
            public const float PaperOutY1 = 1f;
            public const float PaperOutX2 = 0.3f;
            public const float PaperOutY2 = 1f;
        }

        public static class Chart
        {
            public const int HeaderPoints = 14;
            public const int RankingPoints = 20;
            public const int CardPoints = 26;
            public const int DefaultDetailPoints = 26;
            public const float DetailBrushWidth = 4.2f;
            public const int NoiseSeed = 4908;
        }

        public static readonly LayoutProfile Desktop1920 = new LayoutProfile(
            "Desktop1920", 20f, 82f, 258f, 45f, 48f, 355f, 130f, 45f,
            34f, 360f, 28f, 20f, 18f, 26f, 20f, 23f, 15f, 6, 142f, 220f, 68f);

        public static readonly LayoutProfile Compact1366 = new LayoutProfile(
            "Compact1366", 14f, 70f, 224f, 38f, 39f, 305f, 102f, 39f,
            34f, 294f, 24f, 12f, 11f, 15f, 9f, 13f, 10f, 4, 132f, 138f, 57f);

        public static LayoutProfile LayoutFor(float width, float height)
        {
            return width <= 1450f || height <= 790f ? Compact1366 : Desktop1920;
        }

        public static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color Hex(string value)
        {
            return new Color(
                Convert.ToInt32(value.Substring(0, 2), 16) / 255f,
                Convert.ToInt32(value.Substring(2, 2), 16) / 255f,
                Convert.ToInt32(value.Substring(4, 2), 16) / 255f,
                1f);
        }

        internal sealed class LayoutProfile
        {
            internal LayoutProfile(
                string name, float windowInset, float mastheadHeight, float brandWidth,
                float brandSealWidth, float brandSealHeight, float indexWidth,
                float indexSparkWidth, float indexSparkHeight, float tickerHeight,
                float sidebarWidth, float statusHeight, float goodsGapX, float goodsGapY,
                float goodsPaddingLeft, float goodsPaddingTop, float goodsPaddingRight,
                float goodsPaddingBottom, int inventoryColumns, float inventoryRowHeight,
                float detailStageHeight, float rankingRowHeight)
            {
                Name = name;
                WindowInset = windowInset;
                MastheadHeight = mastheadHeight;
                BrandWidth = brandWidth;
                BrandSealWidth = brandSealWidth;
                BrandSealHeight = brandSealHeight;
                IndexWidth = indexWidth;
                IndexSparkWidth = indexSparkWidth;
                IndexSparkHeight = indexSparkHeight;
                TickerHeight = tickerHeight;
                SidebarWidth = sidebarWidth;
                StatusHeight = statusHeight;
                GoodsGapX = goodsGapX;
                GoodsGapY = goodsGapY;
                GoodsPaddingLeft = goodsPaddingLeft;
                GoodsPaddingTop = goodsPaddingTop;
                GoodsPaddingRight = goodsPaddingRight;
                GoodsPaddingBottom = goodsPaddingBottom;
                InventoryColumns = inventoryColumns;
                InventoryRowHeight = inventoryRowHeight;
                DetailStageHeight = detailStageHeight;
                RankingRowHeight = rankingRowHeight;
            }

            internal readonly string Name;
            internal readonly float WindowInset;
            internal readonly float MastheadHeight;
            internal readonly float BrandWidth;
            internal readonly float BrandSealWidth;
            internal readonly float BrandSealHeight;
            internal readonly float IndexWidth;
            internal readonly float IndexSparkWidth;
            internal readonly float IndexSparkHeight;
            internal readonly float TickerHeight;
            internal readonly float SidebarWidth;
            internal readonly float StatusHeight;
            internal readonly float GoodsGapX;
            internal readonly float GoodsGapY;
            internal readonly float GoodsPaddingLeft;
            internal readonly float GoodsPaddingTop;
            internal readonly float GoodsPaddingRight;
            internal readonly float GoodsPaddingBottom;
            internal readonly int InventoryColumns;
            internal readonly float InventoryRowHeight;
            internal readonly float DetailStageHeight;
            internal readonly float RankingRowHeight;
        }
    }
}
