using ResourceLedger.Core;

/// <summary>Bookkeeping repeated by every cause capture stays cheap without changing what is recorded.</summary>
public static class CaptureCostTests {
 /// <summary>The implementation before key iteration was unrolled, kept as the reference result.</summary>
 private static List<(string,long,long)> PreviousDiff(IReadOnlyDictionary<string,long> before,IReadOnlyDictionary<string,long> after,IReadOnlyDictionary<string,long> consumed){
  var result=new List<(string,long,long)>();foreach(var resource in before.Keys.Concat(after.Keys).Concat(consumed.Keys).Distinct()){
   long delta=after.GetValueOrDefault(resource)-before.GetValueOrDefault(resource),removed=Math.Max(0,consumed.GetValueOrDefault(resource)),gained=delta+removed;
   if(gained<0){removed-=gained;gained=0;}if(gained!=0||removed!=0)result.Add((resource,gained,removed));
  }return result;
 }
 public static void Register(Action<string,Action> test){
  void Yes(bool value,string what="Expected true"){if(!value)throw new Exception(what);}
  void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}; actual {actual}");}
  Dictionary<string,long> C(long steel,long wood)=>new(){{"Steel",steel},{"Wood",wood}};

  test("A complete baseline is rechecked without building series keys",()=>{
   var l=new Ledger();var resources=Enumerable.Range(0,500).Select(i=>"R"+i).ToHashSet(StringComparer.Ordinal);int reads=0;
   var counts=resources.ToDictionary(k=>k,_=>1L);
   IReadOnlyDictionary<string,long> Read(){reads++;return counts;}
   l.EnsureBaselineLazy("m","map",0,Read,resources);Eq(1,reads);Eq(500,l.Series.Count);
   for(int i=0;i<10;i++){l.EnsureBaselineLazy("m","map",10,Read,resources);l.EnsureBaselineLazy("m","map",20,Read,resources,historical:true);}
   Eq(1,reads);
   // A key per resource and call (about 25 KB) is what the check used to allocate; a set lookup allocates nothing.
   var before=GC.GetAllocatedBytesForCurrentThread();
   for(int i=0;i<200;i++)l.EnsureBaselineLazy("m","map",30,Read,resources);
   var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
   Yes(allocated<200_000,"complete baselines still allocate "+allocated+" bytes");
   // Another scope or map is checked on its own.
   l.EnsureBaselineLazy("m","stored",0,Read,resources);Eq(2,reads);Eq(1000,l.Series.Count);
  });
  test("A grown resource list still gets a baseline for the new type",()=>{
   var l=new Ledger();l.Discover(new[]{"Steel"});
   l.EnsureBaseline("m","map",0,C(5,7),l.Tracked);
   l.Discover(new[]{"Wood"});l.EnsureBaseline("m","map",100,C(6,7),l.Tracked);
   Eq(0L,l.Series[Ledger.Key("m","map","Steel")].Samples.Single().Tick);Eq(100L,l.Series[Ledger.Key("m","map","Wood")].Samples.Single().Tick);
  });
  test("A skipped backdated custom baseline is still created later",()=>{
   var l=new Ledger();var resources=new[]{"Steel"};var counts=C(5,7);
   l.EnsureBaseline("m","custom",50,counts,resources,historical:true);Eq(0,l.Series.Count);
   l.EnsureBaseline("m","custom",100,counts,resources);Eq(100L,l.Series.Values.Single().Samples.Single().Tick);
  });
  test("Tracked lookups follow discovery and a replaced list",()=>{
   var l=new Ledger();Yes(!l.IsTracked("Steel"));l.Discover(new[]{"Steel"});Yes(l.IsTracked("Steel"));
   l.Tracked=new(){"Wood"};Yes(!l.IsTracked("Steel"));Yes(l.IsTracked("Wood"));
   l.Tracked.Add("Steel");Yes(l.IsTracked("Steel"));
   Eq(1,l.Discover(new[]{"Steel","Rifle","Rifle"}).Count);Eq(3,l.Tracked.Count);
  });
  test("Swapping a same-sized resource set member still creates its baseline",()=>{
   var l=new Ledger();var resources=new HashSet<string>{"Steel"};var counts=C(5,7);
   l.EnsureBaseline("m","map",0,counts,resources);
   resources.Remove("Steel");resources.Add("Wood");
   l.EnsureBaseline("m","map",100,counts,resources);
   Eq(100L,l.Series[Ledger.Key("m","map","Wood")].Samples.Single().Tick);
  });
  test("Tracked lookups follow in-place edits of the tracked list",()=>{
   var l=new Ledger();l.Discover(new[]{"Steel"});Yes(l.IsTracked("Steel"));
   l.Tracked[0]="Wood";Yes(l.IsTracked("Wood"));Yes(!l.IsTracked("Steel"));
   Eq(1,l.Discover(new[]{"Steel","Wood"}).Count);Eq(2,l.Tracked.Count);
   l.Tracked.Remove("Wood");Yes(!l.IsTracked("Wood"));Yes(l.IsTracked("Steel"));
   l.Tracked.Clear();Yes(!l.IsTracked("Steel"));
  });
  test("Operation flows match the previous key order and amounts",()=>{
   var random=new Random(7);
   for(int round=0;round<500;round++){
    Dictionary<string,long> Make(int keys)=>Enumerable.Range(0,keys).Select(_=>"R"+random.Next(12)).Distinct().ToDictionary(k=>k,_=>(long)random.Next(-3,20));
    var before=Make(random.Next(0,10));var after=Make(random.Next(0,10));var consumed=Make(random.Next(0,5));
    var expected=PreviousDiff(before,after,consumed);var actual=OperationDiff.Compute(before,after,consumed);
    Yes(expected.SequenceEqual(actual),"flows differ in round "+round);
   }
  });
 }
}
