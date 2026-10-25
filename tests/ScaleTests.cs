using ResourceLedger.Core;

/// <summary>Issues #8/#9: unchanged stock is stored once, retention only touches new periods, and views stay exact.</summary>
public static class ScaleTests {
 public static void Register(Action<string,Action> test){
  void Yes(bool value,string what="Expected true"){if(!value)throw new Exception(what);}
  void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; actual {actual}");}
  Dictionary<string,long> C(long n)=>new(){{"Steel",n}};
  bool Aligned(Series s)=>s.Periods.Count==0?s.Samples.Count<=1:s.Samples.Count==s.Periods.Count+1&&s.Samples[0].Tick==s.Periods[0].From
   &&s.Periods.Select((p,i)=>s.Samples[i+1].Tick==p.To&&s.Samples[i+1].Count==p.End&&s.Samples[i].Count==p.Start).All(x=>x);

  test("Unchanged stock extends one period instead of adding one per sample",()=>{
   var l=new Ledger();for(int h=0;h<=48;h++)l.Observe("m","map",h*2500L,C(7));
   var s=l.Series.Values.Single();Eq(1,s.Periods.Count);Eq(2,s.Samples.Count);Eq(120000L,s.Periods[0].To);Yes(Aligned(s));
  });
  test("Summaries inside an unchanged period are continuous and exact",()=>{
   var l=new Ledger();for(int h=0;h<=48;h++)l.Observe("m","map",h*2500L,C(7));
   var sum=Queries.Summarize(l.Series.Values.Single(),30000,90000);
   Yes(sum.Continuous);Eq(30000L,sum.From);Eq(90000L,sum.To);Eq(7L,sum.Start);Eq(7L,sum.End);Eq(0L,sum.Increase);
  });
  test("A change after unchanged hours starts a new period at the last sample",()=>{
   var l=new Ledger();for(int h=0;h<=10;h++)l.Observe("m","map",h*2500L,C(7));
   l.Record(new LedgerEvent{Id="m1",Map="m",Scope="map",Resource="Steel",Tick=26000,Delta=3,Cause=Cause.Mining});l.Observe("m","map",27500,C(10));
   var s=l.Series.Values.Single();Eq(2,s.Periods.Count);Eq(25000L,s.Periods[1].From);Eq(3L,s.Periods[1].In);Yes(Aligned(s));
   var sum=Queries.Summarize(s,10000,27500);Yes(sum.Continuous);Eq(3L,sum.Increase);Eq(10000L,sum.From);
  });
  test("Hourly chart buckets show stock for every hour of an unchanged period",()=>{
   var l=new Ledger();for(int h=0;h<=10;h++)l.Observe("m","map",h*2500L,C(7));
   var buckets=ChartMath.Buckets(l.Series.Values.Single(),0,25000,2500);
   Eq(10,buckets.Count);Yes(buckets.All(b=>b.Stock==7&&!b.Gap&&b.Increase==0));
   Eq(1,ViewMath.Bars(l.Series.Values.Single(),0,25000).Count);
  });
  test("Merging an unchanged map with a changing map keeps every recorded tick",()=>{
   var a=new Ledger();var b=new Ledger();
   for(int h=0;h<=6;h++){a.Observe("m","map",h*2500L,C(5));b.Observe("m","map",h*2500L,C(10+h));}
   var merged=ViewMath.Merge(new[]{a.Series.Values.Single(),b.Series.Values.Single()});
   Eq(7,merged.Samples.Count);Eq(21L,merged.Samples[^1].Count);Yes(merged.Periods.All(p=>!p.Gap));
   var sum=Queries.Summarize(merged,0,15000);Yes(sum.Continuous);Eq(6L,sum.Net);
  });
  test("Retention rolls each period once and keeps totals, alignment and day splits",()=>{
   var random=new Random(7);var l=new Ledger();long count=100;
   for(int h=0;h<=24*20;h++){
    if(random.Next(3)==0){long d=random.Next(-5,6);if(d!=0)l.Record(new LedgerEvent{Id="e"+h,Map="m",Scope="map",Resource="Steel",Tick=h*2500L-1,Delta=d,Cause=Cause.Mining});count+=d+random.Next(-1,2);}
    l.Observe("m","map",h*2500L,C(Math.Max(0,count)));count=Math.Max(0,count);
   }
   var s=l.Series.Values.Single();var before=Queries.Summarize(s,0,20*60000);
   // Trim every hour as the game does, with a small hourly window.
   for(int h=24;h<=24*20;h++)l.Trim(h*2500L,new Retention{HourlyDays=2,DailyDays=600});
   var after=Queries.Summarize(s,0,20*60000);
   Eq(before.Increase,after.Increase);Eq(before.Decrease,after.Decrease);Eq(before.Net,after.Net);Eq(before.Continuous,after.Continuous);
   Yes(Aligned(s),"periods and samples aligned");
   // Rolled periods never span two days unless they are unchanged.
   Yes(s.Periods.Where(p=>p.Daily&&!p.Flat).All(p=>p.From/60000==(p.To-1)/60000||p.To-p.From<=60000));
   Yes(s.Periods.Count(p=>!p.Daily)<=24*2+1);
  });
  test("A long unchanged period is split at the day boundary when it is rolled up",()=>{
   var l=new Ledger();for(int h=0;h<=24*3;h++)l.Observe("m","map",h*2500L,C(7));
   l.Record(new LedgerEvent{Id="x",Map="m",Scope="map",Resource="Steel",Tick=180001,Delta=1,Cause=Cause.Mining});l.Observe("m","map",182500,C(8));
   l.Trim(182500+3*60000,new Retention{HourlyDays=1,DailyDays=600});
   var s=l.Series.Values.Single();Yes(Aligned(s));Yes(s.Periods.All(p=>p.Daily));
   // Earlier days stay unchanged periods ending at a day boundary; the changed hour joins only its own day.
   var day3=s.Periods.Single(p=>p.In==1);Eq(180000L,day3.From);Yes(s.Periods.Where(p=>p.Flat).All(p=>p.To%60000==0));
   Eq(180000L,s.Periods.Where(p=>p.Flat).Max(p=>p.To));Eq(0L,s.Periods[0].From);
  });
  test("Histories saved before alignment are realigned by retention",()=>{
   var l=new Ledger();var s=new Series{Map="m",Scope="map",Resource="Steel",Periods=new(){new Period{From=0,To=2500,Start=1,End=2,In=1,Flows=new(){new Flow{Cause=Cause.Mining,In=1}}}}};
   l.Series[Ledger.Key("m","map","Steel")]=s;l.Trim(2500,new Retention());Yes(Aligned(s));
  });
  test("CSV exports ranges that lie inside an unchanged period",()=>{
   var l=new Ledger();for(int h=0;h<=240;h++)l.Observe("m","map",h*2500L,C(7));
   string[] Rows(long from,long to,string kind)=>CsvExport.Export(l,l.Series.Values,from,to,kind).Split('\n',StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
   var history=Rows(300000,600000,"history");Eq(1,history.Length);Yes(history[0].Contains(",300000,600000,7,7,0,0,0,true,"),history[0]);
   var summary=Rows(310000,320000,"summary");Eq(1,summary.Length);Yes(summary[0].Contains(",310000,320000,7,7,0,0,0,true,"),summary[0]);
  });
  test("Graph range keeps unchanged history before a later change",()=>{
   var l=new Ledger();for(int h=0;h<=240;h++)l.Observe("m","map",h*2500L,C(7));
   l.Record(new LedgerEvent{Id="c",Map="m",Scope="map",Resource="Steel",Tick=601000,Delta=1,Cause=Cause.Mining});l.Observe("m","map",602500,C(8));
   Eq(((long,long)?)(302500L,602500L),Queries.KnownRange(l.Series.Values.Single(),302500,602500));
   Eq(((long,long)?)null,Queries.KnownRange(l.Series.Values.Single(),700000,800000));
  });
  test("Unchanged stock of hundreds of types stays small to save",()=>{
   var l=new Ledger();var counts=Enumerable.Range(0,300).ToDictionary(i=>"R"+i,i=>1L);
   for(int h=0;h<=24*80;h++){l.Observe("m","map",h*2500L,counts);l.Observe("m","stored",h*2500L,counts);if(h%24==0)l.Trim(h*2500L,new Retention());}
   var payload=LedgerSerializer.Save(l);Yes(payload.Length<200_000,"payload "+payload.Length);
   Yes(LedgerSerializer.TryLoad(payload,out var restored));Yes(restored!.Series.Values.All(Aligned));
  });
 }
}
