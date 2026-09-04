using System;
using Unity.Entities;

/// <summary>
/// Tozan-only geometry mode for the official ECS Platformer character.
/// Official sample content keeps OfficialTagOnly (default absent component).
/// </summary>
public enum TozanSurfaceDetectionMode : byte
{
    OfficialTagOnly = 0,
    GeometryOnly = 1,
}

[Serializable]
public struct TozanSurfaceProbeConfig
{
    public float MaxGroundNormalDot;
    public float MinCeilingNormalDot;
    public float MinSteepNormalDot;
    public int MinClusterNormals;
    public float CornerNormalMergeDot;
    public float ReleasePredictDistance;

    public static TozanSurfaceProbeConfig DefaultNaturalRock => new TozanSurfaceProbeConfig
    {
        MaxGroundNormalDot = 0.72f,
        MinCeilingNormalDot = -0.25f,
        MinSteepNormalDot = 0.35f,
        MinClusterNormals = 1,
        CornerNormalMergeDot = 0.65f,
        ReleasePredictDistance = 0.18f,
    };
}

[Serializable]
public struct TozanPlatformerGeometryConfig : IComponentData
{
    public TozanSurfaceDetectionMode DetectionMode;
    public TozanSurfaceProbeConfig Probe;
    public float MantleDuration;
    public float MantleCollisionSkin;

    public static TozanPlatformerGeometryConfig DefaultNaturalRock => new TozanPlatformerGeometryConfig
    {
        DetectionMode = TozanSurfaceDetectionMode.GeometryOnly,
        Probe = TozanSurfaceProbeConfig.DefaultNaturalRock,
        MantleDuration = 0.45f,
        MantleCollisionSkin = 0.02f,
    };
}
