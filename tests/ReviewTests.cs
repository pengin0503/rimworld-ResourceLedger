using ResourceLedger.Core;

public static class ReviewTests {
 public static void Register(Action<string,Action> test){
  void Yes(bool value){if(!value)throw new Exception("Expected true");}
  test("Malformed optional save collections are rejected",()=>{
   foreach(var field in new[]{"NutritionFactors","ValueFactors","Topology","ActiveMaps"})
    Yes(!LedgerSerializer.TryLoad("{\"Schema\":1,\""+field+"\":null}",out _));
  });
  test("Malformed pending events and cause rows are rejected",()=>{
   foreach(var content in new[]{"\"Pending\":[null]","\"Periods\":[{\"From\":0,\"To\":1,\"Flows\":[null]}]"})
    Yes(!LedgerSerializer.TryLoad("{\"Schema\":1,\"Series\":{\"m|map|Steel\":{\"Map\":\"m\",\"Scope\":\"map\",\"Resource\":\"Steel\","+content+"}}}",out _));
  });
  test("Null persisted identifiers are rejected",()=>{
   foreach(var json in new[]{"{\"Schema\":1,\"Tracked\":[null]}","{\"Schema\":1,\"Events\":[{\"Id\":null}]}","{\"Schema\":1,\"Scopes\":[{\"Groups\":[null]}]}","{\"Schema\":1,\"Names\":{\"Steel\":null}}"})Yes(!LedgerSerializer.TryLoad(json,out _));
  });
  test("Duplicate sample ticks cannot reach map merging",()=>{
   var ledger=new Ledger();ledger.Observe("m","map",0,new Dictionary<string,long>{{"Steel",10}});ledger.Series.Values.Single().Samples.Add(new Sample{Tick=0,Count=20});
   Yes(!LedgerSerializer.TryLoad(LedgerSerializer.Save(ledger),out _));
  });
  test("Series identity must match its persisted lookup key",()=>{
   var ledger=new Ledger();ledger.Observe("m","map",0,new Dictionary<string,long>{{"Steel",10}});ledger.Series.Values.Single().Resource="WoodLog";
   Yes(!LedgerSerializer.TryLoad(LedgerSerializer.Save(ledger),out _));
  });
  test("Daily retention preserves missing intervals",()=>{
   var ledger=new Ledger();var series=new Series{Map="m",Scope="map",Resource="Steel",Periods=new(){new Period{From=0,To=2500,Start=100,End=110,In=10,Flows=new(){new Flow{Cause=Cause.Mining,In=10}}},new Period{From=5000,To=7500,Start=110,End=120,In=10,Flows=new(){new Flow{Cause=Cause.Mining,In=10}}}}};ledger.Series[Ledger.Key("m","map","Steel")]=series;
   ledger.Trim(120000,new Retention{HourlyDays=1,DailyDays=10});
   Yes(series.Periods.Single().Gap);Yes(!Queries.Summarize(series,0,7500).Continuous);Yes(series.Periods.Single().In==20);
  });
  test("Failed reads suppress forecasts and recovered interval flow",()=>{
   var ledger=new Ledger();ledger.Observe("m","map",0,new Dictionary<string,long>{{"Steel",100}});ledger.Observe("m","map",60000,new Dictionary<string,long>{{"Steel",80}});
   var series=ledger.Series.Values.Single();Yes(Queries.Forecast(series,60000,5).Available);
   ledger.MarkGap("m");
   Yes(!Queries.Forecast(series,60000,5).Available);
   ledger.Observe("m","map",62500,new Dictionary<string,long>{{"Steel",0}});Yes(series.Periods.Last().Gap);Yes(series.Periods.Last().Out==0);
   ledger.Observe("m","map",65000,new Dictionary<string,long>{{"Steel",10}});Yes(Queries.Summarize(series,62500,65000).Continuous);
  });
  test("Failed reads only invalidate their own map",()=>{
   var ledger=new Ledger();foreach(var map in new[]{"m","other"})ledger.Observe(map,"map",0,new Dictionary<string,long>{{"Steel",100}});
   ledger.MarkGap("m");
   Yes(ledger.Series[Ledger.Key("m","map","Steel")].PendingGap);Yes(!ledger.Series[Ledger.Key("other","map","Steel")].PendingGap);
  });

  test("Combined forecasts preserve an unreadable map",()=>{
   var a=new Ledger();var b=new Ledger();foreach(var ledger in new[]{a,b}){ledger.Observe("m","map",0,new Dictionary<string,long>{{"Steel",100}});ledger.Observe("m","map",60000,new Dictionary<string,long>{{"Steel",80}});}
   b.MarkGap("m");var merged=ViewMath.Merge(new[]{a.Series.Values.Single(),b.Series.Values.Single()});Yes(!Queries.Forecast(merged,60000,5).Available);
  });

  test("CSV does not invent zero stock outside recorded history",()=>{
   var ledger=new Ledger();ledger.Observe("m","map",100,new Dictionary<string,long>{{"Steel",10}});ledger.Observe("m","map",200,new Dictionary<string,long>{{"Steel",20}});
   foreach(var range in new[]{(0L,50L),(250L,300L)})foreach(var kind in new[]{"summary","history","causes","events"}){
    var rows=CsvExport.Export(ledger,ledger.Series.Values,range.Item1,range.Item2,kind).Split('\n',StringSplitOptions.RemoveEmptyEntries);
    Yes(rows.Length==1);
   }
  });
  test("CSV retains an actual zero stock observation",()=>{
   var ledger=new Ledger();ledger.Observe("m","map",100,new Dictionary<string,long>{{"Steel",0}});
   var rows=CsvExport.Export(ledger,ledger.Series.Values,100,100,"summary").Split('\n',StringSplitOptions.RemoveEmptyEntries);Yes(rows.Length==2);Yes(rows[1].Contains(",100,100,0,0,"));
  });

  test("Non-finite persisted conversion factors and alerts are rejected",()=>{
   foreach(var body in new[]{"\"ValueFactors\":{\"Steel\":1e999}","\"NutritionFactors\":{\"Steel\":1e999}","\"Alerts\":[{\"Threshold\":1e999}]"})Yes(!LedgerSerializer.TryLoad("{\"Schema\":1,"+body+"}",out _));
  });

 }
}
