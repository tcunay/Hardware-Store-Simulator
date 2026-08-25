using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class ToggleForkliftDrivingSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _players;
        private readonly IGroup<InputEntity> _inputs;
        private readonly IGroup<GameEntity> _requests;
        private readonly List<GameEntity> _buffer = new(2);

        public ToggleForkliftDrivingSystem(GameContext gameContext,
            InputContext inputContext, IForkliftMotionService motion,
            IGameEventFactory events)
        {
            _gameContext = gameContext;
            _motion = motion;
            _events = events;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId,
                    GameMatcher.StoreEntityId,
                    GameMatcher.Transform,
                    GameMatcher.CharacterController,
                    GameMatcher.MoveDirection,
                    GameMatcher.VerticalVelocity,
                    GameMatcher.HorizontalSpeed,
                    GameMatcher.ViewPivot,
                    GameMatcher.ViewPitch)
                .NoneOf(GameMatcher.Destructed, GameMatcher.ModalOpen));
            _inputs = inputContext.GetGroup(InputMatcher.AllOf(
                InputMatcher.InputState,
                InputMatcher.InteractPressed));
            _requests = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.InteractionRequest,
                GameMatcher.SourceEntityId,
                GameMatcher.TargetEntityId));
        }

        public void Execute()
        {
            foreach (InputEntity ignored in _inputs)
            foreach (GameEntity player in _players.GetEntities(_buffer))
            {
                if (player.isDrivingForklift)
                    ExitForklift(player);
            }

            foreach (GameEntity request in _requests)
            {
                GameEntity forklift = _gameContext.GetEntityWithEntityId(
                    request.TargetEntityId);
                if (forklift == null)
                {
                    throw new InvalidOperationException(
                        $"Interaction targets missing entity {request.TargetEntityId}.");
                }
                if (!forklift.isForklift)
                    continue;

                GameEntity player = _gameContext.GetEntityWithEntityId(
                    request.SourceEntityId);
                if (player == null)
                {
                    throw new InvalidOperationException(
                        $"Interaction source {request.SourceEntityId} does not exist.");
                }

                TryEnterForklift(player, forklift);
            }
        }

        private void TryEnterForklift(GameEntity player, GameEntity forklift)
        {
            if (!player.isPlayer || !player.hasEntityId ||
                !player.hasStoreEntityId || !player.hasTransform ||
                !player.hasCharacterController || !player.hasMoveDirection ||
                !player.hasVerticalVelocity || !player.hasHorizontalSpeed ||
                !player.hasViewPivot || !player.hasViewPitch ||
                player.isDrivingForklift || player.isDestructed ||
                player.isModalOpen || !player.hasFocusedEntityId ||
                !player.hasFocusedInteractionType ||
                player.FocusedEntityId != forklift.EntityId ||
                player.FocusedInteractionType != InteractionTypeId.Forklift)
            {
                throw new InvalidOperationException(
                    $"Forklift interaction source {player.EntityId} is invalid.");
            }

            if (!player.isFocusInteractionAvailable)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} requested an unavailable forklift interaction.");
            }

            if (player.isHandsOccupied)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} cannot drive a forklift with occupied hands.");
            }

            ValidateForkliftForEntry(player, forklift);
            if (forklift.hasForkliftDriverEntityId)
                return;
            if (_gameContext.GetEntityWithForkliftDriverEntityId(player.EntityId) !=
                null)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} already drives another forklift.");
            }

            _motion.EnterDriver(
                player.CharacterController,
                player.Transform,
                forklift.DriverSeatAnchor);
            forklift.AddForkliftDriverEntityId(player.EntityId);
            forklift.isInteractable = false;
            forklift.isHighlighted = false;
            player.isDrivingForklift = true;
            ClearPlayerFocus(player);
            ResetPlayerMotion(player);
            ResetPlayerView(player);
        }

        private void ExitForklift(GameEntity player)
        {
            GameEntity forklift =
                _gameContext.GetEntityWithForkliftDriverEntityId(player.EntityId);
            if (forklift == null || !forklift.isForklift ||
                !forklift.hasEntityId || !forklift.hasDriverExitAnchor ||
                forklift.isDestructed || player.isHandsOccupied)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} has an invalid forklift driver relation.");
            }

            if (!_motion.TryExitDriver(
                    player.CharacterController,
                    player.Transform,
                    forklift.DriverExitAnchor,
                    forklift.Transform))
            {
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationForkliftExitBlocked));
                return;
            }

            forklift.RemoveForkliftDriverEntityId();
            forklift.isInteractable = true;
            player.isDrivingForklift = false;
            ClearPlayerFocus(player);
            ResetPlayerMotion(player);
        }

        private static void ValidateForkliftForEntry(GameEntity player,
            GameEntity forklift)
        {
            if (forklift == null || !forklift.isForklift ||
                !forklift.hasEntityId || !forklift.hasForkliftStoreEntityId ||
                forklift.ForkliftStoreEntityId != player.StoreEntityId ||
                !forklift.hasTransform || !forklift.hasRigidbody ||
                !forklift.hasColliders || !forklift.hasDriverSeatAnchor ||
                !forklift.hasDriverExitAnchor || !forklift.hasLiftTransform ||
                !forklift.hasCargoAnchor || !forklift.isInteractable ||
                forklift.isDestructed)
            {
                throw new InvalidOperationException(
                    $"Player {player.EntityId} focuses an invalid forklift.");
            }
        }

        private static void ClearPlayerFocus(GameEntity player)
        {
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            if (player.hasInteractionPrompt)
                player.RemoveInteractionPrompt();
            player.isFocusInteractionAvailable = false;
        }

        private static void ResetPlayerMotion(GameEntity player)
        {
            player.ReplaceMoveDirection(Vector3.zero);
            player.ReplaceVerticalVelocity(0f);
            player.ReplaceHorizontalSpeed(0f);
        }

        private static void ResetPlayerView(GameEntity player)
        {
            player.ViewPivot.localRotation = Quaternion.identity;
            player.ReplaceViewPitch(0f);
        }
    }
}
