using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ProcurementCartFactory : IProcurementCartFactory
    {
        public const int CurrentDeliveryPackageCapacity = 3;

        private readonly GameContext _gameContext;
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ProcurementCartFactory(GameContext gameContext,
            IIdentifierService identifiers, IStaticDataService staticData)
        {
            _gameContext = gameContext;
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(int procurementTerminalEntityId, int storeEntityId)
        {
            GameEntity terminal = _gameContext.GetEntityWithEntityId(
                procurementTerminalEntityId);
            GameEntity store = _gameContext.GetEntityWithEntityId(storeEntityId);
            if (terminal == null || !terminal.isProcurementTerminal ||
                !terminal.hasEntityId || !terminal.hasStoreEntityId ||
                terminal.StoreEntityId != storeEntityId)
            {
                throw new InvalidOperationException(
                    $"Cannot create a procurement cart for terminal " +
                    $"{procurementTerminalEntityId}.");
            }
            if (store == null || !store.isStore || !store.hasEntityId ||
                store.isDestructed || store.EntityId != terminal.StoreEntityId ||
                !store.hasProcurementTerminalEntityId ||
                store.ProcurementTerminalEntityId != terminal.EntityId)
            {
                throw new InvalidOperationException(
                    $"Cannot create a procurement cart for store {storeEntityId}.");
            }
            if (_gameContext.GetEntityWithProcurementCartTerminalEntityId(
                    procurementTerminalEntityId) != null)
            {
                throw new InvalidOperationException(
                    $"Terminal {procurementTerminalEntityId} already owns a procurement cart.");
            }

            return CreateEntity.Empty(_identifiers.Next())
                .AddProcurementCartTerminalEntityId(procurementTerminalEntityId)
                .AddStoreEntityId(storeEntityId)
                .AddProcurementCartPackageCapacity(CurrentDeliveryPackageCapacity)
                .With(x => x.isProcurementCart = true);
        }

        public GameEntity CreateLine(int cartEntityId, ProductTypeId productType,
            int packageCount)
        {
            GameEntity cart = _gameContext.GetEntityWithEntityId(cartEntityId);
            if (cart == null || !cart.isProcurementCart || cart.isDestructed ||
                !cart.hasEntityId || !cart.hasProcurementCartTerminalEntityId ||
                !cart.hasStoreEntityId || !cart.hasProcurementCartPackageCapacity ||
                cart.ProcurementCartPackageCapacity <= 0 ||
                cart.ProcurementCartPackageCapacity > CurrentDeliveryPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Cannot add a line to invalid procurement cart {cartEntityId}.");
            }
            if (packageCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(packageCount));

            DeliveryConfig config = _staticData.GetDelivery(productType);
            if (config.ProductType != productType)
            {
                throw new InvalidOperationException(
                    $"Delivery config for {productType} exposes {config.ProductType}.");
            }

            int totalPackageCount = packageCount;
            foreach (GameEntity line in _gameContext.GetEntitiesWithProcurementCartEntityId(
                         cartEntityId))
            {
                if (line.isDestructed)
                    continue;
                ValidateLine(line, cartEntityId);
                if (line.ProductType == productType)
                {
                    throw new InvalidOperationException(
                        $"Cart {cartEntityId} already contains {productType}.");
                }

                totalPackageCount = checked(
                    totalPackageCount + line.ProcurementPackageCount);
            }
            if (totalPackageCount > cart.ProcurementCartPackageCapacity)
            {
                throw new InvalidOperationException(
                    $"Cart {cartEntityId} cannot contain {totalPackageCount} packages; " +
                    $"capacity is {cart.ProcurementCartPackageCapacity}.");
            }

            return CreateEntity.Empty(_identifiers.Next())
                .AddProcurementCartEntityId(cartEntityId)
                .AddProductType(productType)
                .AddProcurementPackageCount(packageCount)
                .With(x => x.isProcurementCartLine = true);
        }

        private static void ValidateLine(GameEntity line, int cartEntityId)
        {
            if (!line.isProcurementCartLine || !line.hasEntityId ||
                !line.hasProcurementCartEntityId ||
                line.ProcurementCartEntityId != cartEntityId ||
                !line.hasProductType || !line.hasProcurementPackageCount ||
                line.ProcurementPackageCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Cart {cartEntityId} contains an invalid line.");
            }
        }
    }
}
