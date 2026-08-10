using System;
using System.Collections.Generic;
using System.Linq;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Registrars;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.Installers;
using HardwareStore.Infrastructure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string CementProductConfigName = "ProductConfig";
        private const string BoardProductConfigName = "ProductConfig_BoardBundle";
        private const string CementDeliveryConfigName = "DeliveryConfig";
        private const string BoardDeliveryConfigName = "DeliveryConfig_BoardBundle";
        private const string CementOrderConfigName = "OrderConfig";
        private const string BoardOrderConfigName = "OrderConfig_BoardBundle";
        private const int StorageSlotCapacity = 9;

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
            EconomyConfig economyConfig = LoadConfig<EconomyConfig>("EconomyConfig");
            ProductConfig cementProductConfig =
                LoadConfig<ProductConfig>(CementProductConfigName);
            ProductConfig boardProductConfig =
                LoadConfig<ProductConfig>(BoardProductConfigName);
            OrderConfig cementOrderConfig = LoadConfig<OrderConfig>(CementOrderConfigName);
            OrderConfig boardOrderConfig = LoadConfig<OrderConfig>(BoardOrderConfigName);
            ConfigurePrototypeConfigs(
                cementDeliveryConfig,
                boardDeliveryConfig,
                economyConfig,
                cementProductConfig,
                boardProductConfig,
                cementOrderConfig,
                boardOrderConfig);
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
                new[] { cementOrderConfig, boardOrderConfig },
                truckPaint,
                darkMetal,
                glass,
                loadingGreen);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();

            GameObject environment = CreateEmpty("Environment");
            BuildLighting(environment.transform);
            BuildYard(environment.transform, asphalt, concrete, brandBlue, white, yellow);
            (SceneViewMarker orderCounter, SceneViewMarker procurementTerminal) =
                BuildShop(environment.transform, concrete, brandBlue, brandOrange, darkMetal, glass);
            SceneViewMarker storageZone = BuildMaterialsStorage(
                environment.transform,
                concrete,
                brandBlue,
                timber,
                darkMetal,
                brandOrange,
                cementProductConfig,
                boardProductConfig);
            SceneRouteMarker[] customerVehicleRoutes = BuildCustomerVehicleRoutes(
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

            SpawnPointMarker playerSpawnPoint = BuildPlayerSpawnPoint();
            GameObject systems = CreateEmpty("SceneContext");
            SceneContext sceneContext = systems.AddComponent<SceneContext>();
            PrototypeAudioView audio = systems.AddComponent<PrototypeAudioView>();
            PrototypeHudView hud = systems.AddComponent<PrototypeHudView>();
            PrototypeSceneInitializer initializer = systems.AddComponent<PrototypeSceneInitializer>();
            initializer.Configure(
                new[] { playerSpawnPoint, deliveryVehicleSpawnPoint },
                customerVehicleRoutes,
                new[] { orderCounter, procurementTerminal, storageZone },
                hud,
                audio);
            SceneInitializationInstaller installer = systems.AddComponent<SceneInitializationInstaller>();
            installer.Configure(initializer);
            sceneContext.Installers = new MonoInstaller[] { installer };

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

        private static void BuildLighting(Transform parent)
        {
            GameObject sunObject = CreateEmpty("Sun", parent);
            sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.64f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;

            CreatePointLight("Shop Light", parent, new Vector3(-9f, 2.35f, 4.4f),
                new Color(1f, 0.61f, 0.32f), 6.5f, 520f);
            CreatePointLight("Warehouse Light", parent, new Vector3(5f, 3.25f, 6.2f),
                new Color(0.68f, 0.82f, 1f), 7.5f, 430f);
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

            GameObject customer = CreateCapsule("Customer", shop.transform, new Vector3(-9f, 1f, 3.05f),
                new Vector3(0.62f, 1f, 0.62f), brandOrange);
            Object.DestroyImmediate(customer.GetComponent<Collider>());
            CreateCube("Customer Vest", customer.transform, new Vector3(0f, 0.15f, 0f),
                new Vector3(1.05f, 0.8f, 1.02f), brandBlue, false, true);

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
            CreateWorldLabel("Orders Label", terminal.transform, "ЗАКАЗ КЛИЕНТА", new Vector3(0f, 0f, -0.56f),
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
            CreateWorldLabel("Procurement Label", procurementObject.transform, "ЗАКУПКИ",
                new Vector3(0f, 0f, -0.56f), Quaternion.identity, 0.03f, Color.white);

            CreateCube("Window", shop.transform, new Vector3(-9f, 2.2f, 8f), new Vector3(3.3f, 1.15f, 0.08f),
                glass, false);
            return (orderCounter, procurementTerminal);
        }

        private static SceneViewMarker BuildMaterialsStorage(Transform parent, Material concrete,
            Material brandBlue, Material timber, Material darkMetal, Material brandOrange,
            ProductConfig cementProductConfig, ProductConfig boardProductConfig)
        {
            GameObject storage = CreateEmpty("Materials Storage", parent);
            CreateCube("Storage Pad", storage.transform, new Vector3(5f, 0.1f, 6.5f),
                new Vector3(7.5f, 0.2f, 6.5f), concrete);
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

            GameObject target = CreateCube("Storage Intake Target", storage.transform,
                new Vector3(8.15f, 0.24f, 6.45f), new Vector3(1f, 0.12f, 3.4f), brandOrange);
            BoxCollider storageInteraction = target.GetComponent<BoxCollider>();
            storageInteraction.isTrigger = true;
            storageInteraction.center = new Vector3(0f, 5f, 0f);
            storageInteraction.size = new Vector3(1f, 10f, 1f);
            InteractionHighlight highlight = target.AddComponent<InteractionHighlight>();
            InteractionView storageView = target.AddComponent<InteractionView>();
            storageView.Configure(highlight);
            target.AddComponent<InteractionViewRegistrar>();
            SlotsRegistrar slotsRegistrar = target.AddComponent<SlotsRegistrar>();
            slotsRegistrar.Configure(slots);
            SceneViewMarker storageMarker = target.AddComponent<SceneViewMarker>();
            storageMarker.Configure(SceneViewId.StorageZone);

            CreateWorldLabel("Materials Sign", storage.transform,
                $"СКЛАД • {cementProductConfig.DisplayName.ToUpperInvariant()} • " +
                boardProductConfig.DisplayName.ToUpperInvariant(),
                new Vector3(5f, 2.7f, 3.25f),
                Quaternion.identity, 0.035f, brandOrange.color);
            CreateWorldLabel("Storage Intake Label", target.transform, "ПРИЁМКА",
                new Vector3(0f, 0f, -0.58f), Quaternion.identity, 0.025f, Color.white);
            return storageMarker;
        }

        private static SceneRouteMarker[] BuildCustomerVehicleRoutes(Transform parent, Material asphalt,
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
            CreateWorldLabel("Customer Loading Bay Label", traffic.transform, "ПОГРУЗКА КЛИЕНТА",
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

            return new[] { arrival, departure };
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
                $"B-01 • {boardProductConfig.DisplayName.ToUpperInvariant()} • " +
                $"{boardProductConfig.UnitPrice:N0} ₽ / {boardProductConfig.UnitLabel.ToUpperInvariant()}",
                new Vector3(12f, 2.7f, 1.68f), Quaternion.identity, 0.03f, brandOrange.color);
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

            CreateWorldLabel("Inbound Label", bay.transform, "ПРИЁМКА ПОСТАВКИ",
                new Vector3(11f, 0.04f, -5.15f), Quaternion.Euler(90f, 0f, 0f),
                0.035f, inboundYellow.color);

            GameObject spawnPoint = CreateEmpty("Delivery Vehicle Spawn Point", bay.transform);
            spawnPoint.transform.SetPositionAndRotation(new Vector3(11f, 0.02f, -9f), Quaternion.identity);
            SpawnPointMarker marker = spawnPoint.AddComponent<SpawnPointMarker>();
            marker.Configure(SpawnPointId.DeliveryVehicle);
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
            IReadOnlyCollection<OrderConfig> orderConfigs,
            Material truckPaint, Material darkMetal, Material glass, Material loadingGreen)
        {
            if (orderConfigs == null || orderConfigs.Count == 0)
                throw new ArgumentException("At least one order config is required.", nameof(orderConfigs));

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
                CreateWorldLabel("Loading Label", loadingTarget.transform, "ЗАГРУЗИТЬ",
                    new Vector3(0f, 0f, -0.56f), Quaternion.identity, 0.02f, Color.white);

                GameObject interactionArea = CreateEmpty("Interaction Area", vehicle.transform);
                interactionArea.transform.localPosition = new Vector3(0f, 1.35f, -3.75f);
                BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                interactionCollider.center = new Vector3(0f, 0f, -0.65f);
                interactionCollider.size = new Vector3(3.2f, 3f, 2.6f);

                GameObject slotsRoot = CreateEmpty("Customer Cargo Slots", vehicle.transform);
                int cargoSlotCapacity = orderConfigs.Max(orderConfig => orderConfig.RequiredProductCount);
                Transform[] slots = new Transform[cargoSlotCapacity];
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
                    nextCustomerDelay: 4f);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
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

        private static GameObject CreateCapsule(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name;
            capsule.transform.SetParent(parent, true);
            capsule.transform.position = position;
            capsule.transform.localScale = scale;
            capsule.GetComponent<Renderer>().sharedMaterial = material;
            return capsule;
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

        private static void CreatePointLight(string name, Transform parent, Vector3 position, Color color,
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
        }

        private static void CreateWorldLabel(string name, Transform parent, string text, Vector3 position,
            Quaternion rotation, float characterSize, Color color)
        {
            GameObject labelObject = CreateEmpty(name, parent);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = rotation;
            Vector3 parentScale = parent.lossyScale;
            labelObject.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 64;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
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
            EnsureConfigAsset<OrderConfig>(CementOrderConfigName);
            EnsureConfigAsset<OrderConfig>(BoardOrderConfigName);
            EnsureConfigAsset<ProductConfig>(CementProductConfigName);
            EnsureConfigAsset<ProductConfig>(BoardProductConfigName);
            EnsureConfigAsset<DeliveryConfig>(CementDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(BoardDeliveryConfigName);
            EnsureConfigAsset<CustomerVehicleConfig>("CustomerVehicleConfig");
            EnsureConfigAsset<EconomyConfig>("EconomyConfig");
        }

        private static void ConfigurePrototypeConfigs(
            DeliveryConfig cementDeliveryConfig,
            DeliveryConfig boardDeliveryConfig,
            EconomyConfig economyConfig,
            ProductConfig cementProductConfig,
            ProductConfig boardProductConfig,
            OrderConfig cementOrderConfig,
            OrderConfig boardOrderConfig)
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
            RequireSerializedProperty(economy, "_initialMoney").intValue = 1000;
            economy.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economyConfig);

            ConfigureProductConfig(
                cementProductConfig,
                ProductTypeId.CementBag,
                displayName: "Цемент 25 кг",
                unitLabel: "шт.",
                unitPrice: 350,
                mass: 25f,
                carryMovementSpeed: 3.2f,
                heldRotationEuler: new Vector3(8f, 0f, 0f),
                dropForwardDistance: 1.15f);
            ConfigureProductConfig(
                boardProductConfig,
                ProductTypeId.BoardBundle,
                displayName: "Пачка досок",
                unitLabel: "шт.",
                unitPrice: 480,
                mass: 18f,
                carryMovementSpeed: 2.6f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.35f);

            ConfigureOrderConfig(
                cementOrderConfig,
                ProductTypeId.CementBag,
                requiredProductCount: 2,
                reward: 700);
            ConfigureOrderConfig(
                boardOrderConfig,
                ProductTypeId.BoardBundle,
                requiredProductCount: 2,
                reward: 960);
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
            string displayName, string unitLabel, int unitPrice, float mass, float carryMovementSpeed,
            Vector3 heldRotationEuler, float dropForwardDistance)
        {
            SerializedObject product = new(config);
            RequireSerializedProperty(product, "_productType").intValue = (int)productType;
            RequireSerializedProperty(product, "_displayName").stringValue = displayName;
            RequireSerializedProperty(product, "_unitLabel").stringValue = unitLabel;
            RequireSerializedProperty(product, "_unitPrice").intValue = unitPrice;
            RequireSerializedProperty(product, "_mass").floatValue = mass;
            RequireSerializedProperty(product, "_carryMovementSpeed").floatValue = carryMovementSpeed;
            RequireSerializedProperty(product, "_heldRotationEuler").vector3Value = heldRotationEuler;
            RequireSerializedProperty(product, "_dropForwardDistance").floatValue = dropForwardDistance;
            RequireSerializedProperty(product, "_worldInterpolation").intValue =
                (int)RigidbodyInterpolation.Interpolate;
            RequireSerializedProperty(product, "_worldCollisionDetection").intValue =
                (int)CollisionDetectionMode.ContinuousSpeculative;
            product.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureOrderConfig(OrderConfig config, ProductTypeId productType,
            int requiredProductCount, int reward)
        {
            SerializedObject order = new(config);
            RequireSerializedProperty(order, "_requiredProductType").intValue = (int)productType;
            RequireSerializedProperty(order, "_requiredProductCount").intValue = requiredProductCount;
            RequireSerializedProperty(order, "_reward").intValue = reward;
            order.ApplyModifiedPropertiesWithoutUndo();
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
