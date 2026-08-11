using System;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class StartPushingTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _requests;

        public StartPushingTrolleySystem(GameContext gameContext,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
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
                GameEntity trolley =
                    _gameContext.GetEntityWithEntityId(request.TargetEntityId);
                if (!trolley.isPlatformTrolley)
                    continue;

                ValidateTrolley(trolley);
                GameEntity player =
                    _gameContext.GetEntityWithEntityId(request.SourceEntityId);
                ValidatePlayer(player, trolley);
                if (player.isModalOpen || player.isHandsOccupied ||
                    trolley.hasTrolleyPusherEntityId)
                {
                    continue;
                }

                if (_gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} already pushes another trolley.");
                }

                trolley.AddTrolleyPusherEntityId(player.EntityId);
                trolley.isInteractable = false;
                trolley.isHighlighted = false;
                player.isPushingTrolley = true;
                player.isHandsOccupied = true;
                if (player.hasInteractionPrompt)
                    player.RemoveInteractionPrompt();
                player.isFocusInteractionAvailable = false;
                if (player.hasFocusedInteractionType)
                    player.RemoveFocusedInteractionType();
                if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
                _events.EmitAudio(AudioCueId.PickUp);
            }
        }

        private static void ValidateTrolley(GameEntity trolley)
        {
            if (!trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount ||
                !trolley.hasTrolleyMovementSpeed ||
                !trolley.hasTrolleyFollowDistance || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasSlots ||
                trolley.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Platform trolley {trolley.EntityId} has incomplete runtime state.");
            }
        }

        private static void ValidatePlayer(GameEntity player, GameEntity trolley)
        {
            if (player == null || !player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId ||
                player.StoreEntityId != trolley.TrolleyStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Trolley {trolley.EntityId} received an invalid pushing source.");
            }
        }
    }
}
