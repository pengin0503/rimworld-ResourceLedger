namespace ResourceLedger.Core;

internal static class SaveValidation {
 public static bool Valid(Ledger? ledger){
  if(ledger is not {Series:not null,Names:not null,MapNames:not null,ResourceMods:not null,Topology:not null,
   NutritionFactors:not null,ValueFactors:not null,SeenIds:not null,Events:not null,Tracked:not null,
   Favorites:not null,ActiveMaps:not null,Alerts:not null,Scopes:not null})return false;
  if(!Strings(ledger.Tracked)||!Strings(ledger.Favorites)||!Strings(ledger.ActiveMaps))return false;
  if(new[]{ledger.Names,ledger.MapNames,ledger.ResourceMods,ledger.Topology}.Any(d=>d.Values.Any(v=>v==null)))return false;
  if(ledger.NutritionFactors.Values.Concat(ledger.ValueFactors.Values).Any(v=>!double.IsFinite(v)))return false;
  if(ledger.Events.Any(e=>!Event(e)))return false;
  if(ledger.Alerts.Any(a=>a==null||a.Resource==null||a.Map==null||a.Scope==null||!double.IsFinite(a.Threshold)||!double.IsFinite(a.Hysteresis)))return false;
  if(ledger.Scopes.Any(s=>s==null||s.Id==null||s.Map==null||s.Name==null||s.Groups==null||!Strings(s.Groups)))return false;
  foreach(var (key,s) in ledger.Series){
   if(s is not {Map:not null,Scope:not null,Resource:not null,Samples:not null,Periods:not null,Pending:not null})return false;
   if(key!=Ledger.Key(s.Map,s.Scope,s.Resource))return false;
   long previous=-1;
   foreach(var sample in s.Samples){
    if(sample==null||sample.Tick<=previous||sample.Count<0)return false;
    previous=sample.Tick;
   }
   long end=-1;
   foreach(var period in s.Periods){
    if(period==null||period.From<0||period.From<end||period.To<=period.From||period.Start<0||period.End<0||period.In<0||period.Out<0||period.Flows==null)return false;
    if(period.Flows.Any(f=>f==null||f.In<0||f.Out<0))return false;
    end=period.To;
   }
   if(s.Pending.Any(e=>!Event(e)||e.Map!=s.Map||e.Scope!=s.Scope||e.Resource!=s.Resource))return false;
  }
  return true;
 }
 private static bool Strings(IEnumerable<string> values)=>values.All(v=>v!=null);
 private static bool Event(LedgerEvent? e)=>e is {Id:not null,Map:not null,Scope:not null,Resource:not null,Actor:not null,Facility:not null,ActorId:not null,FacilityId:not null}&&e.Tick>=0&&e.ClosedAt>=-1;
}
