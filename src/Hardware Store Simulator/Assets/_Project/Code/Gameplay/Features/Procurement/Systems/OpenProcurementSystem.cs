using System;
using System.Linq;
using Entitas;
using HardwareStore.Gameplay.Common.Cursor;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class OpenProcurementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly ICursorService _cursor;
        private readonly IStaticDataService _staticData;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public OpenProcurementSystem(GameContext gameContext, ICursorService cursor,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _cursor = cursor;
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
                GameEntity terminal =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!terminal.isProcurementTerminal)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                Open(player, terminal);
            }
        }

        private void Open(GameEntity player, GameEntity terminal)
        {
            ValidateTerminal(terminal);
            if (!player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || !player.hasMoveDirection)
            {
                throw new InvalidOperationException(
                    "Procurement interaction source is not a configured player.");
            }
            if (player.isModalOpen)
                return;
            if (player.isHandsOccupied)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    player.isPushingTrolley
                        ? LocalizationKey.NotificationReleaseTrolleyFirst
                        : LocalizationKey.NotificationFreeHandsForProcurement));
                return;
            }
            if (player.StoreEntityId != terminal.StoreEntityId)
                throw new InvalidOperationException(
                    $"Player {player.EntityId} cannot use procurement terminal " +
                    $"{terminal.EntityId} of another store.");
            if (player.hasConsultationVisitEntityId ||
                player.hasProcurementTerminalEntityId)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has a modal relation without ModalOpen.");
            }
            if (_gameContext.GetEntityWithDeliveryProcurementTerminalEntityId(
                    terminal.EntityId) != null)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationAcceptCurrentDeliveryFirst));
                return;
            }
            if (!TrySelectDeficitProduct(terminal))
                return;

            player.AddProcurementTerminalEntityId(terminal.EntityId);
            player.isModalOpen = true;
            player.ReplaceMoveDirection(Vector3.zero);
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            terminal.isHighlighted = false;
            player.isCursorLocked = true;
            _cursor.SetLocked(true);
        }

        private void ValidateTerminal(GameEntity terminal)
        {
            if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.hasStorageZoneEntityId || !terminal.hasSelectedProductType ||
                !terminal.hasDeliverySpawnPosition ||
                !terminal.hasDeliverySpawnRotation)
            {
                throw new InvalidOperationException(
                    "Procurement terminal has incomplete domain configuration.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            GameEntity storageZone = _gameContext.GetEntityWithEntityId(
                terminal.StorageZoneEntityId);
            if (store == null || !store.isStore || !store.hasMoney ||
                !store.hasProcurementTerminalEntityId ||
                store.ProcurementTerminalEntityId != terminal.EntityId ||
                !store.hasStorageZoneEntityId ||
                store.StorageZoneEntityId != terminal.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid store relation.");
            }
            if (storageZone == null || !storageZone.isStorageZone ||
                !storageZone.hasSlots || !storageZone.hasOccupiedStorageSlotCount)
            {
                throw new InvalidOperationException(
                    $"Procurement terminal {terminal.EntityId} has an invalid storage relation.");
            }
        }

        private bool TrySelectDeficitProduct(GameEntity terminal)
        {
            GameEntity visit = _gameContext.GetEntityWithCustomerVisitStoreEntityId(
                terminal.StoreEntityId);
            if (visit == null)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationWaitForCustomer));
                return false;
            }
            if (visit.isCustomerVisitCompleted || visit.isCustomerVisitReturning ||
                visit.isCustomerVisitDeparting)
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationOrderCompletedWaitCustomer));
                return false;
            }
            if (visit.isCustomerVisitArriving || visit.isCustomerVisitConsulting)
            {
                _events.EmitNotification(
                    visit.isCustomerVisitArriving
                        ? LocalizedTexts.Text(
                            LocalizationKey.NotificationWaitForCustomerConsultation)
                        : LocalizedTexts.Text(
                            LocalizationKey.NotificationConsultAtCounterFirst));
                return false;
            }
            if (!visit.isOrder ||
                (!visit.isCustomerVisitWaiting && !visit.isCustomerVisitLoading))
            {
                throw new InvalidOperationException(
                    $"Customer visit {visit.EntityId} cannot open procurement.");
            }
            if (!visit.hasStorageZoneEntityId ||
                visit.StorageZoneEntityId != terminal.StorageZoneEntityId)
            {
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} references an invalid storage zone.");
            }

            GameEntity[] lines =
                _gameContext.GetEntitiesWithOrderEntityId(visit.EntityId).ToArray();
            ValidateOrderLines(visit, lines);
            if (HasDeficit(lines, terminal.SelectedProductType))
                return true;

            foreach (ProductTypeId productType in _staticData.ProductTypes)
            {
                if (!HasDeficit(lines, productType))
                    continue;

                terminal.ReplaceSelectedProductType(productType);
                return true;
            }

            _events.EmitNotification(LocalizedTexts.Text(
                LocalizationKey.NotificationProcurementNotRequired));
            return false;
        }

        private static void ValidateOrderLines(GameEntity visit, GameEntity[] lines)
        {
            if (lines.Length == 0)
                throw new InvalidOperationException(
                    $"Order {visit.EntityId} has no active product lines.");

            for (int index = 0; index < lines.Length; index++)
            {
                GameEntity line = lines[index];
                if (!line.isOrderLine || line.isDestructed || !line.hasEntityId ||
                    !line.hasOrderEntityId ||
                    line.OrderEntityId != visit.EntityId || !line.hasProductType ||
                    !line.hasStorageZoneEntityId ||
                    line.StorageZoneEntityId != visit.StorageZoneEntityId ||
                    !line.hasLineIndex ||
                    !line.hasRequiredProductCount || !line.hasAvailableProductCount ||
                    !line.hasLoadedProductCount || line.RequiredProductCount <= 0 ||
                    line.AvailableProductCount < 0 || line.LoadedProductCount < 0 ||
                    line.LoadedProductCount > line.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Order line {line.EntityId} has invalid procurement state.");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (lines[previous].ProductType == line.ProductType)
                        throw new InvalidOperationException(
                            $"Order {visit.EntityId} contains duplicate product type " +
                            $"{line.ProductType}.");
                }
            }
        }

        private static bool HasDeficit(GameEntity[] lines, ProductTypeId productType)
        {
            foreach (GameEntity line in lines)
            {
                if (line.ProductType != productType)
                    continue;

                int remainingCount =
                    line.RequiredProductCount - line.LoadedProductCount;
                return remainingCount - line.AvailableProductCount > 0;
            }

            return false;
        }
    }
}
