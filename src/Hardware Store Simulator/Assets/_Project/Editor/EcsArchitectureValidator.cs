using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Entitas;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Registrars;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.Installers;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Factory;
using HardwareStore.Infrastructure.View.Registrars;
using HardwareStore.Infrastructure.View.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace HardwareStore.Editor
{
    public static class EcsArchitectureValidator
    {
        private const string MenuPath = "Tools/Hardware Store/Validate ECS Architecture";
        private const string ProjectContextPath = "Assets/Resources/ProjectContext.prefab";
        private const string PrototypeScenePath = "Assets/Scenes/Prototype_Yard.unity";
        private const string PlayerConfigPath = "Assets/Resources/Configs/PlayerConfig.asset";
        private const string InteractionConfigPath = "Assets/Resources/Configs/InteractionConfig.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string ProductConfigPath = "Assets/Resources/Configs/ProductConfig.asset";
        private const string DeliveryConfigPath = "Assets/Resources/Configs/DeliveryConfig.asset";
        private const string CustomerVehicleConfigPath =
            "Assets/Resources/Configs/CustomerVehicleConfig.asset";
        private const string EconomyConfigPath = "Assets/Resources/Configs/EconomyConfig.asset";
        private const string OrderConfigPath = "Assets/Resources/Configs/OrderConfig.asset";
        private const string ProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/CementBag.prefab";
        private const string DeliveryVehiclePrefabPath = "Assets/_Project/Prefabs/Gameplay/DeliveryTruck.prefab";
        private const string CustomerVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicle.prefab";
        private const int RequiredStorageSlotCapacity = 6;

        private static readonly Type[] ExpectedInputComponents =
        {
            typeof(InputState),
            typeof(MoveInput),
            typeof(LookInput),
            typeof(SprintHeld),
            typeof(InteractPressed),
            typeof(DropPressed),
            typeof(ToggleCursorPressed),
            typeof(PointerLook)
        };

        private static readonly string[] ForbiddenRuntimeTypeNames =
        {
            "PrototypeSession",
            "PrototypeCompositionRoot",
            "ComponentRegistry",
            "GameComponentRegistry",
            "InputComponentRegistry",
            "GameEntityExtensions",
            "InputEntityExtensions",
            "HardwareStoreProjectInstaller",
            "PrototypeSceneInstaller",
            "HardwareStoreGameplayLoop",
            "PrototypeLevelInitializer",
            "PlayerView",
            "PlayerTransformRegistrar",
            "PlayerTransformComponent",
            "PlayerCameraRegistrar",
            "PlayerCameraComponent",
            "LoadingZoneView",
            "LoadingSlotsRegistrar",
            "CustomerVehicleView",
            "CustomerVehicleRegistrar",
            "CustomerVehicleViewRegistrar",
            "ProductView",
            "ProductViewRegistrar",
            "ProductViewComponent",
            "GameProductViewComponent",
            "GameContextEntityExtensions",
            "BindEntityViewSystem",
            "CustomerLoadingZone",
            "CustomerVehicleArriving",
            "CustomerVehicleWaiting",
            "CustomerVehicleLoading",
            "CustomerVehicleCompleted",
            "CustomerVehicleDeparting",
            "CustomerVehicleEntityId",
            "OrderWaiting",
            "OrderActive",
            "OrderCompleted",
            "OrderCompletedEvent",
            "OrderEntityId",
            "LoadingZoneEntityId",
            "HeldProductId",
            "Carried",
            "ProductEntityId"
        };

        private static readonly string[] ForbiddenRuntimeMethodNames =
        {
            "GetRequiredEntity",
            "EmitProductLoaded",
            "EmitProductStocked",
            "EmitOrderCompleted"
        };

        private static readonly (Type Type, string AssetPath)[] ExpectedGameplayConfigs =
        {
            (typeof(PlayerConfig), PlayerConfigPath),
            (typeof(InteractionConfig), InteractionConfigPath),
            (typeof(EconomyConfig), EconomyConfigPath),
            (typeof(DeliveryConfig), DeliveryConfigPath),
            (typeof(CustomerVehicleConfig), CustomerVehicleConfigPath),
            (typeof(OrderConfig), OrderConfigPath),
            (typeof(ProductConfig), ProductConfigPath)
        };

        [MenuItem(MenuPath, priority = 120)]
        public static void ValidateFromMenu()
        {
            ValidationResult result = ValidateOrThrow();
            Debug.Log(
                $"ECS architecture is valid: {result.GameComponentCount} Game components, " +
                $"{result.InputComponentCount} Input components, ProjectContext and Prototype_Yard composition roots.");
        }

        public static ValidationResult ValidateOrThrow()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "ECS architecture validation must run in Edit Mode.");

            Assembly runtimeAssembly = typeof(GameEntity).Assembly;
            Require(runtimeAssembly.GetName().Name == "Assembly-CSharp",
                $"Expected GameEntity in Assembly-CSharp, but found it in {runtimeAssembly.GetName().Name}.");

            Type[] runtimeTypes = runtimeAssembly.GetTypes();
            Type[] componentTypes = FindHardwareStoreComponents(runtimeTypes);

            ValidateComponentShapes(componentTypes);
            ValidateRegistries(componentTypes);
            ValidateLegacyTypesAreAbsent(runtimeTypes);
            ValidateStoreArchitecture(componentTypes);
            ValidateEntityViewBindingBoundary(runtimeTypes, componentTypes);
            ValidateEntityIndices(runtimeTypes, componentTypes);
            ValidateGameplayConfigBoundary(runtimeTypes);
            ValidateJennyPipeline();
            ValidateProjectContextPrefab();
            ValidatePlayerPrefab();
            ValidateSupplyChainAssets();
            ValidatePrototypeSceneComposition();
            ValidateBuildSettings();

            return new ValidationResult(
                GameComponentsLookup.componentTypes.Length,
                InputComponentsLookup.componentTypes.Length);
        }

        private static Type[] FindHardwareStoreComponents(IEnumerable<Type> runtimeTypes) =>
            runtimeTypes
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => typeof(IComponent).IsAssignableFrom(type))
                .Where(type => type.Namespace == "HardwareStore" ||
                               type.Namespace?.StartsWith("HardwareStore.", StringComparison.Ordinal) == true)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

        private static void ValidateComponentShapes(IEnumerable<Type> componentTypes)
        {
            foreach (Type componentType in componentTypes)
            {
                FieldInfo[] publicFields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);

                Require(publicFields.Length <= 1,
                    $"Component {componentType.FullName} contains {publicFields.Length} public instance fields; " +
                    "a component must be a tag or contain one Value field.");

                if (publicFields.Length == 1)
                {
                    Require(publicFields[0].Name == "Value",
                        $"The single public field of component {componentType.FullName} must be named Value, " +
                        $"but is named {publicFields[0].Name}.");
                }
            }
        }

        private static void ValidateRegistries(Type[] componentTypes)
        {
            ValidateRegistry("Game", GameComponentsLookup.componentNames, GameComponentsLookup.componentTypes);
            ValidateRegistry("Input", InputComponentsLookup.componentNames, InputComponentsLookup.componentTypes);

            var gameTypes = new HashSet<Type>(GameComponentsLookup.componentTypes);
            var inputTypes = new HashSet<Type>(InputComponentsLookup.componentTypes);
            var expectedInputTypes = new HashSet<Type>(ExpectedInputComponents);
            var discoveredTypes = new HashSet<Type>(componentTypes);

            Require(!gameTypes.Overlaps(inputTypes),
                "Game and Input component registries must not contain the same component type.");
            Require(inputTypes.SetEquals(expectedInputTypes),
                DescribeSetMismatch("Input registry", expectedInputTypes, inputTypes));

            var expectedGameTypes = new HashSet<Type>(discoveredTypes);
            expectedGameTypes.ExceptWith(expectedInputTypes);
            Require(gameTypes.SetEquals(expectedGameTypes),
                DescribeSetMismatch("Game registry", expectedGameTypes, gameTypes));

            gameTypes.UnionWith(inputTypes);
            Require(gameTypes.SetEquals(discoveredTypes),
                DescribeSetMismatch("Combined Game/Input registries", discoveredTypes, gameTypes));
        }

        private static void ValidateRegistry(string name, string[] componentNames, Type[] componentTypes)
        {
            Require(componentNames.Length == componentTypes.Length,
                $"{name} registry contains {componentNames.Length} names but {componentTypes.Length} types.");

            var uniqueTypes = new HashSet<Type>();
            var uniqueNames = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < componentTypes.Length; index++)
            {
                Type componentType = componentTypes[index];
                Require(componentType != null, $"{name} registry contains a null type at index {index}.");
                Require(typeof(IComponent).IsAssignableFrom(componentType),
                    $"{name} registry type {componentType.FullName} does not implement IComponent.");
                Require(uniqueTypes.Add(componentType),
                    $"{name} registry contains duplicate component type {componentType.FullName}.");
                string expectedName = ResolveGeneratedComponentName(componentType);
                Require(componentNames[index] == expectedName,
                    $"{name} registry name at index {index} is {componentNames[index]}, " +
                    $"but Jenny resolves {componentType.Name} as {expectedName}.");
                Require(uniqueNames.Add(componentNames[index]),
                    $"{name} registry contains duplicate component name {componentNames[index]}.");
            }
        }

        private static string ResolveGeneratedComponentName(Type componentType)
        {
            const string suffix = "Component";
            return componentType.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? componentType.Name[..^suffix.Length]
                : componentType.Name;
        }

        private static void ValidateLegacyTypesAreAbsent(IEnumerable<Type> runtimeTypes)
        {
            foreach (Type runtimeType in runtimeTypes)
            {
                Require(!ForbiddenRuntimeTypeNames.Contains(runtimeType.Name, StringComparer.Ordinal),
                    $"Legacy runtime type {runtimeType.FullName} is still present in Assembly-CSharp.");

                MethodInfo[] declaredMethods = runtimeType.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in declaredMethods)
                {
                    Require(!ForbiddenRuntimeMethodNames.Contains(method.Name, StringComparer.Ordinal),
                        $"Legacy runtime API {runtimeType.FullName}.{method.Name} is still present " +
                        "in Assembly-CSharp.");
                }
            }

            foreach (string sourcePath in GetRuntimeSourcePaths())
            {
                string source = File.ReadAllText(sourcePath);
                Require(!Regex.IsMatch(source, @"\.GetRequiredEntity\s*\("),
                    $"Legacy GetRequiredEntity helper is still used in {sourcePath}.");
                Require(!Regex.IsMatch(source, @"\.GetSingleEntity\s*\("),
                    $"Legacy GetSingleEntity lookup is still used in {sourcePath}; " +
                    "use a relation or entity index for the final architecture.");
            }
        }

        private static void ValidateStoreArchitecture(IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Require(discoveredComponents.Contains(typeof(Store)),
                $"{nameof(Store)} must be declared as a Game component.");
            Require(discoveredComponents.Contains(typeof(StoreEntityId)),
                $"{nameof(StoreEntityId)} must be declared as a Game relation component.");
            Require(discoveredComponents.Contains(typeof(CustomerDepartureDelayRemaining)),
                $"{nameof(CustomerDepartureDelayRemaining)} must be declared as a single-value " +
                "Game component.");
            Require(typeof(IStoreFactory).IsAssignableFrom(typeof(StoreFactory)),
                $"{nameof(StoreFactory)} must implement {nameof(IStoreFactory)}.");

            MethodInfo createMethod = typeof(IStoreFactory).GetMethod(
                nameof(IStoreFactory.Create),
                new[] { typeof(IStoreSceneData) });
            Require(createMethod != null && createMethod.ReturnType == typeof(GameEntity),
                $"{nameof(IStoreFactory)} must create and return a Store GameEntity from " +
                $"{nameof(IStoreSceneData)}.");
        }

        private static void ValidateEntityViewBindingBoundary(Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Require(discoveredComponents.Contains(typeof(ViewComponent)),
                $"{nameof(ViewComponent)} must be the single ECS view-reference component.");
            Require(discoveredComponents.Contains(typeof(SceneViewKey)),
                $"{nameof(SceneViewKey)} must identify views authored in a scene.");
            Require(discoveredComponents.Contains(typeof(ViewPrefabComponent)) &&
                    discoveredComponents.Contains(typeof(SpawnPosition)) &&
                    discoveredComponents.Contains(typeof(SpawnRotation)),
                "Runtime view binding requires ViewPrefab, SpawnPosition and SpawnRotation components.");

            Require(typeof(IEntityView).IsAssignableFrom(typeof(EntityBehaviour)),
                $"{nameof(EntityBehaviour)} must implement {nameof(IEntityView)}.");
            MethodInfo setEntityContract = typeof(IEntityView).GetMethod(
                nameof(IEntityView.SetEntity),
                new[] { typeof(GameEntity) });
            MethodInfo setEntityImplementation = typeof(EntityBehaviour).GetMethod(
                nameof(IEntityView.SetEntity),
                new[] { typeof(GameEntity) });
            Require(setEntityContract != null && setEntityContract.ReturnType == typeof(void) &&
                    setEntityImplementation != null && setEntityImplementation.ReturnType == typeof(void),
                $"{nameof(IEntityView)} and {nameof(EntityBehaviour)} must expose the same SetEntity boundary.");

            Require(typeof(IEntityViewFactory).IsAssignableFrom(typeof(EntityViewFactory)),
                $"{nameof(EntityViewFactory)} must implement {nameof(IEntityViewFactory)}.");
            RequireMethod(
                typeof(IEntityViewFactory),
                nameof(IEntityViewFactory.CreateViewFromPrefab),
                typeof(EntityBehaviour),
                typeof(GameEntity));
            RequireMethod(
                typeof(IEntityViewFactory),
                nameof(IEntityViewFactory.BindExistingView),
                typeof(EntityBehaviour),
                typeof(GameEntity),
                typeof(EntityBehaviour));

            ValidateExecuteOnlyViewBinder(
                typeof(BindEntityViewFromSceneSystem),
                typeof(GameContext),
                typeof(IStoreSceneData),
                typeof(IEntityViewFactory));
            ValidateExecuteOnlyViewBinder(
                typeof(BindEntityViewFromPrefabSystem),
                typeof(GameContext),
                typeof(IEntityViewFactory));

            string factoryPath = GetRuntimeSourcePath(
                "Infrastructure", "View", "Factory", "EntityViewFactory.cs");
            string sceneBinderPath = GetRuntimeSourcePath(
                "Infrastructure", "View", "Systems", "BindEntityViewFromSceneSystem.cs");
            string prefabBinderPath = GetRuntimeSourcePath(
                "Infrastructure", "View", "Systems", "BindEntityViewFromPrefabSystem.cs");
            var setEntityCallers = new List<string>();
            var bindExistingViewCallers = new List<string>();
            var createPrefabViewCallers = new List<string>();
            foreach (string sourcePath in GetRuntimeSourcePaths())
            {
                string source = File.ReadAllText(sourcePath);
                int setEntityCallCount = Regex.Matches(source, @"\.SetEntity\s*\(").Count;
                for (int index = 0; index < setEntityCallCount; index++)
                    setEntityCallers.Add(sourcePath);
                int bindExistingCallCount = Regex.Matches(
                    source,
                    @"\.BindExistingView\s*\(").Count;
                for (int index = 0; index < bindExistingCallCount; index++)
                    bindExistingViewCallers.Add(sourcePath);
                int createPrefabCallCount = Regex.Matches(
                    source,
                    @"\.CreateViewFromPrefab\s*\(").Count;
                for (int index = 0; index < createPrefabCallCount; index++)
                    createPrefabViewCallers.Add(sourcePath);
            }

            Require(setEntityCallers.Count == 1 && PathsEqual(setEntityCallers[0], factoryPath),
                "EntityBehaviour.SetEntity must be called exactly once in runtime source and only by " +
                $"{nameof(EntityViewFactory)}. Found: [{string.Join(", ", setEntityCallers)}].");
            Require(bindExistingViewCallers.Count == 1 &&
                    PathsEqual(bindExistingViewCallers[0], sceneBinderPath),
                $"Only {nameof(BindEntityViewFromSceneSystem)} may bind an authored scene view.");
            Require(createPrefabViewCallers.Count == 1 &&
                    PathsEqual(createPrefabViewCallers[0], prefabBinderPath),
                $"Only {nameof(BindEntityViewFromPrefabSystem)} may request a runtime prefab view.");

            string bindFeatureSource = ReadRuntimeSource(
                "Infrastructure", "View", "BindViewFeature.cs");
            string sceneBinderToken = "Create<BindEntityViewFromSceneSystem>()";
            string prefabBinderToken = "Create<BindEntityViewFromPrefabSystem>()";
            int sceneBinderIndex = bindFeatureSource.IndexOf(sceneBinderToken, StringComparison.Ordinal);
            int prefabBinderIndex = bindFeatureSource.IndexOf(prefabBinderToken, StringComparison.Ordinal);
            Require(sceneBinderIndex >= 0 && prefabBinderIndex > sceneBinderIndex &&
                    CountOccurrences(bindFeatureSource, sceneBinderToken) == 1 &&
                    CountOccurrences(bindFeatureSource, prefabBinderToken) == 1,
                $"{nameof(BindViewFeature)} must execute the scene binder once before the runtime-prefab binder.");

            string sceneBinderSource = ReadRuntimeSource(
                "Infrastructure", "View", "Systems", "BindEntityViewFromSceneSystem.cs");
            RequireSourceContains(sceneBinderSource,
                nameof(SceneViewKey),
                "NoneOf(GameMatcher.View, GameMatcher.Destructed)",
                "_viewFactory.BindExistingView",
                "RemoveSceneViewKey");

            string prefabBinderSource = ReadRuntimeSource(
                "Infrastructure", "View", "Systems", "BindEntityViewFromPrefabSystem.cs");
            RequireSourceContains(prefabBinderSource,
                nameof(ViewPrefabComponent).Replace("Component", string.Empty),
                nameof(SpawnPosition),
                nameof(SpawnRotation),
                "_viewFactory.CreateViewFromPrefab",
                "RemoveSpawnPosition",
                "RemoveSpawnRotation");

            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IEntityViewFactory>().To<EntityViewFactory>().AsSingle()");

            Require(runtimeTypes.Contains(typeof(BindViewFeature)),
                $"{nameof(BindViewFeature)} must remain part of Assembly-CSharp.");
        }

        private static void ValidateEntityIndices(Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] customerVisitRoles =
            {
                typeof(CustomerVisit),
                typeof(CustomerVehicle),
                typeof(Order),
                typeof(LoadingZone),
                typeof(CustomerVisitArriving),
                typeof(CustomerVisitWaiting),
                typeof(CustomerVisitLoading),
                typeof(CustomerVisitCompleted),
                typeof(CustomerVisitDeparting)
            };
            foreach (Type role in customerVisitRoles)
            {
                Require(discoveredComponents.Contains(role),
                    $"Unified customer visits require the {role.Name} Game component.");
            }

            Require(discoveredComponents.Contains(typeof(CustomerVisitEntityId)),
                $"{nameof(CustomerVisitEntityId)} must relate loaded products to their visit.");
            Require(discoveredComponents.Contains(typeof(CustomerVisitStoreEntityId)),
                $"{nameof(CustomerVisitStoreEntityId)} must uniquely relate the active visit to its store.");
            Require(discoveredComponents.Contains(typeof(DeliveryProcurementTerminalEntityId)),
                $"{nameof(DeliveryProcurementTerminalEntityId)} must uniquely relate the active delivery " +
                "to its procurement terminal.");
            Require(discoveredComponents.Contains(typeof(CarrierEntityId)),
                $"{nameof(CarrierEntityId)} must relate the carried product to its carrier.");
            RequireComponentIndexAttribute(
                typeof(CarrierEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerVisitStoreEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(DeliveryProcurementTerminalEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerVisitEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");

            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithCarrierEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithCustomerVisitEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithCustomerVisitStoreEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithDeliveryProcurementTerminalEntityId",
                typeof(GameEntity));

            string combinedRuntimeSource = string.Join(
                Environment.NewLine,
                GetRuntimeSourcePaths().Select(File.ReadAllText));
            Require(!combinedRuntimeSource.Contains(
                    "new PrimaryEntityIndex<",
                    StringComparison.Ordinal),
                "Single-value primary indices must be generated by Jenny from " +
                "[PrimaryEntityIndex] component values, not registered manually.");
            Require(Regex.IsMatch(combinedRuntimeSource, @"\.GetEntityWithCarrierEntityId\s*\("),
                $"Runtime carrying logic must consume the {nameof(CarrierEntityId)} primary index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithCustomerVisitStoreEntityId\s*\("),
                $"Runtime customer/order logic must consume the {nameof(CustomerVisitStoreEntityId)} " +
                "primary index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithDeliveryProcurementTerminalEntityId\s*\("),
                $"Runtime delivery logic must consume the " +
                $"{nameof(DeliveryProcurementTerminalEntityId)} primary index.");

            Require(typeof(ICustomerVisitFactory).IsAssignableFrom(typeof(CustomerVisitFactory)),
                $"{nameof(CustomerVisitFactory)} must implement {nameof(ICustomerVisitFactory)}.");
            RequireMethod(
                typeof(ICustomerVisitFactory),
                nameof(ICustomerVisitFactory.Create),
                typeof(GameEntity),
                typeof(GameEntity),
                typeof(Pose[]),
                typeof(Pose[]));
            RequireMethod(
                typeof(IOrderFactory),
                nameof(IOrderFactory.AddOrderComponents),
                typeof(GameEntity),
                typeof(GameEntity),
                typeof(int));

            string customerVisitFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "CustomerVisitFactory.cs");
            RequireSourceContains(customerVisitFactorySource,
                "isCustomerVisit = true",
                "isCustomerVehicle = true",
                "isCustomerVisitArriving = true",
                "isLoadingZone = true",
                "AddCustomerVisitStoreEntityId",
                "_orderFactory.AddOrderComponents");
            string orderFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "OrderFactory.cs");
            RequireSourceContains(orderFactorySource,
                "AddOrderComponents",
                "isOrder = true");
            Require(!orderFactorySource.Contains("CreateEntity.", StringComparison.Ordinal),
                $"{nameof(OrderFactory)} must enrich the unified CustomerVisit entity, not create another one.");

            string completeCustomerVisitSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerVehicleDepartureSystem.cs");
            RequireSourceContains(completeCustomerVisitSource,
                "RemoveCustomerVisitStoreEntityId",
                "isDestructed = true");

            string deliveryFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "DeliveryFactory.cs");
            RequireSourceContains(deliveryFactorySource,
                "AddDeliveryProcurementTerminalEntityId");
            string completeDeliverySource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems", "CompleteDeliverySystem.cs");
            RequireSourceContains(completeDeliverySource,
                "RemoveDeliveryProcurementTerminalEntityId",
                "isDeliveryActive = false",
                "isDestructed = true");
        }

        private static void ValidateGameplayConfigBoundary(IEnumerable<Type> runtimeTypes)
        {
            var expectedConfigTypes =
                new HashSet<Type>(ExpectedGameplayConfigs.Select(config => config.Type));
            var discoveredConfigTypes = new HashSet<Type>(runtimeTypes
                .Where(type => type.Namespace == typeof(PlayerConfig).Namespace)
                .Where(type => !type.IsAbstract && typeof(ScriptableObject).IsAssignableFrom(type)));
            Require(discoveredConfigTypes.SetEquals(expectedConfigTypes),
                DescribeSetMismatch(
                    "Gameplay config types",
                    expectedConfigTypes,
                    discoveredConfigTypes));

            var staticDataConfigTypes = new HashSet<Type>(typeof(IStaticDataService)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.PropertyType));
            Require(staticDataConfigTypes.SetEquals(expectedConfigTypes),
                DescribeSetMismatch(
                    nameof(IStaticDataService),
                    expectedConfigTypes,
                    staticDataConfigTypes));

            foreach ((Type configType, string assetPath) in ExpectedGameplayConfigs)
            {
                Require(configType.IsSealed && typeof(ScriptableObject).IsAssignableFrom(configType),
                    $"Gameplay config {configType.FullName} must be a sealed ScriptableObject.");
                Require(typeof(IValidatableConfig).IsAssignableFrom(configType),
                    $"Gameplay config {configType.FullName} must implement {nameof(IValidatableConfig)}.");

                MethodInfo validateMethod = configType.GetMethod(
                    nameof(IValidatableConfig.Validate),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                Require(validateMethod != null && validateMethod.ReturnType == typeof(void),
                    $"Gameplay config {configType.FullName} must declare explicit void Validate().");

                PropertyInfo[] properties = configType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                Require(properties.All(property => property.CanRead && !property.CanWrite),
                    $"Gameplay config {configType.FullName} may expose only read-only public properties.");

                string configSource = ReadRuntimeSource(
                    "Gameplay", "Configs", configType.Name + ".cs");
                ValidatePureConfigGetters(configType, properties, configSource);

                ScriptableObject configAsset =
                    AssetDatabase.LoadAssetAtPath(assetPath, configType) as ScriptableObject;
                Require(configAsset != null,
                    $"Gameplay config asset {assetPath} of type {configType.Name} is missing.");
                try
                {
                    ((IValidatableConfig)configAsset).Validate();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"ECS architecture validation failed: {assetPath} did not pass " +
                        $"{nameof(IValidatableConfig)}.{nameof(IValidatableConfig.Validate)}().",
                        exception);
                }
            }

            string customerVehicleConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(CustomerVehicleConfig) + ".cs");
            int lastConfigurationAssignment = customerVehicleConfigSource.IndexOf(
                "_nextCustomerDelay = nextCustomerDelay",
                StringComparison.Ordinal);
            int explicitConfigurationValidation = customerVehicleConfigSource.IndexOf(
                "Validate();",
                lastConfigurationAssignment >= 0 ? lastConfigurationAssignment : 0,
                StringComparison.Ordinal);
            Require(lastConfigurationAssignment >= 0 &&
                    explicitConfigurationValidation > lastConfigurationAssignment,
                $"{nameof(CustomerVehicleConfig)}.Configure must assign all values and then call Validate().");

            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", "StaticDataService.cs");
            Require(!staticDataSource.Contains("_ =", StringComparison.Ordinal),
                $"{nameof(StaticDataService)} must not validate configs through discarded getter reads.");
            RequireSourceContains(staticDataSource,
                "player.Validate()",
                "interaction.Validate()",
                "economy.Validate()",
                "delivery.Validate()",
                "customerVehicle.Validate()",
                "order.Validate()",
                "product.Validate()",
                "ValidateCompatibility(economy, delivery, order, product)",
                "where TConfig : ScriptableObject, IValidatableConfig");

            int compatibilityValidation = staticDataSource.IndexOf(
                "ValidateCompatibility(economy, delivery, order, product)",
                StringComparison.Ordinal);
            int firstPublication = staticDataSource.IndexOf("Player = player", StringComparison.Ordinal);
            int finalPublication = staticDataSource.IndexOf("Product = product", StringComparison.Ordinal);
            Require(compatibilityValidation >= 0 && firstPublication > compatibilityValidation,
                $"{nameof(StaticDataService)} must validate all local configs and compatibility " +
                "before publishing any property.");
            Require(finalPublication > firstPublication,
                $"{nameof(StaticDataService)} must publish the complete validated config set atomically.");
        }

        private static void ValidateJennyPipeline()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string generator = Path.Combine(repositoryRoot, "Jenny", "Jenny-Gen");
            string properties = Path.Combine(repositoryRoot, "Jenny", "JennyRoslyn.properties");
            string customGenerators = Path.Combine(repositoryRoot, "src", "CustomGenerators", "bin",
                "KSyndicate.CustomGenerators.Plugins.dll");

            Require(File.Exists(generator), $"Jenny launcher is missing at {generator}.");
            Require(File.Exists(properties), $"Jenny properties are missing at {properties}.");
            Require(File.Exists(customGenerators), $"Custom single-value generators are missing at {customGenerators}.");

            string generatedEntityPath = Path.Combine(Application.dataPath, "_Project", "Code", "Generated",
                "Game", "GameEntity.cs");
            Require(File.Exists(generatedEntityPath), $"Generated GameEntity is missing at {generatedEntityPath}.");
            Require(File.ReadAllText(generatedEntityPath).Contains("<auto-generated>", StringComparison.Ordinal),
                "GameEntity.cs does not contain the Jenny auto-generated header.");
        }

        private static void ValidateProjectContextPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectContextPath);
            Require(prefab != null, $"ProjectContext prefab is missing at {ProjectContextPath}.");

            ProjectContext[] contexts = prefab.GetComponentsInChildren<ProjectContext>(true);
            BootstrapInstaller[] installers = prefab.GetComponentsInChildren<BootstrapInstaller>(true);

            Require(contexts.Length == 1,
                $"{ProjectContextPath} must contain exactly one ProjectContext, found {contexts.Length}.");
            Require(installers.Length == 1,
                $"{ProjectContextPath} must contain exactly one BootstrapInstaller, " +
                $"found {installers.Length}.");
            Require(contexts[0].Installers.Count() == 1,
                $"{ProjectContextPath} must register exactly one MonoInstaller, " +
                $"found {contexts[0].Installers.Count()}.");
            Require(contexts[0].Installers.Contains(installers[0]),
                "BootstrapInstaller is present in ProjectContext.prefab but is not registered " +
                "in ProjectContext.Installers.");
        }

        private static void ValidatePlayerPrefab()
        {
            PlayerConfig playerConfig = AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            Require(playerConfig != null, $"Player config is missing at {PlayerConfigPath}.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Require(prefab != null, $"Player prefab is missing at {PlayerPrefabPath}.");
            Require(prefab.activeSelf, $"Player prefab root at {PlayerPrefabPath} must be active.");
            Require(prefab.transform.localPosition == Vector3.zero,
                $"Player prefab root at {PlayerPrefabPath} must have zero local position.");
            Require(prefab.transform.localRotation == Quaternion.identity,
                $"Player prefab root at {PlayerPrefabPath} must have identity local rotation.");
            Require(prefab.transform.localScale == Vector3.one,
                $"Player prefab root at {PlayerPrefabPath} must have unit local scale.");

            EntityBehaviour[] views = RequireExactlyOneInPrefab<EntityBehaviour>(prefab, PlayerPrefabPath);
            TransformRegistrar[] transforms = RequireExactlyOneInPrefab<TransformRegistrar>(prefab, PlayerPrefabPath);
            CharacterControllerRegistrar[] characterRegistrars =
                RequireExactlyOneInPrefab<CharacterControllerRegistrar>(prefab, PlayerPrefabPath);
            ViewPivotRegistrar[] viewPivots = RequireExactlyOneInPrefab<ViewPivotRegistrar>(prefab, PlayerPrefabPath);
            CameraRegistrar[] cameraRegistrars = RequireExactlyOneInPrefab<CameraRegistrar>(prefab, PlayerPrefabPath);
            CarryAnchorRegistrar[] carryAnchors =
                RequireExactlyOneInPrefab<CarryAnchorRegistrar>(prefab, PlayerPrefabPath);
            DropOriginRegistrar[] dropOrigins =
                RequireExactlyOneInPrefab<DropOriginRegistrar>(prefab, PlayerPrefabPath);
            CharacterController[] controllers =
                RequireExactlyOneInPrefab<CharacterController>(prefab, PlayerPrefabPath);
            Camera[] cameras = RequireExactlyOneInPrefab<Camera>(prefab, PlayerPrefabPath);
            AudioListener[] listeners = RequireExactlyOneInPrefab<AudioListener>(prefab, PlayerPrefabPath);

            Require(views[0].gameObject == prefab,
                $"The only EntityBehaviour in {PlayerPrefabPath} must be on the prefab root.");
            Require(transforms[0].gameObject == prefab,
                $"The only TransformRegistrar in {PlayerPrefabPath} must be on the prefab root.");
            Require(characterRegistrars[0].gameObject == prefab && controllers[0].gameObject == prefab,
                $"CharacterController and its registrar in {PlayerPrefabPath} must be on the prefab root.");
            Require(cameraRegistrars[0].gameObject == cameras[0].gameObject &&
                    listeners[0].gameObject == cameras[0].gameObject,
                $"Camera, CameraRegistrar and AudioListener in {PlayerPrefabPath} must share one object.");
            Require(viewPivots[0].transform.IsChildOf(prefab.transform),
                $"ViewPivotRegistrar in {PlayerPrefabPath} must belong to the player hierarchy.");
            Require(carryAnchors[0].transform.IsChildOf(cameras[0].transform) &&
                    dropOrigins[0].transform.IsChildOf(cameras[0].transform),
                $"Carry and drop anchors in {PlayerPrefabPath} must be children of the player camera.");

            SerializedObject serializedConfig = new(playerConfig);
            SerializedProperty viewPrefab = serializedConfig.FindProperty("_viewPrefab");
            Require(viewPrefab != null, $"{nameof(PlayerConfig)} must declare _viewPrefab.");
            Require(viewPrefab.objectReferenceValue == views[0],
                $"{PlayerConfigPath} must reference the EntityBehaviour root from {PlayerPrefabPath}.");
        }

        private static void ValidateSupplyChainAssets()
        {
            ProductConfig productConfig = RequireAsset<ProductConfig>(ProductConfigPath);
            DeliveryConfig deliveryConfig = RequireAsset<DeliveryConfig>(DeliveryConfigPath);
            CustomerVehicleConfig customerVehicleConfig =
                RequireAsset<CustomerVehicleConfig>(CustomerVehicleConfigPath);
            EconomyConfig economyConfig = RequireAsset<EconomyConfig>(EconomyConfigPath);
            OrderConfig orderConfig = RequireAsset<OrderConfig>(OrderConfigPath);

            Require(deliveryConfig.ProductType == ProductTypeId.CementBag,
                $"{DeliveryConfigPath} must deliver {ProductTypeId.CementBag}.");
            Require(deliveryConfig.ProductCount == 3,
                $"{DeliveryConfigPath} must contain exactly 3 products for the prototype slice.");
            Require(deliveryConfig.PurchaseUnitPrice == 200,
                $"{DeliveryConfigPath} must use a purchase unit price of 200.");
            Require(deliveryConfig.TotalCost == deliveryConfig.ProductCount * deliveryConfig.PurchaseUnitPrice,
                $"{DeliveryConfigPath} has an inconsistent total cost.");
            Require(economyConfig.InitialMoney == 1000,
                $"{EconomyConfigPath} must start the prototype with 1000.");
            Require(economyConfig.InitialMoney >= deliveryConfig.TotalCost,
                "Initial money must be sufficient for the configured inbound delivery.");
            Require(productConfig.ProductType == deliveryConfig.ProductType &&
                    orderConfig.RequiredProductType == deliveryConfig.ProductType,
                "Product, delivery and customer order configs must use the same product type.");
            Require(orderConfig.RequiredProductCount == 2,
                $"{OrderConfigPath} must require exactly 2 products for the prototype slice.");
            Require(orderConfig.Reward == 700,
                $"{OrderConfigPath} must reward 700 for the prototype slice.");
            Require(deliveryConfig.ProductCount > orderConfig.RequiredProductCount,
                "The delivery must leave at least one product in storage after the customer order.");
            Require(Quaternion.Angle(productConfig.HeldRotationOffset, Quaternion.Euler(8f, 0f, 0f)) < 0.01f,
                $"{ProductConfigPath} must use an 8 degree held rotation offset around X.");
            Require(Mathf.Approximately(productConfig.DropForwardDistance, 1.15f),
                $"{ProductConfigPath} must use a drop forward distance of 1.15.");
            Require(productConfig.WorldInterpolation == RigidbodyInterpolation.Interpolate,
                $"{ProductConfigPath} must use {RigidbodyInterpolation.Interpolate} world interpolation.");
            Require(productConfig.WorldCollisionDetection == CollisionDetectionMode.ContinuousSpeculative,
                $"{ProductConfigPath} must use {CollisionDetectionMode.ContinuousSpeculative} " +
                "world collision detection.");
            Require(Mathf.Approximately(customerVehicleConfig.ArrivalSpeed, 4f),
                $"{CustomerVehicleConfigPath} must use an arrival speed of 4.");
            Require(Mathf.Approximately(customerVehicleConfig.DepartureSpeed, 5.25f),
                $"{CustomerVehicleConfigPath} must use a departure speed of 5.25.");
            Require(Mathf.Approximately(customerVehicleConfig.RotationSpeed, 135f),
                $"{CustomerVehicleConfigPath} must use a rotation speed of 135 degrees per second.");
            Require(Mathf.Approximately(customerVehicleConfig.WaypointTolerance, 0.08f),
                $"{CustomerVehicleConfigPath} must use a waypoint tolerance of 0.08.");
            Require(Mathf.Approximately(customerVehicleConfig.CompletedDwellDuration, 1.25f),
                $"{CustomerVehicleConfigPath} must keep a completed customer visible for 1.25 seconds.");
            Require(Mathf.Approximately(customerVehicleConfig.FirstCustomerDelay, 1f) &&
                    Mathf.Approximately(customerVehicleConfig.NextCustomerDelay, 4f),
                $"{CustomerVehicleConfigPath} must use prototype customer delays of 1 and 4 seconds.");

            GameObject productPrefab = RequireAsset<GameObject>(ProductPrefabPath);
            ValidatePrefabRoot(productPrefab, ProductPrefabPath, requireUnitScale: false);
            InteractionView[] productViews =
                RequireExactlyOneInPrefab<InteractionView>(productPrefab, ProductPrefabPath);
            EntityBehaviour[] productEntityViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(productPrefab, ProductPrefabPath);
            TransformRegistrar[] productTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(productPrefab, ProductPrefabPath);
            InteractionViewRegistrar[] interactionRegistrars =
                RequireExactlyOneInPrefab<InteractionViewRegistrar>(productPrefab, ProductPrefabPath);
            RigidbodyRegistrar[] rigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(productPrefab, ProductPrefabPath);
            CollidersRegistrar[] collidersRegistrars =
                RequireExactlyOneInPrefab<CollidersRegistrar>(productPrefab, ProductPrefabPath);
            EntityComponentRegistrar[] allRegistrars =
                productPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Rigidbody[] rigidbodies = RequireExactlyOneInPrefab<Rigidbody>(productPrefab, ProductPrefabPath);
            InteractionHighlight[] highlights =
                RequireExactlyOneInPrefab<InteractionHighlight>(productPrefab, ProductPrefabPath);
            Collider[] productColliders = productPrefab.GetComponentsInChildren<Collider>(true);

            Require(productViews[0].GetType() == typeof(InteractionView) &&
                    productViews[0].gameObject == productPrefab &&
                    productEntityViews[0].gameObject == productPrefab,
                $"The root view in {ProductPrefabPath} must be a non-specialized InteractionView.");
            Require(productTransforms[0].gameObject == productPrefab &&
                    interactionRegistrars[0].gameObject == productPrefab &&
                    rigidbodyRegistrars[0].gameObject == productPrefab &&
                    collidersRegistrars[0].gameObject == productPrefab &&
                    rigidbodies[0].gameObject == productPrefab &&
                    highlights[0].gameObject == productPrefab,
                $"All product registrars and required adapters in {ProductPrefabPath} must be on its root.");
            var expectedProductRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(InteractionViewRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(CollidersRegistrar)
            };
            Require(allRegistrars.Length == expectedProductRegistrarTypes.Count &&
                    new HashSet<Type>(allRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedProductRegistrarTypes),
                $"{ProductPrefabPath} must contain exactly the generic Transform, InteractionView, " +
                "Rigidbody and Colliders registrars.");
            Require(productColliders.Length >= 2 &&
                    productColliders.All(collider => collider.enabled && collider.gameObject.activeSelf),
                $"Every collider in {ProductPrefabPath} must be enabled on an active object.");
            Require(productColliders.Count(collider => !collider.isTrigger) == 1,
                $"{ProductPrefabPath} must contain exactly one solid product collider.");
            Require(productColliders.Any(collider => collider.isTrigger),
                $"{ProductPrefabPath} must contain an interaction trigger.");
            Require(Mathf.Approximately(rigidbodies[0].mass, productConfig.Mass) &&
                    rigidbodies[0].interpolation == productConfig.WorldInterpolation &&
                    rigidbodies[0].collisionDetectionMode == productConfig.WorldCollisionDetection,
                $"The Rigidbody in {ProductPrefabPath} must match {ProductConfigPath} physics values.");
            Require(productConfig.ViewPrefab == productEntityViews[0],
                $"{ProductConfigPath} must reference the EntityBehaviour root from {ProductPrefabPath}.");

            GameObject deliveryPrefab = RequireAsset<GameObject>(DeliveryVehiclePrefabPath);
            ValidatePrefabRoot(deliveryPrefab, DeliveryVehiclePrefabPath, requireUnitScale: true);
            EntityBehaviour[] deliveryViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(deliveryPrefab, DeliveryVehiclePrefabPath);
            TransformRegistrar[] deliveryTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(deliveryPrefab, DeliveryVehiclePrefabPath);
            SlotsRegistrar[] deliverySlotRegistrars =
                RequireExactlyOneInPrefab<SlotsRegistrar>(deliveryPrefab, DeliveryVehiclePrefabPath);
            Transform[] deliverySlots = ReadSlots(deliverySlotRegistrars[0], DeliveryVehiclePrefabPath);

            Require(deliveryViews[0].gameObject == deliveryPrefab &&
                    deliveryTransforms[0].gameObject == deliveryPrefab &&
                    deliverySlotRegistrars[0].gameObject == deliveryPrefab,
                $"The delivery view and registrars in {DeliveryVehiclePrefabPath} must be on its root.");
            Require(deliverySlots.Length == deliveryConfig.ProductCount,
                $"{DeliveryVehiclePrefabPath} must expose exactly {deliveryConfig.ProductCount} cargo slots.");
            Require(deliverySlots.All(slot => slot.IsChildOf(deliveryPrefab.transform)),
                $"Every cargo slot in {DeliveryVehiclePrefabPath} must belong to the prefab hierarchy.");
            Require(!ContainsPrefabInstance(deliveryPrefab, productPrefab),
                $"{DeliveryVehiclePrefabPath} must be empty before runtime cargo spawning.");
            Require(deliveryConfig.ViewPrefab == deliveryViews[0],
                $"{DeliveryConfigPath} must reference the EntityBehaviour root from " +
                $"{DeliveryVehiclePrefabPath}.");

            GameObject customerVehiclePrefab = RequireAsset<GameObject>(CustomerVehiclePrefabPath);
            ValidatePrefabRoot(customerVehiclePrefab, CustomerVehiclePrefabPath, requireUnitScale: true);
            InteractionView[] customerViews =
                RequireExactlyOneInPrefab<InteractionView>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            EntityBehaviour[] customerEntityViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            TransformRegistrar[] customerTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            InteractionViewRegistrar[] customerInteractionRegistrars =
                RequireExactlyOneInPrefab<InteractionViewRegistrar>(
                    customerVehiclePrefab, CustomerVehiclePrefabPath);
            SlotsRegistrar[] customerSlotRegistrars =
                RequireExactlyOneInPrefab<SlotsRegistrar>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            RigidbodyRegistrar[] customerRigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            CollidersRegistrar[] customerCollidersRegistrars =
                RequireExactlyOneInPrefab<CollidersRegistrar>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            Rigidbody[] customerRigidbodies =
                RequireExactlyOneInPrefab<Rigidbody>(customerVehiclePrefab, CustomerVehiclePrefabPath);
            InteractionHighlight[] customerHighlights =
                RequireExactlyOneInPrefab<InteractionHighlight>(
                    customerVehiclePrefab, CustomerVehiclePrefabPath);
            Collider[] customerColliders = customerVehiclePrefab.GetComponentsInChildren<Collider>(true);
            Transform customerBodyColliderTransform =
                customerVehiclePrefab.transform.Find("Body Collider");
            Transform customerInteractionAreaTransform =
                customerVehiclePrefab.transform.Find("Interaction Area");
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            Require(ignoreRaycastLayer >= 0,
                "Required built-in Ignore Raycast layer is missing.");
            Require(customerBodyColliderTransform != null,
                $"{CustomerVehiclePrefabPath} must contain a Body Collider child.");
            Require(customerInteractionAreaTransform != null,
                $"{CustomerVehiclePrefabPath} must contain an Interaction Area child.");
            Collider customerBodyCollider = customerBodyColliderTransform.GetComponent<Collider>();
            Collider customerInteractionCollider = customerInteractionAreaTransform.GetComponent<Collider>();
            Require(customerBodyCollider != null && !customerBodyCollider.isTrigger &&
                    customerBodyCollider.gameObject.layer == ignoreRaycastLayer,
                $"The solid Body Collider in {CustomerVehiclePrefabPath} must use Ignore Raycast.");
            Require(customerInteractionCollider != null && customerInteractionCollider.isTrigger &&
                    customerInteractionCollider.gameObject.layer != ignoreRaycastLayer,
                $"The rear Interaction Area in {CustomerVehiclePrefabPath} must be the raycastable trigger.");
            Transform[] customerSlots = ReadSlots(customerSlotRegistrars[0], CustomerVehiclePrefabPath);
            EntityComponentRegistrar[] customerRegistrars =
                customerVehiclePrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);

            Require(customerViews[0].GetType() == typeof(InteractionView) &&
                    customerViews[0].gameObject == customerVehiclePrefab &&
                    customerEntityViews[0] == customerViews[0],
                $"The root view in {CustomerVehiclePrefabPath} must be a non-specialized InteractionView.");
            Require(customerTransforms[0].gameObject == customerVehiclePrefab &&
                    customerInteractionRegistrars[0].gameObject == customerVehiclePrefab &&
                    customerSlotRegistrars[0].gameObject == customerVehiclePrefab &&
                    customerRigidbodyRegistrars[0].gameObject == customerVehiclePrefab &&
                    customerCollidersRegistrars[0].gameObject == customerVehiclePrefab &&
                    customerRigidbodies[0].gameObject == customerVehiclePrefab,
                $"The customer vehicle view, Rigidbody and generic registrars in " +
                $"{CustomerVehiclePrefabPath} must be on its root.");
            var expectedCustomerRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(InteractionViewRegistrar),
                typeof(SlotsRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(CollidersRegistrar)
            };
            Require(customerRegistrars.Length == expectedCustomerRegistrarTypes.Count &&
                    new HashSet<Type>(customerRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedCustomerRegistrarTypes),
                $"{CustomerVehiclePrefabPath} must contain exactly the generic Transform, InteractionView, " +
                "Slots, Rigidbody and Colliders registrars.");
            Require(customerSlots.Length == orderConfig.RequiredProductCount &&
                    customerSlots.All(slot => slot.IsChildOf(customerVehiclePrefab.transform)),
                $"{CustomerVehiclePrefabPath} must expose exactly {orderConfig.RequiredProductCount} " +
                "customer cargo slots within its hierarchy.");
            Require(customerColliders.Length >= 2 &&
                    customerColliders.All(collider => collider.enabled && collider.gameObject.activeSelf),
                $"Every collider in {CustomerVehiclePrefabPath} must be enabled on an active object.");
            Require(customerColliders.Count(collider => collider.isTrigger) == 1 &&
                    customerColliders.Where(collider => !collider.isTrigger)
                        .All(collider => collider.gameObject.layer == ignoreRaycastLayer),
                $"{CustomerVehiclePrefabPath} must expose only its rear trigger to interaction raycasts.");
            Rigidbody customerBody = customerRigidbodies[0];
            Require(customerBody.isKinematic && !customerBody.useGravity &&
                    customerBody.interpolation == RigidbodyInterpolation.None,
                $"The Rigidbody in {CustomerVehiclePrefabPath} must be kinematic, gravity-free and use " +
                $"{RigidbodyInterpolation.None} interpolation.");
            SerializedProperty customerHighlight =
                new SerializedObject(customerViews[0]).FindProperty("_highlight");
            Require(customerHighlight?.objectReferenceValue == customerHighlights[0],
                $"The InteractionView in {CustomerVehiclePrefabPath} must reference its loading highlight.");
            Require(!ContainsPrefabInstance(customerVehiclePrefab, productPrefab),
                $"{CustomerVehiclePrefabPath} must be empty before runtime order loading.");
            Require(customerVehicleConfig.ViewPrefab == customerViews[0],
                $"{CustomerVehicleConfigPath} must reference the InteractionView root from " +
                $"{CustomerVehiclePrefabPath}.");
        }

        private static void ValidatePrototypeSceneComposition()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScenePath) != null,
                $"Prototype scene is missing at {PrototypeScenePath}.");

            Scene scene = SceneManager.GetSceneByPath(PrototypeScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Additive);

            try
            {
                SceneContext[] contexts = FindComponentsInScene<SceneContext>(scene);
                SceneInitializationInstaller[] installers =
                    FindComponentsInScene<SceneInitializationInstaller>(scene);
                PrototypeSceneInitializer[] initializers =
                    FindComponentsInScene<PrototypeSceneInitializer>(scene);
                CharacterControllerRegistrar[] characterRegistrars =
                    FindComponentsInScene<CharacterControllerRegistrar>(scene);
                CameraRegistrar[] cameraRegistrars = FindComponentsInScene<CameraRegistrar>(scene);
                SpawnPointMarker[] spawnPoints = FindComponentsInScene<SpawnPointMarker>(scene);
                SceneRouteMarker[] routes = FindComponentsInScene<SceneRouteMarker>(scene);
                SceneViewMarker[] sceneViews = FindComponentsInScene<SceneViewMarker>(scene);
                EntityBehaviour[] entityViews = FindComponentsInScene<EntityBehaviour>(scene);
                SlotsRegistrar[] slotRegistrars = FindComponentsInScene<SlotsRegistrar>(scene);
                PrototypeHudView[] hudViews = FindComponentsInScene<PrototypeHudView>(scene);
                PrototypeAudioView[] audioViews = FindComponentsInScene<PrototypeAudioView>(scene);

                Require(contexts.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one SceneContext, found {contexts.Length}.");
                Require(installers.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one SceneInitializationInstaller, " +
                    $"found {installers.Length}.");
                Require(initializers.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one PrototypeSceneInitializer, " +
                    $"found {initializers.Length}.");
                Require(contexts[0].Installers.Count() == 1,
                    $"{PrototypeScenePath} SceneContext must register exactly one MonoInstaller, " +
                    $"found {contexts[0].Installers.Count()}.");
                Require(contexts[0].Installers.Contains(installers[0]),
                    "SceneInitializationInstaller is present in Prototype_Yard but is not registered " +
                    "in SceneContext.Installers.");
                Require(installers[0].Initializers.Count == 1,
                    "SceneInitializationInstaller in Prototype_Yard must contain exactly one initializer, " +
                    $"found {installers[0].Initializers.Count}.");
                Require(installers[0].Initializers.Contains(initializers[0]),
                    "PrototypeSceneInitializer is present in Prototype_Yard but is not registered " +
                    "in SceneInitializationInstaller.Initializers.");
                SerializedObject serializedInitializer = new(initializers[0]);
                SpawnPointMarker[] configuredSpawnPoints = ReadObjectArray<SpawnPointMarker>(
                    serializedInitializer, "_spawnPoints", nameof(PrototypeSceneInitializer));
                SceneRouteMarker[] configuredRoutes = ReadObjectArray<SceneRouteMarker>(
                    serializedInitializer, "_routes", nameof(PrototypeSceneInitializer));
                SceneViewMarker[] configuredSceneViews = ReadObjectArray<SceneViewMarker>(
                    serializedInitializer, "_sceneViews", nameof(PrototypeSceneInitializer));
                Require(new HashSet<SpawnPointMarker>(configuredSpawnPoints).SetEquals(spawnPoints) &&
                        configuredSpawnPoints.Length == spawnPoints.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene spawn point set.");
                Require(new HashSet<SceneRouteMarker>(configuredRoutes).SetEquals(routes) &&
                        configuredRoutes.Length == routes.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene route set.");
                Require(new HashSet<SceneViewMarker>(configuredSceneViews).SetEquals(sceneViews) &&
                        configuredSceneViews.Length == sceneViews.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the static scene view set.");
                Require(hudViews.Length == 1 && audioViews.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one HUD and one audio view.");
                Require(serializedInitializer.FindProperty("_hudView")?.objectReferenceValue == hudViews[0] &&
                        serializedInitializer.FindProperty("_audioView")?.objectReferenceValue == audioViews[0],
                    $"{nameof(PrototypeSceneInitializer)} must reference the scene HUD and audio views.");
                Require(characterRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CharacterControllerRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                Require(cameraRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CameraRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                GameObject productPrefab = RequireAsset<GameObject>(ProductPrefabPath);
                Require(!ContainsPrefabInstance(scene, productPrefab),
                    $"{PrototypeScenePath} must not contain a product prefab instance; " +
                    "delivery cargo is spawned at runtime.");

                var expectedSpawnIds = new HashSet<SpawnPointId>
                {
                    SpawnPointId.Player,
                    SpawnPointId.DeliveryVehicle
                };
                var actualSpawnIds = new HashSet<SpawnPointId>(spawnPoints.Select(marker => marker.Id));
                Require(spawnPoints.Length == expectedSpawnIds.Count && actualSpawnIds.SetEquals(expectedSpawnIds),
                    $"{PrototypeScenePath} must contain one spawn point for Player and DeliveryVehicle.");

                var expectedRouteIds = new HashSet<SceneRouteId>
                {
                    SceneRouteId.CustomerVehicleArrival,
                    SceneRouteId.CustomerVehicleDeparture
                };
                var actualRouteIds = new HashSet<SceneRouteId>(routes.Select(marker => marker.Id));
                Require(routes.Length == expectedRouteIds.Count && actualRouteIds.SetEquals(expectedRouteIds),
                    $"{PrototypeScenePath} must contain exactly one marker for each customer vehicle route.");

                var routeWaypoints = new Dictionary<SceneRouteId, Transform[]>();
                foreach (SceneRouteMarker route in routes)
                {
                    Transform[] waypoints = ReadObjectArray<Transform>(
                        new SerializedObject(route), "_waypoints", route.name);
                    Require(waypoints.Length == 4,
                        $"Scene route {route.Id} must contain entry, gate, apron and terminal waypoints.");
                    Require(waypoints.All(waypoint => waypoint.gameObject.scene == scene &&
                                                       waypoint.IsChildOf(route.transform)),
                        $"Every waypoint of scene route {route.Id} must belong to its marker hierarchy " +
                        $"in {PrototypeScenePath}.");
                    routeWaypoints.Add(route.Id, waypoints);
                }

                Transform arrivalParking = routeWaypoints[SceneRouteId.CustomerVehicleArrival][^1];
                Transform departureParking = routeWaypoints[SceneRouteId.CustomerVehicleDeparture][0];
                Require(Vector3.Distance(arrivalParking.position, departureParking.position) < 0.001f &&
                        Quaternion.Angle(arrivalParking.rotation, departureParking.rotation) < 0.01f,
                    "Customer vehicle arrival must end at the exact pose where departure begins.");

                Transform[] allSceneTransforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                Require(allSceneTransforms.All(candidate => candidate.name != "Customer Truck"),
                    $"{PrototypeScenePath} must not contain the legacy static Customer Truck.");

                var expectedSceneViewIds = new HashSet<SceneViewId>
                {
                    SceneViewId.CustomerOrderCounter,
                    SceneViewId.ProcurementTerminal,
                    SceneViewId.StorageZone
                };
                var actualSceneViewIds = new HashSet<SceneViewId>(sceneViews.Select(marker => marker.Id));
                Require(sceneViews.Length == expectedSceneViewIds.Count &&
                        actualSceneViewIds.SetEquals(expectedSceneViewIds),
                    $"{PrototypeScenePath} must contain exactly one marker for every SceneViewId.");
                Require(sceneViews.All(marker => marker.View is InteractionView),
                    "Every static scene view marker must reference an InteractionView on the same object.");
                Require(entityViews.Length == sceneViews.Length &&
                        new HashSet<EntityBehaviour>(sceneViews.Select(marker => marker.View)).SetEquals(entityViews),
                    $"{PrototypeScenePath} must contain only the three marked static entity views.");
                Require(slotRegistrars.Length == 1,
                    $"{PrototypeScenePath} must contain scene slots only for storage.");

                SceneViewMarker storage = sceneViews.Single(marker => marker.Id == SceneViewId.StorageZone);
                SlotsRegistrar storageSlotsRegistrar = storage.GetComponent<SlotsRegistrar>();
                Require(storageSlotsRegistrar != null,
                    "The storage scene view must have a SlotsRegistrar.");
                Transform[] storageSlots = ReadSlots(storageSlotsRegistrar, PrototypeScenePath);
                Require(storageSlots.Length >= RequiredStorageSlotCapacity,
                    $"Storage must expose at least {RequiredStorageSlotCapacity} unique slots.");
                Collider[] storageInteractionTriggers = storage.View
                    .GetComponentsInChildren<Collider>(true)
                    .Where(collider => collider.isTrigger)
                    .ToArray();
                Require(storageInteractionTriggers.Length == 1,
                    "Storage scene view must expose exactly one interaction trigger.");
                Collider storageInteractionTrigger = storageInteractionTriggers[0];
                Require(storageInteractionTrigger.enabled &&
                        storageInteractionTrigger.gameObject.activeInHierarchy,
                    "Storage interaction trigger must be enabled and active.");
                foreach (Transform storageSlot in storageSlots)
                {
                    Vector3 slotPosition = storageSlot.position;
                    Vector3 closestPoint = storageInteractionTrigger.ClosestPoint(slotPosition);
                    Require(!storageInteractionTrigger.bounds.Contains(slotPosition) &&
                            (closestPoint - slotPosition).sqrMagnitude > Mathf.Epsilon,
                        $"Storage interaction trigger overlaps slot {storageSlot.name} at " +
                        $"{slotPosition}; the receiving target must be spatially separate from stored products.");
                }

                GameObject deliveryPrefab = RequireAsset<GameObject>(DeliveryVehiclePrefabPath);
                Require(!ContainsPrefabInstance(scene, deliveryPrefab),
                    $"{PrototypeScenePath} must not contain a supplier truck prefab instance.");
                GameObject customerVehiclePrefab = RequireAsset<GameObject>(CustomerVehiclePrefabPath);
                Require(!ContainsPrefabInstance(scene, customerVehiclePrefab),
                    $"{PrototypeScenePath} must not contain a customer vehicle prefab instance.");
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateBuildSettings()
        {
            EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .ToArray();

            Require(enabledScenes.Length > 0,
                "Build Settings must contain at least one enabled scene.");
            Require(enabledScenes[0].path == PrototypeScenePath,
                $"{PrototypeScenePath} must be the first enabled scene in Build Settings, " +
                $"but found {enabledScenes[0].path}.");
        }

        private static TComponent[] FindComponentsInScene<TComponent>(Scene scene)
            where TComponent : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TComponent>(true))
                .ToArray();

        private static TAsset RequireAsset<TAsset>(string path) where TAsset : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<TAsset>(path) ??
            throw new InvalidOperationException($"ECS architecture validation failed: asset is missing at {path}.");

        private static void ValidateExecuteOnlyViewBinder(Type binderType,
            params Type[] constructorParameters)
        {
            Require(typeof(IExecuteSystem).IsAssignableFrom(binderType) &&
                    !typeof(IInitializeSystem).IsAssignableFrom(binderType),
                $"{binderType.Name} must be an execute-only system for dynamically appearing entities.");
            Require(binderType.GetConstructor(constructorParameters) != null,
                $"{binderType.Name} must expose the expected constructor boundary: " +
                $"({JoinTypeNames(constructorParameters)}).");
        }

        private static void RequireMethod(Type owner, string methodName, Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo method = owner.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Require(method != null && method.ReturnType == returnType,
                $"{owner.FullName} must expose {returnType.Name} {methodName}" +
                $"({JoinTypeNames(parameterTypes)}).");
        }

        private static void RequireComponentIndexAttribute(Type componentType,
            string expectedAttributeFullName)
        {
            FieldInfo valueField = componentType.GetField(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);
            Require(valueField != null && valueField.GetCustomAttributes(inherit: false)
                    .Any(attribute => attribute.GetType().FullName == expectedAttributeFullName),
                $"{componentType.FullName}.Value must declare {expectedAttributeFullName}.");
        }

        private static void RequireGeneratedIndexApi(Type[] runtimeTypes, string methodName,
            Type returnType)
        {
            MethodInfo[] methods = runtimeTypes
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly))
                .Where(method => method.Name == methodName)
                .Where(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(GameContext) &&
                           parameters[1].ParameterType == typeof(int);
                })
                .ToArray();

            Require(methods.Length == 1,
                $"Jenny must generate exactly one {methodName}(GameContext, int) index API.");
            if (returnType != null)
            {
                Require(methods[0].ReturnType == returnType,
                    $"Generated {methodName} must return {returnType.FullName}.");
            }
            else
            {
                Require(typeof(IEnumerable<GameEntity>).IsAssignableFrom(methods[0].ReturnType),
                    $"Generated {methodName} must return a collection of GameEntity.");
            }
        }

        private static void ValidatePureConfigGetters(Type configType,
            IEnumerable<PropertyInfo> properties, string source)
        {
            Require(!source.Contains("_ =", StringComparison.Ordinal),
                $"{configType.Name} must not validate through discarded property reads.");

            foreach (PropertyInfo property in properties)
            {
                string pattern =
                    $@"\b{Regex.Escape(property.Name)}\s*=>\s*(?<expression>[^;]+);";
                MatchCollection matches = Regex.Matches(source, pattern);
                Require(matches.Count == 1,
                    $"{configType.Name}.{property.Name} must be one pure expression-bodied getter.");

                string expression = matches[0].Groups["expression"].Value;
                Require(!Regex.IsMatch(
                            expression,
                            @"\b(?:Validate|Require\w*)\s*\(|\bthrow\b|\bchecked\s*\("),
                    $"{configType.Name}.{property.Name} must not validate or throw while being read.");
                bool returnsField = Regex.IsMatch(
                    expression,
                    @"^\s*_[A-Za-z]\w*\s*$");
                bool derivesQuaternion = Regex.IsMatch(
                    expression,
                    @"^\s*Quaternion\.Euler\(\s*_[A-Za-z]\w*\s*\)\s*$");
                bool derivesUncheckedProduct = Regex.IsMatch(
                    expression,
                    @"^\s*unchecked\(\s*_[A-Za-z]\w*\s*\*\s*_[A-Za-z]\w*\s*\)\s*$");
                Require(returnsField || derivesQuaternion || derivesUncheckedProduct,
                    $"{configType.Name}.{property.Name} must return serialized data or a known " +
                    "pure derived value without hidden calls or mutation.");
            }
        }

        private static string[] GetRuntimeSourcePaths()
        {
            string runtimeRoot = GetRuntimeSourcePath();
            string generatedDirectory =
                Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar;
            return Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(generatedDirectory, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ReadRuntimeSource(params string[] relativePath)
        {
            string path = GetRuntimeSourcePath(relativePath);
            Require(File.Exists(path), $"Required runtime source is missing at {path}.");
            return File.ReadAllText(path);
        }

        private static string GetRuntimeSourcePath(params string[] relativePath)
        {
            string[] pathParts = new[] { Application.dataPath, "_Project", "Code" }
                .Concat(relativePath)
                .ToArray();
            return Path.GetFullPath(Path.Combine(pathParts));
        }

        private static bool PathsEqual(string left, string right) =>
            string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static void RequireSourceContains(string source, params string[] fragments)
        {
            foreach (string fragment in fragments)
            {
                Require(source.Contains(fragment, StringComparison.Ordinal),
                    $"Required architecture source fragment is missing: {fragment}.");
            }
        }

        private static void ValidatePrefabRoot(GameObject prefab, string path, bool requireUnitScale)
        {
            Require(prefab.activeSelf, $"Prefab root at {path} must be active.");
            Require(prefab.transform.localPosition == Vector3.zero,
                $"Prefab root at {path} must have zero local position.");
            Require(prefab.transform.localRotation == Quaternion.identity,
                $"Prefab root at {path} must have identity local rotation.");
            if (requireUnitScale)
            {
                Require(prefab.transform.localScale == Vector3.one,
                    $"Prefab root at {path} must have unit local scale.");
            }
            else
            {
                Vector3 scale = prefab.transform.localScale;
                Require(scale.x > 0f && scale.y > 0f && scale.z > 0f,
                    $"Prefab root at {path} must have a positive local scale.");
            }
        }

        private static Transform[] ReadSlots(SlotsRegistrar registrar, string owner)
        {
            SerializedProperty slotsProperty = new SerializedObject(registrar).FindProperty("_slots");
            Require(slotsProperty != null && slotsProperty.isArray,
                $"SlotsRegistrar in {owner} must serialize a slot array.");

            Transform[] slots = new Transform[slotsProperty.arraySize];
            var uniqueSlots = new HashSet<Transform>();
            for (int index = 0; index < slots.Length; index++)
            {
                Transform slot = slotsProperty.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                Require(slot != null && uniqueSlots.Add(slot),
                    $"SlotsRegistrar in {owner} contains a missing or duplicate slot at index {index}.");
                slots[index] = slot;
            }

            return slots;
        }

        private static TObject[] ReadObjectArray<TObject>(SerializedObject owner, string propertyName,
            string ownerName) where TObject : UnityEngine.Object
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            Require(property != null && property.isArray,
                $"{ownerName} must serialize {propertyName} as an array.");

            TObject[] values = new TObject[property.arraySize];
            var uniqueValues = new HashSet<TObject>();
            for (int index = 0; index < values.Length; index++)
            {
                TObject value = property.GetArrayElementAtIndex(index).objectReferenceValue as TObject;
                Require(value != null && uniqueValues.Add(value),
                    $"{ownerName}.{propertyName} contains a missing or duplicate reference at index {index}.");
                values[index] = value;
            }

            return values;
        }

        private static bool ContainsPrefabInstance(Scene scene, GameObject prefab) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(candidate => PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject) == prefab);

        private static bool ContainsPrefabInstance(GameObject root, GameObject prefab) =>
            root.GetComponentsInChildren<Transform>(true)
                .Any(candidate => PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject) == prefab);

        private static TComponent[] RequireExactlyOneInPrefab<TComponent>(GameObject prefab, string prefabPath)
            where TComponent : Component
        {
            TComponent[] components = prefab.GetComponentsInChildren<TComponent>(true);
            Require(components.Length == 1,
                $"{prefabPath} must contain exactly one {typeof(TComponent).Name}, " +
                $"found {components.Length}.");
            return components;
        }

        private static string DescribeSetMismatch(string owner, HashSet<Type> expected, HashSet<Type> actual)
        {
            string missing = JoinTypeNames(expected.Except(actual));
            string unexpected = JoinTypeNames(actual.Except(expected));
            return $"{owner} does not match component declarations. Missing: [{missing}]. " +
                   $"Unexpected: [{unexpected}].";
        }

        private static string JoinTypeNames(IEnumerable<Type> types) =>
            string.Join(", ", types.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"ECS architecture validation failed: {message}");
        }

        public readonly struct ValidationResult
        {
            public int GameComponentCount { get; }
            public int InputComponentCount { get; }

            public ValidationResult(int gameComponentCount, int inputComponentCount)
            {
                GameComponentCount = gameComponentCount;
                InputComponentCount = inputComponentCount;
            }
        }
    }
}
