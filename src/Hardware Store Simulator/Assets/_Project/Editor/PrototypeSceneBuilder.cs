using System;
using System.Collections.Generic;
using System.Linq;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
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
        private const string SpawnPointPrefabFolder = "Assets/_Project/Prefabs/Scene";
        private const string SpawnPointPrefabPath =
            SpawnPointPrefabFolder + "/SpawnPoint.prefab";
        private const string SpawnPointIconName = "sv_label_5";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
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
        private const string WarehouseWorkerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/WarehouseWorker.prefab";
        private const string WarehouseWorkerTrolleyPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/WarehouseWorkerTrolley.prefab";
        private const string PlatformTrolleyPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/PlatformTrolley.prefab";
        private const string ForkliftPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Forklift.prefab";
        private const string FreightTruckPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/FreightTruck.prefab";
        private const string PalletPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Pallet.prefab";
        private const string GleyLine4RoadPrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/Environment/Prefabs/Roads/Line4.prefab";
        private const string GleyBuildingA1PrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/Environment/Prefabs/Buildings/Building_A1.prefab";
        private const string GleyBuildingB1PrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/Environment/Prefabs/Buildings/Building_B1.prefab";
        private const string GleyBuildingD1PrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/Environment/Prefabs/Buildings/Building_D1.prefab";
        private const string GleySmallSedanBodyPrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/PlayerCar/Prefabs/SmallSedanBody.prefab";
        private const string GleySmallSedanWheelPrefabPath =
            "Assets/Gley/UrbanAssets/Runtime/Graphics/PlayerCar/Prefabs/SmallSedanWheel.prefab";
        private const string WarehouseWorkerNavMeshAssetName = "NavMesh-Navigation";
        private const string WarehouseWorkerNavMeshPath =
            "Assets/Scenes/Prototype_Yard/" + WarehouseWorkerNavMeshAssetName + ".asset";
        private const string CementProductConfigName = "ProductConfig";
        private const string BoardProductConfigName = "ProductConfig_BoardBundle";
        private const string BrickProductConfigName = "ProductConfig_BrickPack";
        private const string DrywallProductConfigName = "ProductConfig_DrywallSheet";
        private const string PaintProductConfigName = "ProductConfig_PaintBucket";
        private const string InsulationProductConfigName = "ProductConfig_InsulationRoll";
        private const string CementDeliveryConfigName = "DeliveryConfig";
        private const string BoardDeliveryConfigName = "DeliveryConfig_BoardBundle";
        private const string BrickDeliveryConfigName = "DeliveryConfig_BrickPack";
        private const string DrywallDeliveryConfigName = "DeliveryConfig_DrywallSheet";
        private const string PaintDeliveryConfigName = "DeliveryConfig_PaintBucket";
        private const string InsulationDeliveryConfigName = "DeliveryConfig_InsulationRoll";
        private const string CementProjectConfigName =
            "CustomerProjectConfig_CementFoundation";
        private const string LumberProjectConfigName =
            "CustomerProjectConfig_LumberShelving";
        private const string WorkbenchProjectConfigName =
            "CustomerProjectConfig_WorkbenchFoundation";
        private const string GardenWallProjectConfigName =
            "CustomerProjectConfig_GardenWall";
        private const string DrywallPartitionProjectConfigName =
            "CustomerProjectConfig_DrywallPartition";
        private const string WorkshopRenovationProjectConfigName =
            "CustomerProjectConfig_WorkshopRenovation";
        private const string GarageInsulationProjectConfigName =
            "CustomerProjectConfig_GarageInsulation";
        private const string ProductRecoveryConfigName = "ProductRecoveryConfig";
        private const string PlatformTrolleyConfigName = "PlatformTrolleyConfig";
        private const string WarehouseWorkerConfigName = "WarehouseWorkerConfig";
        private const string ForkliftConfigName = "ForkliftConfig";
        private const string FreightTruckConfigName = "FreightTruckConfig";
        private const string PalletConfigName = "PalletConfig";
        private const string StoreDayConfigName = "StoreDayConfig";
        private const string CustomerFlowConfigName = "CustomerFlowConfig";
        private const string LegacyCementOrderConfigName = "OrderConfig";
        private const string LegacyBoardOrderConfigName = "OrderConfig_BoardBundle";
        private const int CustomerVehicleCargoCapacity = 3;
        private const int StorageSlotCapacity = 18;
        private const int FreightPalletSlotCapacity = 4;
        private static readonly ILocalizationService RussianPreviewLocalization =
            CreateRussianPreviewLocalization();

        private sealed class AreaRoots
        {
            public AreaRoots(
                PrototypeAreaRoot siteShell,
                PrototypeAreaRoot storefront,
                PrototypeAreaRoot warehouse,
                PrototypeAreaRoot lumber,
                PrototypeAreaRoot inboundDelivery,
                PrototypeAreaRoot customerTraffic,
                PrototypeAreaRoot freight)
            {
                SiteShell = siteShell;
                Storefront = storefront;
                Warehouse = warehouse;
                Lumber = lumber;
                InboundDelivery = inboundDelivery;
                CustomerTraffic = customerTraffic;
                Freight = freight;
            }

            public PrototypeAreaRoot SiteShell { get; }
            public PrototypeAreaRoot Storefront { get; }
            public PrototypeAreaRoot Warehouse { get; }
            public PrototypeAreaRoot Lumber { get; }
            public PrototypeAreaRoot InboundDelivery { get; }
            public PrototypeAreaRoot CustomerTraffic { get; }
            public PrototypeAreaRoot Freight { get; }

            public IEnumerable<PrototypeAreaRoot> All
            {
                get
                {
                    yield return SiteShell;
                    yield return Storefront;
                    yield return Warehouse;
                    yield return Lumber;
                    yield return InboundDelivery;
                    yield return CustomerTraffic;
                    yield return Freight;
                }
            }
        }

        [MenuItem("Tools/Hardware Store/Build Prototype Yard")]
        public static void BuildPrototypeYard()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the prototype scene.");

            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the currently open scene before rebuilding the prototype.");

            Dictionary<PrototypeAreaId, Pose> preservedAreaPoses =
                CaptureAreaRootPoses();

            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Gameplay");
            EnsureFolder(SpawnPointPrefabFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(ConfigFolder);
            EnsureConfigAssets();
            EnsureSpawnPointPrefab();
            PlayerConfig playerConfig = LoadConfig<PlayerConfig>("PlayerConfig");
            DeliveryConfig cementDeliveryConfig =
                LoadConfig<DeliveryConfig>(CementDeliveryConfigName);
            DeliveryConfig boardDeliveryConfig =
                LoadConfig<DeliveryConfig>(BoardDeliveryConfigName);
            DeliveryConfig brickDeliveryConfig =
                LoadConfig<DeliveryConfig>(BrickDeliveryConfigName);
            DeliveryConfig drywallDeliveryConfig =
                LoadConfig<DeliveryConfig>(DrywallDeliveryConfigName);
            DeliveryConfig paintDeliveryConfig =
                LoadConfig<DeliveryConfig>(PaintDeliveryConfigName);
            DeliveryConfig insulationDeliveryConfig =
                LoadConfig<DeliveryConfig>(InsulationDeliveryConfigName);
            CustomerVehicleConfig customerVehicleConfig =
                LoadConfig<CustomerVehicleConfig>("CustomerVehicleConfig");
            CustomerConfig customerConfig = LoadConfig<CustomerConfig>("CustomerConfig");
            CustomerFlowConfig customerFlowConfig =
                LoadConfig<CustomerFlowConfig>(CustomerFlowConfigName);
            EconomyConfig economyConfig = LoadConfig<EconomyConfig>("EconomyConfig");
            ProductRecoveryConfig productRecoveryConfig =
                LoadConfig<ProductRecoveryConfig>(ProductRecoveryConfigName);
            PlatformTrolleyConfig platformTrolleyConfig =
                LoadConfig<PlatformTrolleyConfig>(PlatformTrolleyConfigName);
            WarehouseWorkerConfig warehouseWorkerConfig =
                LoadConfig<WarehouseWorkerConfig>(WarehouseWorkerConfigName);
            ForkliftConfig forkliftConfig = LoadConfig<ForkliftConfig>(ForkliftConfigName);
            FreightTruckConfig freightTruckConfig =
                LoadConfig<FreightTruckConfig>(FreightTruckConfigName);
            PalletConfig palletConfig = LoadConfig<PalletConfig>(PalletConfigName);
            StoreDayConfig storeDayConfig = LoadConfig<StoreDayConfig>(StoreDayConfigName);
            ProductConfig cementProductConfig =
                LoadConfig<ProductConfig>(CementProductConfigName);
            ProductConfig boardProductConfig =
                LoadConfig<ProductConfig>(BoardProductConfigName);
            ProductConfig brickProductConfig =
                LoadConfig<ProductConfig>(BrickProductConfigName);
            ProductConfig drywallProductConfig =
                LoadConfig<ProductConfig>(DrywallProductConfigName);
            ProductConfig paintProductConfig =
                LoadConfig<ProductConfig>(PaintProductConfigName);
            ProductConfig insulationProductConfig =
                LoadConfig<ProductConfig>(InsulationProductConfigName);
            CustomerProjectConfig cementProjectConfig =
                LoadConfig<CustomerProjectConfig>(CementProjectConfigName);
            CustomerProjectConfig lumberProjectConfig =
                LoadConfig<CustomerProjectConfig>(LumberProjectConfigName);
            CustomerProjectConfig workbenchProjectConfig =
                LoadConfig<CustomerProjectConfig>(WorkbenchProjectConfigName);
            CustomerProjectConfig gardenWallProjectConfig =
                LoadConfig<CustomerProjectConfig>(GardenWallProjectConfigName);
            CustomerProjectConfig drywallPartitionProjectConfig =
                LoadConfig<CustomerProjectConfig>(DrywallPartitionProjectConfigName);
            CustomerProjectConfig workshopRenovationProjectConfig =
                LoadConfig<CustomerProjectConfig>(WorkshopRenovationProjectConfigName);
            CustomerProjectConfig garageInsulationProjectConfig =
                LoadConfig<CustomerProjectConfig>(GarageInsulationProjectConfigName);
            ConfigurePrototypeConfigs(
                cementDeliveryConfig,
                boardDeliveryConfig,
                brickDeliveryConfig,
                drywallDeliveryConfig,
                paintDeliveryConfig,
                insulationDeliveryConfig,
                economyConfig,
                productRecoveryConfig,
                storeDayConfig,
                customerFlowConfig,
                warehouseWorkerConfig,
                cementProductConfig,
                boardProductConfig,
                brickProductConfig,
                drywallProductConfig,
                paintProductConfig,
                insulationProductConfig,
                cementProjectConfig,
                lumberProjectConfig,
                workbenchProjectConfig,
                gardenWallProjectConfig,
                drywallPartitionProjectConfig,
                workshopRenovationProjectConfig,
                garageInsulationProjectConfig);
            EnsurePlayerPrefab(playerConfig);
            EnsureProjectContextPrefab();

            Material asphalt = GetOrCreateMaterial("Asphalt", new Color(0.12f, 0.14f, 0.15f), 0.12f);
            Material concrete = GetOrCreateMaterial("Concrete", new Color(0.48f, 0.49f, 0.47f), 0.08f);
            Material brandBlue = GetOrCreateMaterial("BrandBlue", new Color(0.055f, 0.19f, 0.32f), 0.26f);
            Material brandOrange = GetOrCreateMaterial("BrandOrange", new Color(0.95f, 0.31f, 0.055f), 0.22f, true);
            Material cement = GetOrCreateMaterial("CementBag", new Color(0.67f, 0.62f, 0.50f), 0.03f);
            Material timber = GetOrCreateMaterial("Timber", new Color(0.48f, 0.27f, 0.11f), 0.14f);
            Material boardStrap = GetOrCreateMaterial("BoardStrap", new Color(0.1f, 0.12f, 0.11f), 0.38f);
            Material brick = GetOrCreateMaterial(
                "Brick", new Color(0.58f, 0.212f, 0.114f), 0.08f);
            Material drywall = GetOrCreateMaterial(
                "Drywall", new Color(0.859f, 0.843f, 0.761f), 0.12f);
            Material drywallEdge = GetOrCreateMaterial(
                "DrywallEdge", new Color(0.08f, 0.32f, 0.55f), 0.24f);
            Material darkMetal = GetOrCreateMaterial("DarkMetal", new Color(0.075f, 0.085f, 0.095f), 0.52f);
            Material truckPaint = GetOrCreateMaterial("TruckPaint", new Color(0.095f, 0.34f, 0.53f), 0.42f);
            Material loadingGreen = GetOrCreateMaterial("LoadingGreen", new Color(0.08f, 0.78f, 0.36f), 0.18f, true);
            Material glass = GetOrCreateMaterial("Glass", new Color(0.12f, 0.24f, 0.31f), 0.72f);
            Material white = GetOrCreateMaterial("White", new Color(0.82f, 0.84f, 0.82f), 0.18f);
            Material yellow = GetOrCreateMaterial("SafetyYellow", new Color(0.95f, 0.65f, 0.08f), 0.18f);

            EnsureCementProductPrefab(cementProductConfig, cement);
            EnsureBoardProductPrefab(boardProductConfig, timber, boardStrap);
            EnsureBrickProductPrefab(brickProductConfig, brick, darkMetal);
            EnsureDrywallProductPrefab(drywallProductConfig, drywall, drywallEdge);
            EnsurePaintProductPrefab(paintProductConfig, brandBlue, white, darkMetal);
            EnsureInsulationProductPrefab(
                insulationProductConfig, yellow, darkMetal);
            EnsureDeliveryVehiclePrefab(
                new[]
                {
                    cementDeliveryConfig,
                    boardDeliveryConfig,
                    brickDeliveryConfig,
                    drywallDeliveryConfig,
                    paintDeliveryConfig,
                    insulationDeliveryConfig
                },
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
            EnsureWarehouseWorkerTrolleyPrefab(
                warehouseWorkerConfig,
                brandBlue,
                yellow,
                darkMetal,
                timber);
            EnsurePlatformTrolleyPrefab(
                platformTrolleyConfig,
                brandOrange,
                darkMetal,
                timber);
            EnsureForkliftPrefab(
                forkliftConfig,
                yellow,
                darkMetal,
                glass);
            EnsureFreightTruckPrefab(
                freightTruckConfig,
                truckPaint,
                darkMetal,
                glass);
            EnsurePalletPrefab(palletConfig, timber, darkMetal);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();

            GameObject environment = CreateEmpty("Environment");
            NavMeshSurface navigation = BuildNavigation(environment.transform);
            (Light sun, Light[] indoorLights) = BuildLighting(environment.transform);
            BuildYard(environment.transform, asphalt, concrete, brandBlue, white, yellow);
            BuildStreetVisuals(environment.transform);
            (SpawnPointMarker forkliftSpawnPoint,
                    SpawnPointMarker freightTruckSpawnPoint,
                    SpawnPointMarker inboundPalletSpawnPoint,
                    SceneViewMarker freightStagingZone) =
                BuildFreightYard(
                    environment.transform,
                    asphalt,
                    concrete,
                    brandBlue,
                    yellow,
                    loadingGreen);
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
            CustomerFlowLayoutMarker customerFlowLayout = BuildCustomerFlowLayout(
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
                    SpawnPointMarker workerStorageAccessPoint,
                    SpawnPointMarker workerCustomerLoadingAccessPoint,
                    SpawnPointMarker workerTrolleyHomePoint,
                    SpawnPointMarker workerTrolleyCustomerLoadingAccessPoint,
                    SpawnPointMarker workerInboundTrolleyStorageBypassPoint,
                    SpawnPointMarker workerInboundTrolleyStorageAccessPoint,
                    SpawnPointMarker workerOutboundTrolleyStorageApproachPoint,
                    SpawnPointMarker workerOutboundTrolleyStorageAccessPoint) =
                BuildWarehouseWorkerAccessPoints(environment.transform);

            SpawnPointMarker playerSpawnPoint = BuildPlayerSpawnPoint();
            AreaRoots areaRoots = GroupMovableAreas(
                environment.transform,
                preservedAreaPoses,
                playerSpawnPoint,
                platformTrolleySpawnPoint,
                workerIdlePoint,
                workerDeliveryAccessPoint,
                workerStorageAccessPoint,
                workerCustomerLoadingAccessPoint,
                workerTrolleyHomePoint,
                workerTrolleyCustomerLoadingAccessPoint,
                workerInboundTrolleyStorageBypassPoint,
                workerInboundTrolleyStorageAccessPoint,
                workerOutboundTrolleyStorageApproachPoint,
                workerOutboundTrolleyStorageAccessPoint);
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
                    workerStorageAccessPoint,
                    workerCustomerLoadingAccessPoint,
                    workerTrolleyHomePoint,
                    workerTrolleyCustomerLoadingAccessPoint,
                    workerInboundTrolleyStorageBypassPoint,
                    workerInboundTrolleyStorageAccessPoint,
                    workerOutboundTrolleyStorageApproachPoint,
                    workerOutboundTrolleyStorageAccessPoint,
                    forkliftSpawnPoint,
                    freightTruckSpawnPoint,
                    inboundPalletSpawnPoint
                },
                Array.Empty<SceneRouteMarker>(),
                customerFlowLayout,
                new[]
                {
                    orderCounter,
                    procurementTerminal,
                    storageZone,
                    trolleyUpgradeTerminal,
                    storeControlTerminal,
                    freightStagingZone
                },
                hud,
                audio,
                dayNight);
            SceneInitializationInstaller installer = systems.AddComponent<SceneInitializationInstaller>();
            installer.Configure(initializer);
            sceneContext.Installers = new MonoInstaller[] { installer };

            BakeAndValidateNavigation(
                navigation,
                customerFlowLayout,
                workerIdlePoint,
                workerDeliveryAccessPoint,
                workerStorageAccessPoint,
                workerCustomerLoadingAccessPoint,
                workerTrolleyHomePoint,
                workerTrolleyCustomerLoadingAccessPoint,
                workerInboundTrolleyStorageBypassPoint,
                workerInboundTrolleyStorageAccessPoint,
                workerOutboundTrolleyStorageApproachPoint,
                workerOutboundTrolleyStorageAccessPoint,
                warehouseWorkerConfig.TrolleyFollowDistance);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save prototype scene to {ScenePath}.");

            GleyTrafficPrototypeBuilder.PrepareFromMenu();
            AttachGeneratedTrafficGraph(areaRoots.CustomerTraffic.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    $"Could not save grouped Gley traffic authoring to {ScenePath}.");
            PutSceneFirstInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.objects = areaRoots.All
                .Select(areaRoot => areaRoot.gameObject)
                .Cast<Object>()
                .ToArray();

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log($"[Hardware Store] Playable prototype scene created: {ScenePath}");
        }

        [MenuItem("Tools/Hardware Store/Apply Manual Yard Layout", priority = 20)]
        public static void ApplyManualYardLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before applying the manual yard layout.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException(
                    $"Open '{ScenePath}' before applying the manual yard layout.");
            }

            Dictionary<PrototypeAreaId, PrototypeAreaRoot> areaRoots =
                FindAreaRoots(scene);
            ValidateAreaRootScales(areaRoots.Values);

            NavMeshSurface navigation = FindSingleSceneComponent<NavMeshSurface>(scene);
            CustomerFlowLayoutMarker customerFlowLayout =
                FindSingleSceneComponent<CustomerFlowLayoutMarker>(scene);
            WarehouseWorkerConfig workerConfig =
                LoadConfig<WarehouseWorkerConfig>(WarehouseWorkerConfigName);
            BakeAndValidateNavigation(
                navigation,
                customerFlowLayout,
                FindSpawnPoint(scene, SpawnPointId.WarehouseWorker),
                FindSpawnPoint(scene, SpawnPointId.WarehouseWorkerDeliveryAccess),
                FindSpawnPoint(scene, SpawnPointId.WarehouseWorkerStorageAccess),
                FindSpawnPoint(scene, SpawnPointId.WarehouseWorkerCustomerLoadingAccess),
                FindSpawnPoint(scene, SpawnPointId.WarehouseWorkerTrolley),
                FindSpawnPoint(
                    scene,
                    SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess),
                FindSpawnPoint(
                    scene,
                    SpawnPointId.WarehouseWorkerInboundTrolleyStorageBypass),
                FindSpawnPoint(
                    scene,
                    SpawnPointId.WarehouseWorkerInboundTrolleyStorageAccess),
                FindSpawnPoint(
                    scene,
                    SpawnPointId.WarehouseWorkerOutboundTrolleyStorageApproach),
                FindSpawnPoint(
                    scene,
                    SpawnPointId.WarehouseWorkerOutboundTrolleyStorageAccess),
                workerConfig.TrolleyFollowDistance);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save '{ScenePath}'.");

            GleyTrafficPrototypeBuilder.PrepareFromMenu();
            AttachGeneratedTrafficGraph(
                areaRoots[PrototypeAreaId.CustomerTraffic].transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save '{ScenePath}'.");

            Selection.objects = areaRoots.Values
                .OrderBy(areaRoot => areaRoot.Id)
                .Select(areaRoot => areaRoot.gameObject)
                .Cast<Object>()
                .ToArray();
            Debug.Log(
                "[Hardware Store] Manual yard layout applied: NavMesh and customer " +
                "traffic data were rebuilt. The AREA roots remain the authoring source.");
        }

        private static Dictionary<PrototypeAreaId, Pose> CaptureAreaRootPoses()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                return new Dictionary<PrototypeAreaId, Pose>();

            Dictionary<PrototypeAreaId, PrototypeAreaRoot> roots = FindAreaRoots(
                scene,
                requireCompleteSet: false);
            ValidateAreaRootScales(roots.Values);
            return roots.ToDictionary(
                pair => pair.Key,
                pair => new Pose(
                    pair.Value.transform.position,
                    pair.Value.transform.rotation));
        }

        private static AreaRoots GroupMovableAreas(
            Transform environment,
            IReadOnlyDictionary<PrototypeAreaId, Pose> preservedPoses,
            SpawnPointMarker playerSpawnPoint,
            SpawnPointMarker platformTrolleySpawnPoint,
            SpawnPointMarker workerIdlePoint,
            SpawnPointMarker workerDeliveryAccessPoint,
            SpawnPointMarker workerStorageAccessPoint,
            SpawnPointMarker workerCustomerLoadingAccessPoint,
            SpawnPointMarker workerTrolleyHomePoint,
            SpawnPointMarker workerTrolleyCustomerLoadingAccessPoint,
            SpawnPointMarker workerInboundTrolleyStorageBypassPoint,
            SpawnPointMarker workerInboundTrolleyStorageAccessPoint,
            SpawnPointMarker workerOutboundTrolleyStorageApproachPoint,
            SpawnPointMarker workerOutboundTrolleyStorageAccessPoint)
        {
            AreaRoots roots = CreateAreaRoots(environment);
            Transform yard = RequireDirectChild(environment, "Yard");

            ReparentDirectChild(yard, "Shop Walkway", roots.Storefront.transform);
            string[] customerYardObjects =
            {
                "Customer Access Road",
                "Customer Exterior Walkway",
                "Customer Pedestrian Gate Walkway",
                "Customer Exit Stripe Left",
                "Customer Exit Stripe Right",
                "Customer Entry Stripe Left",
                "Customer Entry Stripe Right",
                "Customer Lane Barrier South",
                "Customer Lane Barrier North"
            };
            foreach (string objectName in customerYardObjects)
                ReparentDirectChild(yard, objectName, roots.CustomerTraffic.transform);
            ReparentDirectChild(yard, "Freight Lane Divider", roots.Freight.transform);

            ReparentDirectChild(environment, "Navigation", roots.SiteShell.transform);
            ReparentDirectChild(environment, "Sun", roots.SiteShell.transform);
            ReparentDirectChild(environment, "Yard", roots.SiteShell.transform);

            ReparentDirectChild(environment, "Shop Light", roots.Storefront.transform);
            ReparentDirectChild(environment, "Sales Kiosk", roots.Storefront.transform);
            ReparentDirectChild(
                environment,
                "Store Control Station",
                roots.Storefront.transform);
            ReparentDirectChild(
                environment,
                "Trolley Upgrade Station",
                roots.Storefront.transform);
            Reparent(playerSpawnPoint.transform, roots.Storefront.transform);

            ReparentDirectChild(environment, "Warehouse Light", roots.Warehouse.transform);
            ReparentDirectChild(environment, "Materials Storage", roots.Warehouse.transform);
            ReparentDirectChild(environment, "Lumber Display", roots.Lumber.transform);
            ReparentDirectChild(
                environment,
                "Inbound Delivery Bay",
                roots.InboundDelivery.transform);
            ReparentDirectChild(
                environment,
                "Customer Vehicle Traffic",
                roots.CustomerTraffic.transform);
            ReparentDirectChild(
                environment,
                "Imported 3D Street Visuals",
                roots.CustomerTraffic.transform);
            ReparentDirectChild(environment, "Rear Freight Yard", roots.Freight.transform);

            GameObject warehouseAccess = CreateEmpty(
                "Worker Access Points",
                roots.Warehouse.transform);
            Reparent(workerIdlePoint.transform, warehouseAccess.transform);
            Reparent(workerStorageAccessPoint.transform, warehouseAccess.transform);
            Reparent(workerTrolleyHomePoint.transform, warehouseAccess.transform);
            Reparent(platformTrolleySpawnPoint.transform, warehouseAccess.transform);
            Reparent(
                workerInboundTrolleyStorageBypassPoint.transform,
                warehouseAccess.transform);
            Reparent(
                workerInboundTrolleyStorageAccessPoint.transform,
                warehouseAccess.transform);
            Reparent(
                workerOutboundTrolleyStorageApproachPoint.transform,
                warehouseAccess.transform);
            Reparent(
                workerOutboundTrolleyStorageAccessPoint.transform,
                warehouseAccess.transform);

            GameObject deliveryAccess = CreateEmpty(
                "Worker Access Points",
                roots.InboundDelivery.transform);
            Reparent(workerDeliveryAccessPoint.transform, deliveryAccess.transform);

            GameObject customerAccess = CreateEmpty(
                "Worker Access Points",
                roots.CustomerTraffic.transform);
            Reparent(workerCustomerLoadingAccessPoint.transform, customerAccess.transform);
            Reparent(
                workerTrolleyCustomerLoadingAccessPoint.transform,
                customerAccess.transform);

            Transform obsoleteAccessRoot = RequireDirectChild(
                environment,
                "Warehouse Worker Access Points");
            if (obsoleteAccessRoot.childCount != 0)
            {
                throw new InvalidOperationException(
                    "Every warehouse-worker access marker must belong to a movable area.");
            }
            Object.DestroyImmediate(obsoleteAccessRoot.gameObject);

            ApplyAreaRootPoses(roots, preservedPoses);
            return roots;
        }

        private static AreaRoots CreateAreaRoots(Transform environment) =>
            new(
                CreateAreaRoot(
                    "[AREA] Site Shell",
                    environment,
                    PrototypeAreaId.SiteShell,
                    Vector3.zero),
                CreateAreaRoot(
                    "[AREA] Storefront",
                    environment,
                    PrototypeAreaId.Storefront,
                    new Vector3(-9f, 0f, 0f)),
                CreateAreaRoot(
                    "[AREA] Warehouse",
                    environment,
                    PrototypeAreaId.Warehouse,
                    PrototypeYardLayoutSpec.StorageOffset),
                CreateAreaRoot(
                    "[AREA] Lumber",
                    environment,
                    PrototypeAreaId.Lumber,
                    PrototypeYardLayoutSpec.LumberOffset),
                CreateAreaRoot(
                    "[AREA] Inbound Delivery",
                    environment,
                    PrototypeAreaId.InboundDelivery,
                    new Vector3(
                        PrototypeYardLayoutSpec.DeliveryVehiclePose.position.x,
                        0f,
                        PrototypeYardLayoutSpec.DeliveryVehiclePose.position.z)),
                CreateAreaRoot(
                    "[AREA] Customer Traffic",
                    environment,
                    PrototypeAreaId.CustomerTraffic,
                    new Vector3(
                        PrototypeYardLayoutSpec.CustomerLoadingX,
                        0f,
                        PrototypeYardLayoutSpec.CustomerLoadingZ)),
                CreateAreaRoot(
                    "[AREA] Freight",
                    environment,
                    PrototypeAreaId.Freight,
                    new Vector3(
                        PrototypeYardLayoutSpec.FreightTruckPose.position.x,
                        0f,
                        PrototypeYardLayoutSpec.FreightTruckPose.position.z)));

        private static PrototypeAreaRoot CreateAreaRoot(
            string name,
            Transform parent,
            PrototypeAreaId id,
            Vector3 canonicalPosition)
        {
            GameObject root = CreateEmpty(name, parent);
            root.transform.SetPositionAndRotation(canonicalPosition, Quaternion.identity);
            PrototypeAreaRoot marker = root.AddComponent<PrototypeAreaRoot>();
            marker.Configure(id);
            return marker;
        }

        private static void ApplyAreaRootPoses(
            AreaRoots roots,
            IReadOnlyDictionary<PrototypeAreaId, Pose> preservedPoses)
        {
            foreach (PrototypeAreaRoot root in roots.All)
            {
                if (!preservedPoses.TryGetValue(root.Id, out Pose pose))
                    continue;

                root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        private static Dictionary<PrototypeAreaId, PrototypeAreaRoot> FindAreaRoots(
            Scene scene,
            bool requireCompleteSet = true)
        {
            PrototypeAreaRoot[] markers = Object.FindObjectsByType<PrototypeAreaRoot>(
                    FindObjectsInactive.Include)
                .Where(marker => marker.gameObject.scene == scene)
                .ToArray();
            var result = new Dictionary<PrototypeAreaId, PrototypeAreaRoot>();
            foreach (PrototypeAreaRoot marker in markers)
            {
                if (!result.TryAdd(marker.Id, marker))
                {
                    throw new InvalidOperationException(
                        $"Prototype area '{marker.Id}' occurs more than once in '{scene.path}'.");
                }
            }

            if (!requireCompleteSet)
                return result;

            PrototypeAreaId[] requiredIds =
                (PrototypeAreaId[])Enum.GetValues(typeof(PrototypeAreaId));
            foreach (PrototypeAreaId id in requiredIds)
            {
                if (!result.ContainsKey(id))
                {
                    throw new InvalidOperationException(
                        $"Prototype area '{id}' is missing from '{scene.path}'.");
                }
            }

            return result;
        }

        private static void ValidateAreaRootScales(
            IEnumerable<PrototypeAreaRoot> areaRoots)
        {
            foreach (PrototypeAreaRoot areaRoot in areaRoots)
            {
                if (Vector3.Distance(areaRoot.transform.localScale, Vector3.one) > 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"Move or rotate '{areaRoot.name}', but do not scale it. " +
                        "Area root scale must remain (1, 1, 1).");
                }
            }
        }

        private static T FindSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(
                    FindObjectsInactive.Include)
                .Where(component => component.gameObject.scene == scene)
                .ToArray();
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.path}' must contain exactly one {typeof(T).Name}, " +
                    $"but {components.Length} were found.");
            }

            return components[0];
        }

        private static SpawnPointMarker FindSpawnPoint(Scene scene, SpawnPointId id)
        {
            SpawnPointMarker[] points = Object.FindObjectsByType<SpawnPointMarker>(
                    FindObjectsInactive.Include)
                .Where(point => point.gameObject.scene == scene && point.Id == id)
                .ToArray();
            if (points.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.path}' must contain exactly one spawn point '{id}', " +
                    $"but {points.Length} were found.");
            }

            return points[0];
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' must contain direct child '{name}'.");
            }

            return child;
        }

        private static void ReparentDirectChild(
            Transform currentParent,
            string childName,
            Transform newParent) =>
            Reparent(RequireDirectChild(currentParent, childName), newParent);

        private static void Reparent(Transform child, Transform newParent) =>
            child.SetParent(newParent, true);

        private static void AttachGeneratedTrafficGraph(Transform customerTrafficArea)
        {
            Scene scene = customerTrafficArea.gameObject.scene;
            Transform[] gleyRoots = Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include)
                .Where(transform =>
                    transform.gameObject.scene == scene &&
                    transform.name == "Gley")
                .ToArray();
            if (gleyRoots.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.path}' must contain exactly one Gley root, " +
                    $"but {gleyRoots.Length} were found.");
            }

            if (gleyRoots[0].parent != customerTrafficArea)
                Reparent(gleyRoots[0], customerTrafficArea);
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
            Light warehouseLight = CreatePointLight("Warehouse Light", parent,
                new Vector3(-3.5f, 3.25f, 12.7f),
                new Color(0.68f, 0.82f, 1f), 7.5f, 430f);

            return (sun, new[] { shopLight, warehouseLight });
        }

        private static void BuildYard(Transform parent, Material asphalt, Material concrete, Material brandBlue,
            Material white, Material yellow)
        {
            GameObject yard = CreateEmpty("Yard", parent);
            float yardDepth = PrototypeYardLayoutSpec.YardRearZ -
                              PrototypeYardLayoutSpec.YardFrontZ;
            float yardCenterZ = (PrototypeYardLayoutSpec.YardRearZ +
                                 PrototypeYardLayoutSpec.YardFrontZ) * 0.5f;
            CreateCube(
                "Asphalt Ground",
                yard.transform,
                new Vector3(0f, -0.12f, yardCenterZ),
                new Vector3(
                    PrototypeYardLayoutSpec.YardHalfWidth * 2f,
                    0.24f,
                    yardDepth),
                asphalt);
            GameObject publicRoadProxy = CreateCube(
                "Customer Access Road",
                yard.transform,
                new Vector3(
                    0f,
                    -0.12f,
                    PrototypeYardLayoutSpec.PublicRoadCenterZ),
                new Vector3(100f, 0.24f, 26f),
                asphalt);
            publicRoadProxy.GetComponent<Renderer>().enabled = false;

            CreateCube(
                "West Neighbor Plot",
                yard.transform,
                new Vector3(-34f, -0.12f, yardCenterZ),
                new Vector3(24f, 0.24f, yardDepth),
                concrete,
                false);
            CreateCube(
                "East Neighbor Plot",
                yard.transform,
                new Vector3(34f, -0.12f, yardCenterZ),
                new Vector3(24f, 0.24f, yardDepth),
                concrete,
                false);
            CreateCube(
                "Rear Logistics Road Reserve",
                yard.transform,
                new Vector3(0f, -0.1f, 66f),
                new Vector3(92f, 0.2f, 10f),
                asphalt,
                false);
            CreateCube(
                "Future Depth Expansion Reserve",
                yard.transform,
                new Vector3(0f, -0.08f, 82f),
                new Vector3(39f, 0.16f, 22f),
                concrete,
                false);

            CreateCube(
                "North Fence", yard.transform,
                new Vector3(0f, 1.15f, PrototypeYardLayoutSpec.YardRearZ),
                new Vector3(40f, 2.3f, 0.18f), brandBlue);
            CreateCube(
                "West Fence", yard.transform,
                new Vector3(-PrototypeYardLayoutSpec.YardHalfWidth, 1.15f, yardCenterZ),
                new Vector3(0.18f, 2.3f, yardDepth), brandBlue);
            CreateCube(
                "East Fence", yard.transform,
                new Vector3(PrototypeYardLayoutSpec.YardHalfWidth, 1.15f, yardCenterZ),
                new Vector3(0.18f, 2.3f, yardDepth), brandBlue);

            CreateCube("South Fence West Edge", yard.transform,
                new Vector3(-19.25f, 1.15f, PrototypeYardLayoutSpec.YardFrontZ),
                new Vector3(1.5f, 2.3f, 0.18f), brandBlue);
            CreateCube("South Fence Freight To Pedestrian", yard.transform,
                new Vector3(-11f, 1.15f, PrototypeYardLayoutSpec.YardFrontZ),
                new Vector3(5f, 2.3f, 0.18f), brandBlue);
            CreateCube("South Fence Pedestrian To Exit", yard.transform,
                new Vector3(-1.75f, 1.15f, PrototypeYardLayoutSpec.YardFrontZ),
                new Vector3(8.5f, 2.3f, 0.18f), brandBlue);
            CreateCube("South Fence Between Customer Gates", yard.transform,
                new Vector3(9.25f, 1.15f, PrototypeYardLayoutSpec.YardFrontZ),
                new Vector3(3.5f, 2.3f, 0.18f), brandBlue);
            CreateCube("South Fence East Edge", yard.transform,
                new Vector3(18f, 1.15f, PrototypeYardLayoutSpec.YardFrontZ),
                new Vector3(4f, 2.3f, 0.18f), brandBlue);

            CreateCube("Customer Exterior Walkway", yard.transform,
                new Vector3(0f, 0.07f, -18.5f),
                new Vector3(80f, 0.14f, 5f), concrete);
            CreateCube("Customer Pedestrian Gate Walkway", yard.transform,
                new Vector3(-7.25f, 0.025f, -15.15f), new Vector3(2.2f, 0.05f, 2.6f),
                concrete, false);

            CreateCube("Customer Exit Stripe Left", yard.transform,
                new Vector3(2.5f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);
            CreateCube("Customer Exit Stripe Right", yard.transform,
                new Vector3(7.5f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);
            CreateCube("Customer Entry Stripe Left", yard.transform,
                new Vector3(11f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);
            CreateCube("Customer Entry Stripe Right", yard.transform,
                new Vector3(16f, 0.015f, -14.7f),
                new Vector3(0.18f, 0.03f, 2.2f), white, false);

            CreateCube("Freight Lane Divider", yard.transform,
                new Vector3(-13.5f, 0.55f, 9f),
                new Vector3(0.18f, 1.1f, 50f), brandBlue);
            CreateCube("Customer Lane Barrier South", yard.transform,
                new Vector3(0.5f, 0.55f, -5f),
                new Vector3(0.18f, 1.1f, 18f), brandBlue);
            CreateCube("Customer Lane Barrier North", yard.transform,
                new Vector3(0.5f, 0.55f, 25f),
                new Vector3(0.18f, 1.1f, 16f), brandBlue);

            for (int i = 0; i < 5; i++)
            {
                CreateCube($"Safety Marking {i + 1}", yard.transform,
                    new Vector3(-2.4f + i * 1.2f, 0.02f, -8.5f),
                    new Vector3(0.65f, 0.04f, 0.16f), yellow, false);
            }

            CreateCube("Shop Walkway", yard.transform, new Vector3(-9f, 0.02f, -0.95f),
                new Vector3(6.5f, 0.04f, 3f), concrete, false);
        }

        private static void BuildStreetVisuals(Transform parent)
        {
            GameObject root = CreateEmpty("Imported 3D Street Visuals", parent);
            GameObject roadPrefab = LoadRequiredPrefab(GleyLine4RoadPrefabPath);
            float[] roadTileXs = { -45.72f, 0f, 45.72f };
            for (int index = 0; index < roadTileXs.Length; index++)
            {
                GameObject road = InstantiateVisualPrefab(
                    roadPrefab,
                    root.transform,
                    $"Public Road 3D Tile {index + 1}",
                    new Vector3(roadTileXs[index], -0.503f,
                        PrototypeYardLayoutSpec.PublicRoadCenterZ),
                    Quaternion.identity);
                DisableVisualColliders(road);
            }

            GameObject buildingA = LoadRequiredPrefab(GleyBuildingA1PrefabPath);
            GameObject buildingB = LoadRequiredPrefab(GleyBuildingB1PrefabPath);
            GameObject buildingD = LoadRequiredPrefab(GleyBuildingD1PrefabPath);
            DisableVisualColliders(InstantiateVisualPrefab(
                buildingD, root.transform, "West Neighbor Store 3D",
                new Vector3(-34f, 0f, 5f), Quaternion.Euler(0f, 180f, 0f)));
            DisableVisualColliders(InstantiateVisualPrefab(
                buildingA, root.transform, "East Neighbor Store 3D",
                new Vector3(34f, 0f, 5f), Quaternion.Euler(0f, 180f, 0f)));
            DisableVisualColliders(InstantiateVisualPrefab(
                buildingB, root.transform, "Rear District Building 3D",
                new Vector3(34f, 0f, 35f), Quaternion.Euler(0f, 180f, 0f)));

            GameObject sedanBody = LoadRequiredPrefab(GleySmallSedanBodyPrefabPath);
            GameObject sedanWheel = LoadRequiredPrefab(GleySmallSedanWheelPrefabPath);
            BuildStaticSedanVisual(
                root.transform,
                sedanBody,
                sedanWheel,
                "Parked Sedan East Mid",
                new Vector3(
                    PrototypeYardLayoutSpec.GetVisualParkingX(7),
                    0f,
                    PrototypeYardLayoutSpec.ParallelParkingZ));
            BuildStaticSedanVisual(
                root.transform,
                sedanBody,
                sedanWheel,
                "Parked Sedan East Far",
                new Vector3(
                    PrototypeYardLayoutSpec.GetVisualParkingX(9),
                    0f,
                    PrototypeYardLayoutSpec.ParallelParkingZ));
        }

        private static void BuildStaticSedanVisual(
            Transform parent,
            GameObject bodyPrefab,
            GameObject wheelPrefab,
            string name,
            Vector3 position)
        {
            GameObject root = CreateEmpty(name, parent);
            root.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 90f, 0f));
            GameObject body = InstantiateVisualPrefab(
                bodyPrefab,
                root.transform,
                "Body",
                Vector3.zero,
                Quaternion.identity,
                useLocalSpace: true);
            DisableVisualColliders(body);

            (string Name, Vector3 Position, Quaternion Rotation)[] wheels =
            {
                ("Front Left Wheel", new Vector3(-0.75f, 0.38f, 1.29f),
                    Quaternion.identity),
                ("Front Right Wheel", new Vector3(0.75f, 0.38f, 1.29f),
                    Quaternion.Euler(0f, 180f, 0f)),
                ("Rear Left Wheel", new Vector3(-0.75f, 0.38f, -1.29f),
                    Quaternion.identity),
                ("Rear Right Wheel", new Vector3(0.75f, 0.38f, -1.29f),
                    Quaternion.Euler(0f, 180f, 0f))
            };
            foreach ((string wheelName, Vector3 wheelPosition,
                         Quaternion wheelRotation) in wheels)
            {
                GameObject wheel = InstantiateVisualPrefab(
                    wheelPrefab,
                    root.transform,
                    wheelName,
                    wheelPosition,
                    wheelRotation,
                    useLocalSpace: true);
                DisableVisualColliders(wheel);
            }
        }

        private static GameObject LoadRequiredPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
            throw new InvalidOperationException($"Required 3D prefab is missing at '{path}'.");

        private static GameObject InstantiateVisualPrefab(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            bool useLocalSpace = false)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject ??
                                  throw new InvalidOperationException(
                                      $"Could not instantiate visual prefab '{prefab.name}'.");
            instance.name = name;
            if (useLocalSpace)
                instance.transform.SetLocalPositionAndRotation(position, rotation);
            else
                instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        private static void DisableVisualColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        private static (SpawnPointMarker Forklift, SpawnPointMarker FreightTruck,
                SpawnPointMarker InboundPallet, SceneViewMarker StagingZone)
            BuildFreightYard(
                Transform parent,
                Material asphalt,
                Material concrete,
                Material fence,
                Material safetyYellow,
                Material stagingGreen)
        {
            GameObject freightYard = CreateEmpty("Rear Freight Yard", parent);
            CreateCube(
                "Freight Yard Surface",
                freightYard.transform,
                new Vector3(0f, 0.015f, 47f),
                new Vector3(38f, 0.03f, 26f),
                asphalt,
                false);
            CreateCube(
                "Freight Approach Road",
                freightYard.transform,
                new Vector3(-16f, 0.015f, 9f),
                new Vector3(5.5f, 0.03f, 50f),
                asphalt,
                false);
            CreateCube(
                "Freight Staging Pad",
                freightYard.transform,
                new Vector3(-5.5f, 0.035f, 46f),
                new Vector3(5f, 0.04f, 12f),
                concrete,
                false);
            CreateCube(
                "Freight Bay Stop Line",
                freightYard.transform,
                new Vector3(6f, 0.055f, 40.5f),
                new Vector3(5.5f, 0.04f, 0.18f),
                safetyYellow,
                false);
            CreateCube(
                "Freight Lane Left",
                freightYard.transform,
                new Vector3(-18.5f, 0.055f, 9f),
                new Vector3(0.14f, 0.04f, 50f),
                safetyYellow,
                false);
            CreateCube(
                "Freight Lane Right",
                freightYard.transform,
                new Vector3(-13.5f, 0.055f, 9f),
                new Vector3(0.14f, 0.04f, 50f),
                safetyYellow,
                false);
            CreateCube(
                "Rear Freight West Fence",
                freightYard.transform,
                new Vector3(-19f, 1.15f, 47f),
                new Vector3(0.18f, 2.3f, 26f),
                fence);
            CreateCube(
                "Rear Freight East Fence",
                freightYard.transform,
                new Vector3(19f, 1.15f, 47f),
                new Vector3(0.18f, 2.3f, 26f),
                fence);

            GameObject approachNavigationExclusion = CreateEmpty(
                "Freight Approach Navigation Exclusion", freightYard.transform);
            approachNavigationExclusion.transform.position = new Vector3(-16f, 0f, 9f);
            NavMeshModifierVolume approachModifier =
                approachNavigationExclusion.AddComponent<NavMeshModifierVolume>();
            approachModifier.center = new Vector3(0f, 1.5f, 0f);
            approachModifier.size = new Vector3(5.5f, 3f, 50f);
            approachModifier.area = NavMesh.GetAreaFromName("Not Walkable");

            GameObject courtNavigationExclusion = CreateEmpty(
                "Freight Court Navigation Exclusion", freightYard.transform);
            courtNavigationExclusion.transform.position = new Vector3(0f, 0f, 47f);
            NavMeshModifierVolume courtModifier =
                courtNavigationExclusion.AddComponent<NavMeshModifierVolume>();
            courtModifier.center = new Vector3(0f, 1.5f, 0f);
            courtModifier.size = new Vector3(38f, 3f, 26f);
            courtModifier.area = NavMesh.GetAreaFromName("Not Walkable");

            GameObject stagingZoneObject = CreateEmpty(
                "Freight Staging Zone", freightYard.transform);
            stagingZoneObject.AddComponent<EntityBehaviour>();
            stagingZoneObject.AddComponent<TransformRegistrar>();
            var stagingSlots = new Transform[FreightPalletSlotCapacity];
            for (int index = 0; index < stagingSlots.Length; index++)
            {
                float slotZ = 42.7f + index * 2.2f;
                CreateCube(
                    $"Staging Slot Marking {index + 1}",
                    stagingZoneObject.transform,
                    new Vector3(-5.5f, 0.06f, slotZ),
                    new Vector3(1.6f, 0.05f, 1.45f),
                    stagingGreen,
                    false);
                GameObject slot = CreateEmpty(
                    $"Pallet Slot {index + 1}", stagingZoneObject.transform);
                slot.transform.position = new Vector3(-5.5f, 0.03f, slotZ);
                slot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                stagingSlots[index] = slot.transform;
            }

            SlotsRegistrar slotsRegistrar =
                stagingZoneObject.AddComponent<SlotsRegistrar>();
            slotsRegistrar.Configure(stagingSlots);
            SceneViewMarker stagingZone =
                stagingZoneObject.AddComponent<SceneViewMarker>();
            stagingZone.Configure(SceneViewId.FreightStagingZone);

            SpawnPointMarker forklift = CreateSpawnPoint(
                "Forklift Spawn Point",
                freightYard.transform,
                SpawnPointId.Forklift,
                new Vector3(-5.5f, 0.02f, 38.5f),
                Quaternion.identity);
            Pose truckPose = PrototypeYardLayoutSpec.FreightTruckPose;
            SpawnPointMarker freightTruck = CreateSpawnPoint(
                "Freight Truck Spawn Point",
                freightYard.transform,
                SpawnPointId.FreightTruck,
                truckPose.position,
                truckPose.rotation);
            Vector3 palletPosition = truckPose.position +
                                     truckPose.rotation *
                                     GetFreightTruckPalletSlotLocalPosition(0);
            SpawnPointMarker inboundPallet = CreateSpawnPoint(
                "Inbound Pallet Spawn Point",
                freightYard.transform,
                SpawnPointId.InboundPallet,
                palletPosition,
                truckPose.rotation * Quaternion.Euler(0f, -90f, 0f));

            return (forklift, freightTruck, inboundPallet, stagingZone);
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

            Pose spawnPose = PrototypeYardLayoutSpec.PlatformTrolleySpawnPose;
            SpawnPointMarker spawnPoint = CreateSpawnPoint(
                "Platform Trolley Spawn",
                station.transform,
                SpawnPointId.PlatformTrolley,
                spawnPose.position,
                spawnPose.rotation);
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
                new(1.45f, 1.8f, 4.5f), new(8.55f, 1.8f, 4.5f),
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
                CreateCube($"Upper Pallet Beam {row + 1} A", storage.transform,
                    new Vector3(4.3f, 1.55f, z - 0.22f),
                    new Vector3(5.8f, 0.18f, 0.18f), timber);
                CreateCube($"Upper Pallet Beam {row + 1} B", storage.transform,
                    new Vector3(4.3f, 1.55f, z + 0.22f),
                    new Vector3(5.8f, 0.18f, 0.18f), timber);
            }

            GameObject slotsRoot = CreateEmpty("Storage Slots", storage.transform);
            Transform[] slots = new Transform[StorageSlotCapacity];
            const int slotsPerRow = 3;
            const int rowsPerLevel = 3;
            const int slotsPerLevel = slotsPerRow * rowsPerLevel;
            for (int i = 0; i < slots.Length; i++)
            {
                int levelIndex = i / slotsPerLevel;
                int levelSlotIndex = i % slotsPerLevel;
                int column = levelSlotIndex % slotsPerRow;
                int row = levelSlotIndex / slotsPerRow;
                GameObject slot = CreateEmpty($"Stock Slot {i + 1}", slotsRoot.transform);
                slot.transform.position = new Vector3(
                    2.4f + column * 1.9f,
                    0.68f + levelIndex * 1.27f,
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
            storage.transform.position = PrototypeYardLayoutSpec.StorageOffset;
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

        private static CustomerFlowLayoutMarker BuildCustomerFlowLayout(
            Transform parent,
            Material asphalt,
            Material white,
            Material loadingGreen)
        {
            GameObject customerVehiclePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(CustomerVehiclePrefabPath);
            if (customerVehiclePrefab == null)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle prefab is missing at " +
                    $"'{CustomerVehiclePrefabPath}'.");
            }

            GameObject traffic = CreateEmpty("Customer Vehicle Traffic", parent);
            CreateCube(
                "Customer Parallel Parking Pocket",
                traffic.transform,
                new Vector3(
                    0f,
                    0.015f,
                    PrototypeYardLayoutSpec.ParallelParkingZ),
                new Vector3(69f, 0.03f, 3.4f),
                asphalt,
                false);
            for (int dividerIndex = 0;
                 dividerIndex <= PrototypeYardLayoutSpec.VisualParkingCount;
                 dividerIndex++)
            {
                CreateCube(
                    $"Parallel Parking Divider {dividerIndex + 1}",
                    traffic.transform,
                    new Vector3(-34.5f + dividerIndex * 6.9f, 0.035f,
                        PrototypeYardLayoutSpec.ParallelParkingZ),
                    new Vector3(0.12f, 0.04f, 3.2f),
                    white,
                    false);
            }
            for (int activeIndex = 0;
                 activeIndex < PrototypeYardLayoutSpec.ActiveParkingCount;
                 activeIndex++)
            {
                float parkingX = PrototypeYardLayoutSpec.GetActiveParkingX(activeIndex);
                CreateCube(
                    $"Active Parking Marker {activeIndex + 1}",
                    traffic.transform,
                    new Vector3(parkingX, 0.045f, -21.38f),
                    new Vector3(5.8f, 0.04f, 0.12f),
                    loadingGreen,
                    false);
            }
            CreateWorldLabel(
                "Customer Parking Label",
                traffic.transform,
                LocalizationKey.WorldCustomerParking,
                new Vector3(-3.45f, 0.045f, -20.85f),
                Quaternion.Euler(90f, 0f, 0f),
                0.03f,
                white.color);

            CreateCube(
                "Customer Loading Pad",
                traffic.transform,
                new Vector3(
                    PrototypeYardLayoutSpec.CustomerLoadingX,
                    0.015f,
                    PrototypeYardLayoutSpec.CustomerLoadingZ),
                new Vector3(4f, 0.03f, 10f),
                asphalt,
                false);
            CreateCube("Loading Stripe Left", traffic.transform,
                new Vector3(3.25f, 0.035f, PrototypeYardLayoutSpec.CustomerLoadingZ),
                new Vector3(0.12f, 0.04f, 9.4f), loadingGreen, false);
            CreateCube("Loading Stripe Right", traffic.transform,
                new Vector3(6.75f, 0.035f, PrototypeYardLayoutSpec.CustomerLoadingZ),
                new Vector3(0.12f, 0.04f, 9.4f), loadingGreen, false);
            CreateCube("Loading Stop Stripe", traffic.transform,
                new Vector3(PrototypeYardLayoutSpec.CustomerLoadingX, 0.04f, 5.25f),
                new Vector3(3.5f, 0.05f, 0.18f), loadingGreen, false);
            CreateWorldLabel("Customer Loading Bay Label", traffic.transform,
                LocalizationKey.WorldCustomerLoadingBay,
                new Vector3(PrototypeYardLayoutSpec.CustomerLoadingX, 0.045f, 15.55f),
                Quaternion.Euler(90f, 0f, 0f),
                0.03f, loadingGreen.color);
            CreateCube(
                "Future Customer Loading Bay Reserve",
                traffic.transform,
                new Vector3(9.5f, 0.025f, PrototypeYardLayoutSpec.CustomerLoadingZ),
                new Vector3(3.8f, 0.02f, 10f),
                white,
                false);

            Pose[] queuePoses = PrototypeYardLayoutSpec.BuildQueuePoses();
            Transform[] queueWaypoints = CreateWaypointTransforms(
                "Customer Queue",
                traffic.transform,
                queuePoses);
            Pose[] queueAbandonExitRoute =
                PrototypeYardLayoutSpec.BuildQueueAbandonExitRoute();
            Transform[] queueAbandonExitWaypoints = CreateWaypointTransforms(
                "Customer Queue Abandon Exit Route",
                traffic.transform,
                queueAbandonExitRoute);

            Vector3[] loadingDepartureVisualPositions =
                PrototypeYardLayoutSpec.BuildLoadingDepartureVisualPositions();
            Pose[] loadingDepartureRoute =
                CustomerVehicleProviderPoseUtility.BuildProviderRoute(
                    customerVehiclePrefab,
                    loadingDepartureVisualPositions);
            Transform[] loadingDepartureWaypoints = CreateWaypointTransforms(
                "Customer Loading Departure Route",
                traffic.transform,
                loadingDepartureRoute);

            var spotMarkers = new CustomerParkingSpotLayoutMarker[
                PrototypeYardLayoutSpec.ActiveParkingCount];
            for (int index = 0; index < spotMarkers.Length; index++)
            {
                float parkingX = PrototypeYardLayoutSpec.GetActiveParkingX(index);
                Vector3[] arrivalPositions =
                    PrototypeYardLayoutSpec.BuildArrivalVisualPositions(parkingX);
                Pose[] vehicleArrivalRoute =
                    CustomerVehicleProviderPoseUtility.BuildProviderRoute(
                        customerVehiclePrefab,
                        arrivalPositions);

                Vector3[] loadingPositions =
                    PrototypeYardLayoutSpec.BuildToLoadingVisualPositions(parkingX);
                Pose[] vehicleToLoadingRoute =
                    CustomerVehicleProviderPoseUtility.BuildProviderRoute(
                        customerVehiclePrefab,
                        loadingPositions);

                Vector3[] parkingDeparturePositions =
                    PrototypeYardLayoutSpec.BuildParkingDepartureVisualPositions(parkingX);
                Pose[] vehicleParkingDepartureRoute =
                    CustomerVehicleProviderPoseUtility.BuildProviderRoute(
                        customerVehiclePrefab,
                        parkingDeparturePositions);
                Pose[] customerApproachRoute =
                    PrototypeYardLayoutSpec.BuildCustomerApproachRoute(
                        parkingX,
                        queuePoses[^1]);
                Pose[] customerReturnRoute =
                    PrototypeYardLayoutSpec.BuildCustomerReturnRoute(
                        parkingX,
                        queuePoses[0],
                        queueAbandonExitRoute[^1]);

                GameObject spotObject = CreateEmpty(
                    $"Customer Parking Spot {index + 1}",
                    traffic.transform);
                CustomerParkingSpotLayoutMarker spotMarker =
                    spotObject.AddComponent<CustomerParkingSpotLayoutMarker>();
                spotMarker.Configure(
                    index,
                    CreateWaypointTransforms(
                        "Vehicle Arrival Route",
                        spotObject.transform,
                        vehicleArrivalRoute),
                    CreateWaypointTransforms(
                        "Vehicle To Loading Route",
                        spotObject.transform,
                        vehicleToLoadingRoute),
                    CreateWaypointTransforms(
                        "Vehicle Parking Departure Route",
                        spotObject.transform,
                        vehicleParkingDepartureRoute),
                    CreateWaypointTransforms(
                        "Customer Approach Route",
                        spotObject.transform,
                        customerApproachRoute),
                    CreateWaypointTransforms(
                        "Customer Return Route",
                        spotObject.transform,
                        customerReturnRoute));
                spotMarkers[index] = spotMarker;
            }

            CustomerFlowLayoutMarker marker =
                traffic.AddComponent<CustomerFlowLayoutMarker>();
            marker.Configure(
                spotMarkers,
                queueWaypoints,
                queueAbandonExitWaypoints,
                loadingDepartureWaypoints);
            return marker;
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
            lumber.transform.position = PrototypeYardLayoutSpec.LumberOffset;
        }

        private static SpawnPointMarker BuildInboundDeliveryBay(Transform parent, Material asphalt,
            Material inboundYellow)
        {
            GameObject bay = CreateEmpty("Inbound Delivery Bay", parent);
            Pose deliveryPose = PrototypeYardLayoutSpec.DeliveryVehiclePose;
            CreateCube("Inbound Bay Surface", bay.transform,
                new Vector3(deliveryPose.position.x, 0.015f, deliveryPose.position.z),
                new Vector3(5f, 0.03f, 7.5f), asphalt, false);

            for (int index = 0; index < 6; index++)
            {
                CreateCube($"Inbound Marking {index + 1}", bay.transform,
                    new Vector3(
                        deliveryPose.position.x - 2.25f + index % 2 * 4.5f,
                        0.035f,
                        deliveryPose.position.z - 2.7f + index / 2 * 2.7f),
                    new Vector3(0.14f, 0.04f, 1.4f), inboundYellow, false);
            }

            CreateWorldLabel("Inbound Label", bay.transform,
                LocalizationKey.WorldDeliveryIntake,
                new Vector3(deliveryPose.position.x, 0.04f, deliveryPose.position.z - 3.85f),
                Quaternion.Euler(90f, 0f, 0f),
                0.035f, inboundYellow.color);

            return CreateSpawnPoint(
                "Delivery Vehicle Spawn Point",
                bay.transform,
                SpawnPointId.DeliveryVehicle,
                deliveryPose.position,
                deliveryPose.rotation);
        }

        private static (SpawnPointMarker Idle, SpawnPointMarker DeliveryAccess,
                SpawnPointMarker StorageAccess,
                SpawnPointMarker CustomerLoadingAccess,
                SpawnPointMarker WorkerTrolleyHome,
                SpawnPointMarker WorkerTrolleyCustomerLoadingAccess,
                SpawnPointMarker WorkerInboundTrolleyStorageBypass,
                SpawnPointMarker WorkerInboundTrolleyStorageAccess,
                SpawnPointMarker WorkerOutboundTrolleyStorageApproach,
                SpawnPointMarker WorkerOutboundTrolleyStorageAccess)
            BuildWarehouseWorkerAccessPoints(Transform parent)
        {
            GameObject root = CreateEmpty("Warehouse Worker Access Points", parent);
            Pose idlePose = PrototypeYardLayoutSpec.WarehouseWorkerIdlePose;
            SpawnPointMarker idle = CreateSpawnPoint(
                "Warehouse Worker Idle",
                root.transform,
                SpawnPointId.WarehouseWorker,
                idlePose.position,
                idlePose.rotation);
            Pose deliveryAccessPose =
                PrototypeYardLayoutSpec.WarehouseWorkerDeliveryAccessPose;
            SpawnPointMarker deliveryAccess = CreateSpawnPoint(
                "Warehouse Worker Delivery Access",
                root.transform,
                SpawnPointId.WarehouseWorkerDeliveryAccess,
                deliveryAccessPose.position,
                deliveryAccessPose.rotation);
            Pose storageAccessPose =
                PrototypeYardLayoutSpec.WarehouseWorkerStorageAccessPose;
            SpawnPointMarker storageAccess = CreateSpawnPoint(
                "Warehouse Worker Storage Access",
                root.transform,
                SpawnPointId.WarehouseWorkerStorageAccess,
                storageAccessPose.position,
                storageAccessPose.rotation);
            Pose customerLoadingAccessPose =
                PrototypeYardLayoutSpec.WarehouseWorkerCustomerLoadingAccessPose;
            SpawnPointMarker customerLoadingAccess = CreateSpawnPoint(
                "Warehouse Worker Customer Loading Access",
                root.transform,
                SpawnPointId.WarehouseWorkerCustomerLoadingAccess,
                customerLoadingAccessPose.position,
                customerLoadingAccessPose.rotation);
            Pose trolleyHomePose = PrototypeYardLayoutSpec.WorkerTrolleyHomePose;
            SpawnPointMarker workerTrolleyHome = CreateSpawnPoint(
                "Warehouse Worker Trolley Home",
                root.transform,
                SpawnPointId.WarehouseWorkerTrolley,
                trolleyHomePose.position,
                trolleyHomePose.rotation);
            Pose trolleyCustomerLoadingPose =
                PrototypeYardLayoutSpec.WorkerTrolleyCustomerLoadingAccessPose;
            SpawnPointMarker workerTrolleyCustomerLoadingAccess = CreateSpawnPoint(
                "Warehouse Worker Trolley Customer Loading Access",
                root.transform,
                SpawnPointId.WarehouseWorkerTrolleyCustomerLoadingAccess,
                trolleyCustomerLoadingPose.position,
                trolleyCustomerLoadingPose.rotation);
            Pose inboundTrolleyStorageBypassPose =
                PrototypeYardLayoutSpec.WorkerInboundTrolleyStorageBypassPose;
            SpawnPointMarker workerInboundTrolleyStorageBypass = CreateSpawnPoint(
                "Warehouse Worker Inbound Trolley Storage Bypass",
                root.transform,
                SpawnPointId.WarehouseWorkerInboundTrolleyStorageBypass,
                inboundTrolleyStorageBypassPose.position,
                inboundTrolleyStorageBypassPose.rotation);
            Pose inboundTrolleyStorageAccessPose =
                PrototypeYardLayoutSpec.WorkerInboundTrolleyStorageAccessPose;
            SpawnPointMarker workerInboundTrolleyStorageAccess = CreateSpawnPoint(
                "Warehouse Worker Inbound Trolley Storage Access",
                root.transform,
                SpawnPointId.WarehouseWorkerInboundTrolleyStorageAccess,
                inboundTrolleyStorageAccessPose.position,
                inboundTrolleyStorageAccessPose.rotation);
            Pose outboundTrolleyStorageApproachPose =
                PrototypeYardLayoutSpec.WorkerOutboundTrolleyStorageApproachPose;
            SpawnPointMarker workerOutboundTrolleyStorageApproach = CreateSpawnPoint(
                "Warehouse Worker Outbound Trolley Storage Approach",
                root.transform,
                SpawnPointId.WarehouseWorkerOutboundTrolleyStorageApproach,
                outboundTrolleyStorageApproachPose.position,
                outboundTrolleyStorageApproachPose.rotation);
            Pose outboundTrolleyStorageAccessPose =
                PrototypeYardLayoutSpec.WorkerOutboundTrolleyStorageAccessPose;
            SpawnPointMarker workerOutboundTrolleyStorageAccess = CreateSpawnPoint(
                "Warehouse Worker Outbound Trolley Storage Access",
                root.transform,
                SpawnPointId.WarehouseWorkerOutboundTrolleyStorageAccess,
                outboundTrolleyStorageAccessPose.position,
                outboundTrolleyStorageAccessPose.rotation);
            return (idle, deliveryAccess, storageAccess, customerLoadingAccess,
                workerTrolleyHome, workerTrolleyCustomerLoadingAccess,
                workerInboundTrolleyStorageBypass,
                workerInboundTrolleyStorageAccess,
                workerOutboundTrolleyStorageApproach,
                workerOutboundTrolleyStorageAccess);
        }

        private static SpawnPointMarker CreateSpawnPoint(
            string name,
            Transform parent,
            SpawnPointId id,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject prefab = LoadRequiredPrefab(SpawnPointPrefabPath);
            GameObject spawnPoint = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject ??
                                    throw new InvalidOperationException(
                                        $"Could not instantiate spawn point prefab " +
                                        $"'{SpawnPointPrefabPath}'.");
            spawnPoint.name = name;
            spawnPoint.transform.SetPositionAndRotation(position, rotation);
            EditorGUIUtility.SetIconForObject(spawnPoint, GetSpawnPointIcon());
            SpawnPointMarker marker = spawnPoint.GetComponent<SpawnPointMarker>() ??
                                      throw new InvalidOperationException(
                                          $"Spawn point prefab '{SpawnPointPrefabPath}' has no " +
                                          $"{nameof(SpawnPointMarker)} component.");
            marker.Configure(id);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawnPoint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawnPoint.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            return marker;
        }

        private static SpawnPointMarker BuildPlayerSpawnPoint() =>
            CreateSpawnPoint(
                "Player Spawn Point",
                null,
                SpawnPointId.Player,
                new Vector3(-9f, 0.02f, -2.1f),
                Quaternion.identity);

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

        private static void EnsureBrickProductPrefab(ProductConfig productConfig,
            Material brickMaterial, Material strapMaterial)
        {
            GameObject product = CreateEmpty("Brick Pack");

            try
            {
                product.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = Vector3.one;
                product.SetActive(true);

                InteractionHighlight highlight = null;
                for (int layerIndex = 0; layerIndex < 2; layerIndex++)
                for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                {
                    GameObject brick = CreateCube(
                        $"Brick {layerIndex * 3 + columnIndex + 1}",
                        product.transform,
                        new Vector3(
                            -0.27f + columnIndex * 0.27f,
                            -0.08f + layerIndex * 0.16f,
                            0f),
                        new Vector3(0.24f, 0.14f, 0.48f),
                        brickMaterial,
                        false,
                        true);
                    if (highlight == null)
                        highlight = brick.AddComponent<InteractionHighlight>();
                }

                CreateCube("Left Strap", product.transform, new Vector3(-0.18f, 0f, 0f),
                    new Vector3(0.05f, 0.34f, 0.5f), strapMaterial, false, true);
                CreateCube("Right Strap", product.transform, new Vector3(0.18f, 0f, 0f),
                    new Vector3(0.05f, 0.34f, 0.5f), strapMaterial, false, true);

                FinalizeBoxProductPrefab(
                    product,
                    productConfig,
                    Vector3.zero,
                    new Vector3(0.82f, 0.34f, 0.52f),
                    new Vector3(0f, 0.35f, 0f),
                    new Vector3(1.45f, 1.8f, 1.15f),
                    highlight,
                    BrickProductPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void EnsureDrywallProductPrefab(ProductConfig productConfig,
            Material drywallMaterial, Material edgeMaterial)
        {
            GameObject product = CreateEmpty("Drywall Sheet");

            try
            {
                product.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = Vector3.one;
                product.SetActive(true);

                InteractionHighlight highlight = null;
                for (int index = 0; index < 3; index++)
                {
                    GameObject sheet = CreateCube(
                        $"Drywall Layer {index + 1}",
                        product.transform,
                        new Vector3(0f, -0.05f + index * 0.05f, 0f),
                        new Vector3(1.52f, 0.04f, 0.42f),
                        drywallMaterial,
                        false,
                        true);
                    if (highlight == null)
                        highlight = sheet.AddComponent<InteractionHighlight>();
                }

                CreateCube("Front Edge", product.transform, new Vector3(0f, 0f, -0.215f),
                    new Vector3(1.55f, 0.17f, 0.025f), edgeMaterial, false, true);
                CreateCube("Back Edge", product.transform, new Vector3(0f, 0f, 0.215f),
                    new Vector3(1.55f, 0.17f, 0.025f), edgeMaterial, false, true);

                FinalizeBoxProductPrefab(
                    product,
                    productConfig,
                    Vector3.zero,
                    new Vector3(1.55f, 0.18f, 0.46f),
                    new Vector3(0f, 0.35f, 0f),
                    new Vector3(2.15f, 1.75f, 1.15f),
                    highlight,
                    DrywallProductPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void EnsurePaintProductPrefab(ProductConfig productConfig,
            Material bucketMaterial, Material lidMaterial, Material handleMaterial)
        {
            GameObject product = CreateEmpty("Paint Bucket");

            try
            {
                product.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = Vector3.one;
                product.SetActive(true);

                GameObject body = CreateVisualCylinder(
                    "Bucket Body",
                    product.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(0.48f, 0.26f, 0.48f),
                    bucketMaterial);
                InteractionHighlight highlight = body.AddComponent<InteractionHighlight>();
                CreateVisualCylinder(
                    "Bucket Lid",
                    product.transform,
                    new Vector3(0f, 0.265f, 0f),
                    Quaternion.identity,
                    new Vector3(0.52f, 0.025f, 0.52f),
                    lidMaterial);
                CreateCube("Handle Left", product.transform, new Vector3(-0.22f, 0.08f, 0f),
                    new Vector3(0.025f, 0.28f, 0.025f), handleMaterial, false, true);
                CreateCube("Handle Right", product.transform, new Vector3(0.22f, 0.08f, 0f),
                    new Vector3(0.025f, 0.28f, 0.025f), handleMaterial, false, true);
                CreateCube("Handle Grip", product.transform, new Vector3(0f, 0.22f, 0f),
                    new Vector3(0.46f, 0.025f, 0.025f), handleMaterial, false, true);

                FinalizeBoxProductPrefab(
                    product,
                    productConfig,
                    Vector3.zero,
                    new Vector3(0.52f, 0.58f, 0.52f),
                    new Vector3(0f, 0.35f, 0f),
                    new Vector3(1.25f, 1.8f, 1.25f),
                    highlight,
                    PaintProductPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void EnsureInsulationProductPrefab(ProductConfig productConfig,
            Material insulationMaterial, Material strapMaterial)
        {
            GameObject product = CreateEmpty("Insulation Roll");

            try
            {
                product.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                product.transform.localScale = Vector3.one;
                product.SetActive(true);

                GameObject roll = CreateVisualCylinder(
                    "Insulation Roll Visual",
                    product.transform,
                    Vector3.zero,
                    Quaternion.Euler(0f, 0f, 90f),
                    new Vector3(0.56f, 0.55f, 0.56f),
                    insulationMaterial);
                InteractionHighlight highlight = roll.AddComponent<InteractionHighlight>();
                CreateCube("Left Strap", product.transform, new Vector3(-0.3f, 0f, 0f),
                    new Vector3(0.055f, 0.58f, 0.58f), strapMaterial, false, true);
                CreateCube("Right Strap", product.transform, new Vector3(0.3f, 0f, 0f),
                    new Vector3(0.055f, 0.58f, 0.58f), strapMaterial, false, true);

                FinalizeBoxProductPrefab(
                    product,
                    productConfig,
                    Vector3.zero,
                    new Vector3(1.15f, 0.58f, 0.58f),
                    new Vector3(0f, 0.35f, 0f),
                    new Vector3(1.75f, 1.8f, 1.25f),
                    highlight,
                    InsulationProductPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(product);
            }
        }

        private static void FinalizeBoxProductPrefab(
            GameObject product,
            ProductConfig productConfig,
            Vector3 solidColliderCenter,
            Vector3 solidColliderSize,
            Vector3 interactionCenter,
            Vector3 interactionSize,
            InteractionHighlight highlight,
            string prefabPath)
        {
            if (highlight == null)
                throw new ArgumentNullException(nameof(highlight));

            Rigidbody body = product.AddComponent<Rigidbody>();
            body.mass = productConfig.Mass;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = productConfig.WorldInterpolation;
            body.collisionDetectionMode = productConfig.WorldCollisionDetection;

            BoxCollider solidCollider = product.AddComponent<BoxCollider>();
            solidCollider.center = solidColliderCenter;
            solidCollider.size = solidColliderSize;

            GameObject interactionArea = CreateEmpty("Interaction Area", product.transform);
            BoxCollider interactionCollider = interactionArea.AddComponent<BoxCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.center = interactionCenter;
            interactionCollider.size = interactionSize;

            InteractionView interactionView = product.AddComponent<InteractionView>();
            interactionView.Configure(highlight);
            product.AddComponent<TransformRegistrar>();
            product.AddComponent<InteractionViewRegistrar>();
            product.AddComponent<RigidbodyRegistrar>();
            product.AddComponent<CollidersRegistrar>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(product, prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Could not create product prefab at {prefabPath}.");

            EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                         throw new InvalidOperationException(
                                             $"Product prefab at {prefabPath} has no view root.");
            AssignViewPrefab(productConfig, prefabView);
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
                int cargoSlotCapacity = checked(
                    deliveryConfigs.Max(config => config.ProductCount) *
                    ProcurementCartFactory.CurrentDeliveryPackageCapacity);
                Transform[] cargoSlots = new Transform[cargoSlotCapacity];
                for (int index = 0; index < cargoSlots.Length; index++)
                {
                    GameObject slot = CreateEmpty($"Cargo Slot {index + 1}", slotsRoot.transform);
                    int levelIndex = index / 3;
                    int positionIndex = index % 3;
                    slot.transform.localPosition = new Vector3(
                        0f,
                        1.5f + levelIndex * 0.67f,
                        -1.8f + positionIndex * 0.92f);
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
                body.isKinematic = false;
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.FreezeRotationX |
                                   RigidbodyConstraints.FreezeRotationZ;
                body.linearDamping = 0.35f;
                body.angularDamping = 2.5f;
                body.centerOfMass = new Vector3(0f, 0.58f, -0.15f);

                NavMeshObstacle obstacle = vehicle.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0f, 1f, -0.15f);
                obstacle.size = new Vector3(4f, 2f, 6.4f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.carvingMoveThreshold = 0.05f;
                obstacle.carvingTimeToStationary = 0.1f;

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

                Vector3 customerWheelScale = new(0.82f, 0.19f, 0.82f);
                CreateLocalWheel("Front Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.41f, 1.6f),
                    darkMetal, customerWheelScale);
                CreateLocalWheel("Front Right Wheel", vehicle.transform, new Vector3(1.12f, 0.41f, 1.6f),
                    darkMetal, customerWheelScale);
                CreateLocalWheel("Rear Left Wheel", vehicle.transform, new Vector3(-1.12f, 0.41f, -2.05f),
                    darkMetal, customerWheelScale);
                CreateLocalWheel("Rear Right Wheel", vehicle.transform, new Vector3(1.12f, 0.41f, -2.05f),
                    darkMetal, customerWheelScale);

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

                CapsuleCollider trafficCollider =
                    customer.AddComponent<CapsuleCollider>();
                trafficCollider.center = new Vector3(0f, 0.9f, 0f);
                trafficCollider.radius = 0.32f;
                trafficCollider.height = 1.8f;
                trafficCollider.direction = 1;

                GameObject torso = CreateCube(
                    "Torso", customer.transform, new Vector3(0f, 1.18f, 0f),
                    new Vector3(0.62f, 0.78f, 0.34f), jacket, false, true);
                GameObject vest = CreateCube(
                    "Work Vest", customer.transform, new Vector3(0f, 1.2f, -0.18f),
                    new Vector3(0.66f, 0.54f, 0.05f), workwear, false, true);
                GameObject head = CreateCube(
                    "Head", customer.transform, new Vector3(0f, 1.82f, 0f),
                    new Vector3(0.38f, 0.38f, 0.38f), jacket, false, true);

                GameObject leftShoulder = CreateEmpty("Left Shoulder", customer.transform);
                leftShoulder.transform.localPosition = new Vector3(-0.42f, 1.46f, 0f);
                GameObject leftArm = CreateCube(
                    "Left Arm", leftShoulder.transform, new Vector3(0f, -0.28f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), jacket, false, true);
                GameObject rightShoulder = CreateEmpty("Right Shoulder", customer.transform);
                rightShoulder.transform.localPosition = new Vector3(0.42f, 1.46f, 0f);
                GameObject rightArm = CreateCube(
                    "Right Arm", rightShoulder.transform, new Vector3(0f, -0.28f, 0f),
                    new Vector3(0.16f, 0.72f, 0.18f), jacket, false, true);

                GameObject leftLeg = CreateCube(
                    "Left Leg", customer.transform, new Vector3(-0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                GameObject rightLeg = CreateCube(
                    "Right Leg", customer.transform, new Vector3(0.17f, 0.48f, 0f),
                    new Vector3(0.22f, 0.72f, 0.24f), workwear, false, true);
                GameObject leftShoe = CreateCube(
                    "Left Shoe", customer.transform, new Vector3(-0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);
                GameObject rightShoe = CreateCube(
                    "Right Shoe", customer.transform, new Vector3(0.17f, 0.11f, 0.08f),
                    new Vector3(0.24f, 0.14f, 0.4f), shoes, false, true);

                GameObject warningObject = CreateEmpty(
                    "Dissatisfaction Label", customer.transform);
                warningObject.transform.localPosition = new Vector3(0f, 2.35f, 0f);
                TextMesh warningLabel = warningObject.AddComponent<TextMesh>();
                warningLabel.text = RussianPreviewLocalization.Resolve(
                    LocalizedTexts.Text(LocalizationKey.WorldCustomerDissatisfied));
                warningLabel.anchor = TextAnchor.MiddleCenter;
                warningLabel.alignment = TextAlignment.Center;
                warningLabel.characterSize = 0.045f;
                warningLabel.fontSize = 64;
                warningLabel.fontStyle = FontStyle.Bold;
                warningLabel.color = new Color(1f, 0.08f, 0.04f, 1f);
                warningObject.SetActive(false);

                Renderer[] moodRenderers =
                {
                    torso.GetComponent<Renderer>(),
                    vest.GetComponent<Renderer>(),
                    head.GetComponent<Renderer>(),
                    leftArm.GetComponent<Renderer>(),
                    rightArm.GetComponent<Renderer>(),
                    leftLeg.GetComponent<Renderer>(),
                    rightLeg.GetComponent<Renderer>(),
                    leftShoe.GetComponent<Renderer>(),
                    rightShoe.GetComponent<Renderer>()
                };

                customer.AddComponent<EntityBehaviour>();
                customer.AddComponent<TransformRegistrar>();
                customer.AddComponent<RigidbodyRegistrar>();
                customer.AddComponent<CollidersRegistrar>();
                CustomerDissatisfactionView moodView =
                    customer.AddComponent<CustomerDissatisfactionView>();
                moodView.Configure(
                    moodRenderers,
                    leftShoulder.transform,
                    rightShoulder.transform,
                    warningLabel);
                customer.AddComponent<CustomerDissatisfactionViewRegistrar>();

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

                Rigidbody body = worker.AddComponent<Rigidbody>();
                body.mass = 80f;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                body.constraints = RigidbodyConstraints.FreezeRotationX |
                                   RigidbodyConstraints.FreezeRotationZ;

                GameObject trafficColliderObject = CreateEmpty(
                    "Traffic Collider", worker.transform);
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer < 0)
                    throw new InvalidOperationException(
                        "Required Ignore Raycast layer is missing.");
                trafficColliderObject.layer = ignoreRaycastLayer;
                CapsuleCollider trafficCollider =
                    trafficColliderObject.AddComponent<CapsuleCollider>();
                trafficCollider.center = new Vector3(0f, 0.95f, 0f);
                trafficCollider.radius = 0.32f;
                trafficCollider.height = 1.9f;
                trafficCollider.direction = 1;

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
                worker.AddComponent<RigidbodyRegistrar>();
                worker.AddComponent<CollidersRegistrar>();
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

        private static void EnsureWarehouseWorkerTrolleyPrefab(
            WarehouseWorkerConfig config,
            Material workwearBlue,
            Material safetyYellow,
            Material darkMetal,
            Material timber)
        {
            GameObject trolley = CreateEmpty("Warehouse Worker Trolley");

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
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;

                CreateCube(
                    "Deck", trolley.transform, new Vector3(0f, 0.42f, 0f),
                    new Vector3(1.9f, 0.18f, 2.4f), workwearBlue, false, true);
                CreateCube(
                    "Deck Inlay", trolley.transform, new Vector3(0f, 0.53f, 0f),
                    new Vector3(1.62f, 0.05f, 2.08f), timber, false, true);
                CreateCube(
                    "Left Rail", trolley.transform, new Vector3(-0.9f, 0.68f, 0f),
                    new Vector3(0.1f, 0.52f, 2.35f), safetyYellow, false, true);
                CreateCube(
                    "Right Rail", trolley.transform, new Vector3(0.9f, 0.68f, 0f),
                    new Vector3(0.1f, 0.52f, 2.35f), safetyYellow, false, true);
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
                    new Vector3(1.55f, 0.12f, 0.12f), safetyYellow, false, true);

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

                GameObject bodyColliderObject = CreateEmpty(
                    "Body Collider", trolley.transform);
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer < 0)
                {
                    throw new InvalidOperationException(
                        "Required Ignore Raycast layer is missing.");
                }
                bodyColliderObject.layer = ignoreRaycastLayer;
                BoxCollider bodyCollider = bodyColliderObject.AddComponent<BoxCollider>();
                bodyCollider.center = new Vector3(0f, 0.27f, 0.15f);
                bodyCollider.size = new Vector3(2f, 0.5f, 2.1f);

                GameObject slotsRoot = CreateEmpty("Cargo Slots", trolley.transform);
                var slots = new Transform[config.TrolleyCapacity];
                for (int index = 0; index < slots.Length; index++)
                {
                    GameObject slot = CreateEmpty(
                        $"Cargo Slot {index + 1}", slotsRoot.transform);
                    slot.transform.localPosition =
                        new Vector3(0f, 0.66f, -0.66f + index * 0.66f);
                    slots[index] = slot.transform;
                }

                trolley.AddComponent<EntityBehaviour>();
                trolley.AddComponent<TransformRegistrar>();
                trolley.AddComponent<RigidbodyRegistrar>();
                trolley.AddComponent<CollidersRegistrar>();
                SlotsRegistrar slotsRegistrar = trolley.AddComponent<SlotsRegistrar>();
                slotsRegistrar.Configure(slots);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    trolley, WarehouseWorkerTrolleyPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create warehouse worker trolley prefab at " +
                        $"{WarehouseWorkerTrolleyPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Warehouse worker trolley prefab at " +
                                                 $"{WarehouseWorkerTrolleyPrefabPath} has no " +
                                                 "EntityBehaviour root.");
                SerializedObject serializedConfig = new(config);
                RequireSerializedProperty(serializedConfig, "_trolleyViewPrefab")
                    .objectReferenceValue = prefabView;
                serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(trolley);
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

                NavMeshObstacle navigationObstacle =
                    trolley.AddComponent<NavMeshObstacle>();
                navigationObstacle.shape = NavMeshObstacleShape.Box;
                navigationObstacle.center = new Vector3(0f, 0.27f, 0.15f);
                navigationObstacle.size = new Vector3(2f, 0.5f, 2.1f);
                navigationObstacle.carving = true;
                navigationObstacle.carveOnlyStationary = true;
                navigationObstacle.carvingMoveThreshold = 0.05f;
                navigationObstacle.carvingTimeToStationary = 0.1f;

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
                trolley.AddComponent<NavMeshObstacleRegistrar>();
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

        private static void EnsureForkliftPrefab(
            ForkliftConfig config,
            Material safetyYellow,
            Material darkMetal,
            Material glass)
        {
            const float initialForkHeight = 0.15f;
            GameObject forklift = CreateEmpty("Forklift");

            try
            {
                forklift.transform.SetLocalPositionAndRotation(
                    Vector3.zero, Quaternion.identity);
                forklift.transform.localScale = Vector3.one;
                forklift.SetActive(true);

                Rigidbody body = forklift.AddComponent<Rigidbody>();
                body.mass = 3500f;
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                body.constraints = RigidbodyConstraints.FreezeRotationX |
                                   RigidbodyConstraints.FreezeRotationZ;

                BoxCollider hull = forklift.AddComponent<BoxCollider>();
                hull.center = new Vector3(0f, 1.05f, -0.25f);
                hull.size = new Vector3(1.85f, 2.1f, 3.1f);

                GameObject bodyVisual = CreateCube(
                    "Body",
                    forklift.transform,
                    new Vector3(0f, 0.82f, -0.35f),
                    new Vector3(1.75f, 1.05f, 1.85f),
                    safetyYellow,
                    false,
                    true);
                InteractionHighlight highlight =
                    bodyVisual.AddComponent<InteractionHighlight>();
                CreateCube(
                    "Counterweight",
                    forklift.transform,
                    new Vector3(0f, 0.78f, -1.28f),
                    new Vector3(1.8f, 1.15f, 0.65f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Overhead Guard",
                    forklift.transform,
                    new Vector3(0f, 2.25f, -0.35f),
                    new Vector3(1.7f, 0.12f, 1.7f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Guard Left",
                    forklift.transform,
                    new Vector3(-0.72f, 1.55f, -0.35f),
                    new Vector3(0.12f, 1.5f, 1.55f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Guard Right",
                    forklift.transform,
                    new Vector3(0.72f, 1.55f, -0.35f),
                    new Vector3(0.12f, 1.5f, 1.55f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Windshield",
                    forklift.transform,
                    new Vector3(0f, 1.65f, 0.43f),
                    new Vector3(1.25f, 0.9f, 0.06f),
                    glass,
                    false,
                    true);
                CreateCube(
                    "Mast Left",
                    forklift.transform,
                    new Vector3(-0.67f, 1.45f, 1.02f),
                    new Vector3(0.16f, 2.7f, 0.18f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Mast Right",
                    forklift.transform,
                    new Vector3(0.67f, 1.45f, 1.02f),
                    new Vector3(0.16f, 2.7f, 0.18f),
                    darkMetal,
                    false,
                    true);

                CreateLocalWheel(
                    "Front Left Wheel",
                    forklift.transform,
                    new Vector3(-0.92f, 0.48f, 0.72f),
                    darkMetal);
                CreateLocalWheel(
                    "Front Right Wheel",
                    forklift.transform,
                    new Vector3(0.92f, 0.48f, 0.72f),
                    darkMetal);
                CreateLocalWheel(
                    "Rear Left Wheel",
                    forklift.transform,
                    new Vector3(-0.92f, 0.42f, -1.02f),
                    darkMetal);
                CreateLocalWheel(
                    "Rear Right Wheel",
                    forklift.transform,
                    new Vector3(0.92f, 0.42f, -1.02f),
                    darkMetal);

                GameObject driverSeat = CreateEmpty(
                    "Driver Seat Anchor", forklift.transform);
                // The anchor stores the player root pose. The player camera sits
                // 1.65 m above it, so this keeps the first-person view below the
                // overhead guard instead of placing it inside the roof mesh.
                driverSeat.transform.localPosition = new Vector3(0f, 0.2f, -0.55f);
                driverSeat.AddComponent<DriverSeatAnchorRegistrar>();
                GameObject driverExit = CreateEmpty(
                    "Driver Exit Anchor", forklift.transform);
                driverExit.transform.localPosition = new Vector3(-1.65f, 0f, -0.35f);
                driverExit.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                driverExit.AddComponent<DriverExitAnchorRegistrar>();

                GameObject lift = CreateEmpty("Lift", forklift.transform);
                lift.transform.localPosition = new Vector3(0f, initialForkHeight, 0f);
                lift.AddComponent<LiftTransformRegistrar>();
                CreateCube(
                    "Lift Carriage",
                    lift.transform,
                    new Vector3(0f, 0.42f, 1.05f),
                    new Vector3(1.5f, 0.75f, 0.12f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Left Fork",
                    lift.transform,
                    new Vector3(-0.5f, 0.06f, 1.75f),
                    new Vector3(0.16f, 0.12f, 1.55f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Right Fork",
                    lift.transform,
                    new Vector3(0.5f, 0.06f, 1.75f),
                    new Vector3(0.16f, 0.12f, 1.55f),
                    darkMetal,
                    false,
                    true);
                GameObject cargoAnchor = CreateEmpty("Cargo Anchor", lift.transform);
                cargoAnchor.transform.localPosition = new Vector3(0f, 0.14f, 1.75f);
                cargoAnchor.AddComponent<CargoAnchorRegistrar>();
                GameObject cargoCollisionHull = CreateEmpty(
                    "Fork And Cargo Collision Hull", lift.transform);
                BoxCollider cargoCollider =
                    cargoCollisionHull.AddComponent<BoxCollider>();
                cargoCollider.center = new Vector3(0f, 0.25f, 1.75f);
                cargoCollider.size = new Vector3(1.3f, 0.5f, 1.1f);

                InteractionView interactionView =
                    forklift.AddComponent<InteractionView>();
                interactionView.Configure(highlight);
                forklift.AddComponent<TransformRegistrar>();
                forklift.AddComponent<RigidbodyRegistrar>();
                forklift.AddComponent<CollidersRegistrar>();
                forklift.AddComponent<InteractionViewRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    forklift, ForkliftPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create forklift prefab at {ForkliftPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Forklift prefab at {ForkliftPrefabPath} " +
                                                 "has no EntityBehaviour root.");
                config.Configure(
                    prefabView,
                    forwardSpeed: 4.5f,
                    reverseSpeed: 3f,
                    steeringSpeed: 70f,
                    liftSpeed: 1.25f,
                    minForkHeight: initialForkHeight,
                    maxForkHeight: 2.4f,
                    initialForkHeight: initialForkHeight,
                    transferDistance: 1.25f,
                    transferHeightTolerance: 0.35f,
                    transferMaxAlignmentAngle: 25f);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(forklift);
            }
        }

        private static void EnsureFreightTruckPrefab(
            FreightTruckConfig config,
            Material truckPaint,
            Material darkMetal,
            Material glass)
        {
            GameObject truck = CreateEmpty("Freight Truck");

            try
            {
                truck.transform.SetLocalPositionAndRotation(
                    Vector3.zero, Quaternion.identity);
                truck.transform.localScale = Vector3.one;
                truck.SetActive(true);

                CreateCube(
                    "Cab",
                    truck.transform,
                    new Vector3(0f, 1.45f, 3.75f),
                    new Vector3(2.55f, 2.55f, 2.3f),
                    truckPaint,
                    true,
                    true);
                CreateCube(
                    "Hood",
                    truck.transform,
                    new Vector3(0f, 0.95f, 5.3f),
                    new Vector3(2.45f, 1.1f, 1.2f),
                    truckPaint,
                    true,
                    true);
                CreateCube(
                    "Windshield",
                    truck.transform,
                    new Vector3(0f, 1.85f, 4.93f),
                    new Vector3(2.05f, 0.82f, 0.08f),
                    glass,
                    false,
                    true);
                CreateCube(
                    "Chassis",
                    truck.transform,
                    new Vector3(0f, 0.66f, 0f),
                    new Vector3(2.25f, 0.3f, 11f),
                    darkMetal,
                    true,
                    true);
                CreateCube(
                    "Trailer Deck",
                    truck.transform,
                    new Vector3(0f, 1f, -2f),
                    new Vector3(2.55f, 0.22f, 7.8f),
                    darkMetal,
                    false,
                    true);
                CreateCube(
                    "Trailer Left Rail",
                    truck.transform,
                    new Vector3(-1.2f, 1.48f, -2f),
                    new Vector3(0.14f, 0.85f, 7.8f),
                    truckPaint,
                    true,
                    true);
                const float trailerMinimumZ = -5.9f;
                const float trailerMaximumZ = 1.9f;
                const float loadingGateHalfWidth = 0.775f;
                float railSegmentStart = trailerMinimumZ;
                for (int index = 0; index < FreightPalletSlotCapacity; index++)
                {
                    float slotZ = GetFreightTruckPalletSlotLocalPosition(index).z;
                    float gateStart = Mathf.Max(
                        trailerMinimumZ,
                        slotZ - loadingGateHalfWidth);
                    if (gateStart > railSegmentStart)
                    {
                        CreateTrailerRailSegment(
                            truck.transform,
                            index + 1,
                            railSegmentStart,
                            gateStart,
                            truckPaint);
                    }

                    railSegmentStart = Mathf.Min(
                        trailerMaximumZ,
                        slotZ + loadingGateHalfWidth);
                }
                if (railSegmentStart < trailerMaximumZ)
                {
                    CreateTrailerRailSegment(
                        truck.transform,
                        FreightPalletSlotCapacity + 1,
                        railSegmentStart,
                        trailerMaximumZ,
                        truckPaint);
                }

                CreateLocalWheel(
                    "Front Left Wheel", truck.transform,
                    new Vector3(-1.3f, 0.55f, 4.3f), darkMetal);
                CreateLocalWheel(
                    "Front Right Wheel", truck.transform,
                    new Vector3(1.3f, 0.55f, 4.3f), darkMetal);
                CreateLocalWheel(
                    "Middle Left Wheel", truck.transform,
                    new Vector3(-1.3f, 0.55f, -0.4f), darkMetal);
                CreateLocalWheel(
                    "Middle Right Wheel", truck.transform,
                    new Vector3(1.3f, 0.55f, -0.4f), darkMetal);
                CreateLocalWheel(
                    "Rear Left Wheel", truck.transform,
                    new Vector3(-1.3f, 0.55f, -4.6f), darkMetal);
                CreateLocalWheel(
                    "Rear Right Wheel", truck.transform,
                    new Vector3(1.3f, 0.55f, -4.6f), darkMetal);

                GameObject slotsRoot = CreateEmpty("Pallet Slots", truck.transform);
                var slots = new Transform[FreightPalletSlotCapacity];
                for (int index = 0; index < slots.Length; index++)
                {
                    GameObject slot = CreateEmpty(
                        $"Pallet Slot {index + 1}", slotsRoot.transform);
                    slot.transform.localPosition =
                        GetFreightTruckPalletSlotLocalPosition(index);
                    slot.transform.localRotation =
                        Quaternion.Euler(0f, -90f, 0f);
                    slots[index] = slot.transform;
                }

                truck.AddComponent<EntityBehaviour>();
                truck.AddComponent<TransformRegistrar>();
                SlotsRegistrar slotsRegistrar = truck.AddComponent<SlotsRegistrar>();
                slotsRegistrar.Configure(slots);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    truck, FreightTruckPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create freight truck prefab at " +
                        $"{FreightTruckPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Freight truck prefab at " +
                                                 $"{FreightTruckPrefabPath} has no " +
                                                 "EntityBehaviour root.");
                config.Configure(prefabView);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(truck);
            }
        }

        private static Vector3 GetFreightTruckPalletSlotLocalPosition(int index)
        {
            if (index < 0 || index >= FreightPalletSlotCapacity)
                throw new ArgumentOutOfRangeException(nameof(index));

            return new Vector3(0f, 1.11f, -4.5f + index * 2f);
        }

        private static void CreateTrailerRailSegment(Transform truck,
            int index, float startZ, float endZ, Material material)
        {
            if (endZ <= startZ)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endZ),
                    "Trailer rail segment end must be greater than its start.");
            }

            CreateCube(
                $"Trailer Loading Rail Segment {index}",
                truck,
                new Vector3(1.2f, 1.48f, (startZ + endZ) * 0.5f),
                new Vector3(0.14f, 0.85f, endZ - startZ),
                material,
                true,
                true);
        }

        private static void EnsurePalletPrefab(
            PalletConfig config,
            Material timber,
            Material darkMetal)
        {
            GameObject pallet = CreateEmpty("Pallet");

            try
            {
                pallet.transform.SetLocalPositionAndRotation(
                    Vector3.zero, Quaternion.identity);
                pallet.transform.localScale = Vector3.one;
                pallet.SetActive(true);

                for (int index = 0; index < 3; index++)
                {
                    CreateCube(
                        $"Runner {index + 1}",
                        pallet.transform,
                        new Vector3(-0.46f + index * 0.46f, 0.07f, 0f),
                        new Vector3(0.16f, 0.14f, 1.05f),
                        darkMetal,
                        false,
                        true);
                }

                for (int index = 0; index < 5; index++)
                {
                    CreateCube(
                        $"Deck Board {index + 1}",
                        pallet.transform,
                        new Vector3(0f, 0.18f, -0.42f + index * 0.21f),
                        new Vector3(1.25f, 0.08f, 0.17f),
                        timber,
                        false,
                        true);
                }

                pallet.AddComponent<EntityBehaviour>();
                pallet.AddComponent<TransformRegistrar>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    pallet, PalletPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create pallet prefab at {PalletPrefabPath}.");
                }

                EntityBehaviour prefabView = prefab.GetComponent<EntityBehaviour>() ??
                                             throw new InvalidOperationException(
                                                 $"Pallet prefab at {PalletPrefabPath} " +
                                                 "has no EntityBehaviour root.");
                config.Configure(prefabView);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(pallet);
            }
        }

        private static void EnsureSpawnPointPrefab()
        {
            GameObject spawnPoint = CreateEmpty("Spawn Point");

            try
            {
                spawnPoint.AddComponent<SpawnPointMarker>();
                Texture2D icon = GetSpawnPointIcon();
                EditorGUIUtility.SetIconForObject(spawnPoint, icon);

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(spawnPoint, SpawnPointPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create spawn point prefab at {SpawnPointPrefabPath}.");
                }

                EditorGUIUtility.SetIconForObject(prefab, icon);
                EditorUtility.SetDirty(prefab);
            }
            finally
            {
                Object.DestroyImmediate(spawnPoint);
            }
        }

        private static Texture2D GetSpawnPointIcon() =>
            EditorGUIUtility.IconContent(SpawnPointIconName).image as Texture2D ??
            throw new InvalidOperationException(
                $"Unity editor icon '{SpawnPointIconName}' is unavailable.");

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

        private static Transform[] CreateWaypointTransforms(
            string name,
            Transform parent,
            Pose[] poses)
        {
            if (poses == null || poses.Length == 0)
                throw new ArgumentException("Waypoints require at least one pose.", nameof(poses));

            GameObject routeObject = CreateEmpty(name, parent);
            var waypoints = new Transform[poses.Length];
            for (int index = 0; index < poses.Length; index++)
            {
                GameObject waypoint = CreateEmpty(
                    $"Waypoint {index + 1}",
                    routeObject.transform);
                waypoint.transform.SetPositionAndRotation(
                    poses[index].position,
                    poses[index].rotation);
                waypoints[index] = waypoint.transform;
            }

            return waypoints;
        }

        private static void BakeAndValidateNavigation(
            NavMeshSurface surface,
            CustomerFlowLayoutMarker customerFlowLayout,
            SpawnPointMarker idlePoint,
            SpawnPointMarker deliveryAccessPoint,
            SpawnPointMarker storageAccessPoint,
            SpawnPointMarker customerLoadingAccessPoint,
            SpawnPointMarker workerTrolleyHomePoint,
            SpawnPointMarker workerTrolleyCustomerLoadingAccessPoint,
            SpawnPointMarker workerInboundTrolleyStorageBypassPoint,
            SpawnPointMarker workerInboundTrolleyStorageAccessPoint,
            SpawnPointMarker workerOutboundTrolleyStorageApproachPoint,
            SpawnPointMarker workerOutboundTrolleyStorageAccessPoint,
            float workerTrolleyFollowDistance)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (customerFlowLayout == null)
                throw new ArgumentNullException(nameof(customerFlowLayout));
            SpawnPointMarker[] authoredPoints =
            {
                idlePoint,
                deliveryAccessPoint,
                storageAccessPoint,
                customerLoadingAccessPoint,
                workerTrolleyHomePoint,
                workerTrolleyCustomerLoadingAccessPoint,
                workerInboundTrolleyStorageBypassPoint,
                workerInboundTrolleyStorageAccessPoint,
                workerOutboundTrolleyStorageApproachPoint,
                workerOutboundTrolleyStorageAccessPoint
            };
            if (authoredPoints.Any(point => point == null))
            {
                throw new ArgumentException(
                    "Warehouse worker navigation requires every authored access point.");
            }
            if (float.IsNaN(workerTrolleyFollowDistance) ||
                float.IsInfinity(workerTrolleyFollowDistance) ||
                workerTrolleyFollowDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workerTrolleyFollowDistance));
            }

            Pose trolleyHomePose = workerTrolleyHomePoint.Pose;
            Pose trolleyCustomerLoadingPose =
                workerTrolleyCustomerLoadingAccessPoint.Pose;
            Pose trolleyHomePusherPose = ResolveWorkerTrolleyPusherPose(
                trolleyHomePose, workerTrolleyFollowDistance);
            Pose trolleyCustomerLoadingPusherPose = ResolveWorkerTrolleyPusherPose(
                trolleyCustomerLoadingPose, workerTrolleyFollowDistance);
            Pose inboundTrolleyStorageBypassPusherPose = ResolveWorkerTrolleyPusherPose(
                workerInboundTrolleyStorageBypassPoint.Pose,
                workerTrolleyFollowDistance);
            Pose inboundTrolleyStoragePusherPose = ResolveWorkerTrolleyPusherPose(
                workerInboundTrolleyStorageAccessPoint.Pose,
                workerTrolleyFollowDistance);
            Pose outboundTrolleyStorageApproachPusherPose =
                ResolveWorkerTrolleyPusherPose(
                    workerOutboundTrolleyStorageApproachPoint.Pose,
                    workerTrolleyFollowDistance);
            Pose outboundTrolleyStorageAccessPusherPose =
                ResolveWorkerTrolleyPusherPose(
                    workerOutboundTrolleyStorageAccessPoint.Pose,
                    workerTrolleyFollowDistance);
            if (Vector3.Distance(
                    idlePoint.transform.position,
                    trolleyHomePusherPose.position) > 0.001f ||
                Quaternion.Angle(
                    idlePoint.transform.rotation,
                    trolleyHomePusherPose.rotation) > 0.01f)
            {
                throw new InvalidOperationException(
                    "Warehouse worker idle pose must align with the authored worker-trolley " +
                    "home pusher pose.");
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

            (string Name, Vector3 Position)[] workerAccessPoints =
            {
                (idlePoint.name, idlePoint.transform.position),
                (deliveryAccessPoint.name, deliveryAccessPoint.transform.position),
                (storageAccessPoint.name, storageAccessPoint.transform.position),
                (customerLoadingAccessPoint.name,
                    customerLoadingAccessPoint.transform.position),
                (workerTrolleyCustomerLoadingAccessPoint.name + " Pusher",
                    trolleyCustomerLoadingPusherPose.position),
                (workerInboundTrolleyStorageBypassPoint.name + " Pusher",
                    inboundTrolleyStorageBypassPusherPose.position),
                (workerInboundTrolleyStorageAccessPoint.name + " Pusher",
                    inboundTrolleyStoragePusherPose.position),
                (workerOutboundTrolleyStorageApproachPoint.name + " Pusher",
                    outboundTrolleyStorageApproachPusherPose.position),
                (workerOutboundTrolleyStorageAccessPoint.name + " Pusher",
                    outboundTrolleyStorageAccessPusherPose.position)
            };
            var sampledPositions = new Vector3[workerAccessPoints.Length];
            for (int index = 0; index < workerAccessPoints.Length; index++)
            {
                Vector3 point = workerAccessPoints[index].Position;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(
                        $"Warehouse worker access point '{workerAccessPoints[index].Name}' " +
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
                            $"'{workerAccessPoints[originIndex].Name}' to " +
                            $"'{workerAccessPoints[destinationIndex].Name}'.");
                    }
                }
            }

            ValidateCustomerFlowNavigation(customerFlowLayout.Layout);
        }

        private static Pose ResolveWorkerTrolleyPusherPose(
            Pose trolleyPose,
            float followDistance) =>
            new(
                trolleyPose.position -
                trolleyPose.rotation * Vector3.forward * followDistance,
                trolleyPose.rotation);

        private static void ValidateCustomerFlowNavigation(
            CustomerFlowSceneLayout layout)
        {
            Pose servicePose = layout.QueuePoses[0];
            if (!NavMesh.SamplePosition(
                    servicePose.position,
                    out NavMeshHit serviceHit,
                    2f,
                    NavMesh.AllAreas) ||
                Vector3.Distance(serviceHit.position, servicePose.position) > 0.35f)
            {
                throw new InvalidOperationException(
                    $"Customer queue service pose at {servicePose.position} is not on the " +
                    "baked NavMesh.");
            }

            foreach (CustomerParkingSpotSceneLayout parkingSpot in layout.ParkingSpots)
            {
                Vector3 doorPosition = parkingSpot.CustomerApproachRoute[0].position;
                if (!NavMesh.SamplePosition(
                        doorPosition,
                        out NavMeshHit doorHit,
                        2f,
                        NavMesh.AllAreas) ||
                    Vector3.Distance(doorHit.position, doorPosition) > 0.35f)
                {
                    throw new InvalidOperationException(
                        $"Customer parking spot {parkingSpot.Index} door pose at " +
                        $"{doorPosition} is not on the baked NavMesh.");
                }

                var approachPath = new NavMeshPath();
                var returnPath = new NavMeshPath();
                bool canApproach = NavMesh.CalculatePath(
                    doorHit.position,
                    serviceHit.position,
                    NavMesh.AllAreas,
                    approachPath);
                bool canReturn = NavMesh.CalculatePath(
                    serviceHit.position,
                    doorHit.position,
                    NavMesh.AllAreas,
                    returnPath);
                if (!canApproach || approachPath.status != NavMeshPathStatus.PathComplete ||
                    !canReturn || returnPath.status != NavMeshPathStatus.PathComplete)
                {
                    throw new InvalidOperationException(
                        $"Customer parking spot {parkingSpot.Index} must have complete " +
                        "NavMesh paths between its vehicle door and the order counter.");
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

        private static GameObject CreateVisualCylinder(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            return cylinder;
        }

        private static void CreateLocalWheel(string name, Transform parent, Vector3 localPosition,
            Material material, Vector3? localScale = null)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, 0f, 90f));
            wheel.transform.localScale = localScale ?? new Vector3(0.48f, 0.19f, 0.48f);
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
            EnsureConfigAsset<CustomerProjectConfig>(GardenWallProjectConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(DrywallPartitionProjectConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(WorkshopRenovationProjectConfigName);
            EnsureConfigAsset<CustomerProjectConfig>(GarageInsulationProjectConfigName);
            EnsureConfigAsset<ProductConfig>(CementProductConfigName);
            EnsureConfigAsset<ProductConfig>(BoardProductConfigName);
            EnsureConfigAsset<ProductConfig>(BrickProductConfigName);
            EnsureConfigAsset<ProductConfig>(DrywallProductConfigName);
            EnsureConfigAsset<ProductConfig>(PaintProductConfigName);
            EnsureConfigAsset<ProductConfig>(InsulationProductConfigName);
            EnsureConfigAsset<DeliveryConfig>(CementDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(BoardDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(BrickDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(DrywallDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(PaintDeliveryConfigName);
            EnsureConfigAsset<DeliveryConfig>(InsulationDeliveryConfigName);
            EnsureConfigAsset<CustomerVehicleConfig>("CustomerVehicleConfig");
            EnsureConfigAsset<CustomerConfig>("CustomerConfig");
            EnsureConfigAsset<CustomerFlowConfig>(CustomerFlowConfigName);
            EnsureConfigAsset<EconomyConfig>("EconomyConfig");
            EnsureConfigAsset<ProductRecoveryConfig>(ProductRecoveryConfigName);
            EnsureConfigAsset<LocalTrafficConfig>("LocalTrafficConfig");
            EnsureConfigAsset<PlatformTrolleyConfig>(PlatformTrolleyConfigName);
            EnsureConfigAsset<WarehouseWorkerConfig>(WarehouseWorkerConfigName);
            EnsureConfigAsset<ForkliftConfig>(ForkliftConfigName);
            EnsureConfigAsset<FreightTruckConfig>(FreightTruckConfigName);
            EnsureConfigAsset<PalletConfig>(PalletConfigName);
            EnsureConfigAsset<StoreDayConfig>(StoreDayConfigName);
        }

        private static void ConfigurePrototypeConfigs(
            DeliveryConfig cementDeliveryConfig,
            DeliveryConfig boardDeliveryConfig,
            DeliveryConfig brickDeliveryConfig,
            DeliveryConfig drywallDeliveryConfig,
            DeliveryConfig paintDeliveryConfig,
            DeliveryConfig insulationDeliveryConfig,
            EconomyConfig economyConfig,
            ProductRecoveryConfig productRecoveryConfig,
            StoreDayConfig storeDayConfig,
            CustomerFlowConfig customerFlowConfig,
            WarehouseWorkerConfig warehouseWorkerConfig,
            ProductConfig cementProductConfig,
            ProductConfig boardProductConfig,
            ProductConfig brickProductConfig,
            ProductConfig drywallProductConfig,
            ProductConfig paintProductConfig,
            ProductConfig insulationProductConfig,
            CustomerProjectConfig cementProjectConfig,
            CustomerProjectConfig lumberProjectConfig,
            CustomerProjectConfig workbenchProjectConfig,
            CustomerProjectConfig gardenWallProjectConfig,
            CustomerProjectConfig drywallPartitionProjectConfig,
            CustomerProjectConfig workshopRenovationProjectConfig,
            CustomerProjectConfig garageInsulationProjectConfig)
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
            ConfigureDeliveryConfig(
                brickDeliveryConfig,
                ProductTypeId.BrickPack,
                productCount: 3,
                purchaseUnitPrice: 190);
            ConfigureDeliveryConfig(
                drywallDeliveryConfig,
                ProductTypeId.DrywallSheet,
                productCount: 3,
                purchaseUnitPrice: 80);
            ConfigureDeliveryConfig(
                paintDeliveryConfig,
                ProductTypeId.PaintBucket,
                productCount: 3,
                purchaseUnitPrice: 140);
            ConfigureDeliveryConfig(
                insulationDeliveryConfig,
                ProductTypeId.InsulationRoll,
                productCount: 3,
                purchaseUnitPrice: 150);

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

            customerFlowConfig.Configure(
                parkingCapacity: 3,
                firstArrivalDelay: 10f,
                arrivalSchedule: new[]
                {
                    new CustomerArrivalSchedulePoint(8 * 60, 45f),
                    new CustomerArrivalSchedulePoint(10 * 60, 36f),
                    new CustomerArrivalSchedulePoint(13 * 60, 26f),
                    new CustomerArrivalSchedulePoint(17 * 60, 28f),
                    new CustomerArrivalSchedulePoint(19 * 60, 45f),
                    new CustomerArrivalSchedulePoint(20 * 60, 70f)
                },
                defaultPatienceDuration: 120f,
                patienceWarningThreshold: 30f);
            EditorUtility.SetDirty(customerFlowConfig);

            SerializedObject warehouseWorker = new(warehouseWorkerConfig);
            RequireSerializedProperty(warehouseWorker, "_requiredCompletedOrderCount").intValue = 4;
            RequireSerializedProperty(warehouseWorker, "_hirePrice").intValue = 400;
            RequireSerializedProperty(warehouseWorker, "_dailyWage").intValue = 100;
            RequireSerializedProperty(warehouseWorker, "_movementSpeed").floatValue = 2.8f;
            RequireSerializedProperty(warehouseWorker, "_acceleration").floatValue = 12f;
            RequireSerializedProperty(warehouseWorker, "_angularSpeed").floatValue = 720f;
            RequireSerializedProperty(warehouseWorker, "_stoppingDistance").floatValue = 0.2f;
            RequireSerializedProperty(warehouseWorker, "_navigationSampleRadius").floatValue = 2f;
            RequireSerializedProperty(warehouseWorker, "_taskTimeout").floatValue = 45f;
            RequireSerializedProperty(warehouseWorker, "_trolleyCapacity").intValue = 3;
            RequireSerializedProperty(warehouseWorker, "_trolleyFollowDistance").floatValue = 1.7f;
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
            ConfigureProductConfig(
                brickProductConfig,
                ProductTypeId.BrickPack,
                unitPrice: 330,
                mass: 24f,
                carryMovementSpeed: 2.9f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.2f,
                productDropCollisionRadius: 0.53f);
            ConfigureProductConfig(
                drywallProductConfig,
                ProductTypeId.DrywallSheet,
                unitPrice: 260,
                mass: 14f,
                carryMovementSpeed: 2.8f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.35f,
                productDropCollisionRadius: 0.84f);
            ConfigureProductConfig(
                paintProductConfig,
                ProductTypeId.PaintBucket,
                unitPrice: 340,
                mass: 16f,
                carryMovementSpeed: 3.4f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.05f,
                productDropCollisionRadius: 0.49f);
            ConfigureProductConfig(
                insulationProductConfig,
                ProductTypeId.InsulationRoll,
                unitPrice: 350,
                mass: 8f,
                carryMovementSpeed: 3.3f,
                heldRotationEuler: Vector3.zero,
                dropForwardDistance: 1.25f,
                productDropCollisionRadius: 0.72f);

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
            ConfigureMixedProject(
                gardenWallProjectConfig,
                CustomerProjectTypeId.GardenWall,
                ProductTypeId.BrickPack,
                ProductTypeId.CementBag);
            ConfigureMixedProject(
                drywallPartitionProjectConfig,
                CustomerProjectTypeId.DrywallPartition,
                ProductTypeId.DrywallSheet,
                ProductTypeId.BoardBundle);
            ConfigureMixedProject(
                workshopRenovationProjectConfig,
                CustomerProjectTypeId.WorkshopRenovation,
                ProductTypeId.PaintBucket,
                ProductTypeId.DrywallSheet);
            ConfigureMixedProject(
                garageInsulationProjectConfig,
                CustomerProjectTypeId.GarageInsulation,
                ProductTypeId.InsulationRoll,
                ProductTypeId.BoardBundle);
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

        private static void ConfigureMixedProject(
            CustomerProjectConfig config,
            CustomerProjectTypeId projectType,
            ProductTypeId primaryProductType,
            ProductTypeId secondaryProductType)
        {
            config.Configure(
                projectType,
                defaultOfferIndex: 1,
                offers: new[]
                {
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(
                            primaryProductType, requiredCount: 1),
                        new CustomerProjectLineDefinition(
                            secondaryProductType, requiredCount: 1)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(
                            primaryProductType, requiredCount: 2),
                        new CustomerProjectLineDefinition(
                            secondaryProductType, requiredCount: 1)),
                    new CustomerProjectOfferDefinition(
                        new CustomerProjectLineDefinition(
                            primaryProductType, requiredCount: 1),
                        new CustomerProjectLineDefinition(
                            secondaryProductType, requiredCount: 2))
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
