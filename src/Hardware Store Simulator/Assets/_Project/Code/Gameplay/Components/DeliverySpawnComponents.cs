using Entitas;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class DeliverySpawnPosition : IComponent { public Vector3 Value; }
    [Game] public class DeliverySpawnRotation : IComponent { public Quaternion Value; }
}
