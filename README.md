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
| Surface climb | 公式 `ClimbingState` + `TozanSurfaceProbe`（`GeometryOnly`） | 垂直壁 fixture で PlayMode 検証 |

### ジオメトリの限界（重要）

リポジトリには **スキャンした自然岩メッシュは含まれていません**。`TozanNaturalRockGeometry` が生成する **無印ボックス／手続きメッシュ**（垂直壁・オーバーハング・可変幅台・段差・凸凹・不規則変位ボックス）がストレスフィクスチャです。`Rock_Irregular` は頂点変位したボックスであり、任意の photogrammetry 岩を代表しません。

### 設定

- `TozanPlatformerGeometryAuthoring` を Platformer キャラ Prefab に付与 → `TozanPlatformerGeometryConfig.DetectionMode = GeometryOnly`
- 公式サンプル用コンテンツは従来どおり `ClimbableTag` ベースの `ClimbingState` を維持

### テスト

```bash
unity command run_tests -- --mode PlayMode --filter NaturalRockSandbox
```

- `NaturalRockSandboxTests` — TestDrive による回帰（ledge / mantle）
- `NaturalRockSandboxInputTests` — **Input System キューイベント**（Move / Jump / Climb / Crouch）

### 出典

公式 Platformer サンプルの vendor 記録: `Assets/ThirdParty/UnityPlatformer/PROVENANCE.md`

## その他シーン

- `ClimbingSandbox` — DPS 参考（Vault タグ等あり。採用ゲートではない）
- `TerrainSandbox` — Unity Terrain + 植生
