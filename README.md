# Tozan

Unity 6.6 / URP の登山・登攀プロトタイプ。Godot 版 `climbing_physics_prototype` との手触り比較用。

- エンジン: Unity 6000.6.0f1 / URP
- **Player（本命）:** 公式 ECS Platformer Sample（`com.unity.charactercontroller` 1.4.2）
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

`Rock_VerticalWall`（幅 12m / 高さ 8m / 正面 z≈1.275）で、F 登攀 → WASD 壁面移動 → 登攀モーション表示までを Unity 実行で確認できる。

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
- `NaturalRockSandboxInputTests` — **Input System キューイベント**（F 登攀、W/S/A/D 壁面移動、ClipIndex 10 / velocity）

### 出典

公式 Platformer サンプルの vendor 記録: `Assets/ThirdParty/UnityPlatformer/PROVENANCE.md`

## その他シーン

- `ClimbingSandbox` — DPS 参考（Vault タグ等あり。採用ゲートではない）
- `TerrainSandbox` — Unity Terrain + 植生
