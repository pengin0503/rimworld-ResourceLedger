using ResourceLedger.Core;
using RimWorld;
using Verse;
namespace ResourceLedger.Game;

public sealed class OperationCapture : IDisposable {
 [ThreadStatic]public static OperationCapture? Current;
 [ThreadStatic]public static int MergeDepth;
 [ThreadStatic]public static int RotDepth;
 private static long sequence;
 private static readonly string session=Guid.NewGuid().ToString("N");
 private readonly LedgerGameComponent component;
 private readonly Map map;private readonly InventorySnapshot before;
 private readonly Cause positive;private Cause negative;
 private readonly string actor,facility,actorId,facilityId;
 private readonly Dictionary<string,Dictionary<string,long>> scopeBefore;
 public readonly Dictionary<Thing,long> Consumed=new();
 private bool disposed;
 private OperationCapture(LedgerGameComponent c,Map m,Cause pos,Cause neg,Pawn? p,Thing? building){
  component=c;map=m;positive=pos;negative=neg;actor=p?.LabelShort??"";facility=building?.LabelCap??"";
  actorId=p?.ThingID??"";facilityId=building?.ThingID??"";
  before=InventoryReader.Read(m,c.Resources);
  if(before.UnsupportedHolders>0){c.Invalidate(InventoryReader.MapId(m));throw new InvalidOperationException("A storage holder could not be read; capture omitted.");}
  c.Discover(before);
  if(c.Live.GetValueOrDefault(InventoryReader.MapId(m)) is {UnsupportedHolders:0} prior)c.EnsureBaseline(m,prior,true);
  c.EnsureBaseline(m,before);
  scopeBefore=c.ScopeIds(m).ToDictionary(s=>s,s=>c.Project(before,s));
 }
 public static OperationCapture? Begin(Map? map,Cause positive,Cause negative,Pawn? actor=null,Thing? facility=null,bool movement=false){
  if(Current!=null||MergeDepth>0||map==null||!map.IsPlayerHome||CurrentProgramNotPlaying()||!LedgerMod.Settings.CaptureCauses||(movement&&!LedgerMod.Settings.CaptureTransfers))return null;
  try{var c=LedgerGameComponent.Instance;if(c==null||c.ReadOnly)return null;return Current=new OperationCapture(c,map,positive,negative,actor,facility);}
  catch(Exception ex){LedgerGameComponent.Instance?.Invalidate(InventoryReader.MapId(map));LedgerGameComponent.Report(ex);return null;}
 }
 private static bool CurrentProgramNotPlaying()=>Verse.Current.ProgramState!=ProgramState.Playing;
 public void Dispose(){
  if(disposed)return;disposed=true;
  try{
   var after=InventoryReader.Read(map,component.Resources);
   if(after.UnsupportedHolders>0)throw new InvalidOperationException("A storage holder could not be read; capture omitted.");
   component.Discover(after);component.EnsureBaseline(map,before);
   var mapId=InventoryReader.MapId(map);var tick=Find.TickManager.TicksGame;
   foreach(var (scope,counts) in scopeBefore){
    var definition=component.Data.Scopes.FirstOrDefault(s=>s.Id==scope);
    var consumed=before.ProjectConsumed(scope,Consumed,definition==null?null:new ScopeDefinitionAdapter{Groups=definition.Groups.ToHashSet()});
    var flows=OperationDiff.Compute(counts,component.Project(after,scope),consumed);
    foreach(var f in flows){
     var tag=session+":"+Interlocked.Increment(ref sequence);
     if(f.In>0)component.Record(mapId,scope,f.Resource,tick,f.In,positive,actor,facility,tag+":in",actorId,facilityId);
     if(f.Out>0)component.Record(mapId,scope,f.Resource,tick,-f.Out,negative,actor,facility,tag+":out",actorId,facilityId);
    }
   }
   component.Live[mapId]=after;component.OperationCount++;component.OperationMs+=before.Milliseconds+after.Milliseconds;component.Revision++;
  }catch(Exception ex){component.Invalidate(InventoryReader.MapId(map));LedgerGameComponent.Report(ex);}
  finally{if(ReferenceEquals(Current,this))Current=null;}
 }
 public static void AddConsumed(IEnumerable<(Thing Thing,long Quantity)> amounts){
  if(Current is not {} c)return;
  foreach(var (thing,qty) in amounts)if(qty>0)c.Consumed[thing]=c.Consumed.GetValueOrDefault(thing)+qty;
 }
 public static void PromoteConstruction(){if(Current is {negative:Cause.ScopeOut} capture)capture.negative=Cause.Construction;}
}

