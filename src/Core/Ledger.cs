using System.Text.Json;
using System.IO.Compression;
using System.Text;
namespace ResourceLedger.Core;
public class Ledger {
 public int Schema=1;public bool DefaultsSeeded;
 public Dictionary<string,Series> Series=new();
 public Dictionary<string,string> Names=new(),MapNames=new(),ResourceMods=new(),Topology=new();
 public Dictionary<string,double> NutritionFactors=new(),ValueFactors=new();
 public Dictionary<string,long> SeenIds=new();
 public List<LedgerEvent> Events=new();
 public List<string> Tracked=new(),Favorites=new(),ActiveMaps=new();
 public List<AlertRule> Alerts=new();public List<ScopeDefinition> Scopes=new();
 private Dictionary<string,LedgerEvent>? eventIndex;
 public static string Key(string map,string scope,string resource)=>map+"|"+scope+"|"+resource;
 public void MarkGap(string map){foreach(var series in Series.Values.Where(s=>s.Map==map))series.PendingGap=true;}
 public void EnsureBaseline(string map,string scope,long tick,IReadOnlyDictionary<string,long> counts,IEnumerable<string> resources,bool historical=false){
  if(historical&&scope!="map"&&scope!="stored"&&!Series.Values.Any(s=>s.Map==map&&s.Scope==scope&&s.Samples.Any(p=>p.Tick<=tick)))return;
  var missing=resources.Distinct().Where(id=>!Series.ContainsKey(Key(map,scope,id))).ToDictionary(id=>id,id=>counts.GetValueOrDefault(id));
  if(missing.Count>0)Observe(map,scope,tick,missing);
 }
 public List<string> Discover(IEnumerable<string> resources){
  var known=Tracked.ToHashSet(StringComparer.Ordinal);var added=new List<string>();
  foreach(var id in resources)if(!string.IsNullOrEmpty(id)&&known.Add(id)){Tracked.Add(id);added.Add(id);}
  return added;
 }
 public void Observe(string map,string scope,long tick,IReadOnlyDictionary<string,long> counts,bool discontinuity=false){
  foreach(var (resource,count) in counts){
   var key=Key(map,scope,resource);
   if(!Series.TryGetValue(key,out var s))Series[key]=s=new Series{Map=map,Scope=scope,Resource=resource};
   var prev=s.Samples.LastOrDefault();
   if(prev==null){s.Samples.Add(new Sample{Tick=tick,Count=count});s.Pending.Clear();continue;}
   if(tick<=prev.Tick){s.PendingGap|=discontinuity;continue;}
   var p=new Period{From=prev.Tick,To=tick,Start=prev.Count,End=count,Gap=discontinuity||s.PendingGap};s.PendingGap=false;
   if(!p.Gap){
    eventIndex??=Events.Where(e=>e.Id.Length>0).GroupBy(e=>e.Id).ToDictionary(g=>g.Key,g=>g.Last());
    foreach(var e in s.Pending.Where(e=>e.Tick<=tick)){AddFlow(p,e.Cause,e.Delta);e.ClosedAt=tick;if(eventIndex.TryGetValue(e.Id,out var detail))detail.ClosedAt=tick;}
    p.Adjustment=count-prev.Count-(p.In-p.Out);
    if(p.Adjustment!=0){AddFlow(p,Cause.Unclassified,p.Adjustment);var adjustment=new LedgerEvent{Id="adjust:"+key+":"+tick,Map=map,Scope=scope,Resource=resource,Tick=tick,ClosedAt=tick,Delta=p.Adjustment,Cause=Cause.Unclassified};Events.Add(adjustment);eventIndex[adjustment.Id]=adjustment;}
   }
   s.Pending.RemoveAll(e=>e.Tick<=tick);s.Periods.Add(p);s.Samples.Add(new Sample{Tick=tick,Count=count,Gap=p.Gap});
  }
 }
 public void Record(LedgerEvent e){
  if(e.Id.Length==0)e.Id=Guid.NewGuid().ToString("N");
  if(e.Delta==0||(e.Id.Length>0&&SeenIds.ContainsKey(e.Id)))return;
  if(!Series.TryGetValue(Key(e.Map,e.Scope,e.Resource),out var s)||s.Samples.Count==0||e.Tick<s.Samples[^1].Tick)return;
  if(e.Id.Length>0)SeenIds[e.Id]=e.Tick;s.Pending.Add(e);Events.Add(e);if(eventIndex!=null)eventIndex[e.Id]=e;
 }
 public static void AddFlow(Period p,Cause cause,long delta){
  var f=p.Flows.FirstOrDefault(f=>f.Cause==cause);if(f==null)p.Flows.Add(f=new Flow{Cause=cause});
  if(delta>0){f.In+=delta;p.In+=delta;}else{f.Out-=delta;p.Out-=delta;}
 }
 public void Trim(long now,Retention policy){
  var hourlyCutoff=now-Math.Max(1,policy.HourlyDays)*60000L;var dailyCutoff=now-Math.Max(policy.HourlyDays,policy.DailyDays)*60000L;
  foreach(var s in Series.Values){
   var rolled=new List<Period>();
   foreach(var group in s.Periods.Where(p=>p.To<=hourlyCutoff&&p.To>=dailyCutoff).GroupBy(p=>(p.To-1)/60000)){
    var ordered=group.OrderBy(p=>p.From).ToList();var p=new Period{From=ordered[0].From,To=ordered[^1].To,Start=ordered[0].Start,End=ordered[^1].End,Gap=ordered.Any(x=>x.Gap)||ordered.Zip(ordered.Skip(1),(a,b)=>a.To!=b.From).Any(x=>x),Adjustment=ordered.Sum(x=>x.Adjustment)};
    foreach(var source in ordered)foreach(var f in source.Flows){AddFlow(p,f.Cause,f.In);AddFlow(p,f.Cause,-f.Out);}rolled.Add(p);
   }
   s.Periods=rolled.Concat(s.Periods.Where(p=>p.To>hourlyCutoff)).OrderBy(p=>p.From).ToList();
   if(s.Periods.Count>0){s.Samples=new(){new Sample{Tick=s.Periods[0].From,Count=s.Periods[0].Start}};s.Samples.AddRange(s.Periods.Select(p=>new Sample{Tick=p.To,Count=p.End,Gap=p.Gap}));}else if(s.Samples.Count>1)s.Samples=new(){s.Samples[^1]};
  }
  var cutoff=now-Math.Max(0,policy.DetailDays)*60000L;Events.RemoveAll(e=>e.Tick<cutoff);var cap=Math.Max(0,policy.DetailLimit);if(Events.Count>cap)Events.RemoveRange(0,Events.Count-cap);
  foreach(var key in SeenIds.Where(x=>x.Value<cutoff).Select(x=>x.Key).ToArray())SeenIds.Remove(key);
  eventIndex=null;
 }
}
public static class LedgerSerializer {
 public static readonly JsonSerializerOptions Options=new(){IncludeFields=true,IgnoreReadOnlyProperties=true};
 public static string Save(Ledger ledger){
  var bytes=JsonSerializer.SerializeToUtf8Bytes(ledger,Options);
  using var output=new MemoryStream();
  using(var gzip=new GZipStream(output,CompressionLevel.Fastest,true))gzip.Write(bytes);
  return "RLZ1:"+Convert.ToBase64String(output.ToArray());
 }
 private static string Unpack(string payload){
  using var input=new MemoryStream(Convert.FromBase64String(payload[5..]));
  using var gzip=new GZipStream(input,CompressionMode.Decompress);
  using var reader=new StreamReader(gzip,Encoding.UTF8);return reader.ReadToEnd();
 }
 public static bool TryLoad(string json,out Ledger? ledger){
  ledger=null;
  try{
   if(json.StartsWith("RLZ1:",StringComparison.Ordinal))json=Unpack(json);
   using var doc=JsonDocument.Parse(json);
   if(!doc.RootElement.TryGetProperty("Schema",out var v)||v.GetInt32()!=1)return false;
   var candidate=JsonSerializer.Deserialize<Ledger>(json,Options);
   if(!SaveValidation.Valid(candidate))return false;
   ledger=candidate;return true;
  }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(JsonException){return false;}catch(FormatException){return false;}catch(InvalidOperationException){return false;}catch(OverflowException){return false;}
 }
}
