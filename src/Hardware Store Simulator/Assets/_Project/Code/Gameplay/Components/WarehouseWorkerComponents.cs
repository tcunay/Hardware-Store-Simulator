using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Components
{
    public enum WarehouseWorkerStatusId
    {
        OffShift,
        Idle,
        StorageFull,
        MovingToPickup,
        MovingToStorage,
        MovingToCustomerLoading,
        Blocked
    }

    public enum WarehouseTaskStepId
    {
        Available,
        MovingToPickup,
        MovingToStorage,
        MovingToCustomerLoading,
        Blocked
    }

    public enum WarehouseTaskBlockReasonId
    {
        None,
        StorageFull,
        NoPickupPath,
        NoStoragePath,
        NoCustomerLoadingPath,
        TimedOut,
        WorkerMissing
    }

    [Game] public class WarehouseWorker : IComponent { }
    [Game] public class WorkerShiftActive : IComponent { }
    [Game] public class WarehouseTask : IComponent { }
    [Game] public class InboundToStorageTask : IComponent { }
    [Game] public class StockToCustomerLoadingTask : IComponent { }
    [Game] public class WarehouseWorkerStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class WarehouseWorkerStatus : IComponent { public WarehouseWorkerStatusId Value; }
    [Game] public class WarehouseWorkerPickupPosition : IComponent { public Vector3 Value; }
    [Game] public class WarehouseWorkerStoragePosition : IComponent { public Vector3 Value; }
    [Game] public class WarehouseWorkerCustomerLoadingPosition : IComponent { public Vector3 Value; }
    [Game] public class WarehouseWorkerPickupRotation : IComponent { public Quaternion Value; }
    [Game] public class WarehouseWorkerStorageRotation : IComponent { public Quaternion Value; }
    [Game] public class WarehouseWorkerCustomerLoadingRotation : IComponent { public Quaternion Value; }
    [Game] public class NavigationAgentComponent : IComponent { public NavMeshAgent Value; }
    [Game] public class WarehouseTaskStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class WarehouseTaskStorageZoneEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class WarehouseTaskCustomerVisitEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class WarehouseTaskOrderLineEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class WarehouseTaskProductEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class AssignedWorkerEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class WarehouseTaskReservedStorageSlotIndex : IComponent { public int Value; }
    [Game] public class WarehouseTaskReservedLoadingSlotIndex : IComponent { public int Value; }
    [Game] public class WarehouseTaskStep : IComponent { public WarehouseTaskStepId Value; }
    [Game] public class WarehouseTaskBlockReason : IComponent { public WarehouseTaskBlockReasonId Value; }
    [Game] public class WarehouseTaskTimeoutRemaining : IComponent { public float Value; }
}
