using System;
using System.Collections.Generic;
using System.Linq;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using UnityEngine;

namespace HardwareStore.Gameplay.StaticData
{
    public sealed class StaticDataService : IStaticDataService
    {
        private const string ConfigRoot = "Configs";

        private Dictionary<ProductTypeId, ProductConfig> _products;
        private Dictionary<ProductTypeId, DeliveryConfig> _deliveries;
        private Dictionary<ProductTypeId, OrderConfig> _orders;
        private IReadOnlyList<ProductTypeId> _productTypes;

        public PlayerConfig Player { get; private set; }
        public InteractionConfig Interaction { get; private set; }
        public EconomyConfig Economy { get; private set; }
        public CustomerVehicleConfig CustomerVehicle { get; private set; }
        public IReadOnlyList<ProductTypeId> ProductTypes =>
            _productTypes ?? throw new InvalidOperationException(
                "Static product data has not been loaded yet.");

        public void LoadAll()
        {
            PlayerConfig player = Load<PlayerConfig>(nameof(PlayerConfig));
            InteractionConfig interaction = Load<InteractionConfig>(nameof(InteractionConfig));
            EconomyConfig economy = Load<EconomyConfig>(nameof(EconomyConfig));
            CustomerVehicleConfig customerVehicle =
                Load<CustomerVehicleConfig>(nameof(CustomerVehicleConfig));
            Dictionary<ProductTypeId, ProductConfig> products = LoadCatalog<ProductConfig>(
                config => config.ProductType);
            Dictionary<ProductTypeId, DeliveryConfig> deliveries = LoadCatalog<DeliveryConfig>(
                config => config.ProductType);
            Dictionary<ProductTypeId, OrderConfig> orders = LoadCatalog<OrderConfig>(
                config => config.RequiredProductType);
            ProductTypeId[] productTypes = products.Keys
                .OrderBy(productType => (int)productType)
                .ToArray();

            player.Validate();
            interaction.Validate();
            economy.Validate();
            customerVehicle.Validate();
            ValidateProductTypeCoverage(products);
            ValidateExactKeyParity(products, deliveries, orders);
            ValidateCompatibility(player, economy, productTypes, products, deliveries, orders);

            Player = player;
            Interaction = interaction;
            Economy = economy;
            CustomerVehicle = customerVehicle;
            _products = products;
            _deliveries = deliveries;
            _orders = orders;
            _productTypes = Array.AsReadOnly(productTypes);
        }

        public ProductConfig GetProduct(ProductTypeId productType) =>
            GetRequired(_products, productType, nameof(ProductConfig));

        public DeliveryConfig GetDelivery(ProductTypeId productType) =>
            GetRequired(_deliveries, productType, nameof(DeliveryConfig));

        public OrderConfig GetOrder(ProductTypeId productType) =>
            GetRequired(_orders, productType, nameof(OrderConfig));

        private static void ValidateProductTypeCoverage(
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products)
        {
            ProductTypeId[] missingProductTypes = Enum
                .GetValues(typeof(ProductTypeId))
                .Cast<ProductTypeId>()
                .Where(productType => !products.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            if (missingProductTypes.Length == 0)
                return;

            throw new InvalidOperationException(
                $"Product catalog must configure every {nameof(ProductTypeId)} value. " +
                $"Missing products: {Format(missingProductTypes)}.");
        }

        private static void ValidateExactKeyParity(
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries,
            IReadOnlyDictionary<ProductTypeId, OrderConfig> orders)
        {
            ProductTypeId[] missingDeliveries = products.Keys
                .Where(productType => !deliveries.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            ProductTypeId[] unexpectedDeliveries = deliveries.Keys
                .Where(productType => !products.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            ProductTypeId[] missingOrders = products.Keys
                .Where(productType => !orders.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            ProductTypeId[] unexpectedOrders = orders.Keys
                .Where(productType => !products.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();

            if (missingDeliveries.Length == 0 && unexpectedDeliveries.Length == 0 &&
                missingOrders.Length == 0 && unexpectedOrders.Length == 0)
                return;

            throw new InvalidOperationException(
                "Product, delivery and order catalogs must contain exactly the same product types. " +
                $"Missing deliveries: {Format(missingDeliveries)}. " +
                $"Unexpected deliveries: {Format(unexpectedDeliveries)}. " +
                $"Missing orders: {Format(missingOrders)}. " +
                $"Unexpected orders: {Format(unexpectedOrders)}.");
        }

        private static void ValidateCompatibility(
            PlayerConfig player,
            EconomyConfig economy,
            IReadOnlyList<ProductTypeId> productTypes,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries,
            IReadOnlyDictionary<ProductTypeId, OrderConfig> orders)
        {
            foreach (ProductTypeId productType in productTypes)
            {
                ProductConfig product = products[productType];
                DeliveryConfig delivery = deliveries[productType];
                OrderConfig order = orders[productType];

                if (product.CarryMovementSpeed >= player.WalkSpeed)
                {
                    throw new InvalidOperationException(
                        $"{nameof(ProductConfig)} for {productType} must use a carry movement " +
                        $"speed lower than {nameof(PlayerConfig)}.{nameof(PlayerConfig.WalkSpeed)}.");
                }

                if (delivery.PurchaseUnitPrice >= product.UnitPrice)
                {
                    throw new InvalidOperationException(
                        $"Purchase unit price for {productType} must be lower than its retail " +
                        "unit price.");
                }

                int expectedReward;
                try
                {
                    expectedReward = checked(product.UnitPrice * order.RequiredProductCount);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        $"Retail order total for {productType} must fit a 32-bit signed integer.",
                        exception);
                }

                if (order.Reward != expectedReward)
                {
                    throw new InvalidOperationException(
                        $"Order reward for {productType} must equal retail unit price " +
                        $"{product.UnitPrice} multiplied by required product count " +
                        $"{order.RequiredProductCount} ({expectedReward}).");
                }

                if (delivery.ProductCount <= order.RequiredProductCount)
                {
                    throw new InvalidOperationException(
                        $"Delivery for {productType} must leave at least one product in stock " +
                        "after its customer order.");
                }
            }

            int projectedMoney = economy.InitialMoney;
            foreach (ProductTypeId productType in productTypes)
            {
                DeliveryConfig delivery = deliveries[productType];
                OrderConfig order = orders[productType];
                if (projectedMoney < delivery.TotalCost)
                {
                    throw new InvalidOperationException(
                        $"Configured customer sequence cannot afford the {productType} " +
                        $"delivery: available {projectedMoney}, required " +
                        $"{delivery.TotalCost}.");
                }

                try
                {
                    projectedMoney = checked(
                        projectedMoney - delivery.TotalCost + order.Reward);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        "Projected money after the configured customer sequence must fit a " +
                        "32-bit signed integer.",
                        exception);
                }
            }
        }

        private static Dictionary<ProductTypeId, TConfig> LoadCatalog<TConfig>(
            Func<TConfig, ProductTypeId> productTypeSelector)
            where TConfig : ScriptableObject, IValidatableConfig
        {
            TConfig[] configs = Resources.LoadAll<TConfig>(ConfigRoot);
            if (configs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No {typeof(TConfig).Name} assets were found below Resources/{ConfigRoot}.");
            }

            var configsByProductType = new Dictionary<ProductTypeId, TConfig>(configs.Length);
            foreach (TConfig config in configs)
            {
                config.Validate();
                ProductTypeId productType = productTypeSelector(config);
                if (!configsByProductType.TryAdd(productType, config))
                {
                    throw new InvalidOperationException(
                        $"More than one {typeof(TConfig).Name} is configured for {productType}.");
                }
            }

            return configsByProductType;
        }

        private static TConfig GetRequired<TConfig>(
            IReadOnlyDictionary<ProductTypeId, TConfig> configs,
            ProductTypeId productType,
            string configName)
        {
            if (configs == null)
                throw new InvalidOperationException("Static product data has not been loaded yet.");
            if (configs.TryGetValue(productType, out TConfig config))
                return config;

            throw new KeyNotFoundException(
                $"{configName} for product type {productType} was not found.");
        }

        private static string Format(IReadOnlyCollection<ProductTypeId> productTypes) =>
            productTypes.Count == 0 ? "none" : string.Join(", ", productTypes);

        private static TConfig Load<TConfig>(string assetName)
            where TConfig : ScriptableObject, IValidatableConfig
        {
            TConfig config = Resources.Load<TConfig>($"{ConfigRoot}/{assetName}") ??
                             throw new InvalidOperationException(
                                 $"Required config '{ConfigRoot}/{assetName}' of type " +
                                 $"{typeof(TConfig).Name} was not found in Resources.");
            return config;
        }
    }
}
