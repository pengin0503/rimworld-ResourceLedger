using RimWorld;
using Verse;
namespace ResourceLedger.Game;

public sealed class InventoryItem {
 public Thing Thing=null!, Locator=null!;
 public string Resource="",Group="",Place="";
 public long Count;
}
public sealed class InventorySnapshot {
 public List<InventoryItem> Items=new();
 public Dictionary<string,long> Counts=new();
 public long Tick;public double Milliseconds;public int UnsupportedHolders;
 public Dictionary<string,long> Project(string scope,ScopeDefinitionAdapter? custom=null){
  var result=Counts.Keys.ToDictionary(k=>k,k=>0L);
  foreach(var item in Items)if(scope=="map"||(scope=="stored"?item.Group.Length>0:custom!=null&&custom.Groups.Contains(item.Group)))result[item.Resource]+=item.Count;
  return result;
 }
}
public sealed class ScopeDefinitionAdapter {public HashSet<string> Groups=new();}
public static class InventoryReader {
 public static string MapId(Map map)=>map.uniqueID.ToString(System.Globalization.CultureInfo.InvariantCulture);
 public static string GroupId(SlotGroup group)=>group.parent is ILoadReferenceable r?r.GetUniqueLoadID():group.GetName();
 public static bool IsTrackable(ThingDef def)=>def.category==ThingCategory.Item&&!def.IsCorpse&&(def.stackLimit>1||def.CountAsResource||def.IsIngestible);
 public static InventorySnapshot Read(Map map,ISet<string> resources){
  var watch=System.Diagnostics.Stopwatch.StartNew();
  var snapshot=new InventorySnapshot{Tick=Find.TickManager.TicksGame,Counts=resources.ToDictionary(k=>k,k=>0L)};
  var seen=new HashSet<Thing>();var holders=new HashSet<IThingHolder>();
  void Holder(IThingHolder holder,Thing locator,string group,string place){
   if(!holders.Add(holder))return;
   if(holder is Pawn p&&p.Faction!=Faction.OfPlayer)return;
   if(holder is Frame)return;
   try{
   var direct=holder.GetDirectlyHeldThings();
   if(direct!=null)foreach(var child in direct)Walk(child,locator,group,place);
   var children=new List<IThingHolder>();holder.GetChildHolders(children);
   foreach(var child in children)if(child!=null)Holder(child,locator,group,place);
   }catch{snapshot.UnsupportedHolders++;}
  }
  void Walk(Thing thing,Thing locator,string group,string place){
   if(thing==null||thing.Destroyed||!seen.Add(thing))return;
   if(thing is Frame)return;
   if(thing is Pawn pawn&&pawn.Faction!=Faction.OfPlayer)return;
   if(thing is Corpse corpse&&corpse.InnerPawn?.Faction!=Faction.OfPlayer)return;
   if(thing.def.category==ThingCategory.Item&&resources.Contains(thing.def.defName)){
    var item=new InventoryItem{Thing=thing,Locator=locator,Resource=thing.def.defName,Count=Math.Max(0,thing.stackCount),Group=group,Place=place};
    snapshot.Items.Add(item);snapshot.Counts[item.Resource]+=item.Count;
   }
   if(thing is UnfinishedThing unfinished&&unfinished.ingredients!=null)
    foreach(var ingredient in unfinished.ingredients)Walk(ingredient,locator,group,place);
   if(thing is IThingHolder holder)Holder(holder,locator,group,place);
   if(thing is ThingWithComps comps)
    foreach(var comp in comps.AllComps)if(comp is IThingHolder ch)Holder(ch,locator,group,place);
  }
  foreach(var root in map.listerThings.AllThings){
   if(root.Destroyed||root.Position.Fogged(map))continue;
   string group="";string place=root.LabelCap;
   var slot=StoreUtility.GetSlotGroup(root);
   if(slot!=null){group=GroupId(slot);place=slot.GetName();}
   else if(root is Pawn)place=root.LabelShort;
   Walk(root,root,group,place);
  }
  watch.Stop();snapshot.Milliseconds=watch.Elapsed.TotalMilliseconds;return snapshot;
 }
}

