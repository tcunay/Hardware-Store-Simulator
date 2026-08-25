using System;
using System.Collections.Generic;
using System.Linq;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.StaticData
{
    public sealed class StaticDataService : IStaticDataService
    {
        private const string ConfigRoot = "Configs";

        private Dictionary<ProductTypeId, ProductConfig> _products;
        private Dictionary<ProductTypeId, DeliveryConfig> _deliveries;
        private Dictionary<CustomerProjectTypeId, CustomerProjectConfig> _projects;
        private IReadOnlyList<ProductTypeId> _productTypes;
        private IReadOnlyList<CustomerProjectTypeId> _projectTypes;

        public PlayerConfig Player { get; private set; }
        public InteractionConfig Interaction { get; private set; }
        public EconomyConfig Economy { get; private set; }
        public ProductRecoveryConfig ProductRecovery { get; private set; }
        public PlatformTrolleyConfig PlatformTrolley { get; private set; }
        public ForkliftConfig Forklift { get; private set; }
        public FreightTruckConfig FreightTruck { get; private set; }
        public PalletConfig Pallet { get; private set; }
        public WarehouseWorkerConfig WarehouseWorker { get; private set; }
        public StoreDayConfig StoreDay { get; private set; }
        public CustomerFlowConfig CustomerFlow { get; private set; }
        public CustomerConfig Customer { get; private set; }
        public CustomerVehicleConfig CustomerVehicle { get; private set; }
        public IReadOnlyList<ProductTypeId> ProductTypes =>
            _productTypes ?? throw new InvalidOperationException(
                "Static product data has not been loaded yet.");
        public IReadOnlyList<CustomerProjectTypeId> ProjectTypes =>
            _projectTypes ?? throw new InvalidOperationException(
                "Static customer project data has not been loaded yet.");

        public void LoadAll()
        {
            PlayerConfig player = Load<PlayerConfig>(nameof(PlayerConfig));
            InteractionConfig interaction = Load<InteractionConfig>(nameof(InteractionConfig));
            EconomyConfig economy = Load<EconomyConfig>(nameof(EconomyConfig));
            ProductRecoveryConfig productRecovery =
                Load<ProductRecoveryConfig>(nameof(ProductRecoveryConfig));
            PlatformTrolleyConfig platformTrolley =
                Load<PlatformTrolleyConfig>(nameof(PlatformTrolleyConfig));
            ForkliftConfig forklift = Load<ForkliftConfig>(nameof(ForkliftConfig));
            FreightTruckConfig freightTruck =
                Load<FreightTruckConfig>(nameof(FreightTruckConfig));
            PalletConfig pallet = Load<PalletConfig>(nameof(PalletConfig));
            WarehouseWorkerConfig warehouseWorker =
                Load<WarehouseWorkerConfig>(nameof(WarehouseWorkerConfig));
            StoreDayConfig storeDay = Load<StoreDayConfig>(nameof(StoreDayConfig));
            CustomerFlowConfig customerFlow =
                Load<CustomerFlowConfig>(nameof(CustomerFlowConfig));
            CustomerConfig customer = Load<CustomerConfig>(nameof(CustomerConfig));
            CustomerVehicleConfig customerVehicle =
                Load<CustomerVehicleConfig>(nameof(CustomerVehicleConfig));
            Dictionary<ProductTypeId, ProductConfig> products =
                LoadCatalog<ProductConfig, ProductTypeId>(config => config.ProductType);
            Dictionary<ProductTypeId, DeliveryConfig> deliveries =
                LoadCatalog<DeliveryConfig, ProductTypeId>(config => config.ProductType);
            Dictionary<CustomerProjectTypeId, CustomerProjectConfig> projects =
                LoadCatalog<CustomerProjectConfig, CustomerProjectTypeId>(
                    config => config.ProjectType);
            ProductTypeId[] productTypes = products.Keys
                .OrderBy(productType => (int)productType)
                .ToArray();
            CustomerProjectTypeId[] projectTypes = projects.Keys
                .OrderBy(projectType => (int)projectType)
                .ToArray();

            player.Validate();
            interaction.Validate();
            economy.Validate();
            productRecovery.Validate();
            platformTrolley.Validate();
            forklift.Validate();
            freightTruck.Validate();
            pallet.Validate();
            warehouseWorker.Validate();
            storeDay.Validate();
            customerFlow.Validate();
            customer.Validate();
            customerVehicle.Validate();
            ValidateEnumCoverage<ProductTypeId, ProductConfig>(products, "Product");
            ValidateEnumCoverage<CustomerProjectTypeId, CustomerProjectConfig>(
                projects,
                "Customer project");
            ValidateProductDeliveryParity(products, deliveries);
            ValidateCompatibility(
                player,
                economy,
                customerVehicle,
                platformTrolley,
                warehouseWorker,
                storeDay,
                customerFlow,
                productTypes,
                projectTypes,
                products,
                deliveries,
                projects);

            Player = player;
            Interaction = interaction;
            Economy = economy;
            ProductRecovery = productRecovery;
            PlatformTrolley = platformTrolley;
            Forklift = forklift;
            FreightTruck = freightTruck;
            Pallet = pallet;
            WarehouseWorker = warehouseWorker;
            StoreDay = storeDay;
            CustomerFlow = customerFlow;
            Customer = customer;
            CustomerVehicle = customerVehicle;
            _products = products;
            _deliveries = deliveries;
            _projects = projects;
            _productTypes = Array.AsReadOnly(productTypes);
            _projectTypes = Array.AsReadOnly(projectTypes);
        }

        public ProductConfig GetProduct(ProductTypeId productType) =>
            GetRequired(_products, productType, nameof(ProductConfig));

        public DeliveryConfig GetDelivery(ProductTypeId productType) =>
            GetRequired(_deliveries, productType, nameof(DeliveryConfig));

        public CustomerProjectConfig GetProject(CustomerProjectTypeId projectType) =>
            GetRequired(_projects, projectType, nameof(CustomerProjectConfig));

        private static void ValidateEnumCoverage<TKey, TConfig>(
            IReadOnlyDictionary<TKey, TConfig> configs,
            string catalogName)
            where TKey : struct, Enum
        {
            TKey[] missingKeys = Enum
                .GetValues(typeof(TKey))
                .Cast<TKey>()
                .Where(key => !configs.ContainsKey(key))
                .OrderBy(key => Convert.ToInt32(key))
                .ToArray();
            if (missingKeys.Length == 0)
                return;

            throw new InvalidOperationException(
                $"{catalogName} catalog must configure every {typeof(TKey).Name} value. " +
                $"Missing values: {Format(missingKeys)}.");
        }

        private static void ValidateProductDeliveryParity(
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries)
        {
            ProductTypeId[] missingDeliveries = products.Keys
                .Where(productType => !deliveries.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            ProductTypeId[] unexpectedDeliveries = deliveries.Keys
                .Where(productType => !products.ContainsKey(productType))
                .OrderBy(productType => (int)productType)
                .ToArray();
            if (missingDeliveries.Length == 0 && unexpectedDeliveries.Length == 0)
            {
                DeliveryConfig sharedVehicle = deliveries.Values.First();
                foreach (DeliveryConfig delivery in deliveries.Values)
                {
                    if (delivery.ViewPrefab != sharedVehicle.ViewPrefab)
                    {
                        throw new InvalidOperationException(
                            "Every delivery config must use the same vehicle prefab so a " +
                            "mixed purchase order can be delivered atomically.");
                    }
                }
                ValidateDeliveryVehiclePrefab(
                    deliveries.Values,
                    sharedVehicle.ViewPrefab);
                return;
            }

            throw new InvalidOperationException(
                "Product and delivery catalogs must contain exactly the same product types. " +
                $"Missing deliveries: {Format(missingDeliveries)}. " +
                $"Unexpected deliveries: {Format(unexpectedDeliveries)}.");
        }

        private static void ValidateDeliveryVehiclePrefab(
            IEnumerable<DeliveryConfig> deliveries,
            EntityBehaviour viewPrefab)
        {
            EntityBehaviour[] entityBehaviours =
                viewPrefab.GetComponentsInChildren<EntityBehaviour>(includeInactive: true);
            TransformRegistrar[] transformRegistrars =
                viewPrefab.GetComponentsInChildren<TransformRegistrar>(includeInactive: true);
            SlotsRegistrar[] slotsRegistrars =
                viewPrefab.GetComponentsInChildren<SlotsRegistrar>(includeInactive: true);
            if (viewPrefab.transform.parent != null ||
                entityBehaviours.Length != 1 ||
                !ReferenceEquals(entityBehaviours[0], viewPrefab) ||
                transformRegistrars.Length != 1 ||
                transformRegistrars[0].transform != viewPrefab.transform ||
                slotsRegistrars.Length != 1 ||
                slotsRegistrars[0].transform != viewPrefab.transform)
            {
                throw new InvalidOperationException(
                    "The shared delivery vehicle must expose exactly one EntityBehaviour, " +
                    "TransformRegistrar, and SlotsRegistrar on its prefab root.");
            }

            int maximumProductCount = deliveries.Max(delivery => delivery.ProductCount);
            int minimumSlotCount;
            try
            {
                minimumSlotCount = checked(
                    maximumProductCount *
                    ProcurementCartFactory.CurrentDeliveryPackageCapacity);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Maximum mixed-delivery cargo capacity must fit a 32-bit signed integer.",
                    exception);
            }

            slotsRegistrars[0].ValidateConfiguration(
                viewPrefab,
                minimumSlotCount);
        }

        private static void ValidateCompatibility(
            PlayerConfig player,
            EconomyConfig economy,
            CustomerVehicleConfig customerVehicle,
            PlatformTrolleyConfig platformTrolley,
            WarehouseWorkerConfig warehouseWorker,
            StoreDayConfig storeDay,
            CustomerFlowConfig customerFlow,
            IReadOnlyList<ProductTypeId> productTypes,
            IReadOnlyList<CustomerProjectTypeId> projectTypes,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries,
            IReadOnlyDictionary<CustomerProjectTypeId, CustomerProjectConfig> projects)
        {
            ValidateCustomerFlow(storeDay, customerFlow);

            foreach (ProductTypeId productType in productTypes)
            {
                ProductConfig product = products[productType];
                DeliveryConfig delivery = deliveries[productType];
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
            }

            float fastestCarryMovementSpeed = productTypes
                .Max(productType => products[productType].CarryMovementSpeed);
            if (platformTrolley.MovementSpeed <= fastestCarryMovementSpeed ||
                platformTrolley.MovementSpeed >= player.WalkSpeed)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlatformTrolleyConfig)}.{nameof(PlatformTrolleyConfig.MovementSpeed)} " +
                    "must be faster than carrying every product and slower than walking.");
            }
            if (platformTrolley.Capacity < customerVehicle.CargoCapacity)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlatformTrolleyConfig)}.{nameof(PlatformTrolleyConfig.Capacity)} " +
                    "must fit a complete customer-vehicle order.");
            }
            if (warehouseWorker.MovementSpeed >= player.WalkSpeed)
            {
                throw new InvalidOperationException(
                    $"{nameof(WarehouseWorkerConfig)}.{nameof(WarehouseWorkerConfig.MovementSpeed)} " +
                    "must be slower than player walking speed.");
            }
            if (warehouseWorker.TrolleyCapacity != customerVehicle.CargoCapacity)
            {
                throw new InvalidOperationException(
                    $"{nameof(WarehouseWorkerConfig)}.{nameof(WarehouseWorkerConfig.TrolleyCapacity)} " +
                    $"must match {nameof(CustomerVehicleConfig)}." +
                    $"{nameof(CustomerVehicleConfig.CargoCapacity)}.");
            }

            foreach (CustomerProjectTypeId projectType in projectTypes)
                ValidateProject(projects[projectType], customerVehicle, products);

            ValidateDefaultProjectSequenceEconomy(
                economy.InitialMoney,
                projectTypes,
                projects,
                products,
                deliveries);
            ValidateTrolleyUpgradeLiquidity(
                economy.InitialMoney,
                platformTrolley,
                projectTypes,
                projects,
                products,
                deliveries);
        }

        private static void ValidateCustomerFlow(
            StoreDayConfig storeDay,
            CustomerFlowConfig customerFlow)
        {
            IReadOnlyList<CustomerArrivalSchedulePoint> schedule =
                customerFlow.ArrivalSchedule;
            if (schedule[0].Minute != storeDay.StartMinute ||
                schedule[schedule.Count - 1].Minute != storeDay.ClosingMinute)
            {
                throw new InvalidOperationException(
                    $"{nameof(CustomerFlowConfig)} schedule must start at " +
                    $"{nameof(StoreDayConfig)}.{nameof(StoreDayConfig.StartMinute)} and end at " +
                    $"{nameof(StoreDayConfig)}.{nameof(StoreDayConfig.ClosingMinute)}.");
            }
        }

        private static void ValidateProject(
            CustomerProjectConfig project,
            CustomerVehicleConfig customerVehicle,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products)
        {
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[offerIndex];
                int totalRequiredCount = 0;
                int reward = 0;
                try
                {
                    foreach (CustomerProjectLineDefinition line in offer.Lines)
                    {
                        if (!products.TryGetValue(line.ProductType, out ProductConfig product))
                        {
                            throw new InvalidOperationException(
                                $"Customer project {project.ProjectType} offer {offerIndex} " +
                                $"references missing product {line.ProductType}.");
                        }

                        totalRequiredCount = checked(totalRequiredCount + line.RequiredCount);
                        reward = checked(
                            reward + checked(product.UnitPrice * line.RequiredCount));
                    }
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        $"Customer project {project.ProjectType} offer {offerIndex} totals must " +
                        "fit a 32-bit signed integer.",
                        exception);
                }

                if (totalRequiredCount > customerVehicle.CargoCapacity)
                {
                    throw new InvalidOperationException(
                        $"Customer project {project.ProjectType} offer {offerIndex} requires " +
                        $"{totalRequiredCount} cargo slots, but the customer vehicle capacity " +
                        $"is {customerVehicle.CargoCapacity}.");
                }

                if (reward <= 0)
                    throw new InvalidOperationException(
                        $"Customer project {project.ProjectType} offer {offerIndex} must have " +
                        "positive calculated revenue.");
            }
        }

        private static void ValidateDefaultProjectSequenceEconomy(
            int initialMoney,
            IReadOnlyList<CustomerProjectTypeId> projectTypes,
            IReadOnlyDictionary<CustomerProjectTypeId, CustomerProjectConfig> projects,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries)
        {
            int projectedMoney = initialMoney;
            var projectedStock = products.Keys.ToDictionary(
                productType => productType,
                ignoredProductType => 0);

            foreach (CustomerProjectTypeId projectType in projectTypes)
            {
                CustomerProjectConfig project = projects[projectType];
                CustomerProjectOfferDefinition offer = project.Offers[project.DefaultOfferIndex];
                int purchaseCost = 0;
                int reward = 0;
                try
                {
                    foreach (CustomerProjectLineDefinition line in offer.Lines)
                    {
                        int availableCount = projectedStock[line.ProductType];
                        int deficit = Math.Max(0, line.RequiredCount - availableCount);
                        DeliveryConfig delivery = deliveries[line.ProductType];
                        int batchCount = deficit == 0
                            ? 0
                            : checked((deficit + delivery.ProductCount - 1) /
                                      delivery.ProductCount);
                        purchaseCost = checked(
                            purchaseCost + checked(batchCount * delivery.TotalCost));
                        projectedStock[line.ProductType] = checked(
                            availableCount + checked(batchCount * delivery.ProductCount));
                        reward = checked(
                            reward + checked(
                                products[line.ProductType].UnitPrice * line.RequiredCount));
                    }

                    if (projectedMoney < purchaseCost)
                    {
                        throw new InvalidOperationException(
                            $"Configured customer project sequence cannot afford project " +
                            $"{projectType}: available {projectedMoney}, required " +
                            $"{purchaseCost}.");
                    }

                    projectedMoney = checked(projectedMoney - purchaseCost + reward);
                    foreach (CustomerProjectLineDefinition line in offer.Lines)
                    {
                        projectedStock[line.ProductType] = checked(
                            projectedStock[line.ProductType] - line.RequiredCount);
                    }
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        "Configured customer project sequence totals must fit a 32-bit signed " +
                        "integer.",
                        exception);
                }
            }
        }

        private static void ValidateTrolleyUpgradeLiquidity(
            int initialMoney,
            PlatformTrolleyConfig trolley,
            IReadOnlyList<CustomerProjectTypeId> projectTypes,
            IReadOnlyDictionary<CustomerProjectTypeId, CustomerProjectConfig> projects,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries)
        {
            int validatedProjectCount = trolley.RequiredCompletedOrderCount + 1;
            if (projectTypes.Count < validatedProjectCount)
            {
                throw new InvalidOperationException(
                    $"The trolley unlock after {trolley.RequiredCompletedOrderCount} orders " +
                    "requires at least one subsequent configured customer project.");
            }

            var initialStock = products.Keys.ToDictionary(
                productType => productType,
                ignoredProductType => 0);
            ValidateTrolleyOfferPaths(
                projectIndex: 0,
                validatedProjectCount,
                initialMoney,
                initialStock,
                new List<int>(validatedProjectCount),
                trolley,
                projectTypes,
                projects,
                products,
                deliveries);
        }

        private static void ValidateTrolleyOfferPaths(
            int projectIndex,
            int validatedProjectCount,
            int money,
            IReadOnlyDictionary<ProductTypeId, int> stock,
            List<int> offerPath,
            PlatformTrolleyConfig trolley,
            IReadOnlyList<CustomerProjectTypeId> projectTypes,
            IReadOnlyDictionary<CustomerProjectTypeId, CustomerProjectConfig> projects,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries)
        {
            if (projectIndex >= validatedProjectCount)
                return;

            CustomerProjectTypeId projectType = projectTypes[projectIndex];
            CustomerProjectConfig project = projects[projectType];
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                var nextStock = stock.ToDictionary(pair => pair.Key, pair => pair.Value);
                offerPath.Add(offerIndex);
                int nextMoney;
                try
                {
                    nextMoney = CompleteProjectedProject(
                        money,
                        nextStock,
                        project.Offers[offerIndex],
                        products,
                        deliveries,
                        projectType,
                        offerPath);
                    if (projectIndex + 1 == trolley.RequiredCompletedOrderCount)
                    {
                        if (nextMoney < trolley.PurchasePrice)
                        {
                            throw new InvalidOperationException(
                                $"Trolley liquidity path {FormatOfferPath(projectTypes, offerPath)} " +
                                $"cannot afford the {trolley.PurchasePrice} trolley after " +
                                $"{trolley.RequiredCompletedOrderCount} orders; available " +
                                $"money is {nextMoney}.");
                        }

                        nextMoney = checked(nextMoney - trolley.PurchasePrice);
                    }

                    ValidateTrolleyOfferPaths(
                        projectIndex + 1,
                        validatedProjectCount,
                        nextMoney,
                        nextStock,
                        offerPath,
                        trolley,
                        projectTypes,
                        projects,
                        products,
                        deliveries);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        $"Trolley liquidity path {FormatOfferPath(projectTypes, offerPath)} " +
                        "must fit a 32-bit signed integer.",
                        exception);
                }
                finally
                {
                    offerPath.RemoveAt(offerPath.Count - 1);
                }
            }
        }

        private static int CompleteProjectedProject(
            int money,
            IDictionary<ProductTypeId, int> stock,
            CustomerProjectOfferDefinition offer,
            IReadOnlyDictionary<ProductTypeId, ProductConfig> products,
            IReadOnlyDictionary<ProductTypeId, DeliveryConfig> deliveries,
            CustomerProjectTypeId projectType,
            IReadOnlyList<int> offerPath)
        {
            int purchaseCost = 0;
            int reward = 0;
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                int availableCount = stock[line.ProductType];
                int deficit = Math.Max(0, line.RequiredCount - availableCount);
                DeliveryConfig delivery = deliveries[line.ProductType];
                int batchCount = deficit == 0
                    ? 0
                    : checked((deficit + delivery.ProductCount - 1) /
                              delivery.ProductCount);
                purchaseCost = checked(
                    purchaseCost + checked(batchCount * delivery.TotalCost));
                stock[line.ProductType] = checked(
                    availableCount + checked(batchCount * delivery.ProductCount));
                reward = checked(
                    reward + checked(products[line.ProductType].UnitPrice *
                                     line.RequiredCount));
            }

            if (money < purchaseCost)
            {
                throw new InvalidOperationException(
                    $"Trolley liquidity path {FormatOfferPathPrefix(projectType, offerPath)} " +
                    $"cannot afford required deliveries: available {money}, required " +
                    $"{purchaseCost}.");
            }

            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                stock[line.ProductType] = checked(
                    stock[line.ProductType] - line.RequiredCount);
            }

            return checked(money - purchaseCost + reward);
        }

        private static string FormatOfferPath(
            IReadOnlyList<CustomerProjectTypeId> projectTypes,
            IReadOnlyList<int> offerPath) =>
            string.Join(
                " -> ",
                offerPath.Select((offerIndex, index) =>
                    $"{projectTypes[index]}[{offerIndex}]"));

        private static string FormatOfferPathPrefix(
            CustomerProjectTypeId projectType,
            IReadOnlyList<int> offerPath) =>
            $"{projectType}[{offerPath[offerPath.Count - 1]}]";

        private static Dictionary<TKey, TConfig> LoadCatalog<TConfig, TKey>(
            Func<TConfig, TKey> keySelector)
            where TConfig : ScriptableObject, IValidatableConfig
        {
            TConfig[] configs = Resources.LoadAll<TConfig>(ConfigRoot);
            if (configs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No {typeof(TConfig).Name} assets were found below Resources/{ConfigRoot}.");
            }

            var configsByKey = new Dictionary<TKey, TConfig>(configs.Length);
            foreach (TConfig config in configs)
            {
                config.Validate();
                TKey key = keySelector(config);
                if (!configsByKey.TryAdd(key, config))
                {
                    throw new InvalidOperationException(
                        $"More than one {typeof(TConfig).Name} is configured for {key}.");
                }
            }

            return configsByKey;
        }

        private static TConfig GetRequired<TKey, TConfig>(
            IReadOnlyDictionary<TKey, TConfig> configs,
            TKey key,
            string configName)
        {
            if (configs == null)
                throw new InvalidOperationException("Static data has not been loaded yet.");
            if (configs.TryGetValue(key, out TConfig config))
                return config;

            throw new KeyNotFoundException($"{configName} for key {key} was not found.");
        }

        private static string Format<T>(IReadOnlyCollection<T> values) =>
            values.Count == 0 ? "none" : string.Join(", ", values);

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
