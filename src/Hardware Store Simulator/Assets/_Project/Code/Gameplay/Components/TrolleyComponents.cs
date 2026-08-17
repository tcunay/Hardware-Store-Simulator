using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class PlatformTrolley : IComponent { }
    [Game] public class WorkerTrolley : IComponent { }
    [Game] public class TrolleyUpgradeTerminal : IComponent { }
    [Game] public class CarryingProduct : IComponent { }
    [Game] public class PushingTrolley : IComponent { }
    [Game] public class PushingWorkerTrolley : IComponent { }
    [Game] public class TrolleyUpgradeUnlocked : IComponent { }
    [Game] public class OrderProgressionCounted : IComponent { }
    [Game] public class CompletedOrderCount : IComponent { public int Value; }
    [Game] public class TrolleyUpgradeTerminalEntityId : IComponent { public int Value; }
    [Game] public class TrolleyStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class WorkerTrolleyStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class TrolleyPusherEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class TrolleyEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class WorkerTrolleyEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class TrolleySlotIndex : IComponent { public int Value; }
    [Game] public class WorkerTrolleySlotIndex : IComponent { public int Value; }
    [Game] public class TrolleyCapacity : IComponent { public int Value; }
    [Game] public class OccupiedTrolleySlotCount : IComponent { public int Value; }
    [Game] public class TrolleyMovementSpeed : IComponent { public float Value; }
    [Game] public class TrolleyFollowDistance : IComponent { public float Value; }
    [Game] public class TrolleySpawnPosition : IComponent { public Vector3 Value; }
    [Game] public class TrolleySpawnRotation : IComponent { public Quaternion Value; }
    [Game] public class WorkerTrolleyHomePosition : IComponent { public Vector3 Value; }
    [Game] public class WorkerTrolleyHomeRotation : IComponent { public Quaternion Value; }
    [Game] public class WorkerTrolleyCustomerLoadingPosition : IComponent { public Vector3 Value; }
    [Game] public class WorkerTrolleyCustomerLoadingRotation : IComponent { public Quaternion Value; }
}
