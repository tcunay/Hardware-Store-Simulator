using System;
using Entitas;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class ValidatePlayerHandlingStateSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _players;

        public ValidatePlayerHandlingStateSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Player,
                    GameMatcher.EntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                bool carryingProduct = player.isCarryingProduct;
                bool pushingTrolley = player.isPushingTrolley;
                bool drivingForklift = player.isDrivingForklift;
                bool hasExactlyOneHandlingRole = carryingProduct ^ pushingTrolley;
                if (player.isHandsOccupied != hasExactlyOneHandlingRole ||
                    player.isModalOpen && hasExactlyOneHandlingRole ||
                    drivingForklift && (hasExactlyOneHandlingRole || player.isModalOpen))
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has inconsistent hand occupancy state.");
                }

                GameEntity product =
                    _gameContext.GetEntityWithCarrierEntityId(player.EntityId);
                if (carryingProduct != (product != null) ||
                    product != null && (!product.isProduct || product.isDestructed))
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has inconsistent carried-product relation.");
                }

                GameEntity trolley =
                    _gameContext.GetEntityWithTrolleyPusherEntityId(player.EntityId);
                if (pushingTrolley != (trolley != null) ||
                    trolley != null &&
                    (!trolley.isPlatformTrolley || trolley.isDestructed))
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} has inconsistent pushed-trolley relation.");
                }
            }
        }
    }
}
