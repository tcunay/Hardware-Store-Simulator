using System;
using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Factories;
using UnityEngine;

namespace HardwareStore.Gameplay.Features.Delivery.Systems
{
    public sealed class SpawnDeliveryProductsSystem : IExecuteSystem
    {
        private readonly IProductFactory _productFactory;
        private readonly IGroup<GameEntity> _deliveries;
        private readonly List<GameEntity> _buffer = new(4);

        public SpawnDeliveryProductsSystem(GameContext gameContext, IProductFactory productFactory)
        {
            _productFactory = productFactory;
            _deliveries = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Delivery,
                    GameMatcher.DeliveryActive,
                    GameMatcher.EntityId,
                    GameMatcher.ProductType,
                    GameMatcher.DeliveryProductCount,
                    GameMatcher.Slots)
                .NoneOf(GameMatcher.DeliveryProductsSpawned));
        }

        public void Execute()
        {
            foreach (GameEntity delivery in _deliveries.GetEntities(_buffer))
            {
                if (delivery.Slots.Length < delivery.DeliveryProductCount)
                    throw new InvalidOperationException(
                        $"Delivery {delivery.EntityId} has {delivery.Slots.Length} cargo slots, " +
                        $"but requires {delivery.DeliveryProductCount}.");

                for (int index = 0; index < delivery.DeliveryProductCount; index++)
                {
                    Transform slot = delivery.Slots[index];
                    GameEntity product = _productFactory.CreateInbound(
                        delivery.ProductType,
                        new Pose(slot.position, slot.rotation),
                        delivery.EntityId,
                        index);

                    if (product.ProductType != delivery.ProductType)
                        throw new InvalidOperationException(
                            $"Delivery {delivery.EntityId} and product {product.EntityId} types do not match.");
                }

                delivery.isDeliveryProductsSpawned = true;
            }
        }
    }
}
