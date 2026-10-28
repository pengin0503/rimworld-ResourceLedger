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
 public TrackedList Tracked=new();
 public List<string> Favorites=new(),ActiveMaps=new();
 public List<AlertRule> Alerts=new();public List<ScopeDefinition> Scopes=new();
 private Dictionary<string,LedgerEvent>? eventIndex;
 /// <summary>Resources already known to have a series in a map and scope. Series are only ever added: retention trims periods, and a load replaces the whole ledger.</summary>
 private readonly Dictionary<(string Map,string Scope),HashSet<string>> baselined=new();
 private HashSet<string>? trackedSet;private TrackedList? trackedSource;private int trackedVersion=-1;
 public static string Key(string map,string scope,string resource)=>map+"|"+scope+"|"+resource;
 public void MarkGap(string map){foreach(var series in Series.Values.Where(s=>s.Map==map))series.PendingGap=true;}
 public void EnsureBaseline(string map,string scope,long tick,IReadOnlyDictionary<string,long> counts,IEnumerable<string> resources,bool historical=false){
  EnsureBaselineLazy(map,scope,tick,()=>counts,resources,historical);
 }
 public void EnsureBaselineLazy(string map,string scope,long tick,Func<IReadOnlyDictionary<string,long>> counts,IEnumerable<string> resources,bool historical=false){
  // Captures check the same resources several times per operation. A key built from a name is only needed the first time a resource is seen in a map and scope.
  if(!baselined.TryGetValue((map,scope),out var known))baselined[(map,scope)]=known=new HashSet<string>(StringComparer.Ordinal);
  List<string>? missing=null;
  foreach(var id in resources){
   if(known.Contains(id))continue;
   if(Series.ContainsKey(Key(map,scope,id)))known.Add(id);else(missing??=new()).Add(id);
  }
  if(missing==null)return;
  if(historical&&scope!="map"&&scope!="stored"&&!Series.Values.Any(s=>s.Map==map&&s.Scope==scope&&s.Samples.Any(p=>p.Tick<=tick)))return;
  var distinct=missing.Distinct().ToList();var current=counts();
  Observe(map,scope,tick,distinct.ToDictionary(id=>id,id=>current.GetValueOrDefault(id)));
  foreach(var id in distinct)known.Add(id);
 }
 /// <summary>Lookup for <see cref="Tracked"/>, rebuilt whenever the list object or its <see cref="TrackedList.Version"/> changed.</summary>
 private HashSet<string> TrackedSet(){
  if(trackedSet==null||!ReferenceEquals(trackedSource,Tracked)||trackedVersion!=Tracked.Version){trackedSet=Tracked.ToHashSet(StringComparer.Ordinal);trackedSource=Tracked;trackedVersion=Tracked.Version;}
  return trackedSet;
 }
 public bool IsTracked(string resource)=>TrackedSet().Contains(resource);
 public List<string> Discover(IEnumerable<string> resources){
  var known=TrackedSet();var added=new List<string>();
  foreach(var id in resources)if(!string.IsNullOrEmpty(id)&&known.Add(id)){Tracked.Add(id);added.Add(id);}
  // The set already holds what was added above, so it stays valid for the new version.
  trackedVersion=Tracked.Version;return added;
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
   s.Pending.RemoveAll(e=>e.Tick<=tick);
   // Unchanged stock extends the previous unchanged period instead of storing a new one every sample.
   var last=s.Periods.Count>0?s.Periods[^1]:null;
   if(p.Flat&&last!=null&&last.Flat&&last.To==prev.Tick&&s.Samples.Count==s.Periods.Count+1){last.To=tick;prev.Tick=tick;continue;}
   s.Periods.Add(p);s.Samples.Add(new Sample{Tick=tick,Count=count,Gap=p.Gap});
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
 public const long TicksPerDay=60000;
 /// <summary>
 /// Rolls hourly periods older than the hourly window into one period per day and drops periods older than the daily
 /// window. Only periods that crossed the hourly cutoff since the last call are touched, so the cost does not grow with
 /// the stored history. Periods and samples stay aligned: Samples[0] starts Periods[0] and Samples[i+1] ends Periods[i].
 /// </summary>
 public void Trim(long now,Retention policy){
  var hourlyCutoff=now-Math.Max(1,policy.HourlyDays)*TicksPerDay;var dailyCutoff=now-Math.Max(policy.HourlyDays,policy.DailyDays)*TicksPerDay;
  foreach(var s in Series.Values){
   if(s.Periods.Count>0&&s.Samples.Count!=s.Periods.Count+1)AlignSamples(s);
   int i=0;while(i<s.Periods.Count&&s.Periods[i].Daily)i++;
   while(i<s.Periods.Count&&s.Periods[i].To<=hourlyCutoff){
    var p=s.Periods[i];long dayStart=(p.To-1)/TicksPerDay*TicksPerDay;
    if(p.Flat&&p.From<dayStart){
     // Exact split: the earlier days stay one unchanged period, the rest joins its day.
     s.Periods.Insert(i,new Period{From=p.From,To=dayStart,Start=p.Start,End=p.Start,Daily=true});s.Samples.Insert(i+1,new Sample{Tick=dayStart,Count=p.Start});
     p.From=dayStart;i++;
    }
    var day=i>0?s.Periods[i-1]:null;
    if(day!=null&&day.Daily&&(day.To-1)/TicksPerDay==(p.To-1)/TicksPerDay){
     day.Gap|=p.Gap||day.To!=p.From;day.To=p.To;day.End=p.End;day.Adjustment+=p.Adjustment;
     foreach(var f in p.Flows){AddFlow(day,f.Cause,f.In);AddFlow(day,f.Cause,-f.Out);}
     s.Periods.RemoveAt(i);s.Samples.RemoveAt(i);s.Samples[i].Gap=day.Gap;
    }
    else{p.Daily=true;i++;}
   }
   int drop=0;while(drop<s.Periods.Count&&s.Periods[drop].To<dailyCutoff)drop++;
   if(drop>0){s.Periods.RemoveRange(0,drop);s.Samples.RemoveRange(0,drop);}
   if(s.Periods.Count==0&&s.Samples.Count>1)s.Samples.RemoveRange(0,s.Samples.Count-1);
  }
  var cutoff=now-Math.Max(0,policy.DetailDays)*60000L;Events.RemoveAll(e=>e.Tick<cutoff);var cap=Math.Max(0,policy.DetailLimit);if(Events.Count>cap)Events.RemoveRange(0,Events.Count-cap);
  foreach(var key in SeenIds.Where(x=>x.Value<cutoff).Select(x=>x.Key).ToArray())SeenIds.Remove(key);
  eventIndex=null;
 }
 /// <summary>Rebuilds samples from periods for histories saved before periods and samples were kept aligned.</summary>
 private static void AlignSamples(Series s){
  s.Periods=s.Periods.OrderBy(p=>p.From).ToList();
  s.Samples=new(){new Sample{Tick=s.Periods[0].From,Count=s.Periods[0].Start}};s.Samples.AddRange(s.Periods.Select(p=>new Sample{Tick=p.To,Count=p.End,Gap=p.Gap}));
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
