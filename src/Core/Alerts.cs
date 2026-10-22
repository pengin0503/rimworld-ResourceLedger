namespace ResourceLedger.Core;
public static class AlertEvaluator {
 public static bool Evaluate(AlertRule r,Summary s,Forecast f,long tick){
  if(!r.Enabled||r.Muted||!s.Continuous)return false;
  double? v=r.Kind switch{AlertKind.Below or AlertKind.Above=>s.End,AlertKind.DropAmount=>Math.Max(0,-s.Net),AlertKind.DropPercent=>s.Percent.HasValue?Math.Max(0,-s.Percent.Value):null,AlertKind.Days=>f.Available?f.Days:null,_=>null};
  if(!v.HasValue)return false;bool lower=r.Kind is AlertKind.Below or AlertKind.Days;
  if(r.Active){bool recovered=lower?v>=r.Threshold+Math.Max(0,r.Hysteresis):v<=r.Threshold-Math.Max(0,r.Hysteresis);if(recovered){r.Active=false;return false;}}
  else if(lower?v<r.Threshold:v>r.Threshold)r.Active=true;
  if(!r.Active||!(lower?v<r.Threshold:v>r.Threshold))return false;if(r.LastFired!=long.MinValue&&tick-r.LastFired<Math.Max(1,r.Cooldown))return false;r.LastFired=tick;return true;
 }
}
