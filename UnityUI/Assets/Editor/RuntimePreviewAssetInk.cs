using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        [Serializable]
        private sealed class InkWindow
        {
            public string state;
            public int leftWeek, rightWeek, sign, samples, signedHits, goldHits, longestMissingRun;
            public float begin, end;
        }

        [Serializable]
        private sealed class AssetInkEvidence
        {
            public int width, height;
            public Vector2 switchSize;
            public float frameAngle;
            public string mouseTitle, keyboardTitle;
            public List<InkWindow> windows = new List<InkWindow>();
            public List<ZeroCut> crossings = new List<ZeroCut>();
        }

        [Serializable]
        private sealed class ZeroCut
        {
            public int leftWeek;
            public Vector2 theoreticalPixel;
            public float firstAfterInkPx, lastBeforeInkPx;
        }

        private static IEnumerator AssetSignInk(RuntimePreviewInteractionContext c)
        {
            c.Open("assets",true);
            yield return c.WaitFrames(3);
            var a = c.Adapter;
            Graphic chart = (Graphic)a.AssetPlot;
            var rt = chart.rectTransform;
            Rect plot = (Rect)chart.GetType().GetMethod("GetPlotRect",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(chart,null);
            object[] axis = {1,new Rect(),0d,0d,0};
            bool hasAxis = (bool)chart.GetType().GetMethod("TryGetAssetAxisScale",
                BindingFlags.NonPublic|BindingFlags.Instance).Invoke(chart,axis);
            c.Check(hasAxis,"Signed return chart has no right axis.");
            var sign = c.Capture("continuous-signed-ink",c.Now,"one lot: amount and percent curves coincide; red above zero, green below",true);
            var button = a.AssetsSwitch;
            var frame = (RectTransform)button.transform.Find("SlantedFrame");
            var data = new AssetInkEvidence { width=c.Width,height=c.Height,switchSize=frame.rect.size,frameAngle=frame.localEulerAngles.z };
            c.Check(Mathf.Abs(frame.rect.width-frame.rect.height)<.01f && Mathf.Abs(Mathf.DeltaAngle(frame.localEulerAngles.z,0f))>=2f &&
                button.GetComponentsInChildren<Text>(true).Length==0,"Chart switch is not an icon-only tilted square.");

            var image = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!ImageConversion.LoadImage(image,File.ReadAllBytes(sign.png))) throw new IOException(sign.png);
                Color32[] pixels = image.GetPixels32();
                double min = (double)axis[2], max = (double)axis[3];
                Camera camera = chart.canvas.rootCanvas.worldCamera;
                int[] rates = RuntimePreviewAdapter.ReturnInkPercents;
                Action<int,float,float,string> check = (index,t0,t1,label) =>
                {
                    Vector2 p0 = InkScreenPoint(rt,camera,plot,min,max,index,rates[index]);
                    Vector2 p1 = InkScreenPoint(rt,camera,plot,min,max,index+1,rates[index+1]);
                    float mid = (t0+t1)*.5f;
                    int expected = Math.Sign(rates[index]+(rates[index+1]-rates[index])*mid);
                    var window = new InkWindow {state=label,leftWeek=52+index,rightWeek=53+index,sign=expected,begin=t0,end=t1};
                    Vector2 delta=p1-p0;
                    Vector2 normal=new Vector2(-delta.y,delta.x).normalized;
                    window.samples=Math.Max(2,(int)Math.Ceiling(delta.magnitude*(t1-t0)));
                    int missed=0;
                    for(int i=0;i<window.samples;i++)
                    {
                        float t=t0+(t1-t0)*i/(window.samples-1f);
                        Vector2 center=p0+delta*t;
                        bool found=false,gold=false;
                        for(float offset=-4f;offset<=4.01f;offset+=.35f)
                        {
                            Vector2 at=center+normal*offset;
                            int x=Mathf.Clamp(Mathf.FloorToInt(at.x),0,c.Width-1);
                            int y=Mathf.Clamp(Mathf.FloorToInt(at.y),0,c.Height-1);
                            Color32 ink=pixels[y*c.Width+x];
                            found |= MatchesRateInk(ink,expected);
                            gold |= ink.r>ink.g+11 && ink.g>ink.b+28;
                        }
                        if(found){window.signedHits++;missed=0;}
                        else{missed++;window.longestMissingRun=Math.Max(window.longestMissingRun,missed);}
                        if(gold)window.goldHits++;
                    }
                    c.Check(window.signedHits >= window.samples*.88f && window.longestMissingRun<=3,
                        label+" did not show a continuous signed brush: "+window.signedHits+"/"+window.samples+", gap "+window.longestMissingRun);
                    data.windows.Add(window);
                };
                check(1,.14f,.87f,"positive while falling");
                check(3,.14f,.87f,"positive while rising");
                check(12,.14f,.87f,"negative while recovering");
                check(11,.14f,.80f,"negative while falling");
                check(9,.16f,.84f,"flat at zero");
                check(14,.09f,.30f,"before crossing up");
                check(14,.45f,.88f,"after crossing up");
                check(17,.09f,.34f,"before crossing down");
                check(17,.50f,.88f,"after crossing down");
                int gold=0,count=0;
                foreach(InkWindow window in data.windows){gold+=window.goldHits;count+=window.samples;}
                c.Check(gold>=count*.45f,"The gold amount series lost its visible rim at overlapping observations: "+gold+"/"+count);
                foreach (int index in new[] { 14, 17 })
                {
                    int before = Math.Sign(rates[index]), after = -before;
                    float atZero = (float)rates[index] / (rates[index] - rates[index+1]);
                    Vector2 p0 = InkScreenPoint(rt,camera,plot,min,max,index,rates[index]);
                    Vector2 p1 = InkScreenPoint(rt,camera,plot,min,max,index+1,rates[index+1]);
                    Vector2 direction = (p1-p0).normalized, normal = new Vector2(-direction.y,direction.x);
                    Vector2 seam = Vector2.Lerp(p0,p1,atZero);
                    var cut = new ZeroCut {leftWeek=52+index,theoreticalPixel=seam,firstAfterInkPx=100,lastBeforeInkPx=-100};
                    for (float along=-8f;along<=8.01f;along+=.25f)
                        for (float across=-1.3f;across<=1.31f;across+=.3f)
                        {
                            Vector2 pos=seam+direction*along+normal*across;
                            Color32 ink=pixels[Mathf.FloorToInt(pos.y)*c.Width+Mathf.FloorToInt(pos.x)];
                            if (MatchesRateInk(ink,before)) cut.lastBeforeInkPx=Mathf.Max(cut.lastBeforeInkPx,along);
                            if (MatchesRateInk(ink,after)) cut.firstAfterInkPx=Mathf.Min(cut.firstAfterInkPx,along);
                        }
                    c.Check(Mathf.Abs(cut.lastBeforeInkPx)<=2.5f && Mathf.Abs(cut.firstAfterInkPx)<=2.5f,
                        "Zero-color seam is displaced: "+cut.leftWeek+" "+cut.lastBeforeInkPx+"/"+cut.firstAfterInkPx+" px.");
                    data.crossings.Add(cut);
                }
            }
            finally { Destroy(image); }

            Pointer(c,button.gameObject,true);
            yield return c.Wait(.65f);
            data.mouseTitle=AssetTip(a);
            c.Check(data.mouseTitle=="查看资产留痕","Mouse tooltip did not describe the destination.");
            c.Capture("icon-hover",c.Now,"icon tooltip in profit view",true);
            Click(c,button.gameObject);
            yield return c.WaitFrames(3);
            c.Check(a.AssetsTitle.text=="行商资产留痕","Icon click did not open the ledger.");
            Pointer(c,button.gameObject,false);
            EventSystem.current.SetSelectedGameObject(null);
            yield return c.WaitFrames(2);
            EventSystem.current.SetSelectedGameObject(button.gameObject,new BaseEventData(EventSystem.current));
            yield return c.Wait(.65f);
            data.keyboardTitle=AssetTip(a);
            c.Check(data.keyboardTitle=="查看盈亏走势","Keyboard focus did not announce the reverse action.");
            c.Capture("icon-keyboard",c.Now,"focused icon in ledger view",true);
            ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            yield return c.WaitFrames(2);
            c.Check(a.AssetsTitle.text=="总盈亏走势","Native submit did not restore profit view.");
            VisualValidation.WriteJson(Path.Combine(Path.GetDirectoryName(sign.png),"signed-ink-"+c.Width+"x"+c.Height+".json"),data);
        }

        private static string AssetTip(RuntimePreviewAdapter a)
        {
            RectTransform tip=a.TooltipRoot;
            if(tip==null || !tip.gameObject.activeInHierarchy) return "";
            foreach(Text t in tip.GetComponentsInChildren<Text>()) if(t.name=="TitleText") return t.text;
            return "";
        }

        private static Vector2 InkScreenPoint(RectTransform chart,Camera camera,Rect plot,double min,double max,int i,int value)
        {
            Vector2 local=new Vector2(plot.xMin+plot.width*i/25f,plot.yMin+plot.height*(float)((value-min)/(max-min)));
            return RectTransformUtility.WorldToScreenPoint(camera,chart.TransformPoint(local));
        }

        private static bool MatchesRateInk(Color32 c,int sign)
        {
            if(sign>0) return c.r>c.g+42 && c.g-c.b<30 && c.r>c.b+48;
            if(sign<0) return c.g>c.r+15 && c.g>c.b+8;
            return Math.Max(c.r,Math.Max(c.g,c.b))<176 && Math.Max(c.r,Math.Max(c.g,c.b))-Math.Min(c.r,Math.Min(c.g,c.b))<=14;
        }
    }
}
