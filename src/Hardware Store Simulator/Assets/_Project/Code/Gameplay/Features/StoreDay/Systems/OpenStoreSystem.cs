using System;
using Entitas;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.StoreDay.Systems
{
    public sealed class OpenStoreSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly float _firstCustomerDelay;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public OpenStoreSystem(GameContext gameContext,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _firstCustomerDelay = staticData.CustomerVehicle.FirstCustomerDelay;
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
                if (terminal == null)
                    throw new InvalidOperationException(
                        $"Store interaction targets missing entity {request.TargetEntityId}.");
                if (!terminal.isStoreControlTerminal)
                    continue;

                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                GameEntity store = ValidateInteraction(player, terminal);
                if (player.isModalOpen)
                    continue;
                if (player.hasConsultationVisitEntityId ||
                    player.hasProcurementTerminalEntityId ||
                    player.hasDayReportStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has a modal relation without ModalOpen.");
                }
                if (!store.isStorePreparing)
                    continue;
                if (store.hasCustomerCooldownRemaining ||
                    _gameContext.GetEntityWithCustomerVisitStoreEntityId(store.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Preparing store {store.EntityId} cannot already schedule a customer.");
                }

                store.isStorePreparing = false;
                store.isStoreOpen = true;
                store.AddCustomerCooldownRemaining(_firstCustomerDelay);
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationStoreOpened));
            }
        }

        private GameEntity ValidateInteraction(GameEntity player, GameEntity terminal)
        {
            if (!terminal.hasEntityId || !terminal.hasStoreEntityId ||
                !terminal.isInteractable)
            {
                throw new InvalidOperationException(
                    "Store control terminal has incomplete configuration.");
            }
            if (player == null || !player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || player.StoreEntityId != terminal.StoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Interaction source cannot use store control terminal " +
                    $"{terminal.EntityId}.");
            }

            GameEntity store = _gameContext.GetEntityWithEntityId(terminal.StoreEntityId);
            if (store == null || !store.isStore || !store.hasEntityId ||
                !store.hasStoreControlTerminalEntityId ||
                store.StoreControlTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    $"Store control terminal {terminal.EntityId} has an invalid store relation.");
            }
            int phaseCount =
                (store.isStorePreparing ? 1 : 0) +
                (store.isStoreOpen ? 1 : 0) +
                (store.isStoreClosing ? 1 : 0) +
                (store.isDayReportOpen ? 1 : 0);
            if (phaseCount != 1)
            {
                throw new InvalidOperationException(
                    $"Store {store.EntityId} must have exactly one day-cycle phase.");
            }
            return store;
        }
    }
}
