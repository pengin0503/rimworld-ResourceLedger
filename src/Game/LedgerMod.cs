using HarmonyLib;
using UnityEngine;
using Verse;
using ResourceLedger.UI;
namespace ResourceLedger.Game;
public sealed class LedgerSettings : ModSettings {
 public int IntervalHours=1,HourlyDays=60,DailyDays=600,DetailDays=7,DetailLimit=20000;
 public bool CaptureCauses=true,CaptureTransfers=true;
 public int Visibility,GroupHours;public bool ShowIncrease=true,ShowDecrease=true,ShowNet;
 public string SortColumn="Name",DisplayUnit="count";public bool SortDescending,FavoritesOnly;
 public List<string> HiddenColumns=new(){"Start","End","Percent"};
 public override void ExposeData(){
  Scribe_Values.Look(ref IntervalHours,"intervalHours",1);Scribe_Values.Look(ref HourlyDays,"hourlyDays",60);Scribe_Values.Look(ref DailyDays,"dailyDays",600);
  Scribe_Values.Look(ref DetailDays,"detailDays",7);Scribe_Values.Look(ref DetailLimit,"detailLimit",20000);
  Scribe_Values.Look(ref CaptureCauses,"captureCauses",true);Scribe_Values.Look(ref CaptureTransfers,"captureTransfers",true);
  Scribe_Values.Look(ref SortColumn,"sortColumn","Name");Scribe_Values.Look(ref DisplayUnit,"displayUnit","count");Scribe_Values.Look(ref SortDescending,"sortDescending",false);Scribe_Values.Look(ref FavoritesOnly,"favoritesOnly",false);
  Scribe_Values.Look(ref Visibility,"visibility",0);Scribe_Values.Look(ref GroupHours,"groupHours",0);
  Scribe_Values.Look(ref ShowIncrease,"showIncrease",true);Scribe_Values.Look(ref ShowDecrease,"showDecrease",true);Scribe_Values.Look(ref ShowNet,"showNet",false);
  Scribe_Collections.Look(ref HiddenColumns,"hiddenColumns",LookMode.Value);HiddenColumns??=new(){"Start","End","Percent"};
 }
}
public sealed class LedgerMod : Mod {
 public static LedgerSettings Settings=new();
 public LedgerMod(ModContentPack pack):base(pack){Settings=GetSettings<LedgerSettings>();CapturePatches.Install(new Harmony("pengin0503.resourceledger"));}
 public override string SettingsCategory()=>TextCatalog.Get("Title");
 public override void DoSettingsWindowContents(Rect rect){LedgerDialogs.DrawRecordingSettings(rect,Settings);}
}

