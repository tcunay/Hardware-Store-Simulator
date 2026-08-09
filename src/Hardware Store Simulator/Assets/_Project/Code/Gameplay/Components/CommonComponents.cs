using Entitas;
using Entitas.CodeGeneration.Attributes;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class EntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ViewComponent : IComponent { public IEntityView Value; }
    [Game] public class ViewPrefabComponent : IComponent { public EntityBehaviour Value; }
    [Game] public class TransformComponent : IComponent { public Transform Value; }
    [Game] public class CollidersComponent : IComponent { public Collider[] Value; }
    [Game] public class RigidbodyComponent : IComponent { public Rigidbody Value; }
    [Game] public class CameraComponent : IComponent { public Camera Value; }
    [Game] public class SpawnPosition : IComponent { public Vector3 Value; }
    [Game] public class SpawnRotation : IComponent { public Quaternion Value; }
    [Game] public class WorldPosition : IComponent { public Vector3 Value; }
    [Game] public class WorldRotation : IComponent { public Quaternion Value; }
    [Game] public class Destructed : IComponent { }
}
