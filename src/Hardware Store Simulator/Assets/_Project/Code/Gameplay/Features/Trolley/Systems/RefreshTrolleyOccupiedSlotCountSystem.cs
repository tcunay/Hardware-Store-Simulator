using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class RefreshTrolleyOccupiedSlotCountSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly List<GameEntity> _buffer = new(4);

        public RefreshTrolleyOccupiedSlotCountSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PlatformTrolley,
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyCapacity,
                    GameMatcher.OccupiedTrolleySlotCount)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys.GetEntities(_buffer))
            {
                var occupiedSlots = new bool[trolley.TrolleyCapacity];
                int occupiedCount = 0;
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId))
                {
                    if (product.isDestructed || !product.isProduct ||
                        !product.hasTrolleySlotIndex ||
                        product.TrolleySlotIndex < 0 ||
                        product.TrolleySlotIndex >= occupiedSlots.Length ||
                        occupiedSlots[product.TrolleySlotIndex])
                    {
                        throw new InvalidOperationException(
                            $"Trolley {trolley.EntityId} has invalid cargo slot ownership.");
                    }

                    occupiedSlots[product.TrolleySlotIndex] = true;
                    occupiedCount++;
                }

                trolley.ReplaceOccupiedTrolleySlotCount(occupiedCount);
            }
        }
    }
}
