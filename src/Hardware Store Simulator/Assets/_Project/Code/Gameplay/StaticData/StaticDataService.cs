using System;
using HardwareStore.Gameplay.Configs;
using UnityEngine;

namespace HardwareStore.Gameplay.StaticData
{
    public sealed class StaticDataService : IStaticDataService
    {
        private const string ConfigRoot = "Configs/";

        public PlayerConfig Player { get; private set; }
        public InteractionConfig Interaction { get; private set; }
        public EconomyConfig Economy { get; private set; }
        public DeliveryConfig Delivery { get; private set; }
        public OrderConfig Order { get; private set; }
        public ProductConfig Product { get; private set; }

        public void LoadAll()
        {
            Player = Load<PlayerConfig>(nameof(PlayerConfig));
            Interaction = Load<InteractionConfig>(nameof(InteractionConfig));
            Economy = Load<EconomyConfig>(nameof(EconomyConfig));
            Delivery = Load<DeliveryConfig>(nameof(DeliveryConfig));
            Order = Load<OrderConfig>(nameof(OrderConfig));
            Product = Load<ProductConfig>(nameof(ProductConfig));

            if (Player.ViewPrefab == null)
                throw new InvalidOperationException(
                    $"Required {nameof(PlayerConfig)} must reference an {nameof(PlayerConfig.ViewPrefab)}.");
            if (Product.ViewPrefab == null)
                throw new InvalidOperationException(
                    $"Required {nameof(ProductConfig)} must reference an {nameof(ProductConfig.ViewPrefab)}.");
            if (Delivery.ViewPrefab == null)
                throw new InvalidOperationException(
                    $"Required {nameof(DeliveryConfig)} must reference an {nameof(DeliveryConfig.ViewPrefab)}.");
            if (Delivery.ProductType != Product.ProductType || Order.RequiredProductType != Product.ProductType)
                throw new InvalidOperationException("Delivery, product and order configs must use the same product type.");
            if (Delivery.ProductCount <= Order.RequiredProductCount)
                throw new InvalidOperationException(
                    "The prototype delivery must leave at least one product in stock after the customer order.");
            if (Economy.InitialMoney < Delivery.TotalCost)
                throw new InvalidOperationException("Initial money must cover the first prototype delivery.");
        }

        private static TConfig Load<TConfig>(string assetName) where TConfig : ScriptableObject =>
            Resources.Load<TConfig>(ConfigRoot + assetName) ??
            throw new InvalidOperationException(
                $"Required config '{ConfigRoot}{assetName}' of type {typeof(TConfig).Name} was not found in Resources.");
    }
}
