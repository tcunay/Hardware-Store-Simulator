using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class StartPushingTrolleySystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly List<GameEntity> _buffer = new(4);

        public StartPushingTrolleySystem(GameContext gameContext,
            InputContext inputContext, IGameEventFactory events)
        {
            _gameContext = gameContext;
            _events = events;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.StoreEntityId,
                    GameMatcher.FocusedEntityId,
                    GameMatcher.FocusedInteractionType)
                .NoneOf(
                    GameMatcher.Destructed,
                    GameMatcher.ModalOpen,
                    GameMatcher.HandsOccupied,
                    GameMatcher.PushingTrolley));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.TrolleyPressed));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                GameEntity focusedTarget =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                GameEntity trolley = ResolveFocusedTrolley(player, focusedTarget);
                if (trolley == null)
                    continue;

                ValidatePlayer(player, trolley, focusedTarget);
                if (trolley.hasTrolleyPusherEntityId)
                    continue;

                if (_gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId) != null)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} already pushes another trolley.");
                }

                trolley.AddTrolleyPusherEntityId(player.EntityId);
                trolley.isInteractable = false;
                trolley.isHighlighted = false;
                focusedTarget.isHighlighted = false;
                player.isPushingTrolley = true;
                player.isHandsOccupied = true;
                if (player.hasInteractionPrompt)
                    player.RemoveInteractionPrompt();
                player.isFocusInteractionAvailable = false;
                player.RemoveFocusedInteractionType();
                player.RemoveFocusedEntityId();
                _events.EmitAudio(AudioCueId.PickUp);
            }
        }

        private GameEntity ResolveFocusedTrolley(
            GameEntity player,
            GameEntity focusedTarget)
        {
            if (focusedTarget == null || !focusedTarget.hasEntityId ||
                focusedTarget.EntityId != player.FocusedEntityId ||
                focusedTarget.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has an invalid focused entity relation.");
            }

            if (player.FocusedInteractionType == InteractionTypeId.PlatformTrolley)
            {
                if (!focusedTarget.isPlatformTrolley)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} focuses entity {focusedTarget.EntityId} " +
                        "as a platform trolley, but it has another role.");
                }

                if (focusedTarget.isWorkerTrolley)
                    return null;

                ValidateTrolley(focusedTarget);
                return focusedTarget;
            }

            if (player.FocusedInteractionType != InteractionTypeId.Product)
                return null;

            if (!focusedTarget.isProduct || !focusedTarget.hasProductType ||
                !focusedTarget.isInteractable)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid product " +
                    $"{focusedTarget.EntityId}.");
            }

            if (!focusedTarget.hasTrolleyEntityId)
            {
                if (focusedTarget.hasTrolleySlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Product {focusedTarget.EntityId} has a trolley slot without " +
                        "a trolley relation.");
                }

                return null;
            }

            if (!focusedTarget.hasTrolleySlotIndex)
            {
                throw new InvalidOperationException(
                    $"Product {focusedTarget.EntityId} has no trolley slot for its " +
                    "trolley relation.");
            }

            GameEntity trolley =
                _gameContext.GetEntityWithEntityId(focusedTarget.TrolleyEntityId);
            ValidateTrolley(trolley);
            if (!_gameContext.GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                    .Contains(focusedTarget) ||
                focusedTarget.TrolleySlotIndex < 0 ||
                focusedTarget.TrolleySlotIndex >= trolley.TrolleyCapacity)
            {
                throw new InvalidOperationException(
                    $"Product {focusedTarget.EntityId} has an invalid indexed relation " +
                    $"to trolley {trolley.EntityId}.");
            }

            return trolley;
        }

        private static void ValidateTrolley(GameEntity trolley)
        {
            if (trolley == null || !trolley.isPlatformTrolley ||
                !trolley.hasEntityId || !trolley.hasTrolleyStoreEntityId ||
                !trolley.hasTrolleyCapacity ||
                !trolley.hasOccupiedTrolleySlotCount ||
                !trolley.hasTrolleyMovementSpeed ||
                !trolley.hasTrolleyFollowDistance || !trolley.hasTransform ||
                !trolley.hasRigidbody || !trolley.hasSlots ||
                trolley.TrolleyCapacity <= 0 ||
                trolley.Slots.Length != trolley.TrolleyCapacity ||
                trolley.OccupiedTrolleySlotCount < 0 ||
                trolley.OccupiedTrolleySlotCount > trolley.TrolleyCapacity ||
                (!trolley.hasTrolleyPusherEntityId && !trolley.isInteractable) ||
                (trolley.hasTrolleyPusherEntityId && trolley.isInteractable) ||
                trolley.isDestructed)
            {
                throw new InvalidOperationException(
                    "Focused platform trolley has incomplete runtime state.");
            }
        }

        private static void ValidatePlayer(
            GameEntity player,
            GameEntity trolley,
            GameEntity focusedTarget)
        {
            if (!player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || player.isDestructed ||
                !player.hasFocusedEntityId || !player.hasFocusedInteractionType ||
                player.FocusedEntityId != focusedTarget.EntityId ||
                player.StoreEntityId != trolley.TrolleyStoreEntityId)
            {
                throw new InvalidOperationException(
                    $"Trolley {trolley.EntityId} received an invalid pushing source.");
            }
        }
    }
}
