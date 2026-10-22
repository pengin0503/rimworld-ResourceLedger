# Resource Ledger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** RimWorld 1.7で資源在庫・原因別収支・グラフ・通知・所在・CSVを提供する独立MODを作る。
**Architecture:** ゲーム非依存の台帳・検索計算と、実本体APIに依存する在庫読取・Harmony捕捉・画面を分離する。在庫スナップショットと確定した原因イベントを時間境界で照合する。
**Tech Stack:** .NET 10、C#、RimWorld 1.7.4985 rev1269、UnityStubs、Harmony 1.7版。外部テストサービスやCIを使わずローカル検証する。
**Spec:** ../specs/2026-10-22-resource-ledger-design.md
**Execution:** ユーザーが2026-10-22に「実装まで進めてOK」と指示済み。計画から実装まで同一セッションで続行し、サブエージェントは使わない。

## Global Constraints
- 対象: RimWorld 1.7を優先。1.6対応は本版の必須要件に含めない。
- 作業方法: サブエージェントを使わず、同一セッションで実装・検証する。
- 途中セーブから導入し、導入後の履歴を記録する。導入以前の履歴は復元しない。
- 日本語と英語の表示に対応する。ゲームの資源生産量や消費量、作業指示は変更しない。
- 詳細グラフ用の時間単位履歴は60日、日次集計は600日を既定保存期間とする。
- 詳細な行動明細は直近7日または20,000件の先に達した上限まで保持する。
- ゲームインストール先・既存セーブ・MOD設定は変更しない。配布物に本体DLLやHarmonyを同梱しない。

## Review Focus
- 同一資源の材料消費と製品生成が相殺されても増加・減少を消さない（Task 1/3）。
- セーブ直前の未確定イベントを失わず、ロード時の初期化を入手として加算しない（Task 2/3）。
- 集計範囲・マップ構成の変更を消費や枯渇として表示しない（Task 1/2/4）。
- 定義・保管場所・担当者が消えても過去の名前と数値を表示する（Task 2/4）。
- 収納移動・分割結合・二重パッチが収支を水増ししない（Task 3）。

## File Structure
ResourceLedger/
- src/Core/{Models,Ledger,Queries,Alerts,CsvExport}.cs: ゲーム非依存のデータ・照合・集計・通知・出力。
- src/Game/{LedgerMod,LedgerGameComponent,InventoryReader,OperationCapture,CapturePatches}.cs: 設定、保存、在庫、捕捉。
- src/UI/{LedgerWindow,LedgerGraph,LedgerDialogs,TextCatalog}.cs: 一覧・詳細・設定・日本語英語表示。
- mod/About/About.xml, mod/Defs/Ledger.xml, mod/Languages/: ゲーム定義と翻訳。
- tests/{CoreTests,GameContractTests}.cs: 実ロジックと実DLLに対する検証。
- tools/{verify,package}.py: 配布物検証・ソースおよび導入用ZIP作成。
- README.md, VALIDATION.md: 利用方法・実測と検証限界。
- ResourceLedger.csproj, tests/ResourceLedger.Tests.csproj, Directory.Build.props: 構成。
- work/progress.md: 進捗・判断・検証記録。配布物に個人のゲームパスを含めない。

### Task 1: 台帳・期間比較・原因別集計
**Files:** src/Core/Models.cs, Ledger.cs, Queries.cs; tests/CoreTests.cs
**Interfaces:** Ledger.Observe(string map,string scope,int tick,IReadOnlyDictionary<string,long> counts,bool discontinuity=false); Ledger.Record(LedgerEvent item); Queries.Summarize(Series series,int from,int to) -> Summary.
- [ ] 100在庫→生産100・消費60→140在庫でGrossIn=100,GrossOut=60,Net=40となるテストを先に書いて失敗を確認。
- [ ] 未分類差、相殺、0期首、開始前／不連続、同時刻、期間端点、複数拠点の構成変更を追加して失敗を確認。
- [ ] モデル、イベント保留、照合と期間結果を実装する。
- [ ] ローカルテストを実行し全件合格を確認する。

### Task 2: 保持・保存・通知・CSV
**Files:** src/Core/Alerts.cs, CsvExport.cs; src/Game/LedgerGameComponent.cs, LedgerMod.cs; tests/CoreTests.cs
**Interfaces:** Ledger.Trim(int now,Retention policy); Queries.Forecast(Series series,int now,int days) -> Forecast; AlertEvaluator.Evaluate(AlertRule rule,Summary summary,Forecast forecast,int tick) -> bool; CsvExport.Write(...) -> string.
- [ ] 時間→日次集約で収支維持、明細削除で集計維持、JSON保存復元で保留イベント維持の失敗テストを書く。
- [ ] 記録不足・下限／上限・ヒステリシス・クールダウン・0期首・CSV特殊文字と未記録の失敗テストを書く。
- [ ] 保存版、保持、通知、CSVを実装。JSONをGameComponentのScribe文字列としてセーブに埋め込む。
- [ ] 不明な将来スキーマは変更せず保持し、UIは読取不可状態を表示。
- [ ] テストで保存復元・照合・通知・出力の合格を確認する。

### Task 3: 実ゲームAPI・在庫・原因捕捉
**Files:** src/Game/InventoryReader.cs, OperationCapture.cs, CapturePatches.cs; tests/GameContractTests.cs
**Interfaces:** InventoryReader.Read(Map map,ScopeDefinition scope,ISet<string> resources) -> InventorySnapshot; OperationCapture.Begin(Map map,Cause positive,Cause negative,Pawn actor=null) -> IDisposable; CapturePatches.Install(Harmony harmony) -> diagnostics.
- [ ] 本体DLLの存在メソッド・戻り型・引数と、パッチ対象の整合を検査する失敗テストを書く。
- [ ] 純粋な操作差分変換で入出力相殺・二重計上抑制・例外後の解放を検証する。
- [ ] 同期処理は操作前後の在庫差を捕捉。製造完了toilは実行されるinitActionをラップし、材料の実減少を別に捕捉して同一資源の入出力も分離する。
- [ ] 採掘、深部採掘、伐採収穫、製造調理、食事薬物、建築投入、解体回収、治療、燃料、畜産、売買、移出入、損失の実在メソッドを確認してパッチする。未確認は明示した未分類へ残す。
- [ ] スタック合流中の破棄を抑制し、ネストした操作は外側だけで収支を確定する。
- [ ] マップ在庫／保管場所内／指定保管場所を独立集計。所属ポーンの携帯、未完成品の原料を二重なく扱う。
- [ ] 実DLL参照でビルドし、メタデータ契約・可能な範囲のゲーム型実行を検証する。

### Task 4: 日本語UI・グラフ・関連機能
**Files:** src/UI/*.cs, mod/Defs/Ledger.xml, mod/Languages/*; tests/CoreTests.cs
**Interfaces:** LedgerWindow : MainTabWindow; LedgerGraph.Draw(Rect rect,IReadOnlyList<Series> series,int from,int to,bool relative,bool bars); TextCatalog.Get(string key,params object[] values) -> string.
- [ ] 表示単位変換、検索・並べ替え・欠落区間・比較系列の変換、日英キーの対応、CSVと画面の共通集計を検証する。
- [ ] 一覧、列切替、追跡選択、カテゴリ/MOD絞り込み、お気に入り、期間指定、拠点/範囲選択を実装。
- [ ] 折れ線・収支棒・ホバー数値・凡例・相対比較・任意期間を実装。
- [ ] 原因別と保存済み明細、現在の所在と選択移動、通知編集、記録設定、診断、CSVを実装。
- [ ] 調整差0でも完全捕捉を保証しない旨を画面とREADMEに表示する。
- [ ] XML・翻訳・実DLLビルドを検証。ゲーム画面の実機確認ができない場合はその事実を記録する。

### Task 5: 全体レビュー・配布
**Files:** README.md, VALIDATION.md, tools/verify.py, tools/package.py, outputs/*.zip
**Interfaces:** python tools/verify.py; python tools/package.py -> install/source zip.
- [ ] 台帳のランダム入出庫照合テスト、通知境界・保持・操作差分の回帰テストを追加する。
- [ ] 本体版とハッシュ、ビルド・テスト結果、捕捉対象メソッドと未対応項目、負荷計測をVALIDATION.mdへ記録する。
- [ ] 自分で仕様対コードのレビューを別工程として行う。重大な不足は修正して再検証する。
- [ ] 導入用ZIPはAbout/Defs/Languages/1.7/Assembliesとユーザー説明だけを含める。ソースZIPは参照DLL・bin/obj・一時ファイルを除く。
- [ ] 配布ZIPを展開検証し、全テストとReleaseビルドを最終実行する。
- [ ] 成果物を保存し、導入用ZIP・ソースZIP・検証限界をユーザーに渡す。

## Self-review / execution adaptation
全仕様の各節をTask 1〜5に対応付けた。計画の型と役割を揃えた。
この作業場所には既存Gitリポジトリがないため、専用ResourceLedgerディレクトリで実装し、進捗ファイルとソースZIPを残す。
計画レビューのために再停止せず、ユーザーの「実装まで進めてOK」を優先して継続する。

