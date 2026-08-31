using System;
using Entitas;
using HardwareStore.Gameplay.Common.VehicleTraffic;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Factory;

namespace HardwareStore.Gameplay.Features.VehicleTraffic.Systems
{
    public sealed class DrainVehicleTrafficSignalsSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IVehicleTrafficService _traffic;
        private readonly IEntityViewFactory _viewFactory;

        public DrainVehicleTrafficSignalsSystem(GameContext gameContext,
            IVehicleTrafficService traffic, IEntityViewFactory viewFactory)
        {
            _gameContext = gameContext;
            _traffic = traffic;
            _viewFactory = viewFactory;
        }

        public void Execute()
        {
            while (_traffic.TryDequeue(out VehicleTrafficSignal signal))
                Apply(signal);
        }

        private void Apply(VehicleTrafficSignal signal)
        {
            switch (signal.Kind)
            {
                case VehicleTrafficSignalKind.Initialized:
                    return;
                case VehicleTrafficSignalKind.VehicleActivated:
                    BindActivatedVehicle(signal);
                    return;
                case VehicleTrafficSignalKind.DestinationReached:
                    CompleteMove(signal);
                    return;
                case VehicleTrafficSignalKind.VehicleDespawned:
                    return;
                case VehicleTrafficSignalKind.VehicleRemoved:
                    HandleUnexpectedRemoval(signal);
                    return;
                case VehicleTrafficSignalKind.SpawnRejected:
                case VehicleTrafficSignalKind.DestinationRejected:
                case VehicleTrafficSignalKind.DespawnRejected:
                    throw new InvalidOperationException(
                        $"Vehicle traffic command {signal.CommandSequence} for entity " +
                        $"{signal.OwnerEntityId} failed: {signal.Failure}.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(signal.Kind), signal.Kind, null);
            }
        }

        private void BindActivatedVehicle(VehicleTrafficSignal signal)
        {
            GameEntity vehicle = GetOwner(signal);
            if (!vehicle.isVehicleTrafficControlled ||
                !vehicle.isVehicleTrafficSpawnPending ||
                vehicle.isVehicleTrafficReady || vehicle.hasVehicleTrafficRuntimeId ||
                vehicle.hasView || signal.VehicleRoot == null)
            {
                throw InvalidSignal(vehicle, signal, "activation");
            }

            EntityBehaviour view = signal.VehicleRoot.GetComponent<EntityBehaviour>();
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Traffic vehicle {signal.RuntimeVehicleId} has no root " +
                    $"{nameof(EntityBehaviour)}.");
            }

            _viewFactory.InjectAndBindExistingView(vehicle, view);
            vehicle.ReplaceTrafficPreviousPosition(view.transform.position);
            if (vehicle.hasSpawnPosition)
                vehicle.RemoveSpawnPosition();
            if (vehicle.hasSpawnRotation)
                vehicle.RemoveSpawnRotation();
            vehicle.AddVehicleTrafficRuntimeId(signal.RuntimeVehicleId);
            vehicle.isVehicleTrafficSpawnPending = false;
            vehicle.isVehicleTrafficReady = true;
            vehicle.isVehicleTrafficMoving = true;
        }

        private void CompleteMove(VehicleTrafficSignal signal)
        {
            GameEntity vehicle = GetOwner(signal);
            if (!vehicle.isVehicleTrafficControlled ||
                !vehicle.isVehicleTrafficReady || !vehicle.isVehicleTrafficMoving ||
                !vehicle.hasVehicleTrafficRuntimeId ||
                vehicle.VehicleTrafficRuntimeId != signal.RuntimeVehicleId ||
                !vehicle.hasRoute || !vehicle.hasRouteWaypointIndex ||
                vehicle.isRouteCompleted)
            {
                throw InvalidSignal(vehicle, signal, "destination");
            }

            vehicle.isVehicleTrafficMoving = false;
            vehicle.isRouteCompleted = true;
        }

        private void HandleUnexpectedRemoval(VehicleTrafficSignal signal)
        {
            GameEntity vehicle = _gameContext.GetEntityWithEntityId(signal.OwnerEntityId);
            if (vehicle == null || vehicle.isDestructed)
                return;

            throw new InvalidOperationException(
                $"Traffic provider unexpectedly removed vehicle " +
                $"{signal.RuntimeVehicleId} owned by entity {signal.OwnerEntityId}.");
        }

        private GameEntity GetOwner(VehicleTrafficSignal signal)
        {
            GameEntity owner = _gameContext.GetEntityWithEntityId(signal.OwnerEntityId);
            if (owner == null || owner.isDestructed ||
                !owner.hasVehicleTrafficCommandSequence ||
                owner.VehicleTrafficCommandSequence != signal.CommandSequence)
            {
                throw new InvalidOperationException(
                    $"Vehicle traffic signal {signal.Kind} references missing or stale " +
                    $"entity {signal.OwnerEntityId}, command {signal.CommandSequence}.");
            }

            return owner;
        }

        private static InvalidOperationException InvalidSignal(GameEntity owner,
            VehicleTrafficSignal signal, string phase) => new(
            $"Entity {owner.EntityId} received an invalid vehicle-traffic {phase} " +
            $"signal for runtime vehicle {signal.RuntimeVehicleId}.");
    }
}
