using System.Globalization;
using ResourceLedger.Core;
using ResourceLedger.Game;
using RimWorld;
using UnityEngine;
using Verse;
namespace ResourceLedger.UI;

public static class LedgerDialogs {
 public static void DrawRecordingSettings(Rect rect,LedgerSettings settings){
  var l=new Listing_Standard();l.Begin(rect);
  l.CheckboxLabeled(TextCatalog.Get("CaptureCauses"),ref settings.CaptureCauses);
  l.CheckboxLabeled(TextCatalog.Get("CaptureTransfers"),ref settings.CaptureTransfers);
  l.Label(TextCatalog.Get("IntervalHours",settings.IntervalHours));settings.IntervalHours=(int)Math.Round(l.Slider(settings.IntervalHours,1,24));
  l.Label(TextCatalog.Get("HourlyDays",settings.HourlyDays));settings.HourlyDays=(int)Math.Round(l.Slider(settings.HourlyDays,1,120));
  l.Label(TextCatalog.Get("DailyDays",settings.DailyDays));settings.DailyDays=Math.Max(settings.HourlyDays,(int)Math.Round(l.Slider(settings.DailyDays,60,1200)));
  l.Label(TextCatalog.Get("DetailDays",settings.DetailDays));settings.DetailDays=(int)Math.Round(l.Slider(settings.DetailDays,0,30));
  l.Label(TextCatalog.Get("DetailLimit",settings.DetailLimit));settings.DetailLimit=(int)Math.Round(l.Slider(settings.DetailLimit,0,50000)/100)*100;
  l.Gap(12);l.Label(TextCatalog.Get("RecordingHint"));l.End();
 }
 public static void Recording()=>Find.WindowStack.Add(new SettingsWindow());
 private sealed class SettingsWindow:Window {
  public override Vector2 InitialSize=>new(720,680);
  public SettingsWindow(){doCloseButton=true;doCloseX=true;forcePause=true;optionalTitle=TextCatalog.Get("Recording");}
  public override void DoWindowContents(Rect rect)=>DrawRecordingSettings(new Rect(0,35,rect.width,rect.height-100),LedgerMod.Settings);
  public override void PostClose(){base.PostClose();LoadedModManager.GetMod<LedgerMod>().WriteSettings();}
 }
 public static void Tracking(LedgerGameComponent component)=>Find.WindowStack.Add(new TrackingWindow(component));
 private sealed class TrackingWindow:Window {
  readonly LedgerGameComponent component;string search="";Vector2 scroll;
  public override Vector2 InitialSize=>new(760,700);
  public TrackingWindow(LedgerGameComponent c){component=c;doCloseButton=true;doCloseX=true;forcePause=true;optionalTitle=TextCatalog.Get("Tracking");}
  public override void DoWindowContents(Rect rect){
   Widgets.Label(new Rect(0,34,rect.width,58),TextCatalog.Get("AutomaticTracking",component.Data.Tracked.Count));
   search=Widgets.TextField(new Rect(0,98,rect.width,30),search);
   var defs=component.Data.Tracked.Select(id=>DefDatabase<ThingDef>.GetNamedSilentFail(id)).Where(d=>d!=null&&(d.LabelCap.ToString().Contains(search,StringComparison.CurrentCultureIgnoreCase)||d.defName.Contains(search,StringComparison.OrdinalIgnoreCase))).OrderBy(d=>d.label).ToList();
   var viewport=new Rect(0,138,rect.width,rect.height-198);Widgets.BeginScrollView(viewport,ref scroll,new Rect(0,0,viewport.width-20,defs.Count*32));
   for(int i=Math.Max(0,(int)(scroll.y/32)-1);i<Math.Min(defs.Count,(int)((scroll.y+viewport.height)/32)+2);i++){var d=defs[i];Widgets.ThingIcon(new Rect(4,i*32,28,28),d);Widgets.LabelEllipses(new Rect(40,i*32,viewport.width-75,29),d.LabelCap+" ["+(d.modContentPack?.Name??"Core")+"]");}Widgets.EndScrollView();
  }
  public override void PostClose(){base.PostClose();component.SampleAll(true);}
 }
 public static void Scope(LedgerGameComponent c,Map map)=>Find.WindowStack.Add(new ScopeWindow(c,map));
 private sealed class ScopeWindow:Window {
  readonly LedgerGameComponent c;readonly Map map;readonly HashSet<string> chosen=new();string label="";Vector2 scroll;
  public override Vector2 InitialSize=>new(700,650);
  public ScopeWindow(LedgerGameComponent component,Map m){c=component;map=m;doCloseX=true;forcePause=true;optionalTitle=TextCatalog.Get("NewScope");}
  public override void DoWindowContents(Rect rect){
   label=Widgets.TextField(new Rect(0,35,rect.width,30),label);
   var groups=map.haulDestinationManager.AllGroupsListForReading;
   var viewport=new Rect(0,78,rect.width,rect.height-140);Widgets.BeginScrollView(viewport,ref scroll,new Rect(0,0,viewport.width-20,groups.Count*32));
   for(int i=0;i<groups.Count;i++){var g=groups[i];var id=InventoryReader.GroupId(g);bool selected=chosen.Contains(id);Widgets.CheckboxLabeled(new Rect(0,i*32,viewport.width-30,30),g.GetName(),ref selected);if(selected)chosen.Add(id);else chosen.Remove(id);}Widgets.EndScrollView();
   if(Widgets.ButtonText(new Rect(0,rect.height-42,rect.width,34),TextCatalog.Get("Create"))&&chosen.Count>0&&label.Trim().Length>0){
    c.Data.Scopes.Add(new ScopeDefinition{Id=Guid.NewGuid().ToString("N"),Map=InventoryReader.MapId(map),Name=label.Trim(),Groups=chosen.ToList()});c.SampleAll(true);Close();
   }
  }
 }
 public static void Alert(LedgerGameComponent c,string resource,string map,string scope)=>Find.WindowStack.Add(new AlertWindow(c,resource,map,scope));
 private sealed class AlertWindow:Window {
  readonly LedgerGameComponent c;readonly AlertRule rule;readonly string resource,map,scope;string threshold="",hysteresis="";float cooldown;bool delete;
  public override Vector2 InitialSize=>new(640,570);
  public AlertWindow(LedgerGameComponent component,string r,string m,string s){
   c=component;resource=r;map=m;scope=s;
   var existing=c.Data.Alerts.FirstOrDefault(a=>a.Resource==r&&a.Map==m&&a.Scope==s);
   rule=existing==null?new AlertRule{Resource=r,Map=m,Scope=s,Enabled=true}:System.Text.Json.JsonSerializer.Deserialize<AlertRule>(System.Text.Json.JsonSerializer.Serialize(existing,LedgerSerializer.Options),LedgerSerializer.Options)!;
   threshold=rule.Threshold.ToString(CultureInfo.CurrentCulture);hysteresis=rule.Hysteresis.ToString(CultureInfo.CurrentCulture);cooldown=rule.Cooldown/60000f;
   doCloseX=true;forcePause=true;optionalTitle=TextCatalog.Get("Alerts");
  }
  public override void DoWindowContents(Rect rect){
   var l=new Listing_Standard();l.Begin(new Rect(0,35,rect.width,rect.height-100));
   l.Label(c.Data.Names.GetValueOrDefault(resource,resource));l.Label(TextCatalog.Get("Map")+": "+c.Data.MapNames.GetValueOrDefault(map,map));
   l.CheckboxLabeled(TextCatalog.Get("Enabled"),ref rule.Enabled);l.CheckboxLabeled(TextCatalog.Get("Muted"),ref rule.Muted);
   if(l.ButtonText(TextCatalog.Get("Alert_"+rule.Kind)))Find.WindowStack.Add(new FloatMenu(Enum.GetValues<AlertKind>().Select(kind=>new FloatMenuOption(TextCatalog.Get("Alert_"+kind),()=>rule.Kind=kind)).ToList()));
   threshold=l.TextEntryLabeled(TextCatalog.Get("Threshold"),threshold);hysteresis=l.TextEntryLabeled(TextCatalog.Get("Hysteresis"),hysteresis);
   l.Label(TextCatalog.Get("Cooldown",TextCatalog.Number(cooldown,1)));cooldown=MathF.Round(l.Slider(cooldown,.1f,15)*10)/10;
   l.CheckboxLabeled(TextCatalog.Get("DeleteRule"),ref delete);l.Label(TextCatalog.Get("AlertHint"));l.End();
   if(Widgets.ButtonText(new Rect(0,rect.height-42,rect.width,34),TextCatalog.Get("Save"))){
    if(!delete&&(!double.TryParse(threshold,out var a)||!double.TryParse(hysteresis,out var b)||!double.IsFinite(a)||!double.IsFinite(b)||a<0||b<0)){Messages.Message(TextCatalog.Get("InvalidNumber"),MessageTypeDefOf.RejectInput,false);return;}
    c.Data.Alerts.RemoveAll(x=>x.Resource==resource&&x.Map==map&&x.Scope==scope);
    if(!delete){rule.Threshold=double.Parse(threshold);rule.Hysteresis=double.Parse(hysteresis);rule.Cooldown=(long)(cooldown*60000);rule.Active=false;rule.LastFired=long.MinValue;c.Data.Alerts.Add(rule);}c.Revision++;Close();
   }
  }
 }
}

