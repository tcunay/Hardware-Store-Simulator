using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Entitas;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Registrars;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.Installers;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Registrars;
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
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string ProductConfigPath = "Assets/Resources/Configs/ProductConfig.asset";
        private const string DeliveryConfigPath = "Assets/Resources/Configs/DeliveryConfig.asset";
        private const string EconomyConfigPath = "Assets/Resources/Configs/EconomyConfig.asset";
        private const string OrderConfigPath = "Assets/Resources/Configs/OrderConfig.asset";
        private const string ProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/CementBag.prefab";
        private const string DeliveryVehiclePrefabPath = "Assets/_Project/Prefabs/Gameplay/DeliveryTruck.prefab";
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
            "ProductView",
            "ProductViewRegistrar",
            "ProductViewComponent",
            "GameProductViewComponent"
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
            }
        }

        private static void ValidateStoreArchitecture(IEnumerable<Type> componentTypes)
        {
            var discoveredComponents = new HashSet<Type>(componentTypes);
            Require(discoveredComponents.Contains(typeof(Store)),
                $"{nameof(Store)} must be declared as a Game component.");
            Require(discoveredComponents.Contains(typeof(StoreEntityId)),
                $"{nameof(StoreEntityId)} must be declared as a Game relation component.");
            Require(typeof(IStoreFactory).IsAssignableFrom(typeof(StoreFactory)),
                $"{nameof(StoreFactory)} must implement {nameof(IStoreFactory)}.");

            MethodInfo createMethod = typeof(IStoreFactory).GetMethod(
                nameof(IStoreFactory.Create),
                new[] { typeof(IStoreSceneData) });
            Require(createMethod != null && createMethod.ReturnType == typeof(GameEntity),
                $"{nameof(IStoreFactory)} must create and return a Store GameEntity from " +
                $"{nameof(IStoreSceneData)}.");
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
                SceneViewMarker[] configuredSceneViews = ReadObjectArray<SceneViewMarker>(
                    serializedInitializer, "_sceneViews", nameof(PrototypeSceneInitializer));
                Require(new HashSet<SpawnPointMarker>(configuredSpawnPoints).SetEquals(spawnPoints) &&
                        configuredSpawnPoints.Length == spawnPoints.Length,
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene spawn point set.");
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

                var expectedSceneViewIds = new HashSet<SceneViewId>
                {
                    SceneViewId.CustomerOrderCounter,
                    SceneViewId.CustomerLoadingZone,
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
                    $"{PrototypeScenePath} must contain only the four marked static entity views.");
                Require(slotRegistrars.Length == 2,
                    $"{PrototypeScenePath} must contain slots only for storage and customer loading.");

                SceneViewMarker storage = sceneViews.Single(marker => marker.Id == SceneViewId.StorageZone);
                SceneViewMarker customerLoading =
                    sceneViews.Single(marker => marker.Id == SceneViewId.CustomerLoadingZone);
                SlotsRegistrar storageSlotsRegistrar = storage.GetComponent<SlotsRegistrar>();
                SlotsRegistrar customerSlotsRegistrar = customerLoading.GetComponent<SlotsRegistrar>();
                Require(storageSlotsRegistrar != null && customerSlotsRegistrar != null,
                    "Storage and customer loading scene views must each have a SlotsRegistrar.");
                Transform[] storageSlots = ReadSlots(storageSlotsRegistrar, PrototypeScenePath);
                Transform[] customerSlots = ReadSlots(customerSlotsRegistrar, PrototypeScenePath);
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

                OrderConfig orderConfig = RequireAsset<OrderConfig>(OrderConfigPath);
                Require(customerSlots.Length >= orderConfig.RequiredProductCount,
                    "Customer loading slots must cover the configured outbound order quantity.");
                Require(storageSlots.Concat(customerSlots).Distinct().Count() ==
                        storageSlots.Length + customerSlots.Length,
                    "Storage and customer loading slot references must be globally unique.");

                GameObject deliveryPrefab = RequireAsset<GameObject>(DeliveryVehiclePrefabPath);
                Require(!ContainsPrefabInstance(scene, deliveryPrefab),
                    $"{PrototypeScenePath} must not contain a supplier truck prefab instance.");
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
