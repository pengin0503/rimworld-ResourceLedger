# Validation / 検証結果
対象リリース: Resource Ledger 0.1.0（試験版）
日付: 2026-10-22

## 環境
- Windows上の.NET SDK 10.0.401 / .NET 10.0.12。
- 実際に参照した本体: RimWorld 1.7.4985 rev1269。
- ゲーム同梱UnityStubsと、Harmony MODの1.7用0Harmony.dllを参照。
- ゲーム本体、Harmony、.NETのDLLは配布物へ含めない。

参照ファイルのSHA-256:
| ファイル | SHA-256 |
|---|---|
| Assembly-CSharp.dll | 0BE7A7EA4B3111D4430F45B36C234A5E43287880B0514B23915F2EF9A0629E16 |
| UnityStubs.dll | EA962C9274514E0633D4E4C5471A1A98AFF5FC52AF20CA2E506C16DCD8BE6F81 |
| 0Harmony.dll | C8EF7856B2BE3CF24B345C9B5C2F197F68FCF92649635064FCE7B3CCC8F77CB0 |

## 自動検証
最終実行結果は、このファイルの末尾の「Final run」に記載する。
基礎テスト、実本体のAPI契約、実DLLに対するHarmony適用・解除、XMLと日英キーの静的検査を含む。
テストはソースのtests/Program.csとtests/ContractTests.csで再実行できる。
Harmonyの適用試験は、ゲーム本体を起動したものではなく、独立したテストプロセスで実DLLを読み込んだ試験である。

## 原因捕捉の接続点
- Mineable.TrySpawnYield、CompDeepDrill.TryProducePortion。
- Toils_Recipeの製造完了、ConsumeIngredients、GenRecipe.MakeRecipeProductsの生成物。
- QuestManager.Notify_PlantHarvested、CompEggLayer.ProduceEgg、CompHasGatherableBodyResource.Gathered。
- Thing.Ingested、TendUtility.DoTend、製造作業中のBill_Medical。
- CompRefuelable.Refuel、Toils_Refuel.FinalizeRefueling。
- ThingOwnerの具体TryAddとTryTransferToContainer、Frameの失敗・破棄、Building.Destroy。
- TradeDeal.TryExecute。贈与モードはGiftに分類。
- QuestPart_DropPodsの配送文脈、DropPodUtilityの配置。
- 所属ポーンの到着・退出、運搬・収納への出し入れ、CompLaunchable.TryLaunch。
- Thing.Destroy。スタック合流中の破棄を抑制し、腐敗処理中の消失を区別。

署名とパッチ適用を検証したことは、あらゆるゲーム状況や他MODとの組合せで正しい原因になるという保証ではない。
未対応の原因は調整差として残り、同量の増加と減少が相殺された場合は内訳を復元できない。
独自収納や自動工場の対応を実績として記載していない。

## 保存と負荷
既定: 時間単位60日、日次600日、詳細7日または20,000件。
集計ベンチマークは20資源・80日を模擬し、保持後のJSONサイズと処理時間を測定する。
これは台帳の計算・保持・シリアライズだけを対象とし、実マップの走査やTPS・FPSは測定していない。
ゲーム内の実際の負荷は「診断」に表示される記録時間と、通常プレイで確認する必要がある。
診断の操作累計時間は在庫読取部分の時間であり、MOD全体のCPU時間とは異なる。

## 実機未検証
- 実際のゲーム画面とレイアウト、日本語フォント、操作感。
- バニラ相当の新規コロニーでの作業実行、途中セーブへの追加、削除後ロード。
- 個別MODとの収納・自動工場・物流・グラヴシップの互換性。
- 大規模コロニーでのTPS・FPS、実セーブの増加量、長時間プレイ。

上記のため0.1.0は試験版であり、「ゲームへ入れて動作確認済み」とはしていない。

## Final run


- Release build: success, 0 warnings / 0 errors.
- Full suite: 53/53 passed.
- Capture targets: 41 methods resolved, patched and unpatched in an isolated test CLR.
- Static artifact check: 8 XML files, 117 matching English/Japanese translation keys, 2 compiled PE assemblies, no implementation stubs.

Core-only benchmark (20 resources, 80 game days, one map scope; excludes game scanning):

```json
{
  "scope": "core-only, no game scanning",
  "resources": 20,
  "days": 80,
  "elapsed_ms": 921.5649,
  "json_bytes": 7697054,
  "history_points": 29220,
  "detail_records": 6720,
  "merge_ms": 9.928,
  "merged_points": 1461
}
```

This JSON size is not a measurement of a live RimWorld save. The default map and storage scopes retain separate histories.
