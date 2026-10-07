# CoreBeasts Design Notes

将来対応として記録しておく設計メモです。
現時点で着手しないが、次の改修で判断材料になるものをまとめます。

## Core Beast thumbnail readability

- 現在のヴォルクスは内部線と装甲分割が多く、スマートフォンの縮小表示では細部が潰れやすい
- 次期デザインでは外形シルエットを優先する
- 顔、胸部コア、脚、尻尾などの主要部位を大きく明確にする
- 細かな内部線と装甲分割を減らす
- primary／secondary 属性色を載せる面積を広げる
- 約 86 x 105 pt のサムネイルでも属性と個体を識別できることを評価基準にする
- 2048 x 2048 の 6 レイヤー構成を維持する
- 全レイヤーのピクセル位置一致と、上下左右 8% 以上の余白を維持する
- 現在の PNG、マスク、シェーダーは今回変更しない

## Battle core rules (確定)

UIやシーンに依存しないバトル中核ロジックとして確定した仕様です。
実装は `Assets/CoreBeasts/Scripts/Battle`（アセンブリ `CoreBeasts.Battle`）にあります。

### 対戦の形

- プレイヤー・CPUとも 7 体編成（編成枠 `SquadFormation.SlotCount` と同数）
- 同じ個体（`OwnedCoreBeast.InstanceId`）を 1 編成内へ重複登録できない
- 最大 7 ラウンド、先に 4 勝した側がマッチ勝利
- 4 勝到達時点で即終了し、それ以降の選出・解決は受け付けない
- 7 ラウンドを終えてどちらも 4 勝していなければマッチ引き分け

### 選出

- 各ラウンドで双方が未使用ユニットを 1 体ずつ選ぶ
- 選出は同時かつ非公開。CPU の選出内容は解決前には公開せず、
  解決後に `BattleSession.History` から参照する
- 一度使用した個体は、その対戦中は再使用できない
- 受け付けられない選出（相手編成の個体、存在しない ID、使用済み、二重選択、
  終了後の操作）はセッションの状態を一切変更せず、`BattleError` で理由を返す

### 属性相性

三すくみは Red → Green → Blue → Red の順に勝ちます。

属性は集合として扱い、一次・二次の並び順で結果は変わりません。

「属性有利」は、**自分の属性のうち 1 つでも相手の属性すべてに勝てること**と定義します。
相手の属性の一部にしか勝てない属性は有利とみなしません。

- 片側だけが属性有利 → その側の属性勝利（POWER は見ない）
- 双方に有利な組み合わせがある（相殺）→ POWER 比較
- どちらにも有利な組み合わせがない → POWER 比較
- POWER も同値 → ラウンド引き分け

判定例:

| プレイヤー | CPU | 結果 |
| --- | --- | --- |
| Red | Green | Red の属性勝利 |
| Green | Blue | Green の属性勝利 |
| Blue | Red | Blue の属性勝利 |
| Red | Red | POWER 比較 |
| Red/Blue | Green | Red が Green（相手の全属性）に勝つため Red/Blue 側の属性勝利 |
| Red/Blue | Green/Blue | どちらも相手の全属性は制圧できないため POWER 比較 |
| Red/Blue | Red/Blue | 同上。POWER 比較 |

- ラウンド引き分けは、どちらの勝利数にも加算しない
- 決着理由は `RoundDecision`（属性相性 / POWER 比較）として記録する

### CPU

- 初期 CPU は戦略を持たず、未使用ユニットから一様にランダム選択する
- 選択器 `IBattleUnitSelector` は自分の未使用候補だけを受け取る。
  プレイヤーの選択は引数に現れないため、相手の手を見て選ぶことは構造的にできない
- 乱数は `IRandomSource` を注入する。`UnityEngine.Random` へは直接依存しない
- 同じ seed と同じ候補順なら結果を再現できる

### 今回の範囲

今回は純粋ロジックのみです。次の項目は未実装です。

- バトル画面 UI、立ち絵演出、アニメーション、エフェクト、サウンド
- スキル効果、バフ・デバフ、COREの戦闘利用、リーダー効果
- 編成コスト制限、白属性・黒属性
- CPU の戦略 AI、相手編成の情報公開
- 報酬、セーブデータ、永続化

## Battle UI (今回の実装)

`Assets/CoreBeasts/Scripts/Battle/UI`（アセンブリ `CoreBeasts.Battle.UI`）。
中核ロジック（`CoreBeasts.Battle`）へは触れず、その結果を表示するだけの層です。

### 責務の分け方

| 型 | 役割 |
| --- | --- |
| `BattleFlowCoordinator` | 画面の状態遷移。Unity非依存で、そのままテストできる |
| `IBattleMatchSource` / `BattleMatchSource` | 1マッチぶんの編成と選択器を用意する境界 |
| `PlayerSideLoader` | 保存済み編成 → 対戦用の7体 |
| `ICpuSideBuilder` / `RosterCpuSideBuilder` | CPU編成の生成。所持一覧から無作為に7体 |
| `BattleUnitCard` / `BattleSideRoster` | 中核用の値と表示用の値を1組で持つ |
| `BattleScreenController` | 各Viewとの受け渡しと、Coroutineの管理 |
| `BattleCombatantView` | 出場中1体の立ち絵と情報 |
| `BattleSquadTrayView` / `BattleTraySlotView` | プレイヤー7枠の表示と入力 |
| `EnemySquadStatusView` / `EnemyMarkerView` | CPU側7枠の非公開状態 |
| `BattleScoreView` / `BattleResultView` | ラウンド・スコア・結果 |
| `BattleFxPlayer` | 演出のON/OFFとCoroutine |
| `BattleSettingsView` | 右上の歯車から開く設定パネル（FX / HOME / CLOSE） |
| `BattleInputGate` | 「設定パネル表示中」「演出中」を入力可否へ重ねるだけの純粋関数 |
| `BattleTextCatalog` (`IBattleTextSource`) | バトル画面の表示文字列 |

勝敗判定はUI側に一切ありません。`BattleSession` / `BattleRules` の結果
（`RoundResult.Winner` と `RoundDecision`）を文字と色へ写すだけです。

### 画面の状態

`Loading → SquadRequired | Selecting → Resolving → ShowingResult → (Selecting | MatchFinished)`

- `Selecting` だけがトレイのタップと DEPLOY を受け付ける
- `Resolving` / `ShowingResult` の間は、選択も DEPLOY も REMATCH も通らない
- `MatchFinished` は REMATCH と画面遷移のみ
- `BattleFlowCoordinator.AbortPresentation()` が、演出を中断しても
  進行を矛盾なく確定させる（`OnDisable`・シーン離脱用）

### CPU選出の非公開

- ラウンド開始時に `BattleSession.SelectCpuUnit()` まで済ませる
- 進行役はCPUの選出個体を公開しない。公開されるのは解決後の `RoundResult` だけ
- `EnemySquadStatusView.Refresh(usedCount, hasPendingSelection)` は
  数と真偽値しか受け取らない。個体を渡す口が構造的に無い
- CPU側の内部IDには `cpu:` を付け、プレイヤー編成のIDと衝突させない

### 編成データ

- 保存先は `SquadRepositoryProvider.Shared` に統一（UnitSetとBattleで同じ実体）
- 7枠が埋まっていない・IDが所持一覧に無い場合は対戦を開始せず、
  `SQUAD REQUIRED` 画面から `UNIT SET` / `HOME` へ誘導する
- 永続化は未実装のまま。アプリを終了すると編成は消える（既存仕様のまま）

### 演出

- FX ON: 浮き上がり → 接触 → フラッシュ → 勝敗の強調 → 復帰（約1.0秒）＋バナー0.45秒
- FX OFF: 待たずに最終状態だけを作る。バナーは0.2秒だけ出す
- どちらでもゲーム結果は変わらない（`BattleFxPlayer` はセッションへ触れない）
- 立ち絵の左右反転（ミラー）は `localScale.x` の符号で保ち、演出でも戻さない

### 勝敗バッジ（自軍トレイ）

- 戦闘済みの個体だけ、カード右上に `W` / `L` / `D` を出す（24pxの角丸）
  - Win `#42C77A` / Loss `#E15B64` / Draw `#9AA4B5`、文字は白
  - 未戦闘・選択しただけで未解決のあいだは出さない
- 結果はUIで判定しない。`RoundResult.Winner` を
  `BattleSlotOutcomes.FromWinner()` で言い換えるだけ
  （Player→Win / Cpu→Loss / Draw→Draw）
- `BattleOutcomeLedger` が PLAYER側 `BattleUnit.InstanceId` をキーに控える。
  REMATCHは `Begin()` を通るため、バッジもまとめて消える
- 枠の状態（`BattleSlotState`: Available / Selected / Used）と
  結果（`BattleSlotOutcome`: None / Win / Loss / Draw）は別の列挙。
  「選択中だが未解決」と「引き分け」を取り違えないため混ぜない
- 使用済みカードの暗転はカード全体の `CanvasGroup` で行うため、
  バッジ側に `ignoreParentGroups = true` の `CanvasGroup` を置いて暗転から外す。
  バッジは `Lift`（見た目の根）の最後の子に置き、番号・立ち絵・属性バーを隠さない
- 4勝でマッチが決まるため、7体すべてにバッジが付くのは
  7ラウンド戦い切った場合（引き分けが続いた場合など）のみ
- CPU側には付けない

### バッジを出すタイミング（Pending / Presented）

DEPLOYを押した直後にW/L/Dが出ると、演出を見る前に勝敗が分かってしまいます。
そのため「解決済みの結果」と「表示してよい結果」を分けています。

| 段階 | 状態 | 今回のバッジ |
| --- | --- | --- |
| DEPLOY | `Resolving` | 出さない（`PendingOutcome`へ） |
| 接触・フラッシュ・勝敗強調 | `Resolving` | 出さない |
| 結果バナー | `ShowingResult` | 出さない |
| 演出と結果表示の完了 | `ShowingResult` | ここで公開（`PresentedOutcomes`へ） |
| 次ラウンド | `Selecting` | 出したまま |

- `BattleFlowCoordinator.Deploy()` は結果を `pendingResult` へ置くだけで、控えへは入れない
- `BattleFlowCoordinator.PublishPendingOutcome()` だけが公開の入口。
  未公開が無ければ何もしないので、二重公開は起きない
- `Outcomes`（= `PresentedOutcomes`）が、トレイへ渡す唯一の口。
  `RefreshAll` は常にここを渡すため、`Resolving` 中に未公開の結果が漏れる経路が無い
- 過去ラウンドのバッジは `PresentedOutcomes` に残り続けるので、演出中も消えない
- FX OFF でも順序は同じ。`BattleFxPlayer.BannerHoldSeconds` の待ちが短くなるだけで、
  公開は常に `PublishPendingOutcome()` が起点（演出設定では前後しない）
- `AbortPresentation()`（`OnDisable` / HOME遷移 / 演出中断）も公開まで行う。
  「出したのに結果が分からない個体」を残さないため
- REMATCH（`Begin()`）は `PresentedOutcomes` と `pendingResult` の両方を捨てる

### 設定パネル（右上の歯車）

ヘッダーへ常時出していた HOME と FX ON/OFF を、右上の歯車の中へ移しました。
画面を離れる操作を常時 Primary で見せないためです。

Battleヘッダー:

- 中央: `BATTLE`
- 下段: `ROUND n / 7` とスコア
- 右上: 歯車（120 x 120 Canvas単位 ≒ 49pt）

パネルの中身:

```
SETTINGS
FX          ON / OFF
HOME
CLOSE
```

- `BattleSettingsView` が開閉と通知だけを持つ。進行にも勝敗にも触れない
- 歯車で開閉、`CLOSE` またはパネル外（Backdrop）のタップで閉じる
- `FX` は `FxToggleRequested`、`HOME` は `HomeRequested` を投げるだけ。
  実際の切り替えと片付けは `BattleScreenController` が行い、
  Homeへの遷移は従来どおり `SceneLoadButton` が担う
- 初期状態は非表示。`OnDisable` でも必ず閉じる
- パネル表示中はユニット選択とDEPLOYを受け付けない
  （Backdropで物理的にふさぎ、`BattleInputGate` でも通さない）
- `Resolving` / `ShowingResult` と演出Coroutineの実行中は歯車を押せない
- FX設定の永続化は従来どおり行わない

歯車アイコンは Unicode の「⚙」を使いません（LiberationSans SDF に無く、
未収録文字の警告が出るため）。外部画像も足さず、Unity UI の `Image` だけで組みます。

```
SettingsButton (Image + Button, RaycastTarget = ON)
└ GearIcon
  ├ Tooth1〜Tooth8 … 中心から半径32の位置へ45度おきに置いた小さな矩形
  ├ Ring          … 直径64の外周
  └ Hub           … 直径26の中央円（背景色で抜く）
```

- タップを受けるのはルートの `Button` だけ。子の `Image` は
  すべて `RaycastTarget = OFF`
- 文字を一切使わないため、フォントの収録状況に依存しない

### 今回やっていないこと

- CPUの戦略AI（無作為選出のみ）
- 本格的なエフェクト・サウンド・アニメーション用アセット
- FX設定の永続化（バトル画面の中だけで保持）
- CPU側の勝敗バッジ
- 編成セットの切り替え（UnitSetのSETは現在のセット名を出すだけ）

## Phase 6: Playable Vertical Slice

戦闘だけで終わらず、獲得した報酬が次の編成へ戻る一周を成立させます。

```
Home → Battle → Reward → Gacha → Acquisition → Collection → UnitSet → Battle
```

### 経済値

- 初期所持: 100 CORE COIN
- ガチャ1回: 100 CORE COIN
- 勝利報酬: 30
- 引き分け報酬: 20
- 敗北報酬: 10
- 初期所持個体: カタログ先頭の7体。8体目以降はガチャで解放する
- 重複獲得: 同じ個体の所持数を増やす。勝敗能力はまだ増やさない

値は `GameEconomy` だけを正本とし、画面やバトルへ数値を散らしません。

### 保存

- `PlayerProfile` を `PlayerPrefs` のJSONへ保存する
- コイン、戦績、所持個体ID、個体ごとの所持数を保持する
- 編成セットも `PlayerPrefsSquadRepository` で保持する
- テストは `InMemoryPlayerProfileRepository` / `InMemorySquadRepository` へ差し替える
- 新規プレイヤーには所持7体とSET 1を作り、起動直後からBattleへ入れる

### Home内の全画面遷移

Homeシーン内に次の全画面ページを置き、シーン再読み込みなしで切り替えます。

- Home: コイン、戦績、BATTLE / UNIT SET / GACHA / COLLECTION
- Reward: 未確認の戦闘報酬をまとめて表示
- Gacha: コストと残高を表示し、1回召喚
- Acquisition: NEW / DUPLICATE、個体、属性、所持数を表示
- Collection: 所持個体と未獲得個体を一覧表示

報酬は試合決着時に即保存します。Reward画面は保存済み結果の確認なので、
画面遷移中やアプリ終了でコインが失われません。

## モバイルUI原則

画面を作るときの共通の決めごとです。BattleとUnitSetはこれに合わせています。

- 上部はタイトル・状態・スコアなどの**情報表示**に使う
- 頻繁に触る主要操作は、親指が届きやすい**下部**へ置く
- 設定・画面離脱など低頻度の操作は、**右上の設定メニュー**へ集約する
- 破壊的な操作や画面離脱を、常時 Primary として見せない
- タップ領域は最低 44pt（推奨 48〜56pt）
- Safe Area とホームインジケーターを避ける（`SafeAreaController` の内側へ置く）

Canvas は 1080 x 1920 参照・Match 0.5 のため、iPhone 16 Pro では
Canvas 1単位 ≒ 0.41pt です。44pt は約107単位、48〜56pt は約117〜136単位にあたります。
テストは `MobileLayoutMetrics` でこの換算を行い、定数を埋め込まずに確かめます。

## UnitSet の下部 Action Dock

上部にあった HOME / SET / SAVE SET を、画面下部の Action Dock へまとめました。

上部:

- 中央に `UNIT SET` タイトルのみ（操作ボタンは置かない）
- ユニット詳細表示はそのまま維持

下部:

```
MY SQUAD
[7枠]
┌ ActionDock ─────────────┐
│ [ HOME ]      [ SET 1 ] │
│ [      SAVE SET       ] │
└─────────────────────────┘
```

- `SAVE SET` が Primary（青・全幅）。`HOME` と `SET` は Secondary（暗色）
- ボタンはいずれも高さ120単位（≒49pt）で、推奨の48〜56ptに収まる
- Action Dock は Safe Area の内側で下端から12単位浮かせ、
  ホームインジケーターと重ならない
- Dock はスクロールさせず下部へ固定。MY SQUAD の7枠にも
  Roster のスクロール領域にも重ならない
- `SAVE SET` の完了メッセージ（Toast）は画面中央のままで、Dockも7枠も隠さない
- `SET` はセット切り替えが未実装のため、現在のセット名をToastで出すだけ
  （上部から撤去した SET バッジの代わり）
- ドラッグ・タップ除外・シアンチェックなど、既存の編成操作は変更していない
