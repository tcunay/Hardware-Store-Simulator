using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.VehicleTraffic;

namespace HardwareStore.Gameplay.Features.VehicleTraffic.Systems
{
    public sealed class ReleaseDestructedVehicleTrafficViewsSystem : ICleanupSystem
    {
        private readonly IVehicleTrafficService _traffic;
        private readonly IGroup<GameEntity> _vehicles;
        private readonly List<GameEntity> _buffer = new(8);

        public ReleaseDestructedVehicleTrafficViewsSystem(GameContext gameContext,
            IVehicleTrafficService traffic)
        {
            _traffic = traffic;
            _vehicles = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Destructed,
                GameMatcher.VehicleTrafficControlled,
                GameMatcher.EntityId,
                GameMatcher.VehicleTrafficCommandSequence));
        }

        public void Cleanup()
        {
            foreach (GameEntity vehicle in _vehicles.GetEntities(_buffer))
            {
                if (vehicle.isVehicleTrafficSpawnPending)
                {
                    if (vehicle.hasVehicleTrafficRuntimeId)
                    {
                        throw new System.InvalidOperationException(
                            $"Traffic vehicle {vehicle.EntityId} is both pending and active.");
                    }

                    _traffic.CancelSpawn(vehicle.EntityId);
                    vehicle.isVehicleTrafficSpawnPending = false;
                    continue;
                }

                if (!vehicle.hasVehicleTrafficRuntimeId)
                    continue;

                int runtimeVehicleId = vehicle.VehicleTrafficRuntimeId;
                // Gley may disable a pooled vehicle between the last gameplay tick and
                // StoreLoopState.Exit. EntityBehaviour then releases the ECS view before the
                // queued VehicleRemoved signal can be drained, while the runtime id still has
                // to be cleared by this destructed pipeline. In that state the provider has
                // already removed the vehicle, or its following Shutdown owns the removal.
                if (vehicle.hasView)
                {
                    int commandSequence = checked(
                        vehicle.VehicleTrafficCommandSequence + 1);
                    vehicle.View.ReleaseEntity();
                    _traffic.RequestDespawn(runtimeVehicleId, vehicle.EntityId,
                        commandSequence);
                    vehicle.ReplaceVehicleTrafficCommandSequence(commandSequence);
                }
                vehicle.RemoveVehicleTrafficRuntimeId();
                vehicle.isVehicleTrafficReady = false;
                vehicle.isVehicleTrafficMoving = false;
            }
        }
    }
}
