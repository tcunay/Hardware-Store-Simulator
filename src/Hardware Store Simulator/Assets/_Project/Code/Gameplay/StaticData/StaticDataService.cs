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
        public CustomerVehicleConfig CustomerVehicle { get; private set; }
        public OrderConfig Order { get; private set; }
        public ProductConfig Product { get; private set; }

        public void LoadAll()
        {
            PlayerConfig player = Load<PlayerConfig>(nameof(PlayerConfig));
            InteractionConfig interaction = Load<InteractionConfig>(nameof(InteractionConfig));
            EconomyConfig economy = Load<EconomyConfig>(nameof(EconomyConfig));
            DeliveryConfig delivery = Load<DeliveryConfig>(nameof(DeliveryConfig));
            CustomerVehicleConfig customerVehicle =
                Load<CustomerVehicleConfig>(nameof(CustomerVehicleConfig));
            OrderConfig order = Load<OrderConfig>(nameof(OrderConfig));
            ProductConfig product = Load<ProductConfig>(nameof(ProductConfig));

            player.Validate();
            interaction.Validate();
            economy.Validate();
            delivery.Validate();
            customerVehicle.Validate();
            order.Validate();
            product.Validate();
            ValidateCompatibility(economy, delivery, order, product);

            Player = player;
            Interaction = interaction;
            Economy = economy;
            Delivery = delivery;
            CustomerVehicle = customerVehicle;
            Order = order;
            Product = product;
        }

        private static void ValidateCompatibility(EconomyConfig economy, DeliveryConfig delivery,
            OrderConfig order, ProductConfig product)
        {
            if (delivery.ProductType != product.ProductType ||
                order.RequiredProductType != product.ProductType)
                throw new InvalidOperationException("Delivery, product and order configs must use the same product type.");
            if (delivery.ProductCount <= order.RequiredProductCount)
                throw new InvalidOperationException(
                    "The prototype delivery must leave at least one product in stock after the customer order.");
            if (economy.InitialMoney < delivery.TotalCost)
                throw new InvalidOperationException("Initial money must cover the first prototype delivery.");
        }

        private static TConfig Load<TConfig>(string assetName)
            where TConfig : ScriptableObject, IValidatableConfig =>
            Resources.Load<TConfig>(ConfigRoot + assetName) ??
            throw new InvalidOperationException(
                $"Required config '{ConfigRoot}{assetName}' of type {typeof(TConfig).Name} was not found in Resources.");
    }
}
