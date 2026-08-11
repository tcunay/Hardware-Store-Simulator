using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class RewardCompletedOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _visits;
        private readonly List<GameEntity> _buffer = new(4);

        public RewardCompletedOrderSystem(GameContext gameContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _visits = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerVisit,
                    GameMatcher.Order,
                    GameMatcher.CustomerVisitCompleted,
                    GameMatcher.CustomerVisitStoreEntityId,
                    GameMatcher.OrderReward)
                .NoneOf(
                    GameMatcher.OrderRewarded,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity visit in _visits.GetEntities(_buffer))
            {
                GameEntity store =
                    _gameContext.GetEntityWithEntityId(visit.CustomerVisitStoreEntityId);

                store.ReplaceMoney(checked(store.Money + visit.OrderReward));
                visit.isOrderRewarded = true;
                _events.EmitAudio(AudioCueId.Reward);
            }
        }
    }
}
