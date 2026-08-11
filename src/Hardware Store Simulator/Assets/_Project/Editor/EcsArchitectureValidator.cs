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
using HardwareStore.Gameplay.Features.Customers.Systems;
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
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Zenject;

namespace HardwareStore.Editor
{
    public static class EcsArchitectureValidator
    {
        private const string MenuPath = "Tools/Hardware Store/Validate ECS Architecture";
        private const string ProjectContextPath = "Assets/Resources/ProjectContext.prefab";
        private const string PrototypeScenePath = "Assets/Scenes/Prototype_Yard.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerConfigPath = "Assets/Resources/Configs/PlayerConfig.asset";
        private const string InteractionConfigPath = "Assets/Resources/Configs/InteractionConfig.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string ConfigFolder = "Assets/Resources/Configs";
        private const string CementProductConfigPath = "Assets/Resources/Configs/ProductConfig.asset";
        private const string BoardProductConfigPath =
            "Assets/Resources/Configs/ProductConfig_BoardBundle.asset";
        private const string CementDeliveryConfigPath = "Assets/Resources/Configs/DeliveryConfig.asset";
        private const string BoardDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_BoardBundle.asset";
        private const string CustomerVehicleConfigPath =
            "Assets/Resources/Configs/CustomerVehicleConfig.asset";
        private const string CustomerConfigPath =
            "Assets/Resources/Configs/CustomerConfig.asset";
        private const string EconomyConfigPath = "Assets/Resources/Configs/EconomyConfig.asset";
        private const string CementOrderConfigPath = "Assets/Resources/Configs/OrderConfig.asset";
        private const string BoardOrderConfigPath =
            "Assets/Resources/Configs/OrderConfig_BoardBundle.asset";
        private const string CementProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/CementBag.prefab";
        private const string BoardProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/BoardBundle.prefab";
        private const string DeliveryVehiclePrefabPath = "Assets/_Project/Prefabs/Gameplay/DeliveryTruck.prefab";
        private const string CustomerVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicle.prefab";
        private const string CustomerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Customer.prefab";
        private const int RequiredStorageSlotCapacity = 9;

        private static readonly ProductTypeId[] ExpectedProductTypes =
        {
            ProductTypeId.CementBag,
            ProductTypeId.BoardBundle
        };

        private static readonly Type[] ExpectedInputComponents =
        {
            typeof(InputState),
            typeof(MoveInput),
            typeof(LookInput),
            typeof(SprintHeld),
            typeof(InteractPressed),
            typeof(ConfirmPressed),
            typeof(DropPressed),
            typeof(PreviousPressed),
            typeof(NextPressed),
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
            "CustomerView",
            "CustomerTransformRegistrar",
            "CustomerRigidbodyRegistrar",
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

        private static readonly Type[] ExpectedGameplayConfigTypes =
        {
            typeof(PlayerConfig),
            typeof(InteractionConfig),
            typeof(EconomyConfig),
            typeof(DeliveryConfig),
            typeof(CustomerVehicleConfig),
            typeof(CustomerConfig),
            typeof(OrderConfig),
            typeof(ProductConfig)
        };

        private static readonly (Type Type, string AssetPath)[] ExpectedSingletonGameplayConfigs =
        {
            (typeof(PlayerConfig), PlayerConfigPath),
            (typeof(InteractionConfig), InteractionConfigPath),
            (typeof(EconomyConfig), EconomyConfigPath),
            (typeof(CustomerVehicleConfig), CustomerVehicleConfigPath),
            (typeof(CustomerConfig), CustomerConfigPath)
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
            ValidateInputContract();
            ValidateLegacyTypesAreAbsent(runtimeTypes);
            ValidateStoreArchitecture(componentTypes);
            ValidateEntityViewBindingBoundary(runtimeTypes, componentTypes);
            ValidateEntityIndices(runtimeTypes, componentTypes);
            ValidateConsultationArchitecture(runtimeTypes, componentTypes);
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

        private static void ValidateInputContract()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            Require(inputActions != null,
                $"{InputActionsPath} must contain the gameplay Input Actions asset.");

            InputActionMap playerMap = inputActions.FindActionMap("Player");
            Require(playerMap != null,
                $"{InputActionsPath} must contain the Player action map.");

            InputAction move = playerMap.FindAction("Move");
            InputAction previous = playerMap.FindAction("Previous");
            InputAction next = playerMap.FindAction("Next");
            InputAction confirm = playerMap.FindAction("Confirm");
            Require(move != null && previous != null && next != null && confirm != null,
                "Player input must expose Move, Previous, Next and Confirm actions.");

            Require(HasBinding(previous, "<Keyboard>/leftArrow") &&
                    HasBinding(next, "<Keyboard>/rightArrow"),
                "Consultation navigation must use the keyboard left and right arrows.");
            Require(!HasAnyBinding(previous,
                        "<Keyboard>/1",
                        "<Keyboard>/2",
                        "<Keyboard>/digit1",
                        "<Keyboard>/digit2") &&
                    !HasAnyBinding(next,
                        "<Keyboard>/1",
                        "<Keyboard>/2",
                        "<Keyboard>/digit1",
                        "<Keyboard>/digit2"),
                "Consultation navigation must not retain the legacy 1/2 keyboard bindings.");
            Require(!HasAnyBinding(move,
                    "<Keyboard>/leftArrow",
                    "<Keyboard>/rightArrow",
                    "<Keyboard>/upArrow",
                    "<Keyboard>/downArrow"),
                "Move must not consume keyboard arrows reserved for modal navigation.");
            Require(HasBinding(confirm, "<Keyboard>/enter") &&
                    HasBinding(confirm, "<Keyboard>/numpadEnter"),
                "Consultation confirmation must accept Enter and Numpad Enter through Confirm.");
            Require(HasOnlyBindings(confirm,
                    "<Keyboard>/enter",
                    "<Keyboard>/numpadEnter",
                    "<Gamepad>/buttonSouth"),
                "Confirm may only use Enter, Numpad Enter and gamepad button South; " +
                "mouse, touch, joystick and XR bindings are forbidden.");
        }

        private static bool HasBinding(InputAction action, string path) =>
            action.bindings.Any(binding =>
                string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase));

        private static bool HasAnyBinding(InputAction action, params string[] paths) =>
            paths.Any(path => HasBinding(action, path));

        private static bool HasOnlyBindings(InputAction action, params string[] paths) =>
            action.bindings.Any(binding =>
                paths.Any(path => string.Equals(
                    binding.path,
                    path,
                    StringComparison.OrdinalIgnoreCase))) &&
            action.bindings.All(binding =>
                paths.Any(path => string.Equals(
                    binding.path,
                    path,
                    StringComparison.OrdinalIgnoreCase)));

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
                typeof(CustomerVisitConsulting),
                typeof(CustomerVisitWaiting),
                typeof(CustomerVisitLoading),
                typeof(CustomerVisitCompleted),
                typeof(CustomerVisitReturning),
                typeof(CustomerVisitDeparting)
            };
            foreach (Type role in customerVisitRoles)
            {
                Require(discoveredComponents.Contains(role),
                    $"Unified customer visits require the {role.Name} Game component.");
            }

            Require(discoveredComponents.Contains(typeof(CustomerVisitEntityId)),
                $"{nameof(CustomerVisitEntityId)} must relate loaded products to their visit.");
            Require(discoveredComponents.Contains(typeof(Customer)),
                $"{nameof(Customer)} must identify the runtime customer actor.");
            Require(discoveredComponents.Contains(typeof(RouteMover)),
                $"{nameof(RouteMover)} must opt runtime actors into generic route movement.");
            Require(discoveredComponents.Contains(typeof(CustomerActorVisitEntityId)),
                $"{nameof(CustomerActorVisitEntityId)} must uniquely relate the customer actor " +
                "to its visit.");
            Type[] customerActorComponents =
            {
                typeof(CustomerApproachingCounter),
                typeof(CustomerWaitingAtCounter),
                typeof(CustomerReturningToVehicle),
                typeof(CustomerReturnRoute)
            };
            foreach (Type actorComponent in customerActorComponents)
            {
                Require(discoveredComponents.Contains(actorComponent),
                    $"Runtime customers require the {actorComponent.Name} Game component.");
            }
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
            RequireComponentIndexAttribute(
                typeof(CustomerActorVisitEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");

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
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithCustomerActorVisitEntityId",
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
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithCustomerActorVisitEntityId\s*\("),
                $"Runtime customer lifecycle must consume the " +
                $"{nameof(CustomerActorVisitEntityId)} primary index.");

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
                typeof(GameEntity));
            Require(typeof(IConsultationOfferFactory).IsAssignableFrom(
                    typeof(ConsultationOfferFactory)),
                $"{nameof(ConsultationOfferFactory)} must implement " +
                $"{nameof(IConsultationOfferFactory)}.");
            RequireMethod(
                typeof(IConsultationOfferFactory),
                nameof(IConsultationOfferFactory.CreateOffers),
                typeof(void),
                typeof(GameEntity));
            Require(typeof(ICustomerFactory).IsAssignableFrom(typeof(CustomerFactory)),
                $"{nameof(CustomerFactory)} must implement {nameof(ICustomerFactory)}.");
            RequireMethod(
                typeof(ICustomerFactory),
                nameof(ICustomerFactory.Create),
                typeof(GameEntity),
                typeof(GameEntity),
                typeof(Pose[]),
                typeof(Pose[]));
            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<ICustomerFactory>().To<CustomerFactory>().AsSingle()");

            string customerVisitFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "CustomerVisitFactory.cs");
            RequireSourceContains(customerVisitFactorySource,
                "isCustomerVisit = true",
                "isCustomerVehicle = true",
                "isCustomerVisitArriving = true",
                "isRouteMover = true",
                "isLoadingZone = true",
                "AddCustomerVisitStoreEntityId",
                "AddRequestedProductType",
                "AddCustomerProjectTitle",
                "AddCustomerRequest",
                "_consultationOffers.CreateOffers");
            Require(!customerVisitFactorySource.Contains(
                    "_orderFactory",
                    StringComparison.Ordinal),
                $"{nameof(CustomerVisitFactory)} must not create the order before consultation.");
            string orderFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "OrderFactory.cs");
            RequireSourceContains(orderFactorySource,
                "AddOrderComponents",
                "selectedOffer.RequiredProductType",
                "selectedOffer.RequiredProductCount",
                "selectedOffer.OrderReward",
                "isOrder = true");
            Require(!orderFactorySource.Contains("CreateEntity.", StringComparison.Ordinal),
                $"{nameof(OrderFactory)} must enrich the unified CustomerVisit entity, not create another one.");

            string consultationOfferFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "ConsultationOfferFactory.cs");
            RequireSourceContains(consultationOfferFactorySource,
                "CreateEntity.Empty",
                "AddCustomerVisitEntityId",
                "AddOfferIndex",
                "AddOfferTitle",
                "AddOfferDescription",
                "AddExpectedProfit",
                "isConsultationOffer = true",
                "isSelectedConsultationOffer");

            string customerFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "CustomerFactory.cs");
            RequireSourceContains(customerFactorySource,
                "CreateEntity.Empty",
                "AddCustomerActorVisitEntityId",
                "AddCustomerReturnRoute",
                "isCustomer = true",
                "isCustomerApproachingCounter = true",
                "isRouteMover = true");

            string routeMovementSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems", "MoveRouteSystem.cs");
            Require(runtimeTypes.Contains(typeof(MoveRouteSystem)),
                $"{nameof(MoveRouteSystem)} must remain part of Assembly-CSharp.");
            RequireSourceContains(routeMovementSource,
                "GameMatcher.RouteMover",
                "GameMatcher.Route",
                "GameMatcher.RouteWaypointIndex",
                "GameMatcher.MovementSpeed",
                "GameMatcher.RotationSpeed",
                "GameMatcher.WaypointTolerance",
                "GameMatcher.Transform",
                "GameMatcher.Rigidbody",
                "GameMatcher.RouteCompleted",
                "GameMatcher.Destructed");
            Require(!routeMovementSource.Contains("GameMatcher.Customer", StringComparison.Ordinal) &&
                    !routeMovementSource.Contains("GameMatcher.CustomerVehicle", StringComparison.Ordinal),
                $"{nameof(MoveRouteSystem)} must move any RouteMover instead of depending on a " +
                "customer role.");

            string completeCustomerReturnSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerReturnSystem.cs");
            int removeActorRelation = completeCustomerReturnSource.IndexOf(
                "RemoveCustomerActorVisitEntityId",
                StringComparison.Ordinal);
            int destructCustomerActor = completeCustomerReturnSource.IndexOf(
                "isDestructed = true",
                StringComparison.Ordinal);
            Require(removeActorRelation >= 0 && destructCustomerActor > removeActorRelation,
                $"{nameof(CustomerActorVisitEntityId)} must be removed before the customer actor " +
                "enters the Destructed pipeline.");

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
                new HashSet<Type>(ExpectedGameplayConfigTypes);
            var discoveredConfigTypes = new HashSet<Type>(runtimeTypes
                .Where(type => type.Namespace == typeof(PlayerConfig).Namespace)
                .Where(type => !type.IsAbstract && typeof(ScriptableObject).IsAssignableFrom(type)));
            Require(discoveredConfigTypes.SetEquals(expectedConfigTypes),
                DescribeSetMismatch(
                    "Gameplay config types",
                    expectedConfigTypes,
                    discoveredConfigTypes));

            Type staticDataType = typeof(IStaticDataService);
            var staticDataConfigTypes = new HashSet<Type>(staticDataType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.PropertyType)
                .Concat(staticDataType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => method.ReturnType))
                .Where(expectedConfigTypes.Contains));
            Require(staticDataConfigTypes.SetEquals(expectedConfigTypes),
                DescribeSetMismatch(
                    nameof(IStaticDataService),
                    expectedConfigTypes,
                    staticDataConfigTypes));

            PropertyInfo productTypesProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.ProductTypes),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(productTypesProperty?.PropertyType == typeof(IReadOnlyList<ProductTypeId>),
                $"{nameof(IStaticDataService)} must expose a read-only ProductTypeId catalog key list.");
            RequireMethod(staticDataType, nameof(IStaticDataService.GetProduct), typeof(ProductConfig),
                typeof(ProductTypeId));
            RequireMethod(staticDataType, nameof(IStaticDataService.GetDelivery), typeof(DeliveryConfig),
                typeof(ProductTypeId));
            RequireMethod(staticDataType, nameof(IStaticDataService.GetOrder), typeof(OrderConfig),
                typeof(ProductTypeId));
            PropertyInfo customerProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.Customer),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(customerProperty?.PropertyType == typeof(CustomerConfig),
                $"{nameof(IStaticDataService)} must expose the validated {nameof(CustomerConfig)}.");

            foreach (Type configType in ExpectedGameplayConfigTypes)
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
            }

            foreach ((Type configType, string assetPath) in ExpectedSingletonGameplayConfigs)
            {
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

            ValidateConfigCatalogAssets<ProductConfig>(expectedCount: 2);
            ValidateConfigCatalogAssets<DeliveryConfig>(expectedCount: 2);
            ValidateConfigCatalogAssets<OrderConfig>(expectedCount: 2);

            Require(typeof(OrderOfferDefinition).IsSealed &&
                    typeof(OrderOfferDefinition).IsSerializable,
                $"{nameof(OrderOfferDefinition)} must be a serializable sealed value definition.");
            Require(typeof(OrderOfferDefinition).GetConstructor(new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(int)
                    }) != null,
                $"{nameof(OrderOfferDefinition)} must expose its complete four-value constructor.");
            PropertyInfo offersProperty = typeof(OrderConfig).GetProperty(
                nameof(OrderConfig.Offers),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(offersProperty?.PropertyType ==
                    typeof(IReadOnlyList<OrderOfferDefinition>),
                $"{nameof(OrderConfig)}.{nameof(OrderConfig.Offers)} must expose a read-only list.");
            RequireMethod(
                typeof(OrderConfig),
                nameof(OrderConfig.Configure),
                typeof(void),
                typeof(ProductTypeId),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(OrderOfferDefinition[]));

            string orderConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(OrderConfig) + ".cs");
            RequireSourceContains(orderConfigSource,
                "(OrderOfferDefinition[])offers.Clone()",
                "Validate();",
                "strictly increasing product counts");

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

            RequireMethod(
                typeof(CustomerConfig),
                nameof(CustomerConfig.Configure),
                typeof(void),
                typeof(EntityBehaviour),
                typeof(float),
                typeof(float),
                typeof(float));
            string customerConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(CustomerConfig) + ".cs");
            int lastCustomerConfigurationAssignment = customerConfigSource.IndexOf(
                "_waypointTolerance = waypointTolerance",
                StringComparison.Ordinal);
            int explicitCustomerConfigurationValidation = customerConfigSource.IndexOf(
                "Validate();",
                lastCustomerConfigurationAssignment >= 0
                    ? lastCustomerConfigurationAssignment
                    : 0,
                StringComparison.Ordinal);
            Require(lastCustomerConfigurationAssignment >= 0 &&
                    explicitCustomerConfigurationValidation > lastCustomerConfigurationAssignment,
                $"{nameof(CustomerConfig)}.Configure must assign all values and then call Validate().");

            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", "StaticDataService.cs");
            Require(!staticDataSource.Contains("_ =", StringComparison.Ordinal),
                $"{nameof(StaticDataService)} must not validate configs through discarded getter reads.");
            Require(!staticDataSource.Contains("public ProductConfig Product", StringComparison.Ordinal) &&
                    !staticDataSource.Contains("public DeliveryConfig Delivery", StringComparison.Ordinal) &&
                    !staticDataSource.Contains("public OrderConfig Order", StringComparison.Ordinal),
                $"{nameof(StaticDataService)} must not retain the legacy single-SKU config properties.");
            RequireSourceContains(staticDataSource,
                "GetProduct(ProductTypeId productType)",
                "GetDelivery(ProductTypeId productType)",
                "GetOrder(ProductTypeId productType)",
                "ProductTypes",
                "CustomerConfig Customer");
        }

        private static void ValidateConsultationArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] consultationComponents =
            {
                typeof(CustomerVisitConsulting),
                typeof(RequestedProductType),
                typeof(CustomerProjectTitle),
                typeof(CustomerRequest),
                typeof(ConsultationVisitEntityId),
                typeof(ConsultationOffer),
                typeof(OfferIndex),
                typeof(OfferTitle),
                typeof(OfferDescription),
                typeof(ExpectedProfit),
                typeof(SelectedConsultationOffer)
            };
            foreach (Type component in consultationComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Consultation requires the {component.Name} Game component.");
            }

            string[] executeSystemNames =
            {
                "CycleConsultationOfferSystem",
                "ConfirmConsultationOfferSystem",
                "CancelConsultationSystem",
                "OpenConsultationSystem"
            };
            foreach (string systemName in executeSystemNames)
            {
                Type systemType = runtimeTypes.SingleOrDefault(type => type.Name == systemName);
                Require(systemType != null && typeof(IExecuteSystem).IsAssignableFrom(systemType),
                    $"{systemName} must be an executable Entitas system.");
            }

            string consultationFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "ConsultationFeature.cs");
            int previousSystemPosition = -1;
            foreach (string systemName in executeSystemNames)
            {
                int systemPosition = consultationFeatureSource.IndexOf(
                    systemName,
                    StringComparison.Ordinal);
                Require(systemPosition > previousSystemPosition,
                    $"ConsultationFeature must execute {string.Join(" -> ", executeSystemNames)}.");
                previousSystemPosition = systemPosition;
            }

            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            RequireSourceContains(storeFeatureSource, "ConsultationFeature");

            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IConsultationOfferFactory>().To<ConsultationOfferFactory>().AsSingle()");

            string arrivalSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerVehicleArrivalSystem.cs");
            RequireSourceContains(arrivalSource,
                "GameMatcher.RequestedProductType",
                "GameMatcher.CustomerProjectTitle",
                "GameMatcher.CustomerRequest",
                "SceneRouteId.CustomerWalkToCounter",
                "SceneRouteId.CustomerWalkToVehicle",
                "_customerFactory.Create");
            Require(!arrivalSource.Contains("GameMatcher.Order", StringComparison.Ordinal) &&
                    !arrivalSource.Contains("GameMatcher.RequiredProductCount", StringComparison.Ordinal) &&
                    !arrivalSource.Contains("isCustomerVisitConsulting = true", StringComparison.Ordinal),
                "A parked customer must enter consultation before order components exist.");

            string approachSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerApproachSystem.cs");
            RequireSourceContains(approachSource,
                "GameMatcher.CustomerApproachingCounter",
                "GameMatcher.CustomerActorVisitEntityId",
                "GameMatcher.RouteCompleted",
                "isCustomerApproachingCounter = false",
                "isCustomerWaitingAtCounter = true",
                "isCustomerVisitArriving = false",
                "isCustomerVisitConsulting = true");

            string openSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "OpenConsultationSystem.cs");
            RequireSourceContains(openSource,
                "GameMatcher.InteractionRequest",
                "isCustomerVisitConsulting",
                "AddConsultationVisitEntityId",
                "Vector3.zero");
            Require(openSource.Contains("isHandsOccupied", StringComparison.Ordinal),
                "Consultation must not open while the player carries a product.");

            string cycleSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "CycleConsultationOfferSystem.cs");
            RequireSourceContains(cycleSource,
                "InputMatcher.PreviousPressed",
                "InputMatcher.NextPressed",
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithCustomerVisitEntityId",
                "isSelectedConsultationOffer");

            string confirmSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "ConfirmConsultationOfferSystem.cs");
            RequireSourceContains(confirmSource,
                "InputMatcher.ConfirmPressed",
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithCustomerVisitEntityId",
                "_orderFactory.AddOrderComponents",
                "RemoveRequestedProductType",
                "RemoveConsultationVisitEntityId",
                "isCustomerVisitConsulting = false",
                "isCustomerVisitWaiting = true",
                "isDestructed = true");

            string cancelSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "CancelConsultationSystem.cs");
            RequireSourceContains(cancelSource,
                "InputMatcher.ToggleCursorPressed",
                "GameMatcher.ConsultationVisitEntityId",
                "RemoveConsultationVisitEntityId");
            Require(!cancelSource.Contains("isDestructed = true", StringComparison.Ordinal),
                "Cancelling consultation must preserve its offer entities.");

            string emitInteractionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "EmitInteractionRequestSystem.cs");
            string moveSource = ReadRuntimeSource(
                "Gameplay", "Features", "Movement", "Systems",
                "SetMoveDirectionFromInputSystem.cs");
            string lookSource = ReadRuntimeSource(
                "Gameplay", "Features", "Player", "Systems",
                "ApplyLookInputSystem.cs");
            string toggleCursorSource = ReadRuntimeSource(
                "Gameplay", "Features", "Player", "Systems",
                "ToggleCursorSystem.cs");
            string dropSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "DropHeldProductSystem.cs");
            string focusSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "DetectFocusedInteractableSystem.cs");
            string highlightSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "UpdateFocusHighlightSystem.cs");
            string procurementSelectionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ChangeProcurementSelectionSystem.cs");
            foreach ((string source, string owner) in new[]
                     {
                         (emitInteractionSource, "interaction requests"),
                         (lookSource, "look input"),
                         (toggleCursorSource, "cursor toggling"),
                         (dropSource, "dropping products"),
                         (focusSource, "world focus detection"),
                         (highlightSource, "world focus highlights"),
                         (procurementSelectionSource, "procurement selection")
                     })
            {
                Require(source.Contains("GameMatcher.ConsultationVisitEntityId", StringComparison.Ordinal),
                    $"Modal consultation must capture {owner}.");
            }
            RequireSourceContains(moveSource,
                "hasConsultationVisitEntityId",
                "Vector2.zero");

            string purchaseSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "PurchaseDeliverySystem.cs");
            Require(purchaseSource.Contains("isCustomerVisitConsulting", StringComparison.Ordinal),
                "Procurement must reject a delivery before consultation forms an order.");

            string[] promptSystemFiles =
            {
                "ResolveProcurementTerminalPromptSystem.cs",
                "ResolveEmptyHandsStoragePromptSystem.cs",
                "ResolveHeldProductStoragePromptSystem.cs",
                "ResolveOrderCounterPromptSystem.cs",
                "ResolveProductPromptSystem.cs",
                "ResolveLoadingZonePromptSystem.cs"
            };
            foreach (string promptSystemFile in promptSystemFiles)
            {
                string promptSource = ReadRuntimeSource(
                    "Gameplay", "Features", "Interaction", "Systems", promptSystemFile);
                Require(promptSource.Contains("isCustomerVisitConsulting", StringComparison.Ordinal),
                    $"{promptSystemFile} must present the pre-order consultation state before " +
                    "reading order components.");
            }

            RequireMethod(
                typeof(IHudService),
                nameof(IHudService.PresentConsultation),
                typeof(void),
                typeof(ConsultationSnapshot?));
            ConstructorInfo consultationSnapshotConstructor = typeof(ConsultationSnapshot)
                .GetConstructor(new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(ConsultationOfferSnapshot[])
                });
            Require(consultationSnapshotConstructor != null,
                $"{nameof(ConsultationSnapshot)} must expose project, request and three offer cards.");
            string presentConsultationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentConsultationSystem.cs");
            RequireSourceContains(presentConsultationSource,
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithCustomerVisitEntityId",
                "OrderBy(offer => offer.OfferIndex)",
                "PresentConsultation");
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
            ProductTypeId[] enumValues = Enum.GetValues(typeof(ProductTypeId))
                .Cast<ProductTypeId>()
                .ToArray();
            Require(enumValues.SequenceEqual(ExpectedProductTypes),
                $"{nameof(ProductTypeId)} must append exactly CementBag = 0 and BoardBundle = 1.");

            ProductConfig cementProductConfig =
                RequireAsset<ProductConfig>(CementProductConfigPath);
            ProductConfig boardProductConfig =
                RequireAsset<ProductConfig>(BoardProductConfigPath);
            DeliveryConfig cementDeliveryConfig =
                RequireAsset<DeliveryConfig>(CementDeliveryConfigPath);
            DeliveryConfig boardDeliveryConfig =
                RequireAsset<DeliveryConfig>(BoardDeliveryConfigPath);
            CustomerVehicleConfig customerVehicleConfig =
                RequireAsset<CustomerVehicleConfig>(CustomerVehicleConfigPath);
            CustomerConfig customerConfig = RequireAsset<CustomerConfig>(CustomerConfigPath);
            EconomyConfig economyConfig = RequireAsset<EconomyConfig>(EconomyConfigPath);
            OrderConfig cementOrderConfig = RequireAsset<OrderConfig>(CementOrderConfigPath);
            OrderConfig boardOrderConfig = RequireAsset<OrderConfig>(BoardOrderConfigPath);

            ProductConfig[] productConfigs = LoadConfigCatalogAssets<ProductConfig>();
            DeliveryConfig[] deliveryConfigs = LoadConfigCatalogAssets<DeliveryConfig>();
            OrderConfig[] orderConfigs = LoadConfigCatalogAssets<OrderConfig>();
            Require(productConfigs.Length == ExpectedProductTypes.Length &&
                    deliveryConfigs.Length == ExpectedProductTypes.Length &&
                    orderConfigs.Length == ExpectedProductTypes.Length,
                "Product, delivery and order catalogs must each contain exactly two assets.");
            var expectedKeys = new HashSet<ProductTypeId>(ExpectedProductTypes);
            var productKeys = new HashSet<ProductTypeId>(productConfigs.Select(config => config.ProductType));
            var deliveryKeys = new HashSet<ProductTypeId>(deliveryConfigs.Select(config => config.ProductType));
            var orderKeys = new HashSet<ProductTypeId>(orderConfigs.Select(config => config.RequiredProductType));
            Require(productKeys.SetEquals(expectedKeys) && productKeys.Count == productConfigs.Length,
                "ProductConfig assets must provide every ProductTypeId exactly once.");
            Require(deliveryKeys.SetEquals(expectedKeys) && deliveryKeys.Count == deliveryConfigs.Length,
                "DeliveryConfig assets must have one-to-one key parity with the product catalog.");
            Require(orderKeys.SetEquals(expectedKeys) && orderKeys.Count == orderConfigs.Length,
                "OrderConfig assets must have one-to-one key parity with the product catalog.");

            ValidateCatalogEntry(
                cementProductConfig,
                cementDeliveryConfig,
                cementOrderConfig,
                ProductTypeId.CementBag,
                displayName: "Цемент 25 кг",
                unitLabel: "шт.",
                unitPrice: 350,
                mass: 25f,
                carryMovementSpeed: 3.2f,
                deliveryCount: 3,
                purchaseUnitPrice: 200,
                customerProjectTitle: "Стяжка в мастерской",
                customerRequest:
                    "Нужно подготовить материал для небольшой стяжки. " +
                    "Предложите подходящий запас.");
            ValidateCatalogEntry(
                boardProductConfig,
                boardDeliveryConfig,
                boardOrderConfig,
                ProductTypeId.BoardBundle,
                displayName: "Пачка досок",
                unitLabel: "шт.",
                unitPrice: 480,
                mass: 18f,
                carryMovementSpeed: 2.6f,
                deliveryCount: 3,
                purchaseUnitPrice: 260,
                customerProjectTitle: "Полки для мастерской",
                customerRequest:
                    "Нужно собрать рабочие полки. Предложите объём с подходящим запасом.");

            Require(economyConfig.InitialMoney == 1100,
                $"{EconomyConfigPath} must start the prototype with 1100.");
            Require(deliveryConfigs.All(config => economyConfig.InitialMoney >= config.TotalCost),
                "Initial money must cover either configured inbound delivery.");
            Require(!Mathf.Approximately(cementProductConfig.Mass, boardProductConfig.Mass) &&
                    !Mathf.Approximately(
                        cementProductConfig.CarryMovementSpeed,
                        boardProductConfig.CarryMovementSpeed),
                "Cement and board bundles must have distinct mass and carry movement speed.");
            Require(Quaternion.Angle(
                        cementProductConfig.HeldRotationOffset,
                        Quaternion.Euler(8f, 0f, 0f)) < 0.01f &&
                    Mathf.Approximately(cementProductConfig.DropForwardDistance, 1.15f),
                $"{CementProductConfigPath} must preserve cement handling offsets.");
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
            Require(Mathf.Approximately(customerConfig.MovementSpeed, 2.4f),
                $"{CustomerConfigPath} must use a customer movement speed of 2.4.");
            Require(Mathf.Approximately(customerConfig.RotationSpeed, 360f),
                $"{CustomerConfigPath} must use a customer rotation speed of 360 degrees per second.");
            Require(Mathf.Approximately(customerConfig.WaypointTolerance, 0.08f),
                $"{CustomerConfigPath} must use a waypoint tolerance of 0.08.");

            GameObject cementProductPrefab = RequireAsset<GameObject>(CementProductPrefabPath);
            GameObject boardProductPrefab = RequireAsset<GameObject>(BoardProductPrefabPath);
            Vector3 cementGeometry = ValidateProductPrefab(
                cementProductConfig,
                CementProductConfigPath,
                cementProductPrefab,
                CementProductPrefabPath,
                requireUnitScale: false);
            Vector3 boardGeometry = ValidateProductPrefab(
                boardProductConfig,
                BoardProductConfigPath,
                boardProductPrefab,
                BoardProductPrefabPath,
                requireUnitScale: true);
            float boardLength = Mathf.Max(boardGeometry.x, boardGeometry.z);
            float cementLength = Mathf.Max(cementGeometry.x, cementGeometry.z);
            Require(boardLength >= 1.4f && boardLength <= 1.6f,
                $"{BoardProductPrefabPath} must be approximately 1.4-1.6 metres long.");
            Require(boardLength > cementLength + 0.5f,
                "Board bundle and cement bag must have materially distinct geometry.");
            Require(boardProductPrefab.GetComponentsInChildren<Renderer>(true).Length >= 6,
                $"{BoardProductPrefabPath} must visibly contain four boards and retaining straps.");

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
            int maximumDeliverySize = deliveryConfigs.Max(config => config.ProductCount);
            Require(deliverySlots.Length == maximumDeliverySize,
                $"{DeliveryVehiclePrefabPath} must expose exactly {maximumDeliverySize} cargo slots.");
            Require(deliverySlots.All(slot => slot.IsChildOf(deliveryPrefab.transform)),
                $"Every cargo slot in {DeliveryVehiclePrefabPath} must belong to the prefab hierarchy.");
            Require(!ContainsPrefabInstance(deliveryPrefab, cementProductPrefab) &&
                    !ContainsPrefabInstance(deliveryPrefab, boardProductPrefab),
                $"{DeliveryVehiclePrefabPath} must be empty before runtime cargo spawning.");
            Require(deliveryConfigs.All(config => config.ViewPrefab == deliveryViews[0]),
                $"Every DeliveryConfig must reference the EntityBehaviour root from " +
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
            int maximumOrderSize = orderConfigs
                .SelectMany(config => config.Offers)
                .Max(offer => offer.RequiredProductCount);
            Require(customerSlots.Length == maximumOrderSize &&
                    customerSlots.All(slot => slot.IsChildOf(customerVehiclePrefab.transform)),
                $"{CustomerVehiclePrefabPath} must expose exactly {maximumOrderSize} " +
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
            Require(!ContainsPrefabInstance(customerVehiclePrefab, cementProductPrefab) &&
                    !ContainsPrefabInstance(customerVehiclePrefab, boardProductPrefab),
                $"{CustomerVehiclePrefabPath} must be empty before runtime order loading.");
            Require(customerVehicleConfig.ViewPrefab == customerViews[0],
                $"{CustomerVehicleConfigPath} must reference the InteractionView root from " +
                $"{CustomerVehiclePrefabPath}.");

            GameObject customerPrefab = RequireAsset<GameObject>(CustomerPrefabPath);
            ValidatePrefabRoot(customerPrefab, CustomerPrefabPath, requireUnitScale: true);
            EntityBehaviour[] customerActorViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(customerPrefab, CustomerPrefabPath);
            TransformRegistrar[] customerActorTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(customerPrefab, CustomerPrefabPath);
            RigidbodyRegistrar[] customerActorRigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(customerPrefab, CustomerPrefabPath);
            Rigidbody[] customerActorRigidbodies =
                RequireExactlyOneInPrefab<Rigidbody>(customerPrefab, CustomerPrefabPath);
            EntityComponentRegistrar[] customerActorRegistrars =
                customerPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Collider[] customerActorColliders =
                customerPrefab.GetComponentsInChildren<Collider>(true);
            Renderer[] customerActorRenderers =
                customerPrefab.GetComponentsInChildren<Renderer>(true);

            Require(customerActorViews[0].GetType() == typeof(EntityBehaviour) &&
                    customerActorViews[0].gameObject == customerPrefab &&
                    customerActorTransforms[0].gameObject == customerPrefab &&
                    customerActorRigidbodyRegistrars[0].gameObject == customerPrefab &&
                    customerActorRigidbodies[0].gameObject == customerPrefab,
                $"{CustomerPrefabPath} must use only the generic EntityBehaviour, Transform and " +
                "Rigidbody boundary on its root.");
            var expectedCustomerActorRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(RigidbodyRegistrar)
            };
            Require(customerActorRegistrars.Length == expectedCustomerActorRegistrarTypes.Count &&
                    new HashSet<Type>(customerActorRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedCustomerActorRegistrarTypes),
                $"{CustomerPrefabPath} must contain exactly the generic Transform and Rigidbody " +
                "registrars.");
            Require(customerActorColliders.Length == 0 &&
                    customerPrefab.GetComponentsInChildren<InteractionView>(true).Length == 0 &&
                    customerPrefab.GetComponentsInChildren<CollidersRegistrar>(true).Length == 0,
                $"{CustomerPrefabPath} must not expose interaction or collider gameplay adapters.");
            Require(customerActorRenderers.Length >= 8,
                $"{CustomerPrefabPath} must contain a visible low-poly customer silhouette.");
            Rigidbody customerActorBody = customerActorRigidbodies[0];
            Require(customerActorBody.isKinematic && !customerActorBody.useGravity &&
                    customerActorBody.interpolation == RigidbodyInterpolation.None,
                $"The Rigidbody in {CustomerPrefabPath} must be kinematic, gravity-free and use " +
                $"{RigidbodyInterpolation.None} interpolation.");
            Require(customerConfig.ViewPrefab == customerActorViews[0],
                $"{CustomerConfigPath} must reference the EntityBehaviour root from " +
                $"{CustomerPrefabPath}.");
        }

        private static void ValidateCatalogEntry(ProductConfig productConfig,
            DeliveryConfig deliveryConfig, OrderConfig orderConfig, ProductTypeId productType,
            string displayName, string unitLabel, int unitPrice, float mass, float carryMovementSpeed,
            int deliveryCount, int purchaseUnitPrice, string customerProjectTitle,
            string customerRequest)
        {
            string productPath = AssetDatabase.GetAssetPath(productConfig);
            string deliveryPath = AssetDatabase.GetAssetPath(deliveryConfig);
            string orderPath = AssetDatabase.GetAssetPath(orderConfig);
            Require(productConfig.ProductType == productType &&
                    deliveryConfig.ProductType == productType &&
                    orderConfig.RequiredProductType == productType,
                $"Catalog entry {productType} must use the same key across product, delivery and order.");
            Require(productConfig.DisplayName == displayName && productConfig.UnitLabel == unitLabel,
                $"{productPath} must expose display name '{displayName}' and unit '{unitLabel}'.");
            Require(productConfig.UnitPrice == unitPrice &&
                    Mathf.Approximately(productConfig.Mass, mass) &&
                    Mathf.Approximately(productConfig.CarryMovementSpeed, carryMovementSpeed),
                $"{productPath} has incorrect sale, mass or carry-speed values.");
            Require(productConfig.WorldInterpolation == RigidbodyInterpolation.Interpolate &&
                    productConfig.WorldCollisionDetection ==
                    CollisionDetectionMode.ContinuousSpeculative,
                $"{productPath} must use the supported loose-product physics modes.");
            Require(deliveryConfig.ProductCount == deliveryCount &&
                    deliveryConfig.PurchaseUnitPrice == purchaseUnitPrice &&
                    deliveryConfig.TotalCost == deliveryCount * purchaseUnitPrice,
                $"{deliveryPath} has incorrect delivery quantity, unit cost or total.");
            Require(orderConfig.CustomerProjectTitle == customerProjectTitle &&
                    orderConfig.CustomerRequest == customerRequest,
                $"{orderPath} has incorrect project presentation text.");
            Require(orderConfig.DefaultOfferIndex == 1,
                $"{orderPath} must select its standard offer by default.");
            Require(orderConfig.Offers.Count == 3,
                $"{orderPath} must expose exactly economy, standard and professional offers.");

            string[] expectedTitles = { "Эконом", "Стандарт", "Профи" };
            for (int index = 0; index < orderConfig.Offers.Count; index++)
            {
                OrderOfferDefinition offer = orderConfig.Offers[index];
                int expectedCount = index + 1;
                Require(offer.Title == expectedTitles[index] &&
                        !string.IsNullOrWhiteSpace(offer.Description),
                    $"{orderPath} offer {index} must have the expected title and a visible trade-off.");
                Require(offer.RequiredProductCount == expectedCount,
                    $"{orderPath} offer quantities must increase exactly from one to three.");
                Require(offer.Reward == checked(unitPrice * expectedCount),
                    $"{orderPath} offer {index} reward must equal retail price multiplied by quantity.");
            }

            int maximumOfferSize = orderConfig.Offers.Max(offer => offer.RequiredProductCount);
            Require(deliveryConfig.ProductCount >= maximumOfferSize,
                $"Delivery {productType} must contain enough products for its largest offer.");
        }

        private static Vector3 ValidateProductPrefab(ProductConfig productConfig, string productConfigPath,
            GameObject productPrefab, string productPrefabPath, bool requireUnitScale)
        {
            ValidatePrefabRoot(productPrefab, productPrefabPath, requireUnitScale);
            InteractionView[] productViews =
                RequireExactlyOneInPrefab<InteractionView>(productPrefab, productPrefabPath);
            EntityBehaviour[] productEntityViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(productPrefab, productPrefabPath);
            TransformRegistrar[] productTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(productPrefab, productPrefabPath);
            InteractionViewRegistrar[] interactionRegistrars =
                RequireExactlyOneInPrefab<InteractionViewRegistrar>(productPrefab, productPrefabPath);
            RigidbodyRegistrar[] rigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(productPrefab, productPrefabPath);
            CollidersRegistrar[] collidersRegistrars =
                RequireExactlyOneInPrefab<CollidersRegistrar>(productPrefab, productPrefabPath);
            EntityComponentRegistrar[] allRegistrars =
                productPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Rigidbody[] rigidbodies = RequireExactlyOneInPrefab<Rigidbody>(productPrefab, productPrefabPath);
            InteractionHighlight[] highlights =
                RequireExactlyOneInPrefab<InteractionHighlight>(productPrefab, productPrefabPath);
            Collider[] productColliders = productPrefab.GetComponentsInChildren<Collider>(true);

            Require(productViews[0].GetType() == typeof(InteractionView) &&
                    productViews[0].gameObject == productPrefab &&
                    productEntityViews[0].gameObject == productPrefab,
                $"The root view in {productPrefabPath} must be a non-specialized InteractionView.");
            Require(productTransforms[0].gameObject == productPrefab &&
                    interactionRegistrars[0].gameObject == productPrefab &&
                    rigidbodyRegistrars[0].gameObject == productPrefab &&
                    collidersRegistrars[0].gameObject == productPrefab &&
                    rigidbodies[0].gameObject == productPrefab,
                $"All product registrars, Rigidbody and view in {productPrefabPath} must be on its root.");
            var expectedRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(InteractionViewRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(CollidersRegistrar)
            };
            Require(allRegistrars.Length == expectedRegistrarTypes.Count &&
                    new HashSet<Type>(allRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedRegistrarTypes),
                $"{productPrefabPath} must contain only the four generic product registrars.");
            Require(productColliders.Length == 2 &&
                    productColliders.All(collider => collider.enabled && collider.gameObject.activeSelf),
                $"{productPrefabPath} must contain one solid collider and one interaction trigger.");
            Collider solidCollider = productColliders.Single(collider => !collider.isTrigger);
            Collider triggerCollider = productColliders.Single(collider => collider.isTrigger);
            Require(solidCollider.gameObject == productPrefab,
                $"The solid collider in {productPrefabPath} must be on the prefab root.");
            Require(triggerCollider.gameObject != productPrefab,
                $"The interaction trigger in {productPrefabPath} must be a separate child collider.");
            SerializedProperty highlightProperty =
                new SerializedObject(productViews[0]).FindProperty("_highlight");
            Require(highlightProperty?.objectReferenceValue == highlights[0],
                $"The InteractionView in {productPrefabPath} must reference its generic highlight.");
            Require(Mathf.Approximately(rigidbodies[0].mass, productConfig.Mass) &&
                    rigidbodies[0].interpolation == productConfig.WorldInterpolation &&
                    rigidbodies[0].collisionDetectionMode == productConfig.WorldCollisionDetection,
                $"The Rigidbody in {productPrefabPath} must match {productConfigPath}.");
            Require(productConfig.ViewPrefab == productEntityViews[0],
                $"{productConfigPath} must reference the EntityBehaviour root from {productPrefabPath}.");

            return ReadSolidProductGeometry(productPrefab, productPrefabPath);
        }

        private static Vector3 ReadSolidProductGeometry(GameObject productPrefab,
            string productPrefabPath)
        {
            Collider[] solidColliders = productPrefab.GetComponentsInChildren<Collider>(true)
                .Where(collider => !collider.isTrigger)
                .ToArray();
            Require(solidColliders.Length == 1 && solidColliders[0] is BoxCollider,
                $"{productPrefabPath} must provide exactly one solid BoxCollider.");
            BoxCollider boxCollider = (BoxCollider)solidColliders[0];
            Vector3 lossyScale = boxCollider.transform.lossyScale;
            return Vector3.Scale(
                boxCollider.size,
                new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
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
                GameObject cementProductPrefab = RequireAsset<GameObject>(CementProductPrefabPath);
                GameObject boardProductPrefab = RequireAsset<GameObject>(BoardProductPrefabPath);
                Require(!ContainsPrefabInstance(scene, cementProductPrefab) &&
                        !ContainsPrefabInstance(scene, boardProductPrefab),
                    $"{PrototypeScenePath} must not contain product prefab instances; " +
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
                    SceneRouteId.CustomerVehicleDeparture,
                    SceneRouteId.CustomerWalkToCounter,
                    SceneRouteId.CustomerWalkToVehicle
                };
                var actualRouteIds = new HashSet<SceneRouteId>(routes.Select(marker => marker.Id));
                Require(routes.Length == expectedRouteIds.Count && actualRouteIds.SetEquals(expectedRouteIds),
                    $"{PrototypeScenePath} must contain exactly one marker for both vehicle and " +
                    "customer actor routes.");

                var routeWaypoints = new Dictionary<SceneRouteId, Transform[]>();
                foreach (SceneRouteMarker route in routes)
                {
                    Transform[] waypoints = ReadObjectArray<Transform>(
                        new SerializedObject(route), "_waypoints", route.name);
                    Require(waypoints.Length == 4,
                        $"Scene route {route.Id} must contain exactly four authored waypoints.");
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

                Transform[] walkToCounter = routeWaypoints[SceneRouteId.CustomerWalkToCounter];
                Transform[] walkToVehicle = routeWaypoints[SceneRouteId.CustomerWalkToVehicle];
                Require(Vector3.Distance(walkToCounter[^1].position, walkToVehicle[0].position) < 0.001f &&
                        Quaternion.Angle(walkToCounter[^1].rotation, walkToVehicle[0].rotation) < 0.01f,
                    "Customer return route must begin at the exact counter position where the " +
                    "approach route ends, without a pose discontinuity.");
                Require(Vector3.Distance(walkToCounter[0].position, walkToVehicle[^1].position) < 0.001f,
                    "Customer return route must end at the exact vehicle-door position where the " +
                    "approach route begins.");
                Require(Vector3.Distance(arrivalParking.position, walkToCounter[0].position) < 2.5f,
                    "Customer approach route must begin beside the parked vehicle.");

                Transform[] allSceneTransforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                Require(allSceneTransforms.All(candidate => candidate.name != "Customer Truck"),
                    $"{PrototypeScenePath} must not contain the legacy static Customer Truck.");
                Require(allSceneTransforms.All(candidate => candidate.name != "Customer"),
                    $"{PrototypeScenePath} must not contain a static Customer; the actor is " +
                    "instantiated from its runtime prefab.");
                Transform customerCounter = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "Counter" && candidate.parent != null &&
                    candidate.parent.name == "Sales Kiosk");
                Require(customerCounter != null,
                    $"{PrototypeScenePath} must contain the Sales Kiosk customer counter.");
                Transform customerOrderTerminal = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "Customer Order Terminal" && candidate.parent != null &&
                    candidate.parent.name == "Sales Kiosk");
                Require(customerOrderTerminal != null,
                    $"{PrototypeScenePath} must contain the customer order terminal.");
                Bounds customerCounterBounds = customerCounter.GetComponent<Renderer>().bounds;
                Bounds customerOrderTerminalBounds =
                    customerOrderTerminal.GetComponent<Renderer>().bounds;
                Vector3 customerCounterPosition = walkToCounter[^1].position;
                Require(customerCounterPosition.z < customerCounterBounds.min.z - 0.2f &&
                        customerCounterPosition.x > customerOrderTerminalBounds.min.x &&
                        customerCounterPosition.x < customerOrderTerminalBounds.max.x,
                    "Customer walk route must end on the accessible visitor side, aligned with " +
                    "the customer order terminal rather than the divider between terminals.");

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
                Vector3 cementGeometry = ReadSolidProductGeometry(
                    cementProductPrefab,
                    CementProductPrefabPath);
                Vector3 boardGeometry = ReadSolidProductGeometry(
                    boardProductPrefab,
                    BoardProductPrefabPath);
                Vector3 maximumGeometry = new(
                    Mathf.Max(cementGeometry.x, boardGeometry.x),
                    Mathf.Max(cementGeometry.y, boardGeometry.y),
                    Mathf.Max(cementGeometry.z, boardGeometry.z));
                for (int first = 0; first < storageSlots.Length; first++)
                {
                    Bounds firstBounds = new(storageSlots[first].position, maximumGeometry);
                    for (int second = first + 1; second < storageSlots.Length; second++)
                    {
                        Bounds secondBounds = new(storageSlots[second].position, maximumGeometry);
                        Require(!firstBounds.Intersects(secondBounds),
                            $"Storage slots {storageSlots[first].name} and " +
                            $"{storageSlots[second].name} overlap for board-bundle geometry.");
                    }
                }
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

                Transform lumberDisplay = allSceneTransforms.SingleOrDefault(
                    candidate => candidate.name == "Lumber Display");
                Require(lumberDisplay != null,
                    $"{PrototypeScenePath} must contain the functional Lumber Display area.");
                Require(lumberDisplay.GetComponentsInChildren<Transform>(true)
                            .Count(candidate => candidate.name.StartsWith(
                                "Board Display Bundle",
                                StringComparison.Ordinal)) >= 3 &&
                        lumberDisplay.GetComponentsInChildren<TextMesh>(true)
                            .Any(label => label.text.Contains("B-01", StringComparison.Ordinal) &&
                                          label.text.Contains("ПАЧКА ДОСОК", StringComparison.Ordinal)),
                    "Lumber Display must visibly expose stocked board bundles, address and product signage.");

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

        private static TConfig[] LoadConfigCatalogAssets<TConfig>() where TConfig : ScriptableObject =>
            AssetDatabase.FindAssets($"t:{typeof(TConfig).Name}", new[] { ConfigFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TConfig>)
                .Where(config => config != null)
                .Distinct()
                .OrderBy(config => AssetDatabase.GetAssetPath(config), StringComparer.Ordinal)
                .ToArray();

        private static void ValidateConfigCatalogAssets<TConfig>(int expectedCount)
            where TConfig : ScriptableObject, IValidatableConfig
        {
            TConfig[] configs = LoadConfigCatalogAssets<TConfig>();
            Require(configs.Length == expectedCount,
                $"{ConfigFolder} must contain exactly {expectedCount} {typeof(TConfig).Name} assets, " +
                $"found {configs.Length}.");
            foreach (TConfig config in configs)
            {
                try
                {
                    config.Validate();
                }
                catch (Exception exception)
                {
                    string path = AssetDatabase.GetAssetPath(config);
                    throw new InvalidOperationException(
                        $"ECS architecture validation failed: {path} did not pass " +
                        $"{nameof(IValidatableConfig)}.{nameof(IValidatableConfig.Validate)}().",
                        exception);
                }
            }
        }

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
