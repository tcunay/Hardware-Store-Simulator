using System;
using Entitas;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Employees.Systems
{
    public sealed class RefreshWorkerTrolleyOccupiedSlotCountSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _trolleys;
        private readonly bool[] _occupiedSlots;

        public RefreshWorkerTrolleyOccupiedSlotCountSystem(
            GameContext gameContext, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _occupiedSlots = new bool[staticData.PlatformTrolley.Capacity];
            _trolleys = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WorkerTrolley, GameMatcher.EntityId,
                    GameMatcher.TrolleyCapacity,
                    GameMatcher.OccupiedTrolleySlotCount)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity trolley in _trolleys)
            {
                if (trolley.TrolleyCapacity != _occupiedSlots.Length)
                    throw new InvalidOperationException(
                        $"Worker trolley {trolley.EntityId} capacity changed at runtime.");
                Array.Clear(_occupiedSlots, 0, _occupiedSlots.Length);
                int count = 0;
                foreach (GameEntity product in
                         _gameContext.GetEntitiesWithWorkerTrolleyEntityId(
                             trolley.EntityId))
                {
                    if (product.isDestructed || !product.isProduct ||
                        !product.hasWorkerTrolleySlotIndex ||
                        product.WorkerTrolleySlotIndex < 0 ||
                        product.WorkerTrolleySlotIndex >= _occupiedSlots.Length ||
                        _occupiedSlots[product.WorkerTrolleySlotIndex])
                        throw new InvalidOperationException(
                            $"Worker trolley {trolley.EntityId} has invalid cargo slots.");
                    _occupiedSlots[product.WorkerTrolleySlotIndex] = true;
                    count++;
                }
                trolley.ReplaceOccupiedTrolleySlotCount(count);
            }
        }
    }
}
