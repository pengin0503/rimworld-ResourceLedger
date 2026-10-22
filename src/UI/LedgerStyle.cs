using UnityEngine;
using Verse;
namespace ResourceLedger.UI;

public static class LedgerStyle {
 public static readonly Color Positive=new(.4f,.92f,.58f),Negative=new(1,.53f,.43f),Stock=new(.47f,.82f,1),Warning=new(1,.82f,.36f);
 public static void Metric(Rect rect,string label,string value,Color color,string tip){
  var font=Text.Font;var anchor=Text.Anchor;var previous=GUI.color;
  Widgets.DrawBoxSolid(rect,new Color(.13f,.15f,.18f));Widgets.DrawBoxSolid(new Rect(rect.x,rect.y,rect.width,3),color);
  Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.UpperLeft;GUI.color=new Color(.8f,.83f,.87f);Widgets.Label(new Rect(rect.x+10,rect.y+7,rect.width-20,22),label);
  Text.Font=GameFont.Medium;GUI.color=color;if(Text.CalcSize(value).x>rect.width-20)Text.Font=GameFont.Small;
  Widgets.LabelEllipses(new Rect(rect.x+10,rect.y+28,rect.width-20,32),value);TooltipHandler.TipRegion(rect,tip+"\n"+label+": "+value);
  GUI.color=previous;Text.Font=font;Text.Anchor=anchor;
 }
}
