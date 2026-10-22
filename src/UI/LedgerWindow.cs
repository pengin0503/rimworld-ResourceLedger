using ResourceLedger.Core;
using ResourceLedger.Game;
using RimWorld;
using UnityEngine;
using Verse;
namespace ResourceLedger.UI;

public sealed class LedgerWindow : MainTabWindow {
 private string mapChoice="",scope="map",search="",modFilter="",category="",selected="",sort="Name",unit="count";
 private bool descending,favoritesOnly,relative,bars;
 private int periodDays=5,detailTab;
 private string customFrom="0",customTo="0";
 private Vector2 tableScroll,detailScroll;
 private readonly HashSet<string> compared=new(),hidden=new();
 private readonly HashSet<string> hiddenGraph=new();
 private string detailCacheKey="";private List<(string Text,Thing? Target)> detailCache=new();
 private string graphCacheKey="";private List<(string Label,Series Series,double Factor)> graphCache=new();
 private string cacheKey="";private List<RowData> cached=new();
 private sealed class RowData {public string Resource="",Name="",Mod="",Category="";public Summary Summary=new();public List<Series> Sources=new();public Forecast Forecast=new();public long? Live;public bool Missing;}
 public override Vector2 RequestedTabSize=>new(Math.Min(1480,Verse.UI.screenWidth-40),Math.Min(940,Verse.UI.screenHeight-90));
 public override void PostOpen(){base.PostOpen();var settings=LedgerMod.Settings;sort=settings.SortColumn;unit=settings.DisplayUnit;descending=settings.SortDescending;favoritesOnly=settings.FavoritesOnly;hidden.Clear();foreach(var col in settings.HiddenColumns)hidden.Add(col);LedgerGameComponent.Instance?.SampleAll(true);}
 public override void PostClose(){var settings=LedgerMod.Settings;settings.SortColumn=sort;settings.DisplayUnit=unit;settings.SortDescending=descending;settings.FavoritesOnly=favoritesOnly;settings.HiddenColumns=hidden.ToList();LoadedModManager.GetMod<LedgerMod>().WriteSettings();base.PostClose();}
 private static string T(string key,params object[] args)=>TextCatalog.Get(key,args);
 private static string N(double value,int places=0)=>TextCatalog.Number(value,places);
 private LedgerGameComponent? C=>LedgerGameComponent.Instance;
 private Map? ChosenMap=>C?.Maps.FirstOrDefault(m=>InventoryReader.MapId(m)==mapChoice)??Find.CurrentMap;
 private (long From,long To) Range(){
  long now=C?.LastSampleTick>=0?C.LastSampleTick:Find.TickManager.TicksGame;
  if(periodDays>0)return(Math.Max(0,now-periodDays*60000L),now);
  if(double.TryParse(customFrom,out var a)&&double.TryParse(customTo,out var b)&&double.IsFinite(a)&&double.IsFinite(b)&&a>=0&&b>a&&b<35000)return((long)(a*60000),(long)(b*60000));
  return(Math.Max(0,now-60000),now);
 }
 private double Factor(string id){
  var d=DefDatabase<ThingDef>.GetNamedSilentFail(id);
  return unit=="nutrition"?d?.ingestible?.CachedNutrition??C?.Data.NutritionFactors.GetValueOrDefault(id)??0:unit=="value"?d?.BaseMarketValue??C?.Data.ValueFactors.GetValueOrDefault(id)??0:1;
 }
 private string UnitLabel=>T(unit=="nutrition"?"Nutrition":unit=="value"?"MarketValue":"Count");
 private List<Series> Sources(string id){
  if(C==null)return new();
  var active=C.Maps.Select(InventoryReader.MapId).ToHashSet();
  return C.Data.Series.Values.Where(s=>s.Resource==id&&s.Scope==scope&&(mapChoice=="all"?active.Contains(s.Map):s.Map==mapChoice)).ToList();
 }
 private List<RowData> Rows(){
  var c=C!;var range=Range();
  string key=c.Revision+"|"+mapChoice+"|"+scope+"|"+search+"|"+modFilter+"|"+category+"|"+favoritesOnly+"|"+sort+"|"+descending+"|"+range.From+"|"+range.To;
  // Time only changes with simulation; all GUI events in a paused frame reuse this result.
  if(key==cacheKey)return cached;cacheKey=key;
  var rows=new List<RowData>();
  var mapList=mapChoice=="all"?c.Maps.ToList():c.Maps.Where(m=>InventoryReader.MapId(m)==mapChoice).ToList();
  var liveCounts=mapList.Where(m=>c.Live.GetValueOrDefault(InventoryReader.MapId(m)) is {UnsupportedHolders:0}).ToDictionary(m=>InventoryReader.MapId(m),m=>c.Project(c.Live[InventoryReader.MapId(m)],scope));
  foreach(var id in c.Data.Tracked.Distinct()){
   var d=DefDatabase<ThingDef>.GetNamedSilentFail(id);string name=c.Data.Names.GetValueOrDefault(id,id),source=c.Data.ResourceMods.GetValueOrDefault(id,""),cat=d?.FirstThingCategory?.LabelCap??T("Other");
   if(favoritesOnly&&!c.Data.Favorites.Contains(id))continue;
   if(search.Length>0&&!name.Contains(search,StringComparison.CurrentCultureIgnoreCase)&&!id.Contains(search,StringComparison.OrdinalIgnoreCase))continue;
   if(modFilter.Length>0&&source!=modFilter)continue;if(category.Length>0&&cat!=category)continue;
   var sources=Sources(id);var merged=ViewMath.Merge(sources);var summary=Queries.Summarize(merged,range.From,range.To);
   var forecast=d==null?new Forecast():Queries.Forecast(merged,Find.TickManager.TicksGame,5);
   long? live=null;
   if(d!=null&&mapList.Count>0&&liveCounts.Count==mapList.Count)live=liveCounts.Values.Sum(counts=>counts.GetValueOrDefault(id));
   rows.Add(new RowData{Resource=id,Name=name,Mod=source,Category=cat,Sources=sources,Summary=summary,Forecast=forecast,Live=live,Missing=d==null});
  }
  Func<RowData,IComparable> order=sort switch{"Current"=>r=>r.Live??long.MinValue,"Start"=>r=>r.Summary.Start,"End"=>r=>r.Summary.End,"Increase"=>r=>r.Summary.Increase,"Decrease"=>r=>r.Summary.Decrease,"Net"=>r=>r.Summary.Net,"Percent"=>r=>r.Summary.Percent??double.MinValue,"Daily"=>r=>r.Summary.DailyMean,"Forecast"=>r=>r.Forecast.Available?r.Forecast.Days:double.MaxValue,_=>r=>r.Name};
  var ordered=descending?rows.OrderByDescending(order):rows.OrderBy(order);
  cached=ordered.OrderByDescending(r=>c.Data.Favorites.Contains(r.Resource)).ToList();return cached;
 }
 private static void Menu(Rect rect,string label,IEnumerable<(string Label,Action Action)> choices){
  if(Widgets.ButtonText(rect,label))Find.WindowStack.Add(new FloatMenu(choices.Select(x=>new FloatMenuOption(x.Label,x.Action)).ToList()));
 }
 public override void DoWindowContents(Rect rect){
  var oldColor=GUI.color;var oldFont=Text.Font;var oldAnchor=Text.Anchor;
  try{
   Text.Font=GameFont.Small;Text.Anchor=TextAnchor.UpperLeft;
   var c=C;if(c==null){Widgets.Label(rect,T("NoColony"));return;}
   if(c.ReadOnly){Widgets.Label(rect,T("UnsupportedSave"));return;}
   var maps=c.Maps.ToList();if(maps.Count==0){Widgets.Label(rect,T("NoColony"));return;}
   if(mapChoice.Length==0||(mapChoice!="all"&&!maps.Any(m=>InventoryReader.MapId(m)==mapChoice)))mapChoice=InventoryReader.MapId(Find.CurrentMap?.IsPlayerHome==true?Find.CurrentMap:maps[0]);
   if(scope!="map"&&scope!="stored"&&!c.Data.Scopes.Any(s=>s.Id==scope&&s.Map==mapChoice))scope="map";
   DrawControls(new Rect(0,0,rect.width,108),maps);
   var content=new Rect(0,113,rect.width,rect.height-182);
   if(selected.Length==0)DrawTable(content);else DrawDetail(content);
   Widgets.DrawLineHorizontal(0,rect.height-62,rect.width);
   Widgets.Label(new Rect(0,rect.height-56,rect.width,26),T("AttributionHint"));
   string stamp=TextCatalog.Day(c.LastSampleTick<0?0:c.LastSampleTick);
   var unreadable=c.Live.Values.Sum(s=>s.UnsupportedHolders);
   Widgets.Label(new Rect(0,rect.height-29,rect.width,25),unreadable>0?T("StorageUnavailable",unreadable):T("Status",stamp,N(c.LastSampleMs,2),c.Data.Series.Values.Sum(s=>s.Samples.Count),c.Data.Events.Count));
   if(c.Error.Length>0)TooltipHandler.TipRegion(new Rect(0,rect.height-56,rect.width,56),c.Error);
  }finally{GUI.color=oldColor;Text.Font=oldFont;Text.Anchor=oldAnchor;}
 }
 private void DrawControls(Rect rect,List<Map> maps){
  var c=C!;
  var mapOptions=maps.Select(m=>(Label:c.Data.MapNames.GetValueOrDefault(InventoryReader.MapId(m),m.Parent?.LabelCap??"Map"),Action:(Action)(()=>{mapChoice=InventoryReader.MapId(m);scope="map";}))).ToList();
  mapOptions.Insert(0,(T("AllMaps"),()=>{mapChoice="all";scope="map";}));
  Menu(new Rect(0,0,220,30),mapChoice=="all"?T("AllMaps"):c.Data.MapNames.GetValueOrDefault(mapChoice,mapChoice),mapOptions);
  var scopes=new List<(string,Action)>{(T("ScopeMap"),()=>scope="map"),(T("ScopeStored"),()=>scope="stored")};
  scopes.AddRange(c.Data.Scopes.Where(s=>s.Map==mapChoice).Select(s=>(s.Name,(Action)(()=>scope=s.Id))));
  if(mapChoice!="all")scopes.Add((T("NewScope"),()=>LedgerDialogs.Scope(c,ChosenMap!)));
  Menu(new Rect(230,0,210,30),scope=="map"?T("ScopeMap"):scope=="stored"?T("ScopeStored"):c.Data.Scopes.FirstOrDefault(s=>s.Id==scope)?.Name??scope,scopes);
  var periods=new[]{1,5,15,60}.Select(days=>(T("Days",days),(Action)(()=>periodDays=days))).ToList();
  periods.Add((T("CustomPeriod"),()=>{periodDays=0;customFrom=Math.Max(0,Find.TickManager.TicksGame/60000d-5).ToString("F2");customTo=(Find.TickManager.TicksGame/60000d).ToString("F2");}));
  Menu(new Rect(450,0,140,30),periodDays>0?T("Days",periodDays):T("CustomPeriod"),periods);
  if(periodDays==0){customFrom=Widgets.TextField(new Rect(600,0,90,30),customFrom);customTo=Widgets.TextField(new Rect(695,0,90,30),customTo);TooltipHandler.TipRegion(new Rect(600,0,185,30),T("CustomPeriodHint"));}
  else Menu(new Rect(600,0,185,30),UnitLabel,new[]{(T("Count"),(Action)(()=>unit="count")),(T("Nutrition"),()=>unit="nutrition"),(T("MarketValue"),()=>unit="value")});
  if(Widgets.ButtonText(new Rect(rect.width-380,38,120,30),T("Tracking")))LedgerDialogs.Tracking(c);
  if(Widgets.ButtonText(new Rect(rect.width-250,38,120,30),T("Recording")))LedgerDialogs.Recording();
  if(Widgets.ButtonText(new Rect(rect.width-120,38,120,30),T("Export")))ExportMenu(new Rect(rect.width-120,38,120,30));
  search=Widgets.TextField(new Rect(0,38,220,30),search);
  Menu(new Rect(230,38,210,30),modFilter.Length==0?T("AllMods"):modFilter,c.Data.ResourceMods.Values.Distinct().Order().Select(m=>(m,(Action)(()=>modFilter=m))).Prepend((T("AllMods"),()=>modFilter="")));
  Menu(new Rect(450,38,140,30),category.Length==0?T("AllCategories"):category,Rows().Select(r=>r.Category).Distinct().Select(cat=>(cat,(Action)(()=>category=cat))).Prepend((T("AllCategories"),()=>category="")));
  Widgets.CheckboxLabeled(new Rect(600,38,155,30),T("Favorites"),ref favoritesOnly);
  if(selected.Length>0&&Widgets.ButtonText(new Rect(0,76,135,28),T("Back")))selected="";
  else if(selected.Length==0&&Widgets.ButtonText(new Rect(0,76,135,28),T("Compare"))){selected=compared.FirstOrDefault()??Rows().FirstOrDefault()?.Resource??"";}
  Menu(new Rect(145,76,150,28),T("Columns"),new[]{"Start","End","Increase","Decrease","Net","Percent","Daily","Forecast"}.Select(col=>((hidden.Contains(col)?"☐ ":"☑ ")+T(col),(Action)(()=>{if(!hidden.Add(col))hidden.Remove(col);}))));
  if(Widgets.ButtonText(new Rect(rect.width-120,76,120,28),T("Diagnostics")))Diagnostics();
 }
 private void ExportMenu(Rect ignored){
  var range=Range();var c=C!;
  Find.WindowStack.Add(new FloatMenu(new[]{"summary","history","causes","events"}.Select(kind=>new FloatMenuOption(T("Export_"+kind),()=>{
   try{IEnumerable<Series> sources=selected.Length==0?Rows().SelectMany(r=>r.Sources):Sources(selected);if(mapChoice=="all"&&kind!="events")sources=sources.GroupBy(s=>s.Resource).Select(g=>ViewMath.Merge(g));var selectedSummary=selected.Length==0?null:Rows().FirstOrDefault(r=>r.Resource==selected)?.Summary;var path=c.Export(sources,selectedSummary?.From??range.From,selectedSummary?.To??range.To,kind,unit);Find.WindowStack.Add(new Dialog_MessageBox(T("Exported",path)));}
   catch(Exception ex){LedgerGameComponent.Report(ex);Messages.Message(ex.Message,MessageTypeDefOf.RejectInput,false);}
  })).ToList()));
 }
 private void Diagnostics(){
  var c=C!;
  string text=T("DiagnosticsText",c.OperationCount,N(c.OperationMs,2),c.Data.Tracked.Count,c.Data.Series.Values.Sum(s=>s.Samples.Count),c.Data.Events.Count)+"\n\n"+T("CaptureMethods",CapturePatches.Installed.Count)+"\n"+string.Join("\n",CapturePatches.Installed)+"\n\n"+string.Join("\n",CapturePatches.Warnings);
  Find.WindowStack.Add(new Dialog_MessageBox(text));
 }
 private void DrawTable(Rect rect){
  var rows=Rows();var columns=new[]{"Start","End","Increase","Decrease","Net","Percent","Daily","Forecast"}.Where(col=>!hidden.Contains(col)).ToList();
  float nameWidth=Math.Clamp(rect.width*.23f,190,290),currentWidth=95,cw=Math.Max(65,(rect.width-nameWidth-currentWidth-96)/Math.Max(1,columns.Count));
  if(Widgets.ButtonText(new Rect(75,rect.y,nameWidth-75,28),T("Resource")))SortBy("Name");
  if(Widgets.ButtonText(new Rect(nameWidth,rect.y,currentWidth,28),T("Current")))SortBy("Current");
  for(int i=0;i<columns.Count;i++){var col=columns[i];if(Widgets.ButtonText(new Rect(nameWidth+currentWidth+i*cw,rect.y,cw-3,28),T(col)))SortBy(col);}
  var viewport=new Rect(rect.x,rect.y+34,rect.width,rect.height-34);Widgets.BeginScrollView(viewport,ref tableScroll,new Rect(0,0,rect.width-20,rows.Count*36+8));
  for(int i=Math.Max(0,(int)(tableScroll.y/36)-1);i<Math.Min(rows.Count,(int)((tableScroll.y+viewport.height)/36)+2);i++){
   var row=rows[i];float y=i*36;
   if(i%2==0)Widgets.DrawBoxSolid(new Rect(0,y,rect.width-22,35),new Color(.18f,.19f,.21f,.55f));
   bool compare=compared.Contains(row.Resource);Widgets.Checkbox(2,y+6,ref compare,22);if(compare)compared.Add(row.Resource);else compared.Remove(row.Resource);
   bool favorite=C!.Data.Favorites.Contains(row.Resource);
   if(Widgets.ButtonText(new Rect(28,y,35,32),favorite?"★":"☆")){if(favorite)C.Data.Favorites.Remove(row.Resource);else C.Data.Favorites.Add(row.Resource);C.Revision++;}
   var label=new Rect(70,y,nameWidth-75,32);Widgets.LabelEllipses(label,row.Name);
   if(Widgets.ButtonInvisible(label))selected=row.Resource;
   TooltipHandler.TipRegion(label,row.Name+"\n"+row.Resource+"\n"+row.Mod+(row.Missing?"\n"+T("MissingDefinition"):""));
   Widgets.Label(new Rect(nameWidth+5,y,currentWidth-8,32),row.Live.HasValue?N(row.Live.Value*Factor(row.Resource),unit=="count"?0:2):"—");
   for(int j=0;j<columns.Count;j++){
    var cell=new Rect(nameWidth+currentWidth+j*cw+4,y,cw-6,32);var s=row.Summary;double f=Factor(row.Resource);
    string value=columns[j] switch{
     "Start"=>s.Continuous?N(s.Start*f,unit=="count"?0:2):"—","End"=>s.Continuous?N(s.End*f,unit=="count"?0:2):"—",
     "Increase"=>s.Continuous?"+"+N(s.Increase*f,unit=="count"?0:2):"—","Decrease"=>s.Continuous?"−"+N(s.Decrease*f,unit=="count"?0:2):"—",
     "Net"=>s.Continuous?(s.Net>=0?"+":"")+N(s.Net*f,unit=="count"?0:2):"—","Percent"=>s.Continuous&&s.Percent.HasValue?N(s.Percent.Value,1)+"%":"—",
     "Daily"=>s.Continuous?N(s.DailyMean*f,1):"—","Forecast"=>!row.Missing&&row.Forecast.Available?T("DaysShort",N(row.Forecast.Days,1)):"—",_=>""};
    Widgets.LabelEllipses(cell,value);TooltipHandler.TipRegion(cell,T("RecordedPeriod",TextCatalog.Day(s.From),TextCatalog.Day(s.To))+"\n"+(s.Continuous?T("Recorded"):T("Gap")));
   }
  }
  Widgets.EndScrollView();
  if(rows.Count==0)Widgets.Label(viewport.ContractedBy(20),T("NoResources"));
 }
 private void SortBy(string column){if(sort==column)descending=!descending;else{sort=column;descending=true;}cacheKey="";}
 private void DrawDetail(Rect rect){
  var c=C!;var row=Rows().FirstOrDefault(r=>r.Resource==selected);
  if(row==null){selected="";return;}
  var range=Range();var sum=row.Summary;float y=rect.y;
  Text.Font=GameFont.Medium;Widgets.Label(new Rect(0,y,rect.width-180,30),row.Name+" — "+UnitLabel);Text.Font=GameFont.Small;
  if(Widgets.ButtonText(new Rect(rect.width-170,y,170,30),T("Alerts"))){var map=mapChoice=="all"?InventoryReader.MapId(ChosenMap!):mapChoice;LedgerDialogs.Alert(c,selected,map,scope);}
  y+=35;
  Widgets.Label(new Rect(0,y,rect.width,28),T("Summary",row.Live.HasValue?N(row.Live.Value*Factor(selected),unit=="count"?0:2):"—",sum.Continuous?N(sum.Increase*Factor(selected),2):"—",sum.Continuous?N(sum.Decrease*Factor(selected),2):"—",sum.Continuous?N(sum.Net*Factor(selected),2):"—",row.Forecast.Available?T("DaysShort",N(row.Forecast.Days,1)):T(row.Forecast.Status)));
  y+=30;Widgets.Label(new Rect(0,y,rect.width,26),T("RecordedPeriod",TextCatalog.Day(sum.From),TextCatalog.Day(sum.To))+" / "+T("ForecastHint",N(row.Forecast.ObservedDays,2)));y+=30;
  Widgets.CheckboxLabeled(new Rect(0,y,180,26),T("Relative"),ref relative);
  Widgets.CheckboxLabeled(new Rect(190,y,220,26),T("DailyBars"),ref bars);
  var ids=new[]{selected}.Concat(compared.Where(id=>id!=selected&&c.Data.Tracked.Contains(id))).Take(6).ToList();
  string graphKey=c.Revision+"|"+mapChoice+"|"+scope+"|"+unit+"|"+string.Join(",",ids);
  if(graphKey!=graphCacheKey){graphCache=ids.Take(6).Select(id=>(c.Data.Names.GetValueOrDefault(id,id),ViewMath.Merge(Sources(id)),Factor(id))).ToList();graphCacheKey=graphKey;}
  var graphSources=graphCache;
  float lx=430;
  float legendWidth=Math.Max(45,(rect.width-430)/Math.Max(1,graphSources.Count));int visibleColor=0;
  for(int i=0;i<graphSources.Count;i++){var g=graphSources[i];var legend=new Rect(lx,y,legendWidth-4,26);GUI.color=hiddenGraph.Contains(ids[i])?Color.gray:LedgerGraph.Colors[visibleColor++%LedgerGraph.Colors.Length];Widgets.LabelEllipses(legend,g.Item1);TooltipHandler.TipRegion(legend,g.Item1);GUI.color=Color.white;if(Widgets.ButtonInvisible(legend)){if(!hiddenGraph.Add(ids[i]))hiddenGraph.Remove(ids[i]);}lx+=legendWidth;}
  var visibleGraph=graphSources.Where((s,i)=>!hiddenGraph.Contains(ids[i])).ToList();if(bars)visibleGraph=hiddenGraph.Contains(selected)?new():graphSources.Take(1).ToList();
  y+=30;float graphHeight=Math.Clamp(rect.height*.40f,160,290);LedgerGraph.Draw(new Rect(0,y,rect.width,graphHeight),visibleGraph,range.From,range.To,relative,bars);y+=graphHeight+10;
  var tabLabels=new[]{"Causes","Events","Locations"};
  for(int i=0;i<tabLabels.Length;i++){int index=i;if(Widgets.ButtonText(new Rect(i*160,y,150,28),T(tabLabels[i]))) {detailTab=index;detailScroll=Vector2.zero;}}
  y+=35;
  var viewport=new Rect(0,y,rect.width,Math.Max(40,rect.yMax-y));var lines=new List<(string Text,Thing? Target)>();
  string detailKey=c.Revision+"|"+selected+"|"+scope+"|"+mapChoice+"|"+detailTab+"|"+range.From+"|"+range.To+"|"+unit;
  if(detailKey==detailCacheKey)lines=detailCache;else{
  if(detailTab==0){
   lines.Add((T("FlowHeader"),null));foreach(var flow in sum.Flows.OrderByDescending(f=>f.In+f.Out))lines.Add((T("Cause_"+flow.Cause)+"    +"+N(flow.In*Factor(selected),2)+"    −"+N(flow.Out*Factor(selected),2),null));
   lines.Add((T("Adjustment",N(sum.Adjustment*Factor(selected),2)),null));
  }else if(detailTab==1){
   lines.Add((T("DetailRetention",LedgerMod.Settings.DetailDays,LedgerMod.Settings.DetailLimit),null));
   var targets=c.Maps.SelectMany(m=>m.listerThings.AllThings).GroupBy(t=>t.ThingID).ToDictionary(g=>g.Key,g=>g.First());
   foreach(var e in c.Data.Events.Where(e=>e.Resource==selected&&e.Scope==scope&&(mapChoice=="all"||e.Map==mapChoice)&&e.ClosedAt>sum.From&&e.ClosedAt<=sum.To).Reverse())
    lines.Add((TextCatalog.Day(e.Tick)+"  "+T("Cause_"+e.Cause)+"  "+(e.Delta>=0?"+":"")+N(e.Delta*Factor(selected),unit=="count"?0:2)+"  "+e.Actor+"  "+e.Facility,targets.GetValueOrDefault(e.ActorId)??targets.GetValueOrDefault(e.FacilityId)));
  }else{
   foreach(var m in c.Maps.Where(m=>mapChoice=="all"||InventoryReader.MapId(m)==mapChoice)){
    if(!c.Live.TryGetValue(InventoryReader.MapId(m),out var snap))continue;
    var chosen=c.Data.Scopes.FirstOrDefault(s=>s.Id==scope);
    foreach(var group in snap.Items.Where(i=>i.Resource==selected&&(scope=="map"||(scope=="stored"?i.Group.Length>0:chosen?.Groups.Contains(i.Group)==true))).GroupBy(i=>(i.Locator,i.Place)))
     lines.Add((c.Data.MapNames.GetValueOrDefault(InventoryReader.MapId(m),"")+" / "+group.Key.Place+" : "+N(group.Sum(i=>i.Count)),group.Key.Locator));
   }
  }
  detailCache=lines;detailCacheKey=detailKey;}
  Widgets.BeginScrollView(viewport,ref detailScroll,new Rect(0,0,viewport.width-20,lines.Count*28+5));
  for(int i=Math.Max(0,(int)(detailScroll.y/28)-1);i<Math.Min(lines.Count,(int)((detailScroll.y+viewport.height)/28)+2);i++){var line=lines[i];var lineRect=new Rect(4,i*28,viewport.width-35,27);Widgets.LabelEllipses(lineRect,line.Text);TooltipHandler.TipRegion(lineRect,line.Text);if(line.Target is {Destroyed:false} target&&target.MapHeld!=null&&Widgets.ButtonInvisible(lineRect)){CameraJumper.TryJumpAndSelect(target);}}
  Widgets.EndScrollView();
 }
}

