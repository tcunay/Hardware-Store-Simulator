using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class ClassifyFocusedInteractionSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _classifiedPlayers;
        private readonly IGroup<GameEntity> _focusedPlayers;
        private readonly List<GameEntity> _buffer = new(4);

        public ClassifyFocusedInteractionSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _classifiedPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.FocusedInteractionType));
            _focusedPlayers = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.FocusedEntityId));
        }

        public void Execute()
        {
            foreach (GameEntity player in _classifiedPlayers.GetEntities(_buffer))
                player.RemoveFocusedInteractionType();

            foreach (GameEntity player in _focusedPlayers)
            {
                GameEntity target =
                    _gameContext.GetEntityWithEntityId(player.FocusedEntityId);
                if (!target.isInteractable)
                    throw new InvalidOperationException(
                        $"Focused entity {target.EntityId} is not interactable.");

                player.AddFocusedInteractionType(ResolveType(target));
            }
        }

        private static InteractionTypeId ResolveType(GameEntity target)
        {
            if (target.isProcurementTerminal)
                return InteractionTypeId.ProcurementTerminal;
            if (target.isStorageZone)
                return InteractionTypeId.StorageZone;
            if (target.isOrderCounter)
                return InteractionTypeId.OrderCounter;
            if (target.isProduct)
                return InteractionTypeId.Product;
            if (target.isLoadingZone)
                return InteractionTypeId.LoadingZone;
            if (target.isTrolleyUpgradeTerminal)
                return InteractionTypeId.TrolleyUpgradeTerminal;
            if (target.isPlatformTrolley)
                return InteractionTypeId.PlatformTrolley;
            if (target.isStoreControlTerminal)
                return InteractionTypeId.StoreControlTerminal;

            throw new InvalidOperationException(
                $"Entity {target.EntityId} is interactable without a supported role.");
        }
    }
}
