using ResourceLedger.Core;
using System.Text.Json;
using System.Globalization;

var tests = new List<(string,Action)>();
void Test(string name, Action run) => tests.Add((name,run));
void Eq<T>(T expected,T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
void Yes(bool value) { if(!value) throw new Exception("Expected true"); }
Ledger Seed(long tick=0,long count=100) {var l=new Ledger();l.Observe("m","map",tick,new Dictionary<string,long>{{"Steel",count}});return l;}
void Observe(Ledger l,long tick,long count,bool gap=false) => l.Observe("m","map",tick,new Dictionary<string,long>{{"Steel",count}},gap);
void Event(Ledger l,long tick,long delta,Cause cause,string id="")=>l.Record(new LedgerEvent{Tick=tick,Map="m",Scope="map",Resource="Steel",Delta=delta,Cause=cause,Id=id});
Summary Sum(Ledger l,long from=0,long to=60000)=>Queries.Summarize(l.Series.Values.Single(),from,to);

Test("Gross flows survive opposing changes",()=>{var l=Seed();Event(l,5,100,Cause.Production);Event(l,6,-60,Cause.RecipeMaterial);Observe(l,2500,140);var s=Sum(l);Eq(100L,s.Increase);Eq(60L,s.Decrease);Eq(40L,s.Net);Eq(0L,s.Adjustment);});
Test("Unclassified positive residual",()=>{var l=Seed();Event(l,5,10,Cause.Mining);Observe(l,2500,125);Eq(15L,Sum(l).Adjustment);Eq(25L,Sum(l).Increase);});
Test("Unclassified negative residual",()=>{var l=Seed();Observe(l,2500,70);Eq(-30L,Sum(l).Adjustment);Eq(30L,Sum(l).Decrease);});
Test("Opposing unknowns are not called complete",()=>{var l=Seed();Observe(l,2500,100);Yes(!Sum(l).CompleteAttribution);});
Test("Duplicate event IDs count once",()=>{var l=Seed();Event(l,5,20,Cause.Mining,"x");Event(l,5,20,Cause.Mining,"x");Observe(l,2500,120);Eq(20L,Sum(l).Increase);});
Test("Tracking starts at actual stock",()=>{var l=Seed(10000,500);Eq(0,l.Events.Count);Eq(0L,Sum(l,0,20000).Net);Yes(!Sum(l,0,20000).Continuous);});
Test("Gap does not become consumption",()=>{var l=Seed();Observe(l,2500,120);Observe(l,5000,0,true);Yes(!Sum(l).Continuous);Eq(0L,l.Series.Values.Single().Periods.Last().Out);});
Test("A period after gap can be continuous",()=>{var l=Seed();Observe(l,2500,0,true);Observe(l,5000,10);Yes(Sum(l,2500,5000).Continuous);});
Test("Zero baseline has no percent",()=>{var l=Seed(0,0);Observe(l,2500,10);Eq<double?>(null,Sum(l).Percent);});
Test("Period snaps to recorded endpoints",()=>{var l=Seed();Observe(l,2500,110);Observe(l,5000,130);var s=Sum(l,2000,5000);Eq(2500L,s.From);Eq(20L,s.Net);});
Test("Same tick updates do not erase pending flow",()=>{var l=Seed();Event(l,0,10,Cause.Mining);Observe(l,2500,110);Eq(10L,Sum(l).Increase);});
Test("Different scopes are independent",()=>{var l=Seed();l.Observe("m","stored",0,new Dictionary<string,long>{{"Steel",20}});l.Observe("m","stored",2500,new Dictionary<string,long>{{"Steel",10}});Eq(2,l.Series.Count);Eq(100L,l.Series.Values.Single(s=>s.Scope=="map").Samples.Last().Count);});
Test("Forecast needs one full day",()=>{var l=Seed();Observe(l,2500,90);Yes(!Queries.Forecast(l.Series.Values.Single(),2500,5).Available);});
Test("Forecast uses actual days",()=>{var l=Seed(0,100);Observe(l,60000,80);var f=Queries.Forecast(l.Series.Values.Single(),60000,5);Yes(f.Available);Eq(4d,f.Days);Eq(1d,f.ObservedDays);});
Test("Forecast nondecreasing is not finite",()=>{var l=Seed();Observe(l,60000,120);Yes(!Queries.Forecast(l.Series.Values.Single(),60000,5).Available);});
Test("Forecast refuses gaps",()=>{var l=Seed();Observe(l,60000,80,true);Yes(!Queries.Forecast(l.Series.Values.Single(),60000,5).Available);});
Test("Daily rollup preserves gross flows",()=>{var l=Seed();for(int i=1;i<=24*62;i++){Event(l,i*2500-1,2,Cause.Mining);Event(l,i*2500-1,-1,Cause.Food);Observe(l,i*2500,100+i);}var before=Sum(l,0,62*60000);l.Trim(62*60000,new Retention());var after=Sum(l,0,62*60000);Eq(before.Net,after.Net);Eq(before.Increase,after.Increase);Eq(before.Decrease,after.Decrease);Yes(l.Series.Values.Single().Periods.Count < 24*62);});
Test("Detail cap preserves totals",()=>{var l=Seed();for(int i=1;i<=30;i++)Event(l,i,1,Cause.Mining);Observe(l,2500,130);l.Trim(2500,new Retention{DetailLimit=10});Eq(10,l.Events.Count);Eq(30L,Sum(l).Increase);});
Test("Serialization preserves pending events",()=>{var l=Seed();Event(l,5,12,Cause.Mining);Yes(LedgerSerializer.TryLoad(LedgerSerializer.Save(l),out var restored));Observe(restored!,2500,112);Eq(12L,Sum(restored!).Increase);});
Test("Future schema rejected",()=>{Yes(!LedgerSerializer.TryLoad("{\"Schema\":999}",out _));});
Test("Removed definition names survive serialization",()=>{var l=Seed();l.Names["Mod.Steel"]="特殊鋼";Yes(LedgerSerializer.TryLoad(LedgerSerializer.Save(l),out var r));Eq("特殊鋼",r!.Names["Mod.Steel"]);});
Test("Below alert cooldown and hysteresis",()=>{var r=new AlertRule{Enabled=true,Kind=AlertKind.Below,Threshold=50,Hysteresis=10,Cooldown=100};var s=new Summary{Continuous=true,End=40};Yes(AlertEvaluator.Evaluate(r,s,new Forecast(),1000));Yes(!AlertEvaluator.Evaluate(r,s,new Forecast(),1050));s.End=55;Yes(!AlertEvaluator.Evaluate(r,s,new Forecast(),1200));Yes(r.Active);s.End=65;Yes(!AlertEvaluator.Evaluate(r,s,new Forecast(),1300));Yes(!r.Active);s.End=45;Yes(AlertEvaluator.Evaluate(r,s,new Forecast(),1400));});
Test("Muted alerts never fire",()=>{var r=new AlertRule{Enabled=true,Muted=true,Threshold=100};Yes(!AlertEvaluator.Evaluate(r,new Summary{Continuous=true,End=0},new Forecast(),1000));});
Test("Gap suppresses alerts",()=>{var r=new AlertRule{Enabled=true,Threshold=100};Yes(!AlertEvaluator.Evaluate(r,new Summary{Continuous=false,End=0},new Forecast(),1000));});
Test("Drop percent ignores zero baseline",()=>{var r=new AlertRule{Enabled=true,Kind=AlertKind.DropPercent,Threshold=10};Yes(!AlertEvaluator.Evaluate(r,new Summary{Continuous=true,Start=0,End=0},new Forecast(),1000));});
Test("CSV escapes Japanese quotes and newlines",()=>{Eq("\"鋼,\\\"特殊\\\"\n材\"".Replace("\\\"","\"\""),CsvExport.Cell("鋼,\"特殊\"\n材"));});
Test("CSV protects spreadsheet formulas",()=>{Eq("'=1+1",CsvExport.Cell("=1+1"));});
Test("Operation decomposition keeps same-resource gross",()=>{var f=OperationDiff.Compute(new Dictionary<string,long>{{"Steel",100}},new Dictionary<string,long>{{"Steel",100}},new Dictionary<string,long>{{"Steel",30}});Eq(30L,f.Single().In);Eq(30L,f.Single().Out);});
Test("No-change operation gives no flows",()=>{Eq(0,OperationDiff.Compute(new Dictionary<string,long>{{"Steel",100}},new Dictionary<string,long>{{"Steel",100}},new Dictionary<string,long>()).Count);});
Test("Combined maps require common period",()=>{Yes(!Queries.Combine(new[]{new Summary{From=0,To=100,Continuous=true,Start=10,End=20},new Summary{From=50,To=100,Continuous=true,Start=30,End=40}}).Continuous);});

Test("Relative plot has no invented zero baseline",()=>{var s=new Series{Samples=new(){new Sample{Tick=0,Count=0},new Sample{Tick=2500,Count=10}}};Yes(ViewMath.Points(s,0,2500,1,true).All(p=>double.IsNaN(p.Y)));});
Test("Plot applies unit factor and gaps",()=>{var s=new Series{Samples=new(){new Sample{Tick=0,Count=10},new Sample{Tick=2500,Count=20,Gap=true}}};var p=ViewMath.Points(s,0,2500,.05,false);Eq(.5,p[0].Y);Yes(p[1].Gap);});
Test("Map merge uses common recorded endpoints",()=>{var a=Seed();Observe(a,2500,110);var b=Seed(2500,20);Observe(b,5000,30);var m=ViewMath.Merge(new[]{a.Series.Values.Single(),b.Series.Values.Single()});Eq(1,m.Samples.Count);Eq(130L,m.Samples[0].Count);});
Test("Daily bars retain increase and decrease",()=>{var l=Seed();Event(l,1,20,Cause.Mining);Event(l,2,-10,Cause.Food);Observe(l,2500,110);var b=ViewMath.Bars(l.Series.Values.Single(),0,2500);Eq(20L,b.Single().In);Eq(10L,b.Single().Out);});
Test("CSV includes events at the opening tick when committed later",()=>{var l=Seed();Event(l,0,10,Cause.Mining);Observe(l,2500,110);var csv=CsvExport.Export(l,l.Series.Values,0,2500,"events");Yes(csv.Contains("Mining"));});
Test("Same-tick composition change is remembered until next sample",()=>{var l=Seed();Observe(l,0,0,true);Observe(l,2500,0);Yes(!Sum(l).Continuous);});
Test("Forecast refuses stale history",()=>{var l=Seed();Observe(l,60000,80);Yes(!Queries.Forecast(l.Series.Values.Single(),180001,5).Available);});
Test("Malformed nested save is rejected",()=>{Yes(!LedgerSerializer.TryLoad("{\"Schema\":1,\"Series\":{\"bad\":null}}",out _));});
Test("CSV unit conversion matches displayed quantities",()=>{var l=Seed();Observe(l,2500,120);var csv=CsvExport.Export(l,l.Series.Values,0,2500,"summary",factors:new Dictionary<string,double>{{"Steel",.5}},unit:"value");var fields=csv.Split('\n')[1].TrimEnd('\r').Split(',');Eq("value",fields[4]);Eq("50",fields[7]);Eq("10",fields[11]);});
Test("CSV incomplete periods leave aggregate deltas blank",()=>{var l=Seed();Observe(l,2500,0,true);var csv=CsvExport.Export(l,l.Series.Values,0,2500,"summary");var fields=csv.Split('\n')[1].TrimEnd('\r').Split(',');Eq("",fields[9]);Eq("",fields[10]);Eq("",fields[11]);});
Test("Restored pending event is included in committed details",()=>{var l=Seed();Event(l,0,10,Cause.Mining);Yes(LedgerSerializer.TryLoad(LedgerSerializer.Save(l),out var restored));Observe(restored!,2500,110);Yes(CsvExport.Export(restored!,restored!.Series.Values,0,2500,"events").Contains("Mining"));});
Test("Random mixed known and unknown operations conserve stock",()=>{var random=new Random(42);var l=Seed(0,10000);long count=10000;for(int i=1;i<=1200;i++){int inc=random.Next(0,40),dec=random.Next(0,40);if(i%4!=0){Event(l,i*2500-1,inc,Cause.Mining);Event(l,i*2500-1,-dec,Cause.Food);}count+=inc-dec;Observe(l,i*2500,count);var s=Sum(l,0,i*2500);Eq(s.Net,s.Increase-s.Decrease);if(i%100==0){Yes(LedgerSerializer.TryLoad(LedgerSerializer.Save(l),out var restored));l=restored!;l.Trim(i*2500,new Retention{DetailLimit=10});}}});
Test("Delayed composition gaps also break graph lines",()=>{var l=Seed();Observe(l,0,0,true);Observe(l,2500,0);Yes(ViewMath.Points(l.Series.Values.Single(),0,2500,1,false)[1].Gap);});
Test("Merge keeps gross flows across different sampling intervals",()=>{var a=Seed();Event(a,1,1,Cause.Mining);Observe(a,2500,101);Event(a,2501,1,Cause.Mining);Observe(a,5000,102);var b=Seed();Event(b,1,5,Cause.Mining);Observe(b,5000,105);var s=Queries.Summarize(ViewMath.Merge(new[]{a.Series.Values.Single(),b.Series.Values.Single()}),0,5000);Eq(7L,s.Increase);Eq(7L,s.Net);Yes(s.Continuous);});
Test("Daily chart marks an unrecorded interval as a gap",()=>{var s=new Series{Periods=new(){new Period{From=0,To=2500,Start=100,End=110,In=10},new Period{From=5000,To=7500,Start=110,End=120,In=10}}};Yes(ViewMath.Bars(s,0,7500).Single().Gap);});
Test("Daily chart preserves unexplained adjustments",()=>{var s=new Series{Periods=new(){new Period{From=0,To=2500,Start=100,End=110,In=10,Adjustment=10}}};Eq(10L,ViewMath.Bars(s,0,2500).Single().Adjustment);});
FeatureTests.Register(Test);
if(args.Contains("--contracts"))ContractTests.Register(Test);
int failed=0;
foreach(var test in tests){try{test.Item2();Console.WriteLine("PASS "+test.Item1);}catch(Exception e){failed++;Console.WriteLine("FAIL "+test.Item1+": "+e.GetBaseException());}}
Console.WriteLine($"{tests.Count-failed}/{tests.Count} passed");
if(failed==0&&args.Contains("--benchmark")){
 var b=new Ledger();var resources=Enumerable.Range(0,20).Select(i=>"Resource"+i).ToArray();var counts=resources.ToDictionary(k=>k,k=>100000L);b.Observe("m","map",0,counts);
 var sw=System.Diagnostics.Stopwatch.StartNew();
 for(int hour=1;hour<=80*24;hour++){
  foreach(var resource in resources){b.Record(new LedgerEvent{Map="m",Scope="map",Resource=resource,Tick=hour*2500-1,Delta=4,Cause=Cause.Mining});b.Record(new LedgerEvent{Map="m",Scope="map",Resource=resource,Tick=hour*2500-1,Delta=-3,Cause=Cause.RecipeMaterial});counts[resource]++;}
  b.Observe("m","map",hour*2500,counts);if(hour%24==0)b.Trim(hour*2500,new Retention());
 }
 sw.Stop();var payload=LedgerSerializer.Save(b);var single=b.Series.Values.First();var mergeWatch=System.Diagnostics.Stopwatch.StartNew();var combined=ViewMath.Merge(new[]{single,single});mergeWatch.Stop();
 Console.WriteLine("BENCHMARK "+System.Text.Json.JsonSerializer.Serialize(new{scope="core-only, no game scanning",resources=20,days=80,elapsed_ms=sw.Elapsed.TotalMilliseconds,save_bytes=System.Text.Encoding.UTF8.GetByteCount(payload),history_points=b.Series.Values.Sum(s=>s.Samples.Count),detail_records=b.Events.Count,merge_ms=mergeWatch.Elapsed.TotalMilliseconds,merged_points=combined.Samples.Count}));
}
return failed==0?0:1;

