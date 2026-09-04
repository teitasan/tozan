# Tozan

Unity 6.6 / URP の登山・登攀プロトタイプ。Godot 版 `climbing_physics_prototype` との手触り比較用。

- エンジン: Unity 6000.6.0f1 / URP
- **Player（本命）:** 公式 ECS Platformer Sample（`com.unity.charactercontroller` 1.4.2）
- **表示モデル（NaturalRockSandbox）:** Erika（Mixamo Humanoid、`Assets/Characters/Erika/ErikaCharacterMesh.prefab`）。ECS 物理・入力・状態機械は公式 Platformer のまま。ProtoCharacter `CharacterMesh.prefab` は rollback 参照として保持。
- 表示PrefabはECSのMeshRootに合わせたwrapper Animatorを持ち、生成時にSkinnedMeshRendererの最下点を`y=0`へ自動補正する。これにより足元とカプセルの接地基準を一致させる。
- 参考: Dynamic Parkour System（`ClimbingSandbox`）、Starter Assets 地上クローン
- 自然岩ゲート: `Assets/Scenes/NaturalRockSandbox.unity`

## STEP 15 — マーカーなし自然岩

`NaturalRockSandbox` は **Climbable Tag / HandlePoints / Traverser コンポーネントなし** のコース。

| 能力 | 方式 | 状態 |
|------|------|------|
| Ledge hang / shimmy | 公式 `LedgeGrabState.LedgeDetection`（形状のみ） | Overhang lip で PlayMode 検証 |
| Mantle | `LedgeStandingUpState` + `TozanMantleUtility`（スイープ付き時間補間、テレポートなし） | Overhang lip で PlayMode 検証 |
| Surface climb | 公式 `ClimbingState` + `TozanSurfaceProbe`（`GeometryOnly`） | 確認用大壁（12m×8m）で壁面 WASD 移動 + 登攀モーション（ClipIndex 10）を PlayMode 検証 |

### 壁面移動（確認用縦切り）

`Rock_VerticalWall`（幅 12m / 高さ 8m / 正面 z≈1.275）で、WASD で壁へ接近 → 自動で壁面移動 → 登攀モーション表示までを Unity 実行で確認できる。

- **入力:** `ClimbingState.GetWallRelativeMoveVector` — W/S = 壁面上の上/下、A/D = カメラ画面基準の接線方向の左右。壁法線方向の入力は生成しない。
- **アニメ:** 公式 `PlatformerCharacterAnimation` の `ClimbingMoveClip=10`。移動中は velocity に応じて `animator.speed` が変化。
- **未完了:** 自然岩コース全体での連続 traversal、曲面/凸凹コーナーでの接線一貫性、LedgeGrab/Mantle とのシームレス遷移、JumpGrab / Traverse / Hang 見た目。

### ジオメトリの限界（重要）

リポジトリには **スキャンした自然岩メッシュは含まれていません**。`TozanNaturalRockGeometry` が生成する **無印ボックス／手続きメッシュ**（垂直壁・オーバーハング・可変幅台・段差・凸凹・不規則変位ボックス）がストレスフィクスチャです。`Rock_Irregular` は頂点変位したボックスであり、任意の photogrammetry 岩を代表しません。

### 設定

- `TozanPlatformerGeometryAuthoring` を Platformer キャラ Prefab に付与 → `TozanPlatformerGeometryConfig.DetectionMode = GeometryOnly`
- 公式サンプル用コンテンツは従来どおり `ClimbableTag` ベースの `ClimbingState` を維持

### テスト

```bash
unity command run_tests -- --mode PlayMode --filter NaturalRockSandbox
```

- `NaturalRockSandboxTests` — TestDrive による回帰（ledge / mantle / 大壁 fixture）
- `NaturalRockSandboxInputTests` — **Input System キューイベント**（WASD 自動壁面移動、Space ジャンプ、Shift ダッシュ、W/S/A/D 壁面移動、ClipIndex 10 / velocity）
- `NaturalRockSandboxErikaVisualTests` — Erika hybrid visual（Humanoid avatar、Hips/Head mapping、接地範囲、ClipIndex、Erika renderer、登攀 ClipIndex 10）

### Erika ビジュアルセットアップ

Editor API（手編集 YAML なし）:

```bash
unity command eval -- --code 'return Tozan.Editor.TozanErikaPlatformerSetup.EnsureReady();' --timeout 120000
```

生成物: `Assets/Characters/Erika/ErikaPlatformerAnimator.controller`（ClipIndex 0–15 → Mixamo クリップ）、`ErikaCharacterMesh.prefab`、`Animations/Freehang Climb.anim`（DPS固有の`EnableController`イベントを除去したECS用ローカル複製）、`PlatformerCharacter.prefab` の `MeshPrefab` 参照更新。

### 出典

公式 Platformer サンプルの vendor 記録: `Assets/ThirdParty/UnityPlatformer/PROVENANCE.md`

## その他シーン

- `ClimbingSandbox` — DPS 参考（Vault タグ等あり。採用ゲートではない）
- `TerrainSandbox` — Unity Terrain + 植生
