using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.StoreSceneBindings.Systems
{
    public sealed class ValidateStoreSceneBindingsSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IStaticDataService _staticData;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(1);

        public ValidateStoreSceneBindingsSystem(GameContext gameContext, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _staticData = staticData;
            _stores = gameContext.GetGroup(GameMatcher
                .AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.OrderCounterEntityId,
                    GameMatcher.ProcurementTerminalEntityId,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.TrolleyUpgradeTerminalEntityId)
                .NoneOf(GameMatcher.StoreSceneBindingsValidated, GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                ValidateStore(store);
                store.isStoreSceneBindingsValidated = true;
            }
        }

        private void ValidateStore(GameEntity store)
        {
            GameEntity orderCounter = _gameContext.GetEntityWithEntityId(store.OrderCounterEntityId);
            GameEntity procurementTerminal =
                _gameContext.GetEntityWithEntityId(store.ProcurementTerminalEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(store.StorageZoneEntityId);
            GameEntity trolleyUpgradeTerminal = _gameContext.GetEntityWithEntityId(
                store.TrolleyUpgradeTerminalEntityId);

            if (orderCounter == null || procurementTerminal == null || storageZone == null ||
                trolleyUpgradeTerminal == null)
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references a missing scene interaction entity.");

            ValidateOrderCounter(orderCounter, store);
            ValidateProcurementTerminal(procurementTerminal, store, storageZone);
            ValidateStorageZone(storageZone);
            ValidateTrolleyUpgradeTerminal(trolleyUpgradeTerminal, store);
        }

        private static void ValidateTrolleyUpgradeTerminal(
            GameEntity terminal,
            GameEntity store)
        {
            ValidateBoundInteractionTarget(terminal, "trolley upgrade terminal");
            if (!terminal.isTrolleyUpgradeTerminal || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != store.EntityId ||
                !terminal.hasTrolleySpawnPosition || !terminal.hasTrolleySpawnRotation)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references an invalid trolley upgrade terminal.");
            }
        }

        private static void ValidateOrderCounter(GameEntity orderCounter, GameEntity store)
        {
            ValidateBoundInteractionTarget(orderCounter, "order counter");
            if (!orderCounter.isOrderCounter || !orderCounter.hasStoreEntityId ||
                orderCounter.StoreEntityId != store.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references an invalid order counter.");
            }
        }

        private static void ValidateProcurementTerminal(GameEntity terminal, GameEntity store,
            GameEntity storageZone)
        {
            ValidateBoundInteractionTarget(terminal, "procurement terminal");
            if (!terminal.isProcurementTerminal || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != store.EntityId || !terminal.hasStorageZoneEntityId ||
                terminal.StorageZoneEntityId != storageZone.EntityId ||
                !terminal.hasSelectedProductType)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} references an invalid procurement terminal.");
            }
        }

        private void ValidateStorageZone(GameEntity storageZone)
        {
            ValidateBoundInteractionTarget(storageZone, "storage zone");
            if (!storageZone.isStorageZone)
                throw new InvalidOperationException(
                    $"Entity {storageZone.EntityId} is not a storage zone.");
            int largestDeliverySize = 0;
            foreach (var productType in _staticData.ProductTypes)
            {
                largestDeliverySize = Math.Max(
                    largestDeliverySize,
                    _staticData.GetDelivery(productType).ProductCount);
            }

            if (!storageZone.hasSlots || storageZone.Slots == null ||
                storageZone.Slots.Length < largestDeliverySize)
            {
                throw new InvalidOperationException(
                    "The storage zone must contain enough slots for the complete delivery.");
            }
        }

        private static void ValidateBoundInteractionTarget(GameEntity entity, string targetName)
        {
            if (!entity.hasEntityId || entity.isDestructed || !entity.isInteractable ||
                !entity.hasView || !entity.hasInteractionView || entity.hasSceneViewKey ||
                entity.hasViewPrefab || entity.hasSpawnPosition || entity.hasSpawnRotation)
            {
                throw new InvalidOperationException(
                    $"The store {targetName} must be bound to exactly one configured scene view.");
            }
        }
    }
}
