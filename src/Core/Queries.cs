namespace ResourceLedger.Core;
public static class Queries {
 public static Summary Summarize(Series s,long from,long to){
  var samples=s.Samples.Where(x=>x.Tick>=from&&x.Tick<=to).OrderBy(x=>x.Tick).ToList();if(samples.Count==0)return new Summary();
  var first=samples[0];var last=samples[^1];var r=new Summary{From=first.Tick,To=last.Tick,Start=first.Count,End=last.Count,Continuous=samples.Count>1};long cursor=first.Tick;
  foreach(var p in s.Periods.Where(p=>p.From>=first.Tick&&p.To<=last.Tick).OrderBy(p=>p.From)){
   if(p.Gap||p.From!=cursor)r.Continuous=false;cursor=p.To;r.Increase+=p.In;r.Decrease+=p.Out;r.Adjustment+=p.Adjustment;
   foreach(var flow in p.Flows){var f=r.Flows.FirstOrDefault(x=>x.Cause==flow.Cause);if(f==null)r.Flows.Add(f=new Flow{Cause=flow.Cause});f.In+=flow.In;f.Out+=flow.Out;}
  }
  if(cursor!=last.Tick)r.Continuous=false;return r;
 }
 public static Forecast Forecast(Series s,long now,int days){
  var f=new Forecast();var summary=Summarize(s,Math.Max(0,now-Math.Clamp(days,1,600)*60000L),now);f.ObservedDays=(summary.To-summary.From)/60000d;
  if(s.PendingGap||!summary.Continuous||f.ObservedDays<1||now-summary.To>60000)return f;
  if(summary.Net>=0){f.Status="NotDeclining";return f;}f.Available=true;f.Status="Estimate";f.Days=summary.End/-summary.DailyMean;return f;
 }
 public static Summary Combine(IEnumerable<Summary> summaries){
  var list=summaries.ToList();if(list.Count==0)return new Summary();var r=new Summary{From=list[0].From,To=list[0].To,Continuous=list.All(x=>x.Continuous&&x.From==list[0].From&&x.To==list[0].To)};
  r.Start=list.Sum(x=>x.Start);r.End=list.Sum(x=>x.End);r.Increase=list.Sum(x=>x.Increase);r.Decrease=list.Sum(x=>x.Decrease);r.Adjustment=list.Sum(x=>x.Adjustment);
  foreach(var g in list.SelectMany(x=>x.Flows).GroupBy(x=>x.Cause))r.Flows.Add(new Flow{Cause=g.Key,In=g.Sum(x=>x.In),Out=g.Sum(x=>x.Out)});return r;
 }
}
public static class OperationDiff {
 public static List<(string Resource,long In,long Out)> Compute(IReadOnlyDictionary<string,long> before,IReadOnlyDictionary<string,long> after,IReadOnlyDictionary<string,long> consumed){
  var result=new List<(string,long,long)>();foreach(var resource in before.Keys.Concat(after.Keys).Concat(consumed.Keys).Distinct()){
   long delta=after.GetValueOrDefault(resource)-before.GetValueOrDefault(resource),removed=Math.Max(0,consumed.GetValueOrDefault(resource)),gained=delta+removed;
   if(gained<0){removed-=gained;gained=0;}if(gained!=0||removed!=0)result.Add((resource,gained,removed));
  }return result;
 }
}
