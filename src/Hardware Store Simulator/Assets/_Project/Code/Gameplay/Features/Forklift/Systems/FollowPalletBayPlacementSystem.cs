using System;
using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class FollowPalletBayPlacementSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IForkliftMotionService _motion;
        private readonly IGroup<GameEntity> _pallets;

        public FollowPalletBayPlacementSystem(GameContext gameContext,
            IForkliftMotionService motion)
        {
            _gameContext = gameContext;
            _motion = motion;
            _pallets = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Pallet,
                    GameMatcher.EntityId,
                    GameMatcher.PalletBayEntityId,
                    GameMatcher.PalletBaySlotIndex,
                    GameMatcher.Transform)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity pallet in _pallets)
            {
                if (pallet.hasForkliftCarrierEntityId)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} cannot be in a bay and on a forklift.");
                }

                GameEntity bay = _gameContext.GetEntityWithEntityId(
                    pallet.PalletBayEntityId);
                if (bay == null || !bay.isPalletBay || !bay.hasSlots ||
                    pallet.PalletBaySlotIndex < 0 ||
                    pallet.PalletBaySlotIndex >= bay.Slots.Length ||
                    bay.Slots[pallet.PalletBaySlotIndex] == null ||
                    bay.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Pallet {pallet.EntityId} references an invalid bay slot.");
                }

                _motion.PlacePallet(
                    pallet.Transform,
                    bay.Slots[pallet.PalletBaySlotIndex]);
            }
        }
    }
}
