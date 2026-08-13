using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class ReserveCustomerLoadingBaySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _bays;
        private readonly List<GameEntity> _bayBuffer = new(1);
        private readonly List<GameEntity> _candidates = new(4);

        public ReserveCustomerLoadingBaySystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _bays = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerLoadingBay,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerLoadingBayStoreEntityId,
                    GameMatcher.CustomerLoadingDepartureRoute)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity bay in _bays.GetEntities(_bayBuffer))
                TryReserve(bay);
        }

        private void TryReserve(GameEntity bay)
        {
            if (bay.CustomerLoadingDepartureRoute == null ||
                bay.CustomerLoadingDepartureRoute.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Customer loading bay {bay.EntityId} has an invalid departure route.");
            }
            if (_gameContext.GetEntityWithReservedCustomerLoadingBayEntityId(
                    bay.EntityId) != null)
            {
                return;
            }

            _candidates.Clear();
            foreach (GameEntity visit in
                     _gameContext.GetEntitiesWithCustomerVisitStoreEntityId(
                         bay.CustomerLoadingBayStoreEntityId))
            {
                if (visit.isDestructed || visit.hasReservedCustomerLoadingBayEntityId)
                    continue;
                if (!visit.isCustomerVisitReturning &&
                    !visit.isCustomerVisitWaitingForLoadingBay)
                {
                    continue;
                }
                if (!visit.isCustomerVisit || !visit.isCustomerVehicle ||
                    !visit.isOrder || !visit.hasEntityId ||
                    !visit.hasCustomerArrivalSequence ||
                    !visit.hasReservedCustomerParkingSpotEntityId)
                {
                    throw new InvalidOperationException(
                        $"Store {bay.CustomerLoadingBayStoreEntityId} has an invalid " +
                        "loading-bay candidate.");
                }
                _candidates.Add(visit);
            }

            if (_candidates.Count == 0)
                return;
            _candidates.Sort((left, right) =>
                left.CustomerArrivalSequence.CompareTo(right.CustomerArrivalSequence));
            _candidates[0].AddReservedCustomerLoadingBayEntityId(bay.EntityId);
        }
    }
}
