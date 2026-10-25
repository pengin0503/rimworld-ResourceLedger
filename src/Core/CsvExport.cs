using System.Globalization;
using System.Text;
namespace ResourceLedger.Core;
public static class CsvExport {
 public static string Cell(string value){if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return value.IndexOfAny(new[]{',','"','\r','\n'})>=0?"\""+value.Replace("\"","\"\"")+"\"":value;}
 public static string Number(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
 public static string Row(params string[] cells)=>string.Join(",",cells.Select(Cell));
 public static string Export(Ledger ledger,IEnumerable<Series> sources,long from,long to,string kind,Func<string,string>? name=null,IReadOnlyDictionary<string,double>? factors=null,string unit="count"){
  name??=r=>ledger.Names.GetValueOrDefault(r,r);var b=new StringBuilder();b.AppendLine("map,scope,resource_id,resource_name,unit,from_tick,to_tick,start,end,increase,decrease,net,continuous,cause,actor,facility");
  foreach(var s in sources){
   if(Queries.KnownRange(s,from,to)==null)continue;double factor=factors?.GetValueOrDefault(s.Resource)??1;
   string Prefix()=>Row(ledger.MapNames.GetValueOrDefault(s.Map,s.Map),s.Scope,s.Resource,name(s.Resource),unit);
   void Emit(long f,long t,long start,long end,long inc,long dec,bool continuous,string cause="",string actor="",string facility="")=>b.AppendLine(Prefix()+","+string.Join(",",new[]{f.ToString(CultureInfo.InvariantCulture),t.ToString(CultureInfo.InvariantCulture),Number(start*factor),Number(end*factor),continuous?Number(inc*factor):"",continuous?Number(dec*factor):"",continuous?Number((end-start)*factor):"",continuous?"true":"false"})+","+Row(cause,actor,facility));
   var sum=Queries.Summarize(s,from,to);
   if(kind=="summary")Emit(sum.From,sum.To,sum.Start,sum.End,sum.Increase,sum.Decrease,sum.Continuous);
   else if(kind=="causes")foreach(var f in sum.Flows)Emit(sum.From,sum.To,0,f.In-f.Out,f.In,f.Out,sum.Continuous,f.Cause.ToString());
   else if(kind=="events")foreach(var e in ledger.Events.Where(e=>e.Map==s.Map&&e.Scope==s.Scope&&e.Resource==s.Resource&&e.ClosedAt>sum.From&&e.ClosedAt<=sum.To))Emit(e.Tick,e.Tick,0,e.Delta,Math.Max(0,e.Delta),Math.Max(0,-e.Delta),true,e.Cause.ToString(),e.Actor,e.Facility);
   // Unchanged periods are clipped to the range (one row each); periods with flows are listed only when wholly inside.
   else foreach(var p in PeriodMath.SplitFlat(s.Periods,long.MaxValue,sum.From,sum.To))Emit(p.From,p.To,p.Start,p.End,p.In,p.Out,!p.Gap);
  }return b.ToString();
 }
}
