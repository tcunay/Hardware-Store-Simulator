using UnityEngine;

namespace HardwareStore.Gameplay.Common.VehicleTraffic
{
    public enum VehicleTrafficSignalKind
    {
        Initialized,
        VehicleActivated,
        SpawnRejected,
        DestinationReached,
        DestinationRejected,
        VehicleDespawned,
        DespawnRejected,
        VehicleRemoved,
    }

    public enum VehicleTrafficFailure
    {
        None,
        OwnerAlreadyHasVehicle,
        CapacityReached,
        SpawnUnavailable,
        SpawnTimedOut,
        NoPath,
        RuntimeVehicleNotFound,
        OwnerMismatch,
    }

    /// <summary>
    /// Immutable message crossing the traffic-provider boundary. Fields form one result value:
    /// their meaning is determined by <see cref="Kind"/>.
    /// </summary>
    public readonly struct VehicleTrafficSignal
    {
        public VehicleTrafficSignal(VehicleTrafficSignalKind kind, int ownerEntityId,
            int commandSequence, int runtimeVehicleId, GameObject vehicleRoot,
            VehicleTrafficFailure failure)
        {
            Kind = kind;
            OwnerEntityId = ownerEntityId;
            CommandSequence = commandSequence;
            RuntimeVehicleId = runtimeVehicleId;
            VehicleRoot = vehicleRoot;
            Failure = failure;
        }

        public VehicleTrafficSignalKind Kind { get; }
        public int OwnerEntityId { get; }
        public int CommandSequence { get; }
        public int RuntimeVehicleId { get; }
        public GameObject VehicleRoot { get; }
        public VehicleTrafficFailure Failure { get; }
    }
}
