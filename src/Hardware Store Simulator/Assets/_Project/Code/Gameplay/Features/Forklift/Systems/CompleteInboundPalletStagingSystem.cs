using System;
using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Forklift.Systems
{
    public sealed class CompleteInboundPalletStagingSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IGroup<GameEntity> _stagedPallets;
        private readonly List<GameEntity> _buffer = new(4);

        public CompleteInboundPalletStagingSystem(GameContext gameContext)
        {
            _gameContext = gameContext;
            _stagedPallets = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Pallet,
                    GameMatcher.InboundPallet,
                    GameMatcher.EntityId,
                    GameMatcher.PalletStoreEntityId,
                    GameMatcher.PalletBayEntityId,
                    GameMatcher.PalletBaySlotIndex)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity pallet in _stagedPallets.GetEntities(_buffer))
            {
                GameEntity bay = _gameContext.GetEntityWithEntityId(
                    pallet.PalletBayEntityId);
                if (bay == null || !bay.isPalletBay || !bay.hasEntityId ||
                    !bay.hasSlots || pallet.PalletBaySlotIndex < 0 ||
                    pallet.PalletBaySlotIndex >= bay.Slots.Length ||
                    bay.isDestructed)
                {
                    throw new InvalidOperationException(
                        $"Inbound pallet {pallet.EntityId} references an invalid bay.");
                }

                if (!bay.isFreightStagingZone)
                    continue;
                if (!bay.hasFreightStagingZoneStoreEntityId ||
                    bay.FreightStagingZoneStoreEntityId !=
                    pallet.PalletStoreEntityId)
                {
                    throw new InvalidOperationException(
                        $"Inbound pallet {pallet.EntityId} reached a staging zone from another store.");
                }

                pallet.isInboundPallet = false;
            }
        }
    }
}
