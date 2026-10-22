using ResourceLedger.Core;
using UnityEngine;
using Verse;
namespace ResourceLedger.UI;
public static class LedgerGraph {
 public static readonly Color[] Colors={new(.25f,.8f,1),new(1,.8f,.25f),new(.7f,.5f,1),new(.35f,1,.5f),new(1,.5f,.65f),new(.9f,.9f,.9f)};
 public static void Draw(Rect rect,IReadOnlyList<(string Label,Series Series,double Factor)> sources,long from,long to,bool relative,bool bars){
  Widgets.DrawBoxSolid(rect,new Color(.07f,.08f,.10f));var plot=new Rect(rect.x+74,rect.y+15,Math.Max(1,rect.width-100),Math.Max(1,rect.height-55));
  Widgets.DrawLineHorizontal(plot.x,plot.yMax,plot.width);Widgets.DrawLineVertical(plot.x,plot.y,plot.height);
  if(to<=from||sources.Count==0){Widgets.Label(rect.ContractedBy(20),TextCatalog.Get("NoHistory"));return;}
  if(bars){
   var series=sources[0];var periods=ViewMath.Bars(series.Series,from,to);double max=Math.Max(1,periods.SelectMany(p=>new[]{p.In*series.Factor,p.Out*series.Factor}).DefaultIfEmpty(1).Max());float middle=plot.center.y;
   Widgets.DrawLineHorizontal(plot.x,middle,plot.width,new Color(.5f,.5f,.5f));
   foreach(var p in periods){
    float x=plot.x+(float)((p.To-from)/(double)(to-from))*plot.width;
    float width=Math.Clamp((float)((p.To-p.From)/(double)(to-from))*plot.width*.4f,1,18);
    float plus=(float)(p.In*series.Factor/max)*plot.height*.46f,minus=(float)(p.Out*series.Factor/max)*plot.height*.46f;
    if(!p.Gap){Widgets.DrawBoxSolid(new Rect(x-width,middle-plus,width,plus),new Color(.3f,.8f,.4f));Widgets.DrawBoxSolid(new Rect(x,middle,width,minus),new Color(.95f,.4f,.3f));}
    var hit=new Rect(x-width,plot.y,width*2,plot.height);
    TooltipHandler.TipRegion(hit,TextCatalog.Day(p.To)+"\n"+TextCatalog.Get("Increase")+": "+TextCatalog.Number(p.In*series.Factor,2)+"\n"+TextCatalog.Get("Decrease")+": "+TextCatalog.Number(p.Out*series.Factor,2)+(p.Gap?"\n"+TextCatalog.Get("Gap"):""));
   }
   Widgets.Label(new Rect(rect.x,plot.y,72,25),TextCatalog.Number(max,1));Widgets.Label(new Rect(rect.x,middle-12,72,25),"0");
  }else{
   var plots=sources.Select(s=>(s.Label,Points:ViewMath.Points(s.Series,from,to,s.Factor,relative))).ToList();
   var values=plots.SelectMany(p=>p.Points).Select(p=>p.Y).Where(double.IsFinite).ToList();
   if(values.Count==0){Widgets.Label(rect.ContractedBy(20),TextCatalog.Get("NoHistory"));return;}
   double min=relative?Math.Min(0,values.Min()):0,max=Math.Max(min+1,values.Max());
   float Y(double value)=>plot.yMax-(float)((value-min)/(max-min))*plot.height;
   Widgets.Label(new Rect(rect.x,plot.y,72,25),TextCatalog.Number(max,1)+(relative?"%":""));
   Widgets.Label(new Rect(rect.x,plot.yMax-23,72,25),TextCatalog.Number(min,1));
   for(int k=0;k<plots.Count;k++){
    var points=plots[k].Points;
    for(int i=1;i<points.Count;i++){
     var a=points[i-1];var b=points[i];if(b.Gap||!double.IsFinite(a.Y)||!double.IsFinite(b.Y))continue;
     var p1=new Vector2(plot.x+(float)((a.Tick-from)/(double)(to-from))*plot.width,Y(a.Y));var p2=new Vector2(plot.x+(float)((b.Tick-from)/(double)(to-from))*plot.width,Y(b.Y));
     Widgets.DrawLine(p1,p2,Colors[k%Colors.Length],1.7f);
    }
   }
   if(Mouse.IsOver(plot)){
    float fraction=Math.Clamp((Event.current.mousePosition.x-plot.x)/plot.width,0,1);long target=from+(long)((to-from)*fraction);
    var text=new List<string>();
    for(int k=0;k<plots.Count;k++){var point=plots[k].Points.MinBy(p=>Math.Abs(p.Tick-target));if(point!=null)text.Add(plots[k].Label+": "+(double.IsFinite(point.Y)?TextCatalog.Number(point.Y,relative?2:3):"—")+" ("+TextCatalog.Day(point.Tick)+")");}
    TooltipHandler.TipRegion(plot,string.Join("\n",text));
    Widgets.DrawLineVertical(plot.x+fraction*plot.width,plot.y,plot.height);
   }
  }
  Widgets.Label(new Rect(plot.x,plot.yMax+6,180,25),TextCatalog.Day(from));Widgets.Label(new Rect(plot.xMax-170,plot.yMax+6,180,25),TextCatalog.Day(to));
 }
}

