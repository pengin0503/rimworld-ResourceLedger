using Verse;
namespace ResourceLedger.UI;
public static class TextCatalog {
 public static string Get(string key,params object[] args){
  string text=("RL_"+key).Translate().ToString();
  return args.Length==0?text:string.Format(System.Globalization.CultureInfo.CurrentCulture,text,args);
 }
 public static string Number(double value,int places=0)=>value.ToString("N"+places,System.Globalization.CultureInfo.CurrentCulture);
 public static string Day(long tick)=>Get("Day",Number(tick/60000d,2));
}

