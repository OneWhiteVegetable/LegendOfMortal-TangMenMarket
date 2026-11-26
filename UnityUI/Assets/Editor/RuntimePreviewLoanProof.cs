using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        private Texture2D _inventoryReadback;
        [Serializable]
        private sealed class HeaderInk
        {
            public string key, text;
            public RectInt panel, actualInk;
            public int leftPadding, rightPadding, inkPixels;
        }
        [Serializable]
        private sealed class AttributesFrame
        {
            public int life, lightness, strength;
            public bool empty;
            public List<HeaderInk> labels = new List<HeaderInk>();
        }
        [Serializable]
        private sealed class CountdownFrame
        {
            public int weeks, numeralPixels, aboveCardPixels;
            public RectInt card, graphic;
        }
        [Serializable]
        private sealed class InventoryProof
        {
            public int width, height;
            public string tooltip;
            public List<CountdownFrame> countdowns = new List<CountdownFrame>();
            public List<AttributesFrame> attributes = new List<AttributesFrame>();
        }

        private static IEnumerator LoanProof(RuntimePreviewInteractionContext c)
        {
            c.Open("inventory", true);
            yield return c.WaitFrames(4);
            var adapter = c.Adapter;
            var runner = c.Runner;
            GameObject slot = adapter.ShortSlotRoot;
            Pointer(c, slot, true);
            yield return c.Wait(0.65f);
            var hover = adapter.TooltipRoot;
            var proof = new InventoryProof { width = c.Width, height = c.Height };
            if (hover != null && hover.gameObject.activeInHierarchy)
            {
                foreach (Text text in hover.GetComponentsInChildren<Text>())
                    if (text.name == "BodyText") proof.tooltip = text.text;
            }
            c.Check(adapter.HasBothMaturities(proof.tooltip),"Real tooltip must show both deadlines, weeks remaining and quantities.");
            c.Capture("two-deadlines",c.Now,"two batches; earliest numeral and full due-date hover text",true);
            Pointer(c, slot, false);
            yield return c.WaitFrames(3);
            string dir = Path.Combine(runner._options.OutputPath,"pixels-" + c.Width + "x" + c.Height);
            Directory.CreateDirectory(dir);
            Graphic badge = adapter.LoanCountdown;
            c.Check(badge.mainTexture != null && badge.mainTexture.name == "loan_maturity_atlas", "Incorrect numeric artwork.");
            c.Check(!badge.raycastTarget && badge.GetComponentsInChildren<Text>(true).Length == 0,
                "Ticket may not intercept input or carry printed prose.");
            int[] original = adapter.GetLoanDueWeeks();
            int[] dates = new int[original.Length];
            Action restoreInventory = null;
            try
            {
                for (int n = 0; n <= 24; n++)
                {
                    for (int i = 0; i < dates.Length; i++) dates[i] = adapter.LoanWeek + n;
                    adapter.SetLoanDueWeeks(dates);
                    yield return c.WaitFrames(1);
                    var face = (RectTransform)slot.transform.Find("MotionPivot/CardClip/Background");
                    RectInt card = runner.ScreenBounds(face);
                    RectInt stamp = runner.ScreenBounds(badge.rectTransform);
                    RectInt region = Bounds(card,stamp,5,c.Width,c.Height);
                    Color32[] shown = runner.ReadInventoryPixels(region,Path.Combine(dir,"card-"+n.ToString("00")+".png"));
                    badge.enabled = false;
                    Color32[] plain = runner.ReadInventoryPixels(region,null);
                    badge.enabled = true;
                    var frame = new CountdownFrame { weeks=n, card=card, graphic=stamp };
                    for (int y = 0; y < region.height; y++)
                        for (int x = 0; x < region.width; x++)
                        {
                            int i = y*region.width+x;
                            if (Difference(shown[i],plain[i]) < 8) continue;
                            var ink = shown[i];
                            bool colored = n <= 3 ? ink.r > ink.g + 55 && ink.r > ink.b + 55 :
                                ink.g > ink.r + 22 && ink.g > ink.b + 12;
                            if (colored) frame.numeralPixels++;
                            if (region.y+y >= card.yMax) frame.aboveCardPixels++;
                        }
                    c.Check(frame.aboveCardPixels >= 2,"Ticket was clipped against its card for countdown "+n);
                    c.Check(frame.numeralPixels >= 8,"Countdown "+n+" did not paint the expected ink.");
                    proof.countdowns.Add(frame);
                }
                var samples = new[]{new[]{0,0,0},new[]{0,0,0},new[]{7,8,12},new[]{30,30,30},new[]{99,99,99},new[]{120,160,199},new[]{300,300,300}};
                for (int index = 0; index < samples.Length; index++)
                {
                    var stats = samples[index];
                    bool empty = index == 0;
                    if (empty) restoreInventory = adapter.TemporarilyEmptyInventory();
                    adapter.SetInventoryAttributes(stats[0],stats[1],stats[2]);
                    yield return c.WaitFrames(2);
                    RectTransform cap = adapter.InventoryMetric("SummaryCapacityCell");
                    RectTransform wt = adapter.InventoryMetric("SummaryWeightCell");
                    RectInt panels = Bounds(runner.ScreenBounds(cap),runner.ScreenBounds(wt),14,c.Width,c.Height);
                    string name = "attributes-"+stats[0]+(empty ? "-empty" : "")+".png";
                    Color32[] originalInk = runner.ReadInventoryPixels(panels,Path.Combine(dir,name));
                    var sample = new AttributesFrame {life=stats[0],lightness=stats[1],strength=stats[2],empty=empty};
                    foreach (var panel in new[]{cap,wt})
                        foreach (var text in panel.GetComponentsInChildren<Text>())
                        {
                            text.enabled=false;
                            Color32[] hidden=runner.ReadInventoryPixels(panels,null);
                            text.enabled=true;
                            int l=panels.xMax,r=panels.xMin,b=panels.yMax,t=panels.yMin,count=0;
                            for (int y=0; y<panels.height; y++)
                                for(int x=0; x<panels.width; x++)
                                {
                                    int i=y*panels.width+x;
                                    if(Difference(originalInk[i],hidden[i])<8) continue;
                                    l=Math.Min(l,panels.x+x);r=Math.Max(r,panels.x+x+1);
                                    b=Math.Min(b,panels.y+y);t=Math.Max(t,panels.y+y+1);count++;
                                }
                            RectInt bounds=runner.ScreenBounds(panel);
                            var ink=new HeaderInk{key=text.name,text=text.text,panel=bounds,actualInk=new RectInt(l,b,r-l,t-b),
                                leftPadding=l-bounds.xMin,rightPadding=bounds.xMax-r,inkPixels=count};
                            c.Check(count>0 && ink.leftPadding>=3 && ink.rightPadding>=3 &&
                                b>=bounds.yMin && t<=bounds.yMax,"Out-of-bounds metric ink: "+text.name+" / "+text.text);
                            sample.labels.Add(ink);
                        }
                    c.Check(cap.GetChild(0).GetComponent<Text>().text.EndsWith(stats[0].ToString()) &&
                        wt.GetChild(0).GetComponent<Text>().text.Contains("轻功 "+stats[1]+" / 内功 "+stats[2]),
                        "Panel did not show the actual bound attributes.");
                    if (stats[0] == 0)
                        c.Check(cap.GetChild(1).GetComponent<Text>().text == (empty ? "0/2 格" : "超载 · 共 2 格"),
                            "Zero-carry inventory displayed an incorrect or sentinel slot count.");
                    proof.attributes.Add(sample);
                    if (restoreInventory != null) { restoreInventory(); restoreInventory=null; }
                }
            }
            finally
            {
                badge.enabled=true;
                if (restoreInventory != null) restoreInventory();
                adapter.RestoreInventoryAttributes();
                adapter.SetLoanDueWeeks(original);
                if (runner._inventoryReadback != null) Destroy(runner._inventoryReadback);
                runner._inventoryReadback = null;
            }
            VisualValidation.WriteJson(Path.Combine(dir,"evidence.json"),proof);
        }

        private RectInt ScreenBounds(RectTransform rt)
        {
            var corners=new Vector3[4];rt.GetWorldCorners(corners);
            Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 v=RectTransformUtility.WorldToScreenPoint(_camera,corner);
                min=Vector2.Min(min,v);max=Vector2.Max(max,v);
            }
            int x=Mathf.FloorToInt(min.x),y=Mathf.FloorToInt(min.y);
            return new RectInt(x,y,Mathf.CeilToInt(max.x)-x,Mathf.CeilToInt(max.y)-y);
        }

        private static RectInt Bounds(RectInt first,RectInt second,int padding,int w,int h)
        {
            int x=Math.Max(0,Math.Min(first.xMin,second.xMin)-padding);
            int y=Math.Max(0,Math.Min(first.yMin,second.yMin)-padding);
            return new RectInt(x,y,Math.Min(w,Math.Max(first.xMax,second.xMax)+padding)-x,
                Math.Min(h,Math.Max(first.yMax,second.yMax)+padding)-y);
        }

        private Color32[] ReadInventoryPixels(RectInt rect,string png)
        {
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=_target;
                // D3D's partial ReadPixels origin depends on the render target orientation.
                // Capture identically to the full viewport proof, then crop CPU rows.
                if (_inventoryReadback == null)
                    _inventoryReadback = new Texture2D(_target.width,_target.height,TextureFormat.RGBA32,false,false);
                _inventoryReadback.ReadPixels(new Rect(0,0,_target.width,_target.height),0,0);
                _inventoryReadback.Apply(false,false);
                var source = _inventoryReadback.GetRawTextureData<Color32>();
                var result = new Color32[rect.width * rect.height];
                for (int row = 0; row < rect.height; row++)
                    Unity.Collections.NativeArray<Color32>.Copy(source,(rect.y+row)*_target.width+rect.x,
                        result,row*rect.width,rect.width);
                if(png!=null)
                {
                    var crop = new Texture2D(rect.width,rect.height,TextureFormat.RGBA32,false,false);
                    try
                    {
                        crop.SetPixels32(result);
                        crop.Apply(false,false);
                        File.WriteAllBytes(png,ImageConversion.EncodeToPNG(crop));
                    }
                    finally { Destroy(crop); }
                }
                return result;
            }
            finally{RenderTexture.active=previous;}
        }
        private static int Difference(Color32 a,Color32 b)
        {
            return Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
        }
    }
}
