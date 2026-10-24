using System.Diagnostics;
using System.Text;
using ResourceLedger.Core;
using ResourceLedger.UI;
using RimWorld;
using UnityEngine;
using Verse;
namespace ResourceLedger.Game;

public sealed class LedgerGameComponent : GameComponent {
 public Ledger Data=new();
 public string? PreservedPayload;
 public bool ReadOnly;
 public string Error="";
 public Dictionary<string,InventorySnapshot> Live=new();
 public int Revision;
 public long LastSampleTick=-1;
 public double LastSampleMs,OperationMs;public long OperationCount;
 private string payload="";
 private long nextTrim;
 private HashSet<string> previousMaps=new();
 private bool membershipKnown;
 public static LedgerGameComponent? Instance=>Current.Game?.GetComponent<LedgerGameComponent>();
 public LedgerGameComponent(Verse.Game game){}
 public IEnumerable<Map> Maps=>Find.Maps.Where(m=>m.IsPlayerHome);
 private HashSet<string>? resources;private Ledger? resourcesData;private int resourcesCount=-1;
 /// <summary>Tracked types with a loaded definition. Cached: tracking only ever grows, and a load replaces <see cref="Data"/>.</summary>
 public HashSet<string> Resources{get{
  if(resources==null||!ReferenceEquals(resourcesData,Data)||resourcesCount!=Data.Tracked.Count){
   resources=Data.Tracked.Where(id=>DefDatabase<ThingDef>.GetNamedSilentFail(id)!=null).ToHashSet(StringComparer.Ordinal);resourcesData=Data;resourcesCount=Data.Tracked.Count;
  }
  return resources;
 }}
 /// <summary>Changes when a capture updated the live stock; summaries only change with <see cref="Revision"/>.</summary>
 public int LiveRevision;
 /// <summary>Size of the ledger string written by the last save or read by the last load; -1 before either.</summary>
 public int LastPayloadLength=-1;
 public IEnumerable<string> ScopeIds(Map map)=>new[]{"map","stored"}.Concat(Data.Scopes.Where(s=>s.Map==InventoryReader.MapId(map)).Select(s=>s.Id));
 public Dictionary<string,long> Project(InventorySnapshot snapshot,string scope){
  var definition=Data.Scopes.FirstOrDefault(s=>s.Id==scope);
  return snapshot.Project(scope,definition==null?null:new ScopeDefinitionAdapter{Groups=definition.Groups.ToHashSet()});
 }
 public void Discover(InventorySnapshot snapshot){
  Data.DefaultsSeeded=true;
  if(Data.Discover(snapshot.Counts.Where(p=>p.Value>0).Select(p=>p.Key)).Count>0){UpdateNames();Revision++;}
 }
 public void UpdateNames(){
  foreach(var id in Data.Tracked){var d=DefDatabase<ThingDef>.GetNamedSilentFail(id);if(d!=null){Data.Names[id]=d.LabelCap;Data.ResourceMods[id]=d.modContentPack?.Name??"Core";Data.NutritionFactors[id]=d.ingestible?.CachedNutrition??0;Data.ValueFactors[id]=d.BaseMarketValue;}}
 }
 public void EnsureBaseline(Map map,InventorySnapshot snapshot,bool historical=false){
  if(snapshot.UnsupportedHolders>0)return;
  var id=InventoryReader.MapId(map);Data.MapNames[id]=map.Parent?.LabelCap??("Map "+id);
  var resources=Resources;
  foreach(var scope in ScopeIds(map))Data.EnsureBaseline(id,scope,snapshot.Tick,Project(snapshot,scope),resources,historical);
 }
 public void Invalidate(string map){
  Data.MarkGap(map);
  if(Live.TryGetValue(map,out var snapshot))snapshot.UnsupportedHolders=Math.Max(1,snapshot.UnsupportedHolders);
  Revision++;
 }
 public void SampleAll(bool force=false){
  if(ReadOnly||Current.ProgramState!=ProgramState.Playing)return;
  try {
   var tick=Find.TickManager.TicksGame;var settings=LedgerMod.Settings;
   int interval=Math.Clamp(settings.IntervalHours,1,24)*2500;
   if(!force&&LastSampleTick>=0&&tick-LastSampleTick<interval)return;
   UpdateNames();
   var watch=Stopwatch.StartNew();var maps=Maps.ToList();var ids=maps.Select(InventoryReader.MapId).ToHashSet();
   bool changed=membershipKnown&&!ids.SetEquals(previousMaps);
   var previous=maps.ToDictionary(InventoryReader.MapId,m=>Live.GetValueOrDefault(InventoryReader.MapId(m)));
   var known=Resources;
   foreach(var map in maps){
    var snapshot=InventoryReader.Read(map,known);Live[InventoryReader.MapId(map)]=snapshot;
    if(snapshot.UnsupportedHolders==0)Discover(snapshot);else Invalidate(InventoryReader.MapId(map));
   }
   known=Resources;
   foreach(var map in maps){
    var id=InventoryReader.MapId(map);var snapshot=Live[id];if(snapshot.UnsupportedHolders>0)continue;
    foreach(var resource in known)snapshot.Counts.TryAdd(resource,0);
    if(previous[id] is {UnsupportedHolders:0} oldSnapshot)EnsureBaseline(map,oldSnapshot,true);
    EnsureBaseline(map,snapshot);
    foreach(var scope in ScopeIds(map)){
     string topologyKey=id+"|"+scope;string topology=ScopeSignature(map,scope);
     bool scopeChanged=Data.Topology.TryGetValue(topologyKey,out var oldTopology)&&oldTopology!=topology;
     Data.Topology[topologyKey]=topology;
     foreach(var count in Project(snapshot,scope)){
      var last=Data.Series.GetValueOrDefault(Ledger.Key(id,scope,count.Key))?.Samples.LastOrDefault();
      bool gap=changed||scopeChanged||(last!=null&&tick-last.Tick>interval*2L);
      Data.Observe(id,scope,tick,new Dictionary<string,long>{{count.Key,count.Value}},gap);
     }
    }
   }
   foreach(var stale in Live.Keys.Where(k=>!ids.Contains(k)).ToArray())Live.Remove(stale);
   previousMaps=ids;Data.ActiveMaps=ids.ToList();membershipKnown=true;LastSampleTick=tick;Revision++;
   if(tick>=nextTrim){Data.Trim(tick,new Retention{HourlyDays=settings.HourlyDays,DailyDays=settings.DailyDays,DetailDays=settings.DetailDays,DetailLimit=settings.DetailLimit});nextTrim=tick+2500;}
   EvaluateAlerts(tick);watch.Stop();LastSampleMs=watch.Elapsed.TotalMilliseconds;
  }catch(Exception ex){foreach(var map in Maps)Invalidate(InventoryReader.MapId(map));Report(ex);}
 }
 public override void GameComponentTick()=>SampleAll();
 public override void GameComponentOnGUI(){
  // Skip while a text field has focus: KeyDownEvent only ignores the quick search box, so a bound letter would be eaten while typing.
  if(Current.ProgramState==ProgramState.Playing&&GUIUtility.keyboardControl==0&&DefDatabase<KeyBindingDef>.GetNamedSilentFail("ResourceLedger_Open")?.KeyDownEvent==true){
   var button=DefDatabase<MainButtonDef>.GetNamedSilentFail("ResourceLedger");
   if(button!=null)Find.MainTabsRoot.ToggleTab(button);
  }
 }
 public override void ExposeData(){
  if(Scribe.mode==LoadSaveMode.Saving){SampleAll(true);payload=ReadOnly?PreservedPayload??payload:LedgerSerializer.Save(Data);LastPayloadLength=payload.Length;}
  Scribe_Values.Look(ref payload,"resourceLedgerJson","");
  if(Scribe.mode==LoadSaveMode.LoadingVars&&payload.Length>0){
   LastPayloadLength=payload.Length;
   if(LedgerSerializer.TryLoad(payload,out var loaded)){Data=loaded!;LastSampleTick=Data.Series.Values.SelectMany(s=>s.Samples).Select(s=>s.Tick).DefaultIfEmpty(-1).Max();previousMaps=Data.ActiveMaps.ToHashSet();membershipKnown=previousMaps.Count>0;}
   else{ReadOnly=true;PreservedPayload=payload;Error="UnsupportedSave";}
  }
 }
 /// <summary>Alert rules with this map apply to the sum of all player home maps.</summary>
 public const string AllMaps="all";
 public void EvaluateAlerts(long tick){
  var homes=Maps.Select(InventoryReader.MapId).ToList();
  // Rules for maps that no longer exist can never fire again.
  var existing=Find.Maps.Select(InventoryReader.MapId).ToHashSet();
  Data.Alerts.RemoveAll(rule=>rule.Map!=AllMaps&&!existing.Contains(rule.Map));
  foreach(var rule in Data.Alerts){
   if(DefDatabase<ThingDef>.GetNamedSilentFail(rule.Resource)==null)continue;
   var maps=rule.Map==AllMaps?homes:homes.Where(m=>m==rule.Map).ToList();
   if(maps.Count==0||maps.Any(m=>Live.GetValueOrDefault(m)?.UnsupportedHolders>0))continue;
   var sources=maps.Select(m=>Data.Series.GetValueOrDefault(Ledger.Key(m,rule.Scope,rule.Resource))).ToList();
   if(sources.Any(s=>s==null))continue;
   var series=ViewMath.Merge(sources.Select(s=>s!));
   var summary=Queries.Summarize(series,Math.Max(0,tick-5*60000),tick);
   if(AlertEvaluator.Evaluate(rule,summary,Queries.Forecast(series,tick,5),tick))
    Messages.Message(TextCatalog.Get("AlertMessage",Data.Names.GetValueOrDefault(rule.Resource,rule.Resource),TextCatalog.Get("Alert_"+rule.Kind),rule.Threshold,summary.End),MessageTypeDefOf.CautionInput,false);
  }
 }
 public void Record(string map,string scope,string resource,long tick,long delta,Cause cause,string actor,string facility,string id,string actorId="",string facilityId=""){
  if(ReadOnly||!Data.Tracked.Contains(resource))return;
  // Pending events only reach summaries at the next sample, which bumps Revision itself.
  Data.Record(new LedgerEvent{Map=map,Scope=scope,Resource=resource,Tick=tick,Delta=delta,Cause=cause,Actor=actor,Facility=facility,Id=id,ActorId=actorId,FacilityId=facilityId});
 }
 private string ScopeSignature(Map map,string scope){
  if(scope=="map")return "map";
  var custom=Data.Scopes.FirstOrDefault(s=>s.Id==scope);
  return string.Join(";",map.haulDestinationManager.AllGroupsListForReading.Where(g=>scope=="stored"||custom?.Groups.Contains(InventoryReader.GroupId(g))==true).Select(g=>InventoryReader.GroupId(g)+":"+string.Join(",",g.CellsList.Select(cell=>cell.x+"/"+cell.z).Order())).Order());
 }
 public static void Report(Exception error){
  Log.WarningOnce("[Resource Ledger] "+error.GetType().Name+": "+error.Message,78145219);
  if(Instance is {} c)c.Error=error.Message;
 }
 public string Export(IEnumerable<Series> series,long from,long to,string kind,string unit="count"){
  var folder=Path.Combine(GenFilePaths.SaveDataFolderPath,"ResourceLedgerExports");Directory.CreateDirectory(folder);
  var path=Path.Combine(folder,"ResourceLedger_"+kind+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N")[..6]+".csv");
  var factors=unit=="nutrition"?Data.NutritionFactors:unit=="value"?Data.ValueFactors:null;
  File.WriteAllText(path,CsvExport.Export(Data,series,from,to,kind,factors:factors,unit:unit),new UTF8Encoding(true));return path;
 }
}

