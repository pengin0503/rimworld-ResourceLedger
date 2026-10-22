namespace ResourceLedger.Core;
public enum Cause { Mining,DeepMining,Harvest,Logging,Production,RecipeMaterial,Construction,Recovery,Food,Drug,Medical,Surgery,Fuel,AnimalProduct,Purchase,Sale,Gift,Reward,TransferIn,TransferOut,ScopeIn,ScopeOut,Rot,Loss,Unclassified }
public class LedgerEvent { public string Id="",Map="",Scope="",Resource="",Actor="",Facility="",ActorId="",FacilityId=""; public long Tick,Delta,ClosedAt=-1; public Cause Cause; }
public class Flow { public Cause Cause; public long In,Out; }
public class Sample { public long Tick,Count; public bool Gap; }
public class Period { public long From,To,Start,End,In,Out,Adjustment; public bool Gap; public List<Flow> Flows=new(); }
public class Series { public string Map="",Scope="",Resource=""; public bool PendingGap; public List<Sample> Samples=new();public List<Period> Periods=new(); public List<LedgerEvent> Pending=new(); }
public class Retention { public int HourlyDays=60,DailyDays=600,DetailDays=7,DetailLimit=20000; }
public class Summary { public long From,To,Start,End,Increase,Decrease,Adjustment;public long Net=>End-Start;public bool Continuous,CompleteAttribution=false;public double? Percent=>Start==0?null:Net*100d/Start;public double DailyMean=>To>From?Net*60000d/(To-From):0;public List<Flow> Flows=new(); }
public class Forecast {public bool Available;public double Days,ObservedDays;public string Status="Insufficient";}
public enum AlertKind { Below,Above,DropAmount,DropPercent,Days }
public class AlertRule {public string Resource="",Map="",Scope="map";public AlertKind Kind;public double Threshold=50,Hysteresis=5;public long Cooldown=60000,LastFired=long.MinValue;public bool Enabled,Muted,Active;}

public class ScopeDefinition {public string Id="custom",Map="",Name="";public List<string> Groups=new();}
