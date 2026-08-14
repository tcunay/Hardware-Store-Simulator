using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Customers.Systems
{
    public sealed class FinalizeAcceptedCustomerPatienceSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public FinalizeAcceptedCustomerPatienceSystem(GameContext gameContext)
        {
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.CustomerVehicle,
                    GameMatcher.Order,
                    GameMatcher.EntityId,
                    GameMatcher.CustomerPatienceRemaining)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                if (!visit.isCustomerVisitReturning ||
                    visit.hasServingOrderCounterEntityId)
                {
                    throw new InvalidOperationException(
                        $"Accepted customer visit {visit.EntityId} has invalid patience " +
                        "lifecycle state.");
                }

                visit.RemoveCustomerPatienceRemaining();
                if (visit.isCustomerPatienceWarningIssued)
                    visit.isCustomerPatienceWarningIssued = false;
            }
        }
    }
}
