using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class FollowForkliftCarriedPalletSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly IGroup<GameEntity> _pallets;

        public FollowForkliftCarriedPalletSystem(GameContext gameContext,
            IForkliftMotionService motion)
        {
            _gameContext = gameContext;
            _motion = motion;
            _pallets = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Pallet,
                    GameMatcher.EntityId,
                    GameMatcher.ForkliftCarrierEntityId,
                    GameMatcher.Transform)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity pallet in _pallets)
            {
                if (pallet.hasPalletBayEntityId ||
                    pallet.hasPalletBaySlotIndex)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} cannot be on a forklift and in a bay.");
                }

                GameEntity forklift = _gameContext.GetEntityWithEntityId(
                    pallet.ForkliftCarrierEntityId);
                if (forklift == null || !forklift.isForklift ||
                    !forklift.hasEntityId || !forklift.hasCargoAnchor ||
                    forklift.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} references an invalid forklift carrier.");
                }

                _motion.PlacePallet(pallet.Transform, forklift.CargoAnchor);
            }
        }
    }
}
