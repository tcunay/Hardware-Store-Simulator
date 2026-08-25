using System;
using Entitas;
using HardwareStore.Gameplay.Factories;

namespace HardwareStore.Gameplay.Features.Procurement.Systems
{
    public sealed class EnsureProcurementCartSystem : IExecuteSystem
    {
        private readonly GameContext _gameContext;
        private readonly IProcurementCartFactory _carts;
        private readonly IGroup<GameEntity> _terminals;

        public EnsureProcurementCartSystem(GameContext gameContext,
            IProcurementCartFactory carts)
        {
            _gameContext = gameContext;
            _carts = carts;
            _terminals = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.ProcurementTerminal,
                    GameMatcher.EntityId,
                    GameMatcher.StoreEntityId)
                .NoneOf(GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity terminal in _terminals)
            {
                GameEntity cart =
                    _gameContext.GetEntityWithProcurementCartTerminalEntityId(
                        terminal.EntityId);
                if (cart == null)
                {
                    _carts.Create(terminal.EntityId, terminal.StoreEntityId);
                    continue;
                }
                if (!cart.isProcurementCart || cart.isDestructed ||
                    !cart.hasEntityId || !cart.hasStoreEntityId ||
                    cart.StoreEntityId != terminal.StoreEntityId ||
                    !cart.hasProcurementCartPackageCapacity ||
                    cart.ProcurementCartPackageCapacity <= 0 ||
                    cart.ProcurementCartPackageCapacity >
                    ProcurementCartFactory.CurrentDeliveryPackageCapacity)
                {
                    throw new InvalidOperationException(
                        $"Terminal {terminal.EntityId} owns an invalid procurement cart.");
                }
            }
        }
    }
}
