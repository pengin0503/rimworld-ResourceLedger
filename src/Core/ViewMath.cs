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
  var result=new Series{Resource=all[0].Resource,Map="all",Scope=all[0].Scope};
  var samples=all.Select(s=>s.Samples.ToDictionary(p=>p.Tick)).ToList();
  var ticks=samples[0].Keys.ToHashSet();foreach(var index in samples.Skip(1))ticks.IntersectWith(index.Keys);
  foreach(var tick in ticks.Order())result.Samples.Add(new Sample{Tick=tick,Count=samples.Sum(index=>index[tick].Count),Gap=samples.Any(index=>index[tick].Gap)});
  var cursor=new int[all.Count];
  for(int i=1;i<result.Samples.Count;i++){
   var a=result.Samples[i-1];var b=result.Samples[i];var merged=new Period{From=a.Tick,To=b.Tick,Start=a.Count,End=b.Count};
   for(int source=0;source<all.Count;source++){
    var periods=all[source].Periods;
    while(cursor[source]<periods.Count&&periods[cursor[source]].To<=a.Tick)cursor[source]++;
    long next=a.Tick;
    while(cursor[source]<periods.Count&&periods[cursor[source]].To<=b.Tick){
     var p=periods[cursor[source]++];if(p.From!=next||p.Gap)merged.Gap=true;next=p.To;merged.Adjustment+=p.Adjustment;
     foreach(var f in p.Flows){Ledger.AddFlow(merged,f.Cause,f.In);Ledger.AddFlow(merged,f.Cause,-f.Out);}
    }
    if(next!=b.Tick)merged.Gap=true;
   }
   result.Periods.Add(merged);b.Gap|=merged.Gap;
  }return result;
 }

 public static List<Period> Bars(Series s,long from,long to){
  var result=new List<Period>();
  foreach(var g in s.Periods.Where(p=>p.From>=from&&p.To<=to).GroupBy(p=>(p.To-1)/60000)){
   var rows=g.OrderBy(p=>p.From).ToList();result.Add(new Period{From=rows[0].From,To=rows[^1].To,Start=rows[0].Start,End=rows[^1].End,In=rows.Sum(p=>p.In),Out=rows.Sum(p=>p.Out),Gap=rows.Any(p=>p.Gap)});
  }return result;
 }
}
