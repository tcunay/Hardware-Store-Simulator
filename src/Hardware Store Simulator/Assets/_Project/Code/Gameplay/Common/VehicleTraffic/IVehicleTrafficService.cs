using UnityEngine;

namespace HardwareStore.Gameplay.Common.VehicleTraffic
{
    public interface IVehicleTrafficService
    {
        void Initialize(Transform observer);
        void RequestSpawn(int ownerEntityId, int commandSequence, Pose start,
            Pose destination);
        void CancelSpawn(int ownerEntityId);
        void RequestDestination(int runtimeVehicleId, int ownerEntityId,
            int commandSequence, Pose destination);
        void RequestDespawn(int runtimeVehicleId, int ownerEntityId,
            int commandSequence);
        bool TryDequeue(out VehicleTrafficSignal signal);
        void Shutdown();
    }
}
