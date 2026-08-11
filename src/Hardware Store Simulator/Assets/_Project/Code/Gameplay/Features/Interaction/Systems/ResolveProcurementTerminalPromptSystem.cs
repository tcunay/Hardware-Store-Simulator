using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Components;
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
                    throw new System.InvalidOperationException(
                        $"Procurement terminal {terminal.EntityId} has no selected product type.");

                GameEntity store =
                    _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
                GameEntity storageZone =
                    _gameContext.GetEntityWithEntityId(terminal.StorageZoneEntityId);
                DeliveryConfig selectedDelivery =
                    _staticData.GetDelivery(terminal.SelectedProductType);
                ProductConfig selectedProduct =
                    _staticData.GetProduct(terminal.SelectedProductType);

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

                if (terminal.SelectedProductType != customerVisit.RequiredProductType)
                {
                    ProductConfig requiredProduct =
                        _staticData.GetProduct(customerVisit.RequiredProductType);
                    player.SetInteractionPrompt(
                        $"←/→ — выбрать товар для заказа: {requiredProduct.DisplayName}",
                        false);
                    continue;
                }

                int freeSlotCount =
                    storageZone.Slots.Length - storageZone.OccupiedStorageSlotCount;
                if (freeSlotCount < selectedDelivery.ProductCount)
                {
                    player.SetInteractionPrompt(
                        $"Недостаточно места на складе: свободно {freeSlotCount}/" +
                        $"{selectedDelivery.ProductCount}",
                        false);
                    continue;
                }

                if (store.Money < selectedDelivery.TotalCost)
                {
                    player.SetInteractionPrompt(
                        $"Недостаточно денег • товар: {selectedProduct.DisplayName} • " +
                        $"нужно: {selectedDelivery.TotalCost:N0} ₽",
                        false);
                    continue;
                }

                player.SetInteractionPrompt(
                    $"←/→ — {selectedProduct.DisplayName} • E — заказать " +
                    $"{selectedDelivery.ProductCount} {selectedProduct.UnitLabel} за " +
                    $"{selectedDelivery.TotalCost:N0} ₽",
                    true);
            }
        }
    }
}
