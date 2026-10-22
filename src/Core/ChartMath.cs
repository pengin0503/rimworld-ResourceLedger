namespace ResourceLedger.Core;

public record AxisScale(double Min,double Max,double Step) {
 public IEnumerable<double> Ticks {
  get {int count=(int)Math.Round((Max-Min)/Step);for(int i=0;i<=Math.Min(count,20);i++)yield return Min+i*Step;}
 }
}
public record ChartBucket(long From,long To,long Stock,long Increase,long Decrease,long Adjustment,bool Gap,bool Partial);
public static class ChartMath {
 public static AxisScale Scale(double min,double max,int intervals=4){
  if(!double.IsFinite(min)||!double.IsFinite(max))return new(0,1,.25);
  min=Math.Min(0,min);max=Math.Max(0,max);
  if(max<=min)max=min+1;
  double raw=(max-min)/Math.Clamp(intervals,2,10);
  double magnitude=Math.Pow(10,Math.Floor(Math.Log10(raw)));
  double normalized=raw/magnitude;
  double step=(normalized<=1?1:normalized<=2?2:normalized<=5?5:10)*magnitude;
  return new(Math.Floor(min/step)*step,Math.Ceiling(max/step)*step,step);
 }
 public static List<ChartBucket> Buckets(Series series,long from,long to,long interval){
  interval=Math.Max(1,interval);var result=new List<ChartBucket>();
  foreach(var group in series.Periods.Where(p=>p.From>=from&&p.To<=to&&p.To>p.From).GroupBy(p=>(p.To-1)/interval).OrderBy(g=>g.Key)){
   var rows=group.OrderBy(p=>p.From).ToList();var first=rows[0];var last=rows[^1];
   bool gap=rows.Any(p=>p.Gap)||rows.Zip(rows.Skip(1),(a,b)=>a.To!=b.From).Any(x=>x);
   bool partial=first.From!=group.Key*interval||last.To!=(group.Key+1)*interval;
   result.Add(new(first.From,last.To,last.End,rows.Sum(p=>p.In),rows.Sum(p=>p.Out),rows.Sum(p=>p.Adjustment),gap,partial));
  }
  if(result.Count==0){
   var point=series.Samples.LastOrDefault(s=>s.Tick>=from&&s.Tick<=to);
   if(point!=null)result.Add(new(point.Tick,point.Tick,point.Count,0,0,0,true,true));
  }return result;
 }
}
public enum ResourceVisibility {Owned,Changed,History}
public static class ResourceSelection {
 public static bool Matches(ResourceVisibility visibility,long? current,Summary summary)=>visibility switch {
  ResourceVisibility.Owned=>current>0,
  ResourceVisibility.Changed=>summary.Continuous&&(summary.Increase>0||summary.Decrease>0),
  _=>true
 };
}
