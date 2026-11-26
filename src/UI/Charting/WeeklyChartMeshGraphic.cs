using UnityEngine;
using UnityEngine.UI;

namespace XingShangMod.UI.Charting
{
    internal enum WeeklyChartMeshLayer
    {
        Plot,
        Overlay
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class WeeklyChartMeshGraphic : MaskableGraphic
    {
        internal WeeklyChartGraphic Owner;
        internal WeeklyChartMeshLayer Layer;
        internal Texture2D DryBrush;

        public override Texture mainTexture
        {
            get
            {
                return Layer == WeeklyChartMeshLayer.Plot && DryBrush != null
                    ? DryBrush
                    : Texture2D.whiteTexture;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            color = Color.white;
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (Owner == null)
                return;
            if (Layer == WeeklyChartMeshLayer.Plot)
                Owner.PopulatePlot(vertexHelper);
            else
                Owner.PopulateOverlay(vertexHelper);
        }
    }
}
