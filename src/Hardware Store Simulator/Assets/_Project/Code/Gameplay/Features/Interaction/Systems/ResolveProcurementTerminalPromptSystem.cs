using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ResolveProcurementTerminalPromptSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _players;

        public ResolveProcurementTerminalPromptSystem(GameContext gameContext,
            IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.FocusedEntityId,
                GameMatcher.FocusedInteractionType));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                if (player.FocusedInteractionType != InteractionTypeId.ProcurementTerminal)
                    continue;

                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (terminal.StoreEntityId != player.StoreEntityId)
                    continue;
                if (!terminal.hasSelectedProductType)
                    throw new InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                if (player.isHandsOccupied)
                {
                    player.SetInteractionPrompt(
                        "Освободите руки, чтобы открыть каталог закупок",
                        false);
                    continue;
                }

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);

                GameEntity delivery =
                    _gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                        terminal.EntityId);
                if (delivery != null)
                {
                    ProductConfig deliveredProduct =
                        _staticData.GetProduct(delivery.ProductType);
                    player.SetInteractionPrompt(
                        $"Поставка принимается • товар: {deliveredProduct.DisplayName} • " +
                        $"принято: {delivery.StockedProductCount}/" +
                        $"{delivery.DeliveryProductCount} {deliveredProduct.UnitLabel}",
                        false);
                    continue;
                }

                GameEntity customerVisit =
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
                if (customerVisit == null)
                {
                    player.SetInteractionPrompt(
                        "Ожидайте клиента — поставка выбирается под заказ",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitCompleted ||
                    customerVisit.isCustomerVisitReturning ||
                    customerVisit.isCustomerVisitDeparting)
                {
                    player.SetInteractionPrompt(
                        "Заказ выполнен — дождитесь следующего клиента",
                        false);
                    continue;
                }

                if (customerVisit.isCustomerVisitArriving ||
                    customerVisit.isCustomerVisitConsulting)
                {
                    player.SetInteractionPrompt(
                        customerVisit.isCustomerVisitArriving
                            ? "Клиент прибывает — дождитесь начала консультации"
                            : "Сначала согласуйте предложение с клиентом у стойки",
                        false);
                    continue;
                }

                if (!customerVisit.isOrder || !customerVisit.hasEntityId ||
                    (!customerVisit.isCustomerVisitWaiting &&
                     !customerVisit.isCustomerVisitLoading))
                {
                    throw new InvalidOperationException(
                        $"Customer visit {customerVisit.EntityId} cannot open procurement.");
                }

                if (!HasOrderDeficit(customerVisit))
                {
                    player.SetInteractionPrompt(
                        "Запаса для текущего заказа достаточно",
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    "E — открыть каталог закупок",
                    true);
            }
        }

        private bool HasOrderDeficit(GameEntity order)
        {
            var lines = _gameContext.GetEntitiesWithOrderEntityId(order.EntityId);
            int activeLineCount = 0;
            bool hasDeficit = false;
            foreach (GameEntity line in lines)
            {
                activeLineCount++;
                if (!line.isOrderLine || line.isDestructed ||
                    !line.hasEntityId || !line.hasOrderEntityId ||
                    !line.hasStorageZoneEntityId || !line.hasProductType ||
                    !line.hasRequiredProductCount ||
                    !line.hasAvailableProductCount || !line.hasLoadedProductCount ||
                    line.OrderEntityId != order.EntityId ||
                    !order.hasStorageZoneEntityId ||
                    line.StorageZoneEntityId != order.StorageZoneEntityId ||
                    line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0 || line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order {order.EntityId} has an invalid procurement line.");
                }

                int remainingProductCount =
                    line.RequiredProductCount - line.LoadedProductCount;
                if (line.AvailableProductCount < remainingProductCount)
                    hasDeficit = true;
            }

            if (activeLineCount == 0)
                throw new InvalidOperationException(
                    $"Order {order.EntityId} has no active product lines.");

            return hasDeficit;
        }
    }
}
