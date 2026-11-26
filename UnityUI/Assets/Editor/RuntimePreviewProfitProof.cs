using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        private static IEnumerator AssetProfitProof(RuntimePreviewInteractionContext c)
        {
            c.Open("assets",true);
            yield return c.WaitFrames(4);
            var a = c.Adapter;
            var chart = a.AssetPlot;
            var rt = (RectTransform)chart.transform;
            c.Check(a.AssetsTitle.text == "总盈亏走势","Assets must open on cumulative trading returns.");
            c.Check(ChartFloat(chart,"TotalPointCount") == 78,"Reconciled legacy history must provide 78 weeks.");
            c.Capture("default-profit",c.Now,"old weekly snapshots recovered using visible quotes and transactions",true);
            Rect plot = (Rect)chart.GetType().GetMethod("GetPlotRect",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chart,null);
            var end = ProfitPointer(c,rt,new Vector2(plot.xMax-1,plot.center.y));
            ExecuteEvents.Execute(chart.gameObject,end,ExecuteEvents.pointerEnterHandler);
            CheckProfitRows(c,chart,"-100 文","-10%");
            c.Capture("selected-loss",c.Now,"last week: -100 wen divided by 1000 wen is -10%",true);
            var center = ProfitPointer(c,rt,plot.center);
            center.scrollDelta=new Vector2(0,3);
            ExecuteEvents.Execute(chart.gameObject,center,ExecuteEvents.scrollHandler);
            float range=ChartFloat(chart,"VisibleRange"), start=ChartFloat(chart,"VisibleStart");
            c.Check(range<26,"Mouse wheel did not zoom the week axis.");
            ExecuteEvents.Execute(chart.gameObject,center,ExecuteEvents.pointerDownHandler);
            center.position += new Vector2(75,0);
            ExecuteEvents.Execute(chart.gameObject,center,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(chart.gameObject,center,ExecuteEvents.pointerUpHandler);
            c.Check(ChartFloat(chart,"VisibleStart")<start,"Dragging to the right did not reveal earlier weeks.");
            var rateAxis = ProfitPointer(c,rt,new Vector2(plot.xMax+18,plot.center.y));
            rateAxis.scrollDelta = new Vector2(0,4);
            ExecuteEvents.Execute(chart.gameObject,rateAxis,ExecuteEvents.scrollHandler);
            float zoom=ChartFloat(chart,"YZoomFactor");
            c.Check(zoom>1,"Right-hand percentage axis did not take the wheel.");
            a.RefreshInventoryData();
            yield return c.WaitFrames(2);
            c.Check(Math.Abs(ChartFloat(chart,"YZoomFactor")-zoom)<.001 && ChartFloat(chart,"VisibleRange")==range,
                "Readonly refresh lost the selected horizontal or vertical zoom.");
            double z1=AxisZero(chart,0), z2=AxisZero(chart,1);
            c.Check(Math.Abs(z1-z2)<.00001,"Amount and rate axes disagree about zero after zoom.");
            c.Capture("pan-zoom",c.Now,"time drag and right-axis scroll modify both curves",true);
            Click(c,a.AssetsChartReset.gameObject);
            yield return c.WaitFrames(2);
            c.Check(ChartFloat(chart,"YZoomFactor")==1 && ChartFloat(chart,"VisibleRange")==26,
                "Reset failed to restore both scales.");
            Click(c,a.AssetsSwitch.gameObject);
            yield return c.WaitFrames(2);
            c.Check(a.AssetsTitle.text=="行商资产留痕","Chart switch failed to restore the signed cash / goods graph.");
            end=ProfitPointer(c,rt,new Vector2(plot.xMax-1,plot.center.y));
            ExecuteEvents.Execute(chart.gameObject,end,ExecuteEvents.pointerEnterHandler);
            CheckProfitRows(c,chart,"-125 文","900 文");
            c.Capture("signed-ledger",c.Now,"historical wallet below zero, independent goods axis",true);
            Click(c,a.AssetsSwitch.gameObject);
            yield return c.WaitFrames(2);
            c.Check(a.AssetsTitle.text=="总盈亏走势","Chart toggle failed to return to P&L.");
            Action restore = a.OmitProfitHistory(false);
            try
            {
                yield return c.WaitFrames(2);
                c.Check(ChartFloat(chart,"TotalPointCount")==1,"Newly opened old save should show only its known current mark.");
                c.Capture("one-known-week",c.Now,"current point, no invented curve",true);
            }
            finally { restore(); }
            restore = a.OmitProfitHistory(true);
            try
            {
                yield return c.WaitFrames(2);
                c.Check(ChartFloat(chart,"TotalPointCount")==0,"Missing opening records silently became zero on the chart.");
                c.Check(a.AssetsEmptyMessage.gameObject.activeInHierarchy,"Missing-data explanation is hidden.");
                c.Capture("missing-input",c.Now,"missing trade log leaves the graph empty",true);
            }
            finally { restore(); }
            restore = a.OmitProfitHistory(true,true);
            try
            {
                yield return c.WaitFrames(2);
                var middle=ProfitPointer(c,rt,plot.center);
                ExecuteEvents.Execute(chart.gameObject,middle,ExecuteEvents.pointerEnterHandler);
                CheckProfitRows(c,chart,"0 文","—");
                c.Capture("no-capital",c.Now,"zero monetary profit, undefined percentage",true);
            }
            finally { restore(); }
        }

        private static void CheckProfitRows(RuntimePreviewInteractionContext c, Component chart, string left, string right)
        {
            bool hasLeft=false,hasRight=false;
            foreach(Text row in chart.GetComponentsInChildren<Text>())
            {
                if (!row.gameObject.activeInHierarchy || row.name != "HoverSeries") continue;
                hasLeft |= row.text.EndsWith(left,StringComparison.Ordinal);
                hasRight |= row.text.EndsWith(right,StringComparison.Ordinal);
            }
            c.Check(hasLeft && hasRight,"Point readout did not show "+left+" and "+right+" with correct units.");
        }

        private static PointerEventData ProfitPointer(RuntimePreviewInteractionContext c,RectTransform chart,Vector2 local)
        {
            Canvas canvas=chart.GetComponentInParent<Canvas>().rootCanvas;
            Vector2 pos=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,chart.TransformPoint(local));
            var data=new PointerEventData(EventSystem.current){position=pos,pointerId=-1,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,hits);
            c.Check(hits.Count>0 && ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject)==chart.gameObject,
                "An overlay blocked the chart raycast at "+pos);
            if(hits.Count>0)data.pointerPressRaycast=data.pointerCurrentRaycast=hits[0];
            return data;
        }

        private static double AxisZero(Component chart,int index)
        {
            MethodInfo method=chart.GetType().GetMethod("TryGetAssetAxisScale",BindingFlags.NonPublic|BindingFlags.Instance);
            object[] args={index,new Rect(),0d,0d,0};
            if(!(bool)method.Invoke(chart,args)) return double.NaN;
            return -(double)args[2]/((double)args[3]-(double)args[2]);
        }
    }
}
