using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ResourceLedger.Core;
using RimWorld;
using Verse;
using Verse.AI;
namespace ResourceLedger.Game;

public sealed class CaptureTarget {
 public MethodBase? Method;public string Kind="";public Cause Positive,Negative;
}
public static class CapturePatches {
 public static readonly List<string> Installed=new(),Warnings=new();
 private static readonly Dictionary<MethodBase,CaptureTarget> descriptors=new();
 private sealed class ProductTag {public Cause Cause;public Pawn? Actor;}
 private static readonly ConditionalWeakTable<Thing,ProductTag> products=new();
 [ThreadStatic]private static int questDeliveryDepth;
 public static IEnumerable<CaptureTarget> Targets(){
  var targets=new List<CaptureTarget>();
  void Add(Type type,string method,string kind,Cause pos=Cause.Unclassified,Cause neg=Cause.Unclassified,Func<MethodInfo,bool>? filter=null){
   foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>m.Name==method&&(filter==null||filter(m))))targets.Add(new CaptureTarget{Method=m,Kind=kind,Positive=pos,Negative=neg});
  }
  Add(typeof(Mineable),"TrySpawnYield","Operation",Cause.Mining,Cause.Unclassified);
  Add(typeof(CompDeepDrill),"TryProducePortion","Operation",Cause.DeepMining,Cause.Unclassified);
  Add(typeof(Toils_Recipe),"FinishRecipeAndStartStoringProduct","RecipeToil",Cause.Production,Cause.RecipeMaterial);
  Add(typeof(Toils_Refuel),"FinalizeRefueling","RefuelToil",Cause.Unclassified,Cause.Fuel);
  Add(typeof(Toils_Recipe),"ConsumeIngredients","Ingredients");
  Add(typeof(Thing),"Ingested","Operation",Cause.Unclassified,Cause.Food);
  Add(typeof(TendUtility),"DoTend","Operation",Cause.Unclassified,Cause.Medical);
  Add(typeof(CompRefuelable),"Refuel","Operation",Cause.Unclassified,Cause.Fuel,m=>m.GetParameters().FirstOrDefault()?.ParameterType==typeof(List<Thing>));
  Add(typeof(CompHasGatherableBodyResource),"Gathered","Operation",Cause.AnimalProduct,Cause.Unclassified);
  Add(typeof(CompEggLayer),"ProduceEgg","Egg");
  Add(typeof(GenRecipe),"MakeRecipeProducts","RecipeProducts");
  Add(typeof(QuestPart_DropPods),"Notify_QuestSignalReceived","QuestDelivery");
  Add(typeof(QuestManager),"Notify_PlantHarvested","Harvest");
  Add(typeof(GenPlace),"TryPlaceThing","Placement");
  Add(typeof(TradeDeal),"TryExecute","Operation",Cause.Purchase,Cause.Sale);
  Add(typeof(Building),"Destroy","Operation",Cause.Recovery,Cause.Loss);
  Add(typeof(Frame),"FailConstruction","Operation",Cause.Recovery,Cause.Construction);
  Add(typeof(Frame),"Destroy","Operation",Cause.Recovery,Cause.Loss);
  Add(typeof(ThingOwner<Thing>),"TryAdd","Frame",Cause.Recovery,Cause.Construction);
  Add(typeof(ThingOwner),"TryTransferToContainer","FrameTransfer",Cause.Recovery,Cause.Construction);
  Add(typeof(Pawn_CarryTracker),"TryStartCarry","Movement",Cause.ScopeIn,Cause.ScopeOut);
  Add(typeof(Pawn_CarryTracker),"TryDropCarriedThing","Movement",Cause.ScopeIn,Cause.ScopeOut);
  Add(typeof(ThingOwner),"TryDrop","Movement",Cause.ScopeIn,Cause.ScopeOut);
  Add(typeof(Pawn),"ExitMap","Movement",Cause.TransferIn,Cause.TransferOut);
  Add(typeof(Pawn),"SpawnSetup","Arrival",Cause.TransferIn,Cause.TransferOut);
  Add(typeof(CompLaunchable),"TryLaunch","Movement",Cause.TransferIn,Cause.TransferOut);
  Add(typeof(DropPodUtility),"MakeDropPodAt","Movement",Cause.TransferIn,Cause.TransferOut);
  Add(typeof(DropPodUtility),"DropThingsNear","Movement",Cause.TransferIn,Cause.TransferOut);
  Add(typeof(Thing),"Destroy","Loss",Cause.Unclassified,Cause.Loss);
  Add(typeof(Thing),"TryAbsorbStack","Merge");
  Add(typeof(CompRottable),"TickInterval","Rot");
  return targets;
 }
 public static void Install(Harmony harmony){
  foreach(var target in Targets()){
   if(target.Method==null)continue;descriptors[target.Method]=target;
   try{
    switch(target.Kind){
     case "RecipeToil":harmony.Patch(target.Method,postfix:new HarmonyMethod(typeof(CapturePatches),nameof(RecipeToilPostfix)));break;
     case "RefuelToil":harmony.Patch(target.Method,postfix:new HarmonyMethod(typeof(CapturePatches),nameof(RefuelToilPostfix)));break;
     case "Ingredients":harmony.Patch(target.Method,prefix:new HarmonyMethod(typeof(CapturePatches),nameof(IngredientsPrefix)),finalizer:new HarmonyMethod(typeof(CapturePatches),nameof(IngredientsFinally)));break;
     case "Egg":harmony.Patch(target.Method,postfix:new HarmonyMethod(typeof(CapturePatches),nameof(EggPostfix)));break;
     case "RecipeProducts":harmony.Patch(target.Method,postfix:new HarmonyMethod(typeof(CapturePatches),nameof(RecipeProductsPostfix)));break;
     case "QuestDelivery":harmony.Patch(target.Method,prefix:new HarmonyMethod(typeof(CapturePatches),nameof(QuestPrefix)),finalizer:new HarmonyMethod(typeof(CapturePatches),nameof(QuestFinally)));break;
     case "Harvest":harmony.Patch(target.Method,postfix:new HarmonyMethod(typeof(CapturePatches),nameof(HarvestPostfix)));break;
     case "Merge":harmony.Patch(target.Method,prefix:new HarmonyMethod(typeof(CapturePatches),nameof(MergePrefix)),finalizer:new HarmonyMethod(typeof(CapturePatches),nameof(MergeFinally)));break;
     case "Rot":harmony.Patch(target.Method,prefix:new HarmonyMethod(typeof(CapturePatches),nameof(RotPrefix)),finalizer:new HarmonyMethod(typeof(CapturePatches),nameof(RotFinally)));break;
     default:harmony.Patch(target.Method,prefix:new HarmonyMethod(typeof(CapturePatches),nameof(Prefix)),finalizer:new HarmonyMethod(typeof(CapturePatches),nameof(Finally)));break;
    }
    Installed.Add(target.Method.DeclaringType!.FullName+"."+target.Method.Name+" ["+target.Kind+"]");
   }catch(Exception e){Warnings.Add(target.Method.Name+": "+e.Message);Log.Warning("[Resource Ledger] Cannot capture "+target.Method+": "+e.Message);}
  }
 }
 private static Map? MapOf(object? value)=>value switch{Map m=>m,Thing t=>t.MapHeld,ThingComp comp=>comp.parent.MapHeld,Pawn_CarryTracker carry=>carry.pawn.MapHeld,ThingOwner owner=>ThingOwnerUtility.GetRootMap(owner.Owner),_=>null};
 public static void Prefix(MethodBase __originalMethod,object? __instance,object[] __args,out OperationCapture? __state){
  __state=null;
  try{
   var descriptor=descriptors[__originalMethod];var pos=descriptor.Positive;var neg=descriptor.Negative;
   var actor=__args.OfType<Pawn>().FirstOrDefault()??(__instance as Pawn);
   var map=__args.OfType<Map>().FirstOrDefault()??MapOf(__instance)??actor?.MapHeld;
   if(descriptor.Kind=="Frame"&&(__instance as ThingOwner)?.Owner is not Frame)return;
   if(descriptor.Kind=="FrameTransfer"){if(__args.OfType<ThingOwner>().FirstOrDefault()?.Owner is not Frame)return;OperationCapture.PromoteConstruction();}
   if(descriptor.Kind=="Arrival"&&(__args.OfType<bool>().FirstOrDefault()||actor?.Faction!=Faction.OfPlayer))return;
   if(__instance is TradeDeal){actor=TradeSession.playerNegotiator;map=actor?.Map;if(TradeSession.giftMode)pos=neg=Cause.Gift;}
   if(__originalMethod.DeclaringType==typeof(DropPodUtility)&&questDeliveryDepth>0){pos=Cause.Reward;foreach(var list in __args.OfType<List<Thing>>())foreach(var item in list)Tag(item,Cause.Reward,null);}
   if(__instance is Thing food&&__originalMethod.Name=="Ingested"&&food.def.IsDrug)neg=Cause.Drug;
   if(descriptor.Kind=="Loss"){
    if(__instance is not Thing item||item.def.category!=ThingCategory.Item)return;
    var mode=__args.OfType<DestroyMode>().FirstOrDefault();neg=OperationCapture.RotDepth>0?Cause.Rot:mode is DestroyMode.KillFinalize or DestroyMode.KillFinalizeLeavingsOnly?Cause.Loss:Cause.Unclassified;
   }
   if(descriptor.Kind=="Placement"){
    var thing=__args.OfType<Thing>().FirstOrDefault();if(thing==null||!products.TryGetValue(thing,out var tag))return;pos=tag.Cause;actor=tag.Actor;
   }
   __state=OperationCapture.Begin(map,pos,neg,actor,(__instance as ThingComp)?.parent??__instance as Building,descriptor.Kind is "Movement" or "Arrival");
  }catch(Exception e){LedgerGameComponent.Report(e);}
 }
 public static Exception? Finally(Exception? __exception,OperationCapture? __state){__state?.Dispose();return __exception;}
 private sealed class RecipeAction {
  public Toil Toil=null!;public Action Original=null!;
  public Cause Positive=Cause.Production,Negative=Cause.RecipeMaterial;
  public void Run(){using var capture=OperationCapture.Begin(Toil.actor?.MapHeld,Positive,Negative==Cause.RecipeMaterial&&Toil.actor?.CurJob?.bill is Bill_Medical?Cause.Surgery:Negative,Toil.actor,Toil.actor?.CurJob?.targetA.Thing as Building);Original();}
 }
 public static void RecipeToilPostfix(Toil __result){
  if(__result?.initAction==null||__result.initAction.Target is RecipeAction)return;
  var wrapper=new RecipeAction{Toil=__result,Original=__result.initAction};__result.initAction=wrapper.Run;
 }
 public static void RefuelToilPostfix(Toil __result){
  if(__result?.initAction==null||__result.initAction.Target is RecipeAction)return;
  var wrapper=new RecipeAction{Toil=__result,Original=__result.initAction,Positive=Cause.Unclassified,Negative=Cause.Fuel};__result.initAction=wrapper.Run;
 }
 public static void IngredientsPrefix(object[] __args,out List<(Thing Thing,int Count)>? __state){
  __state=null;if(OperationCapture.Current==null)return;
  var list=__args.OfType<List<Thing>>().FirstOrDefault();if(list!=null)__state=list.Where(t=>!t.Destroyed).Select(t=>(t,t.stackCount)).ToList();
 }
 public static Exception? IngredientsFinally(Exception? __exception,List<(Thing Thing,int Count)>? __state){
  try{if(__state!=null)OperationCapture.AddConsumed(__state.Select(x=>(x.Thing.def.defName,(long)Math.Max(0,x.Count-(x.Thing.Destroyed?0:x.Thing.stackCount)))));}
  catch(Exception e){LedgerGameComponent.Report(e);}return __exception;
 }
 public static void EggPostfix(Thing? __result){if(__result!=null)Tag(__result,Cause.AnimalProduct,null);}
 public static void RecipeProductsPostfix(ref IEnumerable<Thing> __result,object[] __args){var actor=__args.OfType<Pawn>().FirstOrDefault();__result=TagProducts(__result,actor);}
 private static IEnumerable<Thing> TagProducts(IEnumerable<Thing> original,Pawn? actor){foreach(var thing in original){Tag(thing,Cause.Production,actor);yield return thing;}}
 public static void QuestPrefix(QuestPart_DropPods __instance,Signal signal,out bool __state){__state=signal.tag==__instance.inSignal;if(__state)questDeliveryDepth++;}
 public static Exception? QuestFinally(Exception? __exception,bool __state){if(__state)questDeliveryDepth--;return __exception;}
 public static void HarvestPostfix(object[] __args){var t=__args.OfType<Thing>().FirstOrDefault(t=>t is not Pawn);if(t!=null)Tag(t,t.def.defName=="WoodLog"?Cause.Logging:Cause.Harvest,__args.OfType<Pawn>().FirstOrDefault());}
 private static void Tag(Thing thing,Cause cause,Pawn? actor){products.Remove(thing);products.Add(thing,new ProductTag{Cause=cause,Actor=actor});}
 public static void MergePrefix()=>OperationCapture.MergeDepth++;
 public static Exception? MergeFinally(Exception? __exception){OperationCapture.MergeDepth--;return __exception;}
 public static void RotPrefix()=>OperationCapture.RotDepth++;
 public static Exception? RotFinally(Exception? __exception){OperationCapture.RotDepth--;return __exception;}
}

