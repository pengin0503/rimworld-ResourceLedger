namespace ResourceLedger.Core;
public record PlotPoint(long Tick,double Y,bool Gap);
public static class ViewMath {
 public static List<PlotPoint> Points(Series s,long from,long to,double factor,bool relative){
  var points=s.Samples.Where(p=>p.Tick>=from&&p.Tick<=to).OrderBy(p=>p.Tick).ToList();
  var baseline=points.FirstOrDefault()?.Count??0;
  return points.Select(p=>new PlotPoint(p.Tick,relative?(baseline==0?double.NaN:(p.Count-baseline)*100d/baseline):p.Count*factor,p.Gap)).ToList();
 }
 public static Series Merge(IEnumerable<Series> sources){
  var all=sources.ToList();if(all.Count==0)return new Series();if(all.Count==1)return all[0];
  var result=new Series{Resource=all[0].Resource,Map="all",Scope=all[0].Scope,PendingGap=all.Any(s=>s.PendingGap)};
  // A tick is usable when every map has a value there: a recorded sample, or any tick inside an unchanged period.
  var ticks=new SortedSet<long>(all.SelectMany(s=>s.Samples.Select(p=>p.Tick)));
  foreach(var tick in ticks){
   long sum=0;bool gap=false,known=true;
   foreach(var s in all){if(ValueAt(s,tick) is {} v){sum+=v.Count;gap|=v.Gap;}else{known=false;break;}}
   if(known)result.Samples.Add(new Sample{Tick=tick,Count=sum,Gap=gap});
  }
  for(int i=1;i<result.Samples.Count;i++){
   var a=result.Samples[i-1];var b=result.Samples[i];var merged=new Period{From=a.Tick,To=b.Tick,Start=a.Count,End=b.Count};
   foreach(var s in all){
    long next=a.Tick;
    for(int k=FirstEndingAfter(s.Periods,a.Tick);k<s.Periods.Count&&s.Periods[k].From<b.Tick;k++){
     var p=s.Periods[k];long from=Math.Max(p.From,a.Tick),to=Math.Min(p.To,b.Tick);
     if(from!=next||p.Gap||(from!=p.From||to!=p.To)&&!p.Flat)merged.Gap=true;next=to;
     if(from==p.From&&to==p.To){merged.Adjustment+=p.Adjustment;foreach(var f in p.Flows){Ledger.AddFlow(merged,f.Cause,f.In);Ledger.AddFlow(merged,f.Cause,-f.Out);}}
    }
    if(next!=b.Tick)merged.Gap=true;
   }
   result.Periods.Add(merged);b.Gap|=merged.Gap;
  }return result;
 }
 private static (long Count,bool Gap)? ValueAt(Series s,long tick){
  int lo=0,hi=s.Samples.Count-1;
  while(lo<=hi){int mid=(lo+hi)/2;long t=s.Samples[mid].Tick;if(t==tick)return (s.Samples[mid].Count,s.Samples[mid].Gap);if(t<tick)lo=mid+1;else hi=mid-1;}
  int k=FirstEndingAfter(s.Periods,tick);
  return k<s.Periods.Count&&s.Periods[k].From<tick&&s.Periods[k].Flat?(s.Periods[k].Start,false):null;
 }
 /// <summary>Index of the first period whose end is after <paramref name="tick"/>; periods are ordered by time.</summary>
 private static int FirstEndingAfter(List<Period> periods,long tick){
  int lo=0,hi=periods.Count;
  while(lo<hi){int mid=(lo+hi)/2;if(periods[mid].To<=tick)lo=mid+1;else hi=mid;}
  return lo;
 }

 public static List<Period> Bars(Series s,long from,long to){
  var result=new List<Period>();
  foreach(var g in PeriodMath.SplitFlat(s.Periods,60000,from,to).GroupBy(p=>(p.To-1)/60000)){
   var rows=g.OrderBy(p=>p.From).ToList();result.Add(new Period{From=rows[0].From,To=rows[^1].To,Start=rows[0].Start,End=rows[^1].End,In=rows.Sum(p=>p.In),Out=rows.Sum(p=>p.Out),Adjustment=rows.Sum(p=>p.Adjustment),Gap=rows.Any(p=>p.Gap)||rows.Zip(rows.Skip(1),(a,b)=>a.To!=b.From).Any(x=>x)});
  }return result;
 }
}
