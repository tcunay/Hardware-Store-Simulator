using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Entitas;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Common.Customers;
using HardwareStore.Gameplay.Common.Input;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Localization;
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
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
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
        private const string BrickProductConfigPath =
            "Assets/Resources/Configs/ProductConfig_BrickPack.asset";
        private const string DrywallProductConfigPath =
            "Assets/Resources/Configs/ProductConfig_DrywallSheet.asset";
        private const string PaintProductConfigPath =
            "Assets/Resources/Configs/ProductConfig_PaintBucket.asset";
        private const string InsulationProductConfigPath =
            "Assets/Resources/Configs/ProductConfig_InsulationRoll.asset";
        private const string CementDeliveryConfigPath = "Assets/Resources/Configs/DeliveryConfig.asset";
        private const string BoardDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_BoardBundle.asset";
        private const string BrickDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_BrickPack.asset";
        private const string DrywallDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_DrywallSheet.asset";
        private const string PaintDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_PaintBucket.asset";
        private const string InsulationDeliveryConfigPath =
            "Assets/Resources/Configs/DeliveryConfig_InsulationRoll.asset";
        private const string CustomerVehicleConfigPath =
            "Assets/Resources/Configs/CustomerVehicleConfig.asset";
        private const string CustomerConfigPath =
            "Assets/Resources/Configs/CustomerConfig.asset";
        private const string CustomerFlowConfigPath =
            "Assets/Resources/Configs/CustomerFlowConfig.asset";
        private const string EconomyConfigPath = "Assets/Resources/Configs/EconomyConfig.asset";
        private const string ProductRecoveryConfigPath =
            "Assets/Resources/Configs/ProductRecoveryConfig.asset";
        private const string PlatformTrolleyConfigPath =
            "Assets/Resources/Configs/PlatformTrolleyConfig.asset";
        private const string WarehouseWorkerConfigPath =
            "Assets/Resources/Configs/WarehouseWorkerConfig.asset";
        private const string StoreDayConfigPath =
            "Assets/Resources/Configs/StoreDayConfig.asset";
        private const string CementProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_CementFoundation.asset";
        private const string LumberProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_LumberShelving.asset";
        private const string WorkbenchProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_WorkbenchFoundation.asset";
        private const string GardenWallProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_GardenWall.asset";
        private const string DrywallPartitionProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_DrywallPartition.asset";
        private const string WorkshopRenovationProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_WorkshopRenovation.asset";
        private const string GarageInsulationProjectConfigPath =
            "Assets/Resources/Configs/CustomerProjectConfig_GarageInsulation.asset";
        private const string LegacyCementOrderConfigPath =
            "Assets/Resources/Configs/OrderConfig.asset";
        private const string LegacyBoardOrderConfigPath =
            "Assets/Resources/Configs/OrderConfig_BoardBundle.asset";
        private const string CementProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/CementBag.prefab";
        private const string BoardProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/BoardBundle.prefab";
        private const string BrickProductPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/BrickPack.prefab";
        private const string DrywallProductPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/DrywallSheet.prefab";
        private const string PaintProductPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/PaintBucket.prefab";
        private const string InsulationProductPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/InsulationRoll.prefab";
        private const string DeliveryVehiclePrefabPath = "Assets/_Project/Prefabs/Gameplay/DeliveryTruck.prefab";
        private const string CustomerVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicle.prefab";
        private const string CustomerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Customer.prefab";
        private const string PlatformTrolleyPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/PlatformTrolley.prefab";
        private const string WarehouseWorkerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/WarehouseWorker.prefab";
        private const string WarehouseWorkerTrolleyPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/WarehouseWorkerTrolley.prefab";
        private const string WarehouseWorkerNavMeshPath =
            "Assets/Scenes/Prototype_Yard/NavMesh-Navigation.asset";
        private const int RequiredStorageSlotCapacity = 18;

        private static readonly ProductTypeId[] ExpectedProductTypes =
        {
            ProductTypeId.CementBag,
            ProductTypeId.BoardBundle,
            ProductTypeId.BrickPack,
            ProductTypeId.DrywallSheet,
            ProductTypeId.PaintBucket,
            ProductTypeId.InsulationRoll
        };

        private static readonly CustomerProjectTypeId[] ExpectedProjectTypes =
        {
            CustomerProjectTypeId.CementFoundation,
            CustomerProjectTypeId.LumberShelving,
            CustomerProjectTypeId.WorkbenchFoundation,
            CustomerProjectTypeId.GardenWall,
            CustomerProjectTypeId.DrywallPartition,
            CustomerProjectTypeId.WorkshopRenovation,
            CustomerProjectTypeId.GarageInsulation
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
            typeof(TrolleyPressed),
            typeof(PreviousPressed),
            typeof(NextPressed),
            typeof(IncreasePressed),
            typeof(DecreasePressed),
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
            "CustomerVisitEntityId",
            "OrderWaiting",
            "OrderActive",
            "OrderCompleted",
            "OrderCompletedEvent",
            "CustomerVisitWaiting",
            "AcceptOrderSystem",
            "OrdersFeature",
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
            typeof(CustomerFlowConfig),
            typeof(CustomerProjectConfig),
            typeof(ProductConfig),
            typeof(ProductRecoveryConfig),
            typeof(PlatformTrolleyConfig),
            typeof(WarehouseWorkerConfig),
            typeof(StoreDayConfig)
        };

        private static readonly (Type Type, string AssetPath)[] ExpectedSingletonGameplayConfigs =
        {
            (typeof(PlayerConfig), PlayerConfigPath),
            (typeof(InteractionConfig), InteractionConfigPath),
            (typeof(EconomyConfig), EconomyConfigPath),
            (typeof(CustomerVehicleConfig), CustomerVehicleConfigPath),
            (typeof(CustomerConfig), CustomerConfigPath),
            (typeof(CustomerFlowConfig), CustomerFlowConfigPath),
            (typeof(ProductRecoveryConfig), ProductRecoveryConfigPath),
            (typeof(PlatformTrolleyConfig), PlatformTrolleyConfigPath),
            (typeof(WarehouseWorkerConfig), WarehouseWorkerConfigPath),
            (typeof(StoreDayConfig), StoreDayConfigPath)
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
            ValidateStoreDayArchitecture(runtimeTypes, componentTypes);
            ValidateEntityViewBindingBoundary(runtimeTypes, componentTypes);
            ValidateEntityIndices(runtimeTypes, componentTypes);
            ValidateCustomerQueueArchitecture(runtimeTypes, componentTypes);
            ValidateContextAwareInteractionFocus();
            ValidateProductRecoveryArchitecture(runtimeTypes, componentTypes);
            ValidateCollisionSafeProductDrop(runtimeTypes, componentTypes);
            ValidateTrolleyArchitecture(runtimeTypes, componentTypes);
            ValidateWarehouseWorkerArchitecture(runtimeTypes, componentTypes);
            ValidateLocalizationArchitecture(runtimeTypes, componentTypes);
            ValidateConsultationArchitecture(runtimeTypes, componentTypes);
            ValidateProcurementArchitecture(runtimeTypes, componentTypes);
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
            InputAction increase = playerMap.FindAction("Increase");
            InputAction decrease = playerMap.FindAction("Decrease");
            InputAction confirm = playerMap.FindAction("Confirm");
            InputAction interact = playerMap.FindAction("Interact");
            InputAction drop = playerMap.FindAction("Drop");
            InputAction trolley = playerMap.FindAction("Trolley");
            Require(move != null && previous != null && next != null &&
                    increase != null && decrease != null && confirm != null &&
                    interact != null && drop != null && trolley != null,
                "Player input must expose Move, catalog navigation and quantity controls, " +
                "E interaction, G drop and the dedicated F trolley action.");

            Require(HasBinding(previous, "<Keyboard>/leftArrow") &&
                    HasBinding(next, "<Keyboard>/rightArrow"),
                "Modal card navigation must use the keyboard left and right arrows.");
            Require(increase.type == InputActionType.Button &&
                    decrease.type == InputActionType.Button &&
                    HasBinding(increase, "<Keyboard>/upArrow") &&
                    HasBinding(increase, "<Gamepad>/dpad/up") &&
                    HasOnlyBindings(increase,
                        "<Keyboard>/upArrow",
                        "<Gamepad>/dpad/up") &&
                    HasBinding(decrease, "<Keyboard>/downArrow") &&
                    HasBinding(decrease, "<Gamepad>/dpad/down") &&
                    HasOnlyBindings(decrease,
                        "<Keyboard>/downArrow",
                        "<Gamepad>/dpad/down"),
                "Procurement package quantity must use only Up/Down arrows and the matching " +
                "gamepad D-pad directions.");
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
            Require(trolley.type == InputActionType.Button &&
                    string.Equals(
                        trolley.expectedControlType,
                        "Button",
                        StringComparison.Ordinal) &&
                    HasBinding(trolley, "<Keyboard>/f") &&
                    HasBinding(trolley, "<Gamepad>/buttonWest") &&
                    HasOnlyBindings(trolley,
                        "<Keyboard>/f",
                        "<Gamepad>/buttonWest"),
                "Trolley attach/detach must use only keyboard F and gamepad button West.");
            Require(HasBinding(interact, "<Keyboard>/e") &&
                    HasBinding(interact, "<Gamepad>/buttonNorth") &&
                    HasOnlyBindings(interact,
                        "<Keyboard>/e",
                        "<Gamepad>/buttonNorth") &&
                    HasBinding(drop, "<Keyboard>/g") &&
                    HasBinding(drop, "<Gamepad>/buttonEast") &&
                    HasOnlyBindings(drop,
                        "<Keyboard>/g",
                        "<Gamepad>/buttonEast"),
                "E must remain the world/product action and G must remain product drop; " +
                "neither action may alias the dedicated trolley input.");

            foreach (string propertyName in new[]
                     {
                         nameof(IInputService.TrolleyPressedThisFrame),
                         nameof(IInputService.IncreasePressedThisFrame),
                         nameof(IInputService.DecreasePressedThisFrame)
                     })
            {
                PropertyInfo pressedProperty = typeof(IInputService).GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.DeclaredOnly);
                Require(pressedProperty?.PropertyType == typeof(bool),
                    $"{nameof(IInputService)} must expose one-frame {propertyName} input " +
                    "explicitly.");
            }
            string inputServiceSource = ReadRuntimeSource(
                "Gameplay", "Common", "Input", nameof(InputSystemService) + ".cs");
            RequireSourceContains(inputServiceSource,
                "_playerMap.FindAction(\"Trolley\", true)",
                "_playerMap.FindAction(\"Increase\", true)",
                "_playerMap.FindAction(\"Decrease\", true)",
                "TrolleyPressedThisFrame => _trolley.WasPressedThisFrame()",
                "IncreasePressedThisFrame => _increase.WasPressedThisFrame()",
                "DecreasePressedThisFrame => _decrease.WasPressedThisFrame()");
            string emitInputSource = ReadRuntimeSource(
                "Gameplay", "Features", "Input", "Systems", "EmitInputSystem.cs");
            RequireSourceContains(emitInputSource,
                "input.isTrolleyPressed = _inputService.TrolleyPressedThisFrame",
                "input.isIncreasePressed = _inputService.IncreasePressedThisFrame",
                "input.isDecreasePressed = _inputService.DecreasePressedThisFrame");
            string cleanupInputSource = ReadRuntimeSource(
                "Gameplay", "Features", "Cleanup", "Systems",
                "CleanupInputRequestsSystem.cs");
            RequireSourceContains(cleanupInputSource,
                "input.isTrolleyPressed = false",
                "input.isIncreasePressed = false",
                "input.isDecreasePressed = false");
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
            Require(discoveredComponents.Contains(typeof(StoreControlTerminal)) &&
                    discoveredComponents.Contains(typeof(StoreControlTerminalEntityId)),
                "The store must own one authored control terminal through an explicit relation.");
            Require(typeof(IStoreFactory).IsAssignableFrom(typeof(StoreFactory)),
                $"{nameof(StoreFactory)} must implement {nameof(IStoreFactory)}.");

            MethodInfo createMethod = typeof(IStoreFactory).GetMethod(
                nameof(IStoreFactory.Create),
                new[] { typeof(IStoreSceneData) });
            Require(createMethod != null && createMethod.ReturnType == typeof(GameEntity),
                $"{nameof(IStoreFactory)} must create and return a Store GameEntity from " +
                $"{nameof(IStoreSceneData)}.");

            RequireMethod(
                typeof(IInteractionTargetFactory),
                nameof(IInteractionTargetFactory.CreateStoreControlTerminal),
                typeof(GameEntity),
                typeof(int));
        }

        private static void ValidateStoreDayArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] dayComponents =
            {
                typeof(StorePreparing),
                typeof(StoreOpen),
                typeof(StoreClosing),
                typeof(DayReportOpen),
                typeof(StoreControlTerminal),
                typeof(DayNumber),
                typeof(CurrentDayMinute),
                typeof(DayOpeningBalance),
                typeof(DayRevenue),
                typeof(DayProcurementExpenses),
                typeof(DayUpgradeExpenses),
                typeof(DayPayrollExpenses),
                typeof(DayCompletedOrderCount),
                typeof(DayLostCustomerCount),
                typeof(StoreControlTerminalEntityId),
                typeof(DayReportStoreEntityId)
            };
            foreach (Type component in dayComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Store day gameplay requires the {component.Name} Game component.");
            }
            RequireComponentIndexAttribute(
                typeof(DayReportStoreEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithDayReportStoreEntityId",
                typeof(GameEntity));
            Require(GameComponentsLookup.componentTypes.Length == 275 &&
                    InputComponentsLookup.componentTypes.Length == 14,
                "The mixed-procurement slice must expose the exact generated registry sizes " +
                "of 275 Game components and 14 Input components.");

            Type featureType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "StoreDayFeature");
            Require(featureType != null && typeof(Feature).IsAssignableFrom(featureType),
                "StoreDayFeature must remain an explicit Entitas feature.");
            string[] systemNames =
            {
                "TickStoreDayClockSystem",
                "ReachStoreClosingTimeSystem",
                "OpenStoreSystem",
                "StartNextDaySystem",
                "OpenDayReportSystem"
            };
            foreach (string systemName in systemNames)
            {
                Type systemType = runtimeTypes.SingleOrDefault(type => type.Name == systemName);
                Require(systemType != null && typeof(IExecuteSystem).IsAssignableFrom(systemType),
                    $"{systemName} must remain an executable Entitas system.");
            }
            Type reconcileMoneyOverrideSystemType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "ReconcileEditorMoneyOverrideSystem");
            Type validateStoreDayStateSystemType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "ValidateStoreDayStateSystem");
            Require(reconcileMoneyOverrideSystemType != null &&
                    typeof(IExecuteSystem).IsAssignableFrom(reconcileMoneyOverrideSystemType),
                "Editor Play Mode money overrides require an executable reconciliation system.");
            Require(validateStoreDayStateSystemType != null &&
                    typeof(IExecuteSystem).IsAssignableFrom(validateStoreDayStateSystemType),
                "The final store-day ledger validator must remain executable.");

            string configSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(StoreDayConfig) + ".cs");
            RequireSourceContains(configSource,
                "private int _startMinute = 480",
                "private int _closingMinute = 1200",
                "private float _dayDurationSeconds = 480f",
                "public int StartMinute => _startMinute",
                "public int ClosingMinute => _closingMinute",
                "public float DayDurationSeconds => _dayDurationSeconds");
            StoreDayConfig config = RequireAsset<StoreDayConfig>(StoreDayConfigPath);
            Require(config.StartMinute == 8 * 60 && config.ClosingMinute == 20 * 60 &&
                    Mathf.Approximately(config.DayDurationSeconds, 480f),
                $"{StoreDayConfigPath} must author an 08:00-20:00 day lasting 480 real seconds.");

            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", nameof(StaticDataService) + ".cs");
            RequireSourceContains(staticDataSource,
                "Load<StoreDayConfig>(nameof(StoreDayConfig))",
                "storeDay.Validate()",
                "StoreDay = storeDay");
            string factorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(StoreFactory) + ".cs");
            RequireSourceContains(factorySource,
                "AddDayNumber(1)",
                "AddCurrentDayMinute(_staticData.StoreDay.StartMinute)",
                "AddDayOpeningBalance(initialMoney)",
                "AddDayRevenue(0)",
                "AddDayProcurementExpenses(0)",
                "AddDayUpgradeExpenses(0)",
                "AddDayPayrollExpenses(0)",
                "AddDayCompletedOrderCount(0)",
                "AddDayLostCustomerCount(0)",
                "isStorePreparing = true",
                "CreateStoreControlTerminal(store.EntityId)",
                "AddStoreControlTerminalEntityId(storeControlTerminal.EntityId)");

            string featureSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "StoreDayFeature.cs");
            int previousSystemIndex = -1;
            foreach (string systemName in systemNames)
            {
                string token = $"Create<{systemName}>()";
                int systemIndex = featureSource.IndexOf(token, StringComparison.Ordinal);
                Require(systemIndex > previousSystemIndex &&
                        CountOccurrences(featureSource, token) == 1,
                    $"StoreDayFeature must execute {string.Join(" -> ", systemNames)} exactly once.");
                previousSystemIndex = systemIndex;
            }

            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            int interactionIndex = storeFeatureSource.IndexOf(
                "Create<InteractionFeature>()", StringComparison.Ordinal);
            int reconcileMoneyIndex = storeFeatureSource.IndexOf(
                "Create<ReconcileEditorMoneyOverrideSystem>()", StringComparison.Ordinal);
            int dayIndex = storeFeatureSource.IndexOf(
                "Create<StoreDayFeature>()", StringComparison.Ordinal);
            int procurementIndex = storeFeatureSource.IndexOf(
                "Create<ProcurementFeature>()", StringComparison.Ordinal);
            int interactionPromptIndex = storeFeatureSource.IndexOf(
                "Create<InteractionPromptFeature>()", StringComparison.Ordinal);
            int validationIndex = storeFeatureSource.IndexOf(
                "Create<ValidateStoreDayStateSystem>()", StringComparison.Ordinal);
            int presentationIndex = storeFeatureSource.IndexOf(
                "Create<PresentationFeature>()", StringComparison.Ordinal);
            Require(reconcileMoneyIndex >= 0 && interactionIndex > reconcileMoneyIndex &&
                    dayIndex > interactionIndex && procurementIndex > dayIndex &&
                    interactionPromptIndex > procurementIndex &&
                    validationIndex > interactionPromptIndex &&
                    presentationIndex > validationIndex &&
                    CountOccurrences(storeFeatureSource,
                        "Create<ReconcileEditorMoneyOverrideSystem>()") == 1 &&
                    CountOccurrences(storeFeatureSource,
                        "Create<ValidateStoreDayStateSystem>()") == 1 &&
                    CountOccurrences(storeFeatureSource, "Create<StoreDayFeature>()") == 1,
                "StoreFeature must reconcile external Editor money overrides before world " +
                "interactions, then strictly validate the final gameplay ledger before " +
                "presentation.");

            string tickSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "TickStoreDayClockSystem.cs");
            RequireSourceContains(tickSource,
                "GameMatcher.StoreOpen",
                "GameMatcher.CurrentDayMinute",
                ".NoneOf(GameMatcher.Destructed)",
                "(config.ClosingMinute - config.StartMinute) / config.DayDurationSeconds",
                "_time.DeltaTime * _minutesPerSecond");
            string reachClosingSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "ReachStoreClosingTimeSystem.cs");
            RequireSourceContains(reachClosingSource,
                "GameMatcher.StoreOpen",
                "store.ReplaceCurrentDayMinute(_closingMinute)",
                "store.isStoreOpen = false",
                "store.isStoreClosing = true",
                "store.RemoveCustomerCooldownRemaining()",
                "LocalizationKey.NotificationStoreClosingTime");

            string spawnCustomerSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "SpawnCustomerVisitSystem.cs");
            string tickCooldownSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "TickCustomerCooldownSystem.cs");
            string arrivalScheduleSource = ReadRuntimeSource(
                "Gameplay", "Common", "Customers", "CustomerArrivalSchedule.cs");
            RequireSourceContains(spawnCustomerSource,
                "GameMatcher.StoreOpen",
                "GetEntitiesWithCustomerParkingSpotStoreEntityId",
                "GetEntityWithReservedCustomerParkingSpotEntityId",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "float nextDelay = _arrivalSchedule.GetDelay(store.CurrentDayMinute)",
                "store.ReplaceCustomerCooldownRemaining(nextDelay)");
            RequireSourceContains(tickCooldownSource, "GameMatcher.StoreOpen");
            RequireSourceContains(arrivalScheduleSource,
                "staticData.CustomerFlow.ArrivalSchedule",
                "currentDayMinute < first.Minute || currentDayMinute > last.Minute",
                "left.Delay + (right.Delay - left.Delay) * progress");

            string openStoreSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems", "OpenStoreSystem.cs");
            RequireSourceContains(openStoreSource,
                "GameMatcher.InteractionRequest",
                "terminal.isStoreControlTerminal",
                "store.isStorePreparing",
                "staticData.CustomerFlow.FirstArrivalDelay",
                "StoreDayCustomerVisitGuard.CountActiveVisits(",
                "store.isStoreOpen = true",
                "store.AddCustomerCooldownRemaining(_firstCustomerDelay)",
                "LocalizationKey.NotificationStoreOpened");
            string openReportSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems", "OpenDayReportSystem.cs");
            RequireSourceContains(openReportSource,
                "store.isStoreClosing",
                "StoreDayCustomerVisitGuard.CountActiveVisits(",
                "task.WarehouseTaskStep != WarehouseTaskStepId.Blocked",
                "if (IsTasklessEmptyTrolleyReturn(worker))",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "GetEntityWithTrolleyPusherEntityId(worker.EntityId)",
                "trolley.OccupiedTrolleySlotCount != 0",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "GetEntityWithAssignedWorkerEntityId(",
                "worker.isHandsOccupied || worker.isCarryingProduct",
                "GetEntityWithCarrierEntityId(worker.EntityId)",
                "player.isModalOpen || player.isHandsOccupied",
                "store.isDayReportOpen = true",
                "player.AddDayReportStoreEntityId(store.EntityId)",
                "player.isModalOpen = true");
            string startNextDaySource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems", "StartNextDaySystem.cs");
            RequireSourceContains(startNextDaySource,
                "InputMatcher.ConfirmPressed",
                "GameMatcher.DayReportStoreEntityId",
                "store.ReplaceDayNumber(nextDayNumber)",
                "store.ReplaceCurrentDayMinute(_startMinute)",
                "store.ReplaceDayOpeningBalance(openingBalance)",
                "store.ReplaceDayRevenue(0)",
                "store.ReplaceDayProcurementExpenses(0)",
                "store.ReplaceDayUpgradeExpenses(0)",
                "store.ReplaceDayPayrollExpenses(0)",
                "store.ReplaceDayCompletedOrderCount(0)",
                "store.ReplaceDayLostCustomerCount(0)",
                "store.isStorePreparing = true",
                "player.RemoveDayReportStoreEntityId()");
            Require(!startNextDaySource.Contains("ReplaceMoney", StringComparison.Ordinal) &&
                    !startNextDaySource.Contains("NextProjectSequenceIndex", StringComparison.Ordinal) &&
                    !startNextDaySource.Contains("ReplaceCompletedOrderCount(", StringComparison.Ordinal) &&
                    !startNextDaySource.Contains("StorageProductCount", StringComparison.Ordinal) &&
                    !startNextDaySource.Contains("Delivery", StringComparison.Ordinal) &&
                    !startNextDaySource.Contains("Trolley", StringComparison.Ordinal),
                "Starting the next day must reset only the daily clock/ledger/modal and preserve " +
                "money, stock, delivery, trolley, progression and project sequence state.");
            string validationSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "ValidateStoreDayStateSystem.cs");
            RequireSourceContains(validationSource,
                "phaseCount != 1",
                "store.DayOpeningBalance + store.DayRevenue",
                "store.DayProcurementExpenses - store.DayUpgradeExpenses",
                "store.DayPayrollExpenses",
                "store.isStorePreparing",
                "store.isStoreOpen",
                "store.isStoreClosing",
                "store.isDayReportOpen",
                "player.isModalOpen || player.isHandsOccupied");
            RequireSourceContains(validationSource,
                "if (expectedMoney != store.Money)",
                "throw new InvalidOperationException(",
                "violates its day ledger");
            Require(!validationSource.Contains(
                    "ReplaceDayOpeningBalance", StringComparison.Ordinal) &&
                    !validationSource.Contains(
                    "Debug.Log", StringComparison.Ordinal),
                "The final store-day validator must remain strict and must not repair or " +
                "silence gameplay ledger bugs.");
            string reconcileMoneySource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "ReconcileEditorMoneyOverrideSystem.cs");
            RequireSourceContains(reconcileMoneySource,
                "#if UNITY_EDITOR",
                "GameMatcher.Money",
                "GameMatcher.DayOpeningBalance",
                "GameMatcher.DayRevenue",
                "GameMatcher.DayProcurementExpenses",
                "GameMatcher.DayUpgradeExpenses",
                "GameMatcher.DayPayrollExpenses",
                "long balanceDelta = (long)store.Money - expectedMoney",
                "adjustedOpeningBalance < 0",
                "adjustedOpeningBalance > int.MaxValue",
                "store.ReplaceDayOpeningBalance((int)adjustedOpeningBalance)",
                "Debug.LogWarning(",
                "Player builds remain strictly validated");
            RequireSourceOrder(reconcileMoneySource,
                "if (expectedMoney == store.Money)",
                "store.ReplaceDayOpeningBalance((int)adjustedOpeningBalance)",
                "A balanced store must not be mutated by Editor reconciliation.");

            string purchaseDeliverySource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "PurchaseDeliverySystem.cs");
            RequireSourceContains(purchaseDeliverySource,
                "store.DayProcurementExpenses + evaluation.DeliveryCost",
                "store.DayPayrollExpenses",
                "ledgerBalanceAfterPurchase != moneyAfterPurchase",
                "store.ReplaceMoney(moneyAfterPurchase)",
                "store.ReplaceDayProcurementExpenses(procurementExpensesAfterPurchase)",
                "request.isPurchaseDeliverySucceeded = true");
            RequireSourceOrder(
                purchaseDeliverySource,
                "if (!evaluation.CanPurchase)",
                "store.ReplaceDayProcurementExpenses(procurementExpensesAfterPurchase)",
                "A rejected procurement request must leave the day ledger untouched.");
            string rewardSource = ReadRuntimeSource(
                "Gameplay", "Features", "Orders", "Systems",
                "RewardCompletedOrderSystem.cs");
            RequireSourceContains(rewardSource,
                "GameMatcher.OrderRewarded",
                "store.DayRevenue + visit.OrderReward",
                "store.DayPayrollExpenses",
                "store.DayCompletedOrderCount + 1",
                "store.ReplaceDayRevenue(revenueAfterReward)",
                "store.ReplaceDayCompletedOrderCount(completedOrdersAfterReward)",
                "visit.isOrderRewarded = true");
            string trolleyPurchaseSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "PurchasePlatformTrolleySystem.cs");
            RequireSourceContains(trolleyPurchaseSource,
                "store.DayUpgradeExpenses + _config.PurchasePrice",
                "store.DayPayrollExpenses",
                "ledgerBalanceAfterPurchase != moneyAfterPurchase",
                "store.ReplaceMoney(moneyAfterPurchase)",
                "store.ReplaceDayUpgradeExpenses(upgradeExpensesAfterPurchase)");
            RequireSourceOrder(
                trolleyPurchaseSource,
                "if (!debit.CanDebit)",
                "store.ReplaceDayUpgradeExpenses(upgradeExpensesAfterPurchase)",
                "A rejected trolley debit must leave the day ledger untouched.");

            ValidateImmutableSnapshotType(typeof(DayClockSnapshot));
            ValidateImmutableSnapshotType(typeof(DayNightSnapshot));
            ValidateImmutableSnapshotType(typeof(DayReportSnapshot));
            ValidateSnapshotProperties(
                typeof(DayClockSnapshot),
                (nameof(DayClockSnapshot.DayNumber), typeof(int)),
                (nameof(DayClockSnapshot.CurrentDayMinute), typeof(int)),
                (nameof(DayClockSnapshot.Phase), typeof(StoreDayPhase)));
            ValidateSnapshotProperties(
                typeof(DayNightSnapshot),
                (nameof(DayNightSnapshot.NormalizedTime), typeof(float)));
            ValidateSnapshotProperties(
                typeof(DayReportSnapshot),
                (nameof(DayReportSnapshot.DayNumber), typeof(int)),
                (nameof(DayReportSnapshot.OpeningBalance), typeof(int)),
                (nameof(DayReportSnapshot.Revenue), typeof(int)),
                (nameof(DayReportSnapshot.ProcurementExpenses), typeof(int)),
                (nameof(DayReportSnapshot.UpgradeExpenses), typeof(int)),
                (nameof(DayReportSnapshot.PayrollExpenses), typeof(int)),
                (nameof(DayReportSnapshot.NetCashFlow), typeof(int)),
                (nameof(DayReportSnapshot.ClosingBalance), typeof(int)),
                (nameof(DayReportSnapshot.CompletedOrderCount), typeof(int)),
                (nameof(DayReportSnapshot.LostCustomerCount), typeof(int)),
                (nameof(DayReportSnapshot.StorageProductCount), typeof(int)));
            Require(typeof(DayClockSnapshot).GetConstructor(new[]
                    {
                        typeof(int), typeof(int), typeof(StoreDayPhase)
                    }) != null &&
                    typeof(DayNightSnapshot).GetConstructor(new[] { typeof(float) }) != null &&
                    typeof(DayReportSnapshot).GetConstructor(Enumerable.Repeat(
                        typeof(int), 10).ToArray()) != null,
                "Store day presentation snapshots must expose their exact immutable constructors.");
            Require(typeof(HudSnapshot).GetProperty(nameof(HudSnapshot.DayClock))
                        ?.PropertyType == typeof(DayClockSnapshot),
                $"{nameof(HudSnapshot)} must expose its semantic {nameof(DayClockSnapshot)}.");
            Require(Enum.GetValues(typeof(StoreDayPhase)).Cast<StoreDayPhase>()
                    .SequenceEqual(new[]
                    {
                        StoreDayPhase.Preparing,
                        StoreDayPhase.Open,
                        StoreDayPhase.Closing,
                        StoreDayPhase.Report
                    }),
                $"{nameof(StoreDayPhase)} must expose exactly Preparing, Open, Closing and Report.");

            Type promptSystem = runtimeTypes.SingleOrDefault(type =>
                type.Name == "ResolveStoreControlTerminalPromptSystem");
            Type dayNightSystem = runtimeTypes.SingleOrDefault(type =>
                type.Name == "PresentDayNightSystem");
            Type dayReportSystem = runtimeTypes.SingleOrDefault(type =>
                type.Name == "PresentDayReportSystem");
            Require(promptSystem != null && typeof(IExecuteSystem).IsAssignableFrom(promptSystem) &&
                    dayNightSystem != null && typeof(IExecuteSystem).IsAssignableFrom(dayNightSystem) &&
                    dayReportSystem != null && typeof(IExecuteSystem).IsAssignableFrom(dayReportSystem),
                "Store control prompts and day/night/report presentation must remain explicit " +
                "execute systems.");
            string promptFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "InteractionPromptFeature.cs");
            string promptToken = "Create<ResolveStoreControlTerminalPromptSystem>()";
            Require(promptFeatureSource.TrimEnd().LastIndexOf(
                        promptToken, StringComparison.Ordinal) >= 0 &&
                    CountOccurrences(promptFeatureSource, promptToken) == 1 &&
                    promptFeatureSource.IndexOf(promptToken, StringComparison.Ordinal) >
                    promptFeatureSource.IndexOf(
                        "Create<ResolvePlatformTrolleyPromptSystem>()",
                        StringComparison.Ordinal),
                "The store control prompt must resolve once after the existing specialized prompts.");
            string promptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveStoreControlTerminalPromptSystem.cs");
            RequireSourceContains(promptSource,
                "InteractionTypeId.StoreControlTerminal",
                "LocalizationKey.PromptOpenStore",
                "LocalizationKey.PromptCloseStoreCustomerActive",
                "LocalizationKey.PromptCloseStoreHandsOccupied",
                "LocalizationKey.PromptCloseStoreForReport",
                "CountActiveCustomerVisits(store.EntityId)",
                "IsTasklessEmptyTrolleyReturn(worker, workerTask)",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "GetEntityWithTrolleyPusherEntityId(worker.EntityId)",
                "trolley.OccupiedTrolleySlotCount != 0",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "player.isHandsOccupied");
            string orderCounterPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveOrderCounterPromptSystem.cs");
            RequireSourceContains(orderCounterPromptSource,
                "ResolveNoCustomerPrompt(",
                "orderCounter.StoreEntityId",
                "store.isStorePreparing",
                "LocalizationKey.PromptCounterOpenStoreAtControlTerminal",
                "store.isStoreOpen",
                "LocalizationKey.PromptCounterWaitCustomer",
                "store.isStoreClosing",
                "LocalizationKey.PromptCounterFinishDayAtControlTerminal",
                "store.isDayReportOpen");
            RequireSourceOrder(
                orderCounterPromptSource,
                "if (customerVisit == null)",
                "ResolveNoCustomerPrompt(",
                "A missing visit must resolve its prompt from the store day phase.");
            RequireSourceOrder(
                orderCounterPromptSource,
                "if (store.isStorePreparing)",
                "if (store.isStoreOpen)",
                "The order counter must distinguish preparation from an open cooldown.");
            RequireSourceOrder(
                orderCounterPromptSource,
                "if (store.isStoreOpen)",
                "if (store.isStoreClosing)",
                "The order counter must distinguish an open cooldown from closing.");

            string presentationFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "PresentationFeature.cs");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentHudSystem>()",
                "Create<PresentDayNightSystem>()",
                "Day/night presentation must run after the semantic HUD snapshot.");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentDayNightSystem>()",
                "Create<PresentDayReportSystem>()",
                "The environment must update before the mandatory report modal.");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentDayReportSystem>()",
                "Create<PresentProcurementSystem>()",
                "The day report must precede optional gameplay modal presenters.");
            Require(typeof(IDayNightPresentationService).IsAssignableFrom(
                        typeof(PrototypeDayNightView)) &&
                    typeof(IDayNightPresentationService).IsAssignableFrom(
                        typeof(StoreSceneData)),
                "The scene data and prototype view must share one day/night presentation boundary.");
            RequireMethod(
                typeof(IDayNightPresentationService),
                nameof(IDayNightPresentationService.Present),
                typeof(void),
                typeof(DayNightSnapshot));
            RequireMethod(
                typeof(IHudService),
                nameof(IHudService.PresentDayReport),
                typeof(void),
                typeof(DayReportSnapshot?));
            RequireMethod(
                typeof(PrototypeDayNightView),
                nameof(PrototypeDayNightView.Configure),
                typeof(void),
                typeof(Light),
                typeof(Light[]));
            RequireMethod(
                typeof(PrototypeSceneInitializer),
                nameof(PrototypeSceneInitializer.Configure),
                typeof(void),
                typeof(SpawnPointMarker[]),
                typeof(SceneRouteMarker[]),
                typeof(CustomerFlowLayoutMarker),
                typeof(SceneViewMarker[]),
                typeof(PrototypeHudView),
                typeof(PrototypeAudioView),
                typeof(PrototypeDayNightView));
            RequireMethod(
                typeof(IStoreSceneData),
                nameof(IStoreSceneData.GetCustomerFlowLayout),
                typeof(CustomerFlowSceneLayout));
            RequireMethod(
                typeof(CustomerFlowLayoutMarker),
                nameof(CustomerFlowLayoutMarker.Configure),
                typeof(void),
                typeof(CustomerParkingSpotLayoutMarker[]),
                typeof(Transform[]),
                typeof(Transform[]),
                typeof(Transform[]));
            Require(typeof(CustomerFlowSceneLayout).GetConstructor(new[]
                    {
                        typeof(CustomerParkingSpotSceneLayout[]),
                        typeof(Pose[]),
                        typeof(Pose[]),
                        typeof(Pose[])
                    }) != null &&
                    typeof(CustomerParkingSpotSceneLayout).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(Pose[]),
                        typeof(Pose[]),
                        typeof(Pose[]),
                        typeof(Pose[]),
                        typeof(Pose[])
                    }) != null,
                "Customer scene flow must use typed immutable snapshot value objects.");
            string storeSceneDataSource = ReadRuntimeSource(
                "Gameplay", "Scene", nameof(StoreSceneData) + ".cs");
            RequireSourceContains(
                storeSceneDataSource,
                "_customerFlowLayout = customerFlowLayout.Layout",
                "return _customerFlowLayout.Clone()");
            string dayNightViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(PrototypeDayNightView) + ".cs");
            RequireSourceContains(dayNightViewSource,
                "RequiredIndoorLightCount = 2",
                "new Material(_originalSkybox)",
                "HideFlags.DontSave",
                "RenderSettings.skybox = _runtimeSkybox",
                "_indoorNightIntensities[index] = indoorLight.intensity",
                "ReleaseRuntimeState()");
            string hudViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(PrototypeHudView) + ".cs");
            RequireSourceContains(hudViewSource,
                "public const float NewDayFadeHoldSeconds = 0.2f",
                "public const float NewDayFadeOutSeconds = 0.8f",
                "Mathf.Max(0.65f",
                "Screen.width / 1600f",
                "Screen.height / 900f",
                "if (_dayReport.HasValue)",
                "float panelWidth = Mathf.Min(420f, _canvasWidth - 48f)",
                "const float phaseTopOffset = 44f",
                "const float horizontalPadding = 16f",
                "_promptStyle.CalcHeight(",
                "float panelHeight = phaseTopOffset + phaseHeight + 10f",
                "new Rect(42f, 106f, 700f, 46f)",
                "LocalizationKey.HudCustomerFlow",
                "_snapshot.CustomerFlow.TotalActiveCount",
                "float panelWidth = Mathf.Min(780f, _canvasWidth - 48f)",
                "float panelHeight = Mathf.Min(650f, _canvasHeight - 64f)",
                "LocalizationKey.HudDayReportLostCustomers",
                "ShouldStartNewDayFade(_snapshot.DayClock, snapshot.DayClock)",
                "_newDayFadeStartedAt = Time.unscaledTime",
                "previous.Phase == StoreDayPhase.Report",
                "current.Phase == StoreDayPhase.Preparing",
                "current.DayNumber == previous.DayNumber + 1",
                "public static float EvaluateNewDayFadeAlpha(float elapsedSeconds)",
                "elapsedSeconds <= NewDayFadeHoldSeconds",
                "Mathf.SmoothStep(0f, 1f, fadeProgress)",
                "new Rect(0f, 0f, _canvasWidth, _canvasHeight)");
            Require(CountOccurrences(hudViewSource, "DrawNewDayFade();") == 4 &&
                    hudViewSource.LastIndexOf(
                        "DrawNewDayFade();", StringComparison.Ordinal) >
                    hudViewSource.IndexOf("DrawCursorHint();", StringComparison.Ordinal),
                "The new-day fade must remain the final overlay for the HUD and every modal.");
            Require(!hudViewSource.Contains("InputContext", StringComparison.Ordinal) &&
                    !hudViewSource.Contains("ModalOpen", StringComparison.Ordinal),
                "The presentation-only new-day fade must not capture gameplay input or modal state.");
            RequireSourceOrder(
                hudViewSource,
                "if (_dayReport.HasValue)",
                "if (_consultation.HasValue)",
                "The mandatory day report must remain the highest-priority modal at " +
                "1280x720 and larger viewports.");
            RequireSourceContains(
                ReadRuntimeSource(
                    "Gameplay", "Features", "Presentation", "Systems",
                    "PresentDayNightSystem.cs"),
                "GameMatcher.CurrentDayMinute",
                "(currentMinute - _startMinute) /",
                "new DayNightSnapshot(normalizedTime)");
            RequireSourceContains(
                ReadRuntimeSource(
                    "Gameplay", "Features", "Presentation", "Systems",
                    "PresentDayReportSystem.cs"),
                "GameMatcher.StoreEntityId",
                "player.hasDayReportStoreEntityId",
                "playerStore.isDayReportOpen",
                "playerStore.DayLostCustomerCount",
                "new DayReportSnapshot(",
                "_hud.PresentDayReport(null)");

            Require((int)LocalizationKey.HudDayClock == 1039 &&
                    (int)LocalizationKey.HudDayReportContinue == 1052 &&
                    (int)LocalizationKey.HudObjectivePreparing == 1053 &&
                    (int)LocalizationKey.HudObjectiveClosing == 1054 &&
                    (int)LocalizationKey.HudDayReportLostCustomers == 1063 &&
                    (int)LocalizationKey.PromptOpenStore == 2081 &&
                    (int)LocalizationKey.PromptCloseStoreForReport == 2085 &&
                    (int)LocalizationKey.PromptCounterOpenStoreAtControlTerminal == 2086 &&
                    (int)LocalizationKey.PromptCounterFinishDayAtControlTerminal == 2087 &&
                    (int)LocalizationKey.PromptCustomerLeftImpatient == 2098 &&
                    (int)LocalizationKey.NotificationStoreOpened == 3035 &&
                    (int)LocalizationKey.NotificationStoreClosingTime == 3036 &&
                    (int)LocalizationKey.NotificationCustomerPatienceLow == 3041 &&
                    (int)LocalizationKey.NotificationCustomerLeftImpatient == 3042 &&
                    (int)LocalizationKey.WorldStoreControlTerminal == 4009 &&
                    (int)LocalizationKey.WorldCustomerDissatisfied == 4011,
                "Store-day localization keys must preserve their assigned stable ranges and values.");
            var removedPatienceCountdownKeys = new HashSet<string>
            {
                "HudCustomerFlowWithPatience",
                "PromptCounterNextCustomerApproachingWithPatience",
                "PromptDiscussProjectWithPatience",
                "PromptCounterBlockedWithPatience"
            };
            Require(Enum.GetNames(typeof(LocalizationKey))
                    .All(name => !removedPatienceCountdownKeys.Contains(name)),
                "Numeric customer-patience HUD and prompt localization keys must remain " +
                "removed; dissatisfaction is communicated through the actor view.");
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
                typeof(CustomerVisitQueued),
                typeof(CustomerVisitConsulting),
                typeof(CustomerVisitWaitingForLoadingBay),
                typeof(CustomerVisitMovingToLoadingBay),
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

            Type[] orderGraphComponents =
            {
                typeof(OrderLine),
                typeof(OrderContentReleased),
                typeof(OrderEntityId),
                typeof(OrderLineEntityId),
                typeof(ConsultationOfferVisitEntityId),
                typeof(ConsultationOfferEntityId),
                typeof(LineIndex)
            };
            foreach (Type component in orderGraphComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Mixed orders require the {component.Name} Game component.");
            }
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
                typeof(CustomerWaitingInQueue),
                typeof(CustomerWaitingAtCounter),
                typeof(CustomerReturningToVehicle),
                typeof(CustomerAbandonReturningToVehicle),
                typeof(CustomerReturnRoute)
            };
            foreach (Type actorComponent in customerActorComponents)
            {
                Require(discoveredComponents.Contains(actorComponent),
                    $"Runtime customers require the {actorComponent.Name} Game component.");
            }
            Require(discoveredComponents.Contains(typeof(CustomerVisitStoreEntityId)),
                $"{nameof(CustomerVisitStoreEntityId)} must relate every queued or active " +
                "visit to its store.");
            Require(discoveredComponents.Contains(typeof(DeliveryProcurementTerminalEntityId)),
                $"{nameof(DeliveryProcurementTerminalEntityId)} must uniquely relate the active delivery " +
                "to its procurement terminal.");
            Type[] procurementGraphComponents =
            {
                typeof(ProcurementCart),
                typeof(ProcurementCartLine),
                typeof(ProcurementCartTerminalEntityId),
                typeof(ProcurementCartEntityId),
                typeof(ProcurementCartPackageCapacity),
                typeof(ProcurementPackageCount),
                typeof(PurchaseOrder),
                typeof(PurchaseOrderLine),
                typeof(PurchaseOrderProcurementTerminalEntityId),
                typeof(PurchaseOrderEntityId),
                typeof(PurchaseOrderPackageCount),
                typeof(PurchaseOrderProductCount),
                typeof(PurchaseOrderCost),
                typeof(PurchaseOrderLineIndex),
                typeof(PurchaseOrderLinePackageCount),
                typeof(PurchaseOrderLineProductCount),
                typeof(PurchaseOrderLineCost),
                typeof(PurchaseOrderLineStockedProductCount),
                typeof(DeliveryPurchaseOrderEntityId),
                typeof(PurchaseOrderLineEntityId)
            };
            foreach (Type component in procurementGraphComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Mixed procurement requires the {component.Name} Game component.");
            }
            Require(discoveredComponents.Contains(typeof(CarrierEntityId)),
                $"{nameof(CarrierEntityId)} must relate the carried product to its carrier.");
            Require(discoveredComponents.Contains(typeof(ReservedDeliverySlotIndex)) &&
                    discoveredComponents.Contains(typeof(ReservedStorageSlotIndex)) &&
                    discoveredComponents.Contains(typeof(ReservedOrderLineEntityId)),
                "Recoverable products require explicit delivery-slot, storage-slot and " +
                "order-line reservations.");
            Type[] customerFlowComponents =
            {
                typeof(CustomerParkingSpot),
                typeof(CustomerQueueSpot),
                typeof(CustomerLoadingBay),
                typeof(CustomerTrafficLane),
                typeof(CustomerParkingSpotStoreEntityId),
                typeof(CustomerQueueSpotStoreEntityId),
                typeof(CustomerLoadingBayStoreEntityId),
                typeof(CustomerTrafficLaneStoreEntityId),
                typeof(ReservedCustomerParkingSpotEntityId),
                typeof(ReservedCustomerQueueSpotEntityId),
                typeof(ReservedCustomerLoadingBayEntityId),
                typeof(ReservedCustomerTrafficLaneEntityId),
                typeof(ServingOrderCounterEntityId),
                typeof(ParkingSpotIndex),
                typeof(QueueSpotIndex),
                typeof(CustomerArrivalSequence),
                typeof(NextCustomerArrivalSequence),
                typeof(CustomerVehicleArrivalRoute),
                typeof(CustomerVehicleToLoadingRoute),
                typeof(CustomerVehicleParkingDepartureRoute),
                typeof(CustomerQueueAbandonRoute),
                typeof(CustomerApproachRoute),
                typeof(CustomerLoadingDepartureRoute)
            };
            foreach (Type component in customerFlowComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Customer queues require the {component.Name} Game component.");
            }
            Require(discoveredComponents.Contains(typeof(TrolleyStoreEntityId)) &&
                    discoveredComponents.Contains(typeof(TrolleyPusherEntityId)) &&
                    discoveredComponents.Contains(typeof(TrolleyEntityId)),
                "Platform trolleys require indexed store, pusher and cargo relations.");
            RequireComponentIndexAttribute(
                typeof(CarrierEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerVisitStoreEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(DeliveryProcurementTerminalEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(ProcurementCartTerminalEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(ProcurementCartEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(PurchaseOrderProcurementTerminalEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(PurchaseOrderEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(DeliveryPurchaseOrderEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(PurchaseOrderLineEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerActorVisitEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(OrderEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(OrderLineEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(DeliveryEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(ReservedOrderLineEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(TrolleyStoreEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(TrolleyPusherEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(TrolleyEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(ConsultationOfferVisitEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(ConsultationOfferEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerParkingSpotStoreEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(CustomerQueueSpotStoreEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            Type[] primaryCustomerFlowRelations =
            {
                typeof(CustomerLoadingBayStoreEntityId),
                typeof(CustomerTrafficLaneStoreEntityId),
                typeof(ReservedCustomerParkingSpotEntityId),
                typeof(ReservedCustomerQueueSpotEntityId),
                typeof(ReservedCustomerLoadingBayEntityId),
                typeof(ReservedCustomerTrafficLaneEntityId),
                typeof(ServingOrderCounterEntityId)
            };
            foreach (Type relation in primaryCustomerFlowRelations)
            {
                RequireComponentIndexAttribute(
                    relation,
                    "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            }

            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithCarrierEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithCustomerVisitStoreEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithDeliveryProcurementTerminalEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithProcurementCartTerminalEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithProcurementCartEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithPurchaseOrderProcurementTerminalEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithPurchaseOrderEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithDeliveryPurchaseOrderEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithPurchaseOrderLineEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithCustomerActorVisitEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(runtimeTypes, "GetEntitiesWithOrderEntityId", returnType: null);
            RequireGeneratedIndexApi(runtimeTypes, "GetEntitiesWithOrderLineEntityId", returnType: null);
            RequireGeneratedIndexApi(runtimeTypes, "GetEntitiesWithDeliveryEntityId", returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithReservedOrderLineEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithTrolleyStoreEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithTrolleyPusherEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithTrolleyEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithConsultationOfferVisitEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithConsultationOfferEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithCustomerParkingSpotStoreEntityId",
                returnType: null);
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithCustomerQueueSpotStoreEntityId",
                returnType: null);
            string[] primaryCustomerFlowIndexMethods =
            {
                "GetEntityWithCustomerLoadingBayStoreEntityId",
                "GetEntityWithCustomerTrafficLaneStoreEntityId",
                "GetEntityWithReservedCustomerParkingSpotEntityId",
                "GetEntityWithReservedCustomerQueueSpotEntityId",
                "GetEntityWithReservedCustomerLoadingBayEntityId",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "GetEntityWithServingOrderCounterEntityId"
            };
            foreach (string methodName in primaryCustomerFlowIndexMethods)
                RequireGeneratedIndexApi(runtimeTypes, methodName, typeof(GameEntity));

            string combinedRuntimeSource = string.Join(
                Environment.NewLine,
                GetRuntimeSourcePaths().Select(File.ReadAllText));
            Require(!combinedRuntimeSource.Contains(
                    "new PrimaryEntityIndex<",
                    StringComparison.Ordinal),
                "Single-value primary indices must be generated by Jenny from " +
                "[PrimaryEntityIndex] component values, not registered manually.");
            Require(!Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\bCustomerVisitEntityId\b"),
                "Loaded products must reference their OrderLine only; the line already owns the " +
                "visit relation through OrderEntityId.");
            Require(Regex.IsMatch(combinedRuntimeSource, @"\.GetEntityWithCarrierEntityId\s*\("),
                $"Runtime carrying logic must consume the {nameof(CarrierEntityId)} primary index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithCustomerVisitStoreEntityId\s*\("),
                $"Runtime customer/order logic must consume the {nameof(CustomerVisitStoreEntityId)} " +
                "entity index.");
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
            Require(Regex.IsMatch(combinedRuntimeSource, @"\.GetEntitiesWithOrderEntityId\s*\("),
                $"Runtime order logic must consume the {nameof(OrderEntityId)} entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithOrderLineEntityId\s*\("),
                $"Runtime loading logic must consume the {nameof(OrderLineEntityId)} entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithDeliveryEntityId\s*\("),
                $"Runtime delivery logic must consume the {nameof(DeliveryEntityId)} entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithReservedOrderLineEntityId\s*\("),
                "Runtime pickup and loading logic must count outstanding order-line reservations.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithTrolleyStoreEntityId\s*\("),
                "Runtime purchase logic must consume the trolley store primary index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithTrolleyPusherEntityId\s*\("),
                "Runtime handling logic must consume the trolley pusher primary index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithTrolleyEntityId\s*\("),
                "Runtime cargo logic must consume the trolley cargo entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithConsultationOfferVisitEntityId\s*\("),
                $"Runtime consultation logic must consume the " +
                $"{nameof(ConsultationOfferVisitEntityId)} entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithConsultationOfferEntityId\s*\("),
                $"Runtime consultation logic must consume the " +
                $"{nameof(ConsultationOfferEntityId)} entity index.");
            Require(Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntitiesWithCustomerParkingSpotStoreEntityId\s*\(") &&
                    Regex.IsMatch(
                        combinedRuntimeSource,
                        @"\.GetEntitiesWithCustomerQueueSpotStoreEntityId\s*\(") &&
                    primaryCustomerFlowIndexMethods.All(methodName =>
                        Regex.IsMatch(
                            combinedRuntimeSource,
                            $@"\.{methodName}\s*\(")),
                "Runtime customer-flow logic must consume every generated parking, queue, " +
                "loading-bay and traffic-lane relation index.");

            Require(typeof(ICustomerVisitFactory).IsAssignableFrom(typeof(CustomerVisitFactory)),
                $"{nameof(CustomerVisitFactory)} must implement {nameof(ICustomerVisitFactory)}.");
            RequireMethod(
                typeof(ICustomerVisitFactory),
                nameof(ICustomerVisitFactory.Create),
                typeof(GameEntity),
                typeof(GameEntity),
                typeof(GameEntity),
                typeof(GameEntity));
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
                typeof(GameEntity),
                typeof(GameEntity));
            Require(typeof(ICustomerFlowFactory).IsAssignableFrom(
                        typeof(CustomerFlowFactory)),
                $"{nameof(CustomerFlowFactory)} must implement " +
                $"{nameof(ICustomerFlowFactory)}.");
            RequireMethod(
                typeof(ICustomerFlowFactory),
                nameof(ICustomerFlowFactory.Create),
                typeof(void),
                typeof(GameEntity),
                typeof(CustomerFlowSceneLayout));
            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<ICustomerFactory>().To<CustomerFactory>().AsSingle()",
                "Bind<ICustomerFlowFactory>().To<CustomerFlowFactory>().AsSingle()");

            string customerVisitFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "CustomerVisitFactory.cs");
            string customerFlowFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "CustomerFlowFactory.cs");
            RequireSourceContains(
                customerFlowFactorySource,
                "GetEntitiesWithCustomerParkingSpotStoreEntityId",
                "GetEntitiesWithCustomerQueueSpotStoreEntityId",
                "GetEntityWithCustomerLoadingBayStoreEntityId",
                "GetEntityWithCustomerTrafficLaneStoreEntityId",
                "AddParkingSpotIndex",
                "AddQueueSpotIndex",
                "AddCustomerVehicleArrivalRoute",
                "AddCustomerVehicleToLoadingRoute",
                "AddCustomerVehicleParkingDepartureRoute",
                "AddCustomerQueueAbandonRoute",
                "QueueAbandonExitRoute",
                "CreateQueueAbandonRouteSlice(abandonExitRoute, index)",
                "route.Length - queueSpotIndex",
                "AddCustomerApproachRoute",
                "AddCustomerReturnRoute",
                "AddCustomerLoadingDepartureRoute",
                "isCustomerParkingSpot = true",
                "isCustomerQueueSpot = true",
                "isCustomerLoadingBay = true",
                "isCustomerTrafficLane = true");
            RequireSourceContains(customerVisitFactorySource,
                "isCustomerVisit = true",
                "isCustomerVehicle = true",
                "isCustomerVisitArriving = true",
                "isRouteMover = true",
                "isLoadingZone = true",
                "AddCustomerVisitStoreEntityId",
                "AddCustomerArrivalSequence",
                "AddReservedCustomerParkingSpotEntityId",
                "AddReservedCustomerTrafficLaneEntityId",
                "AddCustomerProjectType",
                "AddCustomerPatienceRemaining(",
                "_consultationOffers.CreateOffers");
            Require(!customerVisitFactorySource.Contains(
                        "AddCustomerProjectTitle",
                        StringComparison.Ordinal) &&
                    !customerVisitFactorySource.Contains(
                        "AddCustomerRequest",
                        StringComparison.Ordinal),
                "Customer visits must keep semantic project identity instead of localized text.");
            Require(!customerVisitFactorySource.Contains(
                    "_orderFactory",
                    StringComparison.Ordinal),
                $"{nameof(CustomerVisitFactory)} must not create the order before consultation.");
            Require(!customerVisitFactorySource.Contains(
                        "customerVisit.AddProductType",
                        StringComparison.Ordinal) &&
                    !customerVisitFactorySource.Contains(
                        "customerVisit.AddRequiredProductCount",
                        StringComparison.Ordinal) &&
                    !customerVisitFactorySource.Contains(
                        "customerVisit.AddAvailableProductCount",
                        StringComparison.Ordinal) &&
                    !customerVisitFactorySource.Contains(
                        "customerVisit.AddLoadedProductCount",
                        StringComparison.Ordinal),
                "Pre-order customer visits must not contain single-SKU aggregate fields.");
            string orderFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "OrderFactory.cs");
            RequireSourceContains(orderFactorySource,
                "AddOrderComponents",
                "GetEntitiesWithConsultationOfferEntityId",
                "AddOrderEntityId",
                "AddLineIndex",
                "AddProductType",
                "AddRequiredProductCount",
                "AddAvailableProductCount",
                "AddLoadedProductCount(0)",
                "selectedOffer.OrderReward",
                "isOrder = true");
            Require(!orderFactorySource.Contains("customerVisit.AddProductType", StringComparison.Ordinal) &&
                    !orderFactorySource.Contains("customerVisit.AddRequiredProductCount", StringComparison.Ordinal) &&
                    !orderFactorySource.Contains("customerVisit.AddAvailableProductCount", StringComparison.Ordinal) &&
                    !orderFactorySource.Contains("customerVisit.AddLoadedProductCount", StringComparison.Ordinal),
                $"{nameof(OrderFactory)} must store per-SKU progress on OrderLine entities, not " +
                "aggregate it on the unified visit.");

            string consultationOfferFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "ConsultationOfferFactory.cs");
            RequireSourceContains(consultationOfferFactorySource,
                "CreateEntity.Empty",
                "AddConsultationOfferVisitEntityId",
                "AddConsultationOfferEntityId",
                "AddOfferIndex",
                "AddLineIndex",
                "AddProductType",
                "AddRequiredProductCount",
                "AddAvailableProductCount(0)",
                "AddExpectedProfit",
                "isConsultationOffer = true",
                "isConsultationOfferLine = true",
                "isSelectedConsultationOffer");
            Require(!consultationOfferFactorySource.Contains(
                        "AddOfferTitle",
                        StringComparison.Ordinal) &&
                    !consultationOfferFactorySource.Contains(
                        "AddOfferDescription",
                        StringComparison.Ordinal),
                "Consultation offers must keep semantic indices instead of localized text.");
            Require(!consultationOfferFactorySource.Contains(
                        "offer.AddProductType",
                        StringComparison.Ordinal) &&
                    !consultationOfferFactorySource.Contains(
                        "offer.AddRequiredProductCount",
                        StringComparison.Ordinal) &&
                    !consultationOfferFactorySource.Contains(
                        "offer.AddAvailableProductCount",
                        StringComparison.Ordinal) &&
                    !consultationOfferFactorySource.Contains(
                        "offer.AddLoadedProductCount",
                        StringComparison.Ordinal),
                "Consultation offer roots must not retain single-SKU aggregate fields.");

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
                "GameMatcher.OrderContentReleased",
                "GetEntitiesWithOrderEntityId",
                "RemoveCustomerVisitStoreEntityId",
                "isDestructed = true");

            string releaseOrderContentSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "ReleaseDepartedOrderContentSystem.cs");
            Require(runtimeTypes.Contains(typeof(ReleaseDepartedOrderContentSystem)),
                $"{nameof(ReleaseDepartedOrderContentSystem)} must remain part of Assembly-CSharp.");
            RequireSourceContains(releaseOrderContentSource,
                "GetEntitiesWithOrderEntityId",
                "GetEntitiesWithOrderLineEntityId",
                "RemoveOrderLineEntityId",
                "RemoveOrderEntityId",
                "isOrderContentReleased = true");
            int removeProductLineRelation = releaseOrderContentSource.IndexOf(
                "product.RemoveOrderLineEntityId()",
                StringComparison.Ordinal);
            int destructLoadedProduct = releaseOrderContentSource.IndexOf(
                "product.isDestructed = true",
                StringComparison.Ordinal);
            int removeOrderRelation = releaseOrderContentSource.IndexOf(
                "line.RemoveOrderEntityId()",
                StringComparison.Ordinal);
            int destructOrderLine = releaseOrderContentSource.IndexOf(
                "line.isDestructed = true",
                StringComparison.Ordinal);
            Require(removeProductLineRelation >= 0 &&
                    destructLoadedProduct > removeProductLineRelation &&
                    removeOrderRelation >= 0 &&
                    destructOrderLine > removeOrderRelation,
                "Departure cleanup must remove OrderLineEntityId and OrderEntityId before " +
                "marking loaded products and order lines Destructed.");

            string customerFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "CustomerFeature.cs");
            string releaseSystemToken =
                "Add(systems.Create<ReleaseDepartedOrderContentSystem>())";
            string completeDepartureToken =
                "Add(systems.Create<CompleteCustomerVehicleDepartureSystem>())";
            int releaseSystemIndex = customerFeatureSource.IndexOf(
                releaseSystemToken,
                StringComparison.Ordinal);
            int completeDepartureIndex = customerFeatureSource.IndexOf(
                completeDepartureToken,
                StringComparison.Ordinal);
            Require(releaseSystemIndex >= 0 && completeDepartureIndex > releaseSystemIndex,
                $"CustomerFeature must execute {nameof(ReleaseDepartedOrderContentSystem)} " +
                $"before {nameof(CompleteCustomerVehicleDepartureSystem)}.");

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

        private static void ValidateCustomerQueueArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] patienceComponents =
            {
                typeof(CustomerVisitAbandoning),
                typeof(CustomerVisitWaitingForAbandonDeparture),
                typeof(CustomerVisitAbandonDeparting),
                typeof(CustomerAbandonReturningToVehicle),
                typeof(CustomerPatienceWarningIssued),
                typeof(CustomerPatienceWarningEvent),
                typeof(CustomerAbandonedEvent),
                typeof(CustomerEventVisitEntityId),
                typeof(CustomerPatienceRemaining),
                typeof(CustomerDissatisfactionViewComponent)
            };
            foreach (Type component in patienceComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Customer patience requires the {component.Name} Game component.");
            }
            RequireMethod(
                typeof(CustomerDissatisfactionView),
                nameof(CustomerDissatisfactionView.Configure),
                typeof(void),
                typeof(Renderer[]),
                typeof(Transform),
                typeof(Transform),
                typeof(TextMesh));
            RequireMethod(
                typeof(CustomerDissatisfactionView),
                nameof(CustomerDissatisfactionView.SetDissatisfied),
                typeof(void),
                typeof(bool),
                typeof(string));
            Require(typeof(CustomerDissatisfactionView).GetProperty(
                        nameof(CustomerDissatisfactionView.IsDissatisfied))?.PropertyType ==
                    typeof(bool) &&
                    typeof(CustomerDissatisfactionView).GetProperty(
                        nameof(CustomerDissatisfactionView.Renderers))?.PropertyType ==
                    typeof(Renderer[]) &&
                    typeof(CustomerDissatisfactionView).GetProperty(
                        nameof(CustomerDissatisfactionView.LeftShoulder))?.PropertyType ==
                    typeof(Transform) &&
                    typeof(CustomerDissatisfactionView).GetProperty(
                        nameof(CustomerDissatisfactionView.RightShoulder))?.PropertyType ==
                    typeof(Transform) &&
                    typeof(CustomerDissatisfactionView).GetProperty(
                        nameof(CustomerDissatisfactionView.WorldLabel))?.PropertyType ==
                    typeof(TextMesh),
                "Customer dissatisfaction presentation must expose its exact read-only " +
                "prefab boundary.");
            Require(typeof(EntityComponentRegistrar).IsAssignableFrom(
                        typeof(CustomerDissatisfactionViewRegistrar)),
                $"{nameof(CustomerDissatisfactionViewRegistrar)} must register the generic " +
                "customer mood view component.");
            Require(typeof(IExecuteSystem).IsAssignableFrom(
                        typeof(PresentCustomerDissatisfactionSystem)),
                $"{nameof(PresentCustomerDissatisfactionSystem)} must remain an executable " +
                "presentation system.");

            string[] systemNames =
            {
                "TickCustomerCooldownSystem",
                "FinalizeAcceptedCustomerPatienceSystem",
                "TickCustomerPatienceSystem",
                "BeginCustomerAbandonmentSystem",
                "BeginCustomerVehicleDepartureDelaySystem",
                "TickCustomerVehicleDepartureDelaySystem",
                "ReserveCustomerLoadingBaySystem",
                "BeginCustomerVehicleDepartureSystem",
                "BeginCustomerAbandonDepartureSystem",
                "BeginCustomerReturnSystem",
                "AdvanceCustomerQueueSystem",
                "MoveCustomerVehicleToLoadingBaySystem",
                "SpawnCustomerVisitSystem",
                "MoveRouteSystem",
                "ReleaseDepartedOrderContentSystem",
                "CompleteCustomerVehicleDepartureSystem",
                "CompleteCustomerAbandonDepartureSystem",
                "CompleteCustomerLoadingBayArrivalSystem",
                "CompleteCustomerVehicleArrivalSystem",
                "CompleteCustomerReturnSystem",
                "CompleteCustomerAbandonReturnSystem",
                "CompleteCustomerApproachSystem",
                "PromoteCustomerAtCounterSystem",
                "ValidateCustomerFlowStateSystem"
            };
            foreach (string systemName in systemNames)
            {
                Type systemType = runtimeTypes.SingleOrDefault(type =>
                    type.Name == systemName);
                Require(systemType != null &&
                        typeof(IExecuteSystem).IsAssignableFrom(systemType),
                    $"Customer queue requires executable {systemName}.");
            }

            Type featureType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "CustomerFeature");
            Require(featureType != null && typeof(Feature).IsAssignableFrom(featureType),
                "CustomerFeature must remain an explicit Entitas feature.");
            string featureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "CustomerFeature.cs");
            RequireExactFeatureOrder(featureSource, systemNames, "CustomerFeature");

            string spawnSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "SpawnCustomerVisitSystem.cs");
            RequireSourceContains(spawnSource,
                "GameMatcher.StoreOpen",
                "GetEntitiesWithCustomerParkingSpotStoreEntityId",
                "GetEntityWithReservedCustomerParkingSpotEntityId",
                "GetEntityWithCustomerTrafficLaneStoreEntityId",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "float nextDelay = _arrivalSchedule.GetDelay(store.CurrentDayMinute)",
                "_customerVisitFactory.Create(store, parkingSpot, trafficLane)",
                "store.ReplaceCustomerCooldownRemaining(nextDelay)");
            RequireSourceOrder(
                spawnSource,
                "_customerVisitFactory.Create(store, parkingSpot, trafficLane)",
                "store.ReplaceCustomerCooldownRemaining(nextDelay)",
                "A due customer attempt must reschedule after either spawning or finding " +
                "customer infrastructure occupied.");

            string tickPatienceSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "TickCustomerPatienceSystem.cs");
            string finalizePatienceSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "FinalizeAcceptedCustomerPatienceSystem.cs");
            string beginAbandonmentSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "BeginCustomerAbandonmentSystem.cs");
            string completeAbandonReturnSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerAbandonReturnSystem.cs");
            string beginAbandonDepartureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "BeginCustomerAbandonDepartureSystem.cs");
            string completeAbandonDepartureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerAbandonDepartureSystem.cs");
            RequireSourceContains(tickPatienceSource,
                "GameMatcher.CustomerPatienceRemaining",
                "GameMatcher.CustomerVisitQueued",
                "GameMatcher.CustomerVisitConsulting",
                "GameMatcher.ModalOpen",
                "GameMatcher.ConsultationVisitEntityId",
                "Math.Max(0f, previous - deltaTime)",
                "_events.EmitCustomerPatienceWarning(visit.EntityId)");
            RequireSourceContains(finalizePatienceSource,
                "GameMatcher.Order",
                "visit.isCustomerVisitReturning",
                "visit.RemoveCustomerPatienceRemaining()",
                "visit.isCustomerPatienceWarningIssued = false");
            RequireSourceContains(beginAbandonmentSource,
                "remaining > 0f || HasActiveConsultationModal(visit)",
                "CustomerArrivalSequence",
                "queueSpot.hasCustomerQueueAbandonRoute",
                "CreateAbandonReturnRoute(actor, queueSpot)",
                "Pose[] queueExit = queueSpot.CustomerQueueAbandonRoute",
                "Array.Copy(queueExit, 0, route, 1, queueExit.Length)",
                "customerReturn.Length - 2",
                "RequireMatchingJoin(",
                "actor.RemoveReservedCustomerQueueSpotEntityId()",
                "actor.isCustomerAbandonReturningToVehicle = true",
                "visit.isCustomerVisitAbandoning = true",
                "visit.RemoveServingOrderCounterEntityId()",
                "visit.RemoveCustomerPatienceRemaining()",
                "store.ReplaceDayLostCustomerCount(nextLostCount)",
                "_events.EmitCustomerAbandoned(visit.EntityId)");
            Require(!beginAbandonmentSource.Contains("ExitCorridor", StringComparison.Ordinal) &&
                    !beginAbandonmentSource.Contains("new Vector3(", StringComparison.Ordinal),
                "Customer abandonment must follow the authored queue-exit component without " +
                "hard-coded world-space corridor coordinates.");
            RequireSourceContains(completeAbandonReturnSource,
                "GameMatcher.CustomerAbandonReturningToVehicle",
                "customer.RemoveCustomerActorVisitEntityId()",
                "customer.isDestructed = true",
                "visit.isCustomerVisitWaitingForAbandonDeparture = true");
            RequireSourceContains(beginAbandonDepartureSource,
                "GameMatcher.CustomerVisitWaitingForAbandonDeparture",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "CustomerVehicleParkingDepartureRoute",
                "AddReservedCustomerTrafficLaneEntityId",
                "isCustomerVisitAbandonDeparting = true");
            RequireSourceContains(completeAbandonDepartureSource,
                "GameMatcher.CustomerVisitAbandonDeparting",
                "RemoveCustomerVisitStoreEntityId",
                "RemoveReservedCustomerParkingSpotEntityId",
                "RemoveReservedCustomerTrafficLaneEntityId",
                "isDestructed = true");
            RequireSourceOrder(
                featureSource,
                "Create<FinalizeAcceptedCustomerPatienceSystem>()",
                "Create<TickCustomerPatienceSystem>()",
                "Accepted consultations must finalize before waiting-customer patience ticks.");
            RequireSourceOrder(
                featureSource,
                "Create<TickCustomerPatienceSystem>()",
                "Create<BeginCustomerAbandonmentSystem>()",
                "Patience must reach zero before abandonment is resolved in the same frame.");
            RequireSourceOrder(
                featureSource,
                "Create<BeginCustomerAbandonmentSystem>()",
                "Create<AdvanceCustomerQueueSystem>()",
                "Expired customers must release queue ownership before FIFO compaction.");

            string completeParkingArrivalSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerVehicleArrivalSystem.cs");
            RequireSourceContains(completeParkingArrivalSource,
                "GetEntitiesWithCustomerQueueSpotStoreEntityId",
                "GetEntityWithReservedCustomerQueueSpotEntityId",
                "occupiedCount",
                "isCustomerVisitQueued = true",
                "_customerFactory.Create(visit, parkingSpot, queueSpot)");

            string advanceQueueSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "AdvanceCustomerQueueSystem.cs");
            RequireSourceContains(advanceQueueSource,
                "GetEntitiesWithCustomerVisitStoreEntityId",
                "CustomerArrivalSequence.CompareTo",
                "QueueSpotIndex != index",
                "currentIndex < expectedIndex",
                "RemoveReservedCustomerQueueSpotEntityId",
                "AddReservedCustomerQueueSpotEntityId",
                "BeginMove(actor, targetSpot)");

            string promoteSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "PromoteCustomerAtCounterSystem.cs");
            RequireSourceContains(promoteSource,
                "queueSpot.QueueSpotIndex != 0",
                "GetEntityWithServingOrderCounterEntityId",
                "isCustomerVisitQueued = false",
                "isCustomerVisitConsulting = true",
                "AddServingOrderCounterEntityId",
                "isCustomerWaitingAtCounter = true");

            string reserveBaySource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "ReserveCustomerLoadingBaySystem.cs");
            RequireSourceContains(reserveBaySource,
                "GetEntityWithReservedCustomerLoadingBayEntityId",
                "GetEntitiesWithCustomerVisitStoreEntityId",
                "isCustomerVisitReturning",
                "isCustomerVisitWaitingForLoadingBay",
                "CustomerArrivalSequence.CompareTo",
                "AddReservedCustomerLoadingBayEntityId");

            string beginReturnSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "BeginCustomerReturnSystem.cs");
            string completeReturnSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerReturnSystem.cs");
            RequireSourceContains(beginReturnSource,
                "GameMatcher.CustomerVisitReturning",
                "customer.hasReservedCustomerQueueSpotEntityId",
                "CustomerReturnRoute",
                "customer != null && customer.isCustomerReturningToVehicle",
                "customer.hasRoute && customer.hasRouteWaypointIndex",
                "!customer.isRouteCompleted && !customer.hasCustomerReturnRoute",
                "!customer.hasReservedCustomerQueueSpotEntityId)",
                "if (customer == null || !customer.isCustomer || !customer.isRouteMover",
                "!customer.isCustomerWaitingAtCounter",
                "!customer.hasCustomerReturnRoute",
                "customer.hasRoute ||",
                "customer.hasRouteWaypointIndex || customer.isRouteCompleted",
                "customer.hasReservedCustomerQueueSpotEntityId || customer.isDestructed",
                "cannot begin its accepted return",
                "isCustomerReturningToVehicle = true");
            RequireSourceOrder(
                beginReturnSource,
                "customer != null && customer.isCustomerReturningToVehicle",
                "return;",
                "An already valid in-progress customer return must be an idempotent no-op.");
            RequireSourceOrder(
                beginReturnSource,
                "return;",
                "if (customer == null || !customer.isCustomer || !customer.isRouteMover",
                "Only the exact in-progress return state may bypass strict start validation.");
            RequireSourceOrder(
                beginReturnSource,
                "if (customer == null || !customer.isCustomer || !customer.isRouteMover",
                "cannot begin its accepted return",
                "Broken or partially transitioned return states must fall through to the " +
                "strict fail-fast validation.");
            RequireSourceOrder(
                beginReturnSource,
                "customer.isCustomerWaitingAtCounter = false",
                "customer.isCustomerReturningToVehicle = true",
                "A fresh accepted return must leave the counter before entering route motion.");
            RequireSourceOrder(
                beginReturnSource,
                "customer.AddRoute((Pose[])returnRoute.Clone())",
                "customer.RemoveCustomerReturnRoute()",
                "A fresh return must publish its cloned active route before consuming the " +
                "one-shot authored return route.");
            RequireSourceContains(completeReturnSource,
                "GameMatcher.CustomerReturningToVehicle",
                "RemoveCustomerActorVisitEntityId",
                "isDestructed = true",
                "isCustomerVisitReturning = false",
                "isCustomerVisitWaitingForLoadingBay = true");

            string moveToBaySource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "MoveCustomerVehicleToLoadingBaySystem.cs");
            string completeBayArrivalSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerLoadingBayArrivalSystem.cs");
            RequireSourceContains(moveToBaySource,
                "GameMatcher.CustomerVisitWaitingForLoadingBay",
                "GameMatcher.ReservedCustomerLoadingBayEntityId",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "CustomerVehicleToLoadingRoute",
                "AddReservedCustomerTrafficLaneEntityId",
                "isCustomerVisitMovingToLoadingBay = true");
            RequireSourceContains(completeBayArrivalSource,
                "GameMatcher.CustomerVisitMovingToLoadingBay",
                "RemoveReservedCustomerTrafficLaneEntityId",
                "RemoveReservedCustomerParkingSpotEntityId",
                "isCustomerVisitLoading = true",
                "isInteractable = true");

            string beginDepartureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "BeginCustomerVehicleDepartureSystem.cs");
            string completeDepartureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerVehicleDepartureSystem.cs");
            RequireSourceContains(beginDepartureSource,
                "GameMatcher.CustomerVisitCompleted",
                "GameMatcher.CustomerDepartureDelayRemaining",
                "GetEntityWithReservedCustomerTrafficLaneEntityId",
                "CustomerLoadingDepartureRoute",
                "AddReservedCustomerTrafficLaneEntityId",
                "isCustomerVisitDeparting = true");
            RequireSourceContains(completeDepartureSource,
                "GameMatcher.OrderContentReleased",
                "GetEntitiesWithOrderEntityId",
                "RemoveCustomerVisitStoreEntityId",
                "RemoveReservedCustomerLoadingBayEntityId",
                "RemoveReservedCustomerTrafficLaneEntityId",
                "isDestructed = true");

            string validationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "ValidateCustomerFlowStateSystem.cs");
            RequireSourceContains(validationSource,
                "GetEntitiesWithCustomerParkingSpotStoreEntityId",
                "GetEntitiesWithCustomerQueueSpotStoreEntityId",
                "parkingCount != _config.ParkingCapacity",
                "lifecycleCount != 1",
                "arrivalSequences.Add",
                "ValidateParkingRelation",
                "ValidateBayRelation",
                "ValidateLaneRelation",
                "ValidateCounterRelation",
                "ValidateQueueSpots",
                "spot.hasCustomerQueueAbandonRoute",
                "_config.ParkingCapacity - index + 1",
                "previousRoute[routeIndex + 1]",
                "ValidateFifoQueue",
                "spot.QueueSpotIndex != expectedIndex");

            string storeDayValidationSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "ValidateStoreDayStateSystem.cs");
            string openReportSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "OpenDayReportSystem.cs");
            RequireSourceContains(storeDayValidationSource,
                "StoreDayCustomerVisitGuard.CountActiveVisits",
                "GetEntitiesWithCustomerVisitStoreEntityId",
                "lifecycleCount != 1");
            RequireSourceContains(openReportSource,
                "StoreDayCustomerVisitGuard.CountActiveVisits(",
                "store.EntityId) != 0");

            string combinedRuntimeSource = string.Join(
                Environment.NewLine,
                GetRuntimeSourcePaths().Select(File.ReadAllText));
            Require(!Regex.IsMatch(
                    combinedRuntimeSource,
                    @"\.GetEntityWithCustomerVisitStoreEntityId\s*\("),
                "Customer visits are one-to-many per store; runtime source must use the plural " +
                "CustomerVisitStoreEntityId index.");
            Require(!Regex.IsMatch(
                    combinedRuntimeSource,
                    @"SceneRouteId\.Customer|\.GetRoute\s*\("),
                "Runtime customer flow must consume typed parking, queue and loading layout " +
                "resources instead of legacy generic customer routes.");

            ValidateImmutableSnapshotType(typeof(CustomerFlowSnapshot));
            ValidateSnapshotProperties(
                typeof(CustomerFlowSnapshot),
                (nameof(CustomerFlowSnapshot.TotalActiveCount), typeof(int)),
                (nameof(CustomerFlowSnapshot.ArrivingCount), typeof(int)),
                (nameof(CustomerFlowSnapshot.QueuedCount), typeof(int)),
                (nameof(CustomerFlowSnapshot.ConsultingCount), typeof(int)),
                (nameof(CustomerFlowSnapshot.LoadingPipelineCount), typeof(int)),
                (nameof(CustomerFlowSnapshot.LeavingCount), typeof(int)));
            Require(typeof(CustomerFlowSnapshot).GetConstructor(new[]
                    {
                        typeof(int), typeof(int), typeof(int), typeof(int),
                        typeof(int), typeof(int)
                    }) != null &&
                    typeof(HudSnapshot).GetProperty(nameof(HudSnapshot.CustomerFlow))
                        ?.PropertyType == typeof(CustomerFlowSnapshot),
                "HUD must expose one immutable aggregate snapshot for the full customer queue.");
            string presentHudSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentHudSystem.cs");
            string hudViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", "PrototypeHudView.cs");
            RequireSourceContains(presentHudSource,
                "GetEntitiesWithCustomerVisitStoreEntityId",
                "CreateCustomerFlowSnapshot",
                "isCustomerVisitQueued",
                "isCustomerVisitWaitingForLoadingBay",
                "new CustomerFlowSnapshot(");
            RequireSourceContains(hudViewSource,
                "LocalizationKey.HudCustomerFlow",
                "_snapshot.CustomerFlow.TotalActiveCount",
                "_snapshot.CustomerFlow.QueuedCount",
                "_snapshot.CustomerFlow.LoadingPipelineCount",
                "_snapshot.CustomerFlow.LeavingCount");
            Require(!presentHudSource.Contains(
                        "minimumWaitingPatience",
                        StringComparison.Ordinal) &&
                    !hudViewSource.Contains(
                        "HudCustomerFlowWithPatience",
                        StringComparison.Ordinal) &&
                    !hudViewSource.Contains(
                        "MinimumWaitingPatienceSeconds",
                        StringComparison.Ordinal),
                "HUD presentation must not expose numeric customer patience countdowns.");

            string presentationFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "PresentationFeature.cs");
            string patienceEventsSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentCustomerPatienceEventsSystem.cs");
            string dissatisfactionViewSource = ReadRuntimeSource(
                "Gameplay", "Views", "CustomerDissatisfactionView.cs");
            string dissatisfactionRegistrarSource = ReadRuntimeSource(
                "Gameplay", "Registrars", "CustomerDissatisfactionViewRegistrar.cs");
            string dissatisfactionSystemSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentCustomerDissatisfactionSystem.cs");
            RequireSourceContains(patienceEventsSource,
                "GameMatcher.CustomerPatienceWarningEvent",
                "GameMatcher.CustomerAbandonedEvent",
                "GameMatcher.CustomerEventVisitEntityId",
                "_gameContext.GetEntityWithEntityId(",
                "visit.CustomerPatienceRemaining",
                "LocalizationKey.NotificationCustomerPatienceLow",
                "LocalizationKey.NotificationCustomerLeftImpatient",
                "_notifications.Show(",
                "warningEvent.Destroy()",
                "abandonedEvent.Destroy()");
            Require(!patienceEventsSource.Contains("Math.Ceiling", StringComparison.Ordinal),
                "Customer patience notifications must communicate mood without a numeric " +
                "countdown.");
            RequireSourceContains(dissatisfactionViewSource,
                "DissatisfiedColorBlend = 0.55f",
                "RaisedArmAngle = 145f",
                "ArmWaveAmplitude = 12f",
                "ArmWaveAngularSpeed = 7f",
                "Color.Lerp(",
                "Color.red",
                "SetPropertyBlock(propertyBlock, materialIndex)",
                "Time.unscaledTime * ArmWaveAngularSpeed",
                "-RaisedArmAngle + waveAngle",
                "RaisedArmAngle - waveAngle",
                "Camera.main",
                "FaceWorldLabelTowardCamera()",
                "Quaternion.LookRotation(",
                "RestoreWorldLabelRotation()",
                "_worldLabel.gameObject.SetActive(true)",
                "_worldLabel.gameObject.SetActive(false)",
                "RestoreArmPose()");
            Require(!dissatisfactionViewSource.Contains(
                        ".material",
                        StringComparison.Ordinal),
                "Customer mood tinting must use cached material property blocks without " +
                "instantiating renderer materials.");
            RequireSourceContains(dissatisfactionRegistrarSource,
                "Entity.AddCustomerDissatisfactionView(",
                "GetComponent<CustomerDissatisfactionView>()",
                "Entity.RemoveCustomerDissatisfactionView()");
            RequireSourceContains(dissatisfactionSystemSource,
                "GameMatcher.CustomerActorVisitEntityId",
                "GameMatcher.CustomerDissatisfactionView",
                "LocalizationKey.WorldCustomerDissatisfied",
                "visit.isCustomerVisitQueued || visit.isCustomerVisitConsulting",
                "visit.isCustomerVisitReturning",
                "visit.isCustomerVisitAbandoning",
                "waiting && visit.isCustomerPatienceWarningIssued",
                "view.SetDissatisfied(dissatisfied, _worldLabel)");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentInteractionHighlightsSystem>()",
                "Create<PresentCustomerDissatisfactionSystem>()",
                "Customer mood must update after world highlights.");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentCustomerDissatisfactionSystem>()",
                "Create<PresentHudSystem>()",
                "Customer mood must update before the HUD snapshot is presented.");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentCustomerDissatisfactionSystem>()",
                "Create<PresentCustomerPatienceEventsSystem>()",
                "Customer mood must become visible before patience notifications are consumed.");
            RequireSourceOrder(
                presentationFeatureSource,
                "Create<PresentCustomerPatienceEventsSystem>()",
                "Create<PresentNotificationsSystem>()",
                "Customer patience events must reach the direct HUD notification bridge " +
                "before queued generic notifications.");
        }

        private static void ValidateContextAwareInteractionFocus()
        {
            Require(typeof(IInteractionPhysicsService).IsAssignableFrom(
                    typeof(InteractionPhysicsService)),
                $"{nameof(InteractionPhysicsService)} must implement " +
                $"{nameof(IInteractionPhysicsService)}.");
            RequireMethod(
                typeof(IInteractionPhysicsService),
                nameof(IInteractionPhysicsService.GetFocusCandidates),
                typeof(int),
                typeof(Camera),
                typeof(float),
                typeof(float),
                typeof(InteractionFocusCandidate[]));

            Type candidateType = typeof(InteractionFocusCandidate);
            Require(candidateType.IsValueType && candidateType.IsPublic,
                $"{nameof(InteractionFocusCandidate)} must remain a public value type.");
            Require(candidateType.GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(float),
                        typeof(bool)
                    }) != null &&
                    candidateType.GetProperty(nameof(InteractionFocusCandidate.EntityId))
                        ?.PropertyType == typeof(int) &&
                    candidateType.GetProperty(nameof(InteractionFocusCandidate.Score))
                        ?.PropertyType == typeof(float) &&
                    candidateType.GetProperty(nameof(InteractionFocusCandidate.IsDirect))
                        ?.PropertyType == typeof(bool),
                $"{nameof(InteractionFocusCandidate)} must expose immutable entity, score and " +
                "direct-hit data.");
            Type proxyMarkerType = typeof(NonOccludingInteractionProxy);
            Require(proxyMarkerType.IsSealed &&
                    typeof(MonoBehaviour).IsAssignableFrom(proxyMarkerType) &&
                    proxyMarkerType.GetCustomAttribute<DisallowMultipleComponent>() != null,
                $"{nameof(NonOccludingInteractionProxy)} must remain one sealed, non-repeatable " +
                "presentation marker.");

            string physicsSource = ReadRuntimeSource(
                "Gameplay", "Common", "Physics", nameof(InteractionPhysicsService) + ".cs");
            RequireSourceContains(physicsSource,
                "private const int MaxPhysicsHits = 128",
                "new RaycastHit[MaxPhysicsHits]",
                "RaycastNonAlloc(",
                "SphereCastNonAlloc(",
                "UnityEngine.Physics.DefaultRaycastLayers",
                "QueryTriggerInteraction.Collide",
                "nearestBlockerDistance",
                "HasLineOfSight(",
                "private static Vector3 ResolveLineOfSightTargetPoint(Vector3 origin,",
                "Vector3 targetPoint = ResolveLineOfSightTargetPoint(origin, candidateHit)",
                "candidateHit.distance <= OcclusionTolerance || targetPoint == Vector3.zero",
                "targetPoint = candidateHit.collider.ClosestPoint(origin)",
                "(targetPoint - origin).sqrMagnitude <= Mathf.Epsilon",
                "targetPoint = candidateHit.collider.bounds.center",
                "BlocksDirectFocus(",
                "BlocksAssistedFocus(",
                "IsNonOccludingInteractionProxy(",
                "hitCollider.isTrigger",
                "GetComponentInParent<NonOccludingInteractionProxy>()",
                "hitEntityId != candidateEntityId",
                "AddOrImproveCandidate(",
                "candidateCount == candidates.Length",
                "Interaction focus candidate buffer saturated",
                "EnsureQueryDidNotSaturate(",
                "hitCount == hits.Length");
            RequireSourceOrder(
                physicsSource,
                "targetPoint = candidateHit.collider.ClosestPoint(origin)",
                "targetPoint = candidateHit.collider.bounds.center",
                "Initial-overlap LOS must try Collider.ClosestPoint before its bounds-center " +
                "fallback.");
            Require(!physicsSource.Contains("RaycastAll(", StringComparison.Ordinal) &&
                    !physicsSource.Contains("SphereCastAll(", StringComparison.Ordinal) &&
                    !physicsSource.Contains("Physics.Raycast(ray, out", StringComparison.Ordinal),
                "Interaction focus must use bounded non-alloc physics queries only.");

            string detectionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "DetectFocusedInteractableSystem.cs");
            RequireSourceContains(detectionSource,
                "private const int MaxFocusCandidates = 64",
                "new InteractionFocusCandidate[MaxFocusCandidates]",
                "_physics.GetFocusCandidates(",
                "player.isHandsOccupied && player.isCarryingProduct",
                "GetPlayerStorageZoneEntityId(player)",
                "target.isInStock",
                "target.hasStorageSlotIndex",
                "target.hasStorageZoneEntityId",
                "target.StorageZoneEntityId == storageZoneEntityId",
                "InteractionFocusCandidate storageProxy",
                "target.isStorageZone || target.isLoadingZone",
                "return target.isProduct ? 0 : isDropTarget ? 1 : 0",
                "return target.isProduct ? 2 : 1",
                "candidate.IsDirect != bestIsDirect",
                "candidate.EntityId < bestEntityId");
        }

        private static void ValidateProductRecoveryArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            foreach (Type componentType in new[]
                     {
                         typeof(ReservedDeliverySlotIndex),
                         typeof(ReservedStorageSlotIndex),
                         typeof(ReservedOrderLineEntityId)
                     })
            {
                Require(discoveredComponents.Contains(componentType),
                    $"Product recovery requires the {componentType.Name} Game component.");
            }

            Type recoveryFeatureType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "ProductRecoveryFeature");
            Type recoverySystemType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "RecoverLostLooseProductsSystem");
            Require(recoveryFeatureType != null &&
                    typeof(Feature).IsAssignableFrom(recoveryFeatureType),
                "ProductRecoveryFeature must remain an Entitas feature.");
            Require(recoverySystemType != null &&
                    typeof(IExecuteSystem).IsAssignableFrom(recoverySystemType),
                "RecoverLostLooseProductsSystem must remain an executable Entitas system.");

            string configSource = ReadRuntimeSource(
                "Gameplay", "Configs", "ProductRecoveryConfig.cs");
            RequireSourceContains(configSource,
                "private float _minimumWorldY = -10f",
                "public float MinimumWorldY => _minimumWorldY",
                "ConfigValidation.RequireNegative");

            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", "StaticDataService.cs");
            RequireSourceContains(staticDataSource,
                "Load<ProductRecoveryConfig>(nameof(ProductRecoveryConfig))",
                "productRecovery.Validate()",
                "ProductRecovery = productRecovery");

            string recoveryFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "ProductRecoveryFeature.cs");
            const string recoverySystemToken =
                "Add(systems.Create<RecoverLostLooseProductsSystem>())";
            Require(CountOccurrences(recoveryFeatureSource, recoverySystemToken) == 1,
                "ProductRecoveryFeature must own exactly one lost-product recovery system.");

            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<BindViewFeature>()",
                "Create<StoreSceneBindingsFeature>()",
                "Generic view binding must precede validation of static trolley scene bindings.");
            int playerFeaturePosition = storeFeatureSource.IndexOf(
                "Create<PlayerFeature>()", StringComparison.Ordinal);
            int recoveryFeaturePosition = storeFeatureSource.IndexOf(
                "Create<ProductRecoveryFeature>()", StringComparison.Ordinal);
            int interactionFeaturePosition = storeFeatureSource.IndexOf(
                "Create<InteractionFeature>()", StringComparison.Ordinal);
            Require(playerFeaturePosition >= 0 &&
                    recoveryFeaturePosition > playerFeaturePosition &&
                    interactionFeaturePosition > recoveryFeaturePosition &&
                    CountOccurrences(storeFeatureSource, "Create<ProductRecoveryFeature>()") == 1,
                "StoreFeature must recover lost products once after player state and before " +
                "world interaction is resolved.");

            string recoverySource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "Systems",
                "RecoverLostLooseProductsSystem.cs");
            RequireSourceContains(recoverySource,
                "_minimumWorldY = staticData.ProductRecovery.MinimumWorldY",
                "product.WorldPosition.y < _minimumWorldY",
                "CollectDeliveryReservations()",
                "CollectStorageReservations()",
                "GetEntitiesWithDeliveryEntityId(delivery.EntityId)",
                "ValidateReservedSlot(",
                "int slotIndex = product.ReservedDeliverySlotIndex",
                "product.RemoveReservedDeliverySlotIndex()",
                "product.AddDeliverySlotIndex(slotIndex)",
                "int slotIndex = product.ReservedStorageSlotIndex",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddStorageSlotIndex(slotIndex)",
                "LocalizationKey.NotificationProductsRecovered",
                "recoveredCount");
            Require(!recoverySource.Contains("FindFreeSlot", StringComparison.Ordinal) &&
                    !recoverySource.Contains("FirstFree", StringComparison.Ordinal),
                "Lost-product recovery must restore the exact reserved slot and must never " +
                "fall back to an arbitrary free slot.");

            string pickupSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "PickUpProductSystem.cs");
            RequireSourceContains(pickupSource,
                "GetEntitiesWithReservedOrderLineEntityId(orderLine.EntityId)",
                "matchingLinkedProductCount + reservedProductCount",
                "CountLinkedProducts(matchingLine)",
                "product.RemoveDeliverySlotIndex()",
                "product.AddReservedDeliverySlotIndex(slotIndex)",
                "product.RemoveStorageSlotIndex()",
                "product.AddReservedStorageSlotIndex(slotIndex)",
                "product.AddReservedOrderLineEntityId(orderLine.EntityId)");
            RequireSourceOrder(
                pickupSource,
                "product.RemoveDeliverySlotIndex()",
                "product.AddReservedDeliverySlotIndex(slotIndex)",
                "Inbound pickup must atomically convert its active delivery slot to a reservation.");
            RequireSourceOrder(
                pickupSource,
                "product.RemoveStorageSlotIndex()",
                "product.AddReservedStorageSlotIndex(slotIndex)",
                "Stock pickup must atomically convert its active storage slot to a reservation.");

            string loadSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "LoadHeldProductSystem.cs");
            RequireSourceContains(loadSource,
                "product.ReservedOrderLineEntityId",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddOrderLineEntityId(orderLine.EntityId)");
            RequireSourceOrder(
                loadSource,
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddOrderLineEntityId(orderLine.EntityId)",
                "Loading must convert the reserved order-line relation to the permanent relation.");

            string storageSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "StoreInboundProductSystem.cs");
            RequireSourceContains(storageSource,
                "ReturnStockProduct(",
                "int slotIndex = product.ReservedStorageSlotIndex",
                "reservationOwner != product.EntityId",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddStorageSlotIndex(slotIndex)");
            int returnStockStart = storageSource.IndexOf(
                "private void ReturnStockProduct(", StringComparison.Ordinal);
            int collectSlotsStart = storageSource.IndexOf(
                "private Dictionary<int, Dictionary<int, int>> CollectOccupiedSlots()",
                StringComparison.Ordinal);
            Require(returnStockStart >= 0 && collectSlotsStart > returnStockStart &&
                    !storageSource[returnStockStart..collectSlotsStart]
                        .Contains("FindFreeSlot", StringComparison.Ordinal),
                "Returning held stock must use ReservedStorageSlotIndex exactly, without a " +
                "first-free fallback.");

            string dropSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "DropHeldProductSystem.cs");
            RequireSourceContains(dropSource,
                "product.hasReservedDeliverySlotIndex",
                "product.hasReservedStorageSlotIndex",
                "product.hasReservedOrderLineEntityId");

            string occupiedSlotsSource = ReadRuntimeSource(
                "Gameplay", "Features", "StorageState", "Systems",
                "RefreshStorageOccupiedSlotCountSystem.cs");
            RequireSourceContains(occupiedSlotsSource,
                "GameMatcher.ReservedStorageSlotIndex",
                "GameMatcher.ReservedOrderLineEntityId",
                "product.hasStorageSlotIndex == product.hasReservedStorageSlotIndex",
                "product.ReservedStorageSlotIndex",
                "is occupied or ",
                "reserved more than once.");

            string productPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveProductPromptSystem.cs");
            RequireSourceContains(productPromptSource,
                "GetEntitiesWithReservedOrderLineEntityId(orderLine.EntityId)",
                "matchingLine.LoadedProductCount + reservedProductCount",
                "LocalizationKey.PromptOrderLineAlreadyLoaded");

            string heldStoragePromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveHeldProductStoragePromptSystem.cs");
            RequireSourceContains(heldStoragePromptSource,
                "LocalizationKey.PromptReturnStockProduct",
                "LocalizedTexts.ProductName(heldProduct.ProductType)");
        }

        private static void ValidateCollisionSafeProductDrop(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            Require(componentTypes.Contains(typeof(ProductDropCollisionRadius)),
                $"Collision-safe product dropping requires the " +
                $"{nameof(ProductDropCollisionRadius)} Game component.");

            PropertyInfo radiusProperty = typeof(GameEntity).GetProperty(
                nameof(ProductConfig.ProductDropCollisionRadius),
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo hasRadiusProperty = typeof(GameEntity).GetProperty(
                "hasProductDropCollisionRadius",
                BindingFlags.Instance | BindingFlags.Public);
            Require(radiusProperty?.PropertyType == typeof(float) &&
                    hasRadiusProperty?.PropertyType == typeof(bool),
                "Jenny must generate float ProductDropCollisionRadius and its presence API.");
            RequireMethod(
                typeof(GameEntity),
                "AddProductDropCollisionRadius",
                typeof(GameEntity),
                typeof(float));
            RequireMethod(
                typeof(GameEntity),
                "ReplaceProductDropCollisionRadius",
                typeof(GameEntity),
                typeof(float));
            RequireMethod(
                typeof(GameEntity),
                "RemoveProductDropCollisionRadius",
                typeof(GameEntity));
            PropertyInfo matcherProperty = typeof(GameMatcher).GetProperty(
                nameof(ProductConfig.ProductDropCollisionRadius),
                BindingFlags.Static | BindingFlags.Public);
            Require(matcherProperty != null &&
                    typeof(IMatcher<GameEntity>).IsAssignableFrom(
                        matcherProperty.PropertyType),
                "Jenny must generate GameMatcher.ProductDropCollisionRadius.");

            string productConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(ProductConfig) + ".cs");
            RequireSourceContains(productConfigSource,
                "private float _productDropCollisionRadius",
                "public float ProductDropCollisionRadius => _productDropCollisionRadius",
                "ConfigValidation.RequirePositive(",
                "nameof(ProductDropCollisionRadius)",
                "_productDropCollisionRadius > _dropForwardDistance",
                "ProductDropCollisionRadius must not exceed DropForwardDistance");
            string productFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "ProductFactory.cs");
            RequireSourceContains(productFactorySource,
                "AddProductDropCollisionRadius(config.ProductDropCollisionRadius)");

            Require(typeof(IProductDropPhysicsService).IsAssignableFrom(
                    typeof(ProductDropPhysicsService)),
                $"{nameof(ProductDropPhysicsService)} must implement " +
                $"{nameof(IProductDropPhysicsService)}.");
            RequireMethod(
                typeof(IProductDropPhysicsService),
                nameof(IProductDropPhysicsService.TryGetSafeDropPosition),
                typeof(bool),
                typeof(Vector3),
                typeof(Vector3),
                typeof(float),
                typeof(float),
                typeof(CharacterController),
                typeof(Vector3).MakeByRefType());
            string physicsSource = ReadRuntimeSource(
                "Gameplay", "Common", "Physics", "ProductDropPhysicsService.cs");
            RequireSourceContains(physicsSource,
                "ValidateArguments(",
                "HasBlockingOverlap(",
                "origin,",
                "SphereCastNonAlloc(",
                "OverlapSphereNonAlloc(",
                "UnityEngine.Physics.AllLayers",
                "QueryTriggerInteraction.Ignore",
                "hitCollider != sourceController",
                "_overlapHits[index] != sourceController",
                "ThrowIfSaturated(");

            string dropSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "DropHeldProductSystem.cs");
            RequireSourceContains(dropSource,
                "GameMatcher.CarryingProduct",
                "GameMatcher.CharacterController",
                "product.ProductDropCollisionRadius",
                "player.CharacterController",
                "LocalizationKey.NotificationProductDropBlocked",
                "product.RemoveCarrierEntityId()",
                "player.isHandsOccupied = false",
                "player.isCarryingProduct = false",
                "product.isLooseProduct = true");
            RequireSourceOrder(
                dropSource,
                "LocalizationKey.NotificationProductDropBlocked",
                "continue;",
                "A blocked product drop must exit before changing carrying state.");
            RequireSourceOrder(
                dropSource,
                "continue;",
                "product.RemoveCarrierEntityId()",
                "A blocked product drop must preserve its CarrierEntityId relation.");

            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", nameof(BootstrapInstaller) + ".cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IProductDropPhysicsService>().To<ProductDropPhysicsService>().AsSingle()");

            LocalizationEntry blockedDropEntry = new RussianLocalizationCatalog().Entries
                .Single(entry => entry.Key ==
                                 LocalizationKey.NotificationProductDropBlocked);
            Require(blockedDropEntry.ArgumentCount == 0,
                $"{LocalizationKey.NotificationProductDropBlocked} must have zero arguments.");
        }

        private static void ValidateTrolleyArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] requiredComponents =
            {
                typeof(PlatformTrolley),
                typeof(TrolleyUpgradeTerminal),
                typeof(CarryingProduct),
                typeof(PushingTrolley),
                typeof(TrolleyUpgradeUnlocked),
                typeof(OrderProgressionCounted),
                typeof(CompletedOrderCount),
                typeof(TrolleyUpgradeTerminalEntityId),
                typeof(TrolleyStoreEntityId),
                typeof(TrolleyPusherEntityId),
                typeof(TrolleyEntityId),
                typeof(TrolleySlotIndex),
                typeof(TrolleyCapacity),
                typeof(OccupiedTrolleySlotCount),
                typeof(TrolleyMovementSpeed),
                typeof(TrolleyFollowDistance),
                typeof(TrolleySpawnPosition),
                typeof(TrolleySpawnRotation)
            };
            foreach (Type componentType in requiredComponents)
            {
                Require(discoveredComponents.Contains(componentType),
                    $"Platform trolley gameplay requires the {componentType.Name} Game component.");
            }

            Type trolleyFeatureType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "TrolleyFeature");
            Type trolleyMovementFeatureType = runtimeTypes.SingleOrDefault(type =>
                type.Name == "TrolleyMovementFeature");
            Require(trolleyFeatureType != null &&
                    typeof(Feature).IsAssignableFrom(trolleyFeatureType) &&
                    trolleyMovementFeatureType != null &&
                    typeof(Feature).IsAssignableFrom(trolleyMovementFeatureType),
                "Platform trolley state and movement must remain explicit Entitas features.");

            string[] executableSystemNames =
            {
                "RegisterCompletedOrderForProgressionSystem",
                "UnlockPlatformTrolleyUpgradeSystem",
                "PurchasePlatformTrolleySystem",
                "StartPushingTrolleySystem",
                "DetachPushedTrolleySystem",
                "LoadHeldProductOnTrolleySystem",
                "RefreshTrolleyOccupiedSlotCountSystem",
                "ValidatePlayerHandlingStateSystem",
                "ValidatePlatformTrolleyStateSystem",
                "FollowPushedTrolleySystem",
                "FollowWorkerTrolleySystem",
                "ApplyTrolleyProductPlacementSystem",
                "ApplyWorkerTrolleyProductPlacementSystem"
            };
            foreach (string systemName in executableSystemNames)
            {
                Type systemType = runtimeTypes.SingleOrDefault(type => type.Name == systemName);
                Require(systemType != null && typeof(IExecuteSystem).IsAssignableFrom(systemType),
                    $"{systemName} must remain an executable Entitas system.");
            }

            string configSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(PlatformTrolleyConfig) + ".cs");
            RequireSourceContains(configSource,
                "private int _purchasePrice = 200",
                "private int _requiredCompletedOrderCount = 2",
                "private int _capacity = 3",
                "private float _movementSpeed = 3.8f",
                "private float _followDistance = 1.7f",
                "public EntityBehaviour ViewPrefab => _viewPrefab",
                "public int PurchasePrice => _purchasePrice",
                "public int RequiredCompletedOrderCount => _requiredCompletedOrderCount",
                "public int Capacity => _capacity",
                "public float MovementSpeed => _movementSpeed",
                "public float FollowDistance => _followDistance",
                "ConfigValidation.RequireReference",
                "ConfigValidation.RequirePositive");
            RequireMethod(
                typeof(PlatformTrolleyConfig),
                nameof(PlatformTrolleyConfig.Configure),
                typeof(void),
                typeof(EntityBehaviour),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(float),
                typeof(float));

            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", nameof(StaticDataService) + ".cs");
            RequireSourceContains(staticDataSource,
                "Load<PlatformTrolleyConfig>(nameof(PlatformTrolleyConfig))",
                "platformTrolley.Validate()",
                "PlatformTrolley = platformTrolley",
                "platformTrolley.MovementSpeed <= fastestCarryMovementSpeed",
                "platformTrolley.MovementSpeed >= player.WalkSpeed",
                "platformTrolley.Capacity < customerVehicle.CargoCapacity",
                "ValidateTrolleyUpgradeLiquidity(",
                "trolley.RequiredCompletedOrderCount + 1",
                "nextMoney < trolley.PurchasePrice",
                "nextMoney = checked(nextMoney - trolley.PurchasePrice)");

            Require(typeof(IPlatformTrolleyFactory).IsAssignableFrom(
                    typeof(PlatformTrolleyFactory)),
                $"{nameof(PlatformTrolleyFactory)} must implement " +
                $"{nameof(IPlatformTrolleyFactory)}.");
            RequireMethod(
                typeof(IPlatformTrolleyFactory),
                nameof(IPlatformTrolleyFactory.Create),
                typeof(GameEntity),
                typeof(Pose),
                typeof(int));
            string trolleyFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(PlatformTrolleyFactory) + ".cs");
            RequireSourceContains(trolleyFactorySource,
                "CreateEntity.Empty(_identifiers.Next())",
                "AddViewPrefab(config.ViewPrefab)",
                "AddSpawnPosition(at.position)",
                "AddSpawnRotation(at.rotation)",
                "AddTrolleyStoreEntityId(storeEntityId)",
                "AddTrolleyCapacity(config.Capacity)",
                "AddOccupiedTrolleySlotCount(0)",
                "AddTrolleyMovementSpeed(config.MovementSpeed)",
                "AddTrolleyFollowDistance(config.FollowDistance)",
                "isPlatformTrolley = true",
                "isInteractable = true");
            Require(!trolleyFactorySource.Contains("SetEntity", StringComparison.Ordinal) &&
                    !trolleyFactorySource.Contains("CreateView", StringComparison.Ordinal),
                "PlatformTrolleyFactory must remain entity-first and leave view binding to the " +
                "shared infrastructure pipeline.");

            RequireMethod(
                typeof(IInteractionTargetFactory),
                nameof(IInteractionTargetFactory.CreateTrolleyUpgradeTerminal),
                typeof(GameEntity),
                typeof(int),
                typeof(Pose));
            string interactionTargetFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(InteractionTargetFactory) + ".cs");
            RequireSourceContains(interactionTargetFactorySource,
                "CreateTrolleyUpgradeTerminal(",
                "AddSceneViewKey(SceneViewId.TrolleyUpgradeTerminal)",
                "AddTrolleySpawnPosition(trolleySpawnPose.position)",
                "AddTrolleySpawnRotation(trolleySpawnPose.rotation)",
                "isTrolleyUpgradeTerminal = true");
            string storeFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", "StoreFactory.cs");
            RequireSourceContains(storeFactorySource,
                "GetSpawnPoint(SpawnPointId.PlatformTrolley)",
                "AddCompletedOrderCount(0)",
                "CreateTrolleyUpgradeTerminal(",
                "store.AddTrolleyUpgradeTerminalEntityId");
            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", nameof(BootstrapInstaller) + ".cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IPlatformTrolleyFactory>().To<PlatformTrolleyFactory>().AsSingle()");

            string trolleyFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "TrolleyFeature.cs");
            string[] trolleySystemTokens =
            {
                "Add(systems.Create<DetachPushedTrolleySystem>())",
                "Add(systems.Create<RegisterCompletedOrderForProgressionSystem>())",
                "Add(systems.Create<UnlockPlatformTrolleyUpgradeSystem>())",
                "Add(systems.Create<PurchasePlatformTrolleySystem>())",
                "Add(systems.Create<StartPushingTrolleySystem>())",
                "Add(systems.Create<LoadHeldProductOnTrolleySystem>())",
                "Add(systems.Create<RefreshTrolleyOccupiedSlotCountSystem>())",
                "Add(systems.Create<ValidatePlayerHandlingStateSystem>())",
                "Add(systems.Create<ValidatePlatformTrolleyStateSystem>())"
            };
            for (int index = 0; index < trolleySystemTokens.Length; index++)
            {
                Require(CountOccurrences(trolleyFeatureSource, trolleySystemTokens[index]) == 1,
                    $"TrolleyFeature must own {trolleySystemTokens[index]} exactly once.");
                if (index > 0)
                {
                    RequireSourceOrder(
                        trolleyFeatureSource,
                        trolleySystemTokens[index - 1],
                        trolleySystemTokens[index],
                        "TrolleyFeature system order must preserve detach, progression, purchase, " +
                        "cargo refresh and invariant validation sequencing.");
                }
            }

            string detachTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "DetachPushedTrolleySystem.cs");
            RequireSourceContains(detachTrolleySource,
                "GameMatcher.HandsOccupied",
                "GameMatcher.PushingTrolley",
                "InputMatcher.InputState",
                "InputMatcher.TrolleyPressed",
                "trolley.RemoveTrolleyPusherEntityId()",
                "player.isPushingTrolley = false",
                "player.isHandsOccupied = false");
            Require(!detachTrolleySource.Contains(
                        "InputMatcher.DropPressed", StringComparison.Ordinal) &&
                    !detachTrolleySource.Contains(
                        "InputMatcher.InteractPressed", StringComparison.Ordinal),
                "Neither E world interaction nor G product drop may detach a pushed trolley; " +
                "detach belongs only to F.");

            string startTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "StartPushingTrolleySystem.cs");
            RequireSourceContains(startTrolleySource,
                "GameMatcher.FocusedEntityId",
                "GameMatcher.FocusedInteractionType",
                "InputMatcher.TrolleyPressed",
                "InteractionTypeId.PlatformTrolley",
                "InteractionTypeId.Product",
                "focusedTarget.hasTrolleyEntityId",
                "focusedTarget.hasTrolleySlotIndex",
                "GetEntitiesWithTrolleyEntityId(trolley.EntityId)",
                "trolley.AddTrolleyPusherEntityId(player.EntityId)",
                "focusedTarget.isHighlighted = false");
            Require(!startTrolleySource.Contains(
                    "GameMatcher.InteractionRequest", StringComparison.Ordinal),
                "F trolley attachment must not consume or synthesize an E interaction request.");

            string trolleyMovementFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "TrolleyMovementFeature.cs");
            Require(CountOccurrences(
                        trolleyMovementFeatureSource,
                        "Add(systems.Create<FollowPushedTrolleySystem>())") == 1 &&
                    CountOccurrences(
                        trolleyMovementFeatureSource,
                        "Add(systems.Create<FollowWorkerTrolleySystem>())") == 1,
                "TrolleyMovementFeature must own exactly one player and one worker follow " +
                "system.");
            RequireSourceOrder(
                trolleyMovementFeatureSource,
                "Create<FollowPushedTrolleySystem>()",
                "Create<FollowWorkerTrolleySystem>()",
                "Player and worker trolleys must retain their explicit follow order.");
            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<OrderProgressFeature>()",
                "Create<TrolleyFeature>()",
                "Trolley progression must run after order progress.");
            int trolleyFeatureIndex = storeFeatureSource.IndexOf(
                "Create<TrolleyFeature>()", StringComparison.Ordinal);
            int finalStorageStateIndex = storeFeatureSource.LastIndexOf(
                "Create<StorageStateFeature>()", StringComparison.Ordinal);
            Require(trolleyFeatureIndex >= 0 &&
                    finalStorageStateIndex > trolleyFeatureIndex &&
                    CountOccurrences(storeFeatureSource,
                        "Create<StorageStateFeature>()") == 4,
                "Trolley reservations must refresh at the fourth storage-state barrier.");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<MovementFeature>()",
                "Create<TrolleyMovementFeature>()",
                "The trolley must follow the player after player movement is resolved.");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<TrolleyMovementFeature>()",
                "Create<InteractionPromptFeature>()",
                "Trolley movement must finish before interaction prompts are presented.");
            string sceneBindingsSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreSceneBindings", "Systems",
                "ValidateStoreSceneBindingsSystem.cs");
            RequireSourceContains(sceneBindingsSource,
                "GameMatcher.TrolleyUpgradeTerminalEntityId",
                "store.TrolleyUpgradeTerminalEntityId",
                "terminal.isTrolleyUpgradeTerminal",
                "terminal.hasTrolleySpawnPosition",
                "terminal.hasTrolleySpawnRotation");
            string interactionPromptFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "InteractionPromptFeature.cs");
            RequireSourceOrder(
                interactionPromptFeatureSource,
                "Add(systems.Create<ResolveTrolleyUpgradeTerminalPromptSystem>())",
                "Add(systems.Create<ResolvePlatformTrolleyPromptSystem>())",
                "Trolley terminal and runtime trolley prompts must remain explicit systems.");
            string platformTrolleyPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolvePlatformTrolleyPromptSystem.cs");
            RequireSourceContains(platformTrolleyPromptSource,
                "InteractionTypeId.Product",
                "ResolveProductTrolleyPrompt(player)",
                "product.hasTrolleyEntityId",
                "product.hasTrolleySlotIndex",
                "LocalizationKey.PromptProductAndTrolleyActions",
                "bool productActionAvailable = player.isFocusInteractionAvailable",
                "productActionAvailable");

            string progressionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "RegisterCompletedOrderForProgressionSystem.cs");
            RequireSourceContains(progressionSource,
                "GameMatcher.OrderRewarded",
                "GameMatcher.OrderProgressionCounted",
                "GameMatcher.Destructed",
                "checked(store.CompletedOrderCount + 1)",
                "order.isOrderProgressionCounted = true");
            string unlockSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "UnlockPlatformTrolleyUpgradeSystem.cs");
            RequireSourceContains(unlockSource,
                "GameMatcher.TrolleyUpgradeUnlocked",
                "store.CompletedOrderCount < _config.RequiredCompletedOrderCount",
                "store.isTrolleyUpgradeUnlocked = true",
                "LocalizationKey.NotificationTrolleyUnlocked");
            string purchaseSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "PurchasePlatformTrolleySystem.cs");
            RequireSourceContains(purchaseSource,
                "GetEntityWithTrolleyStoreEntityId(store.EntityId)",
                "LocalizationKey.NotificationTrolleyAlreadyPurchased",
                "!store.isTrolleyUpgradeUnlocked",
                "store.Money < _config.PurchasePrice",
                "_economySolvency.EvaluateDebit(",
                "EconomyDebitAvailability.DemandWouldBecomeInsolvent",
                "LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects",
                "_trolleys.Create(spawnPose, store.EntityId)",
                "store.ReplaceMoney(moneyAfterPurchase)");
            RequireSourceOrder(
                purchaseSource,
                "GetEntityWithTrolleyStoreEntityId(store.EntityId)",
                "_trolleys.Create(spawnPose, store.EntityId)",
                "Purchase must reject an existing trolley before creating another one.");
            RequireSourceOrder(
                purchaseSource,
                "_economySolvency.EvaluateDebit(",
                "_trolleys.Create(spawnPose, store.EntityId)",
                "Purchase must re-evaluate the protected economy before creating a trolley.");
            RequireSourceOrder(
                purchaseSource,
                "_trolleys.Create(spawnPose, store.EntityId)",
                "store.ReplaceMoney(moneyAfterPurchase)",
                "Purchase must create one trolley and then commit its single debit.");

            string trolleyPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveTrolleyUpgradeTerminalPromptSystem.cs");
            RequireSourceContains(trolleyPromptSource,
                "_solvency.EvaluateDebit(",
                "EconomyDebitAvailability.Available",
                "EconomyDebitAvailability.InsufficientMoney",
                "EconomyDebitAvailability.DemandWouldBecomeInsolvent",
                "LocalizationKey.PromptTrolleyPurchaseWouldBlockProjects");

            string loadTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "LoadHeldProductOnTrolleySystem.cs");
            RequireSourceContains(loadTrolleySource,
                "product.hasReservedDeliverySlotIndex",
                "product.hasReservedStorageSlotIndex",
                "product.hasReservedOrderLineEntityId",
                "GetEntitiesWithTrolleyEntityId(trolley.EntityId)",
                "product.AddTrolleyEntityId(trolley.EntityId)",
                "product.AddTrolleySlotIndex(freeSlotIndex)",
                "LocalizationKey.NotificationTrolleyFull");
            Require(!loadTrolleySource.Contains(
                        "RemoveReservedDeliverySlotIndex",
                        StringComparison.Ordinal) &&
                    !loadTrolleySource.Contains(
                        "RemoveReservedStorageSlotIndex",
                        StringComparison.Ordinal) &&
                    !loadTrolleySource.Contains(
                        "RemoveReservedOrderLineEntityId",
                        StringComparison.Ordinal),
                "Putting cargo on the trolley must preserve every recovery reservation.");
            string pickupSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems", "PickUpProductSystem.cs");
            RequireSourceContains(pickupSource,
                "ReleaseTrolleySlot(product)",
                "product.RemoveTrolleyEntityId()",
                "product.RemoveTrolleySlotIndex()");
            string trolleyPlacementSource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "Systems",
                "ApplyTrolleyProductPlacementSystem.cs");
            RequireSourceContains(trolleyPlacementSource,
                "GameMatcher.TrolleyEntityId",
                "GameMatcher.TrolleySlotIndex",
                "GameMatcher.ReservedDeliverySlotIndex",
                "GameMatcher.ReservedStorageSlotIndex",
                "trolley.Slots[product.TrolleySlotIndex]",
                "ProductPhysicsUtility.ConfigureInteractiveSlot");
            string productPlacementFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "ProductPlacementFeature.cs");
            RequireSourceOrder(
                productPlacementFeatureSource,
                "Add(systems.Create<ApplyTrolleyProductPlacementSystem>())",
                "Add(systems.Create<ValidateProductPlacementSystem>())",
                "Trolley cargo placement must be applied before placement validation.");

            string handlingValidationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "ValidatePlayerHandlingStateSystem.cs");
            RequireSourceContains(handlingValidationSource,
                "bool hasExactlyOneHandlingRole = carryingProduct ^ pushingTrolley",
                "player.isHandsOccupied != hasExactlyOneHandlingRole",
                "player.isModalOpen && hasExactlyOneHandlingRole",
                "GetEntityWithCarrierEntityId(player.EntityId)",
                "GetEntityWithTrolleyPusherEntityId(player.EntityId)");
            string movementSource = ReadRuntimeSource(
                "Gameplay", "Features", "Movement", "Systems",
                "ResolveMovementSpeedSystem.cs");
            RequireSourceContains(movementSource,
                "if (player.isPushingTrolley)",
                "speed = trolley.TrolleyMovementSpeed",
                "else if (player.isCarryingProduct)");
            string followSource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "FollowPushedTrolleySystem.cs");
            RequireSourceContains(followSource,
                "ITrolleyMotionService motion",
                "playerTransform.forward * trolley.TrolleyFollowDistance",
                "_motion.TryResolveMove(",
                "trolley.Colliders",
                "player.CharacterController",
                "out Pose resolvedPose",
                "trolley.Rigidbody.position = resolvedPose.position",
                "trolley.Rigidbody.rotation = resolvedPose.rotation",
                "resolvedPose.position",
                "resolvedPose.rotation");
            RequireSourceOrder(
                followSource,
                "_motion.TryResolveMove(",
                "trolley.Rigidbody.position = resolvedPose.position",
                "Trolley movement must pass its collision query before mutating Rigidbody pose.");
            RequireSourceOrder(
                followSource,
                "_motion.TryResolveMove(",
                "trolley.Transform.SetPositionAndRotation(",
                "Trolley movement must pass its collision query before mutating Transform pose.");
            string followWorkerTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Trolley", "Systems",
                "FollowWorkerTrolleySystem.cs");
            RequireSourceContains(followWorkerTrolleySource,
                "GameMatcher.WorkerTrolley",
                "GameMatcher.TrolleyPusherEntityId",
                "workerTransform.forward * trolley.TrolleyFollowDistance",
                "_navigation.SetAutomaticRotation(",
                "_motion.TryResolveMove(",
                "trolley.Colliders",
                "out Pose resolvedPose",
                "trolley.Rigidbody.position = resolvedPose.position",
                "trolley.Transform.SetPositionAndRotation(",
                "GetEntityWithWarehouseTaskWorkerTrolleyEntityId(",
                "WarehouseTaskBlockReasonId.WorkerTrolleyObstructed",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "enabled: true",
                "NormalizeEmptyReturn(worker, trolley)");
            Require(!followWorkerTrolleySource.Contains(
                    ".updateRotation", StringComparison.Ordinal),
                "Worker-trolley following must keep NavMesh rotation control behind the " +
                "worker navigation service.");
            Require(CountOccurrences(followWorkerTrolleySource,
                        "enabled: true") == 1,
                "Obstructed taskless trolley return must restore automatic worker rotation " +
                "exactly once when it detaches.");
            RequireSourceOrder(
                followWorkerTrolleySource,
                "_motion.TryResolveMove(",
                "trolley.Rigidbody.position = resolvedPose.position",
                "Worker-trolley movement must resolve collisions before mutating pose.");
            Require(typeof(ITrolleyMotionService).IsAssignableFrom(
                    typeof(TrolleyMotionService)),
                $"{nameof(TrolleyMotionService)} must implement " +
                $"{nameof(ITrolleyMotionService)}.");
            RequireMethod(
                typeof(ITrolleyMotionService),
                nameof(ITrolleyMotionService.TryResolveMove),
                typeof(bool),
                typeof(Rigidbody),
                typeof(Collider[]),
                typeof(CharacterController),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Pose).MakeByRefType());
            RequireMethod(
                typeof(ITrolleyMotionService),
                nameof(ITrolleyMotionService.TryResolveMove),
                typeof(bool),
                typeof(Rigidbody),
                typeof(Collider[]),
                typeof(Transform),
                typeof(float),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Pose).MakeByRefType());
            string trolleyMotionSource = ReadRuntimeSource(
                "Gameplay", "Common", "Physics", "TrolleyMotionService.cs");
            RequireSourceContains(trolleyMotionSource,
                "private const int MaxQueryHits = 64",
                "new RaycastHit[MaxQueryHits]",
                "new Collider[MaxQueryHits]",
                "public bool TryResolveMove(",
                "out Pose resolvedPose",
                "ValidateStepOffset(sourceController)",
                "float stepHeight = sourceController.stepOffset",
                "Mathf.Abs(rise) > stepHeight + PoseTolerance",
                "Pose raisedPose = new(",
                "Pose raisedTargetPose = new(",
                "resolvedPose = targetPose",
                "BoxCastNonAlloc(",
                "OverlapBoxNonAlloc(",
                "UnityEngine.Physics.ComputePenetration(",
                "float contactProbeDistance = ContactProbeDistance()",
                "if (hitDistance > PoseTolerance)",
                "UnityEngine.Physics.defaultContactOffset",
                "return contactOffset + PoseTolerance",
                "hitDistance + contactProbeDistance",
                "PenetrationTolerance",
                "UnityEngine.Physics.AllLayers",
                "QueryTriggerInteraction.Ignore",
                "enabledSolidColliderCount != 1",
                "candidate == sourceCollider",
                "EnsureBufferWasNotSaturated(");
            RequireSourceOrder(
                trolleyMotionSource,
                "if (hitDistance > PoseTolerance)",
                "hitDistance + contactProbeDistance",
                "A positive-distance trolley sweep hit must block before the near-contact " +
                "recovery probe.");
            Require(CountOccurrences(trolleyMotionSource, "!IsPathClear(") == 3,
                "The trolley curb fallback must validate exactly three bounded path segments: " +
                "rise, traverse and settle.");
            Require(!trolleyMotionSource.Contains(
                        "UnityEngine.Physics.BoxCast(", StringComparison.Ordinal) &&
                    !trolleyMotionSource.Contains(
                        "UnityEngine.Physics.OverlapBox(", StringComparison.Ordinal),
                "Trolley motion must keep its sweep and overlap queries non-allocating.");
            RequireSourceContains(bootstrapSource,
                "Bind<ITrolleyMotionService>().To<TrolleyMotionService>().AsSingle()");
            string emitInteractionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "EmitInteractionRequestSystem.cs");
            RequireSourceContains(emitInteractionSource,
                ".NoneOf(GameMatcher.ModalOpen, GameMatcher.PushingTrolley)");
            string procurementSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "OpenProcurementSystem.cs");
            RequireSourceContains(procurementSource,
                "player.isPushingTrolley",
                "LocalizationKey.NotificationReleaseTrolleyFirst");

            var trolleyLocalizationArities = new Dictionary<LocalizationKey, int>
            {
                { LocalizationKey.HudControlsPushingTrolley, 0 },
                { LocalizationKey.PromptTrolleyUpgradeLocked, 2 },
                { LocalizationKey.PromptPurchaseTrolley, 1 },
                { LocalizationKey.PromptTrolleyInsufficientMoney, 1 },
                { LocalizationKey.PromptTrolleyPurchased, 0 },
                { LocalizationKey.PromptPushTrolley, 2 },
                { LocalizationKey.PromptPlaceProductOnTrolley, 3 },
                { LocalizationKey.PromptTrolleyFull, 2 },
                { LocalizationKey.PromptTrolleyPushedByOther, 0 },
                { LocalizationKey.PromptReleaseTrolley, 0 },
                { LocalizationKey.PromptReleaseTrolleyFirst, 0 },
                { LocalizationKey.PromptFreeHandsForTrolleyUpgrade, 0 },
                { LocalizationKey.PromptTrolleyPurchaseWouldBlockProjects, 0 },
                { LocalizationKey.PromptProductAndTrolleyActions, 1 },
                { LocalizationKey.NotificationTrolleyUnlocked, 1 },
                { LocalizationKey.NotificationTrolleyUpgradeLocked, 2 },
                { LocalizationKey.NotificationTrolleyInsufficientMoney, 1 },
                { LocalizationKey.NotificationTrolleyPurchased, 1 },
                { LocalizationKey.NotificationTrolleyAlreadyPurchased, 0 },
                { LocalizationKey.NotificationTrolleyFull, 0 },
                { LocalizationKey.NotificationReleaseTrolleyFirst, 0 },
                { LocalizationKey.NotificationFreeHandsForTrolleyUpgrade, 0 },
                { LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects, 0 },
                { LocalizationKey.WorldTrolleyUpgrade, 1 }
            };
            Dictionary<LocalizationKey, LocalizationEntry> localizationEntries =
                new RussianLocalizationCatalog().Entries.ToDictionary(entry => entry.Key);
            foreach ((LocalizationKey key, int argumentCount) in trolleyLocalizationArities)
            {
                Require(localizationEntries.TryGetValue(key, out LocalizationEntry entry) &&
                        entry.ArgumentCount == argumentCount,
                    $"Russian trolley localization {key} must exist with arity " +
                    $"{argumentCount}.");
            }
        }

        private static void ValidateWarehouseWorkerArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] requiredComponents =
            {
                typeof(WarehouseWorker),
                typeof(WorkerTrolley),
                typeof(PushingWorkerTrolley),
                typeof(WorkerShiftActive),
                typeof(WarehouseWorkerHiringUnlocked),
                typeof(WorkerPaidDayNumber),
                typeof(WarehouseWorkerStoreEntityId),
                typeof(WarehouseWorkerStatus),
                typeof(WarehouseWorkerPickupPosition),
                typeof(WarehouseWorkerPickupRotation),
                typeof(WarehouseWorkerStoragePosition),
                typeof(WarehouseWorkerStorageRotation),
                typeof(WarehouseWorkerCustomerLoadingPosition),
                typeof(WarehouseWorkerCustomerLoadingRotation),
                typeof(NavigationAgentComponent),
                typeof(WarehouseTask),
                typeof(InboundToStorageTask),
                typeof(StockToCustomerLoadingTask),
                typeof(WorkerTrolleyCustomerLoadingRun),
                typeof(WorkerTrolleyStoreEntityId),
                typeof(WorkerTrolleyEntityId),
                typeof(WorkerTrolleySlotIndex),
                typeof(WorkerTrolleyHomePosition),
                typeof(WorkerTrolleyHomeRotation),
                typeof(WorkerTrolleyCustomerLoadingPosition),
                typeof(WorkerTrolleyCustomerLoadingRotation),
                typeof(WarehouseTaskStoreEntityId),
                typeof(WarehouseTaskStorageZoneEntityId),
                typeof(WarehouseTaskProductEntityId),
                typeof(WarehouseTaskWorkerTrolleyEntityId),
                typeof(WarehouseTaskCustomerVisitEntityId),
                typeof(WarehouseTaskOrderLineEntityId),
                typeof(AssignedWorkerEntityId),
                typeof(WarehouseTaskReservedStorageSlotIndex),
                typeof(WarehouseTaskReservedLoadingSlotIndex),
                typeof(ReservedCustomerLoadingSlotIndex),
                typeof(WarehouseRunEntityId),
                typeof(WarehouseRunProductCount),
                typeof(WarehouseTaskStep),
                typeof(WarehouseTaskBlockReason),
                typeof(WarehouseTaskTimeoutRemaining),
                typeof(DayPayrollExpenses)
            };
            foreach (Type component in requiredComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Warehouse-worker gameplay requires the {component.Name} Game component.");
            }
            Require(!componentTypes.Any(type =>
                    type.Name.Contains("WorkerTrolleyPushPoint", StringComparison.Ordinal)),
                "Worker-trolley approach must derive from root pose and follow distance, not " +
                "a dedicated push-point ECS component.");

            RequireComponentIndexAttribute(
                typeof(WarehouseWorkerStoreEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WorkerTrolleyStoreEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WarehouseTaskProductEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(AssignedWorkerEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WarehouseTaskWorkerTrolleyEntityId),
                "Entitas.CodeGeneration.Attributes.PrimaryEntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WorkerTrolleyEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WarehouseRunEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WarehouseTaskCustomerVisitEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireComponentIndexAttribute(
                typeof(WarehouseTaskOrderLineEntityId),
                "Entitas.CodeGeneration.Attributes.EntityIndexAttribute");
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithWarehouseWorkerStoreEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithWorkerTrolleyStoreEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithWarehouseTaskProductEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithAssignedWorkerEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntityWithWarehouseTaskWorkerTrolleyEntityId",
                typeof(GameEntity));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithWorkerTrolleyEntityId",
                typeof(HashSet<GameEntity>));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithWarehouseRunEntityId",
                typeof(HashSet<GameEntity>));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithWarehouseTaskCustomerVisitEntityId",
                typeof(HashSet<GameEntity>));
            RequireGeneratedIndexApi(
                runtimeTypes,
                "GetEntitiesWithWarehouseTaskOrderLineEntityId",
                typeof(HashSet<GameEntity>));

            Type employeeFeature = runtimeTypes.SingleOrDefault(type =>
                type.Name == "EmployeeFeature");
            Type workerFeature = runtimeTypes.SingleOrDefault(type =>
                type.Name == "WarehouseWorkerFeature");
            Require(employeeFeature != null && typeof(Feature).IsAssignableFrom(employeeFeature) &&
                    workerFeature != null && typeof(Feature).IsAssignableFrom(workerFeature),
                "EmployeeFeature and WarehouseWorkerFeature must remain explicit Entitas features.");

            string[] employeeSystems =
            {
                "UnlockWarehouseWorkerHiringSystem",
                "SyncWarehouseWorkerShiftSystem",
                "PayWarehouseWorkerShiftSystem",
                "HireWarehouseWorkerSystem",
                "WarehouseWorkerFeature"
            };
            string[] workerSystems =
            {
                "ConfigureWarehouseWorkerNavigationSystem",
                "CleanupBlockedWarehouseTaskSystem",
                "CleanupBlockedWorkerTrolleyRunSystem",
                "GenerateCustomerLoadingTaskSystem",
                "GenerateInboundStorageTaskSystem",
                "AssignWarehouseTaskSystem",
                "TickWarehouseTaskTimeoutSystem",
                "ExecuteInboundStorageTaskSystem",
                "ExecuteCustomerLoadingTaskSystem",
                "ExecuteWorkerTrolleyRunSystem",
                "ReturnWorkerTrolleySystem",
                "DetectOrphanedWarehouseTaskSystem",
                "DetectOrphanedWorkerTrolleyRunSystem",
                "RecoverBlockedInboundTaskSystem",
                "RecoverBlockedCustomerLoadingTaskSystem",
                "RecoverBlockedWorkerTrolleyRunSystem",
                "RefreshWorkerTrolleyOccupiedSlotCountSystem",
                "ValidateWarehouseWorkerStateSystem",
                "ValidateWorkerTrolleyStateSystem"
            };
            foreach (string systemName in employeeSystems.Take(employeeSystems.Length - 1)
                         .Concat(workerSystems))
            {
                Type system = runtimeTypes.SingleOrDefault(type => type.Name == systemName);
                Require(system != null && typeof(IExecuteSystem).IsAssignableFrom(system),
                    $"{systemName} must remain an executable Entitas system.");
            }

            string employeeFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "EmployeeFeature.cs");
            RequireExactFeatureOrder(employeeFeatureSource, employeeSystems, "EmployeeFeature");
            string workerFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "WarehouseWorkerFeature.cs");
            RequireExactFeatureOrder(workerFeatureSource, workerSystems, "WarehouseWorkerFeature");

            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<InteractionFeature>()",
                "Create<EmployeeFeature>()",
                "Employee hiring must consume world interaction after interaction emission.");
            RequireSourceOrder(
                storeFeatureSource,
                "Create<EmployeeFeature>()",
                "Create<StoreDayFeature>()",
                "Employee hiring and task state must settle before the day can close.");
            Require(CountOccurrences(storeFeatureSource, "Create<EmployeeFeature>()") == 1,
                "StoreFeature must execute EmployeeFeature exactly once.");

            string configSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(WarehouseWorkerConfig) + ".cs");
            RequireSourceContains(configSource,
                "private int _requiredCompletedOrderCount = 4",
                "private int _hirePrice = 400",
                "private int _dailyWage = 100",
                "private float _movementSpeed = 2.8f",
                "private float _acceleration = 12f",
                "private float _angularSpeed = 720f",
                "private float _stoppingDistance = 0.2f",
                "private float _navigationSampleRadius = 2f",
                "private float _taskTimeout = 20f",
                "private EntityBehaviour _trolleyViewPrefab",
                "private int _trolleyCapacity = 3",
                "private float _trolleyFollowDistance = 1.7f",
                "public EntityBehaviour ViewPrefab => _viewPrefab",
                "public EntityBehaviour TrolleyViewPrefab => _trolleyViewPrefab",
                "public int TrolleyCapacity => _trolleyCapacity",
                "public float TrolleyFollowDistance => _trolleyFollowDistance",
                "nameof(TrolleyCapacity)} must be at least 2",
                "ConfigValidation.RequireReference",
                "ConfigValidation.RequirePositive");
            string staticDataSource = ReadRuntimeSource(
                "Gameplay", "StaticData", nameof(StaticDataService) + ".cs");
            RequireSourceContains(staticDataSource,
                "Load<WarehouseWorkerConfig>(nameof(WarehouseWorkerConfig))",
                "warehouseWorker.Validate()",
                "WarehouseWorker = warehouseWorker");

            Require(typeof(IWarehouseWorkerFactory).IsAssignableFrom(
                    typeof(WarehouseWorkerFactory)),
                $"{nameof(WarehouseWorkerFactory)} must implement " +
                $"{nameof(IWarehouseWorkerFactory)}.");
            Require(typeof(IWarehouseTaskFactory).IsAssignableFrom(
                    typeof(WarehouseTaskFactory)),
                $"{nameof(WarehouseTaskFactory)} must implement " +
                $"{nameof(IWarehouseTaskFactory)}.");
            Require(typeof(IWarehouseWorkerTrolleyFactory).IsAssignableFrom(
                    typeof(WarehouseWorkerTrolleyFactory)),
                $"{nameof(WarehouseWorkerTrolleyFactory)} must implement " +
                $"{nameof(IWarehouseWorkerTrolleyFactory)}.");
            RequireMethod(
                typeof(IWarehouseWorkerFactory),
                nameof(IWarehouseWorkerFactory.Create),
                typeof(GameEntity),
                typeof(int),
                typeof(Pose),
                typeof(Pose),
                typeof(Pose),
                typeof(Pose));
            RequireMethod(
                typeof(IWarehouseTaskFactory),
                nameof(IWarehouseTaskFactory.CreateInboundToStorage),
                typeof(GameEntity),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IWarehouseTaskFactory),
                nameof(IWarehouseTaskFactory.CreateStockToCustomerLoading),
                typeof(GameEntity),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IWarehouseTaskFactory),
                nameof(IWarehouseTaskFactory.CreateWorkerTrolleyCustomerLoadingRun),
                typeof(GameEntity),
                typeof(int),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IWarehouseWorkerTrolleyFactory),
                nameof(IWarehouseWorkerTrolleyFactory.Create),
                typeof(GameEntity),
                typeof(int),
                typeof(Pose),
                typeof(Pose));
            string workerFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(WarehouseWorkerFactory) + ".cs");
            RequireSourceContains(workerFactorySource,
                "CreateEntity.Empty(_identifiers.Next())",
                "AddViewPrefab(config.ViewPrefab)",
                "AddWarehouseWorkerStoreEntityId(storeEntityId)",
                "AddWarehouseWorkerStatus(WarehouseWorkerStatusId.Idle)",
                "AddWarehouseWorkerPickupPosition(pickupPose.position)",
                "AddWarehouseWorkerStoragePosition(storagePose.position)",
                "AddWarehouseWorkerCustomerLoadingPosition(",
                "customerLoadingPose.position",
                "AddWarehouseWorkerCustomerLoadingRotation(",
                "customerLoadingPose.rotation",
                "isWarehouseWorker = true");
            string taskFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(WarehouseTaskFactory) + ".cs");
            RequireSourceContains(taskFactorySource,
                "CreateEntity.Empty(_identifiers.Next())",
                "AddWarehouseTaskStoreEntityId(storeEntityId)",
                "AddWarehouseTaskProductEntityId(productEntityId)",
                "AddWarehouseTaskStorageZoneEntityId(storageZoneEntityId)",
                "AddWarehouseTaskReservedStorageSlotIndex(reservedStorageSlotIndex)",
                "AddWarehouseTaskStep(WarehouseTaskStepId.Available)",
                "AddWarehouseTaskTimeoutRemaining(",
                "isInboundToStorageTask = true",
                "CreateStockToCustomerLoading",
                "AddWarehouseTaskCustomerVisitEntityId(customerVisitEntityId)",
                "AddWarehouseTaskOrderLineEntityId(orderLineEntityId)",
                "AddWarehouseTaskReservedLoadingSlotIndex(",
                "reservedLoadingSlotIndex",
                "isStockToCustomerLoadingTask = true",
                "CreateWorkerTrolleyCustomerLoadingRun",
                "AddWarehouseTaskCustomerVisitEntityId(customerVisitEntityId)",
                "AddWarehouseTaskWorkerTrolleyEntityId(workerTrolleyEntityId)",
                "isWorkerTrolleyCustomerLoadingRun = true");
            string workerTrolleyFactorySource = ReadRuntimeSource(
                "Gameplay", "Factories", nameof(WarehouseWorkerTrolleyFactory) + ".cs");
            RequireSourceContains(workerTrolleyFactorySource,
                "CreateEntity.Empty(_identifiers.Next())",
                "AddViewPrefab(config.TrolleyViewPrefab)",
                "AddSpawnPosition(homePose.position)",
                "AddSpawnRotation(homePose.rotation)",
                "AddWorkerTrolleyStoreEntityId(storeEntityId)",
                "AddTrolleyCapacity(config.TrolleyCapacity)",
                "AddOccupiedTrolleySlotCount(0)",
                "AddTrolleyFollowDistance(config.TrolleyFollowDistance)",
                "AddWorkerTrolleyHomePosition(homePose.position)",
                "AddWorkerTrolleyHomeRotation(homePose.rotation)",
                "AddWorkerTrolleyCustomerLoadingPosition(",
                "customerLoadingPose.position",
                "AddWorkerTrolleyCustomerLoadingRotation(",
                "customerLoadingPose.rotation",
                "isWorkerTrolley = true");
            Require(!workerFactorySource.Contains("SetEntity", StringComparison.Ordinal) &&
                    !workerFactorySource.Contains("CreateView", StringComparison.Ordinal) &&
                    !taskFactorySource.Contains("SetEntity", StringComparison.Ordinal) &&
                    !workerTrolleyFactorySource.Contains(
                        "SetEntity", StringComparison.Ordinal) &&
                    !workerTrolleyFactorySource.Contains(
                        "CreateView", StringComparison.Ordinal),
                "Warehouse-worker factories must remain entity-first.");
            Require(!workerTrolleyFactorySource.Contains(
                        "isPlatformTrolley = true", StringComparison.Ordinal) &&
                    !workerTrolleyFactorySource.Contains(
                        "isInteractable = true", StringComparison.Ordinal),
                "The worker-trolley factory must not grant the player PlatformTrolley or " +
                "Interactable roles.");

            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", nameof(BootstrapInstaller) + ".cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IWorkerNavigationService>().To<NavMeshWorkerNavigationService>().AsSingle()",
                "Bind<IWarehouseWorkerFactory>().To<WarehouseWorkerFactory>().AsSingle()",
                "Bind<IWarehouseTaskFactory>().To<WarehouseTaskFactory>().AsSingle()",
                "Bind<IWarehouseWorkerTrolleyFactory>()",
                ".To<WarehouseWorkerTrolleyFactory>().AsSingle()");
            Require(typeof(IWorkerNavigationService).IsAssignableFrom(
                    typeof(NavMeshWorkerNavigationService)),
                $"{nameof(NavMeshWorkerNavigationService)} must implement " +
                $"{nameof(IWorkerNavigationService)}.");
            RequireMethod(
                typeof(IWorkerNavigationService),
                nameof(IWorkerNavigationService.TryEnsurePlacedOnNavMesh),
                typeof(bool),
                typeof(NavMeshAgent),
                typeof(Vector3),
                typeof(float));
            RequireMethod(
                typeof(IWorkerNavigationService),
                nameof(IWorkerNavigationService.TrySetDestination),
                typeof(bool),
                typeof(NavMeshAgent),
                typeof(Vector3),
                typeof(float));
            RequireMethod(
                typeof(IWorkerNavigationService),
                nameof(IWorkerNavigationService.SetAutomaticRotation),
                typeof(void),
                typeof(NavMeshAgent),
                typeof(bool));
            RequireMethod(
                typeof(IWorkerNavigationService),
                nameof(IWorkerNavigationService.HasReachedDestination),
                typeof(bool),
                typeof(NavMeshAgent),
                typeof(Vector3),
                typeof(Vector3),
                typeof(float));
            string navigationSource = ReadRuntimeSource(
                "Gameplay", "Common", "Navigation",
                nameof(NavMeshWorkerNavigationService) + ".cs");
            RequireSourceContains(navigationSource,
                "if (agent.isOnNavMesh)",
                "NavMesh.SamplePosition(",
                "agent.CalculatePath(hit.position, _path)",
                "_path.status != NavMeshPathStatus.PathComplete",
                "agent.SetPath(_path)",
                "public void SetAutomaticRotation(NavMeshAgent agent, bool enabled)",
                "agent.updateRotation = enabled",
                "GetState(agent) != WorkerNavigationStateId.Reached",
                "currentPosition - destination",
                "agent.ResetPath()");
            string configureNavigationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ConfigureWarehouseWorkerNavigationSystem.cs");
            RequireSourceContains(configureNavigationSource,
                "GameMatcher.WarehouseWorkerCustomerLoadingPosition",
                "_navigation.TryEnsurePlacedOnNavMesh(",
                "_config.NavigationSampleRadius");
            string executeInboundNavigationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ExecuteInboundStorageTaskSystem.cs");
            string executeCustomerLoadingNavigationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ExecuteCustomerLoadingTaskSystem.cs");
            RequireSourceContains(executeInboundNavigationSource,
                "_navigation.HasReachedDestination(",
                "_navigation.TrySetDestination(",
                "_navigation.GetState(");
            RequireSourceContains(executeCustomerLoadingNavigationSource,
                "_navigation.HasReachedDestination(",
                "_navigation.TrySetDestination(",
                "_navigation.GetState(");
            Require(!configureNavigationSource.Contains(".isOnNavMesh",
                        StringComparison.Ordinal) &&
                    !executeInboundNavigationSource.Contains(".isOnNavMesh",
                        StringComparison.Ordinal) &&
                    !executeInboundNavigationSource.Contains(".hasPath",
                        StringComparison.Ordinal) &&
                    !executeCustomerLoadingNavigationSource.Contains(".isOnNavMesh",
                        StringComparison.Ordinal) &&
                    !executeCustomerLoadingNavigationSource.Contains(".hasPath",
                        StringComparison.Ordinal),
                "Warehouse-worker systems must keep NavMesh state behind the navigation " +
                "service boundary.");
            string registrarSource = ReadRuntimeSource(
                "Gameplay", "Registrars", nameof(NavMeshAgentRegistrar) + ".cs");
            RequireSourceContains(registrarSource,
                "RequireComponent(typeof(NavMeshAgent))",
                "Entity.AddNavigationAgent(GetComponent<NavMeshAgent>())",
                "Entity.RemoveNavigationAgent()");

            string unlockSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "UnlockWarehouseWorkerHiringSystem.cs");
            RequireSourceContains(unlockSource,
                "store.CompletedOrderCount < _config.RequiredCompletedOrderCount",
                "store.isWarehouseWorkerHiringUnlocked = true",
                "LocalizationKey.NotificationWarehouseWorkerUnlocked");
            string hireSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "HireWarehouseWorkerSystem.cs");
            RequireSourceContains(hireSource,
                "GetEntityWithWarehouseWorkerStoreEntityId(",
                "GetEntityWithWorkerTrolleyStoreEntityId(",
                "_solvency.EvaluateDebit(",
                "_workers.Create(",
                "_workerTrolleys.Create(",
                "GetSpawnPoint(SpawnPointId.WarehouseWorker)",
                "GetSpawnPoint(SpawnPointId.WarehouseWorkerDeliveryAccess)",
                "GetSpawnPoint(SpawnPointId.WarehouseWorkerStorageAccess)",
                "SpawnPointId.WarehouseWorkerCustomerLoadingAccess",
                "GetSpawnPoint(SpawnPointId.WarehouseWorkerTrolley)",
                "SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess",
                "!trolley.isWorkerTrolley || trolley.isPlatformTrolley",
                "trolley.isInteractable",
                "trolley.TrolleyCapacity != _config.TrolleyCapacity",
                "trolley.OccupiedTrolleySlotCount != 0",
                "trolley.TrolleyFollowDistance != _config.TrolleyFollowDistance",
                "trolley.hasTrolleyPusherEntityId",
                "store.ReplaceDayUpgradeExpenses(upgradeExpensesAfterHire)",
                "worker.isWorkerShiftActive = true");
            RequireSourceOrder(
                hireSource,
                "GetEntityWithWarehouseWorkerStoreEntityId(",
                "_workers.Create(",
                "Repeated hire must be rejected before creating another worker.");
            RequireSourceOrder(
                hireSource,
                "GetEntityWithWarehouseWorkerStoreEntityId(",
                "_workerTrolleys.Create(",
                "Repeated hire must be rejected before creating another worker trolley.");
            RequireSourceOrder(
                hireSource,
                "_workerTrolleys.Create(",
                "store.ReplaceMoney(moneyAfterHire)",
                "The bundled worker trolley must be validated before the hire debit commits.");
            string paySource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "PayWarehouseWorkerShiftSystem.cs");
            RequireSourceContains(paySource,
                "_solvency.EvaluateDebit(",
                "store.DayPayrollExpenses + _config.DailyWage",
                "store.ReplaceDayPayrollExpenses(payrollExpensesAfterPayment)",
                "worker.ReplaceWorkerPaidDayNumber(store.DayNumber)",
                "worker.isWorkerShiftActive = true");
            RequireSourceOrder(
                paySource,
                "if (!debit.CanDebit)",
                "store.ReplaceDayPayrollExpenses(payrollExpensesAfterPayment)",
                "Rejected wage payment must leave payroll and money untouched.");

            string promptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveStoreControlTerminalPromptSystem.cs");
            RequireSourceContains(promptSource,
                "LocalizationKey.PromptWarehouseWorkerLocked",
                "LocalizationKey.PromptHireWarehouseWorker",
                "LocalizationKey.PromptWarehouseWorkerActive",
                "LocalizationKey.PromptPayWarehouseWorkerShift",
                "LocalizationKey.PromptCloseStoreWarehouseWorkerBusy",
                "IsTasklessEmptyTrolleyReturn(worker, workerTask)",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "GetEntityWithTrolleyPusherEntityId(worker.EntityId)",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "_solvency.EvaluateDebit(");
            string reportSource = ReadRuntimeSource(
                "Gameplay", "Features", "StoreDay", "Systems",
                "OpenDayReportSystem.cs");
            RequireSourceContains(reportSource,
                "GameMatcher.WarehouseTask",
                "WarehouseTaskStepId.Blocked",
                "HasActiveWarehouseWork(store)",
                "task.WarehouseTaskStep != WarehouseTaskStepId.Blocked",
                "IsTasklessEmptyTrolleyReturn(worker)",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "GetEntityWithTrolleyPusherEntityId(worker.EntityId)",
                "trolley.OccupiedTrolleySlotCount != 0",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "GetEntityWithAssignedWorkerEntityId(",
                "worker.isHandsOccupied || worker.isCarryingProduct",
                "GetEntityWithCarrierEntityId(worker.EntityId)");

            string generateTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "GenerateInboundStorageTaskSystem.cs");
            RequireSourceContains(generateTaskSource,
                "FindCustomerPrerequisiteProduct(store)",
                "prerequisite != null || store.isStoreClosing",
                "line.LineIndex < selectedLine.LineIndex",
                "linkedCount + reservedCount >= line.RequiredProductCount",
                "HasShelfProduct(line)",
                "CompareInboundProduct(inbound, selectedProduct) < 0",
                "left.DeliverySlotIndex.CompareTo(",
                "left.EntityId.CompareTo(right.EntityId)",
                "task.WarehouseTaskReservedStorageSlotIndex",
                "WarehouseWorkerStatusId.StorageFull",
                "_tasksFactory.CreateInboundToStorage(",
                "product.isInteractable = false");
            string generateCustomerLoadingTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "GenerateCustomerLoadingTaskSystem.cs");
            RequireSourceContains(generateCustomerLoadingTaskSource,
                "GetEntityWithReservedCustomerLoadingBayEntityId(",
                "visit.isCustomerVisitLoading",
                "_orderLines.Sort(CompareOrderLines)",
                "left.LineIndex.CompareTo(right.LineIndex)",
                "left.StorageSlotIndex.CompareTo(",
                "HasValidPlayerRequest(product.EntityId, storeEntityId)",
                "int playerClaimCount = ClaimPendingPlayerRequests(",
                "remaining -= playerClaimCount",
                "!source.isHandsOccupied",
                "_selectedProductIds.Add(product.EntityId)",
                "GameMatcher.LooseProduct",
                "GameMatcher.CarrierEntityId",
                "GameMatcher.TrolleyEntityId",
                "GameMatcher.WorkerTrolleyEntityId",
                "GameMatcher.ReservedCustomerLoadingSlotIndex",
                "GameMatcher.WarehouseRunEntityId",
                "product.RemoveStorageSlotIndex()",
                "product.AddReservedStorageSlotIndex(storageSlotIndex)",
                "product.AddReservedOrderLineEntityId(candidate.Line.EntityId)",
                "if (_batchCandidates.Count >= 2)",
                "else if (_batchCandidates.Count == 1 && !returning)",
                "_tasksFactory.CreateWorkerTrolleyCustomerLoadingRun(",
                "run.AddWarehouseRunProductCount(_batchCandidates.Count)",
                "product.AddReservedCustomerLoadingSlotIndex(",
                "product.AddWarehouseRunEntityId(run.EntityId)",
                "_tasksFactory.CreateStockToCustomerLoading(",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "HasActiveTask(store.EntityId)",
                "WarehouseWorkerStatusId.StorageFull",
                "WarehouseWorkerStatusId.Idle",
                "!store.isStoreOpen && !store.isStoreClosing");
            RequireSourceOrder(
                generateCustomerLoadingTaskSource,
                "ClaimPendingPlayerRequests(",
                "_matchingProducts.Clear()",
                "Pending player shelf claims must consume line quota before worker " +
                "alternatives are collected.");
            string assignTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "AssignWarehouseTaskSystem.cs");
            RequireSourceContains(assignTaskSource,
                "left.isStockToCustomerLoadingTask ||",
                "left.isWorkerTrolleyCustomerLoadingRun ? 0 : 1",
                "right.isStockToCustomerLoadingTask ||",
                "right.isWorkerTrolleyCustomerLoadingRun ? 0 : 1",
                "left.EntityId.CompareTo(right.EntityId)",
                "selected.AddAssignedWorkerEntityId(worker.EntityId)",
                "WarehouseTaskStepId.MovingToWorkerTrolley",
                "WarehouseWorkerStatusId.MovingToWorkerTrolley",
                "returningTrolley &&",
                "!selected.isWorkerTrolleyCustomerLoadingRun",
                "WarehouseTaskStepId.MovingToPickup",
                "!store.isStoreOpen && !store.isStoreClosing");
            string tickTaskTimeoutSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "TickWarehouseTaskTimeoutSystem.cs");
            RequireSourceContains(tickTaskTimeoutSource,
                "task.isWorkerTrolleyCustomerLoadingRun ? 1 : 0",
                "task.WarehouseTaskTimeoutRemaining",
                "current - _time.DeltaTime",
                "WarehouseTaskStepId.Blocked",
                "WarehouseTaskBlockReasonId.TimedOut");
            string executeInboundTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ExecuteInboundStorageTaskSystem.cs");
            RequireSourceContains(executeInboundTaskSource,
                "_tasks.GetEntities(_buffer)",
                "product.RemoveDeliverySlotIndex()",
                "product.AddReservedDeliverySlotIndex(deliverySlotIndex)",
                "product.AddCarrierEntityId(worker.EntityId)",
                "product.RemoveCarrierEntityId()",
                "product.isInboundProduct = false",
                "product.isInStock = true",
                "product.AddStorageSlotIndex(slotIndex)",
                "product.isProductStocked = true",
                "task.isDestructed = true");
            string executeCustomerLoadingTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ExecuteCustomerLoadingTaskSystem.cs");
            RequireSourceContains(executeCustomerLoadingTaskSource,
                "_tasks.GetEntities(_buffer)",
                "WarehouseTaskStepId.MovingToCustomerLoading",
                "WarehouseWorkerStatusId.MovingToCustomerLoading",
                "worker.WarehouseWorkerCustomerLoadingPosition",
                "worker.WarehouseWorkerCustomerLoadingRotation",
                "WarehouseTaskBlockReasonId.NoCustomerLoadingPath",
                "task.WarehouseTaskReservedLoadingSlotIndex",
                "product.RemoveCarrierEntityId()",
                "product.isInStock = false",
                "product.isLoaded = true",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddOrderLineEntityId(line.EntityId)",
                "product.AddLoadingSlotIndex(slotIndex)",
                "product.isProductLoaded = true",
                "task.isDestructed = true");
            string executeWorkerTrolleyRunSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ExecuteWorkerTrolleyRunSystem.cs");
            RequireSourceContains(executeWorkerTrolleyRunSource,
                "GameMatcher.WorkerTrolleyCustomerLoadingRun",
                "GameMatcher.WarehouseTaskWorkerTrolleyEntityId",
                "GameMatcher.WarehouseRunProductCount",
                "WarehouseTaskStepId.MovingToWorkerTrolley",
                "WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading",
                "_gameContext.GetEntitiesWithWarehouseRunEntityId(run.EntityId)",
                "_products.Count < 2 || _products.Count > trolley.TrolleyCapacity",
                "PusherTarget(trolley, cartTarget)",
                "_navigation.HasReachedDestination(",
                "_navigation.TrySetDestination(",
                "_navigation.SetAutomaticRotation(",
                "enabled: false",
                "enabled: true",
                "trolley.AddTrolleyPusherEntityId(worker.EntityId)",
                "worker.isPushingWorkerTrolley = true",
                "product.AddWorkerTrolleyEntityId(trolley.EntityId)",
                "product.AddWorkerTrolleySlotIndex(index)",
                "trolley.ReplaceOccupiedTrolleySlotCount(_products.Count)",
                "product.ReservedCustomerLoadingSlotIndex",
                "product.RemoveWorkerTrolleyEntityId()",
                "product.RemoveWarehouseRunEntityId()",
                "product.AddOrderLineEntityId(line.EntityId)",
                "product.AddLoadingSlotIndex(loadingSlotIndex)",
                "product.isProductLoaded = true",
                "trolley.ReplaceOccupiedTrolleySlotCount(0)",
                "run.RemoveAssignedWorkerEntityId()",
                "run.isDestructed = true",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley");
            Require(!executeWorkerTrolleyRunSource.Contains(
                        ".isOnNavMesh", StringComparison.Ordinal) &&
                    !executeWorkerTrolleyRunSource.Contains(
                        ".hasPath", StringComparison.Ordinal),
                "The worker-trolley run executor must keep NavMesh state behind the " +
                "worker navigation service.");
            string returnWorkerTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ReturnWorkerTrolleySystem.cs");
            RequireSourceContains(returnWorkerTrolleySource,
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "GetEntityWithAssignedWorkerEntityId(",
                "GetEntityWithTrolleyPusherEntityId(worker.EntityId)",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "trolley.OccupiedTrolleySlotCount != 0",
                "if (store.isDayReportOpen)",
                "CompleteReturn(worker, trolley, homePose)",
                "_navigation.TrySetDestination(",
                "_navigation.SetAutomaticRotation(",
                "enabled: false",
                "enabled: true",
                "trolley.Rigidbody.position = homePose.position",
                "trolley.RemoveTrolleyPusherEntityId()",
                "worker.isPushingWorkerTrolley = false",
                "worker.isHandsOccupied = false",
                "WarehouseWorkerStatusId.Idle",
                "WarehouseWorkerStatusId.OffShift");
            string recoverBlockedWorkerTrolleyRunSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "RecoverBlockedWorkerTrolleyRunSystem.cs");
            RequireSourceContains(recoverBlockedWorkerTrolleyRunSource,
                "GameMatcher.WorkerTrolleyCustomerLoadingRun",
                "WarehouseTaskStepId.Blocked",
                "GetEntitiesWithWarehouseRunEntityId(run.EntityId)",
                "_products.Count != run.WarehouseRunProductCount",
                "product.hasReservedStorageSlotIndex",
                "product.hasReservedCustomerLoadingSlotIndex",
                "product.hasWorkerTrolleyEntityId",
                "int storageSlotIndex = product.ReservedStorageSlotIndex",
                "product.RemoveWorkerTrolleyEntityId()",
                "product.RemoveWorkerTrolleySlotIndex()",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.RemoveReservedCustomerLoadingSlotIndex()",
                "product.AddStorageSlotIndex(storageSlotIndex)",
                "product.isInteractable = true",
                "trolley.ReplaceOccupiedTrolleySlotCount(0)",
                "trolley.WorkerTrolleyHomePosition",
                "trolley.RemoveTrolleyPusherEntityId()",
                "_navigation.SetAutomaticRotation(",
                "enabled: true",
                "worker.isPushingWorkerTrolley = false",
                "WarehouseWorkerStatusId.Blocked",
                "run.RemoveAssignedWorkerEntityId()");
            Require(!executeWorkerTrolleyRunSource.Contains(
                        ".updateRotation", StringComparison.Ordinal) &&
                    !returnWorkerTrolleySource.Contains(
                        ".updateRotation", StringComparison.Ordinal) &&
                    !recoverBlockedWorkerTrolleyRunSource.Contains(
                        ".updateRotation", StringComparison.Ordinal),
                "Worker-trolley execute, return and recovery systems must keep NavMesh " +
                "rotation control behind IWorkerNavigationService.");
            Require(CountOccurrences(executeWorkerTrolleyRunSource,
                        "enabled: false") == 1 &&
                    CountOccurrences(executeWorkerTrolleyRunSource,
                        "enabled: true") == 1 &&
                    CountOccurrences(returnWorkerTrolleySource,
                        "enabled: false") == 1 &&
                    CountOccurrences(returnWorkerTrolleySource,
                        "enabled: true") == 2 &&
                    CountOccurrences(recoverBlockedWorkerTrolleyRunSource,
                        "enabled: true") == 1,
                "Worker-trolley preparation/return/recovery must freeze backward-return " +
                "orientation and restore automatic rotation only at its exact handoff points.");
            string detectOrphanedWorkerTrolleyRunSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "DetectOrphanedWorkerTrolleyRunSystem.cs");
            RequireSourceContains(detectOrphanedWorkerTrolleyRunSource,
                "GameMatcher.WorkerTrolleyCustomerLoadingRun",
                "GetEntityWithWarehouseWorkerStoreEntityId(",
                "WarehouseTaskBlockReasonId.WorkerMissing",
                "run.WarehouseTaskWorkerTrolleyEntityId",
                "WarehouseTaskBlockReasonId.WorkerTrolleyMissing",
                "run.WarehouseTaskCustomerVisitEntityId",
                "WarehouseTaskBlockReasonId.NoCustomerLoadingPath",
                "run.ReplaceWarehouseTaskStep(WarehouseTaskStepId.Blocked)");
            string cleanupBlockedWorkerTrolleyRunSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "CleanupBlockedWorkerTrolleyRunSystem.cs");
            RequireSourceContains(cleanupBlockedWorkerTrolleyRunSource,
                "GameMatcher.WorkerTrolleyCustomerLoadingRun",
                "WarehouseTaskStepId.Blocked",
                "recoveryPending |= product.hasReservedStorageSlotIndex",
                "product.hasReservedCustomerLoadingSlotIndex",
                "product.hasWorkerTrolleyEntityId",
                "ShouldAwaitManualHandoff(run)",
                "product.RemoveWarehouseRunEntityId()",
                "run.isDestructed = true",
                "WarehouseWorkerStatusId.Blocked",
                "WarehouseWorkerStatusId.Idle",
                "WarehouseWorkerStatusId.OffShift");
            string workerTrolleyPlacementSource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "Systems",
                "ApplyWorkerTrolleyProductPlacementSystem.cs");
            RequireSourceContains(workerTrolleyPlacementSource,
                "GameMatcher.WorkerTrolleyEntityId",
                "GameMatcher.WorkerTrolleySlotIndex",
                "GameMatcher.WarehouseRunEntityId",
                "GameMatcher.ReservedCustomerLoadingSlotIndex",
                "trolley.Slots[product.WorkerTrolleySlotIndex]",
                "ProductPhysicsUtility.ConfigureLockedSlot(",
                "product.isProductPlacementDirty = false");
            string productPlacementFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Products", "ProductPlacementFeature.cs");
            RequireSourceOrder(
                productPlacementFeatureSource,
                "Create<ApplyTrolleyProductPlacementSystem>()",
                "Create<ApplyWorkerTrolleyProductPlacementSystem>()",
                "Player-trolley placement must settle before worker-trolley placement.");
            RequireSourceOrder(
                productPlacementFeatureSource,
                "Create<ApplyWorkerTrolleyProductPlacementSystem>()",
                "Create<ValidateProductPlacementSystem>()",
                "Worker-trolley placement must settle before placement validation.");
            string refreshWorkerTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "RefreshWorkerTrolleyOccupiedSlotCountSystem.cs");
            RequireSourceContains(refreshWorkerTrolleySource,
                "new bool[config.TrolleyCapacity]",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "product.WorkerTrolleySlotIndex",
                "trolley.ReplaceOccupiedTrolleySlotCount(count)");
            string validateWorkerTrolleySource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ValidateWorkerTrolleyStateSystem.cs");
            RequireSourceContains(validateWorkerTrolleySource,
                "GameMatcher.WorkerTrolley",
                "GameMatcher.WorkerTrolleyStoreEntityId",
                "GameMatcher.WorkerTrolleyCustomerLoadingPosition",
                "trolley.isPlatformTrolley || trolley.isInteractable",
                "trolley.TrolleyCapacity != _config.TrolleyCapacity",
                "trolley.TrolleyFollowDistance != _config.TrolleyFollowDistance",
                "GetEntitiesWithWorkerTrolleyEntityId(",
                "product.WorkerTrolleySlotIndex",
                "product.hasWarehouseRunEntityId",
                "product.hasReservedCustomerLoadingSlotIndex",
                "run.WarehouseTaskWorkerTrolleyEntityId != trolley.EntityId",
                "trolley.TrolleyPusherEntityId",
                "worker.isPushingWorkerTrolley",
                "MovingToWorkerTrolley or",
                "MovingWorkerTrolleyToCustomerLoading or",
                "ReturningWorkerTrolley");
            string detectOrphanedTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "DetectOrphanedWarehouseTaskSystem.cs");
            RequireSourceContains(detectOrphanedTaskSource,
                "task.isInboundToStorageTask ==",
                "task.isStockToCustomerLoadingTask",
                "worker == null || worker.isDestructed",
                "WarehouseTaskBlockReasonId.WorkerMissing");
            string recoverInboundTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "RecoverBlockedInboundTaskSystem.cs");
            RequireSourceContains(recoverInboundTaskSource,
                "GameMatcher.InboundToStorageTask",
                "RestoreProductToDeliverySlot(product, workerEntityId)",
                "int slotIndex = product.ReservedDeliverySlotIndex",
                "product.RemoveCarrierEntityId()",
                "product.RemoveReservedDeliverySlotIndex()",
                "product.AddDeliverySlotIndex(slotIndex)",
                "task.RemoveAssignedWorkerEntityId()",
                "task.RemoveWarehouseTaskReservedStorageSlotIndex()",
                "LocalizationKey.NotificationWarehouseWorkerTaskBlocked");
            string recoverCustomerLoadingTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "RecoverBlockedCustomerLoadingTaskSystem.cs");
            RequireSourceContains(recoverCustomerLoadingTaskSource,
                "GameMatcher.StockToCustomerLoadingTask",
                "RestoreProductToShelf(product, workerEntityId)",
                "int storageSlotIndex = product.ReservedStorageSlotIndex",
                "product.RemoveCarrierEntityId()",
                "product.RemoveReservedStorageSlotIndex()",
                "product.RemoveReservedOrderLineEntityId()",
                "product.AddStorageSlotIndex(storageSlotIndex)",
                "task.RemoveAssignedWorkerEntityId()",
                "task.RemoveWarehouseTaskReservedLoadingSlotIndex()",
                "LocalizationKey.NotificationWarehouseWorkerTaskBlocked");
            string cleanupBlockedTaskSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "CleanupBlockedWarehouseTaskSystem.cs");
            RequireSourceContains(cleanupBlockedTaskSource,
                "task.isInboundToStorageTask == task.isStockToCustomerLoadingTask",
                "ShouldAwaitCustomerHandoff(task, product)",
                "visit.isCustomerVisitLoading",
                "line.LoadedProductCount < line.RequiredProductCount",
                "task.isDestructed = true",
                "ResetWorker(task)");
            string validateWorkerSource = ReadRuntimeSource(
                "Gameplay", "Features", "Employees", "Systems",
                "ValidateWarehouseWorkerStateSystem.cs");
            RequireSourceContains(validateWorkerSource,
                "moving != (task != null)",
                "worker.isCarryingProduct && worker.isPushingWorkerTrolley",
                "worker.isHandsOccupied !=",
                "worker.isCarryingProduct != (carried != null)",
                "worker.isPushingWorkerTrolley != (pushed != null)",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley",
                "task.isWorkerTrolleyCustomerLoadingRun",
                "ValidateWorkerTrolleyRun(task)",
                "run.WarehouseRunProductCount < 2",
                "GetEntitiesWithWarehouseRunEntityId(run.EntityId)",
                "product.ReservedCustomerLoadingSlotIndex",
                "product.WorkerTrolleySlotIndex",
                "ValidateCustomerLoadingTask(task, product)",
                "WarehouseTaskStepId.MovingToCustomerLoading",
                "WarehouseTaskStepId.MovingToWorkerTrolley",
                "WarehouseTaskStepId.MovingWorkerTrolleyToCustomerLoading",
                "case WarehouseTaskStepId.Blocked:",
                "task.hasWarehouseTaskReservedStorageSlotIndex",
                "task.hasWarehouseTaskReservedLoadingSlotIndex",
                "GetEntitiesWithWarehouseTaskCustomerVisitEntityId(");
            string storeInboundSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "StoreInboundProductSystem.cs");
            RequireSourceContains(storeInboundSource,
                "GameMatcher.WarehouseTaskReservedStorageSlotIndex",
                "task.WarehouseTaskStorageZoneEntityId",
                "task.WarehouseTaskReservedStorageSlotIndex",
                "task.WarehouseTaskProductEntityId");
            string pickupSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "PickUpProductSystem.cs");
            RequireSourceOrder(
                pickupSource,
                "if (!product.isInteractable)",
                "CanPickUpInbound(product, player)",
                "A player request must reject worker-reserved inbound cargo before pickup " +
                "eligibility is evaluated.");
            string loadHeldProductSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "Systems",
                "LoadHeldProductSystem.cs");
            RequireSourceContains(loadHeldProductSource,
                "FindFreeLoadingSlot(visit)",
                "GetEntitiesWithWarehouseTaskCustomerVisitEntityId(",
                "task.WarehouseTaskReservedLoadingSlotIndex",
                "ReserveLoadingSlot(visit,",
                "product.isProductLoaded = true");
            string registerLoadedProductSource = ReadRuntimeSource(
                "Gameplay", "Features", "Orders", "Systems",
                "RegisterLoadedProductSystem.cs");
            RequireSourceContains(registerLoadedProductSource,
                "PendingProductComparison",
                "_buffer.Sort(PendingProductComparison)",
                "RegisterGroup(groupStart, groupEnd)",
                "int pendingCount = groupEnd - groupStart",
                "loaded + pendingCount > required",
                "product.isProductLoaded = false");
            string lateCarryingSource = ReadRuntimeSource(
                "Gameplay", "Features", "Carrying", "LateCarryingFeature.cs");
            RequireSourceOrder(
                lateCarryingSource,
                "Create<FollowHeldProductSystem>()",
                "Create<FollowWorkerCarriedProductSystem>()",
                "Player and worker carried-product views must have separate ordered adapters.");
            RequireSourceOrder(
                lateCarryingSource,
                "Create<FollowWorkerCarriedProductSystem>()",
                "Create<SyncLooseProductPoseSystem>()",
                "Worker carry following must settle before loose-product pose synchronization.");

            Require(typeof(WarehouseWorkerStatusSnapshot).GetConstructor(new[]
                    {
                        typeof(WarehouseWorkerStatusId),
                        typeof(ProductTypeId?),
                        typeof(int?)
                    }) != null,
                $"{nameof(WarehouseWorkerStatusSnapshot)} must expose direct-product and " +
                "worker-trolley batch state explicitly.");
            ValidateSnapshotProperties(
                typeof(WarehouseWorkerStatusSnapshot),
                (nameof(WarehouseWorkerStatusSnapshot.Status),
                    typeof(WarehouseWorkerStatusId)),
                (nameof(WarehouseWorkerStatusSnapshot.ProductType),
                    typeof(ProductTypeId?)),
                (nameof(WarehouseWorkerStatusSnapshot.BatchProductCount),
                    typeof(int?)));
            ValidateImmutableSnapshotType(typeof(WarehouseWorkerStatusSnapshot));
            string workerStatusSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation",
                nameof(WarehouseWorkerStatusSnapshot) + ".cs");
            RequireSourceContains(workerStatusSnapshotSource,
                "WarehouseWorkerStatusId.MovingToPickup",
                "WarehouseWorkerStatusId.MovingToStorage",
                "WarehouseWorkerStatusId.MovingToCustomerLoading",
                "WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading",
                "BatchProductCount",
                "A moving warehouse worker requires a valid task product.");
            string presentHudSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentHudSystem.cs");
            RequireSourceContains(presentHudSource,
                "WarehouseWorkerStatusId.MovingToCustomerLoading",
                "WarehouseWorkerStatusId.MovingToWorkerTrolley",
                "WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading",
                "task.WarehouseRunProductCount",
                "new WarehouseWorkerStatusSnapshot(status, product.ProductType)");
            string prototypeHudSource = ReadRuntimeSource(
                "Gameplay", "Presentation", "PrototypeHudView.cs");
            RequireSourceContains(prototypeHudSource,
                "WarehouseWorkerStatusId.MovingToCustomerLoading => Resolve(",
                "LocalizationKey.HudWarehouseWorkerMovingToCustomerLoading",
                "LocalizedTexts.ProductName(snapshot.ProductType.Value)",
                "WarehouseWorkerStatusId.MovingToWorkerTrolley => Resolve(",
                "LocalizationKey.HudWarehouseWorkerMovingToWorkerTrolley",
                "WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading =>",
                "LocalizationKey.HudWarehouseWorkerMovingWorkerTrolleyToCustomerLoading",
                "snapshot.BatchProductCount.Value",
                "WarehouseWorkerStatusId.ReturningWorkerTrolley => Resolve(",
                "LocalizationKey.HudWarehouseWorkerReturningWorkerTrolley");

            var workerLocalizationArities = new Dictionary<LocalizationKey, int>
            {
                { LocalizationKey.HudDayReportPayrollExpenses, 1 },
                { LocalizationKey.HudWarehouseWorkerIdle, 0 },
                { LocalizationKey.HudWarehouseWorkerStorageFull, 0 },
                { LocalizationKey.HudWarehouseWorkerMovingToPickup, 1 },
                { LocalizationKey.HudWarehouseWorkerMovingToStorage, 1 },
                { LocalizationKey.HudWarehouseWorkerMovingToCustomerLoading, 1 },
                { LocalizationKey.HudWarehouseWorkerMovingToWorkerTrolley, 0 },
                { LocalizationKey.HudWarehouseWorkerMovingWorkerTrolleyToCustomerLoading, 1 },
                { LocalizationKey.HudWarehouseWorkerReturningWorkerTrolley, 0 },
                { LocalizationKey.HudWarehouseWorkerBlocked, 0 },
                { LocalizationKey.HudWarehouseWorkerOffShift, 0 },
                { LocalizationKey.PromptWarehouseWorkerLocked, 2 },
                { LocalizationKey.PromptHireWarehouseWorker, 2 },
                { LocalizationKey.PromptWarehouseWorkerHireInsufficientMoney, 1 },
                { LocalizationKey.PromptWarehouseWorkerHireWouldBlockProjects, 0 },
                { LocalizationKey.PromptWarehouseWorkerActive, 1 },
                { LocalizationKey.PromptPayWarehouseWorkerShift, 1 },
                { LocalizationKey.PromptWarehouseWorkerWageInsufficientMoney, 1 },
                { LocalizationKey.PromptWarehouseWorkerWageWouldBlockProjects, 0 },
                { LocalizationKey.PromptCloseStoreWarehouseWorkerBusy, 0 },
                { LocalizationKey.NotificationWarehouseWorkerUnlocked, 2 },
                { LocalizationKey.NotificationWarehouseWorkerHired, 2 },
                { LocalizationKey.NotificationWarehouseWorkerShiftPaid, 1 },
                { LocalizationKey.NotificationWarehouseWorkerTaskBlocked, 1 }
            };
            Dictionary<LocalizationKey, LocalizationEntry> localizationEntries =
                new RussianLocalizationCatalog().Entries.ToDictionary(entry => entry.Key);
            foreach ((LocalizationKey key, int argumentCount) in workerLocalizationArities)
            {
                Require(localizationEntries.TryGetValue(key, out LocalizationEntry entry) &&
                        entry.ArgumentCount == argumentCount,
                    $"Russian warehouse-worker localization {key} must exist with arity " +
                    $"{argumentCount}.");
            }
            Require(localizationEntries[
                        LocalizationKey.HudWarehouseWorkerMovingToCustomerLoading].Template ==
                    "Грузчик несёт в машину клиента: {0}",
                "The customer-loading worker HUD status must remain concise and explicit.");
            Require(localizationEntries[
                        LocalizationKey.HudWarehouseWorkerMovingToWorkerTrolley].Template ==
                    "Грузчик готовит тележку к погрузке" &&
                    localizationEntries[
                        LocalizationKey
                            .HudWarehouseWorkerMovingWorkerTrolleyToCustomerLoading]
                        .Template ==
                    "Грузчик везёт заказ к машине клиента: {0} товара" &&
                    localizationEntries[
                        LocalizationKey.HudWarehouseWorkerReturningWorkerTrolley]
                        .Template ==
                    "Грузчик возвращает тележку",
                "Worker-trolley HUD phases must retain their concise frozen Russian text.");
        }

        private static void ValidateLocalizationArchitecture(Type[] runtimeTypes,
            Type[] componentTypes)
        {
            Require(typeof(ILocalizationService).IsAssignableFrom(typeof(LocalizationService)),
                $"{nameof(LocalizationService)} must implement {nameof(ILocalizationService)}.");
            Require(typeof(ILocalizationCatalog).IsAssignableFrom(
                    typeof(RussianLocalizationCatalog)),
                $"{nameof(RussianLocalizationCatalog)} must implement " +
                $"{nameof(ILocalizationCatalog)}.");

            ILocalizationCatalog catalog = new RussianLocalizationCatalog();
            var localization = new LocalizationService(new[] { catalog });
            try
            {
                localization.Load(LanguageId.Russian);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "ECS architecture validation failed: the Russian localization catalog " +
                    "must load with exact key coverage and valid argument arity.",
                    exception);
            }

            LocalizationKey[] expectedKeys = Enum.GetValues(typeof(LocalizationKey))
                .Cast<LocalizationKey>()
                .Where(key => key != LocalizationKey.None)
                .ToArray();
            string[] localizationKeyNames = Enum.GetNames(typeof(LocalizationKey));
            string[] removedWaitingLocalizationKeys =
            {
                "HudObjectiveWaitingForStock",
                "HudObjectiveWaitingReady",
                "NotificationOrderStockMissingOne",
                "NotificationOrderStockMissingTwo",
                "NotificationOrderAccepted"
            };
            Require(!removedWaitingLocalizationKeys.Any(removedKey =>
                    localizationKeyNames.Contains(removedKey, StringComparer.Ordinal)),
                "Direct offer confirmation must not retain localization keys for the removed " +
                "waiting/accept-order step.");
            int[] localizationKeyValues = localizationKeyNames
                .Select(name => (int)Enum.Parse(typeof(LocalizationKey), name))
                .ToArray();
            Require(localizationKeyValues.Distinct().Count() ==
                    localizationKeyValues.Length,
                $"Every {nameof(LocalizationKey)} member must have a unique stable value.");
            (string Prefix, int Minimum, int Maximum)[] expectedKeyRanges =
            {
                ("Product", 100, 199),
                ("Project", 200, 399),
                ("Hud", 1000, 1099),
                ("ProcurementStatus", 1100, 1199),
                ("Prompt", 2000, 2999),
                ("Notification", 3000, 3999),
                ("World", 4000, 4999)
            };
            foreach (LocalizationKey key in expectedKeys)
            {
                (string prefix, int minimum, int maximum) = expectedKeyRanges.Single(
                    range => key.ToString().StartsWith(
                        range.Prefix, StringComparison.Ordinal));
                int numericValue = (int)key;
                Require(numericValue >= minimum && numericValue <= maximum,
                    $"{nameof(LocalizationKey)}.{key} must stay in the {prefix} range " +
                    $"{minimum}-{maximum}.");
            }

            string localizationKeySource = ReadRuntimeSource(
                "Gameplay", "Localization", nameof(LocalizationKey) + ".cs");
            foreach (string memberName in localizationKeyNames)
            {
                Require(Regex.IsMatch(
                        localizationKeySource,
                        $@"^\s*{Regex.Escape(memberName)}\s*=\s*-?\d+\s*,?\s*$",
                        RegexOptions.Multiline),
                    $"{nameof(LocalizationKey)}.{memberName} must declare an explicit stable " +
                    "numeric initializer.");
            }
            Require(catalog.Language == LanguageId.Russian &&
                    catalog.Culture.Name == "ru-RU" &&
                    localization.Language == LanguageId.Russian &&
                    localization.Culture.Name == "ru-RU",
                "The initial localization catalog must expose the Russian language and ru-RU culture.");
            Require(catalog.Entries.Count == expectedKeys.Length &&
                    catalog.Entries.Select(entry => entry.Key).Distinct().Count() ==
                    expectedKeys.Length &&
                    new HashSet<LocalizationKey>(catalog.Entries.Select(entry => entry.Key))
                        .SetEquals(expectedKeys),
                "The Russian localization catalog must cover every non-None LocalizationKey " +
                "exactly once.");

            var mappedSemanticTexts = new List<LocalizedText>();
            foreach (ProductTypeId productType in ExpectedProductTypes)
            {
                mappedSemanticTexts.Add(LocalizedTexts.ProductName(productType));
                Require(LocalizedTexts.ProductUnit(productType).Key ==
                        LocalizationKey.ProductPieceUnit,
                    $"{nameof(LocalizedTexts)} must map every current product to its semantic unit.");
            }
            foreach (CustomerProjectTypeId projectType in ExpectedProjectTypes)
            {
                mappedSemanticTexts.Add(LocalizedTexts.ProjectTitle(projectType));
                mappedSemanticTexts.Add(LocalizedTexts.ProjectRequest(projectType));
                for (int offerIndex = 0; offerIndex < 3; offerIndex++)
                {
                    mappedSemanticTexts.Add(
                        LocalizedTexts.OfferTitle(projectType, offerIndex));
                    mappedSemanticTexts.Add(
                        LocalizedTexts.OfferDescription(projectType, offerIndex));
                }
            }
            Require(mappedSemanticTexts.Select(text => text.Key).Distinct().Count() ==
                    mappedSemanticTexts.Count &&
                    mappedSemanticTexts.All(text =>
                        !string.IsNullOrWhiteSpace(localization.Resolve(text))),
                $"{nameof(LocalizedTexts)} must map each semantic product/project/offer identity " +
                "to one resolvable catalog entry.");

            Type[] localizedMessageComponents =
            {
                typeof(InteractionPrompt),
                typeof(NotificationMessage)
            };
            foreach (Type componentType in localizedMessageComponents)
            {
                Require(componentTypes.Contains(componentType),
                    $"{componentType.Name} must remain an ECS component.");
                FieldInfo valueField = componentType.GetField(
                    "Value", BindingFlags.Instance | BindingFlags.Public);
                Require(valueField?.FieldType == typeof(LocalizedText),
                    $"{componentType.Name}.Value must carry {nameof(LocalizedText)}, not a " +
                    "resolved presentation string.");
            }

            string[] forbiddenContentComponentNames =
            {
                "CustomerProjectTitle",
                "GameCustomerProjectTitleComponent",
                "CustomerRequest",
                "GameCustomerRequestComponent",
                "OfferTitle",
                "GameOfferTitleComponent",
                "OfferDescription",
                "GameOfferDescriptionComponent"
            };
            Require(!runtimeTypes.Any(type => forbiddenContentComponentNames.Contains(
                        type.Name, StringComparer.Ordinal)),
                "Localized project and offer content must not be duplicated in ECS components.");

            RequireNoDeclaredMembers(
                typeof(ProductConfig),
                "DisplayName", "UnitLabel", "_displayName", "_unitLabel");
            RequireNoDeclaredMembers(
                typeof(CustomerProjectConfig),
                "ProjectTitle", "Request", "_title", "_request");
            RequireNoDeclaredMembers(
                typeof(CustomerProjectOfferDefinition),
                "OfferTitle", "Description", "_title", "_description");

            Type[] semanticSnapshotTypes =
            {
                typeof(OrderLineSnapshot),
                typeof(ConsultationOfferLineSnapshot),
                typeof(ConsultationOfferSnapshot),
                typeof(ConsultationSnapshot),
                typeof(ProcurementProductSnapshot),
                typeof(ProcurementSnapshot),
                typeof(HudSnapshot)
            };
            foreach (Type snapshotType in semanticSnapshotTypes)
            {
                PropertyInfo[] properties = snapshotType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                Require(properties.All(property => property.PropertyType != typeof(string)),
                    $"{snapshotType.Name} must expose semantic identities and values instead " +
                    "of resolved strings.");
            }
            Require(typeof(HudSnapshot).GetProperty(nameof(HudSnapshot.Prompt))?.PropertyType ==
                    typeof(LocalizedText),
                $"{nameof(HudSnapshot)}.{nameof(HudSnapshot.Prompt)} must preserve its " +
                "localization key and arguments until presentation.");

            string bootstrapInstallerSource = ReadRuntimeSource(
                "Infrastructure", "Installers", nameof(BootstrapInstaller) + ".cs");
            RequireSourceContains(bootstrapInstallerSource,
                "Bind<ILocalizationCatalog>().To<RussianLocalizationCatalog>().AsSingle()",
                "BindInterfacesAndSelfTo<LocalizationService>().AsSingle()");
            string bootstrapStateSource = ReadRuntimeSource(
                "Infrastructure", "States", "GameStates", "BootstrapState.cs");
            int localizationLoad = bootstrapStateSource.IndexOf(
                "_localization.Load(LanguageId.Russian)", StringComparison.Ordinal);
            int staticDataLoad = bootstrapStateSource.IndexOf(
                "_staticData.LoadAll()", StringComparison.Ordinal);
            Require(localizationLoad >= 0 && staticDataLoad > localizationLoad,
                "BootstrapState must load localization before static gameplay data.");

            string localizedWorldViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(LocalizedTextMeshView) + ".cs");
            RequireMethod(
                typeof(LocalizedTextMeshView),
                nameof(LocalizedTextMeshView.Configure),
                typeof(void),
                typeof(TextMesh),
                typeof(LocalizationKey),
                typeof(int[]));
            RequireSourceContains(localizedWorldViewSource,
                "[Inject]",
                "_localization.Resolve",
                "new LocalizedText(_key, arguments)");

            string catalogPath = GetRuntimeSourcePath(
                "Gameplay", "Localization", nameof(RussianLocalizationCatalog) + ".cs");
            foreach (string sourcePath in GetRuntimeSourcePaths())
            {
                if (PathsEqual(sourcePath, catalogPath))
                    continue;

                string source = File.ReadAllText(sourcePath);
                Require(!Regex.IsMatch(source, @"[\u0400-\u04FF]"),
                    $"Player-facing Cyrillic text must be declared only in " +
                    $"{nameof(RussianLocalizationCatalog)}; found it in {sourcePath}.");
            }
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
            PropertyInfo projectTypesProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.ProjectTypes),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(projectTypesProperty?.PropertyType ==
                    typeof(IReadOnlyList<CustomerProjectTypeId>),
                $"{nameof(IStaticDataService)} must expose a read-only " +
                $"{nameof(CustomerProjectTypeId)} catalog key list.");
            RequireMethod(staticDataType, nameof(IStaticDataService.GetProduct), typeof(ProductConfig),
                typeof(ProductTypeId));
            RequireMethod(staticDataType, nameof(IStaticDataService.GetDelivery), typeof(DeliveryConfig),
                typeof(ProductTypeId));
            RequireMethod(staticDataType, nameof(IStaticDataService.GetProject),
                typeof(CustomerProjectConfig), typeof(CustomerProjectTypeId));
            PropertyInfo customerProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.Customer),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(customerProperty?.PropertyType == typeof(CustomerConfig),
                $"{nameof(IStaticDataService)} must expose the validated {nameof(CustomerConfig)}.");
            PropertyInfo productRecoveryProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.ProductRecovery),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(productRecoveryProperty?.PropertyType == typeof(ProductRecoveryConfig),
                $"{nameof(IStaticDataService)} must expose the validated " +
                $"{nameof(ProductRecoveryConfig)}.");
            PropertyInfo platformTrolleyProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.PlatformTrolley),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(platformTrolleyProperty?.PropertyType == typeof(PlatformTrolleyConfig),
                $"{nameof(IStaticDataService)} must expose the validated " +
                $"{nameof(PlatformTrolleyConfig)}.");
            PropertyInfo warehouseWorkerProperty = staticDataType.GetProperty(
                nameof(IStaticDataService.WarehouseWorker),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(warehouseWorkerProperty?.PropertyType == typeof(WarehouseWorkerConfig),
                $"{nameof(IStaticDataService)} must expose the validated " +
                $"{nameof(WarehouseWorkerConfig)}.");

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

            ValidateConfigCatalogAssets<ProductConfig>(ExpectedProductTypes.Length);
            ValidateConfigCatalogAssets<DeliveryConfig>(ExpectedProductTypes.Length);
            ValidateConfigCatalogAssets<CustomerProjectConfig>(ExpectedProjectTypes.Length);
            Require(CustomerProjectConfig.MaxLinesPerOffer == 2,
                $"{nameof(CustomerProjectConfig)}.{nameof(CustomerProjectConfig.MaxLinesPerOffer)} " +
                "must match the two-line mixed-project presentation contract.");

            Require(typeof(CustomerProjectOfferDefinition).IsSealed &&
                    typeof(CustomerProjectOfferDefinition).IsSerializable,
                $"{nameof(CustomerProjectOfferDefinition)} must be a serializable sealed " +
                "value definition.");
            Require(typeof(CustomerProjectOfferDefinition).GetConstructor(new[]
                    {
                        typeof(CustomerProjectLineDefinition[])
                    }) != null,
                $"{nameof(CustomerProjectOfferDefinition)} must expose only semantic line " +
                "definitions without localized content or a serialized reward.");
            Require(typeof(CustomerProjectLineDefinition).IsSealed &&
                    typeof(CustomerProjectLineDefinition).IsSerializable,
                $"{nameof(CustomerProjectLineDefinition)} must be a serializable sealed " +
                "value definition.");
            Require(typeof(CustomerProjectLineDefinition).GetConstructor(new[]
                    {
                        typeof(ProductTypeId),
                        typeof(int)
                    }) != null,
                $"{nameof(CustomerProjectLineDefinition)} must expose product type and count.");
            PropertyInfo offersProperty = typeof(CustomerProjectConfig).GetProperty(
                nameof(CustomerProjectConfig.Offers),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(offersProperty?.PropertyType ==
                    typeof(IReadOnlyList<CustomerProjectOfferDefinition>),
                $"{nameof(CustomerProjectConfig)}.{nameof(CustomerProjectConfig.Offers)} must " +
                "expose a read-only list.");
            PropertyInfo linesProperty = typeof(CustomerProjectOfferDefinition).GetProperty(
                nameof(CustomerProjectOfferDefinition.Lines),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(linesProperty?.PropertyType ==
                    typeof(IReadOnlyList<CustomerProjectLineDefinition>),
                $"{nameof(CustomerProjectOfferDefinition)}.{nameof(CustomerProjectOfferDefinition.Lines)} " +
                "must expose a read-only list.");
            RequireMethod(
                typeof(CustomerProjectConfig),
                nameof(CustomerProjectConfig.Configure),
                typeof(void),
                typeof(CustomerProjectTypeId),
                typeof(int),
                typeof(CustomerProjectOfferDefinition[]));

            string projectConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(CustomerProjectConfig) + ".cs");
            RequireSourceContains(projectConfigSource,
                "public const int MaxLinesPerOffer = 2",
                "_lines.Length > CustomerProjectConfig.MaxLinesPerOffer",
                "(CustomerProjectOfferDefinition[])offers.Clone()",
                "(CustomerProjectLineDefinition[])lines.Clone()",
                "duplicate product type",
                "Validate();");
            Require(!projectConfigSource.Contains("Reward", StringComparison.Ordinal),
                $"{nameof(CustomerProjectConfig)} must not serialize a reward; revenue is " +
                "derived from product prices.");
            Require(!projectConfigSource.Contains("ProjectTitle", StringComparison.Ordinal) &&
                    !projectConfigSource.Contains("OfferTitle", StringComparison.Ordinal) &&
                    !projectConfigSource.Contains("Description", StringComparison.Ordinal) &&
                    !Regex.IsMatch(projectConfigSource, @"\bRequest\b|_request\b"),
                $"{nameof(CustomerProjectConfig)} must contain only semantic project and offer " +
                "configuration.");
            string productConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(ProductConfig) + ".cs");
            Require(!productConfigSource.Contains("DisplayName", StringComparison.Ordinal) &&
                    !productConfigSource.Contains("UnitLabel", StringComparison.Ordinal) &&
                    !productConfigSource.Contains("_displayName", StringComparison.Ordinal) &&
                    !productConfigSource.Contains("_unitLabel", StringComparison.Ordinal),
                $"{nameof(ProductConfig)} must not contain localized product text.");

            string customerVehicleConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(CustomerVehicleConfig) + ".cs");
            int lastConfigurationAssignment = customerVehicleConfigSource.IndexOf(
                "_cargoCapacity = cargoCapacity",
                StringComparison.Ordinal);
            int explicitConfigurationValidation = customerVehicleConfigSource.IndexOf(
                "Validate();",
                lastConfigurationAssignment >= 0 ? lastConfigurationAssignment : 0,
                StringComparison.Ordinal);
            Require(lastConfigurationAssignment >= 0 &&
                    explicitConfigurationValidation > lastConfigurationAssignment,
                $"{nameof(CustomerVehicleConfig)}.Configure must assign all values and then call Validate().");

            RequireMethod(
                typeof(CustomerFlowConfig),
                nameof(CustomerFlowConfig.Configure),
                typeof(void),
                typeof(int),
                typeof(float),
                typeof(CustomerArrivalSchedulePoint[]));
            RequireMethod(
                typeof(CustomerFlowConfig),
                nameof(CustomerFlowConfig.Configure),
                typeof(void),
                typeof(int),
                typeof(float),
                typeof(CustomerArrivalSchedulePoint[]),
                typeof(float),
                typeof(float));
            Require(typeof(CustomerArrivalSchedulePoint).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(float)
                    }) != null,
                $"{nameof(CustomerArrivalSchedulePoint)} must expose minute and delay through " +
                "its public value constructor.");
            string customerFlowConfigSource = ReadRuntimeSource(
                "Gameplay", "Configs", nameof(CustomerFlowConfig) + ".cs");
            RequireSourceContains(
                customerFlowConfigSource,
                "private float _defaultPatienceDuration = 120f",
                "private float _patienceWarningThreshold = 30f",
                "public float DefaultPatienceDuration => _defaultPatienceDuration",
                "public float PatienceWarningThreshold => _patienceWarningThreshold",
                "(CustomerArrivalSchedulePoint[])arrivalSchedule.Clone()",
                "ConfigValidation.RequirePositive(",
                "_patienceWarningThreshold >= _defaultPatienceDuration",
                "Validate();");
            Require(typeof(ICustomerArrivalSchedule).IsAssignableFrom(
                        typeof(CustomerArrivalSchedule)),
                $"{nameof(CustomerArrivalSchedule)} must implement " +
                $"{nameof(ICustomerArrivalSchedule)}.");
            RequireMethod(
                typeof(ICustomerArrivalSchedule),
                nameof(ICustomerArrivalSchedule.GetDelay),
                typeof(float),
                typeof(float));
            string arrivalScheduleSource = ReadRuntimeSource(
                "Gameplay", "Common", "Customers",
                nameof(CustomerArrivalSchedule) + ".cs");
            RequireSourceContains(arrivalScheduleSource,
                "staticData.CustomerFlow.ArrivalSchedule",
                "currentDayMinute < first.Minute || currentDayMinute > last.Minute",
                "left.Delay + (right.Delay - left.Delay) * progress");
            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<ICustomerArrivalSchedule>().To<CustomerArrivalSchedule>().AsSingle()");

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
                    !staticDataSource.Contains("OrderConfig", StringComparison.Ordinal),
                $"{nameof(StaticDataService)} must not retain the legacy single-SKU config properties.");
            RequireSourceContains(staticDataSource,
                "GetProduct(ProductTypeId productType)",
                "GetDelivery(ProductTypeId productType)",
                "GetProject(CustomerProjectTypeId projectType)",
                "ProductTypes",
                "ProjectTypes",
                "CustomerConfig Customer",
                "CustomerFlowConfig CustomerFlow",
                "ProductRecoveryConfig ProductRecovery",
                "PlatformTrolleyConfig PlatformTrolley",
                "WarehouseWorkerConfig WarehouseWorker");
        }

        private static void ValidateConsultationArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] consultationComponents =
            {
                typeof(CustomerVisitConsulting),
                typeof(CustomerProjectType),
                typeof(ModalOpen),
                typeof(ConsultationVisitEntityId),
                typeof(ConsultationOffer),
                typeof(ConsultationOfferLine),
                typeof(ConsultationOfferVisitEntityId),
                typeof(ConsultationOfferEntityId),
                typeof(OfferIndex),
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
            int movementFeaturePosition = storeFeatureSource.IndexOf(
                "MovementFeature", StringComparison.Ordinal);
            int refreshedPromptPosition = storeFeatureSource.IndexOf(
                "InteractionPromptFeature", StringComparison.Ordinal);
            int presentationFeaturePosition = storeFeatureSource.IndexOf(
                "PresentationFeature", StringComparison.Ordinal);
            Require(movementFeaturePosition >= 0 &&
                    refreshedPromptPosition > movementFeaturePosition &&
                    presentationFeaturePosition > refreshedPromptPosition,
                "StoreFeature must refresh interaction prompts after gameplay mutations and " +
                "before presentation.");

            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", "BootstrapInstaller.cs");
            RequireSourceContains(bootstrapSource,
                "Bind<IConsultationOfferFactory>().To<ConsultationOfferFactory>().AsSingle()");

            string arrivalSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerVehicleArrivalSystem.cs");
            RequireSourceContains(arrivalSource,
                "GameMatcher.CustomerVisitArriving",
                "GameMatcher.ReservedCustomerParkingSpotEntityId",
                "GetEntitiesWithCustomerQueueSpotStoreEntityId",
                "GetEntityWithReservedCustomerQueueSpotEntityId",
                "isCustomerVisitQueued = true",
                "_customerFactory.Create");
            Require(!arrivalSource.Contains("GameMatcher.Order", StringComparison.Ordinal) &&
                    !arrivalSource.Contains("GameMatcher.RequiredProductCount", StringComparison.Ordinal) &&
                    !arrivalSource.Contains("isCustomerVisitConsulting = true", StringComparison.Ordinal),
                "A parked customer must join the FIFO queue before order components exist.");
            Require(!arrivalSource.Contains("SceneRouteId.", StringComparison.Ordinal),
                "Customer arrival must consume typed parking/queue resources instead of legacy " +
                "generic scene-route identifiers.");

            string approachSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "CompleteCustomerApproachSystem.cs");
            RequireSourceContains(approachSource,
                "GameMatcher.CustomerApproachingCounter",
                "GameMatcher.CustomerActorVisitEntityId",
                "GameMatcher.ReservedCustomerQueueSpotEntityId",
                "GameMatcher.RouteCompleted",
                "isCustomerApproachingCounter = false",
                "isCustomerWaitingInQueue = true");
            string promoteSource = ReadRuntimeSource(
                "Gameplay", "Features", "Customers", "Systems",
                "PromoteCustomerAtCounterSystem.cs");
            RequireSourceContains(promoteSource,
                "GameMatcher.CustomerWaitingInQueue",
                "queueSpot.QueueSpotIndex != 0",
                "GetEntityWithServingOrderCounterEntityId",
                "isCustomerVisitQueued = false",
                "isCustomerVisitConsulting = true",
                "AddServingOrderCounterEntityId",
                "isCustomerWaitingAtCounter = true");

            string openSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "OpenConsultationSystem.cs");
            RequireSourceContains(openSource,
                "GameMatcher.InteractionRequest",
                "isCustomerVisitConsulting",
                "AddConsultationVisitEntityId",
                "isModalOpen = true",
                "Vector3.zero");
            Require(openSource.Contains("isHandsOccupied", StringComparison.Ordinal),
                "Consultation must not open while the player carries a product.");

            string cycleSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "CycleConsultationOfferSystem.cs");
            RequireSourceContains(cycleSource,
                "InputMatcher.PreviousPressed",
                "InputMatcher.NextPressed",
                "GameMatcher.ModalOpen",
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithConsultationOfferVisitEntityId",
                "isSelectedConsultationOffer");

            string confirmSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "ConfirmConsultationOfferSystem.cs");
            RequireSourceContains(confirmSource,
                "InputMatcher.ConfirmPressed",
                "GameMatcher.ModalOpen",
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithConsultationOfferVisitEntityId",
                "GetEntitiesWithConsultationOfferEntityId",
                "_orderFactory.AddOrderComponents",
                "RemoveConsultationOfferEntityId",
                "RemoveConsultationOfferVisitEntityId",
                "RemoveConsultationVisitEntityId",
                "isModalOpen = false",
                "isCustomerVisitConsulting = false",
                "isCustomerVisitReturning = true",
                "RemoveServingOrderCounterEntityId",
                "RemoveReservedCustomerQueueSpotEntityId",
                "_events.EmitAudio(AudioCueId.OrderAccepted)",
                "isDestructed = true");
            Require(!confirmSource.Contains("isCustomerVisitLoading = true",
                    StringComparison.Ordinal),
                "Confirming an offer must release the service point and start the pedestrian " +
                "return before the vehicle may claim the loading bay.");

            string cancelSource = ReadRuntimeSource(
                "Gameplay", "Features", "Consultation", "Systems",
                "CancelConsultationSystem.cs");
            RequireSourceContains(cancelSource,
                "InputMatcher.ToggleCursorPressed",
                "GameMatcher.ModalOpen",
                "GameMatcher.ConsultationVisitEntityId",
                "RemoveConsultationVisitEntityId",
                "isModalOpen = false");
            Require(!cancelSource.Contains("isDestructed = true", StringComparison.Ordinal),
                "Cancelling consultation must preserve its offer entities.");

            string emptyHandsStoragePromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveEmptyHandsStoragePromptSystem.cs");
            RequireSourceContains(emptyHandsStoragePromptSource,
                "GetCurrentLoadingVisit(store.EntityId)",
                "GetOrderLines(customerVisit)");
            RequireSourceOrder(
                emptyHandsStoragePromptSource,
                "GetCurrentLoadingVisit(store.EntityId)",
                "GetOrderLines(customerVisit)",
                "Empty-hands storage prompts must resolve the bay-owned loading visit before " +
                "reading its order graph.");

            string consultationProductPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveProductPromptSystem.cs");
            RequireSourceContains(consultationProductPromptSource,
                "GetCurrentLoadingVisit(store.EntityId)",
                "GetOrderLines(customerVisit)");
            RequireSourceOrder(
                consultationProductPromptSource,
                "GetCurrentLoadingVisit(store.EntityId)",
                "GetOrderLines(customerVisit)",
                "Stock-product prompts must resolve the bay-owned loading visit before " +
                "reading its order graph.");

            string consultationLoadingPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveLoadingZonePromptSystem.cs");
            RequireSourceContains(consultationLoadingPromptSource,
                "isCustomerVisitConsulting",
                "GetCurrentLoadingVisit(player.StoreEntityId)",
                "GetOrderLines(loadingZone)");
            RequireSourceOrder(
                consultationLoadingPromptSource,
                "isCustomerVisitConsulting",
                "GetCurrentLoadingVisit(player.StoreEntityId)",
                "A focused pre-order visit must present its consultation prompt before " +
                "resolving loading-bay order state.");
            RequireSourceOrder(
                consultationLoadingPromptSource,
                "GetCurrentLoadingVisit(player.StoreEntityId)",
                "GetOrderLines(loadingZone)",
                "Loading prompts must validate the bay reservation before reading order lines.");

            string consultationCounterPromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveOrderCounterPromptSystem.cs");
            RequireSourceContains(consultationCounterPromptSource,
                "GetEntityWithServingOrderCounterEntityId",
                "_gameContext.CountQueuedCustomerVisits(",
                "LocalizationKey.PromptCounterNextCustomerApproaching",
                "ValidateConsultingVisit(customerVisit, orderCounter)",
                "isCustomerVisitConsulting",
                "visit.isOrder");

            string promptExtensionsSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "InteractionPromptSystemExtensions.cs");
            RequireSourceContains(promptExtensionsSource,
                "GetEntityWithCustomerLoadingBayStoreEntityId(storeEntityId)",
                "GetEntityWithReservedCustomerLoadingBayEntityId(",
                "if (!visit.isCustomerVisitLoading)",
                "if (!visit.isLoadingZone || !visit.isOrder");
            RequireSourceOrder(
                promptExtensionsSource,
                "if (!visit.isCustomerVisitLoading)",
                "if (!visit.isLoadingZone || !visit.isOrder",
                "The shared prompt lookup must reject non-loading lifecycle states before " +
                "requiring loading-order components.");

            Require(!string.Concat(
                        emptyHandsStoragePromptSource,
                        consultationProductPromptSource,
                        consultationLoadingPromptSource,
                        consultationCounterPromptSource,
                        promptExtensionsSource)
                    .Contains("GetEntityWithCustomerVisitStoreEntityId(",
                        StringComparison.Ordinal),
                "Interaction prompts must not restore the removed singleton customer-visit " +
                "lookup.");

            string heldProductStoragePromptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveHeldProductStoragePromptSystem.cs");
            Require(!heldProductStoragePromptSource.Contains(
                        "GetEntitiesWithOrderEntityId",
                        StringComparison.Ordinal),
                "Held-product storage prompts must be derived from the already established " +
                "inbound or reserved-stock relations, without querying consultation/order state.");

            RequireMethod(
                typeof(IHudService),
                nameof(IHudService.PresentConsultation),
                typeof(void),
                typeof(ConsultationSnapshot?));
            ConstructorInfo consultationSnapshotConstructor = typeof(ConsultationSnapshot)
                .GetConstructor(new[]
                {
                    typeof(CustomerProjectTypeId),
                    typeof(int),
                    typeof(ConsultationOfferSnapshot[])
                });
            Require(consultationSnapshotConstructor != null,
                $"{nameof(ConsultationSnapshot)} must expose semantic project identity, cargo " +
                "capacity and three offer cards.");
            Require(typeof(ConsultationOfferLineSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ProductTypeId),
                        typeof(int),
                        typeof(int)
                    }) != null,
                $"{nameof(ConsultationOfferLineSnapshot)} must expose immutable per-SKU " +
                "availability and requirement data.");
            Require(typeof(OrderLineSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ProductTypeId),
                        typeof(int),
                        typeof(int),
                        typeof(int)
                    }) != null,
                $"{nameof(OrderLineSnapshot)} must expose immutable per-SKU order progress.");
            Require(typeof(ConsultationOfferSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ConsultationOfferLineSnapshot[]),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(bool)
                    }) != null,
                $"{nameof(ConsultationOfferSnapshot)} must expose line composition and its " +
                "derived capacity, cost, revenue and profit.");
            Require(typeof(HudSnapshot).GetConstructor(new[]
                    {
                        typeof(DayClockSnapshot),
                        typeof(HudOrderState),
                        typeof(CustomerProjectTypeId?),
                        typeof(OrderLineSnapshot[]),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(DeliveryProgressSnapshot?),
                        typeof(ProductTypeId?),
                        typeof(LocalizedText),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(CustomerFlowSnapshot),
                        typeof(WarehouseWorkerStatusSnapshot?)
                    }) != null,
                $"{nameof(HudSnapshot)} must expose semantic project/product identity, immutable " +
                "order lines, derived totals, customer flow and localized interaction text.");
            ValidateSnapshotProperties(
                typeof(HudSnapshot),
                (nameof(HudSnapshot.DayClock), typeof(DayClockSnapshot)),
                (nameof(HudSnapshot.OrderState), typeof(HudOrderState)),
                (nameof(HudSnapshot.ProjectType), typeof(CustomerProjectTypeId?)),
                (nameof(HudSnapshot.OrderLines), typeof(IReadOnlyList<OrderLineSnapshot>)),
                (nameof(HudSnapshot.TotalAvailableProductCount), typeof(int)),
                (nameof(HudSnapshot.TotalLoadedProductCount), typeof(int)),
                (nameof(HudSnapshot.TotalRequiredProductCount), typeof(int)),
                (nameof(HudSnapshot.Money), typeof(int)),
                (nameof(HudSnapshot.StockCount), typeof(int)),
                (nameof(HudSnapshot.Delivery), typeof(DeliveryProgressSnapshot?)),
                (nameof(HudSnapshot.HasActiveDelivery), typeof(bool)),
                (nameof(HudSnapshot.CarriedProductType), typeof(ProductTypeId?)),
                (nameof(HudSnapshot.Prompt), typeof(LocalizedText)),
                (nameof(HudSnapshot.HasFocus), typeof(bool)),
                (nameof(HudSnapshot.CanInteract), typeof(bool)),
                (nameof(HudSnapshot.HasItem), typeof(bool)),
                (nameof(HudSnapshot.IsPushingTrolley), typeof(bool)),
                (nameof(HudSnapshot.CursorLocked), typeof(bool)),
                (nameof(HudSnapshot.CustomerFlow), typeof(CustomerFlowSnapshot)),
                (nameof(HudSnapshot.WarehouseWorkerStatus),
                    typeof(WarehouseWorkerStatusSnapshot?)));
            ValidateImmutableSnapshotType(typeof(ConsultationOfferLineSnapshot));
            ValidateImmutableSnapshotType(typeof(OrderLineSnapshot));
            ValidateImmutableSnapshotType(typeof(ConsultationOfferSnapshot));
            ValidateImmutableSnapshotType(typeof(ConsultationSnapshot));
            ValidateImmutableSnapshotType(typeof(DeliveryLineProgressSnapshot));
            ValidateImmutableSnapshotType(typeof(DeliveryProgressSnapshot));
            ValidateImmutableSnapshotType(typeof(HudSnapshot));
            Require(typeof(DeliveryLineProgressSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ProductTypeId),
                        typeof(int),
                        typeof(int)
                    }) != null &&
                    typeof(DeliveryProgressSnapshot).GetConstructor(new[]
                    {
                        typeof(DeliveryLineProgressSnapshot[]),
                        typeof(int),
                        typeof(int)
                    }) != null,
                "Delivery presentation must expose immutable per-SKU line progress and " +
                "aggregate stocked/product totals.");
            ValidateSnapshotProperties(
                typeof(DeliveryLineProgressSnapshot),
                (nameof(DeliveryLineProgressSnapshot.LineIndex), typeof(int)),
                (nameof(DeliveryLineProgressSnapshot.ProductType), typeof(ProductTypeId)),
                (nameof(DeliveryLineProgressSnapshot.StockedProductCount), typeof(int)),
                (nameof(DeliveryLineProgressSnapshot.ProductCount), typeof(int)));
            ValidateSnapshotProperties(
                typeof(DeliveryProgressSnapshot),
                (nameof(DeliveryProgressSnapshot.Lines),
                    typeof(IReadOnlyList<DeliveryLineProgressSnapshot>)),
                (nameof(DeliveryProgressSnapshot.StockedProductCount), typeof(int)),
                (nameof(DeliveryProgressSnapshot.ProductCount), typeof(int)),
                (nameof(DeliveryProgressSnapshot.IncompleteLineCount), typeof(int)));
            ValidateImmutableSnapshotCollection(
                typeof(ConsultationSnapshot),
                nameof(ConsultationSnapshot.Offers),
                typeof(IReadOnlyList<ConsultationOfferSnapshot>));
            ValidateImmutableSnapshotCollection(
                typeof(ConsultationOfferSnapshot),
                nameof(ConsultationOfferSnapshot.Lines),
                typeof(IReadOnlyList<ConsultationOfferLineSnapshot>));
            ValidateImmutableSnapshotCollection(
                typeof(HudSnapshot),
                nameof(HudSnapshot.OrderLines),
                typeof(IReadOnlyList<OrderLineSnapshot>));
            ValidateImmutableSnapshotCollection(
                typeof(DeliveryProgressSnapshot),
                nameof(DeliveryProgressSnapshot.Lines),
                typeof(IReadOnlyList<DeliveryLineProgressSnapshot>));
            string presentConsultationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentConsultationSystem.cs");
            RequireSourceContains(presentConsultationSource,
                "GameMatcher.ConsultationVisitEntityId",
                "GetEntitiesWithConsultationOfferVisitEntityId",
                "GetEntitiesWithConsultationOfferEntityId",
                "OrderBy(offer => offer.OfferIndex)",
                "OrderBy(line => line.LineIndex)",
                "PresentConsultation");
            string consultationOfferSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(ConsultationOfferSnapshot) + ".cs");
            string consultationSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(ConsultationSnapshot) + ".cs");
            string hudSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(HudSnapshot) + ".cs");
            RequireSourceContains(consultationOfferSnapshotSource,
                "public readonly struct ConsultationOfferSnapshot",
                "Array.AsReadOnly((ConsultationOfferLineSnapshot[])lines.Clone())");
            RequireSourceContains(consultationSnapshotSource,
                "public readonly struct ConsultationSnapshot",
                "Array.AsReadOnly((ConsultationOfferSnapshot[])offers.Clone())");
            RequireSourceContains(hudSnapshotSource,
                "public readonly struct HudSnapshot",
                "DeliveryProgressSnapshot? delivery",
                "Array.AsReadOnly((OrderLineSnapshot[])orderLines.Clone())");
            string deliveryProgressSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(DeliveryProgressSnapshot) + ".cs");
            RequireSourceContains(deliveryProgressSnapshotSource,
                "public readonly struct DeliveryProgressSnapshot",
                "Array.AsReadOnly((DeliveryLineProgressSnapshot[])lines.Clone())");
            string offerLineSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(ConsultationOfferLineSnapshot) + ".cs");
            string orderLineSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(OrderLineSnapshot) + ".cs");
            RequireSourceContains(offerLineSnapshotSource,
                "public readonly struct ConsultationOfferLineSnapshot");
            RequireSourceContains(orderLineSnapshotSource,
                "public readonly struct OrderLineSnapshot");

            string hudViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(PrototypeHudView) + ".cs");
            RequireSourceContains(hudViewSource,
                "offer.Lines",
                "consultation.CargoCapacity",
                "_snapshot.OrderLines",
                "TotalLoadedProductCount",
                "TotalRequiredProductCount");
            Require(!hudViewSource.Contains("offer.ProductDisplayName", StringComparison.Ordinal) &&
                    !hudViewSource.Contains("_snapshot.RequiredProductDisplayName", StringComparison.Ordinal) &&
                    !hudViewSource.Contains("_snapshot.RequiredProductType", StringComparison.Ordinal),
                "Consultation and HUD presentation must not retain the single-SKU DTO contract.");
        }

        private static void ValidateProcurementArchitecture(
            Type[] runtimeTypes,
            IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Type[] procurementComponents =
            {
                typeof(ModalOpen),
                typeof(ProcurementTerminalEntityId),
                typeof(SelectedProductType),
                typeof(PurchaseDeliveryRequest),
                typeof(PurchaseDeliverySucceeded),
                typeof(ProcurementCart),
                typeof(ProcurementCartLine),
                typeof(ProcurementCartTerminalEntityId),
                typeof(ProcurementCartEntityId),
                typeof(ProcurementCartPackageCapacity),
                typeof(ProcurementPackageCount),
                typeof(PurchaseOrder),
                typeof(PurchaseOrderLine),
                typeof(PurchaseOrderProcurementTerminalEntityId),
                typeof(PurchaseOrderEntityId),
                typeof(PurchaseOrderPackageCount),
                typeof(PurchaseOrderProductCount),
                typeof(PurchaseOrderCost),
                typeof(PurchaseOrderLineIndex),
                typeof(PurchaseOrderLinePackageCount),
                typeof(PurchaseOrderLineProductCount),
                typeof(PurchaseOrderLineCost),
                typeof(PurchaseOrderLineStockedProductCount),
                typeof(DeliveryPurchaseOrderEntityId),
                typeof(PurchaseOrderLineEntityId)
            };
            foreach (Type component in procurementComponents)
            {
                Require(discoveredComponents.Contains(component),
                    $"Modal procurement requires the {component.Name} Game component.");
            }

            string[] procurementSystemNames =
            {
                "EnsureProcurementCartSystem",
                "ChangeProcurementSelectionSystem",
                "ChangeProcurementCartQuantitySystem",
                "EmitPurchaseDeliveryRequestSystem",
                "CancelProcurementSystem",
                "OpenProcurementSystem"
            };
            string[] deliverySystemNames =
            {
                "PurchaseDeliverySystem",
                "CloseProcurementAfterPurchaseSystem"
            };
            foreach (string systemName in procurementSystemNames.Concat(deliverySystemNames)
                         .Concat(new[]
                         {
                             "ValidatePlayerModalStateSystem",
                             "PresentProcurementSystem"
                         }))
            {
                Type systemType = runtimeTypes.SingleOrDefault(type => type.Name == systemName);
                Require(systemType != null && typeof(IExecuteSystem).IsAssignableFrom(systemType),
                    $"{systemName} must be an executable Entitas system.");
            }

            string procurementFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "ProcurementFeature.cs");
            int previousSystemPosition = -1;
            foreach (string systemName in procurementSystemNames)
            {
                int systemPosition = procurementFeatureSource.IndexOf(
                    systemName,
                    StringComparison.Ordinal);
                Require(systemPosition > previousSystemPosition,
                    $"ProcurementFeature must execute " +
                    $"{string.Join(" -> ", procurementSystemNames)}.");
                previousSystemPosition = systemPosition;
            }

            string deliveryFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "DeliveryFeature.cs");
            previousSystemPosition = -1;
            foreach (string systemName in deliverySystemNames)
            {
                int systemPosition = deliveryFeatureSource.IndexOf(
                    systemName,
                    StringComparison.Ordinal);
                Require(systemPosition > previousSystemPosition,
                    $"DeliveryFeature must execute " +
                    $"{string.Join(" -> ", deliverySystemNames)}.");
                previousSystemPosition = systemPosition;
            }

            string storeFeatureSource = ReadRuntimeSource("Gameplay", "StoreFeature.cs");
            int interactionFeaturePosition = storeFeatureSource.IndexOf(
                "InteractionFeature", StringComparison.Ordinal);
            int procurementFeaturePosition = storeFeatureSource.IndexOf(
                "ProcurementFeature", StringComparison.Ordinal);
            int deliveryFeaturePosition = storeFeatureSource.IndexOf(
                "DeliveryFeature", StringComparison.Ordinal);
            int productRecoveryFeaturePosition = storeFeatureSource.IndexOf(
                "Create<ProductRecoveryFeature>()", StringComparison.Ordinal);
            int employeeFeaturePosition = storeFeatureSource.IndexOf(
                "Create<EmployeeFeature>()", StringComparison.Ordinal);
            int storeDayFeaturePosition = storeFeatureSource.IndexOf(
                "Create<StoreDayFeature>()", StringComparison.Ordinal);
            int orderProgressFeaturePosition = storeFeatureSource.IndexOf(
                "Create<OrderProgressFeature>()", StringComparison.Ordinal);
            int trolleyFeaturePosition = storeFeatureSource.IndexOf(
                "Create<TrolleyFeature>()", StringComparison.Ordinal);
            int productPlacementFeaturePosition = storeFeatureSource.IndexOf(
                "Create<ProductPlacementFeature>()", StringComparison.Ordinal);
            MatchCollection storageBarriers = Regex.Matches(
                storeFeatureSource,
                Regex.Escape("Create<StorageStateFeature>()"));
            Require(interactionFeaturePosition >= 0 &&
                    procurementFeaturePosition > interactionFeaturePosition &&
                    deliveryFeaturePosition > procurementFeaturePosition,
                "StoreFeature must emit world interaction, update the procurement modal, " +
                "and only then process delivery purchases.");
            Require(storageBarriers.Count == 4 &&
                    storageBarriers[0].Index > productRecoveryFeaturePosition &&
                    storageBarriers[0].Index < interactionFeaturePosition &&
                    storageBarriers[1].Index > employeeFeaturePosition &&
                    storageBarriers[1].Index < storeDayFeaturePosition &&
                    storageBarriers[2].Index > orderProgressFeaturePosition &&
                    storageBarriers[2].Index < trolleyFeaturePosition &&
                    storageBarriers[3].Index > trolleyFeaturePosition &&
                    storageBarriers[3].Index < productPlacementFeaturePosition,
                "StoreFeature must refresh derived storage exactly after recovery, employee, " +
                "order-progress and trolley mutations; the employee barrier must precede " +
                "procurement evaluation in the same frame.");

            string interactionFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "InteractionFeature.cs");
            Require(!interactionFeatureSource.Contains(
                    "ChangeProcurementSelectionSystem",
                    StringComparison.Ordinal),
                "Procurement selection belongs to ProcurementFeature, not InteractionFeature.");
            Require(!File.Exists(GetRuntimeSourcePath(
                    "Gameplay", "Features", "Interaction", "Systems",
                    "ChangeProcurementSelectionSystem.cs")),
                "The legacy world-interaction procurement selection system must be removed.");

            string openSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "OpenProcurementSystem.cs");
            RequireSourceContains(openSource,
                "GameMatcher.InteractionRequest",
                "GameMatcher.SourceEntityId",
                "GameMatcher.TargetEntityId",
                "player.isHandsOccupied",
                "player.isModalOpen",
                "GetEntityWithDeliveryProcurementTerminalEntityId",
                "SelectOpeningProduct(terminal)",
                "_solvency.EvaluatePurchase(",
                "ProcurementDemandKind.ProjectForecast",
                "AddProcurementTerminalEntityId",
                "isModalOpen = true",
                "ReplaceMoveDirection(Vector3.zero)");
            Require(!openSource.Contains("PurchaseDeliveryRequest", StringComparison.Ordinal) &&
                    !openSource.Contains("_deliveryFactory", StringComparison.Ordinal),
                "Opening procurement must not emit or execute a purchase.");

            string changeSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "ChangeProcurementSelectionSystem.cs");
            RequireSourceContains(changeSource,
                "GameMatcher.ModalOpen",
                "GameMatcher.ProcurementTerminalEntityId",
                "InputMatcher.PreviousPressed",
                "InputMatcher.NextPressed",
                "terminal.SelectedProductType",
                "% _productTypes.Length");

            string changeQuantitySource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "ChangeProcurementCartQuantitySystem.cs");
            RequireSourceContains(changeQuantitySource,
                "GameMatcher.ModalOpen",
                "GameMatcher.ProcurementTerminalEntityId",
                "InputMatcher.IncreasePressed",
                "InputMatcher.DecreasePressed",
                "GetEntityWithProcurementCartTerminalEntityId",
                "GetEntitiesWithProcurementCartEntityId",
                "ProcurementCartPackageCapacity",
                "_carts.CreateLine(",
                "selectedLine.isDestructed = true");

            string emitPurchaseSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "EmitPurchaseDeliveryRequestSystem.cs");
            RequireSourceContains(emitPurchaseSource,
                "GameMatcher.ModalOpen",
                "GameMatcher.ProcurementTerminalEntityId",
                "InputMatcher.ConfirmPressed",
                "GetEntityWithProcurementCartTerminalEntityId",
                "GetEntitiesWithProcurementCartEntityId",
                "if (!hasLine)",
                "CreateEntity.Empty()",
                "isPurchaseDeliveryRequest = true");
            Require(!emitPurchaseSource.Contains("InteractionRequest", StringComparison.Ordinal),
                "Confirming procurement must emit a dedicated purchase request.");

            string cancelSource = ReadRuntimeSource(
                "Gameplay", "Features", "Procurement", "Systems",
                "CancelProcurementSystem.cs");
            RequireSourceContains(cancelSource,
                "GameMatcher.ModalOpen",
                "GameMatcher.ProcurementTerminalEntityId",
                "InputMatcher.ToggleCursorPressed",
                "RemoveProcurementTerminalEntityId",
                "isModalOpen = false",
                "ReplaceMoveDirection(Vector3.zero)");

            string purchaseSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "PurchaseDeliverySystem.cs");
            RequireSourceContains(purchaseSource,
                "GameMatcher.PurchaseDeliveryRequest",
                "player.isModalOpen",
                "player.hasProcurementTerminalEntityId",
                "GetEntityWithDeliveryProcurementTerminalEntityId",
                "GetEntityWithProcurementCartTerminalEntityId",
                "_solvency.EvaluateCart(cart.EntityId)",
                "ProcurementPurchaseAvailability.InsufficientStorage",
                "ProcurementPurchaseAvailability.InsufficientMoney",
                "ProcurementPurchaseAvailability.DemandWouldBecomeInsolvent",
                "LocalizationKey.NotificationPurchaseWouldBlockOrder",
                "LocalizationKey.NotificationPurchaseWouldBlockForecast",
                "_purchaseOrders.Create(",
                "_deliveryFactory.Create(",
                "LocalizationKey.NotificationMixedDeliveryOrdered",
                "ClearCart(cart)",
                "store.ReplaceMoney(moneyAfterPurchase)",
                "request.isPurchaseDeliverySucceeded = true");
            Require(!purchaseSource.Contains("GameMatcher.InteractionRequest", StringComparison.Ordinal),
                "Purchasing must consume only the dedicated purchase request.");
            Require(!purchaseSource.Contains(
                        "RemoveProcurementTerminalEntityId",
                        StringComparison.Ordinal) &&
                    CountOccurrences(purchaseSource, "store.ReplaceMoney") == 1,
                "PurchaseDeliverySystem must charge once and leave modal closing to the " +
                "success-only close system.");

            string closeSource = ReadRuntimeSource(
                "Gameplay", "Features", "Delivery", "Systems",
                "CloseProcurementAfterPurchaseSystem.cs");
            RequireSourceContains(closeSource,
                "GameMatcher.PurchaseDeliveryRequest",
                "GameMatcher.PurchaseDeliverySucceeded",
                "player.isModalOpen",
                "RemoveProcurementTerminalEntityId",
                "isModalOpen = false",
                "ReplaceMoveDirection(Vector3.zero)");

            string eventCleanupSource = ReadRuntimeSource(
                "Gameplay", "Features", "Cleanup", "Systems",
                "DestroyProcessedEventsSystem.cs");
            RequireSourceContains(eventCleanupSource,
                "GameMatcher.PurchaseDeliveryRequest",
                "Destroy(_purchaseDeliveryRequests)");

            string modalValidationSource = ReadRuntimeSource(
                "Gameplay", "Features", "Player", "Systems",
                "ValidatePlayerModalStateSystem.cs");
            RequireSourceContains(modalValidationSource,
                "player.hasConsultationVisitEntityId",
                "player.hasProcurementTerminalEntityId",
                "player.isModalOpen ? 1 : 0",
                "modalRelationCount != expectedRelationCount");
            string playerFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Player", "PlayerFeature.cs");
            RequireSourceContains(playerFeatureSource, "ValidatePlayerModalStateSystem");

            string emitInteractionSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "EmitInteractionRequestSystem.cs");
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
            foreach ((string source, string owner) in new[]
                     {
                         (emitInteractionSource, "world interaction requests"),
                         (lookSource, "look input"),
                         (toggleCursorSource, "generic cursor toggling"),
                         (dropSource, "dropping products"),
                         (focusSource, "world focus detection"),
                         (highlightSource, "world focus highlights")
                     })
            {
                RequireSourceContains(source, ".NoneOf(GameMatcher.ModalOpen");
                Require(!source.Contains(
                        ".NoneOf(GameMatcher.ConsultationVisitEntityId",
                        StringComparison.Ordinal),
                    $"ModalOpen, rather than a consultation-specific relation, must capture " +
                    $"{owner}.");
            }
            string moveSource = ReadRuntimeSource(
                "Gameplay", "Features", "Movement", "Systems",
                "SetMoveDirectionFromInputSystem.cs");
            RequireSourceContains(moveSource,
                "player.isModalOpen",
                "Vector2.zero");

            string promptSource = ReadRuntimeSource(
                "Gameplay", "Features", "Interaction", "Systems",
                "ResolveProcurementTerminalPromptSystem.cs");
            RequireSourceContains(promptSource,
                "player.isHandsOccupied",
                "GetEntityWithDeliveryProcurementTerminalEntityId",
                "LocalizationKey.PromptOpenProcurement");
            Require(!promptSource.Contains("InputMatcher.PreviousPressed", StringComparison.Ordinal) &&
                    !promptSource.Contains("InputMatcher.NextPressed", StringComparison.Ordinal) &&
                    !promptSource.Contains("HasOrderDeficit", StringComparison.Ordinal) &&
                    !promptSource.Contains("LocalizationKey.PromptWaitForCustomer",
                        StringComparison.Ordinal) &&
                    !promptSource.Contains("LocalizationKey.PromptStockSufficient",
                        StringComparison.Ordinal),
                "A delivery-free, hands-free procurement terminal must stay available in " +
                "every customer lifecycle; selection controls remain modal-only.");

            Require(typeof(IProcurementSolvencyService).IsAssignableFrom(
                        typeof(ProcurementSolvencyService)) &&
                    typeof(IEconomySolvencyService).IsAssignableFrom(
                        typeof(ProcurementSolvencyService)),
                $"{nameof(ProcurementSolvencyService)} must be the shared procurement and " +
                "generic debit policy service.");
            Require(typeof(ProcurementSolvencyService).GetConstructor(new[]
                    {
                        typeof(GameContext),
                        typeof(IStaticDataService)
                    }) != null,
                $"{nameof(ProcurementSolvencyService)} must depend only on ECS state and " +
                "validated static data.");
            RequireMethod(
                typeof(IProcurementCartFactory),
                nameof(IProcurementCartFactory.Create),
                typeof(GameEntity),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IProcurementCartFactory),
                nameof(IProcurementCartFactory.CreateLine),
                typeof(GameEntity),
                typeof(int),
                typeof(ProductTypeId),
                typeof(int));
            RequireMethod(
                typeof(IPurchaseOrderFactory),
                nameof(IPurchaseOrderFactory.Create),
                typeof(GameEntity),
                typeof(int),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IDeliveryFactory),
                nameof(IDeliveryFactory.Create),
                typeof(GameEntity),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(Pose));
            RequireMethod(
                typeof(IProductFactory),
                nameof(IProductFactory.CreateInbound),
                typeof(GameEntity),
                typeof(ProductTypeId),
                typeof(Pose),
                typeof(int),
                typeof(int),
                typeof(int));
            RequireMethod(
                typeof(IProcurementSolvencyService),
                nameof(IProcurementSolvencyService.EvaluateCart),
                typeof(ProcurementPurchaseEvaluation),
                typeof(int));
            RequireMethod(
                typeof(IProcurementSolvencyService),
                nameof(IProcurementSolvencyService.EvaluatePurchase),
                typeof(ProcurementPurchaseEvaluation),
                typeof(int),
                typeof(ProductTypeId));
            RequireMethod(
                typeof(IEconomySolvencyService),
                nameof(IEconomySolvencyService.EvaluateDebit),
                typeof(EconomyDebitEvaluation),
                typeof(int),
                typeof(int));
            string solvencySource = ReadRuntimeSource(
                "Gameplay", "Common", "Economy",
                nameof(ProcurementSolvencyService) + ".cs");
            RequireSourceContains(solvencySource,
                "MaximumProjectionStateCount = 131072",
                "storageZone.Slots.Length",
                "checked(",
                "IncludeCommittedDelivery(state, terminalState.Terminal)",
                "public ProcurementPurchaseEvaluation EvaluateCart(",
                "GetEntitiesWithProcurementCartEntityId(",
                "GetEntitiesWithCustomerVisitStoreEntityId(",
                "OrderBy(visit => visit.CustomerArrivalSequence)",
                "new List<ProtectedDemand>(visits.Length)",
                "int arrivalSequenceDelta =",
                "AdvanceSequenceIndex(",
                "previousProjectIndex,",
                "arrivalSequenceDelta);",
                "int remainingArrivalSequenceCount =",
                "remainingArrivalSequenceCount);",
                "if (stepCount <= 0)",
                "stepCount % _staticData.ProjectTypes.Count",
                "ProtectedDemand.ConfirmedOrder(",
                "CollectRemainingOrderRequirements(visit, stock)",
                "ProtectedDemand.ProjectForecast(visit)",
                "store.NextProjectSequenceIndex,",
                "_staticData.ProjectTypes.Count);",
                "ValidateProjectionPlan(demandPlan)",
                "AreProtectedDemandsSolvent(",
                "demandIndex == demandPlan.ProtectedDemands.Count",
                "AreForecastPathsSolvent(",
                "remainingProjectCount - 1",
                "foreach (CustomerProjectOfferDefinition offer in project.Offers)",
                "demandPlan.ProtectedDemands.Count",
                "demandPlan.FutureProjectCount",
                "new ProjectionMemo(",
                "Dictionary<ProjectionKey, byte>",
                "unique states",
                "completed.OccupiedSlotCount + additionalProductCount >",
                "completed.Capacity");
            RequireSourceOrder(
                solvencySource,
                "GetEntitiesWithCustomerVisitStoreEntityId(",
                "OrderBy(visit => visit.CustomerArrivalSequence)",
                "Procurement demand planning must collect the store's plural active visits " +
                "before imposing deterministic arrival order.");
            RequireSourceOrder(
                solvencySource,
                "OrderBy(visit => visit.CustomerArrivalSequence)",
                "new List<ProtectedDemand>(visits.Length)",
                "Protected demand projection must preserve FIFO visit order.");
            RequireSourceOrder(
                solvencySource,
                "int arrivalSequenceDelta =",
                "AdvanceSequenceIndex(",
                "Project sequence validation must advance by the positive gap between " +
                "surviving customer arrival sequences.");
            RequireSourceOrder(
                solvencySource,
                "int remainingArrivalSequenceCount =",
                "int expectedNextProjectIndex = AdvanceSequenceIndex(",
                "The next store project must account for destroyed visits after the latest " +
                "surviving customer arrival.");
            RequireSourceOrder(
                solvencySource,
                "ProtectedDemand.ConfirmedOrder(",
                "ProtectedDemand.ProjectForecast(visit)",
                "Active confirmed orders must retain their exact requirements while pre-order " +
                "visits branch across configured offers.");
            RequireSourceOrder(
                solvencySource,
                "var demandPlan = new DemandPlan(",
                "ValidateProjectionPlan(demandPlan)",
                "The complete active-visit and future-project plan must be bounded before " +
                "solvency recursion.");
            RequireSourceOrder(
                solvencySource,
                "demandIndex == demandPlan.ProtectedDemands.Count",
                "AreForecastPathsSolvent(",
                "Future catalog forecasting must begin only after every active customer " +
                "demand has been projected.");
            RequireSourceOrder(
                solvencySource,
                "for (int index = 0; index < demandPlan.ProtectedDemands.Count; index++)",
                "for (int offset = 0; offset < demandPlan.FutureProjectCount; offset++)",
                "Projection bounds must include active pre-order branches before the full " +
                "future project horizon.");
            Require(!solvencySource.Contains(
                        "GetEntityWithCustomerVisitStoreEntityId(",
                        StringComparison.Ordinal) &&
                    !solvencySource.Contains("forecastDepth", StringComparison.Ordinal) &&
                    !solvencySource.Contains("int projectCount", StringComparison.Ordinal),
                "Procurement solvency must not restore the singleton visit lookup or the " +
                "obsolete fixed-depth local forecast contract.");
            string bootstrapSource = ReadRuntimeSource(
                "Infrastructure", "Installers", nameof(BootstrapInstaller) + ".cs");
            RequireSourceContains(bootstrapSource,
                "BindInterfacesTo<ProcurementSolvencyService>().AsSingle()",
                "Bind<IProcurementCartFactory>().To<ProcurementCartFactory>().AsSingle()",
                "Bind<IPurchaseOrderFactory>().To<PurchaseOrderFactory>().AsSingle()");

            RequireMethod(
                typeof(IHudService),
                nameof(IHudService.PresentProcurement),
                typeof(void),
                typeof(ProcurementSnapshot?));
            Require(typeof(ProcurementSnapshot).GetConstructor(new[]
                    {
                        typeof(ProcurementDemandKind),
                        typeof(CustomerProjectTypeId),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(ProcurementProductSnapshot[]),
                        typeof(ProcurementCartSnapshot)
                    }) != null,
                $"{nameof(ProcurementSnapshot)} must expose demand kind, project, money, " +
                "free storage, selected catalog index, arbitrary product cards and cart.");
            Require(typeof(ProcurementProductSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ProductTypeId),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int)
                    }) != null,
                $"{nameof(ProcurementProductSnapshot)} must expose immutable package, stock, " +
                "transit, demand, projected deficit and cart data.");
            Require(typeof(ProcurementCartLineSnapshot).GetConstructor(new[]
                    {
                        typeof(int),
                        typeof(ProductTypeId),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int)
                    }) != null &&
                    typeof(ProcurementCartSnapshot).GetConstructor(new[]
                    {
                        typeof(ProcurementCartLineSnapshot[]),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(ProcurementPurchaseState)
                    }) != null,
                "Procurement cart snapshots must expose immutable manifest lines and exact " +
                "aggregate package, product, money, storage and purchase state.");
            ValidateImmutableSnapshotType(typeof(ProcurementProductSnapshot));
            ValidateImmutableSnapshotType(typeof(ProcurementCartLineSnapshot));
            ValidateImmutableSnapshotType(typeof(ProcurementCartSnapshot));
            ValidateImmutableSnapshotType(typeof(ProcurementSnapshot));
            ValidateImmutableSnapshotCollection(
                typeof(ProcurementSnapshot),
                nameof(ProcurementSnapshot.Products),
                typeof(IReadOnlyList<ProcurementProductSnapshot>));
            ValidateImmutableSnapshotCollection(
                typeof(ProcurementCartSnapshot),
                nameof(ProcurementCartSnapshot.Lines),
                typeof(IReadOnlyList<ProcurementCartLineSnapshot>));
            ValidateSnapshotProperties(
                typeof(ProcurementSnapshot),
                (nameof(ProcurementSnapshot.DemandKind), typeof(ProcurementDemandKind)),
                (nameof(ProcurementSnapshot.ProjectType), typeof(CustomerProjectTypeId)),
                (nameof(ProcurementSnapshot.Money), typeof(int)),
                (nameof(ProcurementSnapshot.FreeStorageSlotCount), typeof(int)),
                (nameof(ProcurementSnapshot.SelectedProductIndex), typeof(int)),
                (nameof(ProcurementSnapshot.Products),
                    typeof(IReadOnlyList<ProcurementProductSnapshot>)),
                (nameof(ProcurementSnapshot.Cart), typeof(ProcurementCartSnapshot)));
            ValidateSnapshotProperties(
                typeof(ProcurementProductSnapshot),
                (nameof(ProcurementProductSnapshot.Index), typeof(int)),
                (nameof(ProcurementProductSnapshot.ProductType), typeof(ProductTypeId)),
                (nameof(ProcurementProductSnapshot.PackageProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.PackageCost), typeof(int)),
                (nameof(ProcurementProductSnapshot.StockProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.InTransitProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.MinimumRequiredProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.MaximumRequiredProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.RemainingRequiredProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.ProjectedDeficitProductCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.CartPackageCount), typeof(int)),
                (nameof(ProcurementProductSnapshot.CartProductCount), typeof(int)));
            ValidateSnapshotProperties(
                typeof(ProcurementCartLineSnapshot),
                (nameof(ProcurementCartLineSnapshot.LineIndex), typeof(int)),
                (nameof(ProcurementCartLineSnapshot.ProductType), typeof(ProductTypeId)),
                (nameof(ProcurementCartLineSnapshot.PackageCount), typeof(int)),
                (nameof(ProcurementCartLineSnapshot.PackageProductCount), typeof(int)),
                (nameof(ProcurementCartLineSnapshot.PackageCost), typeof(int)),
                (nameof(ProcurementCartLineSnapshot.ProductCount), typeof(int)),
                (nameof(ProcurementCartLineSnapshot.LineCost), typeof(int)));
            ValidateSnapshotProperties(
                typeof(ProcurementCartSnapshot),
                (nameof(ProcurementCartSnapshot.Lines),
                    typeof(IReadOnlyList<ProcurementCartLineSnapshot>)),
                (nameof(ProcurementCartSnapshot.PackageCount), typeof(int)),
                (nameof(ProcurementCartSnapshot.PackageCapacity), typeof(int)),
                (nameof(ProcurementCartSnapshot.ProductCount), typeof(int)),
                (nameof(ProcurementCartSnapshot.TotalCost), typeof(int)),
                (nameof(ProcurementCartSnapshot.MoneyAfterPurchase), typeof(int)),
                (nameof(ProcurementCartSnapshot.RequiredStorageSlotCount), typeof(int)),
                (nameof(ProcurementCartSnapshot.PurchaseState),
                    typeof(ProcurementPurchaseState)),
                (nameof(ProcurementCartSnapshot.CanCheckout), typeof(bool)));

            string procurementSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(ProcurementSnapshot) + ".cs");
            string procurementProductSnapshotSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(ProcurementProductSnapshot) + ".cs");
            RequireSourceContains(procurementSnapshotSource,
                "public readonly struct ProcurementSnapshot",
                "selectedProductIndex < 0 || selectedProductIndex >= products.Length",
                "ValidateCartProducts(products, cart)",
                "Array.AsReadOnly((ProcurementProductSnapshot[])products.Clone())");
            RequireSourceContains(procurementProductSnapshotSource,
                "public readonly struct ProcurementProductSnapshot",
                "PackageProductCount",
                "InTransitProductCount",
                "MinimumRequiredProductCount",
                "MaximumRequiredProductCount",
                "ProjectedDeficitProductCount",
                "CartPackageCount",
                "CartProductCount");

            string presentSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "Systems",
                "PresentProcurementSystem.cs");
            RequireSourceContains(presentSource,
                "GameMatcher.ModalOpen",
                "GameMatcher.ProcurementTerminalEntityId",
                "PresentProcurement(null)",
                "_solvency.EvaluatePurchase(",
                "_solvency.EvaluateCart(cart.EntityId)",
                "ProcurementDemandKind.ProjectForecast",
                "ProcurementDemandKind.ConfirmedOrder",
                "GetEntitiesWithOrderEntityId",
                "_productTypes = new ProductTypeId[staticData.ProductTypes.Count]",
                "CollectCartLineEntities(cart)",
                "CaptureSourceFingerprint(",
                "SourceFingerprintMatchesCache()",
                "_hud.PresentProcurement(_cachedSnapshot)",
                "minimumRequiredProductCount",
                "maximumRequiredProductCount",
                "remainingRequiredProductCount",
                "projectedDeficitProductCount",
                "new ProcurementCartLineSnapshot(",
                "new ProcurementCartSnapshot(",
                "MapPurchaseState(evaluation.Availability)",
                "var snapshot = new ProcurementSnapshot(",
                "_hud.PresentProcurement(snapshot)");
            Require(!presentSource.Contains("CustomerPatienceRemaining",
                        StringComparison.Ordinal) &&
                    !presentSource.Contains("CustomerPatienceWarningIssued",
                        StringComparison.Ordinal) &&
                    !presentSource.Contains("_staticData.ProductTypes.ToArray()",
                        StringComparison.Ordinal),
                "Procurement presentation cache must use a stable exact fingerprint and must " +
                "not invalidate on per-frame patience or allocate the catalog per frame.");

            string presentationFeatureSource = ReadRuntimeSource(
                "Gameplay", "Features", "Presentation", "PresentationFeature.cs");
            int hudPosition = presentationFeatureSource.IndexOf(
                "PresentHudSystem", StringComparison.Ordinal);
            int procurementPosition = presentationFeatureSource.IndexOf(
                "PresentProcurementSystem", StringComparison.Ordinal);
            int consultationPosition = presentationFeatureSource.IndexOf(
                "PresentConsultationSystem", StringComparison.Ordinal);
            Require(hudPosition >= 0 && procurementPosition > hudPosition &&
                    consultationPosition > procurementPosition,
                "PresentationFeature must draw HUD, procurement, then consultation so only " +
                "the active modal owns the foreground.");

            string hudViewSource = ReadRuntimeSource(
                "Gameplay", "Presentation", nameof(PrototypeHudView) + ".cs");
            RequireSourceContains(hudViewSource,
                "DrawProcurement",
                "const int cardsPerPage = 6",
                "procurement.SelectedProductIndex / cardsPerPage",
                "DrawProcurementCart(",
                "procurement.Cart",
                "LocalizationKey.HudProcurementTitle",
                "LocalizationKey.HudProcurementForecastTitle",
                "LocalizationKey.HudProcurementOrderTitle",
                "LocalizationKey.HudProcurementPackageDetails",
                "LocalizationKey.HudProcurementConfirmedCounts",
                "LocalizationKey.HudProcurementForecastCounts",
                "LocalizationKey.ProcurementStatusPlanWouldBlockOrder",
                "LocalizationKey.ProcurementStatusPlanWouldBlockForecast",
                "LocalizationKey.ProcurementStatusPrepurchaseAvailable",
                "LocalizationKey.HudProcurementCartTitle",
                "LocalizationKey.HudProcurementCartLine",
                "LocalizationKey.HudProcurementCartCapacityReached",
                "LocalizationKey.HudProcurementControls");

            var procurementLocalizationArities = new Dictionary<LocalizationKey, int>
            {
                { LocalizationKey.HudProcurementForecastTitle, 1 },
                { LocalizationKey.HudProcurementForecastProductDetails, 7 },
                { LocalizationKey.HudProcurementPage, 2 },
                { LocalizationKey.HudProcurementPackageDetails, 3 },
                { LocalizationKey.HudProcurementConfirmedCounts, 5 },
                { LocalizationKey.HudProcurementForecastCounts, 5 },
                { LocalizationKey.HudProcurementCardCartQuantity, 3 },
                { LocalizationKey.HudProcurementCartTitle, 2 },
                { LocalizationKey.HudProcurementCartLine, 5 },
                { LocalizationKey.HudProcurementCartProductTotal, 2 },
                { LocalizationKey.HudProcurementCartCost, 1 },
                { LocalizationKey.HudProcurementCartBalanceAfter, 1 },
                { LocalizationKey.HudProcurementCartStorage, 2 },
                { LocalizationKey.HudProcurementCartEmpty, 0 },
                { LocalizationKey.HudProcurementCartCapacityReached, 1 },
                { LocalizationKey.HudObjectiveMixedDelivery, 3 },
                { LocalizationKey.PromptMixedDeliveryBeingStocked, 3 },
                { LocalizationKey.PromptBringMixedDeliveryToIntake, 2 },
                { LocalizationKey.NotificationMixedDeliveryOrdered, 3 },
                { LocalizationKey.ProcurementStatusPlanWouldBlockOrder, 0 },
                { LocalizationKey.ProcurementStatusPlanWouldBlockForecast, 0 },
                { LocalizationKey.ProcurementStatusPrepurchaseAvailable, 0 },
                { LocalizationKey.NotificationPurchaseWouldBlockOrder, 0 },
                { LocalizationKey.NotificationPurchaseWouldBlockForecast, 0 }
            };
            Dictionary<LocalizationKey, LocalizationEntry> localizationEntries =
                new RussianLocalizationCatalog().Entries.ToDictionary(entry => entry.Key);
            foreach ((LocalizationKey key, int argumentCount) in
                     procurementLocalizationArities)
            {
                Require(localizationEntries.TryGetValue(key, out LocalizationEntry entry) &&
                        entry.ArgumentCount == argumentCount,
                    $"Russian procurement localization {key} must exist with arity " +
                    $"{argumentCount}.");
            }
        }

        private static void ValidateBoundedProcurementSolvencyPolicy(int storageCapacity)
        {
            const int protectedPreOrderCount = 3;
            const int runtimeMaximumUniqueStateCount = 131072;
            const int regressionWorkloadBudget = 8192;
            Require(storageCapacity == RequiredStorageSlotCapacity,
                $"The expanded product catalog requires exactly " +
                $"{RequiredStorageSlotCapacity} storage slots.");

            ProductConfig[] products = LoadConfigCatalogAssets<ProductConfig>()
                .OrderBy(config => (int)config.ProductType)
                .ToArray();
            DeliveryConfig[] deliveries = LoadConfigCatalogAssets<DeliveryConfig>()
                .OrderBy(config => (int)config.ProductType)
                .ToArray();
            CustomerProjectConfig[] projects =
                LoadConfigCatalogAssets<CustomerProjectConfig>()
                    .OrderBy(config => (int)config.ProjectType)
                    .ToArray();
            Require(products.Select(config => config.ProductType)
                        .SequenceEqual(ExpectedProductTypes) &&
                    deliveries.Select(config => config.ProductType)
                        .SequenceEqual(ExpectedProductTypes) &&
                    projects.Select(config => config.ProjectType)
                        .SequenceEqual(ExpectedProjectTypes),
                "Bounded procurement assets must preserve the complete deterministic " +
                "product and project ordering.");

            int maximumBranchingFactor = projects.Max(project => project.Offers.Count);
            Require(maximumBranchingFactor == 3,
                "The frozen projection proof requires exactly three offers per project.");
            int protectedStructuralStateCount = Enumerable.Range(
                    0,
                    protectedPreOrderCount + 1)
                .Sum(depth => IntPower(maximumBranchingFactor, depth));
            int forecastStructuralStateCount = Enumerable.Range(
                    protectedPreOrderCount,
                    projects.Length + 1)
                .Sum(depth => IntPower(maximumBranchingFactor, depth));
            int forecastOnlyStructuralBound = checked(
                protectedStructuralStateCount + forecastStructuralStateCount);
            int exactDemandBoundaryStateCount = IntPower(
                maximumBranchingFactor,
                protectedPreOrderCount);
            int structuralProjectionBound = checked(
                forecastOnlyStructuralBound + exactDemandBoundaryStateCount);
            Require(forecastOnlyStructuralBound == 88600 &&
                    structuralProjectionBound == 88627 &&
                    structuralProjectionBound < runtimeMaximumUniqueStateCount,
                $"The current 3-forecast + 7-future topology must have a formal " +
                $"88,627-state upper bound below the strict runtime cap " +
                $"{runtimeMaximumUniqueStateCount}; found {structuralProjectionBound}.");

            List<int[]> cartAllocationStocks = CreateCatalogCartAllocationStocks(
                deliveries,
                ProcurementCartFactory.CurrentDeliveryPackageCapacity);
            int binaryStockStateCount = 1 << ExpectedProductTypes.Length;
            var binaryStartStocks = Enumerable.Range(0, binaryStockStateCount)
                .Select(mask => Enumerable.Range(0, ExpectedProductTypes.Length)
                    .Select(productIndex => (mask & (1 << productIndex)) == 0 ? 0 : 1)
                    .ToArray())
                .ToList();
            Require(cartAllocationStocks.Count == 84 &&
                    binaryStartStocks.Count == 64,
                "Frozen workload validation requires all 84 zero-to-three-package cart " +
                "allocations and all 64 binary catalog stock states.");
            int[][] workloadStocks = cartAllocationStocks
                .Concat(binaryStartStocks)
                .GroupBy(stock => string.Join(",", stock))
                .Select(group => group.First())
                .ToArray();
            Require(workloadStocks.Length == 147,
                "Only the empty state may overlap the cart-allocation and binary-stock " +
                "workload matrices.");

            int maximumHorizon = checked(projects.Length + protectedPreOrderCount);
            int highSolventMoney = checked(
                deliveries.Sum(delivery => delivery.TotalCost) * maximumHorizon + 200);
            int worstObservedStateCount = 0;
            int evaluatedWorkloadCount = 0;
            int allowedWorkloadCount = 0;
            foreach (int[] stock in workloadStocks)
            for (int projectOffset = 0;
                 projectOffset < projects.Length;
                 projectOffset++)
            for (int activeForecastCount = 0;
                 activeForecastCount <= protectedPreOrderCount;
                 activeForecastCount++)
            {
                bool allowed = TryCountCatalogProjectionStates(
                    highSolventMoney,
                    stock,
                    projectOffset,
                    activeForecastCount,
                    storageCapacity,
                    products,
                    deliveries,
                    projects,
                    out int visitedStateCount);
                evaluatedWorkloadCount++;
                if (allowed)
                    allowedWorkloadCount++;
                worstObservedStateCount = Math.Max(
                    worstObservedStateCount,
                    visitedStateCount);
            }
            Require(evaluatedWorkloadCount == workloadStocks.Length * projects.Length *
                        (protectedPreOrderCount + 1) &&
                    allowedWorkloadCount > 0,
                "Frozen procurement workload did not cover every stock, cyclic offset and " +
                "zero-to-three-active-forecast combination.");

            var sampledSolventThresholdStocks = new List<(string Label, int[] Stock)>
            {
                ("empty cart", new int[ExpectedProductTypes.Length])
            };
            for (int productIndex = 0;
                 productIndex < ExpectedProductTypes.Length;
                 productIndex++)
            {
                var onePackageStock = new int[ExpectedProductTypes.Length];
                onePackageStock[productIndex] = deliveries[productIndex].ProductCount;
                sampledSolventThresholdStocks.Add((
                    $"one {ExpectedProductTypes[productIndex]} package",
                    onePackageStock));
            }

            var mixedThreePackageStock = new int[ExpectedProductTypes.Length];
            for (int productIndex = 0; productIndex < 3; productIndex++)
                mixedThreePackageStock[productIndex] = deliveries[productIndex].ProductCount;
            sampledSolventThresholdStocks.Add((
                "mixed three-package cart",
                mixedThreePackageStock));
            Require(sampledSolventThresholdStocks.All(sample =>
                        sample.Stock.Sum() <= storageCapacity),
                "Sampled post-candidate procurement states exceed authored storage capacity.");

            var observedOffsets = new HashSet<int>();
            foreach ((string label, int[] stock) in sampledSolventThresholdStocks)
            for (int projectOffset = 0;
                 projectOffset < projects.Length;
                 projectOffset++)
            {
                int minimumSolventMoney = FindMinimumCatalogProjectionMoney(
                    stock,
                    projectOffset,
                    highSolventMoney,
                    protectedPreOrderCount,
                    storageCapacity,
                    products,
                    deliveries,
                    projects);
                Require(TryCountCatalogProjectionStates(
                        minimumSolventMoney,
                        stock,
                        projectOffset,
                        protectedPreOrderCount,
                        storageCapacity,
                        products,
                        deliveries,
                        projects,
                        out int visitedStateCount),
                    $"The {label} projection at cyclic offset {projectOffset} must " +
                    $"complete at its exact sampled solvent threshold " +
                    $"{minimumSolventMoney}.");
                worstObservedStateCount = Math.Max(
                    worstObservedStateCount,
                    visitedStateCount);

                observedOffsets.Add(projectOffset);
            }

            Require(observedOffsets.Count == projects.Length &&
                    projects.Length == ExpectedProjectTypes.Length,
                "Bounded procurement validation must cover all seven cyclic project starts.");
            Require(worstObservedStateCount == 4961 &&
                    worstObservedStateCount < regressionWorkloadBudget &&
                    worstObservedStateCount < runtimeMaximumUniqueStateCount,
                $"The frozen catalog workload must peak at 4,961 states and stay below the " +
                $"independent {regressionWorkloadBudget}-state regression budget and " +
                $"{runtimeMaximumUniqueStateCount}-state correctness cap; " +
                $"observed {worstObservedStateCount}.");
        }

        private static List<int[]> CreateCatalogCartAllocationStocks(
            IReadOnlyList<DeliveryConfig> deliveries,
            int maximumPackageCount)
        {
            var results = new List<int[]>();
            var packageCounts = new int[deliveries.Count];
            CollectCatalogCartAllocationStocks(
                deliveries,
                maximumPackageCount,
                0,
                packageCounts,
                results);
            return results;
        }

        private static void CollectCatalogCartAllocationStocks(
            IReadOnlyList<DeliveryConfig> deliveries,
            int remainingPackageCount,
            int productIndex,
            int[] packageCounts,
            ICollection<int[]> results)
        {
            if (productIndex == deliveries.Count)
            {
                results.Add(packageCounts
                    .Select((packageCount, index) => checked(
                        packageCount * deliveries[index].ProductCount))
                    .ToArray());
                return;
            }

            for (int packageCount = 0;
                 packageCount <= remainingPackageCount;
                 packageCount++)
            {
                packageCounts[productIndex] = packageCount;
                CollectCatalogCartAllocationStocks(
                    deliveries,
                    remainingPackageCount - packageCount,
                    productIndex + 1,
                    packageCounts,
                    results);
            }
            packageCounts[productIndex] = 0;
        }

        private static int IntPower(int value, int exponent)
        {
            int result = 1;
            for (int index = 0; index < exponent; index++)
                result = checked(result * value);
            return result;
        }

        private static int FindMinimumCatalogProjectionMoney(
            IReadOnlyList<int> stock,
            int projectOffset,
            int highSolventMoney,
            int protectedPreOrderCount,
            int storageCapacity,
            IReadOnlyList<ProductConfig> products,
            IReadOnlyList<DeliveryConfig> deliveries,
            IReadOnlyList<CustomerProjectConfig> projects)
        {
            Require(TryCountCatalogProjectionStates(
                    highSolventMoney,
                    stock,
                    projectOffset,
                    protectedPreOrderCount,
                    storageCapacity,
                    products,
                    deliveries,
                    projects,
                    out _),
                $"Derived high-solvent money {highSolventMoney} cannot cover cyclic " +
                $"project offset {projectOffset}.");

            int lowerBound = 0;
            int upperBound = highSolventMoney;
            while (lowerBound < upperBound)
            {
                int candidateMoney = lowerBound + (upperBound - lowerBound) / 2;
                if (TryCountCatalogProjectionStates(
                        candidateMoney,
                        stock,
                        projectOffset,
                        protectedPreOrderCount,
                        storageCapacity,
                        products,
                        deliveries,
                        projects,
                        out _))
                {
                    upperBound = candidateMoney;
                }
                else
                {
                    lowerBound = candidateMoney + 1;
                }
            }

            return lowerBound;
        }

        private static bool TryCountCatalogProjectionStates(
            int initialMoney,
            IReadOnlyList<int> initialStock,
            int projectOffset,
            int protectedPreOrderCount,
            int storageCapacity,
            IReadOnlyList<ProductConfig> products,
            IReadOnlyList<DeliveryConfig> deliveries,
            IReadOnlyList<CustomerProjectConfig> projects,
            out int visitedStateCount)
        {
            var stock = initialStock.ToArray();
            var initialState = new CatalogProjectionState(initialMoney, stock);
            var states = new Dictionary<string, CatalogProjectionState>
            {
                [initialState.StateKey] = initialState
            };
            visitedStateCount = states.Count;
            int protectedForecastHandoffStateCount = protectedPreOrderCount == 0
                ? states.Count
                : 0;
            int horizon = checked(projects.Count + protectedPreOrderCount);
            for (int stageIndex = 0; stageIndex < horizon; stageIndex++)
            {
                CustomerProjectConfig project = projects[
                    (projectOffset + stageIndex) % projects.Count];
                var nextStates = new Dictionary<string, CatalogProjectionState>();
                foreach (CatalogProjectionState state in states.Values)
                foreach (CustomerProjectOfferDefinition offer in project.Offers)
                {
                    if (!TryCompleteCatalogOffer(
                            state,
                            offer,
                            storageCapacity,
                            products,
                            deliveries,
                            out CatalogProjectionState completed))
                    {
                        return false;
                    }
                    if (stageIndex == 1)
                    {
                        if (completed.Money < 200)
                            return false;
                        completed = completed.Debit(200);
                    }

                    nextStates[completed.StateKey] = completed;
                }

                states = nextStates;
                visitedStateCount = checked(visitedStateCount + states.Count);
                if (stageIndex == protectedPreOrderCount - 1)
                {
                    // Runtime memoizes the final protected-demand state once in the
                    // protected phase and once at the future-forecast phase boundary.
                    protectedForecastHandoffStateCount = states.Count;
                }
            }

            visitedStateCount = checked(
                visitedStateCount + protectedForecastHandoffStateCount);
            return true;
        }

        private static bool TryCompleteCatalogOffer(
            CatalogProjectionState state,
            CustomerProjectOfferDefinition offer,
            int storageCapacity,
            IReadOnlyList<ProductConfig> products,
            IReadOnlyList<DeliveryConfig> deliveries,
            out CatalogProjectionState completed)
        {
            int[] stock = (int[])state.Stock.Clone();
            int occupiedSlotCount = stock.Sum();
            int purchaseCost = 0;
            int reward = 0;
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                int productIndex = Array.IndexOf(ExpectedProductTypes, line.ProductType);
                Require(productIndex >= 0,
                    $"Projected offer references unknown product {line.ProductType}.");
                int missingCount = Math.Max(0, line.RequiredCount - stock[productIndex]);
                DeliveryConfig delivery = deliveries[productIndex];
                int packageCount = checked(
                    (missingCount + delivery.ProductCount - 1) /
                    delivery.ProductCount);
                int purchasedCount = checked(packageCount * delivery.ProductCount);
                stock[productIndex] = checked(stock[productIndex] + purchasedCount);
                occupiedSlotCount = checked(occupiedSlotCount + purchasedCount);
                purchaseCost = checked(
                    purchaseCost + checked(packageCount * delivery.TotalCost));
                reward = checked(
                    reward + checked(
                        products[productIndex].UnitPrice * line.RequiredCount));
            }

            if (state.Money < purchaseCost || occupiedSlotCount > storageCapacity)
            {
                completed = default;
                return false;
            }

            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                int productIndex = Array.IndexOf(ExpectedProductTypes, line.ProductType);
                stock[productIndex] = checked(
                    stock[productIndex] - line.RequiredCount);
            }

            completed = new CatalogProjectionState(
                checked(state.Money - purchaseCost + reward),
                stock);
            return true;
        }

        private readonly struct CatalogProjectionState
        {
            public CatalogProjectionState(int money, int[] stock)
            {
                Money = money;
                Stock = stock;
                StateKey = Key(money, stock);
            }

            public int Money { get; }
            public int[] Stock { get; }
            public string StateKey { get; }

            public CatalogProjectionState Debit(int amount) =>
                new(checked(Money - amount), (int[])Stock.Clone());

            public static string Key(int initialMoney, IReadOnlyList<int> stock) =>
                $"{initialMoney}|{string.Join(",", stock)}";

            public override string ToString() => StateKey;
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
            Require(Mathf.Approximately(controllers[0].stepOffset, 0.32f),
                $"{PlayerPrefabPath} must author a 0.32 metre step offset shared by player and " +
                "trolley threshold traversal.");
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
                $"{nameof(ProductTypeId)} must preserve the six append-only product IDs.");
            CustomerProjectTypeId[] projectEnumValues =
                Enum.GetValues(typeof(CustomerProjectTypeId))
                    .Cast<CustomerProjectTypeId>()
                    .ToArray();
            Require(projectEnumValues.SequenceEqual(ExpectedProjectTypes),
                $"{nameof(CustomerProjectTypeId)} must preserve the seven append-only " +
                "customer-project IDs.");
            Require(AssetDatabase.LoadMainAssetAtPath(LegacyCementOrderConfigPath) == null &&
                    AssetDatabase.LoadMainAssetAtPath(LegacyBoardOrderConfigPath) == null,
                "Legacy single-SKU OrderConfig assets must be removed by the prototype builder.");

            ProductConfig cementProductConfig =
                RequireAsset<ProductConfig>(CementProductConfigPath);
            ProductConfig boardProductConfig =
                RequireAsset<ProductConfig>(BoardProductConfigPath);
            ProductConfig brickProductConfig =
                RequireAsset<ProductConfig>(BrickProductConfigPath);
            ProductConfig drywallProductConfig =
                RequireAsset<ProductConfig>(DrywallProductConfigPath);
            ProductConfig paintProductConfig =
                RequireAsset<ProductConfig>(PaintProductConfigPath);
            ProductConfig insulationProductConfig =
                RequireAsset<ProductConfig>(InsulationProductConfigPath);
            DeliveryConfig cementDeliveryConfig =
                RequireAsset<DeliveryConfig>(CementDeliveryConfigPath);
            DeliveryConfig boardDeliveryConfig =
                RequireAsset<DeliveryConfig>(BoardDeliveryConfigPath);
            DeliveryConfig brickDeliveryConfig =
                RequireAsset<DeliveryConfig>(BrickDeliveryConfigPath);
            DeliveryConfig drywallDeliveryConfig =
                RequireAsset<DeliveryConfig>(DrywallDeliveryConfigPath);
            DeliveryConfig paintDeliveryConfig =
                RequireAsset<DeliveryConfig>(PaintDeliveryConfigPath);
            DeliveryConfig insulationDeliveryConfig =
                RequireAsset<DeliveryConfig>(InsulationDeliveryConfigPath);
            CustomerVehicleConfig customerVehicleConfig =
                RequireAsset<CustomerVehicleConfig>(CustomerVehicleConfigPath);
            CustomerConfig customerConfig = RequireAsset<CustomerConfig>(CustomerConfigPath);
            CustomerFlowConfig customerFlowConfig =
                RequireAsset<CustomerFlowConfig>(CustomerFlowConfigPath);
            EconomyConfig economyConfig = RequireAsset<EconomyConfig>(EconomyConfigPath);
            ProductRecoveryConfig productRecoveryConfig =
                RequireAsset<ProductRecoveryConfig>(ProductRecoveryConfigPath);
            PlatformTrolleyConfig platformTrolleyConfig =
                RequireAsset<PlatformTrolleyConfig>(PlatformTrolleyConfigPath);
            WarehouseWorkerConfig warehouseWorkerConfig =
                RequireAsset<WarehouseWorkerConfig>(WarehouseWorkerConfigPath);
            PlayerConfig playerConfig = RequireAsset<PlayerConfig>(PlayerConfigPath);
            CustomerProjectConfig cementProjectConfig =
                RequireAsset<CustomerProjectConfig>(CementProjectConfigPath);
            CustomerProjectConfig lumberProjectConfig =
                RequireAsset<CustomerProjectConfig>(LumberProjectConfigPath);
            CustomerProjectConfig workbenchProjectConfig =
                RequireAsset<CustomerProjectConfig>(WorkbenchProjectConfigPath);
            CustomerProjectConfig gardenWallProjectConfig =
                RequireAsset<CustomerProjectConfig>(GardenWallProjectConfigPath);
            CustomerProjectConfig drywallPartitionProjectConfig =
                RequireAsset<CustomerProjectConfig>(DrywallPartitionProjectConfigPath);
            CustomerProjectConfig workshopRenovationProjectConfig =
                RequireAsset<CustomerProjectConfig>(WorkshopRenovationProjectConfigPath);
            CustomerProjectConfig garageInsulationProjectConfig =
                RequireAsset<CustomerProjectConfig>(GarageInsulationProjectConfigPath);

            ProductConfig[] productConfigs = LoadConfigCatalogAssets<ProductConfig>();
            DeliveryConfig[] deliveryConfigs = LoadConfigCatalogAssets<DeliveryConfig>();
            CustomerProjectConfig[] projectConfigs =
                LoadConfigCatalogAssets<CustomerProjectConfig>();
            Require(productConfigs.Length == ExpectedProductTypes.Length &&
                    deliveryConfigs.Length == ExpectedProductTypes.Length &&
                    projectConfigs.Length == ExpectedProjectTypes.Length,
                "Product and delivery catalogs must contain six assets, and the customer " +
                "project catalog must contain seven assets.");
            var expectedKeys = new HashSet<ProductTypeId>(ExpectedProductTypes);
            var expectedProjectKeys = new HashSet<CustomerProjectTypeId>(ExpectedProjectTypes);
            var productKeys = new HashSet<ProductTypeId>(productConfigs.Select(config => config.ProductType));
            var deliveryKeys = new HashSet<ProductTypeId>(deliveryConfigs.Select(config => config.ProductType));
            var projectKeys = new HashSet<CustomerProjectTypeId>(
                projectConfigs.Select(config => config.ProjectType));
            Require(productKeys.SetEquals(expectedKeys) && productKeys.Count == productConfigs.Length,
                "ProductConfig assets must provide every ProductTypeId exactly once.");
            Require(deliveryKeys.SetEquals(expectedKeys) && deliveryKeys.Count == deliveryConfigs.Length,
                "DeliveryConfig assets must have one-to-one key parity with the product catalog.");
            Require(projectKeys.SetEquals(expectedProjectKeys) &&
                    projectKeys.Count == projectConfigs.Length,
                "CustomerProjectConfig assets must provide every CustomerProjectTypeId " +
                "exactly once.");

            ValidateProductCatalogEntry(
                cementProductConfig,
                cementDeliveryConfig,
                ProductTypeId.CementBag,
                unitPrice: 350,
                mass: 25f,
                carryMovementSpeed: 3.2f,
                productDropCollisionRadius: 0.51f,
                deliveryCount: 3,
                purchaseUnitPrice: 200);
            ValidateProductCatalogEntry(
                boardProductConfig,
                boardDeliveryConfig,
                ProductTypeId.BoardBundle,
                unitPrice: 480,
                mass: 18f,
                carryMovementSpeed: 2.6f,
                productDropCollisionRadius: 0.86f,
                deliveryCount: 3,
                purchaseUnitPrice: 260);
            ValidateProductCatalogEntry(
                brickProductConfig,
                brickDeliveryConfig,
                ProductTypeId.BrickPack,
                unitPrice: 330,
                mass: 24f,
                carryMovementSpeed: 2.9f,
                productDropCollisionRadius: 0.53f,
                deliveryCount: 3,
                purchaseUnitPrice: 190);
            ValidateProductCatalogEntry(
                drywallProductConfig,
                drywallDeliveryConfig,
                ProductTypeId.DrywallSheet,
                unitPrice: 260,
                mass: 14f,
                carryMovementSpeed: 2.8f,
                productDropCollisionRadius: 0.84f,
                deliveryCount: 3,
                purchaseUnitPrice: 80);
            ValidateProductCatalogEntry(
                paintProductConfig,
                paintDeliveryConfig,
                ProductTypeId.PaintBucket,
                unitPrice: 340,
                mass: 16f,
                carryMovementSpeed: 3.4f,
                productDropCollisionRadius: 0.49f,
                deliveryCount: 3,
                purchaseUnitPrice: 140);
            ValidateProductCatalogEntry(
                insulationProductConfig,
                insulationDeliveryConfig,
                ProductTypeId.InsulationRoll,
                unitPrice: 350,
                mass: 8f,
                carryMovementSpeed: 3.3f,
                productDropCollisionRadius: 0.72f,
                deliveryCount: 3,
                purchaseUnitPrice: 150);
            ValidateSingleProductProject(
                cementProjectConfig,
                CustomerProjectTypeId.CementFoundation,
                ProductTypeId.CementBag,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs);
            ValidateSingleProductProject(
                lumberProjectConfig,
                CustomerProjectTypeId.LumberShelving,
                ProductTypeId.BoardBundle,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs);
            ValidateWorkbenchProject(
                workbenchProjectConfig,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs);
            ValidateMixedProject(
                gardenWallProjectConfig,
                CustomerProjectTypeId.GardenWall,
                ProductTypeId.BrickPack,
                ProductTypeId.CementBag,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs,
                new[]
                {
                    new ProjectOfferMetrics(2, 390, 680, 290),
                    new ProjectOfferMetrics(3, 580, 1010, 430),
                    new ProjectOfferMetrics(3, 590, 1030, 440)
                });
            ValidateMixedProject(
                drywallPartitionProjectConfig,
                CustomerProjectTypeId.DrywallPartition,
                ProductTypeId.DrywallSheet,
                ProductTypeId.BoardBundle,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs,
                new[]
                {
                    new ProjectOfferMetrics(2, 340, 740, 400),
                    new ProjectOfferMetrics(3, 420, 1000, 580),
                    new ProjectOfferMetrics(3, 600, 1220, 620)
                });
            ValidateMixedProject(
                workshopRenovationProjectConfig,
                CustomerProjectTypeId.WorkshopRenovation,
                ProductTypeId.PaintBucket,
                ProductTypeId.DrywallSheet,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs,
                new[]
                {
                    new ProjectOfferMetrics(2, 220, 600, 380),
                    new ProjectOfferMetrics(3, 360, 940, 580),
                    new ProjectOfferMetrics(3, 300, 860, 560)
                });
            ValidateMixedProject(
                garageInsulationProjectConfig,
                CustomerProjectTypeId.GarageInsulation,
                ProductTypeId.InsulationRoll,
                ProductTypeId.BoardBundle,
                customerVehicleConfig,
                productConfigs,
                deliveryConfigs,
                new[]
                {
                    new ProjectOfferMetrics(2, 410, 830, 420),
                    new ProjectOfferMetrics(3, 560, 1180, 620),
                    new ProjectOfferMetrics(3, 670, 1310, 640)
                });

            Require(economyConfig.InitialMoney == 1100,
                $"{EconomyConfigPath} must start the prototype with 1100.");
            Require(Mathf.Approximately(productRecoveryConfig.MinimumWorldY, -10f),
                $"{ProductRecoveryConfigPath} must recover products below world Y -10.");
            Require(platformTrolleyConfig.PurchasePrice == 200 &&
                    platformTrolleyConfig.RequiredCompletedOrderCount == 2 &&
                    platformTrolleyConfig.Capacity == 3 &&
                    Mathf.Approximately(platformTrolleyConfig.MovementSpeed, 3.8f) &&
                    Mathf.Approximately(platformTrolleyConfig.FollowDistance, 1.7f),
                $"{PlatformTrolleyConfigPath} must use price 200, unlock after two rewarded " +
                "orders, capacity 3, movement speed 3.8 and follow distance 1.7.");
            Require(platformTrolleyConfig.Capacity >= customerVehicleConfig.CargoCapacity &&
                    platformTrolleyConfig.MovementSpeed >
                    productConfigs.Max(config => config.CarryMovementSpeed) &&
                    platformTrolleyConfig.MovementSpeed < playerConfig.WalkSpeed,
                "The trolley must fit a complete order and move faster than carried products " +
                "but slower than the unburdened player.");
            Require(warehouseWorkerConfig.RequiredCompletedOrderCount == 4 &&
                    warehouseWorkerConfig.HirePrice == 400 &&
                    warehouseWorkerConfig.DailyWage == 100 &&
                    Mathf.Approximately(warehouseWorkerConfig.MovementSpeed, 2.8f) &&
                    Mathf.Approximately(warehouseWorkerConfig.Acceleration, 12f) &&
                    Mathf.Approximately(warehouseWorkerConfig.AngularSpeed, 720f) &&
                    Mathf.Approximately(warehouseWorkerConfig.StoppingDistance, 0.2f) &&
                    Mathf.Approximately(warehouseWorkerConfig.NavigationSampleRadius, 2f) &&
                    Mathf.Approximately(warehouseWorkerConfig.TaskTimeout, 20f) &&
                    warehouseWorkerConfig.TrolleyCapacity == 3 &&
                    Mathf.Approximately(
                        warehouseWorkerConfig.TrolleyFollowDistance,
                        1.7f) &&
                    warehouseWorkerConfig.TrolleyCapacity ==
                    customerVehicleConfig.CargoCapacity,
                $"{WarehouseWorkerConfigPath} must author the frozen worker progression, " +
                "economy, navigation, recovery and bundled trolley values.");
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
            Require(customerVehicleConfig.CargoCapacity == 3,
                $"{CustomerVehicleConfigPath} must expose exactly three customer cargo slots.");
            Require(customerFlowConfig.ParkingCapacity == 3 &&
                    Mathf.Approximately(customerFlowConfig.FirstArrivalDelay, 10f) &&
                    Mathf.Approximately(customerFlowConfig.DefaultPatienceDuration, 120f) &&
                    Mathf.Approximately(customerFlowConfig.PatienceWarningThreshold, 30f),
                $"{CustomerFlowConfigPath} must author three parking spots, a ten-second " +
                "first-arrival delay, 120 seconds of patience and a 30-second warning.");
            CustomerArrivalSchedulePoint[] expectedArrivalSchedule =
            {
                new(8 * 60, 45f),
                new(10 * 60, 36f),
                new(13 * 60, 26f),
                new(17 * 60, 28f),
                new(19 * 60, 45f),
                new(20 * 60, 70f)
            };
            Require(customerFlowConfig.ArrivalSchedule.Count ==
                    expectedArrivalSchedule.Length &&
                    customerFlowConfig.ArrivalSchedule
                        .Select((point, index) =>
                            point.Minute == expectedArrivalSchedule[index].Minute &&
                            Mathf.Approximately(
                                point.Delay,
                                expectedArrivalSchedule[index].Delay))
                        .All(matches => matches),
                $"{CustomerFlowConfigPath} must preserve the deterministic morning, midday " +
                "and evening arrival schedule.");
            Require(Mathf.Approximately(customerConfig.MovementSpeed, 2.4f),
                $"{CustomerConfigPath} must use a customer movement speed of 2.4.");
            Require(Mathf.Approximately(customerConfig.RotationSpeed, 360f),
                $"{CustomerConfigPath} must use a customer rotation speed of 360 degrees per second.");
            Require(Mathf.Approximately(customerConfig.WaypointTolerance, 0.08f),
                $"{CustomerConfigPath} must use a waypoint tolerance of 0.08.");

            ProductConfig[] orderedProductConfigs =
            {
                cementProductConfig,
                boardProductConfig,
                brickProductConfig,
                drywallProductConfig,
                paintProductConfig,
                insulationProductConfig
            };
            string[] orderedProductConfigPaths =
            {
                CementProductConfigPath,
                BoardProductConfigPath,
                BrickProductConfigPath,
                DrywallProductConfigPath,
                PaintProductConfigPath,
                InsulationProductConfigPath
            };
            string[] productPrefabPaths =
            {
                CementProductPrefabPath,
                BoardProductPrefabPath,
                BrickProductPrefabPath,
                DrywallProductPrefabPath,
                PaintProductPrefabPath,
                InsulationProductPrefabPath
            };
            GameObject[] productPrefabs = productPrefabPaths
                .Select(RequireAsset<GameObject>)
                .ToArray();
            Vector3[] productGeometry = new Vector3[productPrefabs.Length];
            for (int index = 0; index < productPrefabs.Length; index++)
            {
                productGeometry[index] = ValidateProductPrefab(
                    orderedProductConfigs[index],
                    orderedProductConfigPaths[index],
                    productPrefabs[index],
                    productPrefabPaths[index],
                    requireUnitScale: index != 0);
                float boundingRadius = ReadSolidProductBoundingRadius(
                    productPrefabs[index],
                    productPrefabPaths[index]);
                Require(orderedProductConfigs[index].ProductDropCollisionRadius >=
                        boundingRadius,
                    $"{orderedProductConfigPaths[index]} must conservatively contain every " +
                    $"corner of the solid collider from {productPrefabPaths[index]}.");
            }

            GameObject cementProductPrefab = productPrefabs[0];
            GameObject boardProductPrefab = productPrefabs[1];
            Vector3 cementGeometry = productGeometry[0];
            Vector3 boardGeometry = productGeometry[1];
            Require(Vector3.Distance(productGeometry[2], new Vector3(0.82f, 0.34f, 0.52f)) <
                        0.001f &&
                    Vector3.Distance(productGeometry[3], new Vector3(1.55f, 0.18f, 0.46f)) <
                        0.001f &&
                    Vector3.Distance(productGeometry[4], new Vector3(0.52f, 0.58f, 0.52f)) <
                        0.001f &&
                    Vector3.Distance(productGeometry[5], new Vector3(1.15f, 0.58f, 0.58f)) <
                        0.001f,
                "Brick, drywall, paint and insulation prefabs must preserve their frozen " +
                "solid BoxCollider dimensions.");
            float boardLength = Mathf.Max(boardGeometry.x, boardGeometry.z);
            float cementLength = Mathf.Max(cementGeometry.x, cementGeometry.z);
            Require(boardLength >= 1.4f && boardLength <= 1.6f,
                $"{BoardProductPrefabPath} must be approximately 1.4-1.6 metres long.");
            Require(boardLength > cementLength + 0.5f,
                "Board bundle and cement bag must have materially distinct geometry.");
            Require(boardProductPrefab.GetComponentsInChildren<Renderer>(true).Length >= 6,
                $"{BoardProductPrefabPath} must visibly contain four boards and retaining straps.");
            ValidateExpandedProductVisuals(productPrefabs, productPrefabPaths);

            Vector3 maximumProductGeometry = productGeometry.Aggregate(
                Vector3.zero,
                (maximum, geometry) => new Vector3(
                    Mathf.Max(maximum.x, geometry.x),
                    Mathf.Max(maximum.y, geometry.y),
                    Mathf.Max(maximum.z, geometry.z)));

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
            Require(ProcurementCartFactory.CurrentDeliveryPackageCapacity == 3,
                "The frozen mixed-delivery cart must allow exactly three packages.");
            int maximumDeliverySize = checked(
                deliveryConfigs.Max(config => config.ProductCount) *
                ProcurementCartFactory.CurrentDeliveryPackageCapacity);
            Require(deliverySlots.Length == maximumDeliverySize && maximumDeliverySize == 9,
                $"{DeliveryVehiclePrefabPath} must expose exactly {maximumDeliverySize} cargo slots " +
                "for three packages of three product units.");
            Require(deliverySlots.All(slot => slot.IsChildOf(deliveryPrefab.transform)),
                $"Every cargo slot in {DeliveryVehiclePrefabPath} must belong to the prefab hierarchy.");
            Transform cargoPallet = deliveryPrefab.transform.Find("Cargo Pallet");
            Require(cargoPallet != null &&
                    maximumProductGeometry.x <= cargoPallet.localScale.x &&
                    maximumProductGeometry.z <= 0.92f,
                $"The pallet and cargo spacing in {DeliveryVehiclePrefabPath} must contain every " +
                "catalog product hull.");
            float palletTop = cargoPallet.localPosition.y + cargoPallet.localScale.y * 0.5f;
            for (int index = 0; index < deliverySlots.Length; index++)
            {
                int levelIndex = index / 3;
                int positionIndex = index % 3;
                Vector3 expectedPosition = new(
                    0f,
                    1.5f + levelIndex * 0.67f,
                    -1.8f + positionIndex * 0.92f);
                Require(deliverySlots[index].name == $"Cargo Slot {index + 1}" &&
                        Vector3.Distance(deliverySlots[index].localPosition, expectedPosition) <
                        0.001f,
                    $"{DeliveryVehiclePrefabPath} cargo slot {index + 1} must preserve the " +
                    "three-by-three mixed-delivery layout.");
                if (levelIndex == 0)
                {
                    Require(deliverySlots[index].localPosition.y -
                            maximumProductGeometry.y * 0.5f >= palletTop + 0.04f,
                        $"The lower cargo layer in {DeliveryVehiclePrefabPath} must clear the " +
                        "visible pallet for every product hull.");
                }
                else
                {
                    float previousLevelY = deliverySlots[index - 3].localPosition.y;
                    Require(deliverySlots[index].localPosition.y - previousLevelY >=
                            maximumProductGeometry.y + 0.08f,
                        $"Adjacent cargo layers in {DeliveryVehiclePrefabPath} must retain at " +
                        "least 0.08 metres of hull clearance.");
                }
            }
            Require(productPrefabs.All(productPrefab =>
                    !ContainsPrefabInstance(deliveryPrefab, productPrefab)),
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
            LocalizedTextMeshView[] customerVehicleLabels =
                customerVehiclePrefab.GetComponentsInChildren<LocalizedTextMeshView>(true);
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
            Require(customerSlots.Length == customerVehicleConfig.CargoCapacity &&
                    customerSlots.All(slot => slot.IsChildOf(customerVehiclePrefab.transform)),
                $"{CustomerVehiclePrefabPath} must expose exactly " +
                $"{customerVehicleConfig.CargoCapacity} " +
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
            Require(productPrefabs.All(productPrefab =>
                    !ContainsPrefabInstance(customerVehiclePrefab, productPrefab)),
                $"{CustomerVehiclePrefabPath} must be empty before runtime order loading.");
            Require(customerVehicleConfig.ViewPrefab == customerViews[0],
                $"{CustomerVehicleConfigPath} must reference the InteractionView root from " +
                $"{CustomerVehiclePrefabPath}.");
            ValidateLocalizedWorldLabels(
                customerVehicleLabels,
                new[] { LocalizationKey.WorldCustomerVehicleLoading },
                CustomerVehiclePrefabPath);

            GameObject customerPrefab = RequireAsset<GameObject>(CustomerPrefabPath);
            ValidatePrefabRoot(customerPrefab, CustomerPrefabPath, requireUnitScale: true);
            EntityBehaviour[] customerActorViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(customerPrefab, CustomerPrefabPath);
            TransformRegistrar[] customerActorTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(customerPrefab, CustomerPrefabPath);
            RigidbodyRegistrar[] customerActorRigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(customerPrefab, CustomerPrefabPath);
            CustomerDissatisfactionView[] customerMoodViews =
                RequireExactlyOneInPrefab<CustomerDissatisfactionView>(
                    customerPrefab,
                    CustomerPrefabPath);
            CustomerDissatisfactionViewRegistrar[] customerMoodRegistrars =
                RequireExactlyOneInPrefab<CustomerDissatisfactionViewRegistrar>(
                    customerPrefab,
                    CustomerPrefabPath);
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
                    customerMoodViews[0].gameObject == customerPrefab &&
                    customerMoodRegistrars[0].gameObject == customerPrefab &&
                    customerActorRigidbodies[0].gameObject == customerPrefab,
                $"{CustomerPrefabPath} must keep its generic entity, transform, Rigidbody and " +
                "customer-mood boundaries on the root.");
            var expectedCustomerActorRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(CustomerDissatisfactionViewRegistrar)
            };
            Require(customerActorRegistrars.Length == expectedCustomerActorRegistrarTypes.Count &&
                    new HashSet<Type>(customerActorRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedCustomerActorRegistrarTypes),
                $"{CustomerPrefabPath} must contain exactly the generic Transform, Rigidbody " +
                "and customer-dissatisfaction view registrars.");
            Require(customerActorColliders.Length == 0 &&
                    customerPrefab.GetComponentsInChildren<InteractionView>(true).Length == 0 &&
                    customerPrefab.GetComponentsInChildren<CollidersRegistrar>(true).Length == 0,
                $"{CustomerPrefabPath} must not expose interaction or collider gameplay adapters.");
            CustomerDissatisfactionView moodView = customerMoodViews[0];
            Renderer[] moodRenderers = moodView.Renderers;
            Transform leftShoulder = moodView.LeftShoulder;
            Transform rightShoulder = moodView.RightShoulder;
            TextMesh moodLabel = moodView.WorldLabel;
            Renderer moodLabelRenderer = moodLabel.GetComponent<Renderer>();
            Renderer[] bodyRenderers = customerActorRenderers
                .Where(renderer => renderer != moodLabelRenderer)
                .ToArray();
            Require(customerActorRenderers.Length == 10 &&
                    bodyRenderers.Length == 9 &&
                    moodRenderers.Length == bodyRenderers.Length &&
                    new HashSet<Renderer>(moodRenderers).SetEquals(bodyRenderers) &&
                    !moodRenderers.Contains(moodLabelRenderer),
                $"{CustomerPrefabPath} must tint exactly its nine low-poly body renderers " +
                "without tinting the world label renderer.");
            Renderer preservedMoodRenderer = moodRenderers[0];
            moodRenderers[0] = null;
            Require(moodView.Renderers[0] == preservedMoodRenderer,
                $"{nameof(CustomerDissatisfactionView.Renderers)} must return a defensive " +
                "array clone.");
            Transform leftArm = leftShoulder.Find("Left Arm");
            Transform rightArm = rightShoulder.Find("Right Arm");
            Require(leftShoulder.name == "Left Shoulder" &&
                    rightShoulder.name == "Right Shoulder" &&
                    leftShoulder.parent == customerPrefab.transform &&
                    rightShoulder.parent == customerPrefab.transform &&
                    leftArm != null && leftArm.parent == leftShoulder &&
                    rightArm != null && rightArm.parent == rightShoulder &&
                    Vector3.Distance(
                        leftShoulder.localPosition,
                        new Vector3(-0.42f, 1.46f, 0f)) < 0.001f &&
                    Vector3.Distance(
                        rightShoulder.localPosition,
                        new Vector3(0.42f, 1.46f, 0f)) < 0.001f &&
                    Quaternion.Angle(leftShoulder.localRotation, Quaternion.identity) < 0.01f &&
                    Quaternion.Angle(rightShoulder.localRotation, Quaternion.identity) < 0.01f &&
                    Vector3.Distance(
                        leftArm.localPosition,
                        new Vector3(0f, -0.28f, 0f)) < 0.001f &&
                    Vector3.Distance(
                        rightArm.localPosition,
                        new Vector3(0f, -0.28f, 0f)) < 0.001f,
                $"{CustomerPrefabPath} must expose neutral shoulder pivots above their arm " +
                "mesh children so dissatisfaction can raise and animate both arms.");
            Require(moodLabel.name == "Dissatisfaction Label" &&
                    moodLabel.transform.parent == customerPrefab.transform &&
                    Vector3.Distance(
                        moodLabel.transform.localPosition,
                        new Vector3(0f, 2.35f, 0f)) < 0.001f &&
                    moodLabel.anchor == TextAnchor.MiddleCenter &&
                    moodLabel.alignment == TextAlignment.Center &&
                    moodLabel.fontStyle == FontStyle.Bold &&
                    moodLabel.fontSize == 64 &&
                    Mathf.Approximately(moodLabel.characterSize, 0.045f) &&
                    moodLabel.color.r >= 0.95f &&
                    moodLabel.color.g <= 0.1f &&
                    moodLabel.color.b <= 0.1f &&
                    moodLabel.text == "НЕДОВОЛЕН • МОЖЕТ УЙТИ" &&
                    !moodLabel.gameObject.activeSelf &&
                    !moodView.IsDissatisfied,
                $"{CustomerPrefabPath} must author one initially hidden red localized " +
                "dissatisfaction label above the customer.");
            Rigidbody customerActorBody = customerActorRigidbodies[0];
            Require(customerActorBody.isKinematic && !customerActorBody.useGravity &&
                    customerActorBody.interpolation == RigidbodyInterpolation.None,
                $"The Rigidbody in {CustomerPrefabPath} must be kinematic, gravity-free and use " +
                $"{RigidbodyInterpolation.None} interpolation.");
            Require(customerConfig.ViewPrefab == customerActorViews[0],
                $"{CustomerConfigPath} must reference the EntityBehaviour root from " +
                $"{CustomerPrefabPath}.");

            GameObject workerPrefab = RequireAsset<GameObject>(WarehouseWorkerPrefabPath);
            ValidatePrefabRoot(workerPrefab, WarehouseWorkerPrefabPath, requireUnitScale: true);
            EntityBehaviour[] workerViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(
                    workerPrefab,
                    WarehouseWorkerPrefabPath);
            TransformRegistrar[] workerTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(
                    workerPrefab,
                    WarehouseWorkerPrefabPath);
            NavMeshAgentRegistrar[] workerNavigationRegistrars =
                RequireExactlyOneInPrefab<NavMeshAgentRegistrar>(
                    workerPrefab,
                    WarehouseWorkerPrefabPath);
            CarryAnchorRegistrar[] workerCarryAnchors =
                RequireExactlyOneInPrefab<CarryAnchorRegistrar>(
                    workerPrefab,
                    WarehouseWorkerPrefabPath);
            NavMeshAgent[] workerAgents =
                RequireExactlyOneInPrefab<NavMeshAgent>(
                    workerPrefab,
                    WarehouseWorkerPrefabPath);
            EntityComponentRegistrar[] workerRegistrars =
                workerPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Collider[] workerColliders =
                workerPrefab.GetComponentsInChildren<Collider>(true);
            Renderer[] workerRenderers =
                workerPrefab.GetComponentsInChildren<Renderer>(true);

            var expectedWorkerRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(NavMeshAgentRegistrar),
                typeof(CarryAnchorRegistrar)
            };
            Require(workerViews[0].gameObject == workerPrefab &&
                    workerTransforms[0].gameObject == workerPrefab &&
                    workerNavigationRegistrars[0].gameObject == workerPrefab &&
                    workerAgents[0].gameObject == workerPrefab &&
                    workerCarryAnchors[0].transform.IsChildOf(workerPrefab.transform) &&
                    workerCarryAnchors[0].name == "Carry Anchor" &&
                    Vector3.Distance(
                        workerCarryAnchors[0].transform.localPosition,
                        new Vector3(0f, 1.02f, 0.66f)) < 0.001f &&
                    Quaternion.Angle(
                        workerCarryAnchors[0].transform.localRotation,
                        Quaternion.identity) < 0.01f,
                $"{WarehouseWorkerPrefabPath} must expose one generic view root and a child " +
                "carry anchor at its exact customer-loading alignment pose.");
            Require(workerRegistrars.Length == expectedWorkerRegistrarTypes.Count &&
                    new HashSet<Type>(workerRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedWorkerRegistrarTypes),
                $"{WarehouseWorkerPrefabPath} must contain exactly Transform, NavMeshAgent and " +
                "CarryAnchor registrars.");
            Require(workerColliders.Length == 0 &&
                    workerPrefab.GetComponentsInChildren<InteractionView>(true).Length == 0 &&
                    workerPrefab.GetComponentsInChildren<CollidersRegistrar>(true).Length == 0,
                $"{WarehouseWorkerPrefabPath} must not expose interaction or collision targets.");
            Require(workerRenderers.Length >= 14,
                $"{WarehouseWorkerPrefabPath} must contain a visible blue/yellow worker silhouette.");
            string[] blueWorkwearParts =
            {
                "Torso", "Left Arm", "Right Arm", "Left Leg", "Right Leg"
            };
            string[] yellowSafetyParts =
            {
                "Safety Vest Front", "Safety Vest Back", "Hard Hat", "Hard Hat Brim"
            };
            Require(blueWorkwearParts.All(part =>
                        workerRenderers.Single(renderer => renderer.name == part)
                            .sharedMaterial.name == "BrandBlue") &&
                    yellowSafetyParts.All(part =>
                        workerRenderers.Single(renderer => renderer.name == part)
                            .sharedMaterial.name == "SafetyYellow") &&
                    workerRenderers.Where(renderer =>
                            renderer.name is "Left Shoe" or "Right Shoe")
                        .All(renderer => renderer.sharedMaterial.name == "DarkMetal"),
                $"{WarehouseWorkerPrefabPath} must visibly use blue workwear, yellow safety " +
                "vest/helmet and dark shoes.");
            NavMeshAgent workerAgent = workerAgents[0];
            Require(workerAgent.agentTypeID == 0 &&
                    Mathf.Approximately(workerAgent.speed, warehouseWorkerConfig.MovementSpeed) &&
                    Mathf.Approximately(workerAgent.acceleration, warehouseWorkerConfig.Acceleration) &&
                    Mathf.Approximately(workerAgent.angularSpeed, warehouseWorkerConfig.AngularSpeed) &&
                    Mathf.Approximately(workerAgent.stoppingDistance,
                        warehouseWorkerConfig.StoppingDistance) &&
                    workerAgent.autoBraking && workerAgent.autoRepath,
                $"{WarehouseWorkerPrefabPath} NavMeshAgent must mirror its config and repath.");
            Require(warehouseWorkerConfig.ViewPrefab == workerViews[0],
                $"{WarehouseWorkerConfigPath} must reference the EntityBehaviour root from " +
                $"{WarehouseWorkerPrefabPath}.");

            GameObject workerTrolleyPrefab =
                RequireAsset<GameObject>(WarehouseWorkerTrolleyPrefabPath);
            ValidatePrefabRoot(
                workerTrolleyPrefab,
                WarehouseWorkerTrolleyPrefabPath,
                requireUnitScale: true);
            EntityBehaviour[] workerTrolleyViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            TransformRegistrar[] workerTrolleyTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            RigidbodyRegistrar[] workerTrolleyRigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            CollidersRegistrar[] workerTrolleyColliderRegistrars =
                RequireExactlyOneInPrefab<CollidersRegistrar>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            SlotsRegistrar[] workerTrolleySlotRegistrars =
                RequireExactlyOneInPrefab<SlotsRegistrar>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            Rigidbody[] workerTrolleyRigidbodies =
                RequireExactlyOneInPrefab<Rigidbody>(
                    workerTrolleyPrefab,
                    WarehouseWorkerTrolleyPrefabPath);
            EntityComponentRegistrar[] workerTrolleyRegistrars =
                workerTrolleyPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Collider[] workerTrolleyColliders =
                workerTrolleyPrefab.GetComponentsInChildren<Collider>(true);
            Renderer[] workerTrolleyRenderers =
                workerTrolleyPrefab.GetComponentsInChildren<Renderer>(true);
            Transform[] workerTrolleySlots = ReadSlots(
                workerTrolleySlotRegistrars[0],
                WarehouseWorkerTrolleyPrefabPath);

            var expectedWorkerTrolleyRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(CollidersRegistrar),
                typeof(SlotsRegistrar)
            };
            Require(workerTrolleyViews[0].GetType() == typeof(EntityBehaviour) &&
                    workerTrolleyViews[0].gameObject == workerTrolleyPrefab &&
                    workerTrolleyTransforms[0].gameObject == workerTrolleyPrefab &&
                    workerTrolleyRigidbodyRegistrars[0].gameObject ==
                    workerTrolleyPrefab &&
                    workerTrolleyColliderRegistrars[0].gameObject ==
                    workerTrolleyPrefab &&
                    workerTrolleySlotRegistrars[0].gameObject == workerTrolleyPrefab &&
                    workerTrolleyRigidbodies[0].gameObject == workerTrolleyPrefab,
                $"{WarehouseWorkerTrolleyPrefabPath} must expose one generic view root " +
                "with its Rigidbody and registrars on that root.");
            Require(workerTrolleyRegistrars.Length ==
                        expectedWorkerTrolleyRegistrarTypes.Count &&
                    new HashSet<Type>(workerTrolleyRegistrars.Select(
                            registrar => registrar.GetType()))
                        .SetEquals(expectedWorkerTrolleyRegistrarTypes),
                $"{WarehouseWorkerTrolleyPrefabPath} must contain exactly Transform, " +
                "Rigidbody, Colliders and Slots registrars.");
            Require(workerTrolleyPrefab.GetComponentsInChildren<InteractionView>(true)
                        .Length == 0 &&
                    workerTrolleyPrefab.GetComponentsInChildren<InteractionHighlight>(true)
                        .Length == 0 &&
                    workerTrolleyPrefab.GetComponentsInChildren<InteractionViewRegistrar>(true)
                        .Length == 0 &&
                    workerTrolleyPrefab.GetComponentsInChildren<NavMeshObstacle>(true)
                        .Length == 0 &&
                    workerTrolleyPrefab.GetComponentsInChildren<NavMeshAgent>(true)
                        .Length == 0 &&
                    workerTrolleyPrefab.transform.Find("Interaction Area") == null &&
                    workerTrolleyPrefab.transform.Find("Push Point") == null,
                $"{WarehouseWorkerTrolleyPrefabPath} must not expose a player-focusable " +
                "interaction view, highlight, trigger, navigation component or authored " +
                "push point.");
            Require(workerTrolleySlots.Length == warehouseWorkerConfig.TrolleyCapacity &&
                    workerTrolleySlots.Length == 3 &&
                    workerTrolleySlots.Select((slot, index) =>
                            slot.name == $"Cargo Slot {index + 1}" &&
                            slot.IsChildOf(workerTrolleyPrefab.transform) &&
                            Vector3.Distance(
                                slot.localPosition,
                                new Vector3(0f, 0.66f, -0.66f + index * 0.66f)) <
                            0.001f)
                        .All(matches => matches) &&
                    workerTrolleySlots.Distinct().Count() == workerTrolleySlots.Length,
                $"{WarehouseWorkerTrolleyPrefabPath} must expose three exact unique cargo " +
                "slots matching WarehouseWorkerConfig.");
            Transform workerTrolleyBodyTransform =
                workerTrolleyPrefab.transform.Find("Body Collider");
            BoxCollider workerTrolleyBodyCollider =
                workerTrolleyBodyTransform?.GetComponent<BoxCollider>();
            Require(workerTrolleyColliders.Length == 1 &&
                    workerTrolleyBodyCollider != null &&
                    workerTrolleyBodyCollider.enabled &&
                    !workerTrolleyBodyCollider.isTrigger &&
                    workerTrolleyBodyCollider.gameObject.layer == ignoreRaycastLayer &&
                    Vector3.Distance(
                        workerTrolleyBodyCollider.center,
                        new Vector3(0f, 0.27f, 0.15f)) < 0.001f &&
                    Vector3.Distance(
                        workerTrolleyBodyCollider.size,
                        new Vector3(2f, 0.5f, 2.1f)) < 0.001f,
                $"{WarehouseWorkerTrolleyPrefabPath} must expose only its exact solid " +
                "Ignore Raycast body hull.");
            Rigidbody workerTrolleyBody = workerTrolleyRigidbodies[0];
            Require(Mathf.Approximately(workerTrolleyBody.mass, 45f) &&
                    workerTrolleyBody.isKinematic && !workerTrolleyBody.useGravity &&
                    workerTrolleyBody.interpolation == RigidbodyInterpolation.None &&
                    workerTrolleyBody.collisionDetectionMode ==
                    CollisionDetectionMode.ContinuousSpeculative,
                $"{WarehouseWorkerTrolleyPrefabPath} must use its exact deterministic " +
                "kinematic Rigidbody contract.");
            Require(workerTrolleyPrefab.transform.Find("Deck") != null &&
                    workerTrolleyPrefab.transform.Find("Deck Inlay") != null &&
                    workerTrolleyPrefab.transform.Find("Handle") != null &&
                    workerTrolleyPrefab.GetComponentsInChildren<Transform>(true)
                        .Count(candidate => candidate.name.EndsWith(
                            "Wheel", StringComparison.Ordinal)) == 4 &&
                    workerTrolleyRenderers.Single(renderer => renderer.name == "Deck")
                        .sharedMaterial.name == "BrandBlue" &&
                    workerTrolleyRenderers.Single(renderer =>
                            renderer.name == "Deck Inlay")
                        .sharedMaterial.name == "Timber" &&
                    workerTrolleyRenderers.Count(renderer =>
                        renderer.name is "Left Rail" or "Right Rail" or "Handle") == 3 &&
                    workerTrolleyRenderers.Where(renderer =>
                            renderer.name is "Left Rail" or "Right Rail" or "Handle")
                        .All(renderer => renderer.sharedMaterial.name == "SafetyYellow") &&
                    workerTrolleyRenderers.Where(renderer => renderer.name.EndsWith(
                            "Wheel", StringComparison.Ordinal))
                        .All(renderer => renderer.sharedMaterial.name == "DarkMetal"),
                $"{WarehouseWorkerTrolleyPrefabPath} must visibly match the worker's " +
                "blue/yellow safety palette with one deck, handle and four wheels.");
            Require(warehouseWorkerConfig.TrolleyViewPrefab == workerTrolleyViews[0],
                $"{WarehouseWorkerConfigPath} must reference the EntityBehaviour root from " +
                $"{WarehouseWorkerTrolleyPrefabPath}.");
            Require(productPrefabs.All(productPrefab =>
                    !ContainsPrefabInstance(workerTrolleyPrefab, productPrefab)),
                $"{WarehouseWorkerTrolleyPrefabPath} must be empty before runtime cargo " +
                "placement.");

            GameObject trolleyPrefab = RequireAsset<GameObject>(PlatformTrolleyPrefabPath);
            ValidatePrefabRoot(trolleyPrefab, PlatformTrolleyPrefabPath, requireUnitScale: true);
            InteractionView[] trolleyViews =
                RequireExactlyOneInPrefab<InteractionView>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            EntityBehaviour[] trolleyEntityViews =
                RequireExactlyOneInPrefab<EntityBehaviour>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            TransformRegistrar[] trolleyTransforms =
                RequireExactlyOneInPrefab<TransformRegistrar>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            RigidbodyRegistrar[] trolleyRigidbodyRegistrars =
                RequireExactlyOneInPrefab<RigidbodyRegistrar>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            InteractionViewRegistrar[] trolleyInteractionRegistrars =
                RequireExactlyOneInPrefab<InteractionViewRegistrar>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            CollidersRegistrar[] trolleyColliderRegistrars =
                RequireExactlyOneInPrefab<CollidersRegistrar>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            SlotsRegistrar[] trolleySlotRegistrars =
                RequireExactlyOneInPrefab<SlotsRegistrar>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            Rigidbody[] trolleyRigidbodies =
                RequireExactlyOneInPrefab<Rigidbody>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            InteractionHighlight[] trolleyHighlights =
                RequireExactlyOneInPrefab<InteractionHighlight>(
                    trolleyPrefab, PlatformTrolleyPrefabPath);
            EntityComponentRegistrar[] trolleyRegistrars =
                trolleyPrefab.GetComponentsInChildren<EntityComponentRegistrar>(true);
            Collider[] trolleyColliders =
                trolleyPrefab.GetComponentsInChildren<Collider>(true);
            Transform[] trolleySlots = ReadSlots(
                trolleySlotRegistrars[0], PlatformTrolleyPrefabPath);

            Require(trolleyViews[0].GetType() == typeof(InteractionView) &&
                    trolleyViews[0].gameObject == trolleyPrefab &&
                    trolleyEntityViews[0] == trolleyViews[0],
                $"{PlatformTrolleyPrefabPath} must use one generic InteractionView root.");
            Require(trolleyTransforms[0].gameObject == trolleyPrefab &&
                    trolleyRigidbodyRegistrars[0].gameObject == trolleyPrefab &&
                    trolleyInteractionRegistrars[0].gameObject == trolleyPrefab &&
                    trolleyColliderRegistrars[0].gameObject == trolleyPrefab &&
                    trolleySlotRegistrars[0].gameObject == trolleyPrefab &&
                    trolleyRigidbodies[0].gameObject == trolleyPrefab,
                $"All generic platform trolley registrars and its Rigidbody must be on the " +
                $"root of {PlatformTrolleyPrefabPath}.");
            var expectedTrolleyRegistrarTypes = new HashSet<Type>
            {
                typeof(TransformRegistrar),
                typeof(RigidbodyRegistrar),
                typeof(InteractionViewRegistrar),
                typeof(CollidersRegistrar),
                typeof(SlotsRegistrar)
            };
            Require(trolleyRegistrars.Length == expectedTrolleyRegistrarTypes.Count &&
                    new HashSet<Type>(trolleyRegistrars.Select(registrar => registrar.GetType()))
                        .SetEquals(expectedTrolleyRegistrarTypes),
                $"{PlatformTrolleyPrefabPath} must contain exactly the generic Transform, " +
                "Rigidbody, InteractionView, Colliders and Slots registrars.");
            Require(trolleySlots.Length == platformTrolleyConfig.Capacity &&
                    trolleySlots.All(slot => slot.IsChildOf(trolleyPrefab.transform)),
                $"{PlatformTrolleyPrefabPath} must expose exactly three unique cargo slots " +
                "inside its hierarchy.");
            Require(trolleyPrefab.transform.Find("Deck") != null &&
                    trolleyPrefab.transform.Find("Handle") != null &&
                    trolleyPrefab.GetComponentsInChildren<Transform>(true)
                        .Count(candidate => candidate.name.EndsWith(
                            "Wheel", StringComparison.Ordinal)) == 4,
                $"{PlatformTrolleyPrefabPath} must visibly contain a deck, handle and four wheels.");

            Transform trolleyBodyColliderTransform =
                trolleyPrefab.transform.Find("Body Collider");
            Transform trolleyInteractionAreaTransform =
                trolleyPrefab.transform.Find("Interaction Area");
            Require(trolleyBodyColliderTransform != null &&
                    trolleyInteractionAreaTransform != null,
                $"{PlatformTrolleyPrefabPath} must contain separate body and interaction colliders.");
            Collider trolleyBodyCollider = trolleyBodyColliderTransform.GetComponent<Collider>();
            Collider trolleyInteractionCollider =
                trolleyInteractionAreaTransform.GetComponent<Collider>();
            Require(trolleyColliders.Length == 2 &&
                    trolleyBodyCollider != null && !trolleyBodyCollider.isTrigger &&
                    trolleyBodyCollider.gameObject.layer == ignoreRaycastLayer &&
                    trolleyInteractionCollider != null &&
                    trolleyInteractionCollider.enabled &&
                    trolleyInteractionCollider.isTrigger &&
                    trolleyInteractionCollider.gameObject.layer != ignoreRaycastLayer,
                $"{PlatformTrolleyPrefabPath} must keep its solid body on Ignore Raycast and " +
                "expose exactly one enabled, raycastable trolley interaction point.");
            BoxCollider trolleyBodyBox = trolleyBodyCollider as BoxCollider;
            BoxCollider trolleyHandleTrigger = trolleyInteractionCollider as BoxCollider;
            Require(trolleyBodyBox != null &&
                    Vector3.Distance(
                        trolleyBodyBox.center,
                        new Vector3(0f, 0.27f, 0.15f)) < 0.001f &&
                    Vector3.Distance(
                        trolleyBodyBox.size,
                        new Vector3(2f, 0.5f, 2.1f)) < 0.001f &&
                    trolleyHandleTrigger != null &&
                    Vector3.Distance(
                        trolleyHandleTrigger.center,
                        new Vector3(0f, 1.76f, -1.12f)) < 0.001f &&
                    Vector3.Distance(
                        trolleyHandleTrigger.size,
                        new Vector3(1.8f, 0.35f, 0.3f)) < 0.001f,
                $"{PlatformTrolleyPrefabPath} must keep its body hull under the forward deck " +
                "and its interaction trigger only on the handle.");
            Bounds trolleyHandleBounds = new(
                trolleyHandleTrigger.center,
                trolleyHandleTrigger.size);
            Require(trolleySlots.All(slot => !trolleyHandleBounds.Contains(
                        trolleyHandleTrigger.transform.InverseTransformPoint(slot.position))),
                $"The handle trigger in {PlatformTrolleyPrefabPath} must not occlude cargo slots " +
                "from physical product focus.");
            CharacterController playerController =
                RequireAsset<GameObject>(PlayerPrefabPath).GetComponent<CharacterController>();
            float rearBodyDistance = platformTrolleyConfig.FollowDistance +
                                     trolleyBodyBox.center.z -
                                     trolleyBodyBox.size.z * 0.5f;
            float rearHandleDistance = platformTrolleyConfig.FollowDistance +
                                       trolleyHandleTrigger.center.z -
                                       trolleyHandleTrigger.size.z * 0.5f;
            Require(playerController != null &&
                    rearBodyDistance > playerController.radius + 0.05f &&
                    rearHandleDistance > playerController.radius + 0.05f,
                "Platform trolley follow distance must leave a physical gap between the player " +
                "capsule, trolley body and handle trigger.");
            Rigidbody trolleyBody = trolleyRigidbodies[0];
            Require(trolleyBody.isKinematic && !trolleyBody.useGravity &&
                    trolleyBody.interpolation == RigidbodyInterpolation.None,
                $"{PlatformTrolleyPrefabPath} must use a deterministic kinematic, gravity-free " +
                "Rigidbody.");
            SerializedProperty trolleyHighlight =
                new SerializedObject(trolleyViews[0]).FindProperty("_highlight");
            Require(trolleyHighlight?.objectReferenceValue == trolleyHighlights[0],
                $"The InteractionView in {PlatformTrolleyPrefabPath} must reference its deck " +
                "highlight.");
            Require(platformTrolleyConfig.ViewPrefab == trolleyViews[0],
                $"{PlatformTrolleyConfigPath} must reference the InteractionView root from " +
                $"{PlatformTrolleyPrefabPath}.");
            Require(productPrefabs.All(productPrefab =>
                    !ContainsPrefabInstance(trolleyPrefab, productPrefab)),
                $"{PlatformTrolleyPrefabPath} must be empty before runtime cargo placement.");
        }

        private static void ValidateProductCatalogEntry(ProductConfig productConfig,
            DeliveryConfig deliveryConfig, ProductTypeId productType,
            int unitPrice, float mass, float carryMovementSpeed,
            float productDropCollisionRadius, int deliveryCount, int purchaseUnitPrice)
        {
            string productPath = AssetDatabase.GetAssetPath(productConfig);
            string deliveryPath = AssetDatabase.GetAssetPath(deliveryConfig);
            Require(productConfig.ProductType == productType &&
                    deliveryConfig.ProductType == productType,
                $"Catalog entry {productType} must use the same key across product and delivery.");
            Require(productConfig.UnitPrice == unitPrice &&
                    Mathf.Approximately(productConfig.Mass, mass) &&
                    Mathf.Approximately(productConfig.CarryMovementSpeed, carryMovementSpeed) &&
                    Mathf.Approximately(
                        productConfig.ProductDropCollisionRadius,
                        productDropCollisionRadius) &&
                    productConfig.ProductDropCollisionRadius <=
                    productConfig.DropForwardDistance,
                $"{productPath} has incorrect sale, mass, carry-speed or collision-safe drop " +
                "values.");
            Require(productConfig.WorldInterpolation == RigidbodyInterpolation.Interpolate &&
                    productConfig.WorldCollisionDetection ==
                    CollisionDetectionMode.ContinuousSpeculative,
                $"{productPath} must use the supported loose-product physics modes.");
            Require(deliveryConfig.ProductCount == deliveryCount &&
                    deliveryConfig.PurchaseUnitPrice == purchaseUnitPrice &&
                    deliveryConfig.TotalCost == deliveryCount * purchaseUnitPrice,
                $"{deliveryPath} has incorrect delivery quantity, unit cost or total.");
        }

        private static void ValidateSingleProductProject(
            CustomerProjectConfig project,
            CustomerProjectTypeId expectedProjectType,
            ProductTypeId expectedProductType,
            CustomerVehicleConfig vehicle,
            IReadOnlyCollection<ProductConfig> products,
            IReadOnlyCollection<DeliveryConfig> deliveries)
        {
            string path = AssetDatabase.GetAssetPath(project);
            Require(project.ProjectType == expectedProjectType,
                $"{path} has an incorrect semantic project identity.");
            Require(project.DefaultOfferIndex == 1 && project.Offers.Count == 3,
                $"{path} must expose three offers and select its standard offer by default.");

            for (int index = 0; index < project.Offers.Count; index++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[index];
                int expectedCount = index + 1;
                Require(offer.Lines.Count == 1 &&
                        offer.Lines[0].ProductType == expectedProductType &&
                        offer.Lines[0].RequiredCount == expectedCount,
                    $"{path} offer {index} must preserve the configured single-SKU 1/2/3 " +
                    "prototype progression.");
            }

            ValidateProjectOffers(project, vehicle, products, deliveries);
        }

        private static void ValidateWorkbenchProject(
            CustomerProjectConfig project,
            CustomerVehicleConfig vehicle,
            IReadOnlyCollection<ProductConfig> products,
            IReadOnlyCollection<DeliveryConfig> deliveries)
        {
            string path = AssetDatabase.GetAssetPath(project);
            Require(project.ProjectType == CustomerProjectTypeId.WorkbenchFoundation &&
                    project.DefaultOfferIndex == 1 && project.Offers.Count == 3,
                $"{path} must expose the configured mixed workbench project and default offer.");

            (int Cement, int Boards)[] expectedSignatures =
            {
                (1, 1),
                (2, 1),
                (1, 2)
            };
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[offerIndex];
                Require(offer.Lines.Count == CustomerProjectConfig.MaxLinesPerOffer &&
                        offer.Lines.Select(line => line.ProductType).Distinct().Count() == 2 &&
                        offer.Lines.Any(line =>
                            line.ProductType == ProductTypeId.CementBag) &&
                        offer.Lines.Any(line =>
                            line.ProductType == ProductTypeId.BoardBundle),
                    $"{path} offer {offerIndex} must contain one cement and one board line.");
                int cementCount = offer.Lines
                    .Single(line => line.ProductType == ProductTypeId.CementBag)
                    .RequiredCount;
                int boardCount = offer.Lines
                    .Single(line => line.ProductType == ProductTypeId.BoardBundle)
                    .RequiredCount;
                Require((cementCount, boardCount) == expectedSignatures[offerIndex],
                    $"{path} offer {offerIndex} must use its exact mixed-SKU signature.");
            }

            ValidateProjectOffers(project, vehicle, products, deliveries);
            ProjectOfferMetrics[] metrics = project.Offers
                .Select(offer => CalculateOfferMetrics(offer, products, deliveries))
                .ToArray();
            ProjectOfferMetrics[] expectedMetrics =
            {
                new(totalUnits: 2, productCost: 460, revenue: 830, expectedProfit: 370),
                new(totalUnits: 3, productCost: 660, revenue: 1180, expectedProfit: 520),
                new(totalUnits: 3, productCost: 720, revenue: 1310, expectedProfit: 590)
            };
            Require(metrics.SequenceEqual(expectedMetrics),
                $"{path} mixed offers have incorrect calculated cost, revenue or profit.");
            for (int left = 0; left < metrics.Length; left++)
            for (int right = 0; right < metrics.Length; right++)
            {
                if (left == right)
                    continue;
                Require(!Dominates(metrics[left], metrics[right]),
                    $"{path} offer {left} dominates offer {right}; every consultation choice " +
                    "must preserve a visible cost/capacity/profit trade-off.");
            }
        }

        private static void ValidateMixedProject(
            CustomerProjectConfig project,
            CustomerProjectTypeId expectedProjectType,
            ProductTypeId primaryProductType,
            ProductTypeId secondaryProductType,
            CustomerVehicleConfig vehicle,
            IReadOnlyCollection<ProductConfig> products,
            IReadOnlyCollection<DeliveryConfig> deliveries,
            IReadOnlyList<ProjectOfferMetrics> expectedMetrics)
        {
            string path = AssetDatabase.GetAssetPath(project);
            Require(project.ProjectType == expectedProjectType &&
                    project.DefaultOfferIndex == 1 && project.Offers.Count == 3,
                $"{path} must expose its frozen three-offer mixed project.");

            (int Primary, int Secondary)[] expectedSignatures =
            {
                (1, 1),
                (2, 1),
                (1, 2)
            };
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[offerIndex];
                Require(offer.Lines.Count == CustomerProjectConfig.MaxLinesPerOffer &&
                        offer.Lines.Select(line => line.ProductType).Distinct().Count() == 2 &&
                        offer.Lines.Any(line => line.ProductType == primaryProductType) &&
                        offer.Lines.Any(line => line.ProductType == secondaryProductType),
                    $"{path} offer {offerIndex} must contain one {primaryProductType} and " +
                    $"one {secondaryProductType} line.");
                int primaryCount = offer.Lines
                    .Single(line => line.ProductType == primaryProductType)
                    .RequiredCount;
                int secondaryCount = offer.Lines
                    .Single(line => line.ProductType == secondaryProductType)
                    .RequiredCount;
                Require((primaryCount, secondaryCount) == expectedSignatures[offerIndex],
                    $"{path} offer {offerIndex} has an incorrect mixed-product signature.");
            }

            ValidateProjectOffers(project, vehicle, products, deliveries);
            ProjectOfferMetrics[] actualMetrics = project.Offers
                .Select(offer => CalculateOfferMetrics(offer, products, deliveries))
                .ToArray();
            Require(actualMetrics.SequenceEqual(expectedMetrics),
                $"{path} mixed offers have incorrect cost, revenue or profit.");
            for (int left = 0; left < actualMetrics.Length; left++)
            for (int right = 0; right < actualMetrics.Length; right++)
            {
                if (left == right)
                    continue;
                Require(!Dominates(actualMetrics[left], actualMetrics[right]),
                    $"{path} offer {left} dominates offer {right}; every choice must " +
                    "preserve a cost/profit trade-off.");
            }
        }

        private static void ValidateProjectOffers(
            CustomerProjectConfig project,
            CustomerVehicleConfig vehicle,
            IReadOnlyCollection<ProductConfig> products,
            IReadOnlyCollection<DeliveryConfig> deliveries)
        {
            string path = AssetDatabase.GetAssetPath(project);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                CustomerProjectOfferDefinition offer = project.Offers[offerIndex];
                Require(offer.Lines.Count > 0,
                    $"{path} offer {offerIndex} must contain at least one line.");
                Require(offer.Lines.Count <= CustomerProjectConfig.MaxLinesPerOffer,
                    $"{path} offer {offerIndex} exceeds " +
                    $"{nameof(CustomerProjectConfig.MaxLinesPerOffer)}.");
                Require(offer.Lines.Select(line => line.ProductType).Distinct().Count() ==
                        offer.Lines.Count,
                    $"{path} offer {offerIndex} contains a duplicate product type.");
                Require(offer.Lines.All(line => line.RequiredCount > 0),
                    $"{path} offer {offerIndex} must use positive line counts.");

                ProjectOfferMetrics metrics = CalculateOfferMetrics(offer, products, deliveries);
                Require(metrics.TotalUnits <= vehicle.CargoCapacity,
                    $"{path} offer {offerIndex} requires {metrics.TotalUnits}/" +
                    $"{vehicle.CargoCapacity} customer cargo slots.");
                foreach (CustomerProjectLineDefinition line in offer.Lines)
                {
                    DeliveryConfig delivery = deliveries.Single(
                        config => config.ProductType == line.ProductType);
                    Require(delivery.ProductCount >= line.RequiredCount,
                        $"Delivery {line.ProductType} cannot cover line {line.RequiredCount} " +
                        $"in {path} offer {offerIndex}.");
                }

                string signature = string.Join(",", offer.Lines
                    .OrderBy(line => (int)line.ProductType)
                    .Select(line => $"{(int)line.ProductType}:{line.RequiredCount}"));
                Require(signatures.Add(signature),
                    $"{path} contains duplicate offer composition '{signature}'.");
            }
        }

        private static ProjectOfferMetrics CalculateOfferMetrics(
            CustomerProjectOfferDefinition offer,
            IReadOnlyCollection<ProductConfig> products,
            IReadOnlyCollection<DeliveryConfig> deliveries)
        {
            int totalUnits = 0;
            int productCost = 0;
            int revenue = 0;
            foreach (CustomerProjectLineDefinition line in offer.Lines)
            {
                ProductConfig product = products.Single(
                    config => config.ProductType == line.ProductType);
                DeliveryConfig delivery = deliveries.Single(
                    config => config.ProductType == line.ProductType);
                totalUnits = checked(totalUnits + line.RequiredCount);
                productCost = checked(productCost +
                    checked(delivery.PurchaseUnitPrice * line.RequiredCount));
                revenue = checked(revenue + checked(product.UnitPrice * line.RequiredCount));
            }

            return new ProjectOfferMetrics(
                totalUnits,
                productCost,
                revenue,
                checked(revenue - productCost));
        }

        private static bool Dominates(ProjectOfferMetrics left, ProjectOfferMetrics right) =>
            left.TotalUnits <= right.TotalUnits &&
            left.ProductCost <= right.ProductCost &&
            left.ExpectedProfit >= right.ExpectedProfit &&
            (left.TotalUnits < right.TotalUnits ||
             left.ProductCost < right.ProductCost ||
             left.ExpectedProfit > right.ExpectedProfit);

        private readonly struct ProjectOfferMetrics : IEquatable<ProjectOfferMetrics>
        {
            public ProjectOfferMetrics(int totalUnits, int productCost, int revenue,
                int expectedProfit)
            {
                TotalUnits = totalUnits;
                ProductCost = productCost;
                Revenue = revenue;
                ExpectedProfit = expectedProfit;
            }

            public int TotalUnits { get; }
            public int ProductCost { get; }
            public int Revenue { get; }
            public int ExpectedProfit { get; }

            public bool Equals(ProjectOfferMetrics other) =>
                TotalUnits == other.TotalUnits && ProductCost == other.ProductCost &&
                Revenue == other.Revenue && ExpectedProfit == other.ExpectedProfit;

            public override bool Equals(object obj) =>
                obj is ProjectOfferMetrics other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(TotalUnits, ProductCost, Revenue, ExpectedProfit);
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

        private static void ValidateExpandedProductVisuals(
            IReadOnlyList<GameObject> productPrefabs,
            IReadOnlyList<string> productPrefabPaths)
        {
            Require(productPrefabs.Count == ExpectedProductTypes.Length &&
                    productPrefabPaths.Count == ExpectedProductTypes.Length,
                "Expanded product visual validation requires the complete ordered catalog.");

            Renderer[] brickRenderers =
                productPrefabs[2].GetComponentsInChildren<Renderer>(true);
            Require(brickRenderers.Length == 8 &&
                    brickRenderers.Count(renderer =>
                        renderer.name.StartsWith("Brick ", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "Brick") == 6 &&
                    brickRenderers.Count(renderer =>
                        renderer.name.EndsWith("Strap", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "DarkMetal") == 2,
                $"{productPrefabPaths[2]} must visibly contain six terracotta bricks and two " +
                "dark retaining straps.");

            Renderer[] drywallRenderers =
                productPrefabs[3].GetComponentsInChildren<Renderer>(true);
            Require(drywallRenderers.Length == 5 &&
                    drywallRenderers.Count(renderer =>
                        renderer.name.StartsWith("Drywall Layer ", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "Drywall") == 3 &&
                    drywallRenderers.Count(renderer =>
                        renderer.name.EndsWith("Edge", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "DrywallEdge") == 2,
                $"{productPrefabPaths[3]} must visibly contain three warm-white sheets and two " +
                "blue edges.");

            Renderer[] paintRenderers =
                productPrefabs[4].GetComponentsInChildren<Renderer>(true);
            Require(paintRenderers.Length == 5 &&
                    paintRenderers.Count(renderer =>
                        renderer.name == "Bucket Body" &&
                        renderer.sharedMaterial.name == "BrandBlue") == 1 &&
                    paintRenderers.Count(renderer =>
                        renderer.name == "Bucket Lid" &&
                        renderer.sharedMaterial.name == "White") == 1 &&
                    paintRenderers.Count(renderer =>
                        renderer.name.StartsWith("Handle ", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "DarkMetal") == 3,
                $"{productPrefabPaths[4]} must visibly contain one blue bucket, a white lid " +
                "and a three-piece dark handle.");

            Renderer[] insulationRenderers =
                productPrefabs[5].GetComponentsInChildren<Renderer>(true);
            Require(insulationRenderers.Length == 3 &&
                    insulationRenderers.Count(renderer =>
                        renderer.name == "Insulation Roll Visual" &&
                        renderer.sharedMaterial.name == "SafetyYellow") == 1 &&
                    insulationRenderers.Count(renderer =>
                        renderer.name.EndsWith("Strap", StringComparison.Ordinal) &&
                        renderer.sharedMaterial.name == "DarkMetal") == 2,
                $"{productPrefabPaths[5]} must visibly contain one yellow roll and two dark " +
                "retaining straps.");

            Require(productPrefabs.Skip(2).All(prefab =>
                    prefab.GetComponentsInChildren<CapsuleCollider>(true).Length == 0),
                "Cylinder-based product visuals must not retain primitive CapsuleColliders; " +
                "only the authored root BoxCollider may be solid.");
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

        private static float ReadSolidProductBoundingRadius(GameObject productPrefab,
            string productPrefabPath)
        {
            Collider[] solidColliders = productPrefab.GetComponentsInChildren<Collider>(true)
                .Where(collider => !collider.isTrigger)
                .ToArray();
            Require(solidColliders.Length == 1 && solidColliders[0] is BoxCollider,
                $"{productPrefabPath} must provide exactly one solid BoxCollider.");

            BoxCollider boxCollider = (BoxCollider)solidColliders[0];
            Vector3 halfSize = boxCollider.size * 0.5f;
            float maximumRadius = 0f;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 localCorner = boxCollider.center + Vector3.Scale(
                    halfSize,
                    new Vector3(x, y, z));
                Vector3 worldCorner = boxCollider.transform.TransformPoint(localCorner);
                maximumRadius = Mathf.Max(
                    maximumRadius,
                    Vector3.Distance(productPrefab.transform.position, worldCorner));
            }

            return maximumRadius;
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
                // The builder leaves the freshly-authored scene loaded. With automatic
                // transform syncing disabled, Collider.bounds can otherwise still describe
                // the primitive's pre-authoring unit pose instead of its saved transform.
                Physics.SyncTransforms();
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
                CustomerFlowLayoutMarker[] customerFlowLayouts =
                    FindComponentsInScene<CustomerFlowLayoutMarker>(scene);
                CustomerParkingSpotLayoutMarker[] customerParkingSpots =
                    FindComponentsInScene<CustomerParkingSpotLayoutMarker>(scene);
                SceneViewMarker[] sceneViews = FindComponentsInScene<SceneViewMarker>(scene);
                EntityBehaviour[] entityViews = FindComponentsInScene<EntityBehaviour>(scene);
                SlotsRegistrar[] slotRegistrars = FindComponentsInScene<SlotsRegistrar>(scene);
                PrototypeHudView[] hudViews = FindComponentsInScene<PrototypeHudView>(scene);
                PrototypeAudioView[] audioViews = FindComponentsInScene<PrototypeAudioView>(scene);
                PrototypeDayNightView[] dayNightViews =
                    FindComponentsInScene<PrototypeDayNightView>(scene);
                Light[] sceneLights = FindComponentsInScene<Light>(scene);
                LocalizedTextMeshView[] localizedWorldLabels =
                    FindComponentsInScene<LocalizedTextMeshView>(scene);
                NavMeshSurface[] navigationSurfaces =
                    FindComponentsInScene<NavMeshSurface>(scene);

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
                CustomerFlowLayoutMarker configuredCustomerFlowLayout =
                    serializedInitializer.FindProperty("_customerFlowLayout")
                        ?.objectReferenceValue as CustomerFlowLayoutMarker;
                SceneViewMarker[] configuredSceneViews = ReadObjectArray<SceneViewMarker>(
                    serializedInitializer, "_sceneViews", nameof(PrototypeSceneInitializer));
                Require(new HashSet<SpawnPointMarker>(configuredSpawnPoints).SetEquals(spawnPoints) &&
                        configuredSpawnPoints.Length == spawnPoints.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene spawn point set.");
                Require(new HashSet<SceneRouteMarker>(configuredRoutes).SetEquals(routes) &&
                        configuredRoutes.Length == routes.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene route set.");
                Require(customerFlowLayouts.Length == 1 &&
                        configuredCustomerFlowLayout == customerFlowLayouts[0],
                    $"{nameof(PrototypeSceneInitializer)} must reference the scene's one " +
                    "typed customer-flow layout marker.");
                Require(new HashSet<SceneViewMarker>(configuredSceneViews).SetEquals(sceneViews) &&
                        configuredSceneViews.Length == sceneViews.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the static scene view set.");
                Require(hudViews.Length == 1 && audioViews.Length == 1 &&
                        dayNightViews.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one HUD, audio and day/night view.");
                Require(serializedInitializer.FindProperty("_hudView")?.objectReferenceValue == hudViews[0] &&
                        serializedInitializer.FindProperty("_audioView")?.objectReferenceValue == audioViews[0] &&
                        serializedInitializer.FindProperty("_dayNightView")?.objectReferenceValue ==
                        dayNightViews[0],
                    $"{nameof(PrototypeSceneInitializer)} must reference the scene HUD, audio and " +
                    "day/night views.");
                SerializedObject serializedDayNight = new(dayNightViews[0]);
                Light configuredSun = serializedDayNight.FindProperty("_sun")?.objectReferenceValue as Light;
                Light[] configuredIndoorLights = ReadObjectArray<Light>(
                    serializedDayNight,
                    "_indoorLights",
                    nameof(PrototypeDayNightView));
                Require(sceneLights.Length == 3 &&
                        configuredSun != null && configuredSun.gameObject.scene == scene &&
                        configuredSun.name == "Sun" &&
                        configuredSun.type == LightType.Directional &&
                        configuredIndoorLights.Length == 2 &&
                        configuredIndoorLights.All(light =>
                            light != null && light.gameObject.scene == scene &&
                            light.type == LightType.Point && light.intensity > 0f) &&
                        configuredIndoorLights.Select(light => light.name).SequenceEqual(
                            new[] { "Shop Light", "Warehouse Light" }) &&
                        new HashSet<Light>(configuredIndoorLights.Append(configuredSun))
                            .SetEquals(sceneLights),
                    $"{nameof(PrototypeDayNightView)} must reference the scene's one directional " +
                    "Sun and exactly the Shop/Warehouse point lights with authored night intensity.");
                Require(characterRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CharacterControllerRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                Require(cameraRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CameraRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                string[] productPrefabPaths =
                {
                    CementProductPrefabPath,
                    BoardProductPrefabPath,
                    BrickProductPrefabPath,
                    DrywallProductPrefabPath,
                    PaintProductPrefabPath,
                    InsulationProductPrefabPath
                };
                GameObject[] productPrefabs = productPrefabPaths
                    .Select(RequireAsset<GameObject>)
                    .ToArray();
                GameObject workerTrolleyPrefab =
                    RequireAsset<GameObject>(WarehouseWorkerTrolleyPrefabPath);
                Require(productPrefabs.All(productPrefab =>
                            !ContainsPrefabInstance(scene, productPrefab)) &&
                        !ContainsPrefabInstance(scene, workerTrolleyPrefab),
                    $"{PrototypeScenePath} must not contain product or worker-trolley prefab " +
                    "instances; both are spawned at runtime.");

                var expectedSpawnIds = new HashSet<SpawnPointId>
                {
                    SpawnPointId.Player,
                    SpawnPointId.DeliveryVehicle,
                    SpawnPointId.PlatformTrolley,
                    SpawnPointId.WarehouseWorker,
                    SpawnPointId.WarehouseWorkerDeliveryAccess,
                    SpawnPointId.WarehouseWorkerStorageAccess,
                    SpawnPointId.WarehouseWorkerCustomerLoadingAccess,
                    SpawnPointId.WarehouseWorkerTrolley,
                    SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess
                };
                var actualSpawnIds = new HashSet<SpawnPointId>(spawnPoints.Select(marker => marker.Id));
                Require(spawnPoints.Length == expectedSpawnIds.Count && actualSpawnIds.SetEquals(expectedSpawnIds),
                    $"{PrototypeScenePath} must contain one marker for every player, vehicle, " +
                    "trolley and warehouse-worker access point.");

                Require(navigationSurfaces.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one NavMeshSurface.");
                NavMeshSurface navigation = navigationSurfaces[0];
                Require(navigation.name == "Navigation" &&
                        navigation.transform.parent != null &&
                        navigation.transform.parent.name == "Environment" &&
                        navigation.agentTypeID == 0 &&
                        navigation.collectObjects == CollectObjects.All &&
                        navigation.useGeometry == NavMeshCollectGeometry.PhysicsColliders &&
                        navigation.ignoreNavMeshAgent && navigation.ignoreNavMeshObstacle &&
                        navigation.overrideVoxelSize &&
                        Mathf.Approximately(navigation.voxelSize, 0.08f) &&
                        navigation.navMeshData != null &&
                        AssetDatabase.GetAssetPath(navigation.navMeshData) ==
                        WarehouseWorkerNavMeshPath,
                    "Environment/Navigation must own the exact baked warehouse-worker " +
                    "NavMeshSurface asset.");
                SpawnPointMarker[] workerAccessPoints =
                {
                    spawnPoints.Single(marker => marker.Id == SpawnPointId.WarehouseWorker),
                    spawnPoints.Single(marker =>
                        marker.Id == SpawnPointId.WarehouseWorkerDeliveryAccess),
                    spawnPoints.Single(marker =>
                        marker.Id == SpawnPointId.WarehouseWorkerStorageAccess),
                    spawnPoints.Single(marker =>
                        marker.Id == SpawnPointId.WarehouseWorkerCustomerLoadingAccess)
                };
                SpawnPointMarker workerTrolleyHome = spawnPoints.Single(marker =>
                    marker.Id == SpawnPointId.WarehouseWorkerTrolley);
                SpawnPointMarker workerTrolleyCustomerLoadingAccess =
                    spawnPoints.Single(marker => marker.Id ==
                        SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess);
                Vector3[] expectedWorkerPositions =
                {
                    new(2.3f, 0.02f, 1.95f),
                    new(9.15f, 0.02f, -9.85f),
                    new(5f, 0.02f, 2.45f),
                    new(6f, 0.02f, 1.62f)
                };
                Quaternion[] expectedWorkerRotations =
                {
                    Quaternion.Euler(0f, 90f, 0f),
                    Quaternion.Euler(0f, 90f, 0f),
                    Quaternion.identity,
                    Quaternion.Euler(0f, 180f, 0f)
                };
                Require(workerAccessPoints
                        .Select((marker, index) => Vector3.Distance(
                            marker.transform.position,
                            expectedWorkerPositions[index]))
                        .All(distance => distance < 0.001f) &&
                        workerAccessPoints
                            .Select((marker, index) => Quaternion.Angle(
                                marker.transform.rotation,
                                expectedWorkerRotations[index]))
                            .All(angle => angle < 0.01f) &&
                        workerAccessPoints.All(marker =>
                            marker.transform.parent != null &&
                            marker.transform.parent.name ==
                            "Warehouse Worker Access Points"),
                    "Warehouse-worker idle, inbound, storage and customer-loading access " +
                    "markers must preserve their exact safe yard-level poses.");

                Pose expectedWorkerTrolleyHome = new(
                    new Vector3(4f, 0.02f, 1.95f),
                    Quaternion.Euler(0f, 90f, 0f));
                Pose expectedWorkerTrolleyCustomerLoading = new(
                    new Vector3(6f, 0.02f, 1.95f),
                    Quaternion.Euler(0f, 90f, 0f));
                Require(PoseMatches(workerTrolleyHome.Pose, expectedWorkerTrolleyHome) &&
                        PoseMatches(
                            workerTrolleyCustomerLoadingAccess.Pose,
                            expectedWorkerTrolleyCustomerLoading) &&
                        workerTrolleyHome.name == "Warehouse Worker Trolley Home" &&
                        workerTrolleyCustomerLoadingAccess.name ==
                        "Warehouse Worker Trolley Customer Loading Access" &&
                        workerTrolleyHome.transform.parent != null &&
                        workerTrolleyCustomerLoadingAccess.transform.parent ==
                        workerTrolleyHome.transform.parent &&
                        workerTrolleyHome.transform.parent.name ==
                        "Warehouse Worker Access Points",
                    "Worker-trolley home and customer-loading root markers must preserve " +
                    "their exact runtime-only cart poses under the worker access root.");
                WarehouseWorkerConfig sceneWarehouseWorkerConfig =
                    RequireAsset<WarehouseWorkerConfig>(WarehouseWorkerConfigPath);
                Pose homePusherPose = ResolveWorkerTrolleyPusherPose(
                    workerTrolleyHome.Pose,
                    sceneWarehouseWorkerConfig.TrolleyFollowDistance);
                Pose customerLoadingPusherPose = ResolveWorkerTrolleyPusherPose(
                    workerTrolleyCustomerLoadingAccess.Pose,
                    sceneWarehouseWorkerConfig.TrolleyFollowDistance);
                Require(PoseMatches(workerAccessPoints[0].Pose, homePusherPose) &&
                        Vector3.Distance(
                            homePusherPose.position,
                            new Vector3(2.3f, 0.02f, 1.95f)) < 0.001f &&
                        Vector3.Distance(
                            customerLoadingPusherPose.position,
                            new Vector3(4.3f, 0.02f, 1.95f)) < 0.001f,
                    "Worker trolley pusher poses must derive from the two cart-root poses " +
                    "with the authored 1.7m follow distance.");

                (string Name, Vector3 Position)[] navigationAccessPoints =
                {
                    (workerAccessPoints[0].Id.ToString(),
                        workerAccessPoints[0].transform.position),
                    (workerAccessPoints[1].Id.ToString(),
                        workerAccessPoints[1].transform.position),
                    (workerAccessPoints[2].Id.ToString(),
                        workerAccessPoints[2].transform.position),
                    (workerAccessPoints[3].Id.ToString(),
                        workerAccessPoints[3].transform.position),
                    ("WarehouseWorkerTrolleyCustomerLoadingPusher",
                        customerLoadingPusherPose.position)
                };
                var sampledWorkerPoints = new Vector3[navigationAccessPoints.Length];
                for (int index = 0; index < navigationAccessPoints.Length; index++)
                {
                    Require(NavMesh.SamplePosition(
                            navigationAccessPoints[index].Position,
                            out NavMeshHit hit,
                            2f,
                            NavMesh.AllAreas),
                        $"Worker access point {navigationAccessPoints[index].Name} must sample onto " +
                        "the authored NavMesh.");
                    sampledWorkerPoints[index] = hit.position;
                }
                for (int origin = 0; origin < sampledWorkerPoints.Length; origin++)
                {
                    for (int destination = 0;
                         destination < sampledWorkerPoints.Length;
                         destination++)
                    {
                        if (origin == destination)
                            continue;

                        var workerPath = new NavMeshPath();
                        bool foundPath = NavMesh.CalculatePath(
                            sampledWorkerPoints[origin],
                            sampledWorkerPoints[destination],
                            NavMesh.AllAreas,
                            workerPath);
                        Require(foundPath &&
                                workerPath.status == NavMeshPathStatus.PathComplete,
                            $"Worker path from {navigationAccessPoints[origin].Name} to " +
                            $"{navigationAccessPoints[destination].Name} must be complete.");
                    }
                }

                Require(routes.Length == 0,
                    $"{PrototypeScenePath} must express customer traffic through its typed " +
                    "customer-flow layout instead of legacy singleton routes.");
                Require(customerParkingSpots.Length == 3 &&
                        customerParkingSpots.All(spot =>
                            spot.transform.parent == customerFlowLayouts[0].transform),
                    $"{PrototypeScenePath} must contain exactly three customer parking-spot " +
                    "markers beneath the customer-flow root.");

                CustomerFlowSceneLayout customerFlowLayout = customerFlowLayouts[0].Layout;
                CustomerParkingSpotSceneLayout[] parkingLayouts =
                    customerFlowLayout.ParkingSpots;
                Pose[] queuePoses = customerFlowLayout.QueuePoses;
                Pose[] queueAbandonExitRoute =
                    customerFlowLayout.QueueAbandonExitRoute;
                Pose[] loadingDepartureRoute =
                    customerFlowLayout.LoadingDepartureRoute;
                Require(parkingLayouts.Length == 3 &&
                        parkingLayouts.Select(layout => layout.Index)
                            .SequenceEqual(new[] { 0, 1, 2 }),
                    "The customer-flow layout must expose three contiguous parking spots.");
                Pose preservedQueueHead = queuePoses[0];
                queuePoses[0] = default;
                Require(PoseMatches(
                        customerFlowLayout.QueuePoses[0],
                        preservedQueueHead),
                    "Customer-flow queue snapshots must not expose their internal pose array.");
                queuePoses = customerFlowLayout.QueuePoses;
                Pose preservedQueueAbandonExit = queueAbandonExitRoute[0];
                queueAbandonExitRoute[0] = default;
                Require(PoseMatches(
                        customerFlowLayout.QueueAbandonExitRoute[0],
                        preservedQueueAbandonExit),
                    "Customer-flow queue-abandon snapshots must not expose their internal " +
                    "pose array.");
                queueAbandonExitRoute = customerFlowLayout.QueueAbandonExitRoute;
                Pose preservedArrivalStart =
                    parkingLayouts[0].VehicleArrivalRoute[0];
                Pose[] mutableArrival = parkingLayouts[0].VehicleArrivalRoute;
                mutableArrival[0] = default;
                parkingLayouts[0] = null;
                Require(customerFlowLayout.ParkingSpots[0] != null &&
                        PoseMatches(
                            customerFlowLayout.ParkingSpots[0].VehicleArrivalRoute[0],
                            preservedArrivalStart),
                    "Customer-flow parking snapshots must deep-clone their layout and route " +
                    "arrays.");
                parkingLayouts = customerFlowLayout.ParkingSpots;
                Pose preservedParkingDepartureEnd =
                    parkingLayouts[0].VehicleParkingDepartureRoute[^1];
                Pose[] mutableParkingDeparture =
                    parkingLayouts[0].VehicleParkingDepartureRoute;
                mutableParkingDeparture[^1] = default;
                Require(PoseMatches(
                        customerFlowLayout.ParkingSpots[0]
                            .VehicleParkingDepartureRoute[^1],
                        preservedParkingDepartureEnd),
                    "Customer-flow parking snapshots must clone the authored abandonment " +
                    "departure route.");
                Require(queuePoses.Length == 3 &&
                        Vector3.Distance(queuePoses[0].position,
                            new Vector3(-7.25f, 0.02f, 0.55f)) < 0.001f &&
                        Vector3.Distance(queuePoses[1].position,
                            new Vector3(-7.25f, 0.02f, -0.75f)) < 0.001f &&
                        Vector3.Distance(queuePoses[2].position,
                            new Vector3(-7.25f, 0.02f, -2.05f)) < 0.001f,
                    "Customer queue poses must preserve the authored service-to-tail order.");
                for (int index = 1; index < queuePoses.Length; index++)
                {
                    Require(Vector3.Distance(
                            queuePoses[index - 1].position,
                            queuePoses[index].position) >= 1.1f,
                        "Customer queue poses must retain safe pedestrian spacing.");
                }
                Pose[] expectedQueueAbandonExitRoute =
                {
                    new(new Vector3(-8f, 0.02f, 0.55f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(-8f, 0.02f, -0.75f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(-8f, 0.02f, -2.05f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(-8f, 0.02f, -3f),
                        Quaternion.Euler(0f, 180f, 0f))
                };
                Require(queueAbandonExitRoute.Length == queuePoses.Length + 1 &&
                        queueAbandonExitRoute.Select((pose, index) =>
                                PoseMatches(pose, expectedQueueAbandonExitRoute[index]))
                            .All(matches => matches),
                    "Customer queue abandonment must use the exact authored lateral exits " +
                    "and shared return-route join instead of runtime world coordinates.");

                Vector3[] expectedParkingPositions =
                {
                    new(-10.8f, 0.02f, -21.5f),
                    new(-7.4f, 0.02f, -21.5f),
                    new(-4f, 0.02f, -21.5f)
                };
                Pose[] expectedLoadingDepartureRoute =
                {
                    new(new Vector3(6f, 0.02f, -2.5f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(6f, 0.02f, -10f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(3.5f, 0.02f, -11.8f),
                        Quaternion.Euler(0f, -126f, 0f)),
                    new(new Vector3(0f, 0.02f, -13f),
                        Quaternion.Euler(0f, -109f, 0f)),
                    new(new Vector3(0f, 0.02f, -20f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(0f, 0.02f, -30f),
                        Quaternion.Euler(0f, 180f, 0f)),
                    new(new Vector3(0f, 0.02f, -35f),
                        Quaternion.Euler(0f, 180f, 0f))
                };
                Require(loadingDepartureRoute.Length ==
                            expectedLoadingDepartureRoute.Length &&
                        loadingDepartureRoute.Select((pose, index) =>
                                PoseMatches(pose, expectedLoadingDepartureRoute[index]))
                            .All(matches => matches),
                    "Customer loading departure must start from the rear-facing bay pose, " +
                    "leave forward and continue through the vehicle gate along the exterior " +
                    "access road.");
                GameObject alignmentWorkerPrefab =
                    RequireAsset<GameObject>(WarehouseWorkerPrefabPath);
                GameObject alignmentCustomerVehiclePrefab =
                    RequireAsset<GameObject>(CustomerVehiclePrefabPath);
                Transform carryAnchor =
                    alignmentWorkerPrefab.transform.Find("Carry Anchor");
                Transform loadingTarget =
                    alignmentCustomerVehiclePrefab.transform.Find("Loading Target");
                Require(carryAnchor != null && loadingTarget != null,
                    "Warehouse worker and customer vehicle prefabs must expose their authored " +
                    "Carry Anchor and Loading Target transforms.");
                SpawnPointMarker customerLoadingAccess = workerAccessPoints.Single(marker =>
                    marker.Id == SpawnPointId.WarehouseWorkerCustomerLoadingAccess);
                Vector3 carriedProductWorldPosition =
                    customerLoadingAccess.transform.position +
                    customerLoadingAccess.transform.rotation * carryAnchor.localPosition;
                Vector3 customerLoadingTargetWorldPosition =
                    loadingDepartureRoute[0].position +
                    loadingDepartureRoute[0].rotation * loadingTarget.localPosition;
                Require(Vector3.Distance(
                            carriedProductWorldPosition,
                            customerLoadingTargetWorldPosition) <= 0.03f,
                    $"The worker customer-loading access must align Carry Anchor " +
                    $"{carriedProductWorldPosition} with the rear-facing vehicle Loading " +
                    $"Target {customerLoadingTargetWorldPosition} within 0.03m.");
                const float storagePadSouthEdgeZ = 3.25f;
                const float rearFacingVehicleMaxZOffset = 3.2f;
                const float workerTrolleyHullHalfLocalX = 1f;
                const float workerTrolleyHullHalfLocalZ = 1.05f;
                const float workerTrolleyHullCenterLocalZ = 0.15f;
                float workerTrolleyRunMinZ =
                    expectedWorkerTrolleyHome.position.z -
                    workerTrolleyHullHalfLocalX;
                float workerTrolleyRunMaxZ =
                    expectedWorkerTrolleyHome.position.z +
                    workerTrolleyHullHalfLocalX;
                float workerTrolleyHomeMinX =
                    expectedWorkerTrolleyHome.position.x +
                    workerTrolleyHullCenterLocalZ -
                    workerTrolleyHullHalfLocalZ;
                float workerTrolleyHomeMaxX =
                    expectedWorkerTrolleyHome.position.x +
                    workerTrolleyHullCenterLocalZ +
                    workerTrolleyHullHalfLocalZ;
                float workerTrolleyLoadingMinX =
                    expectedWorkerTrolleyCustomerLoading.position.x +
                    workerTrolleyHullCenterLocalZ -
                    workerTrolleyHullHalfLocalZ;
                float workerTrolleyLoadingMaxX =
                    expectedWorkerTrolleyCustomerLoading.position.x +
                    workerTrolleyHullCenterLocalZ +
                    workerTrolleyHullHalfLocalZ;
                float rearFacingVehicleMaxZ =
                    loadingDepartureRoute[0].position.z +
                    rearFacingVehicleMaxZOffset;
                float workerTrolleyVehicleClearance =
                    workerTrolleyRunMinZ - rearFacingVehicleMaxZ;
                float workerTrolleyStorageClearance =
                    storagePadSouthEdgeZ - workerTrolleyRunMaxZ;
                NavMeshAgent alignmentWorkerAgent =
                    alignmentWorkerPrefab.GetComponent<NavMeshAgent>();
                float directHandAccessClearance =
                    customerLoadingAccess.transform.position.x -
                    workerTrolleyHomeMaxX - alignmentWorkerAgent.radius;
                Vector3 trolleyRun =
                    expectedWorkerTrolleyCustomerLoading.position -
                    expectedWorkerTrolleyHome.position;
                Vector3 trolleyForward =
                    expectedWorkerTrolleyHome.rotation * Vector3.forward;
                Require(Mathf.Approximately(
                            expectedWorkerTrolleyHome.rotation.eulerAngles.y,
                            expectedWorkerTrolleyCustomerLoading.rotation.eulerAngles.y) &&
                        Mathf.Approximately(
                            expectedWorkerTrolleyHome.position.z,
                            expectedWorkerTrolleyCustomerLoading.position.z) &&
                        Mathf.Approximately(trolleyRun.magnitude, 2f) &&
                        Vector3.Dot(trolleyRun.normalized, trolleyForward) >= 0.999f &&
                        Mathf.Approximately(workerTrolleyHomeMinX, 3.1f) &&
                        Mathf.Approximately(workerTrolleyHomeMaxX, 5.2f) &&
                        Mathf.Approximately(workerTrolleyLoadingMinX, 5.1f) &&
                        Mathf.Approximately(workerTrolleyLoadingMaxX, 7.2f) &&
                        Mathf.Approximately(workerTrolleyRunMinZ, 0.95f) &&
                        Mathf.Approximately(workerTrolleyRunMaxZ, 2.95f) &&
                        Mathf.Approximately(rearFacingVehicleMaxZ, 0.7f) &&
                        workerTrolleyVehicleClearance >= 0.249f &&
                        workerTrolleyStorageClearance >= 0.299f &&
                        directHandAccessClearance >= 0.479f,
                    "The forward-facing +X worker-trolley run must preserve its exact " +
                    "home/loading hulls and clearances: " +
                    $"vehicle {workerTrolleyVehicleClearance:0.###}m, Storage Pad " +
                    $"{workerTrolleyStorageClearance:0.###}m, direct hand access " +
                    $"{directHandAccessClearance:0.###}m.");
                foreach (CustomerParkingSpotSceneLayout parkingLayout in parkingLayouts)
                {
                    Pose[] arrivalRoute = parkingLayout.VehicleArrivalRoute;
                    Pose[] toLoadingRoute = parkingLayout.VehicleToLoadingRoute;
                    Pose[] parkingDepartureRoute =
                        parkingLayout.VehicleParkingDepartureRoute;
                    Pose[] approachRoute = parkingLayout.CustomerApproachRoute;
                    Pose[] returnRoute = parkingLayout.CustomerReturnRoute;
                    Require(arrivalRoute.Length == 5 && toLoadingRoute.Length == 10 &&
                            parkingDepartureRoute.Length == 5 &&
                            approachRoute.Length == 6 && returnRoute.Length == 6,
                        $"Customer parking spot {parkingLayout.Index} has invalid route lengths.");
                    Require(PoseMatches(arrivalRoute[^1], toLoadingRoute[0]) &&
                            Vector3.Distance(
                                arrivalRoute[^1].position,
                                expectedParkingPositions[parkingLayout.Index]) < 0.001f,
                        $"Customer parking spot {parkingLayout.Index} arrival and loading " +
                        "routes must join at its exact authored parking pose.");
                    Pose[] expectedToLoadingRoute =
                    {
                        new(expectedParkingPositions[parkingLayout.Index],
                            Quaternion.identity),
                        new(new Vector3(
                                expectedParkingPositions[parkingLayout.Index].x,
                                0.02f,
                                -26.5f),
                            Quaternion.identity),
                        new(new Vector3(
                                expectedParkingPositions[parkingLayout.Index].x,
                                0.02f,
                                -30f),
                            Quaternion.identity),
                        new(new Vector3(0f, 0.02f, -30f),
                            Quaternion.Euler(0f, 90f, 0f)),
                        new(new Vector3(0f, 0.02f, -12.5f),
                            Quaternion.identity),
                        new(new Vector3(1.5f, 0.02f, -9f),
                            Quaternion.Euler(0f, 25f, 0f)),
                        new(new Vector3(4f, 0.02f, -7.5f),
                            Quaternion.Euler(0f, 60f, 0f)),
                        new(new Vector3(6f, 0.02f, -8f),
                            Quaternion.Euler(0f, 120f, 0f)),
                        new(new Vector3(6f, 0.02f, -10f),
                            Quaternion.Euler(0f, 180f, 0f)),
                        expectedLoadingDepartureRoute[0]
                    };
                    Require(Vector3.Distance(
                                arrivalRoute[0].position,
                                new Vector3(1.5f, 0.02f, -35f)) < 0.001f &&
                            Mathf.Approximately(arrivalRoute[1].position.z, -30f) &&
                            Mathf.Approximately(arrivalRoute[2].position.z, -30f) &&
                            Mathf.Approximately(arrivalRoute[3].position.z, -26.5f) &&
                            toLoadingRoute.Select((pose, index) =>
                                    PoseMatches(pose, expectedToLoadingRoute[index]))
                                .All(matches => matches),
                        $"Customer parking spot {parkingLayout.Index} must maneuver outside " +
                        "the fence, enter through the vehicle gate, turn in the yard and " +
                        "reverse into the loading bay.");
                    Pose[] expectedParkingDepartureRoute =
                    {
                        new(expectedParkingPositions[parkingLayout.Index],
                            Quaternion.identity),
                        new(new Vector3(
                                expectedParkingPositions[parkingLayout.Index].x,
                                0.02f,
                                -26.5f),
                            Quaternion.identity),
                        new(new Vector3(
                                expectedParkingPositions[parkingLayout.Index].x,
                                0.02f,
                                -30f),
                            Quaternion.identity),
                        new(new Vector3(1.5f, 0.02f, -30f),
                            Quaternion.Euler(0f, 90f, 0f)),
                        new(new Vector3(1.5f, 0.02f, -35f),
                            Quaternion.Euler(0f, 180f, 0f))
                    };
                    Require(PoseMatches(arrivalRoute[^1], parkingDepartureRoute[0]) &&
                            parkingDepartureRoute.Select((pose, index) =>
                                    PoseMatches(
                                        pose,
                                        expectedParkingDepartureRoute[index]))
                                .All(matches => matches),
                        $"Customer parking spot {parkingLayout.Index} must expose its exact " +
                        "authored exterior departure route for an impatient customer.");
                    Require(PoseMatches(toLoadingRoute[^1], loadingDepartureRoute[0]),
                        $"Customer parking spot {parkingLayout.Index} must enter the shared " +
                        "loading bay without a pose discontinuity.");
                    Require(PoseMatches(approachRoute[^1], queuePoses[^1]) &&
                            PoseMatches(returnRoute[0], queuePoses[0]) &&
                            PoseMatches(
                                returnRoute[1],
                                queueAbandonExitRoute[^1]) &&
                            PoseMatches(returnRoute[^1], approachRoute[0]),
                        $"Customer parking spot {parkingLayout.Index} pedestrian routes must " +
                        "connect door, queue tail, authored abandon exit and counter service " +
                        "poses.");
                    Require(Vector3.Distance(
                                approachRoute[2].position,
                                new Vector3(-7.25f, 0.02f, -17.15f)) < 0.001f &&
                            Vector3.Distance(
                                approachRoute[3].position,
                                new Vector3(-7.25f, 0.02f, -14.6f)) < 0.001f &&
                            Vector3.Distance(
                                returnRoute[2].position,
                                new Vector3(-7.25f, 0.02f, -14.6f)) < 0.001f &&
                            Vector3.Distance(
                                returnRoute[3].position,
                                new Vector3(-7.25f, 0.02f, -17.15f)) < 0.001f,
                        $"Customer parking spot {parkingLayout.Index} pedestrians must cross " +
                        "the south fence through the dedicated gate.");

                    bool doorIsOnNavMesh = NavMesh.SamplePosition(
                        approachRoute[0].position,
                        out NavMeshHit doorHit,
                        2f,
                        NavMesh.AllAreas);
                    bool serviceIsOnNavMesh = NavMesh.SamplePosition(
                        queuePoses[0].position,
                        out NavMeshHit serviceHit,
                        2f,
                        NavMesh.AllAreas);
                    Require(doorIsOnNavMesh && serviceIsOnNavMesh &&
                            Vector3.Distance(
                                doorHit.position,
                                approachRoute[0].position) <= 0.35f &&
                            Vector3.Distance(
                                serviceHit.position,
                                queuePoses[0].position) <= 0.35f,
                        $"Customer parking spot {parkingLayout.Index} door and counter poses " +
                        "must sample onto the authored NavMesh.");
                    var approachPath = new NavMeshPath();
                    var returnPath = new NavMeshPath();
                    Require(NavMesh.CalculatePath(
                                doorHit.position,
                                serviceHit.position,
                                NavMesh.AllAreas,
                                approachPath) &&
                            approachPath.status == NavMeshPathStatus.PathComplete &&
                            NavMesh.CalculatePath(
                                serviceHit.position,
                                doorHit.position,
                                NavMesh.AllAreas,
                                returnPath) &&
                            returnPath.status == NavMeshPathStatus.PathComplete,
                        $"Customer parking spot {parkingLayout.Index} must expose complete " +
                        "pedestrian NavMesh paths to and from the counter.");
                }

                for (int first = 0; first < expectedParkingPositions.Length; first++)
                {
                    Bounds firstVehicleBounds = new(
                        expectedParkingPositions[first] + new Vector3(0f, 1f, -0.15f),
                        new Vector3(2.3f, 2f, 6.1f));
                    float southFenceClearance = -16.09f - firstVehicleBounds.max.z;
                    Require(southFenceClearance >= 2.4f,
                        $"Customer parking spot {first} must remain fully outside the south " +
                        $"fence; clearance is {southFenceClearance:0.###}m.");
                    for (int second = first + 1;
                         second < expectedParkingPositions.Length;
                         second++)
                    {
                        Bounds secondVehicleBounds = new(
                            expectedParkingPositions[second] +
                            new Vector3(0f, 1f, -0.15f),
                            new Vector3(2.3f, 2f, 6.1f));
                        Require(!firstVehicleBounds.Intersects(secondVehicleBounds),
                            $"Customer parking spots {first} and {second} overlap for the " +
                            "authored vehicle body.");
                    }
                }

                foreach (CustomerParkingSpotSceneLayout movingLayout in parkingLayouts)
                {
                    Pose[] toLoadingRoute = movingLayout.VehicleToLoadingRoute;
                    Vector3 sweptCenter = new(
                        (toLoadingRoute[2].position.x + toLoadingRoute[3].position.x) * 0.5f,
                        1.02f,
                        toLoadingRoute[2].position.z + 0.15f);
                    Vector3 sweptSize = new(
                        Mathf.Abs(toLoadingRoute[3].position.x -
                                  toLoadingRoute[2].position.x) + 2.3f,
                        2f,
                        6.1f);
                    Bounds sweptBody = new(sweptCenter, sweptSize);
                    for (int parkedIndex = 0;
                         parkedIndex < expectedParkingPositions.Length;
                         parkedIndex++)
                    {
                        if (parkedIndex == movingLayout.Index)
                            continue;
                        Bounds parkedBody = new(
                            expectedParkingPositions[parkedIndex] +
                            new Vector3(0f, 1f, -0.15f),
                            new Vector3(2.3f, 2f, 6.1f));
                        float clearance = parkedBody.min.z - sweptBody.max.z;
                        Require(!sweptBody.Intersects(parkedBody) && clearance >= 2f,
                            $"Customer parking spot {movingLayout.Index} loading route sweeps " +
                            $"too close to occupied parking spot {parkedIndex}: " +
                            $"clearance {clearance:0.###}m.");
                    }
                }

                Transform[] allSceneTransforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                Transform customerAccessRoad = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "Customer Access Road");
                Transform southFenceFarLeft = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "South Fence Far Left");
                Transform southFenceMidLeft = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "South Fence Mid Left");
                Require(customerAccessRoad != null && southFenceFarLeft != null &&
                        southFenceMidLeft != null,
                    $"{PrototypeScenePath} must author the exterior customer road and " +
                    "dedicated pedestrian gate.");
                Bounds customerAccessRoadBounds =
                    customerAccessRoad.GetComponent<Renderer>().bounds;
                Bounds southFenceFarLeftBounds =
                    southFenceFarLeft.GetComponent<Renderer>().bounds;
                Bounds southFenceMidLeftBounds =
                    southFenceMidLeft.GetComponent<Renderer>().bounds;
                Require(customerAccessRoadBounds.min.x <= -16.99f &&
                        customerAccessRoadBounds.max.x >= 5.99f &&
                        customerAccessRoadBounds.min.z <= -38.99f &&
                        customerAccessRoadBounds.max.z >= -16.01f &&
                        southFenceFarLeftBounds.max.x <= -8.49f &&
                        southFenceMidLeftBounds.min.x >= -6.01f &&
                        southFenceMidLeftBounds.min.x - southFenceFarLeftBounds.max.x >= 2.49f,
                    "The exterior access road and pedestrian gate must preserve their " +
                    "authored clearances outside the yard fence.");
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
                Vector3 customerCounterPosition = queuePoses[0].position;
                Require(customerCounterPosition.z < customerCounterBounds.min.z - 0.2f &&
                        customerCounterPosition.x > customerOrderTerminalBounds.min.x &&
                        customerCounterPosition.x < customerOrderTerminalBounds.max.x,
                    "Customer walk route must end on the accessible visitor side, aligned with " +
                    "the customer order terminal rather than the divider between terminals.");

                var expectedSceneViewIds = new HashSet<SceneViewId>
                {
                    SceneViewId.CustomerOrderCounter,
                    SceneViewId.ProcurementTerminal,
                    SceneViewId.StorageZone,
                    SceneViewId.TrolleyUpgradeTerminal,
                    SceneViewId.StoreControlTerminal
                };
                var actualSceneViewIds = new HashSet<SceneViewId>(sceneViews.Select(marker => marker.Id));
                Require(sceneViews.Length == expectedSceneViewIds.Count &&
                        actualSceneViewIds.SetEquals(expectedSceneViewIds),
                    $"{PrototypeScenePath} must contain exactly one marker for every SceneViewId.");
                Require(sceneViews.All(marker => marker.View is InteractionView),
                    "Every static scene view marker must reference an InteractionView on the same object.");
                Require(entityViews.Length == sceneViews.Length &&
                        new HashSet<EntityBehaviour>(sceneViews.Select(marker => marker.View)).SetEquals(entityViews),
                    $"{PrototypeScenePath} must contain only the five marked static entity views.");
                Require(slotRegistrars.Length == 1,
                    $"{PrototypeScenePath} must contain scene slots only for storage.");

                SceneViewMarker storage = sceneViews.Single(marker => marker.Id == SceneViewId.StorageZone);
                SlotsRegistrar storageSlotsRegistrar = storage.GetComponent<SlotsRegistrar>();
                Require(storageSlotsRegistrar != null,
                    "The storage scene view must have a SlotsRegistrar.");
                Transform[] storageSlots = ReadSlots(storageSlotsRegistrar, PrototypeScenePath);
                Require(storageSlots.Length == RequiredStorageSlotCapacity,
                    $"Storage must expose exactly {RequiredStorageSlotCapacity} unique slots.");
                ValidateBoundedProcurementSolvencyPolicy(storageSlots.Length);
                Vector3 maximumGeometry = productPrefabs
                    .Select((productPrefab, index) => ReadSolidProductGeometry(
                        productPrefab,
                        productPrefabPaths[index]))
                    .Aggregate(
                        Vector3.zero,
                        (maximum, geometry) => new Vector3(
                            Mathf.Max(maximum.x, geometry.x),
                            Mathf.Max(maximum.y, geometry.y),
                            Mathf.Max(maximum.z, geometry.z)));
                Transform materialsStorage = storage.transform.parent;
                for (int index = 0; index < storageSlots.Length; index++)
                {
                    int levelIndex = index / 9;
                    int levelSlotIndex = index % 9;
                    int columnIndex = levelSlotIndex % 3;
                    int rowIndex = levelSlotIndex / 3;
                    Vector3 expectedPosition = new(
                        2.4f + columnIndex * 1.9f,
                        0.68f + levelIndex * 1.27f,
                        4.4f + rowIndex * 2.05f);
                    Require(storageSlots[index].name == $"Stock Slot {index + 1}" &&
                            storageSlots[index].IsChildOf(materialsStorage) &&
                            Vector3.Distance(storageSlots[index].position, expectedPosition) <
                            0.001f,
                        $"Storage slot {index + 1} must preserve the authored 3 x 3 x 2 " +
                        "shelf layout.");
                }
                for (int first = 0; first < storageSlots.Length; first++)
                {
                    Bounds firstBounds = new(storageSlots[first].position, maximumGeometry);
                    for (int second = first + 1; second < storageSlots.Length; second++)
                    {
                        Bounds secondBounds = new(storageSlots[second].position, maximumGeometry);
                        Require(!firstBounds.Intersects(secondBounds),
                            $"Storage slots {storageSlots[first].name} and " +
                            $"{storageSlots[second].name} overlap for the largest product hull.");
                    }
                }
                Transform[] upperPalletBeams = allSceneTransforms
                    .Where(candidate => candidate.parent == materialsStorage &&
                        candidate.name.StartsWith(
                            "Upper Pallet Beam ",
                            StringComparison.Ordinal))
                    .ToArray();
                Require(upperPalletBeams.Length == 6 &&
                        upperPalletBeams.All(beam =>
                            beam.gameObject.activeInHierarchy &&
                            beam.GetComponent<Renderer>() != null &&
                            Mathf.Approximately(beam.position.y, 1.55f)),
                    "The expanded storage must visibly expose six authored beams for its " +
                    "second shelf tier.");
                float upperBeamTop = upperPalletBeams
                    .Max(beam => beam.GetComponent<Renderer>().bounds.max.y);
                Require(storageSlots.Skip(9).All(slot =>
                        slot.position.y - maximumGeometry.y * 0.5f >=
                        upperBeamTop + 0.019f),
                    "Every upper storage slot must clear its visible shelf beams for the " +
                    "largest product hull.");
                Require(storage.name == "Storage Intake Target" &&
                        materialsStorage != null &&
                        materialsStorage.name == "Materials Storage" &&
                        storage.transform.position == new Vector3(5f, 1.8f, 6.5f) &&
                        storage.transform.rotation == Quaternion.identity &&
                        storage.transform.lossyScale == Vector3.one,
                    "Storage intake must be one centered, unit-scale proxy for the complete " +
                    "materials-storage footprint.");
                Require(storage.GetComponentsInChildren<Renderer>(true).Length == 0 &&
                        storage.GetComponentsInChildren<InteractionHighlight>(true).Length == 0,
                    "Storage intake proxy and its trigger faces must be completely invisible.");
                NonOccludingInteractionProxy[] nonOccludingProxies =
                    FindComponentsInScene<NonOccludingInteractionProxy>(scene);
                Require(nonOccludingProxies.Length == 1 &&
                        nonOccludingProxies[0].transform == storage.transform &&
                        storage.GetComponents<NonOccludingInteractionProxy>().Length == 1,
                    "The storage intake root must be the scene's only explicitly non-occluding " +
                    "interaction proxy.");

                Transform storagePad = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "Storage Pad" && candidate.parent != null &&
                    candidate.parent.name == "Materials Storage");
                Require(storagePad != null && storagePad.GetComponent<Renderer>() != null,
                    "Materials Storage must retain one visible storage pad.");
                BoxCollider storageThreshold = storagePad.GetComponent<BoxCollider>();
                Require(storagePad.position == new Vector3(5f, 0.1f, 6.5f) &&
                        storagePad.rotation == Quaternion.identity &&
                        storagePad.lossyScale == new Vector3(7.5f, 0.2f, 6.5f) &&
                        storageThreshold != null && storageThreshold.enabled &&
                        !storageThreshold.isTrigger &&
                        storageThreshold.gameObject.layer == LayerMask.NameToLayer("Default") &&
                        storageThreshold.center == Vector3.zero &&
                        storageThreshold.size == Vector3.one &&
                        Mathf.Approximately(storageThreshold.bounds.min.y, 0f) &&
                        Mathf.Approximately(storageThreshold.bounds.max.y, 0.2f),
                    "The authored Storage Pad must remain a solid 0.20 metre warehouse " +
                    "threshold below the player's validated 0.32 metre step limit.");
                InteractionHighlight storagePadHighlight =
                    storagePad.GetComponent<InteractionHighlight>();
                SerializedProperty storageHighlightProperty =
                    new SerializedObject(storage.View).FindProperty("_highlight");
                Require(storagePadHighlight != null &&
                        storageHighlightProperty != null &&
                        storageHighlightProperty.objectReferenceValue == storagePadHighlight,
                    "The invisible storage proxy must highlight the visible storage pad through " +
                    "its InteractionView reference.");

                Collider[] storageInteractionColliders = storage.View
                    .GetComponentsInChildren<Collider>(true);
                Require(storageInteractionColliders.Length == 4 &&
                        storageInteractionColliders.All(collider =>
                            collider is BoxCollider && collider.isTrigger && collider.enabled &&
                            collider.gameObject.activeInHierarchy &&
                            collider.gameObject.layer == LayerMask.NameToLayer("Default") &&
                            collider.GetComponent<NonOccludingInteractionProxy>() == null),
                    "Storage intake must expose exactly four active Default-layer BoxCollider " +
                    "trigger faces.");
                var expectedStorageFaces = new[]
                {
                    (Name: "Storage Intake Front Trigger",
                        Position: new Vector3(0f, 0f, -3.25f),
                        Size: new Vector3(7.5f, 3.4f, 0.2f)),
                    (Name: "Storage Intake Back Trigger",
                        Position: new Vector3(0f, 0f, 3.25f),
                        Size: new Vector3(7.5f, 3.4f, 0.2f)),
                    (Name: "Storage Intake Left Trigger",
                        Position: new Vector3(-3.75f, 0f, 0f),
                        Size: new Vector3(0.2f, 3.4f, 6.5f)),
                    (Name: "Storage Intake Right Trigger",
                        Position: new Vector3(3.75f, 0f, 0f),
                        Size: new Vector3(0.2f, 3.4f, 6.5f))
                };
                foreach ((string name, Vector3 position, Vector3 size) in expectedStorageFaces)
                {
                    BoxCollider face = storageInteractionColliders
                        .Cast<BoxCollider>()
                        .SingleOrDefault(collider => collider.name == name);
                    Require(face != null && face.transform.parent == storage.transform &&
                            face.transform.localPosition == position &&
                            face.transform.localRotation == Quaternion.identity &&
                            face.transform.localScale == Vector3.one &&
                            face.center == Vector3.zero && face.size == size,
                        $"Storage interaction face {name} must preserve its exact hollow-proxy " +
                        "geometry.");
                }

                Bounds storageIntakeVolume = new(
                    storage.transform.position,
                    new Vector3(7.5f, 3.4f, 6.5f));
                foreach (Transform storageSlot in storageSlots)
                {
                    Require(storageIntakeVolume.Contains(storageSlot.position),
                        $"Storage slot {storageSlot.name} at {storageSlot.position} lies outside " +
                        "the complete storage intake proxy.");
                }

                SceneViewMarker trolleyUpgradeTerminal = sceneViews.Single(
                    marker => marker.Id == SceneViewId.TrolleyUpgradeTerminal);
                Require(trolleyUpgradeTerminal.GetComponent<InteractionViewRegistrar>() != null &&
                        trolleyUpgradeTerminal.GetComponent<SlotsRegistrar>() == null,
                    "The trolley upgrade terminal must use the generic interaction registrar " +
                    "without owning runtime cargo slots.");
                SpawnPointMarker trolleySpawn = spawnPoints.Single(
                    marker => marker.Id == SpawnPointId.PlatformTrolley);
                Transform trolleyStationPad = allSceneTransforms.SingleOrDefault(candidate =>
                    candidate.name == "Station Pad" && candidate.parent != null &&
                    candidate.parent.name == "Trolley Upgrade Station");
                Require(trolleySpawn.gameObject.scene == scene &&
                        trolleySpawn.transform != trolleyUpgradeTerminal.transform &&
                        Mathf.Approximately(trolleySpawn.transform.position.y, 0.01f) &&
                        trolleyStationPad != null &&
                        trolleyStationPad.GetComponent<Collider>() == null,
                    "The platform trolley must have one distinct authored runtime spawn pose " +
                    "on yard level, and its station pad must remain decorative so it cannot " +
                    "block the trolley's first collision-safe movement.");

                SceneViewMarker storeControlTerminal = sceneViews.Single(
                    marker => marker.Id == SceneViewId.StoreControlTerminal);
                Require(storeControlTerminal.name == "Store Control Terminal" &&
                        storeControlTerminal.transform.parent != null &&
                        storeControlTerminal.transform.parent.name == "Store Control Station" &&
                        storeControlTerminal.transform.position == new Vector3(-5f, 1.22f, 0f) &&
                        storeControlTerminal.transform.rotation == Quaternion.identity &&
                        storeControlTerminal.transform.lossyScale ==
                        new Vector3(1.65f, 0.72f, 0.18f),
                    "The store control terminal must remain a distinct station beside the kiosk " +
                    "entrance, away from the customer and procurement counter controls.");
                Require(storeControlTerminal.GetComponent<InteractionViewRegistrar>() != null &&
                        storeControlTerminal.GetComponent<SlotsRegistrar>() == null,
                    "The store control terminal must use only the generic interaction registrar.");
                Collider[] storeControlColliders =
                    storeControlTerminal.GetComponents<Collider>();
                Require(storeControlColliders.Length == 1 &&
                        storeControlColliders[0] is BoxCollider controlTrigger &&
                        controlTrigger.enabled && controlTrigger.isTrigger &&
                        controlTrigger.gameObject.layer == LayerMask.NameToLayer("Default") &&
                        controlTrigger.center == new Vector3(0f, 0f, -2f) &&
                        controlTrigger.size == new Vector3(1.35f, 2.8f, 5f),
                    "The store control terminal must expose one authored interaction trigger.");
                Require(Vector3.Distance(
                            storeControlTerminal.transform.position,
                            customerOrderTerminal.position) > 2.5f &&
                        storeControlTerminal.transform.position.z <
                        customerCounterBounds.min.z - 0.2f,
                    "The store control station must stay near the accessible kiosk entrance " +
                    "without crowding either customer-facing counter action.");

                Transform lumberDisplay = allSceneTransforms.SingleOrDefault(
                    candidate => candidate.name == "Lumber Display");
                Require(lumberDisplay != null,
                    $"{PrototypeScenePath} must contain the functional Lumber Display area.");
                Require(lumberDisplay.GetComponentsInChildren<Transform>(true)
                            .Count(candidate => candidate.name.StartsWith(
                                "Board Display Bundle",
                                StringComparison.Ordinal)) >= 3,
                    "Lumber Display must visibly expose stocked board bundles, address and product signage.");
                ValidateLocalizedWorldLabels(
                    localizedWorldLabels,
                    new[]
                    {
                        LocalizationKey.WorldOrderCounter,
                        LocalizationKey.WorldProcurement,
                        LocalizationKey.WorldStorageCatalog,
                        LocalizationKey.WorldStorageIntake,
                        LocalizationKey.WorldCustomerParking,
                        LocalizationKey.WorldCustomerLoadingBay,
                        LocalizationKey.WorldDeliveryIntake,
                        LocalizationKey.WorldBoardProductLabel,
                        LocalizationKey.WorldTrolleyUpgrade,
                        LocalizationKey.WorldStoreControlTerminal
                    },
                    PrototypeScenePath);
                LocalizedTextMeshView trolleyUpgradeLabel = localizedWorldLabels.Single(
                    view => view.Key == LocalizationKey.WorldTrolleyUpgrade);
                Require(trolleyUpgradeLabel.NumberArguments.SequenceEqual(new[] { 200 }),
                    "The trolley upgrade world label must author the configured 200 price.");

                GameObject deliveryPrefab = RequireAsset<GameObject>(DeliveryVehiclePrefabPath);
                Require(!ContainsPrefabInstance(scene, deliveryPrefab),
                    $"{PrototypeScenePath} must not contain a supplier truck prefab instance.");
                GameObject customerVehiclePrefab = RequireAsset<GameObject>(CustomerVehiclePrefabPath);
                Require(!ContainsPrefabInstance(scene, customerVehiclePrefab),
                    $"{PrototypeScenePath} must not contain a customer vehicle prefab instance.");
                GameObject trolleyPrefab = RequireAsset<GameObject>(PlatformTrolleyPrefabPath);
                Require(!ContainsPrefabInstance(scene, trolleyPrefab),
                    $"{PrototypeScenePath} must spawn the platform trolley at runtime, not " +
                    "contain a prefab instance.");
                GameObject workerPrefab = RequireAsset<GameObject>(WarehouseWorkerPrefabPath);
                Require(!ContainsPrefabInstance(scene, workerPrefab),
                    $"{PrototypeScenePath} must spawn the warehouse worker at runtime, not " +
                    "contain a prefab instance.");
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

        private static void RequireNoDeclaredMembers(Type owner,
            params string[] forbiddenMemberNames)
        {
            const BindingFlags declaredMembers =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (string memberName in forbiddenMemberNames)
            {
                Require(owner.GetMember(memberName, declaredMembers).Length == 0,
                    $"{owner.Name} must not retain localized content member {memberName}.");
            }
        }

        private static void ValidateImmutableSnapshotType(Type snapshotType)
        {
            Require(snapshotType.IsValueType && snapshotType.IsSealed,
                $"{snapshotType.Name} must remain an immutable readonly value type.");
            Require(snapshotType.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.DeclaredOnly).Length == 0 &&
                    snapshotType.GetProperties(
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.DeclaredOnly)
                        .All(property => property.CanRead && !property.CanWrite),
                $"{snapshotType.Name} must expose data only through get-only properties.");
        }

        private static void ValidateLocalizedWorldLabels(
            IReadOnlyCollection<LocalizedTextMeshView> localizedViews,
            IReadOnlyCollection<LocalizationKey> expectedKeys,
            string owner)
        {
            LocalizationKey[] actualKeys = localizedViews.Select(view => view.Key).ToArray();
            Require(localizedViews.Count == expectedKeys.Count &&
                    actualKeys.Distinct().Count() == actualKeys.Length &&
                    new HashSet<LocalizationKey>(actualKeys).SetEquals(expectedKeys),
                $"{owner} must contain exactly one localized world label for every expected key.");

            ILocalizationCatalog catalog = new RussianLocalizationCatalog();
            var entries = catalog.Entries.ToDictionary(entry => entry.Key);
            var localization = new LocalizationService(new[] { catalog });
            localization.Load(LanguageId.Russian);
            foreach (LocalizedTextMeshView localizedView in localizedViews)
            {
                Require(localizedView.Label != null &&
                        localizedView.Label.gameObject == localizedView.gameObject,
                    $"Localized world label {localizedView.name} in {owner} must reference the " +
                    "TextMesh on the same object.");
                Require(entries.TryGetValue(localizedView.Key, out LocalizationEntry entry),
                    $"Localized world label {localizedView.name} in {owner} uses an unknown key.");

                int[] numberArguments = localizedView.NumberArguments;
                Require(numberArguments.Length == entry.ArgumentCount,
                    $"Localized world label {localizedView.name} in {owner} must provide " +
                    $"{entry.ArgumentCount} numeric arguments for {localizedView.Key}.");
                var arguments = new LocalizationArgument[numberArguments.Length];
                for (int index = 0; index < arguments.Length; index++)
                    arguments[index] = numberArguments[index];
                string preview = localization.Resolve(
                    LocalizedTexts.Text(localizedView.Key, arguments));
                Require(localizedView.Label.text == preview,
                    $"Localized world label {localizedView.name} in {owner} must serialize the " +
                    "Russian preview resolved from its key and arguments.");
            }
        }

        private static void ValidateImmutableSnapshotCollection(
            Type owner,
            string propertyName,
            Type expectedPropertyType)
        {
            PropertyInfo property = owner.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(property?.PropertyType == expectedPropertyType &&
                    property.CanRead && !property.CanWrite,
                $"{owner.Name}.{propertyName} must expose the read-only collection type " +
                $"{expectedPropertyType.Name}.");
        }

        private static void ValidateSnapshotProperties(
            Type snapshotType,
            params (string Name, Type Type)[] expectedProperties)
        {
            PropertyInfo[] properties = snapshotType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(properties.Length == expectedProperties.Length,
                $"{snapshotType.Name} must expose exactly {expectedProperties.Length} " +
                "public properties.");
            foreach ((string name, Type type) in expectedProperties)
            {
                PropertyInfo property = properties.SingleOrDefault(
                    candidate => candidate.Name == name);
                Require(property?.PropertyType == type &&
                        property.CanRead && !property.CanWrite,
                    $"{snapshotType.Name}.{name} must be a get-only {type.Name} property.");
            }
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

        private static void RequireSourceOrder(
            string source,
            string first,
            string second,
            string message)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Require(firstIndex >= 0 && secondIndex > firstIndex, message);
        }

        private static void RequireExactFeatureOrder(
            string source,
            IReadOnlyList<string> systemNames,
            string featureName)
        {
            int previousIndex = -1;
            foreach (string systemName in systemNames)
            {
                string token = $"Create<{systemName}>()";
                int index = source.IndexOf(token, StringComparison.Ordinal);
                Require(index > previousIndex && CountOccurrences(source, token) == 1,
                    $"{featureName} must execute {string.Join(" -> ", systemNames)} " +
                    "exactly once.");
                previousIndex = index;
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

        private static bool PoseMatches(Pose first, Pose second) =>
            Vector3.Distance(first.position, second.position) < 0.001f &&
            Quaternion.Angle(first.rotation, second.rotation) < 0.01f;

        private static Pose ResolveWorkerTrolleyPusherPose(
            Pose trolleyPose,
            float followDistance) =>
            new(
                trolleyPose.position -
                trolleyPose.rotation * Vector3.forward * followDistance,
                trolleyPose.rotation);

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
