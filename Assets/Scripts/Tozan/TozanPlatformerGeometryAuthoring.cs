using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class TozanPlatformerGeometryAuthoring : MonoBehaviour
{
    public TozanPlatformerGeometryConfig Config = TozanPlatformerGeometryConfig.DefaultNaturalRock;

    class Baker : Baker<TozanPlatformerGeometryAuthoring>
    {
        public override void Bake(TozanPlatformerGeometryAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Config);
        }
    }
}
