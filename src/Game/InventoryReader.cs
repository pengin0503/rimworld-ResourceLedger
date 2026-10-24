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
 public static bool Includes(InventoryItem item,string scope,ScopeDefinitionAdapter? custom=null)=>
  scope=="map"||(item.Locator is not Pawn&&(scope=="stored"?item.Group.Length>0:custom!=null&&custom.Groups.Contains(item.Group)));
 public Dictionary<string,long> Project(string scope,ScopeDefinitionAdapter? custom=null){
  var result=Counts.Keys.ToDictionary(k=>k,k=>0L);
  foreach(var item in Items)if(Includes(item,scope,custom))result[item.Resource]+=item.Count;
  return result;
 }
 public Dictionary<string,long> ProjectConsumed(string scope,IReadOnlyDictionary<Thing,long> consumed,ScopeDefinitionAdapter? custom=null){
  var result=new Dictionary<string,long>();
  foreach(var item in Items){
   if(!Includes(item,scope,custom))continue;
   long amount=Math.Min(item.Count,Math.Max(0,consumed.GetValueOrDefault(item.Thing)));
   if(amount>0)result[item.Resource]=result.GetValueOrDefault(item.Resource)+amount;
  }
  return result;
 }
}
public sealed class ScopeDefinitionAdapter {public HashSet<string> Groups=new();}
/// <summary>Where an item is shown in the location list; the label is only built for roots that hold tracked items.</summary>
public sealed class PlaceLabel {
 private readonly Thing root;private readonly SlotGroup? slot;private string? text;
 public PlaceLabel(Thing root,SlotGroup? slot){this.root=root;this.slot=slot;}
 public string Text=>text??=slot!=null?slot.GetName():root is Pawn?root.LabelShort:root.LabelCap.ToString();
}
public static class InventoryReader {
 public static string MapId(Map map)=>map.uniqueID.ToString(System.Globalization.CultureInfo.InvariantCulture);
 public static string GroupId(SlotGroup group)=>group.parent is ILoadReferenceable r?r.GetUniqueLoadID():group.GetName();
 public static string ResourceId(Thing thing)=>thing is MinifiedThing minified&&minified.InnerThing!=null?minified.InnerThing.def.defName:thing.def.defName;
 private static readonly Dictionary<ThingDef,bool> holdsItems=new();
 /// <summary>Items themselves, and anything that is or has a thing holder (pawns, containers, corpses, transporters).</summary>
 public static bool CanHoldItems(ThingDef def){
  if(def.category==ThingCategory.Item||def.category==ThingCategory.Pawn)return true;
  if(!holdsItems.TryGetValue(def,out var holds))holdsItems[def]=holds=def.ThisOrAnyCompIsThingHolder();
  return holds;
 }
 public static bool IsTrackable(ThingDef def)=>def.category==ThingCategory.Item&&!def.IsCorpse&&(def.thingClass==null||!typeof(UnfinishedThing).IsAssignableFrom(def.thingClass));
 public static InventorySnapshot Read(Map map,ISet<string> resources){
  var watch=System.Diagnostics.Stopwatch.StartNew();
  var snapshot=new InventorySnapshot{Tick=Find.TickManager.TicksGame,Counts=resources.ToDictionary(k=>k,k=>0L)};
  var seen=new HashSet<Thing>();var holders=new HashSet<IThingHolder>();
  void Holder(IThingHolder holder,Thing locator,string group,PlaceLabel place){
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
  void Walk(Thing thing,Thing locator,string group,PlaceLabel place){
   if(thing==null||thing.Destroyed||!seen.Add(thing))return;
   if(thing is Frame)return;
   if(thing is Pawn pawn&&pawn.Faction!=Faction.OfPlayer)return;
   if(thing is Corpse corpse&&corpse.InnerPawn?.Faction!=Faction.OfPlayer)return;
   if(IsTrackable(thing.def)){
    var item=new InventoryItem{Thing=thing,Locator=locator,Resource=ResourceId(thing),Count=Math.Max(0,thing.stackCount),Group=group,Place=place.Text};
    snapshot.Items.Add(item);snapshot.Counts[item.Resource]=snapshot.Counts.GetValueOrDefault(item.Resource)+item.Count;
   }
   if(thing is UnfinishedThing unfinished&&unfinished.ingredients!=null)
    foreach(var ingredient in unfinished.ingredients)Walk(ingredient,locator,group,place);
   if(thing is IThingHolder holder)Holder(holder,locator,group,place);
   if(thing is ThingWithComps comps)
    foreach(var comp in comps.AllComps)if(comp is IThingHolder ch)Holder(ch,locator,group,place);
  }
  foreach(var root in map.listerThings.AllThings){
   // Plants, filth and plain buildings can never hold a tracked item; skip them before any per-thing work.
   if(root.Destroyed||!CanHoldItems(root.def)||root.Position.Fogged(map))continue;
   string group="";
   var slot=root is Pawn?null:StoreUtility.GetSlotGroup(root);
   if(slot!=null)group=GroupId(slot);
   Walk(root,root,group,new PlaceLabel(root,slot));
  }
  watch.Stop();snapshot.Milliseconds=watch.Elapsed.TotalMilliseconds;return snapshot;
 }
}

