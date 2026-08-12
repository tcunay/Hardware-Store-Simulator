using System;
using System.Collections.Generic;
using System.Linq;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Registrars;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.Installers;
using HardwareStore.Infrastructure.View;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Zenject;
using Object = UnityEngine.Object;

namespace HardwareStore.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Prototype_Yard.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/Prototype";
        private const string ProjectContextPath = "Assets/Resources/ProjectContext.prefab";
        private const string ConfigFolder = "Assets/Resources/Configs";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string CementProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/CementBag.prefab";
        private const string BoardProductPrefabPath = "Assets/_Project/Prefabs/Gameplay/BoardBundle.prefab";
        private const string DeliveryVehiclePrefabPath = "Assets/_Project/Prefabs/Gameplay/DeliveryTruck.prefab";
        private const string CustomerVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicle.prefab";
        private const string CustomerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Customer.prefab";
        private const string WarehouseWorkerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/WarehouseWorker.prefab";
        private const string PlatformTrolleyPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/PlatformTrolley.prefab";
        private const string WarehouseWorkerNavMeshAssetName = "NavMesh-Navigation";
        private const string WarehouseWorkerNavMeshPath =
            "Assets/Scenes/Prototype_Yard/" + WarehouseWorkerNavMeshAssetName + ".asset";
        private const string CementProductConfigName = "ProductConfig";
        private const string BoardProductConfigName = "ProductConfig_BoardBundle";
        private const string CementDeliveryConfigName = "DeliveryConfig";
        private const string BoardDeliveryConfigName = "DeliveryConfig_BoardBundle";
        private const string CementProjectConfigName =
            "CustomerProjectConfig_CementFoundation";
        private const string LumberProjectConfigName =
            "CustomerProjectConfig_LumberShelving";
        private const string WorkbenchProjectConfigName =
            "CustomerProjectConfig_WorkbenchFoundation";
        private const string ProductRecoveryConfigName = "ProductRecoveryConfig";
        private const string PlatformTrolleyConfigName = "PlatformTrolleyConfig";
        private const string WarehouseWorkerConfigName = "WarehouseWorkerConfig";
        private const string StoreDayConfigName = "StoreDayConfig";
        private const string LegacyCementOrderConfigName = "OrderConfig";
        private const string LegacyBoardOrderConfigName = "OrderConfig_BoardBundle";
        private const int CustomerVehicleCargoCapacity = 3;
        private const int StorageSlotCapacity = 9;
        private static readonly ILocalizationService RussianPreviewLocalization =
            CreateRussianPreviewLocalization();

        [MenuItem("Tools/Hardware Store/Build Prototype Yard")]
        public static void BuildPrototypeYard()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the prototype scene.");

            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the currently open scene before rebuilding the prototype.");

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Gameplay");
            EnsureFolder("Assets/Resources");
            EnsureFolder(ConfigFolder);
            EnsureConfigAssets();
            PlayerConfig playerConfig = LoadConfig<PlayerConfig>("PlayerConfig");
            DeliveryConfig cementDeliveryConfig =
                LoadConfig<DeliveryConfig>(CementDeliveryConfigName);
            DeliveryConfig boardDeliveryConfig =
                LoadConfig<DeliveryConfig>(BoardDeliveryConfigName);
            CustomerVehicleConfig customerVehicleConfig =
                LoadConfig<CustomerVehicleConfig>("CustomerVehicleConfig");
            CustomerConfig customerConfig = LoadConfig<CustomerConfig>("CustomerConfig");
            EconomyConfig economyConfig = LoadConfig<EconomyConfig>("EconomyConfig");
            ProductRecoveryConfig productRecoveryConfig =
                LoadConfig<ProductRecoveryConfig>(ProductRecoveryConfigName);
            PlatformTrolleyConfig platformTrolleyConfig =
                LoadConfig<PlatformTrolleyConfig>(PlatformTrolleyConfigName);
            WarehouseWorkerConfig warehouseWorkerConfig =
                LoadConfig<WarehouseWorkerConfig>(WarehouseWorkerConfigName);
            StoreDayConfig storeDayConfig = LoadConfig<StoreDayConfig>(StoreDayConfigName);
            ProductConfig cementProductConfig =
                LoadConfig<ProductConfig>(CementProductConfigName);
            ProductConfig boardProductConfig =
                LoadConfig<ProductConfig>(BoardProductConfigName);
            CustomerProjectConfig cementProjectConfig =
                LoadConfig<CustomerProjectConfig>(CementProjectConfigName);
            CustomerProjectConfig lumberProjectConfig =
                LoadConfig<CustomerProjectConfig>(LumberProjectConfigName);
            CustomerProjectConfig workbenchProjectConfig =
                LoadConfig<CustomerProjectConfig>(WorkbenchProjectConfigName);
            ConfigurePrototypeConfigs(
                cementDeliveryConfig,
                boardDeliveryConfig,
                economyConfig,
                productRecoveryConfig,
                storeDayConfig,
                warehouseWorkerConfig,
                cementProductConfig,
                boardProductConfig,
                cementProjectConfig,
                lumberProjectConfig,
                workbenchProjectConfig);
            EnsurePlayerPrefab(playerConfig);
            EnsureProjectContextPrefab();

            Material asphalt = GetOrCreateMaterial("Asphalt", new Color(0.12f, 0.14f, 0.15f), 0.12f);
            Material concrete = GetOrCreateMaterial("Concrete", new Color(0.48f, 0.49f, 0.47f), 0.08f);
            Material brandBlue = GetOrCreateMaterial("BrandBlue", new Color(0.055f, 0.19f, 0.32f), 0.26f);
            Material brandOrange = GetOrCreateMaterial("BrandOrange", new Color(0.95f, 0.31f, 0.055f), 0.22f, true);
            Material cement = GetOrCreateMaterial("CementBag", new Color(0.67f, 0.62f, 0.50f), 0.03f);
            Material timber = GetOrCreateMaterial("Timber", new Color(0.48f, 0.27f, 0.11f), 0.14f);
            Material boardStrap = GetOrCreateMaterial("BoardStrap", new Color(0.1f, 0.12f, 0.11f), 0.38f);
            Material darkMetal = GetOrCreateMaterial("DarkMetal", new Color(0.075f, 0.085f, 0.095f), 0.52f);
            Material truckPaint = GetOrCreateMaterial("TruckPaint", new Color(0.095f, 0.34f, 0.53f), 0.42f);
            Material loadingGreen = GetOrCreateMaterial("LoadingGreen", new Color(0.08f, 0.78f, 0.36f), 0.18f, true);
            Material glass = GetOrCreateMaterial("Glass", new Color(0.12f, 0.24f, 0.31f), 0.72f);
            Material white = GetOrCreateMaterial("White", new Color(0.82f, 0.84f, 0.82f), 0.18f);
            Material yellow = GetOrCreateMaterial("SafetyYellow", new Color(0.95f, 0.65f, 0.08f), 0.18f);

            EnsureCementProductPrefab(cementProductConfig, cement);
            EnsureBoardProductPrefab(boardProductConfig, timber, boardStrap);
            EnsureDeliveryVehiclePrefab(
                new[] { cementDeliveryConfig, boardDeliveryConfig },
                yellow,
                darkMetal,
                glass,
                timber);
            EnsureCustomerVehiclePrefab(
                customerVehicleConfig,
                truckPaint,
                darkMetal,
                glass,
                loadingGreen);
            EnsureCustomerPrefab(customerConfig, brandOrange, brandBlue, darkMetal);
            EnsureWarehouseWorkerPrefab(
                warehouseWorkerConfig,
                brandOrange,
                brandBlue,
                yellow,
                darkMetal);
            EnsurePlatformTrolleyPrefab(
                platformTrolleyConfig,
                brandOrange,
                darkMetal,
                timber);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();

            GameObject environment = CreateEmpty("Environment");
            NavMeshSurface navigation = BuildNavigation(environment.transform);
            (Light sun, Light[] indoorLights) = BuildLighting(environment.transform);
            BuildYard(environment.transform, asphalt, concrete, brandBlue, white, yellow);
            (SceneViewMarker orderCounter, SceneViewMarker procurementTerminal) =
                BuildShop(environment.transform, concrete, brandBlue, brandOrange, darkMetal, glass);
            SceneViewMarker storeControlTerminal = BuildStoreControlTerminal(
                environment.transform,
                concrete,
                brandOrange,
                darkMetal);
            (SceneViewMarker trolleyUpgradeTerminal, SpawnPointMarker platformTrolleySpawnPoint) =
                BuildTrolleyUpgradeArea(
                    environment.transform,
                    concrete,
                    brandOrange,
                    darkMetal);
            SceneViewMarker storageZone = BuildMaterialsStorage(
                environment.transform,
                concrete,
                brandBlue,
                timber,
                darkMetal,
                brandOrange);
            SceneRouteMarker[] customerRoutes = BuildCustomerRoutes(
                environment.transform, asphalt, white, loadingGreen);
            BuildLumberArea(
                environment.transform,
                concrete,
                brandBlue,
                brandOrange,
                timber,
                darkMetal,
                boardProductConfig);
            SpawnPointMarker deliveryVehicleSpawnPoint =
                BuildInboundDeliveryBay(environment.transform, asphalt, yellow);
            (SpawnPointMarker workerIdlePoint,
                    SpawnPointMarker workerDeliveryAccessPoint,
                    SpawnPointMarker workerStorageAccessPoint) =
                BuildWarehouseWorkerAccessPoints(environment.transform);

            SpawnPointMarker playerSpawnPoint = BuildPlayerSpawnPoint();
            GameObject systems = CreateEmpty("SceneContext");
            SceneContext sceneContext = systems.AddComponent<SceneContext>();
            PrototypeAudioView audio = systems.AddComponent<PrototypeAudioView>();
            PrototypeHudView hud = systems.AddComponent<PrototypeHudView>();
            PrototypeDayNightView dayNight = systems.AddComponent<PrototypeDayNightView>();
            dayNight.Configure(sun, indoorLights);
            PrototypeSceneInitializer initializer = systems.AddComponent<PrototypeSceneInitializer>();
            initializer.Configure(
                new[]
                {
                    playerSpawnPoint,
                    deliveryVehicleSpawnPoint,
                    platformTrolleySpawnPoint,
                    workerIdlePoint,
                    workerDeliveryAccessPoint,
                    workerStorageAccessPoint
                },
                customerRoutes,
                new[]
                {
                    orderCounter,
                    procurementTerminal,
                    storageZone,
                    trolleyUpgradeTerminal,
                    storeControlTerminal
                },
                hud,
                audio,
                dayNight);
            SceneInitializationInstaller installer = systems.AddComponent<SceneInitializationInstaller>();
            installer.Configure(initializer);
            sceneContext.Installers = new MonoInstaller[] { installer };

            BakeAndValidateWarehouseWorkerNavigation(
                navigation,
                workerIdlePoint,
                workerDeliveryAccessPoint,
                workerStorageAccessPoint);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save prototype scene to {ScenePath}.");

            PutSceneFirstInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = playerSpawnPoint.gameObject;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log($"[Hardware Store] Playable prototype scene created: {ScenePath}");
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.53f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.37f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.57f, 0.64f, 0.68f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 115f;
        }

        private static NavMeshSurface BuildNavigation(Transform parent)
        {
            GameObject navigation = CreateEmpty("Navigation", parent);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.layerMask = ~0;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = NavMesh.GetAreaFromName("Walkable");
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f;
            surface.minRegionArea = 1f;
            return surface;
        }

        private static (Light Sun, Light[] IndoorLights) BuildLighting(Transform parent)
        {
            GameObject sunObject = CreateEmpty("Sun", parent);
            sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.64f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;

            Light shopLight = CreatePointLight("Shop Light", parent, new Vector3(-9f, 2.35f, 4.4f),
                new Color(1f, 0.61f, 0.32f), 6.5f, 520f);
            Light warehouseLight = CreatePointLight("Warehouse Light", parent, new Vector3(5f, 3.25f, 6.2f),
                new Color(0.68f, 0.82f, 1f), 7.5f, 430f);

            return (sun, new[] { shopLight, warehouseLight });
        }

        private static void BuildYard(Transform parent, Material asphalt, Material concrete, Material brandBlue,
            Material white, Material yellow)
        {
            GameObject yard = CreateEmpty("Yard", parent);
            CreateCube("Asphalt Ground", yard.transform, new Vector3(0f, -0.12f, 0f), new Vector3(34f, 0.24f, 34f),
                asphalt);

            CreateCube("North Fence", yard.transform, new Vector3(0f, 1.15f, 16f), new Vector3(32f, 2.3f, 0.18f),
                brandBlue);
            CreateCube("West Fence", yard.transform, new Vector3(-16f, 1.15f, 0f), new Vector3(0.18f, 2.3f, 32f),
                brandBlue);
            CreateCube("East Fence", yard.transform, new Vector3(16f, 1.15f, 0f), new Vector3(0.18f, 2.3f, 32f),
                brandBlue);
            CreateCube("South Fence Left", yard.transform, new Vector3(-10f, 1.15f, -16f),
                new Vector3(12f, 2.3f, 0.18f), brandBlue);
            CreateCube("South Fence Right", yard.transform, new Vector3(10f, 1.15f, -16f),
                new Vector3(12f, 2.3f, 0.18f), brandBlue);

            CreateCube("Entrance Stripe Left", yard.transform, new Vector3(-3.2f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);
            CreateCube("Entrance Stripe Right", yard.transform, new Vector3(3.2f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);

            for (int i = 0; i < 5; i++)
            {
                CreateCube($"Safety Marking {i + 1}", yard.transform, new Vector3(-2.4f + i * 1.2f, 0.02f, -8.5f),
                    new Vector3(0.65f, 0.04f, 0.16f), yellow, false);
            }

            CreateCube("Shop Walkway", yard.transform, new Vector3(-9f, 0.02f, -0.1f),
                new Vector3(6.5f, 0.04f, 1.3f), concrete, false);
        }

        private static (SceneViewMarker OrderCounter, SceneViewMarker ProcurementTerminal) BuildShop(
            Transform parent, Material concrete, Material brandBlue,
            Material brandOrange, Material darkMetal, Material glass)
        {
            GameObject shop = CreateEmpty("Sales Kiosk", parent);
            CreateCube("Floor", shop.transform, new Vector3(-9f, 0.1f, 4.7f), new Vector3(7f, 0.2f, 7f), concrete);
            CreateCube("Back Wall", shop.transform, new Vector3(-9f, 1.65f, 8.1f),
                new Vector3(7f, 3.3f, 0.22f), brandBlue);
            CreateCube("Left Wall", shop.transform, new Vector3(-12.4f, 1.65f, 4.75f),
                new Vector3(0.22f, 3.3f, 6.7f), brandBlue);
            CreateCube("Right Wall", shop.transform, new Vector3(-5.6f, 1.65f, 4.75f),
                new Vector3(0.22f, 3.3f, 6.7f), brandBlue);
            CreateCube("Roof", shop.transform, new Vector3(-9f, 3.35f, 4.75f),
                new Vector3(7.25f, 0.22f, 7f), darkMetal);
            CreateCube("Counter", shop.transform, new Vector3(-9f, 0.72f, 1.65f),
                new Vector3(4.5f, 1.44f, 0.86f), darkMetal);
            CreateCube("Counter Top", shop.transform, new Vector3(-9f, 1.49f, 1.65f),
                new Vector3(4.7f, 0.12f, 1.02f), brandOrange);

            GameObject terminal = CreateCube("Customer Order Terminal", shop.transform,
                new Vector3(-7.9f, 1.15f, 1.16f), new Vector3(1.7f, 0.72f, 0.12f), brandOrange);
            BoxCollider orderInteraction = terminal.GetComponent<BoxCollider>();
            orderInteraction.isTrigger = true;
            orderInteraction.center = new Vector3(0f, 0f, -2f);
            orderInteraction.size = new Vector3(1.2f, 2.8f, 5f);
            InteractionHighlight highlight = terminal.AddComponent<InteractionHighlight>();
            InteractionView counter = terminal.AddComponent<InteractionView>();
            counter.Configure(highlight);
            terminal.AddComponent<InteractionViewRegistrar>();
            SceneViewMarker orderCounter = terminal.AddComponent<SceneViewMarker>();
            orderCounter.Configure(SceneViewId.CustomerOrderCounter);
            CreateWorldLabel("Orders Label", terminal.transform,
                LocalizationKey.WorldOrderCounter, new Vector3(0f, 0f, -0.56f),
                Quaternion.identity, 0.03f, Color.white);

            GameObject procurementObject = CreateCube("Procurement Terminal", shop.transform,
                new Vector3(-10.15f, 1.15f, 1.16f), new Vector3(1.7f, 0.72f, 0.12f), brandOrange);
            BoxCollider procurementInteraction = procurementObject.GetComponent<BoxCollider>();
            procurementInteraction.isTrigger = true;
            procurementInteraction.center = new Vector3(0f, 0f, -2f);
            procurementInteraction.size = new Vector3(1.2f, 2.8f, 5f);
            InteractionHighlight procurementHighlight = procurementObject.AddComponent<InteractionHighlight>();
            InteractionView procurementView = procurementObject.AddComponent<InteractionView>();
            procurementView.Configure(procurementHighlight);
            procurementObject.AddComponent<InteractionViewRegistrar>();
            SceneViewMarker procurementTerminal = procurementObject.AddComponent<SceneViewMarker>();
            procurementTerminal.Configure(SceneViewId.ProcurementTerminal);
            CreateWorldLabel("Procurement Label", procurementObject.transform,
                LocalizationKey.WorldProcurement,
                new Vector3(0f, 0f, -0.56f), Quaternion.identity, 0.03f, Color.white);

            CreateCube("Window", shop.transform, new Vector3(-9f, 2.2f, 8f), new Vector3(3.3f, 1.15f, 0.08f),
                glass, false);
            return (orderCounter, procurementTerminal);
        }

        private static SceneViewMarker BuildStoreControlTerminal(
            Transform parent,
            Material concrete,
            Material brandOrange,
            Material darkMetal)
        {
            GameObject station = CreateEmpty("Store Control Station", parent);
            CreateCube(
                "Store Control Pad",
                station.transform,
                new Vector3(-5f, 0.04f, -0.25f),
                new Vector3(1.9f, 0.08f, 1.25f),
                concrete,
                collider: false);
            CreateCube(
                "Store Control Pedestal",
                station.transform,
                new Vector3(-5f, 0.55f, 0f),
                new Vector3(0.55f, 1.1f, 0.55f),
                darkMetal);
            GameObject terminalObject = CreateCube(
                "Store Control Terminal",
                station.transform,
                new Vector3(-5f, 1.22f, 0f),
                new Vector3(1.65f, 0.72f, 0.18f),
                brandOrange);
            BoxCollider interactionCollider = terminalObject.GetComponent<BoxCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.center = new Vector3(0f, 0f, -2f);
            interactionCollider.size = new Vector3(1.35f, 2.8f, 5f);
            InteractionHighlight highlight = terminalObject.AddComponent<InteractionHighlight>();
            InteractionView interactionView = terminalObject.AddComponent<InteractionView>();
            interactionView.Configure(highlight);
            terminalObject.AddComponent<InteractionViewRegistrar>();
            SceneViewMarker marker = terminalObject.AddComponent<SceneViewMarker>();
            marker.Configure(SceneViewId.StoreControlTerminal);
            CreateWorldLabel(
                "Store Control Label",
                terminalObject.transform,
                LocalizationKey.WorldStoreControlTerminal,
                new Vector3(0f, 0f, -0.56f),
                Quaternion.identity,
                0.022f,
                Color.white);
            return marker;
        }

        private static (SceneViewMarker Terminal, SpawnPointMarker SpawnPoint)
            BuildTrolleyUpgradeArea(
                Transform parent,
                Material concrete,
                Material brandOrange,
                Material darkMetal)
        {
            GameObject station = CreateEmpty("Trolley Upgrade Station", parent);
            CreateCube(
                "Station Pad",
                station.transform,
                new Vector3(-3.75f, 0.08f, 3.25f),
                new Vector3(3.5f, 0.16f, 2.5f),
                concrete,
                collider: false);
            CreateCube(
                "Terminal Pedestal",
                station.transform,
                new Vector3(-4.75f, 0.55f, 3.85f),
                new Vector3(0.55f, 1.1f, 0.55f),
                darkMetal);

            GameObject terminalObject = CreateCube(
                "Trolley Upgrade Terminal",
                station.transform,
                new Vector3(-4.75f, 1.22f, 3.85f),
                new Vector3(1.55f, 0.72f, 0.18f),
                brandOrange);
            BoxCollider interactionCollider = terminalObject.GetComponent<BoxCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.center = new Vector3(0f, 0f, -2f);
            interactionCollider.size = new Vector3(1.3f, 2.8f, 5f);
            InteractionHighlight highlight = terminalObject.AddComponent<InteractionHighlight>();
            InteractionView interactionView = terminalObject.AddComponent<InteractionView>();
            interactionView.Configure(highlight);
            terminalObject.AddComponent<InteractionViewRegistrar>();
            SceneViewMarker terminal = terminalObject.AddComponent<SceneViewMarker>();
            terminal.Configure(SceneViewId.TrolleyUpgradeTerminal);
            CreateWorldLabel(
                "Trolley Upgrade Label",
                terminalObject.transform,
                LocalizationKey.WorldTrolleyUpgrade,
                new Vector3(0f, 0f, -0.56f),
                Quaternion.identity,
                0.025f,
                Color.white,
                200);

            GameObject spawnObject = CreateEmpty("Platform Trolley Spawn", station.transform);
            spawnObject.transform.SetPositionAndRotation(
                new Vector3(-2.8f, 0.01f, 3.25f),
                Quaternion.Euler(0f, 180f, 0f));
            SpawnPointMarker spawnPoint = spawnObject.AddComponent<SpawnPointMarker>();
            spawnPoint.Configure(SpawnPointId.PlatformTrolley);
            return (terminal, spawnPoint);
        }

        private static SceneViewMarker BuildMaterialsStorage(Transform parent, Material concrete,
            Material brandBlue, Material timber, Material darkMetal, Material brandOrange)
        {
            GameObject storage = CreateEmpty("Materials Storage", parent);
            GameObject storagePad = CreateCube(
                "Storage Pad", storage.transform, new Vector3(5f, 0.1f, 6.5f),
                new Vector3(7.5f, 0.2f, 6.5f), concrete);
            InteractionHighlight storageHighlight =
                storagePad.AddComponent<InteractionHighlight>();
            CreateCube("Roof", storage.transform, new Vector3(5f, 3.6f, 6.5f),
                new Vector3(7.8f, 0.22f, 6.8f), brandBlue);

            Vector3[] posts =
            {
                new(1.45f, 1.8f, 3.35f), new(8.55f, 1.8f, 3.35f),
                new(1.45f, 1.8f, 9.65f), new(8.55f, 1.8f, 9.65f)
            };
            foreach (Vector3 post in posts)
                CreateCube("Canopy Post", storage.transform, post, new Vector3(0.24f, 3.6f, 0.24f), darkMetal);

            for (int row = 0; row < 3; row++)
            {
                float z = 4.4f + row * 2.05f;
                CreateCube($"Pallet Beam {row + 1} A", storage.transform,
                    new Vector3(4.3f, 0.25f, z - 0.22f),
                    new Vector3(5.8f, 0.18f, 0.18f), timber);
                CreateCube($"Pallet Beam {row + 1} B", storage.transform,
                    new Vector3(4.3f, 0.25f, z + 0.22f),
                    new Vector3(5.8f, 0.18f, 0.18f), timber);
            }

            GameObject slotsRoot = CreateEmpty("Storage Slots", storage.transform);
            Transform[] slots = new Transform[StorageSlotCapacity];
            const int slotsPerRow = 3;
            for (int i = 0; i < slots.Length; i++)
            {
                int column = i % slotsPerRow;
                int row = i / slotsPerRow;
                GameObject slot = CreateEmpty($"Stock Slot {i + 1}", slotsRoot.transform);
                slot.transform.position = new Vector3(
                    2.4f + column * 1.9f,
                    0.55f,
                    4.4f + row * 2.05f);
                slots[i] = slot.transform;
            }

            GameObject target = CreateEmpty("Storage Intake Target", storage.transform);
            target.transform.position = new Vector3(5f, 1.8f, 6.5f);
            CreateStorageInteractionFace(
                "Storage Intake Front Trigger",
                target.transform,
                new Vector3(0f, 0f, -3.25f),
                new Vector3(7.5f, 3.4f, 0.2f));
            CreateStorageInteractionFace(
                "Storage Intake Back Trigger",
                target.transform,
                new Vector3(0f, 0f, 3.25f),
                new Vector3(7.5f, 3.4f, 0.2f));
            CreateStorageInteractionFace(
                "Storage Intake Left Trigger",
                target.transform,
                new Vector3(-3.75f, 0f, 0f),
                new Vector3(0.2f, 3.4f, 6.5f));
            CreateStorageInteractionFace(
                "Storage Intake Right Trigger",
                target.transform,
                new Vector3(3.75f, 0f, 0f),
                new Vector3(0.2f, 3.4f, 6.5f));
            target.AddComponent<NonOccludingInteractionProxy>();
            InteractionView storageView = target.AddComponent<InteractionView>();
            storageView.Configure(storageHighlight);
            target.AddComponent<InteractionViewRegistrar>();
            SlotsRegistrar slotsRegistrar = target.AddComponent<SlotsRegistrar>();
            slotsRegistrar.Configure(slots);
            SceneViewMarker storageMarker = target.AddComponent<SceneViewMarker>();
            storageMarker.Configure(SceneViewId.StorageZone);

            CreateWorldLabel("Materials Sign", storage.transform,
                LocalizationKey.WorldStorageCatalog,
                new Vector3(5f, 2.7f, 3.25f),
                Quaternion.identity, 0.035f, brandOrange.color);
            CreateWorldLabel("Storage Intake Label", storage.transform,
                LocalizationKey.WorldStorageIntake,
                new Vector3(5f, 2.2f, 3.24f), Quaternion.identity, 0.025f, Color.white);
            return storageMarker;
        }

        private static void CreateStorageInteractionFace(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size)
        {
            GameObject face = CreateEmpty(name, parent);
            face.transform.localPosition = localPosition;
            BoxCollider collider = face.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
        }

        private static SceneRouteMarker[] BuildCustomerRoutes(Transform parent, Material asphalt,
            Material white, Material loadingGreen)
        {
            GameObject traffic = CreateEmpty("Customer Vehicle Traffic", parent);
            CreateCube("Customer Parking Pad", traffic.transform, new Vector3(6f, 0.015f, -5.4f),
                new Vector3(4f, 0.03f, 9.8f), asphalt, false);
            CreateCube("Parking Stripe Left", traffic.transform, new Vector3(4.25f, 0.035f, -5.4f),
                new Vector3(0.12f, 0.04f, 9.2f), white, false);
            CreateCube("Parking Stripe Right", traffic.transform, new Vector3(7.75f, 0.035f, -5.4f),
                new Vector3(0.12f, 0.04f, 9.2f), white, false);
            CreateCube("Loading Stripe", traffic.transform, new Vector3(6f, 0.04f, -9.9f),
                new Vector3(3.5f, 0.05f, 0.18f), loadingGreen, false);
            CreateWorldLabel("Customer Loading Bay Label", traffic.transform,
                LocalizationKey.WorldCustomerLoadingBay,
                new Vector3(6f, 0.045f, -10.45f), Quaternion.Euler(90f, 0f, 0f),
                0.03f, loadingGreen.color);

            SceneRouteMarker arrival = CreateSceneRoute(
                "Customer Vehicle Arrival Route",
                traffic.transform,
                SceneRouteId.CustomerVehicleArrival,
                new[]
                {
                    new Pose(new Vector3(0f, 0.02f, -22f), Quaternion.identity),
                    new Pose(new Vector3(0f, 0.02f, -11.8f), Quaternion.identity),
                    new Pose(new Vector3(6f, 0.02f, -10f), Quaternion.Euler(0f, 55f, 0f)),
                    new Pose(new Vector3(6f, 0.02f, -3.5f), Quaternion.identity)
                });
            SceneRouteMarker departure = CreateSceneRoute(
                "Customer Vehicle Departure Route",
                traffic.transform,
                SceneRouteId.CustomerVehicleDeparture,
                new[]
                {
                    new Pose(new Vector3(6f, 0.02f, -3.5f), Quaternion.identity),
                    new Pose(new Vector3(6f, 0.02f, -10f), Quaternion.identity),
                    new Pose(new Vector3(0f, 0.02f, -11.8f), Quaternion.Euler(0f, 55f, 0f)),
                    new Pose(new Vector3(0f, 0.02f, -22f), Quaternion.identity)
                });

            SceneRouteMarker walkToCounter = CreateSceneRoute(
                "Customer Walk To Counter Route",
                traffic.transform,
                SceneRouteId.CustomerWalkToCounter,
                new[]
                {
                    new Pose(new Vector3(4.55f, 0.02f, -2.35f), Quaternion.Euler(0f, -90f, 0f)),
                    new Pose(new Vector3(1.8f, 0.02f, -1.85f), Quaternion.Euler(0f, -80f, 0f)),
                    new Pose(new Vector3(-4.4f, 0.02f, 0f), Quaternion.Euler(0f, -75f, 0f)),
                    new Pose(new Vector3(-7.25f, 0.02f, 0.55f), Quaternion.identity)
                });
            SceneRouteMarker walkToVehicle = CreateSceneRoute(
                "Customer Walk To Vehicle Route",
                traffic.transform,
                SceneRouteId.CustomerWalkToVehicle,
                new[]
                {
                    new Pose(new Vector3(-7.25f, 0.02f, 0.55f), Quaternion.identity),
                    new Pose(new Vector3(-4.4f, 0.02f, 0f), Quaternion.Euler(0f, 105f, 0f)),
                    new Pose(new Vector3(1.8f, 0.02f, -1.85f), Quaternion.Euler(0f, 105f, 0f)),
                    new Pose(new Vector3(4.55f, 0.02f, -2.35f), Quaternion.Euler(0f, 90f, 0f))
                });

            return new[] { arrival, departure, walkToCounter, walkToVehicle };
        }

        private static void BuildLumberArea(Transform parent, Material concrete, Material brandBlue,
            Material brandOrange, Material timber, Material darkMetal, ProductConfig boardProductConfig)
        {
            GameObject lumber = CreateEmpty("Lumber Display", parent);
            CreateCube("Lumber Pad", lumber.transform, new Vector3(12f, 0.08f, 6f),
                new Vector3(5.5f, 0.16f, 9f), concrete);
            CreateCube("Rack Left", lumber.transform, new Vector3(10f, 1.6f, 6f),
                new Vector3(0.2f, 3.2f, 8f), darkMetal);
            CreateCube("Rack Right", lumber.transform, new Vector3(14f, 1.6f, 6f),
                new Vector3(0.2f, 3.2f, 8f), darkMetal);
            CreateCube("Rack Roof", lumber.transform, new Vector3(12f, 3.25f, 6f),
                new Vector3(4.4f, 0.18f, 8.4f), brandBlue);

            for (int level = 0; level < 3; level++)
            {
                for (int board = 0; board < 4; board++)
                {
                    CreateCube($"Board {level + 1}-{board + 1}", lumber.transform,
                        new Vector3(10.65f + board * 0.9f, 0.55f + level * 0.75f, 6f),
                        new Vector3(0.72f, 0.18f, 6.6f), timber);
                }
            }

            CreateCube("Lumber Pick Face", lumber.transform, new Vector3(12f, 0.2f, 2.35f),
                new Vector3(3.5f, 0.08f, 0.9f), brandOrange, false);
            for (int bundle = 0; bundle < 3; bundle++)
            {
                GameObject displayBundle = CreateEmpty($"Board Display Bundle {bundle + 1}", lumber.transform);
                displayBundle.transform.position = new Vector3(10.65f + bundle * 1.35f, 0.42f, 2.35f);
                for (int board = 0; board < 4; board++)
                {
                    CreateCube($"Display Board {board + 1}", displayBundle.transform,
                        new Vector3(0f, board * 0.065f, 0f),
                        new Vector3(1.15f, 0.055f, 0.42f), timber, false, true);
                }
            }

            CreateWorldLabel("Lumber Display Sign", lumber.transform,
                LocalizationKey.WorldBoardProductLabel,
                new Vector3(12f, 2.7f, 1.68f), Quaternion.identity, 0.03f,
                brandOrange.color, boardProductConfig.UnitPrice);
        }

        private static SpawnPointMarker BuildInboundDeliveryBay(Transform parent, Material asphalt,
            Material inboundYellow)
        {
            GameObject bay = CreateEmpty("Inbound Delivery Bay", parent);
            CreateCube("Inbound Bay Surface", bay.transform, new Vector3(11f, 0.015f, -9f),
                new Vector3(5f, 0.03f, 7.5f), asphalt, false);

            for (int index = 0; index < 6; index++)
            {
                CreateCube($"Inbound Marking {index + 1}", bay.transform,
                    new Vector3(8.75f + index % 2 * 4.5f, 0.035f, -11.7f + index / 2 * 2.7f),
                    new Vector3(0.14f, 0.04f, 1.4f), inboundYellow, false);
            }

            CreateWorldLabel("Inbound Label", bay.transform,
                LocalizationKey.WorldDeliveryIntake,
                new Vector3(11f, 0.04f, -5.15f), Quaternion.Euler(90f, 0f, 0f),
                0.035f, inboundYellow.color);

            GameObject spawnPoint = CreateEmpty("Delivery Vehicle Spawn Point", bay.transform);
            spawnPoint.transform.SetPositionAndRotation(new Vector3(11f, 0.02f, -9f), Quaternion.identity);
            SpawnPointMarker marker = spawnPoint.AddComponent<SpawnPointMarker>();
            marker.Configure(SpawnPointId.DeliveryVehicle);
            return marker;
        }

        private static (SpawnPointMarker Idle, SpawnPointMarker DeliveryAccess,
                SpawnPointMarker StorageAccess)
            BuildWarehouseWorkerAccessPoints(Transform parent)
        {
            GameObject root = CreateEmpty("Warehouse Worker Access Points", parent);
            SpawnPointMarker idle = CreateSpawnPoint(
                "Warehouse Worker Idle",
                root.transform,
                SpawnPointId.WarehouseWorker,
                new Vector3(7.75f, 0.02f, 2.45f),
                Quaternion.Euler(0f, -90f, 0f));
            SpawnPointMarker deliveryAccess = CreateSpawnPoint(
                "Warehouse Worker Delivery Access",
                root.transform,
                SpawnPointId.WarehouseWorkerDeliveryAccess,
                new Vector3(9.15f, 0.02f, -9.85f),
                Quaternion.Euler(0f, 90f, 0f));
            SpawnPointMarker storageAccess = CreateSpawnPoint(
                "Warehouse Worker Storage Access",
                root.transform,
                SpawnPointId.WarehouseWorkerStorageAccess,
                new Vector3(5f, 0.02f, 2.45f),
                Quaternion.identity);
            return (idle, deliveryAccess, storageAccess);
        }

        private static SpawnPointMarker CreateSpawnPoint(
            string name,
            Transform parent,
            SpawnPointId id,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject spawnPoint = CreateEmpty(name, parent);
            spawnPoint.transform.SetPositionAndRotation(position, rotation);
            SpawnPointMarker marker = spawnPoint.AddComponent<SpawnPointMarker>();
            marker.Configure(id);
            return marker;
        }

        private static SpawnPointMarker BuildPlayerSpawnPoint()
        {
            GameObject spawnPoint = CreateEmpty("Player Spawn Point");
            spawnPoint.transform.SetPositionAndRotation(
                new Vector3(-9f, 0.02f, -2.1f),
                Quaternion.identity);
            SpawnPointMarker marker = spawnPoint.AddComponent<SpawnPointMarker>();
            marker.Configure(SpawnPointId.Player);
            return marker;
        }

        private static void EnsureCementProductPrefab(ProductConfig productConfig, Material cementMaterial)
        {
            GameObject product = GameObject.CreatePrimitive(PrimitiveType.Cube);
            product.name = "Cement Bag";

            try
            {
                product.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = new Vector3(0.84f, 0.28f, 0.48f);
                product.SetActive(true);
                product.GetComponent<Renderer>().sharedMaterial = cementMaterial;

                Rigidbody body = product.AddComponent<Rigidbody>();
                body.mass = productConfig.Mass;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = productConfig.WorldInterpolation;
                body.collisionDetectionMode = productConfig.WorldCollisionDetection;

                GameObject interactionArea = CreateEmpty("Interaction Area", product.transform);
                BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 0.35f, 0f);
                interactionCollider.size = new Vector3(1.4f, 2.5f, 1.8f);

                InteractionHighlight highlight = product.AddComponent<InteractionHighlight>();
                InteractionView interactionView = product.AddComponent<InteractionView>();
                interactionView.Configure(highlight);
                product.AddComponent<TransformRegistrar>();
                product.AddComponent<InteractionViewRegistrar>();
                product.AddComponent<RigidbodyRegistrar>();
                product.AddComponent<CollidersRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(product, CementProductPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create product prefab at {CementProductPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Product prefab at {CementProductPrefabPath} has no view root.");
                AssignViewPrefab(productConfig, prefabView);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void EnsureBoardProductPrefab(ProductConfig productConfig, Material timber,
            Material strapMaterial)
        {
            GameObject product = CreateEmpty("Board Bundle");

            try
            {
                product.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = Vector3.one;
                product.SetActive(true);

                Rigidbody body = product.AddComponent<Rigidbody>();
                body.mass = productConfig.Mass;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = productConfig.WorldInterpolation;
                body.collisionDetectionMode = productConfig.WorldCollisionDetection;

                BoxCollider solidCollider = product.AddComponent<BoxCollider>();
                solidCollider.center = new Vector3(0f, 0.12f, 0f);
                solidCollider.size = new Vector3(1.55f, 0.3f, 0.46f);

                GameObject visualRoot = CreateEmpty("Board Bundle Visual", product.transform);
                InteractionHighlight highlight = null;
                for (int index = 0; index < 4; index++)
                {
                    GameObject board = CreateCube(
                        $"Board {index + 1}",
                        visualRoot.transform,
                        new Vector3(index % 2 == 0 ? -0.025f : 0.025f, index * 0.065f, 0f),
                        new Vector3(1.52f, 0.055f, 0.4f),
                        timber,
                        false,
                        true);
                    if (index == 0)
                        highlight = board.AddComponent<InteractionHighlight>();
                }

                CreateCube("Left Strap", visualRoot.transform, new Vector3(-0.48f, 0.1f, 0f),
                    new Vector3(0.055f, 0.28f, 0.44f), strapMaterial, false, true);
                CreateCube("Right Strap", visualRoot.transform, new Vector3(0.48f, 0.1f, 0f),
                    new Vector3(0.055f, 0.28f, 0.44f), strapMaterial, false, true);

                GameObject interactionArea = CreateEmpty("Interaction Area", product.transform);
                BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 0.35f, 0f);
                interactionCollider.size = new Vector3(2.15f, 1.75f, 1.15f);

                InteractionView interactionView = product.AddComponent<InteractionView>();
                interactionView.Configure(highlight ?? throw new InvalidOperationException(
                    "Board bundle visual must provide an interaction highlight."));
                product.AddComponent<TransformRegistrar>();
                product.AddComponent<InteractionViewRegistrar>();
                product.AddComponent<RigidbodyRegistrar>();
                product.AddComponent<CollidersRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(product, BoardProductPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create product prefab at {BoardProductPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Product prefab at {BoardProductPrefabPath} has no view root.");
                AssignViewPrefab(productConfig, prefabView);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void EnsureDeliveryVehiclePrefab(IReadOnlyCollection<DeliveryConfig> deliveryConfigs,
            Material inboundYellow,
            Material darkMetal, Material glass, Material timber)
        {
            if (deliveryConfigs == null || deliveryConfigs.Count == 0)
                throw new ArgumentException("At least one delivery config is required.", nameof(deliveryConfigs));

            GameObject vehicle = CreateEmpty("Delivery Truck");

            try
            {
                vehicle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                vehicle.transform.localScale = Vector3.one;
                vehicle.SetActive(true);
                vehicle.AddComponent<EntityBehaviour>();
                vehicle.AddComponent<TransformRegistrar>();

                CreateCube("Cab", vehicle.transform, new Vector3(0f, 1.12f, 2.1f),
                    new Vector3(2.25f, 1.8f, 2.2f), inboundYellow, false, true);
                CreateCube("Hood", vehicle.transform, new Vector3(0f, 0.82f, 3.55f),
                    new Vector3(2.18f, 1.02f, 1.05f), inboundYellow, false, true);
                CreateCube("Windshield", vehicle.transform, new Vector3(0f, 1.55f, 2.78f),
                    new Vector3(1.8f, 0.65f, 0.08f), glass, false, true);
                CreateCube("Bed Floor", vehicle.transform, new Vector3(0f, 0.86f, -0.85f),
                    new Vector3(2.3f, 0.22f, 3.7f), darkMetal, false, true);
                CreateCube("Bed Left Rail", vehicle.transform, new Vector3(-1.1f, 1.28f, -0.85f),
                    new Vector3(0.16f, 0.76f, 3.7f), inboundYellow, false, true);
                CreateCube("Bed Right Rail", vehicle.transform, new Vector3(1.1f, 1.28f, -0.85f),
                    new Vector3(0.16f, 0.76f, 3.7f), inboundYellow, false, true);
                CreateCube("Cargo Pallet", vehicle.transform, new Vector3(0f, 1.08f, -0.85f),
                    new Vector3(1.95f, 0.16f, 2.75f), timber, false, true);

                CreateLocalWheel("Front Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.55f, 2.5f),
                    darkMetal);
                CreateLocalWheel("Front Right Wheel", vehicle.transform, new Vector3(1.12f, 0.55f, 2.5f),
                    darkMetal);
                CreateLocalWheel("Rear Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.55f, -1.35f),
                    darkMetal);
                CreateLocalWheel("Rear Right Wheel", vehicle.transform, new Vector3(1.12f, 0.55f, -1.35f),
                    darkMetal);

                GameObject slotsRoot = CreateEmpty("Cargo Slots", vehicle.transform);
                int cargoSlotCapacity = deliveryConfigs.Max(config => config.ProductCount);
                Transform[] cargoSlots = new Transform[cargoSlotCapacity];
                for (int index = 0; index < cargoSlots.Length; index++)
                {
                    GameObject slot = CreateEmpty($"Cargo Slot {index + 1}", slotsRoot.transform);
                    slot.transform.localPosition = new Vector3(0f, 1.28f, -1.8f + index * 0.92f);
                    cargoSlots[index] = slot.transform;
                }

                SlotsRegistrar slotsRegistrar = vehicle.AddComponent<SlotsRegistrar>();
                slotsRegistrar.Configure(cargoSlots);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(vehicle, DeliveryVehiclePrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create delivery vehicle prefab at {DeliveryVehiclePrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Delivery vehicle prefab at {DeliveryVehiclePrefabPath} " +
                                                 "has no EntityBehaviour root.");
                foreach (DeliveryConfig deliveryConfig in deliveryConfigs)
                    AssignViewPrefab(deliveryConfig, prefabView);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        private static void EnsureCustomerVehiclePrefab(CustomerVehicleConfig config,
            Material truckPaint, Material darkMetal, Material glass, Material loadingGreen)
        {
            GameObject vehicle = CreateEmpty("Customer Vehicle");

            try
            {
                vehicle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                vehicle.transform.localScale = Vector3.one;
                vehicle.SetActive(true);

                Rigidbody body = vehicle.AddComponent<Rigidbody>();
                body.mass = 1400f;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                CreateCube("Cab", vehicle.transform, new Vector3(0f, 1.12f, 1.05f),
                    new Vector3(2.25f, 1.8f, 2.3f), truckPaint, false, true);
                CreateCube("Hood", vehicle.transform, new Vector3(0f, 0.82f, 2.58f),
                    new Vector3(2.18f, 1.02f, 1.1f), truckPaint, false, true);
                CreateCube("Windshield", vehicle.transform, new Vector3(0f, 1.55f, 1.82f),
                    new Vector3(1.8f, 0.65f, 0.08f), glass, false, true);
                CreateCube("Bed Floor", vehicle.transform, new Vector3(0f, 0.86f, -1.55f),
                    new Vector3(2.3f, 0.22f, 3.7f), darkMetal, false, true);
                CreateCube("Bed Left Rail", vehicle.transform, new Vector3(-1.1f, 1.28f, -1.55f),
                    new Vector3(0.16f, 0.76f, 3.7f), truckPaint, false, true);
                CreateCube("Bed Right Rail", vehicle.transform, new Vector3(1.1f, 1.28f, -1.55f),
                    new Vector3(0.16f, 0.76f, 3.7f), truckPaint, false, true);

                CreateLocalWheel("Front Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.55f, 1.6f),
                    darkMetal);
                CreateLocalWheel("Front Right Wheel", vehicle.transform, new Vector3(1.12f, 0.55f, 1.6f),
                    darkMetal);
                CreateLocalWheel("Rear Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.55f, -2.05f),
                    darkMetal);
                CreateLocalWheel("Rear Right Wheel", vehicle.transform, new Vector3(1.12f, 0.55f, -2.05f),
                    darkMetal);

                GameObject bodyColliderObject = CreateEmpty("Body Collider", vehicle.transform);
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer < 0)
                    throw new InvalidOperationException("Required Ignore Raycast layer is missing.");
                bodyColliderObject.layer = ignoreRaycastLayer;
                BoxCollider bodyCollider = bodyColliderObject.AddComponent<BoxCollider>();
                bodyCollider.center = new Vector3(0f, 1f, -0.15f);
                bodyCollider.size = new Vector3(2.3f, 2f, 6.1f);

                GameObject loadingTarget = CreateCube("Loading Target", vehicle.transform,
                    new Vector3(0f, 1f, -3.46f), new Vector3(1.85f, 0.32f, 0.1f),
                    loadingGreen, false, true);
                InteractionHighlight highlight = loadingTarget.AddComponent<InteractionHighlight>();
                CreateWorldLabel("Loading Label", loadingTarget.transform,
                    LocalizationKey.WorldCustomerVehicleLoading,
                    new Vector3(0f, 0f, -0.56f), Quaternion.identity, 0.02f, Color.white);

                GameObject interactionArea = CreateEmpty("Interaction Area", vehicle.transform);
                interactionArea.transform.localPosition = new Vector3(0f, 1.35f, -3.75f);
                BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 0f, -0.65f);
                interactionCollider.size = new Vector3(3.2f, 3f, 2.6f);

                GameObject slotsRoot = CreateEmpty("Customer Cargo Slots", vehicle.transform);
                Transform[] slots = new Transform[CustomerVehicleCargoCapacity];
                for (int index = 0; index < slots.Length; index++)
                {
                    GameObject slot = CreateEmpty($"Cargo Slot {index + 1}", slotsRoot.transform);
                    slot.transform.localPosition = new Vector3(0f, 1.16f, -2.25f + index * 0.92f);
                    slots[index] = slot.transform;
                }

                InteractionView view = vehicle.AddComponent<InteractionView>();
                view.Configure(highlight);
                vehicle.AddComponent<TransformRegistrar>();
                vehicle.AddComponent<InteractionViewRegistrar>();
                vehicle.AddComponent<RigidbodyRegistrar>();
                vehicle.AddComponent<CollidersRegistrar>();
                SlotsRegistrar slotsRegistrar = vehicle.AddComponent<SlotsRegistrar>();
                slotsRegistrar.Configure(slots);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(vehicle, CustomerVehiclePrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create customer vehicle prefab at {CustomerVehiclePrefabPath}.");
                }

                InteractionView prefabView = prefab.GetComponent<InteractionView>() ??
                                             throw new InvalidOperationException(
                                                 $"Customer vehicle prefab at {CustomerVehiclePrefabPath} " +
                                                 "has no InteractionView root.");
                config.Configure(
                    prefabView,
                    arrivalSpeed: 4f,
                    departureSpeed: 5.25f,
                    rotationSpeed: 135f,
                    waypointTolerance: 0.08f,
                    completedDwellDuration: 1.25f,
                    firstCustomerDelay: 1f,
                    nextCustomerDelay: 4f,
                    cargoCapacity: CustomerVehicleCargoCapacity);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        private static void EnsureCustomerPrefab(CustomerConfig config, Material jacket,
            Material workwear, Material shoes)
        {
            GameObject customer = CreateEmpty("Customer");

            try
            {
                customer.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                customer.transform.localScale = Vector3.one;
                customer.SetActive(true);

                Rigidbody body = customer.AddComponent<Rigidbody>();
                body.mass = 80f;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                CreateCube("Torso", customer.transform, new Vector3(0f, 1.18f, 0f),
                    new Vector3(0.62f, 0.78f, 0.34f), jacket, false, true);
                CreateCube("Work Vest", customer.transform, new Vector3(0f, 1.2f, -0.18f),
                    new Vector3(0.66f, 0.54f, 0.05f), workwear, false, true);
                CreateCube("Head", customer.transform, new Vector3(0f, 1.82f, 0f),
                    new Vector3(0.38f, 0.38f, 0.38f), jacket, false, true);
                CreateCube("Left Arm", customer.transform, new Vector3(-0.42f, 1.18f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), jacket, false, true);
                CreateCube("Right Arm", customer.transform, new Vector3(0.42f, 1.18f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), jacket, false, true);
                CreateCube("Left Leg", customer.transform, new Vector3(-0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                CreateCube("Right Leg", customer.transform, new Vector3(0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                CreateCube("Left Shoe", customer.transform, new Vector3(-0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);
                CreateCube("Right Shoe", customer.transform, new Vector3(0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);

                customer.AddComponent<EntityBehaviour>();
                customer.AddComponent<TransformRegistrar>();
                customer.AddComponent<RigidbodyRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(customer, CustomerPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create customer prefab at {CustomerPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Customer prefab at {CustomerPrefabPath} " +
                                                 "has no EntityBehaviour root.");
                config.Configure(
                    prefabView,
                    movementSpeed: 2.4f,
                    rotationSpeed: 360f,
                    waypointTolerance: 0.08f);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(customer);
            }
        }

        private static void EnsureWarehouseWorkerPrefab(
            WarehouseWorkerConfig config,
            Material skin,
            Material workwear,
            Material safetyYellow,
            Material shoes)
        {
            GameObject worker = CreateEmpty("Warehouse Worker");

            try
            {
                worker.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                worker.transform.localScale = Vector3.one;
                worker.SetActive(true);

                NavMeshAgent agent = worker.AddComponent<NavMeshAgent>();
                agent.agentTypeID = 0;
                agent.radius = 0.32f;
                agent.height = 1.9f;
                agent.baseOffset = 0f;
                agent.speed = config.MovementSpeed;
                agent.acceleration = config.Acceleration;
                agent.angularSpeed = config.AngularSpeed;
                agent.stoppingDistance = config.StoppingDistance;
                agent.autoBraking = true;
                agent.autoRepath = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                CreateCube("Torso", worker.transform, new Vector3(0f, 1.18f, 0f),
                    new Vector3(0.62f, 0.78f, 0.34f), workwear, false, true);
                CreateCube("Safety Vest Front", worker.transform, new Vector3(0f, 1.2f, 0.18f),
                    new Vector3(0.66f, 0.56f, 0.055f), safetyYellow, false, true);
                CreateCube("Safety Vest Back", worker.transform, new Vector3(0f, 1.2f, -0.18f),
                    new Vector3(0.66f, 0.56f, 0.055f), safetyYellow, false, true);
                CreateCube("Head", worker.transform, new Vector3(0f, 1.82f, 0f),
                    new Vector3(0.38f, 0.38f, 0.38f), skin, false, true);
                CreateCube("Hard Hat", worker.transform, new Vector3(0f, 2.06f, 0f),
                    new Vector3(0.48f, 0.16f, 0.42f), safetyYellow, false, true);
                CreateCube("Hard Hat Brim", worker.transform, new Vector3(0f, 1.99f, 0.14f),
                    new Vector3(0.56f, 0.055f, 0.22f), safetyYellow, false, true);
                CreateCube("Left Arm", worker.transform, new Vector3(-0.42f, 1.18f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), workwear, false, true);
                CreateCube("Right Arm", worker.transform, new Vector3(0.42f, 1.18f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), workwear, false, true);
                CreateCube("Left Hand", worker.transform, new Vector3(-0.42f, 0.78f, 0f),
                    new Vector3(0.17f, 0.18f, 0.19f), skin, false, true);
                CreateCube("Right Hand", worker.transform, new Vector3(0.42f, 0.78f, 0f),
                    new Vector3(0.17f, 0.18f, 0.19f), skin, false, true);
                CreateCube("Left Leg", worker.transform, new Vector3(-0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                CreateCube("Right Leg", worker.transform, new Vector3(0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                CreateCube("Left Shoe", worker.transform, new Vector3(-0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);
                CreateCube("Right Shoe", worker.transform, new Vector3(0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);

                worker.AddComponent<EntityBehaviour>();
                worker.AddComponent<TransformRegistrar>();
                worker.AddComponent<NavMeshAgentRegistrar>();
                GameObject carryAnchor = CreateEmpty("Carry Anchor", worker.transform);
                carryAnchor.transform.localPosition = new Vector3(0f, 1.02f, 0.66f);
                carryAnchor.AddComponent<CarryAnchorRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    worker,
                    WarehouseWorkerPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create warehouse worker prefab at {WarehouseWorkerPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Warehouse worker prefab at " +
                                                 $"{WarehouseWorkerPrefabPath} has no view root.");
                AssignViewPrefab(config, prefabView);
            }
            finally
            {
                Object.DestroyImmediate(worker);
            }
        }

        private static void EnsurePlatformTrolleyPrefab(
            PlatformTrolleyConfig config,
            Material brandOrange,
            Material darkMetal,
            Material timber)
        {
            GameObject trolley = CreateEmpty("Platform Trolley");

            try
            {
                trolley.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                trolley.transform.localScale = Vector3.one;
                trolley.SetActive(true);

                Rigidbody body = trolley.AddComponent<Rigidbody>();
                body.mass = 45f;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                GameObject deck = CreateCube(
                    "Deck", trolley.transform, new Vector3(0f, 0.42f, 0f),
                    new Vector3(1.9f, 0.18f, 2.4f), brandOrange, false, true);
                InteractionHighlight highlight = deck.AddComponent<InteractionHighlight>();
                CreateCube(
                    "Deck Inlay", trolley.transform, new Vector3(0f, 0.53f, 0f),
                    new Vector3(1.62f, 0.05f, 2.08f), timber, false, true);
                CreateCube(
                    "Left Rail", trolley.transform, new Vector3(-0.9f, 0.68f, 0f),
                    new Vector3(0.1f, 0.52f, 2.35f), darkMetal, false, true);
                CreateCube(
                    "Right Rail", trolley.transform, new Vector3(0.9f, 0.68f, 0f),
                    new Vector3(0.1f, 0.52f, 2.35f), darkMetal, false, true);
                CreateCube(
                    "Left Handle Upright", trolley.transform,
                    new Vector3(-0.72f, 1.12f, -1.12f),
                    new Vector3(0.1f, 1.35f, 0.1f), darkMetal, false, true);
                CreateCube(
                    "Right Handle Upright", trolley.transform,
                    new Vector3(0.72f, 1.12f, -1.12f),
                    new Vector3(0.1f, 1.35f, 0.1f), darkMetal, false, true);
                CreateCube(
                    "Handle", trolley.transform, new Vector3(0f, 1.76f, -1.12f),
                    new Vector3(1.55f, 0.12f, 0.12f), darkMetal, false, true);

                CreateLocalWheel(
                    "Front Left Wheel", trolley.transform,
                    new Vector3(-0.82f, 0.23f, 0.76f), darkMetal);
                CreateLocalWheel(
                    "Front Right Wheel", trolley.transform,
                    new Vector3(0.82f, 0.23f, 0.76f), darkMetal);
                CreateLocalWheel(
                    "Rear Left Wheel", trolley.transform,
                    new Vector3(-0.82f, 0.23f, -0.76f), darkMetal);
                CreateLocalWheel(
                    "Rear Right Wheel", trolley.transform,
                    new Vector3(0.82f, 0.23f, -0.76f), darkMetal);

                GameObject bodyColliderObject = CreateEmpty("Body Collider", trolley.transform);
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer < 0)
                    throw new InvalidOperationException("Required Ignore Raycast layer is missing.");
                bodyColliderObject.layer = ignoreRaycastLayer;
                BoxCollider bodyCollider = bodyColliderObject.AddComponent<BoxCollider>();
                bodyCollider.center = new Vector3(0f, 0.27f, 0.15f);
                bodyCollider.size = new Vector3(2f, 0.5f, 2.1f);

                GameObject interactionArea = CreateEmpty("Interaction Area", trolley.transform);
                BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 1.76f, -1.12f);
                interactionCollider.size = new Vector3(1.8f, 0.35f, 0.3f);

                GameObject slotsRoot = CreateEmpty("Cargo Slots", trolley.transform);
                var slots = new Transform[3];
                for (int index = 0; index < slots.Length; index++)
                {
                    GameObject slot = CreateEmpty($"Cargo Slot {index + 1}", slotsRoot.transform);
                    slot.transform.localPosition =
                        new Vector3(0f, 0.66f, -0.66f + index * 0.66f);
                    slots[index] = slot.transform;
                }

                InteractionView view = trolley.AddComponent<InteractionView>();
                view.Configure(highlight);
                trolley.AddComponent<TransformRegistrar>();
                trolley.AddComponent<RigidbodyRegistrar>();
                trolley.AddComponent<InteractionViewRegistrar>();
                trolley.AddComponent<CollidersRegistrar>();
                SlotsRegistrar slotsRegistrar = trolley.AddComponent<SlotsRegistrar>();
                slotsRegistrar.Configure(slots);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    trolley, PlatformTrolleyPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create platform trolley prefab at {PlatformTrolleyPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Platform trolley prefab at " +
                                                 $"{PlatformTrolleyPrefabPath} has no view root.");
                config.Configure(
                    prefabView,
                    purchasePrice: 200,
                    requiredCompletedOrderCount: 2,
                    capacity: 3,
                    movementSpeed: 3.8f,
                    followDistance: 1.7f);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(trolley);
            }
        }

        private static void EnsurePlayerPrefab(PlayerConfig playerConfig)
        {
            GameObject player = CreateEmpty("Player");

            try
            {
                player.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                player.transform.localScale = Vector3.one;
                player.SetActive(true);

                CharacterController controller = player.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.32f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.stepOffset = 0.32f;
                controller.slopeLimit = 48f;

                player.AddComponent<EntityBehaviour>();
                player.AddComponent<TransformRegistrar>();
                player.AddComponent<CharacterControllerRegistrar>();

                GameObject pivot = CreateEmpty("View Pivot", player.transform);
                pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                pivot.AddComponent<ViewPivotRegistrar>();
                GameObject cameraObject = CreateEmpty("Main Camera", pivot.transform);
                cameraObject.tag = "MainCamera";
                Camera viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.fieldOfView = 72f;
                viewCamera.nearClipPlane = 0.05f;
                viewCamera.farClipPlane = 140f;
                viewCamera.backgroundColor = new Color(0.43f, 0.57f, 0.68f);
                cameraObject.AddComponent<AudioListener>();
                cameraObject.AddComponent<CameraRegistrar>();

                GameObject carryAnchor = CreateEmpty("Carry Anchor", cameraObject.transform);
                carryAnchor.transform.localPosition = new Vector3(0f, -0.46f, 1.08f);
                carryAnchor.AddComponent<CarryAnchorRegistrar>();
                GameObject dropOrigin = CreateEmpty("Drop Origin", cameraObject.transform);
                dropOrigin.transform.localPosition = new Vector3(0f, -0.18f, 0.22f);
                dropOrigin.AddComponent<DropOriginRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException($"Could not create player prefab at {PlayerPrefabPath}.");

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Player prefab at {PlayerPrefabPath} has no EntityBehaviour root.");
                SerializedObject serializedConfig = new(playerConfig);
                SerializedProperty viewPrefab = serializedConfig.FindProperty("_viewPrefab") ??
                                                throw new InvalidOperationException(
                                                    $"{nameof(PlayerConfig)} must declare _viewPrefab.");
                viewPrefab.objectReferenceValue = prefabView;
                serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerConfig);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness,
            bool emission = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", smoothness > 0.45f ? 0.35f : 0f);

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.45f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateEmpty(string name, Transform parent = null)
        {
            GameObject gameObject = new(name);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static SceneRouteMarker CreateSceneRoute(string name, Transform parent, SceneRouteId id,
            Pose[] poses)
        {
            if (poses == null || poses.Length == 0)
                throw new ArgumentException("A scene route must contain at least one pose.", nameof(poses));

            GameObject routeObject = CreateEmpty(name, parent);
            var waypoints = new Transform[poses.Length];
            for (int index = 0; index < poses.Length; index++)
            {
                GameObject waypoint = CreateEmpty($"Waypoint {index + 1}", routeObject.transform);
                waypoint.transform.SetPositionAndRotation(poses[index].position, poses[index].rotation);
                waypoints[index] = waypoint.transform;
            }

            SceneRouteMarker marker = routeObject.AddComponent<SceneRouteMarker>();
            marker.Configure(id, waypoints);
            return marker;
        }

        private static void BakeAndValidateWarehouseWorkerNavigation(
            NavMeshSurface surface,
            params SpawnPointMarker[] accessPoints)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (accessPoints == null || accessPoints.Length != 3 || accessPoints.Any(point => point == null))
            {
                throw new ArgumentException(
                    "Warehouse worker navigation requires exactly three access points.",
                    nameof(accessPoints));
            }

            NavMeshData persistedData =
                AssetDatabase.LoadAssetAtPath<NavMeshData>(WarehouseWorkerNavMeshPath);
            surface.BuildNavMesh();
            NavMeshData bakedData = surface.navMeshData;
            if (bakedData == null)
                throw new InvalidOperationException("Warehouse worker NavMesh bake produced no data.");

            EnsureFolder("Assets/Scenes/Prototype_Yard");
            if (persistedData == null)
            {
                bakedData.name = WarehouseWorkerNavMeshAssetName;
                AssetDatabase.CreateAsset(bakedData, WarehouseWorkerNavMeshPath);
            }
            else
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(bakedData, persistedData);
                persistedData.name = WarehouseWorkerNavMeshAssetName;
                EditorUtility.SetDirty(persistedData);
                Object.DestroyImmediate(bakedData);
                surface.navMeshData = persistedData;
                surface.AddData();
            }

            EditorUtility.SetDirty(surface);

            var sampledPositions = new Vector3[accessPoints.Length];
            for (int index = 0; index < accessPoints.Length; index++)
            {
                Vector3 point = accessPoints[index].transform.position;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker access point '{accessPoints[index].name}' " +
                        $"at {point} is not on the baked NavMesh.");
                }

                sampledPositions[index] = hit.position;
            }

            for (int originIndex = 0; originIndex < sampledPositions.Length; originIndex++)
            {
                for (int destinationIndex = 0;
                     destinationIndex < sampledPositions.Length;
                     destinationIndex++)
                {
                    if (originIndex == destinationIndex)
                        continue;

                    var path = new NavMeshPath();
                    bool pathFound = NavMesh.CalculatePath(
                        sampledPositions[originIndex],
                        sampledPositions[destinationIndex],
                        NavMesh.AllAreas,
                        path);
                    if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
                    {
                        throw new InvalidOperationException(
                            $"Warehouse worker NavMesh path is incomplete from " +
                            $"'{accessPoints[originIndex].name}' to " +
                            $"'{accessPoints[destinationIndex].name}'.");
                    }
                }
            }
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = true, bool useLocalSpace = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, true);

            if (useLocalSpace)
                cube.transform.localPosition = position;
            else
                cube.transform.position = position;

            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;

            if (!collider)
                Object.DestroyImmediate(cube.GetComponent<Collider>());

            return cube;
        }

        private static void CreateLocalWheel(string name, Transform parent, Vector3 localPosition,
            Material material)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, 0f, 90f));
            wheel.transform.localScale = new Vector3(0.48f, 0.19f, 0.48f);
            wheel.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
        }

        private static Light CreatePointLight(string name, Transform parent, Vector3 position, Color color,
            float range, float intensity)
        {
            GameObject lightObject = CreateEmpty(name, parent);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void CreateWorldLabel(string name, Transform parent,
            LocalizationKey key, Vector3 position, Quaternion rotation,
            float characterSize, Color color, params int[] numberArguments)
        {
            GameObject labelObject = CreateEmpty(name, parent);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = rotation;
            Vector3 parentScale = parent.lossyScale;
            labelObject.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            var arguments = new LocalizationArgument[numberArguments.Length];
            for (int index = 0; index < arguments.Length; index++)
                arguments[index] = numberArguments[index];
            label.text = RussianPreviewLocalization.Resolve(
                LocalizedTexts.Text(key, arguments));
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 64;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            LocalizedTextMeshView localizedView =
                labelObject.AddComponent<LocalizedTextMeshView>();
            localizedView.Configure(label, key, numberArguments);
        }

        private static ILocalizationService CreateRussianPreviewLocalization()
        {
            var localization = new LocalizationService(new ILocalizationCatalog[]
            {
                new RussianLocalizationCatalog()
            });
            localization.Load(LanguageId.Russian);
            return localization;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            string parent = path[..slashIndex];
            string folderName = path[(slashIndex + 1)..];
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void EnsureProjectContextPrefab()
        {
            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectContextPath) != null;
            GameObject root = prefabExists
                ? PrefabUtility.LoadPrefabContents(ProjectContextPath)
                : new GameObject("ProjectContext");

            try
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                ProjectContext context = root.GetComponent<ProjectContext>() ?? root.AddComponent<ProjectContext>();
                BootstrapInstaller installer = root.GetComponent<BootstrapInstaller>() ??
                                               root.AddComponent<BootstrapInstaller>();
                context.Installers = new MonoInstaller[] { installer };

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectContextPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create Zenject ProjectContext at {ProjectContextPath}.");
                }
            }
            finally
            {
                if (prefabExists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        private static void EnsureConfigAssets()
        {
            EnsureConfigAsset<PlayerConfig>("PlayerConfig");
            EnsureConfigAsset<InteractionConfig>("InteractionConfig");
            DeleteLegacyConfigAsset(LegacyCementOrderConfigName);
            DeleteLegacyConfigAsset(LegacyBoardOrderConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(CementProjectConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(LumberProjectConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(WorkbenchProjectConfigName);
            EnsureConfigAsset<ProductConfig>(CementProductConfigName);
            EnsureConfigAsset<ProductConfig>(BoardProductConfigName);
            EnsureConfigAsset<DeliveryConfig>(CementDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(BoardDeliveryConfigName);
            EnsureConfigAsset<CustomerVehicleConfig>("CustomerVehicleConfig");
            EnsureConfigAsset<CustomerConfig>("CustomerConfig");
            EnsureConfigAsset<EconomyConfig>("EconomyConfig");
            EnsureConfigAsset<ProductRecoveryConfig>(ProductRecoveryConfigName);
            EnsureConfigAsset<PlatformTrolleyConfig>(PlatformTrolleyConfigName);
            EnsureConfigAsset<WarehouseWorkerConfig>(WarehouseWorkerConfigName);
            EnsureConfigAsset<StoreDayConfig>(StoreDayConfigName);
        }

        private static void ConfigurePrototypeConfigs(
            DeliveryConfig cementDeliveryConfig,
            DeliveryConfig boardDeliveryConfig,
            EconomyConfig economyConfig,
            ProductRecoveryConfig productRecoveryConfig,
            StoreDayConfig storeDayConfig,
            WarehouseWorkerConfig warehouseWorkerConfig,
            ProductConfig cementProductConfig,
            ProductConfig boardProductConfig,
            CustomerProjectConfig cementProjectConfig,
            CustomerProjectConfig lumberProjectConfig,
            CustomerProjectConfig workbenchProjectConfig)
        {
            ConfigureDeliveryConfig(
                cementDeliveryConfig,
                ProductTypeId.CementBag,
                productCount: 3,
                purchaseUnitPrice: 200);
            ConfigureDeliveryConfig(
                boardDeliveryConfig,
                ProductTypeId.BoardBundle,
                productCount: 3,
                purchaseUnitPrice: 260);

            SerializedObject economy = new(economyConfig);
            RequireSerializedProperty(economy, "_initialMoney").intValue = 1100;
            economy.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economyConfig);

            SerializedObject productRecovery = new(productRecoveryConfig);
            RequireSerializedProperty(productRecovery, "_minimumWorldY").floatValue = -10f;
            productRecovery.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(productRecoveryConfig);

            SerializedObject storeDay = new(storeDayConfig);
            RequireSerializedProperty(storeDay, "_startMinute").intValue = 8 * 60;
            RequireSerializedProperty(storeDay, "_closingMinute").intValue = 20 * 60;
            RequireSerializedProperty(storeDay, "_dayDurationSeconds").floatValue = 480f;
            storeDay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(storeDayConfig);

            SerializedObject warehouseWorker = new(warehouseWorkerConfig);
            RequireSerializedProperty(warehouseWorker, "_requiredCompletedOrderCount").intValue = 4;
            RequireSerializedProperty(warehouseWorker, "_hirePrice").intValue = 400;
            RequireSerializedProperty(warehouseWorker, "_dailyWage").intValue = 100;
            RequireSerializedProperty(warehouseWorker, "_movementSpeed").floatValue = 2.8f;
            RequireSerializedProperty(warehouseWorker, "_acceleration").floatValue = 12f;
            RequireSerializedProperty(warehouseWorker, "_angularSpeed").floatValue = 720f;
            RequireSerializedProperty(warehouseWorker, "_stoppingDistance").floatValue = 0.2f;
            RequireSerializedProperty(warehouseWorker, "_navigationSampleRadius").floatValue = 2f;
            RequireSerializedProperty(warehouseWorker, "_taskTimeout").floatValue = 20f;
            warehouseWorker.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(warehouseWorkerConfig);

            ConfigureProductConfig(
                cementProductConfig,
                ProductTypeId.CementBag,
                unitPrice: 350,
                mass: 25f,
                carryMovementSpeed: 3.2f,
                heldRotationEuler: new Vector3(8f, 0f, 0f),
                dropForwardDistance: 1.15f,
                productDropCollisionRadius: 0.51f);
            ConfigureProductConfig(
                boardProductConfig,
                ProductTypeId.BoardBundle,
                unitPrice: 480,
                mass: 18f,
                carryMovementSpeed: 2.6f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.35f,
                productDropCollisionRadius: 0.86f);

            ConfigureSingleProductProject(
                cementProjectConfig,
                CustomerProjectTypeId.CementFoundation,
                ProductTypeId.CementBag);
            ConfigureSingleProductProject(
                lumberProjectConfig,
                CustomerProjectTypeId.LumberShelving,
                ProductTypeId.BoardBundle,
                defaultOfferIndex: 1);
            ConfigureWorkbenchProject(workbenchProjectConfig);
        }

        private static void ConfigureDeliveryConfig(DeliveryConfig config, ProductTypeId productType,
            int productCount, int purchaseUnitPrice)
        {
            SerializedObject delivery = new(config);
            RequireSerializedProperty(delivery, "_productType").intValue = (int)productType;
            RequireSerializedProperty(delivery, "_productCount").intValue = productCount;
            RequireSerializedProperty(delivery, "_purchaseUnitPrice").intValue = purchaseUnitPrice;
            delivery.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureProductConfig(ProductConfig config, ProductTypeId productType,
            int unitPrice, float mass, float carryMovementSpeed,
            Vector3 heldRotationEuler, float dropForwardDistance,
            float productDropCollisionRadius)
        {
            SerializedObject product = new(config);
            RequireSerializedProperty(product, "_productType").intValue = (int)productType;
            RequireSerializedProperty(product, "_unitPrice").intValue = unitPrice;
            RequireSerializedProperty(product, "_mass").floatValue = mass;
            RequireSerializedProperty(product, "_carryMovementSpeed").floatValue = carryMovementSpeed;
            RequireSerializedProperty(product, "_heldRotationEuler").vector3Value = heldRotationEuler;
            RequireSerializedProperty(product, "_dropForwardDistance").floatValue = dropForwardDistance;
            RequireSerializedProperty(product, "_productDropCollisionRadius").floatValue =
                productDropCollisionRadius;
            RequireSerializedProperty(product, "_worldInterpolation").intValue =
                (int)RigidbodyInterpolation.Interpolate;
            RequireSerializedProperty(product, "_worldCollisionDetection").intValue =
                (int)CollisionDetectionMode.ContinuousSpeculative;
            product.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureSingleProductProject(
            CustomerProjectConfig config,
            CustomerProjectTypeId projectType,
            ProductTypeId productType,
            int defaultOfferIndex = 1)
        {
            config.Configure(
                projectType,
                defaultOfferIndex,
                offers: new[]
                {
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(productType, requiredCount: 1)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(productType, requiredCount: 2)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(productType, requiredCount: 3))
                });
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureWorkbenchProject(CustomerProjectConfig config)
        {
            config.Configure(
                CustomerProjectTypeId.WorkbenchFoundation,
                defaultOfferIndex: 1,
                offers: new[]
                {
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(ProductTypeId.CementBag, requiredCount: 1),
                        new CustomerProjectLineDefinition(ProductTypeId.BoardBundle, requiredCount: 1)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(ProductTypeId.CementBag, requiredCount: 2),
                        new CustomerProjectLineDefinition(ProductTypeId.BoardBundle, requiredCount: 1)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(ProductTypeId.CementBag, requiredCount: 1),
                        new CustomerProjectLineDefinition(ProductTypeId.BoardBundle, requiredCount: 2))
                });
            EditorUtility.SetDirty(config);
        }

        private static void AssignViewPrefab(ScriptableObject config, EntityBehaviour prefabView)
        {
            SerializedObject serializedConfig = new(config);
            RequireSerializedProperty(serializedConfig, "_viewPrefab").objectReferenceValue = prefabView;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static SerializedProperty RequireSerializedProperty(SerializedObject owner, string propertyName) =>
            owner.FindProperty(propertyName) ??
            throw new InvalidOperationException(
                $"{owner.targetObject.GetType().Name} must declare serialized property {propertyName}.");

        private static void EnsureConfigAsset<TConfig>(string assetName) where TConfig : ScriptableObject
        {
            string path = $"{ConfigFolder}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<TConfig>(path) != null)
                return;

            TConfig config = ScriptableObject.CreateInstance<TConfig>();
            config.name = assetName;
            AssetDatabase.CreateAsset(config, path);
        }

        private static void DeleteLegacyConfigAsset(string assetName)
        {
            string path = $"{ConfigFolder}/{assetName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                return;
            if (!AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Could not delete legacy config asset '{path}'.");
        }

        private static TConfig LoadConfig<TConfig>(string assetName) where TConfig : ScriptableObject =>
            AssetDatabase.LoadAssetAtPath<TConfig>($"{ConfigFolder}/{assetName}.asset") ??
            throw new InvalidOperationException($"Required config asset '{assetName}' was not created.");

        private static void PutSceneFirstInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(scenePath, true)
            };
            scenes.AddRange(EditorBuildSettings.scenes.Where(scene => scene.path != scenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

    }
}
