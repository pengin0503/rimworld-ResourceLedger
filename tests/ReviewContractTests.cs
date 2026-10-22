using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using ResourceLedger.Core;

public static class ReviewContractTests {
 public static void Register(Action<string,Action> test,Func<Assembly> mod,Assembly game){
  object Make(string type)=>Activator.CreateInstance(mod().GetType("ResourceLedger.Game."+type,true)!)!;
  void Set(object o,string field,object value)=>o.GetType().GetField(field)!.SetValue(o,value);
  Type thingType=game.GetType("Verse.Thing",true)!,pawnType=game.GetType("Verse.Pawn",true)!;
  int thingId=0;
  object Thing(){var thing=RuntimeHelpers.GetUninitializedObject(thingType);var def=RuntimeHelpers.GetUninitializedObject(game.GetType("Verse.ThingDef",true)!);Set(def,"defName","Steel");Set(thing,"def",def);Set(thing,"thingIDNumber",++thingId);return thing;}
  (object Snapshot,object Stored,object Carried) Snapshot(){
   var snapshot=Make("InventorySnapshot");Set(snapshot,"Counts",new Dictionary<string,long>{{"Steel",15}});
   var stored=Thing();var carried=Thing();var pawn=RuntimeHelpers.GetUninitializedObject(pawnType);
   var items=(IList)snapshot.GetType().GetField("Items")!.GetValue(snapshot)!;
   foreach(var (thing,locator,count) in new[]{(stored,stored,10L),(carried,pawn,5L)}){
    var item=Make("InventoryItem");Set(item,"Thing",thing);Set(item,"Locator",locator);Set(item,"Resource","Steel");Set(item,"Group","stock");Set(item,"Count",count);items.Add(item);
   }
   return(snapshot,stored,carried);
  }
  object Custom(){var custom=Make("ScopeDefinitionAdapter");Set(custom,"Groups",new HashSet<string>{"stock"});return custom;}
  test("Pawn inventory on a stockpile remains carried in every scope",()=>{
   var (snapshot,_,_)=Snapshot();var project=snapshot.GetType().GetMethod("Project")!;
   foreach(var (scope,custom,expected) in new[]{("map",(object?)null,15L),("stored",null,10L),("custom",Custom(),10L)}){
    var counts=(Dictionary<string,long>)project.Invoke(snapshot,new[]{scope,custom})!;
    if(counts["Steel"]!=expected)throw new Exception($"{scope}: expected {expected}, got {counts["Steel"]}");
   }
  });
  test("Scoped recipes retain same-resource gross consumption",()=>{
   var (snapshot,stored,carried)=Snapshot();var consumed=(IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(thingType,typeof(long)))!;consumed.Add(stored,4L);consumed.Add(carried,3L);consumed.Add(Thing(),30L);
   var project=snapshot.GetType().GetMethod("ProjectConsumed")??throw new Exception("Scoped consumed ingredient projection is missing");
   foreach(var (scope,custom,expected) in new[]{("map",(object?)null,7L),("stored",null,4L),("custom",Custom(),4L)}){
    var counts=(Dictionary<string,long>)project.Invoke(snapshot,new[]{scope,consumed,custom})!;
    var flows=OperationDiff.Compute(new Dictionary<string,long>{{"Steel",10}},new Dictionary<string,long>{{"Steel",10}},counts).Single();
    if(flows.In!=expected||flows.Out!=expected)throw new Exception($"{scope}: lost gross turnover");
   }
  });
  test("Failed game scans invalidate live counts and recorded continuity",()=>{
   var c=mod().GetType("ResourceLedger.Game.LedgerGameComponent",true)!;var component=Activator.CreateInstance(c,new object?[]{null})!;
   var ledger=(Ledger)c.GetField("Data")!.GetValue(component)!;ledger.Observe("m","map",0,new Dictionary<string,long>{{"Steel",100}});
   var (snapshot,_,_)=Snapshot();((IDictionary)c.GetField("Live")!.GetValue(component)!).Add("m",snapshot);
   c.GetMethod("Invalidate")!.Invoke(component,new object[]{"m"});
   if((int)snapshot.GetType().GetField("UnsupportedHolders")!.GetValue(snapshot)!<=0||!ledger.Series.Values.Single().PendingGap)throw new Exception("Unreadable stock remained available");
  });
 }
}
