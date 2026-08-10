using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Orders.Systems
{
    public sealed class AcceptOrderSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public AcceptOrderSystem(GameContext gameContext, IStaticDataService staticData,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _events = events;
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity request in _requests)
            {
                GameEntity orderCounter =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!orderCounter.isOrderCounter)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                if (player.StoreEntityId != orderCounter.StoreEntityId)
                    continue;

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                        orderCounter.StoreEntityId);
                if (customerVisit == null || !customerVisit.isCustomerVisitWaiting)
                    continue;

                if (customerVisit.AvailableProductCount <
                    customerVisit.RequiredProductCount)
                {
                    _events.EmitNotification(
                        $"Недостаточно товара на складе: " +
                        $"{customerVisit.AvailableProductCount}/" +
                        $"{customerVisit.RequiredProductCount}");
                    continue;
                }

                customerVisit.isCustomerVisitWaiting = false;
                customerVisit.isCustomerVisitLoading = true;
                ProductConfig product =
                    _staticData.GetProduct(customerVisit.RequiredProductType);
                _events.EmitNotification(
                    $"Заказ принят • товар: {product.DisplayName} • количество: " +
                    $"{customerVisit.RequiredProductCount} {product.UnitLabel}");
                _events.EmitAudio(AudioCueId.OrderAccepted);
            }
        }
    }
}
