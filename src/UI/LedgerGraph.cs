using ResourceLedger.Core;
using UnityEngine;
using Verse;
namespace ResourceLedger.UI;

public static class LedgerGraph {
 public static readonly Color[] Colors={new(.25f,.8f,1),new(1,.8f,.25f),new(.7f,.5f,1),new(.35f,1,.5f),new(1,.5f,.65f),new(.9f,.9f,.9f)};
 private static string cachedKey="";
 private static List<List<ChartBucket>> cached=new();
 public static void Reset(){cachedKey="";cached.Clear();}
 public static void Draw(Rect rect,IReadOnlyList<(string Label,Series Series,double Factor)> sources,long from,long to,long requestedInterval,bool increase,bool decrease,bool net,string unit,string cacheToken){
  Widgets.DrawBoxSolid(rect,new Color(.07f,.08f,.10f));
  if(to<=from||sources.Count==0){Widgets.Label(rect.ContractedBy(20),TextCatalog.Get("NoHistory"));return;}
  var known=sources.Select(s=>Queries.KnownRange(s.Series,from,to)).Where(r=>r!=null).Select(r=>r!.Value).ToList();
  bool limited=false;
  if(known.Count>0){long first=known.Min(r=>r.From),last=known.Max(r=>r.To);if(last>first){limited=first>from||last<to;from=first;to=last;}}
  long interval=requestedInterval<=0?(to-from<=120000?2500:60000):Math.Max(2500,requestedInterval);
  interval=Math.Max(interval,(long)Math.Ceiling((to-from)/(240d*interval))*interval);
  // Unchanged periods can be split exactly, so only periods with flows limit how fine the buckets can be.
  interval=Math.Max(interval,sources.SelectMany(s=>s.Series.Periods).Where(p=>!p.Flat&&p.From>=from&&p.To<=to).Select(p=>p.To-p.From).DefaultIfEmpty(interval).Max());
  string key=cacheToken+"|"+from+"|"+to+"|"+interval;
  if(key!=cachedKey){cached=sources.Select(s=>ChartMath.Buckets(s.Series,from,to,interval)).ToList();cachedKey=key;}
  var plot=new Rect(rect.x+90,rect.y+51,Math.Max(1,rect.width-195),Math.Max(1,rect.height-89));
  var stockScale=ChartMath.Scale(0,sources.SelectMany((s,i)=>cached[i].Select(p=>p.Stock*s.Factor)).DefaultIfEmpty(0).Max());
  var values=sources.SelectMany((s,i)=>cached[i].Where(b=>!b.Gap).SelectMany(b=>new[]{increase?b.Increase*s.Factor:0,decrease?-b.Decrease*s.Factor:0,net?(b.Increase-b.Decrease)*s.Factor:0})).ToList();
  var flowScale=ChartMath.Scale(values.DefaultIfEmpty(0).Min(),values.DefaultIfEmpty(0).Max());
  float X(long tick)=>plot.x+(float)((tick-from)/(double)(to-from))*plot.width;
  float StockY(double value)=>plot.yMax-(float)((value-stockScale.Min)/(stockScale.Max-stockScale.Min))*plot.height;
  float FlowY(double value)=>plot.yMax-(float)((value-flowScale.Min)/(flowScale.Max-flowScale.Min))*plot.height;
  Widgets.Label(new Rect(rect.x+8,rect.y+3,rect.width/2-10,24),TextCatalog.Get("Stock")+" ("+unit+")");
  Text.Anchor=TextAnchor.UpperRight;
  Widgets.Label(new Rect(rect.center.x,rect.y+3,rect.width/2-10,24),TextCatalog.Get("Change")+" ("+unit+")");Text.Anchor=TextAnchor.UpperLeft;
  string grouping=interval%60000==0?TextCatalog.Get("GroupDays",TextCatalog.Number(interval/60000d,0)):TextCatalog.Get("GroupHours",TextCatalog.Number(interval/2500d,0));
  Widgets.Label(new Rect(plot.x,rect.y+26,plot.width,24),grouping+" · "+TextCatalog.Get("ChartLegend"));
  if(limited)TooltipHandler.TipRegion(new Rect(plot.x,rect.y+26,plot.width,24),TextCatalog.Get("RecordedRangeOnly"));
  foreach(double tick in stockScale.Ticks){float y=StockY(tick);Widgets.DrawLineHorizontal(plot.x,y,plot.width,new Color(.3f,.32f,.35f,.35f));Text.Anchor=TextAnchor.MiddleRight;Widgets.Label(new Rect(rect.x,y-12,82,24),AxisNumber(tick,stockScale.Step));}
  foreach(double tick in flowScale.Ticks){float y=FlowY(tick);Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(plot.xMax+8,y-12,90,24),AxisNumber(tick,flowScale.Step));}
  Text.Anchor=TextAnchor.UpperLeft;
  Widgets.DrawLineVertical(plot.x,plot.y,plot.height);Widgets.DrawLineVertical(plot.xMax,plot.y,plot.height);
  Widgets.DrawLineHorizontal(plot.x,FlowY(0),plot.width,new Color(.6f,.62f,.65f,.65f));
  TooltipHandler.TipRegion(new Rect(rect.x,plot.y,88,plot.height),TextCatalog.Get("AxisStep",TextCatalog.Get("Stock"),TextCatalog.Number(stockScale.Step,Decimals(stockScale.Step)),unit));
  TooltipHandler.TipRegion(new Rect(plot.xMax,plot.y,105,plot.height),TextCatalog.Get("AxisStep",TextCatalog.Get("Change"),TextCatalog.Number(flowScale.Step,Decimals(flowScale.Step)),unit));
  float cluster=Math.Clamp((float)(interval/(double)(to-from)*plot.width)*.78f,2,46),width=Math.Max(.5f,cluster/sources.Count);
  for(int k=0;k<sources.Count;k++){
   var source=sources[k];var points=cached[k];var baseColor=Colors[k%Colors.Length];
   var plus=sources.Count==1?LedgerStyle.Positive:baseColor;var minus=sources.Count==1?LedgerStyle.Negative:baseColor;var netColor=sources.Count==1?LedgerStyle.Warning:baseColor;
   foreach(var b in points){float height=Math.Max(0,plot.yMax-StockY(b.Stock*source.Factor));if(height>0)Widgets.DrawBoxSolid(new Rect(Math.Max(plot.x,X(b.To)-cluster+k*width),plot.yMax-height,width,height),new Color(baseColor.r,baseColor.g,baseColor.b,b.Gap?.14f:.30f));}
   void Line(Func<ChartBucket,double> value,Color color,int style){
    for(int i=0;i<points.Count;i++){
     var b=points[i];if(b.Gap)continue;var end=new Vector2(X(b.To),FlowY(value(b)*source.Factor));
     Widgets.DrawBoxSolid(new Rect(end.x-1.5f,end.y-1.5f,3,3),color);
     if(i==0||points[i-1].Gap||points[i-1].To!=b.From)continue;
     var start=new Vector2(X(points[i-1].To),FlowY(value(points[i-1])*source.Factor));Segment(start,end,color,style);
    }
   }
   if(increase)Line(b=>b.Increase,plus,0);if(decrease)Line(b=>-b.Decrease,minus,1);if(net)Line(b=>b.Increase-b.Decrease,netColor,2);
  }
  if(Mouse.IsOver(plot)){
   double fraction=Math.Clamp((Event.current.mousePosition.x-plot.x)/plot.width,0,1);long target=from+(long)((to-from)*fraction);
   var closest=cached.SelectMany(p=>p).MinBy(p=>Math.Abs(p.To-target));
   if(closest!=null){
    Widgets.DrawLine(new Vector2(X(closest.To),plot.y),new Vector2(X(closest.To),plot.yMax),new Color(.8f,.8f,.8f,.6f),1);
    var tips=new List<string>();
    for(int i=0;i<sources.Count;i++){
     var b=cached[i].MinBy(p=>Math.Abs(p.To-closest.To));if(b==null)continue;var s=sources[i];
     tips.Add(s.Label+" · "+TextCatalog.Day(b.From)+" → "+TextCatalog.Day(b.To));tips.Add(TextCatalog.Get("Stock")+": "+TextCatalog.Number(b.Stock*s.Factor,2)+" "+unit);
     if(b.Gap)tips.Add(TextCatalog.Get("Gap"));else{tips.Add(TextCatalog.Get("Increase")+": +"+TextCatalog.Number(b.Increase*s.Factor,2)+" / "+TextCatalog.Get("Decrease")+": −"+TextCatalog.Number(b.Decrease*s.Factor,2));tips.Add(TextCatalog.Get("Net")+": "+TextCatalog.Number((b.Increase-b.Decrease)*s.Factor,2));if(b.Adjustment!=0)tips.Add(TextCatalog.Get("Adjustment",TextCatalog.Number(b.Adjustment*s.Factor,2)));}
     if(b.Partial)tips.Add(TextCatalog.Get("PartialBucket"));
    }
    TooltipHandler.TipRegion(plot,string.Join("\n",tips));
   }
  }
  Widgets.Label(new Rect(plot.x,plot.yMax+8,180,25),TextCatalog.Day(from));Text.Anchor=TextAnchor.UpperRight;Widgets.Label(new Rect(plot.xMax-180,plot.yMax+8,180,25),TextCatalog.Day(to));Text.Anchor=TextAnchor.UpperLeft;
 }
 private static int Decimals(double step)=>Math.Clamp((int)Math.Ceiling(-Math.Log10(step)),0,6);
 private static string AxisNumber(double value,double step)=>Math.Abs(value)>=1e6?TextCatalog.Number(value/1e6,1)+"M":Math.Abs(value)>=1e4?TextCatalog.Number(value/1000,1)+"k":TextCatalog.Number(value,Decimals(step));
 private static void Segment(Vector2 a,Vector2 b,Color color,int style){
  if(style==0){Widgets.DrawLine(a,b,color,2);return;}
  float dx=b.x-a.x,dy=b.y-a.y,length=MathF.Sqrt(dx*dx+dy*dy);int count=Math.Max(1,(int)Math.Ceiling(length/(style==1?10:6)));
  for(int i=0;i<count;i++){float start=i/(float)count,end=Math.Min(1,(i+(style==1?.6f:.25f))/count);Widgets.DrawLine(new Vector2(a.x+dx*start,a.y+dy*start),new Vector2(a.x+dx*end,a.y+dy*end),color,1.7f);}
 }
}
