using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;

/// <summary>
/// PlayMode harness: overwrite PlatformerCharacterControl after official input mapping.
/// Does not change LedgeDetection.
/// </summary>
public struct TozanPlatformerTestDrive : IComponentData
{
    public float3 MoveVector;
    public bool JumpHeld;
    public bool JumpPressed;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[UpdateAfter(typeof(PlatformerPlayerFixedStepControlSystem))]
public partial struct TozanPlatformerTestDriveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TozanPlatformerTestDrive>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (drive, control) in
                 SystemAPI.Query<TozanPlatformerTestDrive, RefRW<PlatformerCharacterControl>>())
        {
            control.ValueRW.MoveVector = drive.MoveVector;
            control.ValueRW.JumpHeld = drive.JumpHeld;
            control.ValueRW.JumpPressed = drive.JumpPressed;
        }
    }
}
