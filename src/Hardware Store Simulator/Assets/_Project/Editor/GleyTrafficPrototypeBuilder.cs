using System;
using System.Collections.Generic;
using System.Linq;
using Gley.TrafficSystem;
using Gley.TrafficSystem.Editor;
using Gley.UrbanSystem;
using Gley.UrbanSystem.Editor;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Infrastructure.VehicleTraffic.Gley;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;
using Object = UnityEngine.Object;

namespace HardwareStore.Editor
{
    /// <summary>
    /// Prepares the active Gley customer-traffic runtime slice: vehicle assets,
    /// collision layers and the directed Prototype_Yard waypoint graph.
    /// </summary>
    public static class GleyTrafficPrototypeBuilder
    {
        private const string MenuPath =
            "Tools/Hardware Store/Traffic/Prepare Gley Customer Traffic Prototype";
        private const string PrototypeScenePath = "Assets/Scenes/Prototype_Yard.unity";
        private const string SourceVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicle.prefab";
        private const string GleyVehiclePrefabPath =
            "Assets/_Project/Prefabs/Gameplay/CustomerVehicleGley.prefab";
        private const string TrafficAssetFolder = "Assets/_Project/Traffic/Gley";
        private const string VehiclePoolPath =
            TrafficAssetFolder + "/CustomerVehicleGleyPool.asset";
        private const string TrafficConfigPath =
            "Assets/Resources/Configs/GleyTrafficConfig.asset";
        private const string GeneratedRoutesRootName = "HardwareStore Customer Routes";
        private const string AsphaltGroundName = "Asphalt Ground";
        private const string CustomerAccessRoadName = "Customer Access Road";
        private const float PositionMergeTolerance = 0.04f;
        private const float WaypointSpacing = 2.5f;
        private const float MinimumSegmentLength = 0.05f;
        private const float LaneWidth = 3f;
        private const int YardSpeedKilometresPerHour = 12;
        private const int GridCellSize = 10;
        private const float WheelGeometryTolerance = 0.005f;

        private static readonly WheelDefinition[] Wheels =
        {
            new("Front Left Wheel", "FrontLeft"),
            new("Front Right Wheel", "FrontRight"),
            new("Rear Left Wheel", "RearLeft"),
            new("Rear Right Wheel", "RearRight")
        };

        private static readonly string[] PreservedChildNames =
        {
            "Loading Target",
            "Interaction Area",
            "Customer Cargo Slots"
        };

        private static readonly string[] DynamicObstacleCollisionPrefabPaths =
        {
            "Assets/_Project/Prefabs/Gameplay/Player.prefab",
            "Assets/_Project/Prefabs/Gameplay/Forklift.prefab"
        };

        [MenuItem(MenuPath, priority = 150)]
        public static void PrepareFromMenu()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Gley traffic prototype preparation must run in Edit Mode.");

            ValidateRuntimeCollisionPrefabPaths();
            GameObject sourcePrefab = RequirePrefabAsset(SourceVehiclePrefabPath);
            EnsureFolder(TrafficAssetFolder);

            GameObject gleyPrefab = PrepareVehiclePrefab(sourcePrefab);
            VehiclePool vehiclePool = PrepareVehiclePool(gleyPrefab);
            GleyTrafficConfig trafficConfig = PrepareTrafficConfig(vehiclePool);
            PrepareRuntimeCollisionLayers();

            string routeBlocker;
            bool graphBuilt = TryBuildWaypointGraph(out routeBlocker);
            AssetDatabase.SaveAssets();

            Selection.activeObject = trafficConfig;
            if (graphBuilt)
            {
                Debug.Log(
                    $"[Hardware Store] Gley traffic prototype prepared: " +
                    $"'{GleyVehiclePrefabPath}', '{VehiclePoolPath}', " +
                    $"'{TrafficConfigPath}' and the Prototype_Yard waypoint graph were " +
                    "generated.",
                    trafficConfig);
                return;
            }

            Debug.LogWarning(
                $"[Hardware Store] Gley vehicle prefab and pool are ready, but no waypoint " +
                $"graph was generated. {routeBlocker}\n" +
                "The ECS/Gley runtime bridge is active, but customer traffic requires " +
                "the Prototype_Yard graph before it can spawn vehicles.",
                trafficConfig);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanPrepareFromMenu() =>
            !EditorApplication.isPlayingOrWillChangePlaymode;

        private static GameObject PrepareVehiclePrefab(GameObject sourcePrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GleyVehiclePrefabPath) == null)
            {
                Require(AssetDatabase.CopyAsset(SourceVehiclePrefabPath, GleyVehiclePrefabPath),
                    $"Could not copy '{SourceVehiclePrefabPath}' to " +
                    $"'{GleyVehiclePrefabPath}'.");
                AssetDatabase.ImportAsset(GleyVehiclePrefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
            else
            {
                // The Gley prefab is a generated derivative. Rebuild its authored hierarchy
                // from the current source prefab on every run so geometry edits (notably
                // wheel size and placement) cannot leave stale visuals behind. Saving over
                // the existing asset preserves its meta file, GUID and VehiclePool link.
                GameObject sourceRoot = PrefabUtility.LoadPrefabContents(
                    SourceVehiclePrefabPath);
                try
                {
                    GameObject refreshedPrefab = PrefabUtility.SaveAsPrefabAsset(
                        sourceRoot,
                        GleyVehiclePrefabPath);
                    Require(refreshedPrefab != null,
                        $"Could not refresh generated Gley vehicle prefab from " +
                        $"'{SourceVehiclePrefabPath}'.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(sourceRoot);
                }

                AssetDatabase.ImportAsset(GleyVehiclePrefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GleyVehiclePrefabPath);
            try
            {
                // Gley's editor configurator derives collider bounds from the active prefab
                // hierarchy. The saved pool asset remains inactive, but repeat builds must
                // temporarily activate the prefab stage before measuring it again.
                root.SetActive(true);
                ValidatePreservedContract(sourcePrefab, root);
                RemoveNavMeshObstacles(root);

                Transform carHolder = EnsureCarHolder(root.transform);
                Transform wheelsHolder = EnsureDirectChild(carHolder, "Wheels");
                for (int index = 0; index < Wheels.Length; index++)
                {
                    EnsureWheelAnchor(
                        root.transform,
                        carHolder,
                        wheelsHolder,
                        Wheels[index],
                        index);
                }

                int trafficLayer = ResolveTrafficLayer();
                SetLayerRecursively(root, trafficLayer);
                ConfigureRigidbody(root);

                VehicleComponent vehicle = root.GetComponent<VehicleComponent>();
                if (vehicle == null)
                    vehicle = root.AddComponent<VehicleComponent>();
                ConfigureVehicleValues(vehicle);
                ResetComputedWheelDimensions(vehicle);

                VehicleComponentEditor.ConfigureCar(vehicle);
                ValidateWheelGroundContact(root.transform, vehicle);

                // Gley initializes pooled vehicles explicitly after instantiation. Keeping the
                // prefab inactive prevents physics callbacks from reaching VehicleComponent
                // before Initialize() has created its runtime obstacle list.
                root.SetActive(false);
                ValidateConfiguredVehicle(sourcePrefab, root, vehicle);

                GameObject savedPrefab =
                    PrefabUtility.SaveAsPrefabAsset(root, GleyVehiclePrefabPath);
                Require(savedPrefab != null,
                    $"Could not save Gley customer vehicle prefab at " +
                    $"'{GleyVehiclePrefabPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return RequireAsset<GameObject>(GleyVehiclePrefabPath);
        }

        private static void ValidatePreservedContract(
            GameObject sourcePrefab,
            GameObject destinationRoot)
        {
            Type[] requiredRootComponents = sourcePrefab
                .GetComponents<Component>()
                .Where(component => component != null &&
                                    component is not Transform &&
                                    component is not NavMeshObstacle)
                .Select(component => component.GetType())
                .Distinct()
                .ToArray();

            foreach (Type componentType in requiredRootComponents)
            {
                Require(destinationRoot.GetComponent(componentType) != null,
                    $"Existing Gley prefab is missing required source component " +
                    $"'{componentType.FullName}'. Delete '{GleyVehiclePrefabPath}' and run " +
                    "the preparation command again.");
            }

            foreach (string childName in PreservedChildNames)
            {
                Require(FindDeepChild(destinationRoot.transform, childName) != null,
                    $"Customer vehicle child '{childName}' must be preserved for ECS " +
                    "interaction and cargo handling.");
            }
        }

        private static void RemoveNavMeshObstacles(GameObject root)
        {
            foreach (NavMeshObstacle obstacle in
                     root.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                Object.DestroyImmediate(obstacle);
            }
        }

        private static Transform EnsureCarHolder(Transform root)
        {
            Transform carHolder = root.Find("CarHolder");
            if (carHolder == null)
            {
                carHolder = new GameObject("CarHolder").transform;
                carHolder.SetParent(root, false);
            }

            Transform[] rootChildren = root.Cast<Transform>().ToArray();
            foreach (Transform child in rootChildren)
            {
                if (child != carHolder)
                    child.SetParent(carHolder, true);
            }

            return carHolder;
        }

        private static void EnsureWheelAnchor(
            Transform root,
            Transform carHolder,
            Transform wheelsHolder,
            WheelDefinition definition,
            int siblingIndex)
        {
            Transform visual = FindDeepChild(carHolder, definition.VisualName);
            Require(visual != null,
                $"Customer vehicle wheel visual '{definition.VisualName}' is missing.");

            Bounds visualBounds = GetRequiredRendererBounds(visual, definition.VisualName);
            Vector3 visualCenterRootLocal = root.InverseTransformPoint(visualBounds.center);
            Vector3 contactPointRootLocal = new(
                visualCenterRootLocal.x,
                0f,
                visualCenterRootLocal.z);

            Transform anchor = wheelsHolder.Find(definition.AnchorName);
            if (anchor == null)
            {
                anchor = new GameObject(definition.AnchorName).transform;
                anchor.SetParent(wheelsHolder, false);
            }

            if (visual.parent == anchor)
                visual.SetParent(carHolder, true);

            anchor.SetPositionAndRotation(
                root.TransformPoint(contactPointRootLocal),
                carHolder.rotation);
            anchor.localScale = Vector3.one;
            visual.SetParent(anchor, true);

            visualBounds = GetRequiredRendererBounds(visual, definition.VisualName);
            float bottomRootLocalY = GetBoundsMinimumRootLocalY(root, visualBounds);
            visual.position += root.up * -bottomRootLocalY;

            anchor.SetSiblingIndex(siblingIndex);
            visual.SetSiblingIndex(0);
            Require(anchor.childCount > 0 && anchor.GetChild(0) == visual,
                $"Wheel anchor '{definition.AnchorName}' must have its wheel visual as " +
                "the first child for Gley configuration.");
        }

        private static void ConfigureRigidbody(GameObject root)
        {
            Rigidbody body = root.GetComponent<Rigidbody>();
            Require(body != null, "Customer vehicle prefab requires a Rigidbody.");

            body.mass = 1400f;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0.1f;
            body.angularDamping = 3f;
#else
            body.drag = 0.1f;
            body.angularDrag = 3f;
#endif
        }

        private static void ConfigureVehicleValues(VehicleComponent vehicle)
        {
            vehicle.vehicleType = VehicleTypes.Car;
            vehicle.minPossibleSpeed = 8;
            vehicle.maxPossibleSpeed = 15;
            vehicle.accelerationTime = 5f;
            vehicle.brakeTime = 2.5f;
            vehicle.steeringTime = 0.8f;
            vehicle.maxSteer = 35f;
            vehicle.distanceToStop = 2.75f;
            vehicle.triggerLength = 4f;
            vehicle.updateTrigger = true;
            vehicle.maxTriggerLength = 7f;
            vehicle.maxSuspension = 0f;
            vehicle.springStiffness = 5f;
            vehicle.sideLeaningFactor = 0.15f;
            vehicle.forwardLeaningFactor = 0.12f;
        }

        private static void ResetComputedWheelDimensions(VehicleComponent vehicle)
        {
            if (vehicle.allWheels == null)
                return;

            foreach (Wheel wheel in vehicle.allWheels)
            {
                if (wheel == null)
                    continue;

                wheel.wheelRadius = 0f;
                wheel.wheelCircumference = 0f;
                wheel.raycastLength = 0f;
                wheel.maxSuspension = 0f;
            }
        }

        private static void ValidateWheelGroundContact(
            Transform root,
            VehicleComponent vehicle)
        {
            Require(vehicle.allWheels != null && vehicle.allWheels.Length == Wheels.Length,
                $"Gley vehicle must expose exactly {Wheels.Length} wheels before wheel " +
                "ground-contact validation.");

            foreach (Wheel wheel in vehicle.allWheels)
            {
                Require(wheel != null && wheel.wheelTransform != null &&
                        wheel.wheelGraphics != null,
                    "Gley vehicle contains an incomplete wheel configuration.");

                float anchorRootLocalY = root
                    .InverseTransformPoint(wheel.wheelTransform.position).y;
                Require(Mathf.Abs(anchorRootLocalY) <= WheelGeometryTolerance,
                    $"Gley wheel anchor '{wheel.wheelTransform.name}' must be authored on " +
                    $"the provider ground plane, but its root-local Y is " +
                    $"{anchorRootLocalY:F4}.");

                Bounds graphicsBounds = GetRequiredRendererBounds(
                    wheel.wheelGraphics,
                    wheel.wheelGraphics.name);
                float graphicsBottomRootLocalY =
                    GetBoundsMinimumRootLocalY(root, graphicsBounds);
                Require(Mathf.Abs(graphicsBottomRootLocalY) <= WheelGeometryTolerance,
                    $"Gley wheel graphics '{wheel.wheelGraphics.name}' must touch the " +
                    $"provider ground plane, but their lowest root-local Y is " +
                    $"{graphicsBottomRootLocalY:F4}.");

                Require(wheel.wheelRadius > 0f && wheel.maxSuspension > 0f,
                    $"Gley wheel '{wheel.wheelTransform.name}' has invalid dimensions.");
                float expectedRaycastLength =
                    wheel.wheelRadius + wheel.maxSuspension;
                Require(Mathf.Abs(wheel.raycastLength - expectedRaycastLength) <=
                        WheelGeometryTolerance,
                    $"Gley wheel '{wheel.wheelTransform.name}' raycast length " +
                    $"{wheel.raycastLength:F4} must equal radius plus suspension " +
                    $"{expectedRaycastLength:F4}.");

                // VehicleComponent.Initialize raises every authored contact anchor by the
                // radius plus half the suspension. At the neutral suspension pose the road
                // must therefore remain inside the configured ray length.
                float neutralGroundDistance = anchorRootLocalY +
                                              wheel.wheelRadius +
                                              wheel.maxSuspension * 0.5f;
                Require(neutralGroundDistance > 0f &&
                        neutralGroundDistance <= wheel.raycastLength,
                    $"Gley wheel '{wheel.wheelTransform.name}' cannot raycast the road from " +
                    $"its neutral suspension pose: distance {neutralGroundDistance:F4}, " +
                    $"ray length {wheel.raycastLength:F4}.");
            }
        }

        private static Bounds GetRequiredRendererBounds(
            Transform visual,
            string wheelName)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0,
                $"Wheel visual '{wheelName}' must contain at least one Renderer.");

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float GetBoundsMinimumRootLocalY(
            Transform root,
            Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            float minimum = float.PositiveInfinity;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        minimum = Mathf.Min(
                            minimum,
                            root.InverseTransformPoint(corner).y);
                    }
                }
            }

            return minimum;
        }

        private static void ValidateConfiguredVehicle(
            GameObject sourcePrefab,
            GameObject root,
            VehicleComponent vehicle)
        {
            ValidatePreservedContract(sourcePrefab, root);
            Require(!root.activeSelf,
                "The Gley pooled customer vehicle prefab must stay inactive until Gley " +
                "initializes and activates its runtime instance.");
            Require(vehicle.rb == root.GetComponent<Rigidbody>(),
                "Gley VehicleComponent must reference the prefab root Rigidbody.");
            Require(vehicle.carHolder == root.transform.Find("CarHolder"),
                "Gley VehicleComponent must reference the direct CarHolder child.");
            Require(vehicle.frontTrigger != null &&
                    vehicle.frontTrigger.GetComponentInChildren<BoxCollider>() != null,
                "Gley vehicle front trigger was not generated.");
            Vector3 sourceFrontAxleOffset = CustomerVehicleProviderPoseUtility
                .ResolveFrontAxleFromProviderRootLocalXZ(sourcePrefab);
            Vector3 frontTriggerLocal = root.transform.InverseTransformPoint(
                vehicle.frontTrigger.position);
            Vector3 providerActivationOffset =
                Vector3.forward * frontTriggerLocal.magnitude;
            Require(Vector3.Distance(
                        sourceFrontAxleOffset,
                        providerActivationOffset) <= 0.001f,
                $"Gley activation offset {providerActivationOffset} must match the " +
                $"source axle offset {sourceFrontAxleOffset}.");
            Require(vehicle.allWheels != null && vehicle.allWheels.Length == Wheels.Length,
                $"Gley vehicle must expose exactly {Wheels.Length} wheels.");
            Require(vehicle.visibilityScript != null,
                "Gley vehicle visibility script was not assigned.");
            Require(vehicle._frontPosition != null && vehicle._backPosition != null,
                "Gley vehicle front and back positions were not generated.");

            foreach (Wheel wheel in vehicle.allWheels)
            {
                Require(wheel != null && wheel.wheelTransform != null,
                    "Gley vehicle contains an unassigned wheel anchor.");
                Require(wheel.wheelTransform.childCount > 0 &&
                        wheel.wheelGraphics == wheel.wheelTransform.GetChild(0),
                    $"Gley wheel '{wheel.wheelTransform.name}' must reference its first " +
                    "child as graphics.");
                Require(wheel.wheelRadius > 0f && wheel.maxSuspension > 0f,
                    $"Gley wheel '{wheel.wheelTransform.name}' has invalid dimensions.");
            }

            Require(root.GetComponentsInChildren<Collider>(true)
                    .Any(collider => !collider.isTrigger),
                "Gley customer vehicle requires a non-trigger body collider.");
        }

        private static VehiclePool PrepareVehiclePool(GameObject vehiclePrefab)
        {
            VehiclePool pool = AssetDatabase.LoadAssetAtPath<VehiclePool>(VehiclePoolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<VehiclePool>();
                AssetDatabase.CreateAsset(pool, VehiclePoolPath);
            }

            pool.trafficCars = new[] { new CarType() };
            EditorUtility.SetDirty(pool);

            var serializedPool = new SerializedObject(pool);
            SerializedProperty trafficCars = serializedPool.FindProperty("trafficCars");
            Require(trafficCars != null,
                "Gley VehiclePool no longer exposes the serialized trafficCars field.");
            trafficCars.arraySize = 1;
            SerializedProperty entry = trafficCars.GetArrayElementAtIndex(0);
            SetRequiredProperty(entry, "vehiclePrefab").objectReferenceValue = vehiclePrefab;
            SetRequiredProperty(entry, "percent").intValue = 100;
            SetRequiredProperty(entry, "ignore").boolValue = true;
            serializedPool.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pool);

            Require(pool.trafficCars.Length == 1 &&
                    pool.trafficCars[0].VehiclePrefab == vehiclePrefab &&
                    pool.trafficCars[0].Percent == 100 &&
                    pool.trafficCars[0].Ignore,
                "Customer Gley VehiclePool serialization failed.");
            return pool;
        }

        private static GleyTrafficConfig PrepareTrafficConfig(VehiclePool vehiclePool)
        {
            EnsureFolder("Assets/Resources/Configs");
            GleyTrafficConfig config =
                AssetDatabase.LoadAssetAtPath<GleyTrafficConfig>(TrafficConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GleyTrafficConfig>();
                AssetDatabase.CreateAsset(config, TrafficConfigPath);
            }

            var serializedConfig = new SerializedObject(config);
            SerializedProperty pool = serializedConfig.FindProperty("_vehiclePool");
            SerializedProperty maximum =
                serializedConfig.FindProperty("_maximumVehicleCount");
            Require(pool != null && maximum != null,
                "GleyTrafficConfig serialized contract has changed.");
            pool.objectReferenceValue = vehiclePool;
            maximum.intValue = 8;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            config.Validate();
            return config;
        }

        private static void PrepareRuntimeCollisionLayers()
        {
            LayerSetup setup = RequireLayerSetup();
            int obstacleLayer = ResolveFirstLayer(setup.obstaclesLayers, "obstacle");

            // Only solid player-controlled movers are authored as Gley obstacles here.
            // NPCs and both trolley prefabs own the isolated GhostMover layer and must
            // never be rewritten by traffic preparation.
            foreach (string prefabPath in DynamicObstacleCollisionPrefabPaths)
                PrepareSolidColliderLayer(prefabPath, obstacleLayer, "obstacle");
        }

        private static void PrepareSolidColliderLayer(
            string prefabPath,
            int layer,
            string layerRole)
        {
            RequirePrefabAsset(prefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            Require(root != null,
                $"Could not load prefab contents at '{prefabPath}'.");

            try
            {
                // CharacterController derives from Collider, so it participates in the
                // same solid-only selection without changing interaction triggers.
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                GameObject[] solidColliderObjects = colliders
                    .Where(collider => !collider.isTrigger)
                    .Select(collider => collider.gameObject)
                    .Distinct()
                    .ToArray();
                Require(solidColliderObjects.Length > 0,
                    $"Runtime prefab '{prefabPath}' requires at least one non-trigger " +
                    "Collider or CharacterController for Gley obstacle detection.");

                var triggerObjects = new HashSet<GameObject>(colliders
                    .Where(collider => collider.isTrigger)
                    .Select(collider => collider.gameObject));
                GameObject mixedColliderObject = solidColliderObjects
                    .FirstOrDefault(triggerObjects.Contains);
                Require(mixedColliderObject == null,
                    $"Runtime prefab '{prefabPath}' has trigger and non-trigger colliders " +
                    $"on '{mixedColliderObject?.name}'. Put interaction triggers on a " +
                    "separate GameObject so their layer can remain unchanged.");

                var preservedTriggerLayers = triggerObjects.ToDictionary(
                    gameObject => gameObject,
                    gameObject => gameObject.layer);
                foreach (GameObject colliderObject in solidColliderObjects)
                {
                    colliderObject.layer = layer;
                    EditorUtility.SetDirty(colliderObject);
                }

                foreach (KeyValuePair<GameObject, int> entry in preservedTriggerLayers)
                {
                    Require(entry.Key.layer == entry.Value,
                        $"Interaction trigger '{entry.Key.name}' in '{prefabPath}' changed " +
                        $"layer while preparing the {layerRole} collision layer.");
                }

                GameObject savedPrefab =
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Require(savedPrefab != null,
                    $"Could not save {layerRole} collision layers to '{prefabPath}'.");
                Require(savedPrefab.GetComponentsInChildren<Collider>(true)
                        .Where(collider => !collider.isTrigger)
                        .All(collider => collider.gameObject.layer == layer),
                    $"Runtime prefab '{prefabPath}' did not persist all non-trigger " +
                    $"colliders on the configured Gley {layerRole} layer.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateRuntimeCollisionPrefabPaths()
        {
            string[] paths = DynamicObstacleCollisionPrefabPaths;
            Require(paths.Length == paths.Distinct(StringComparer.Ordinal).Count(),
                "A runtime prefab cannot occur twice in the Gley dynamic-obstacle list.");

            foreach (string path in paths)
                RequirePrefabAsset(path);
        }

        private static bool TryBuildWaypointGraph(out string blocker)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeScenePath)
            {
                blocker = $"Open '{PrototypeScenePath}' to validate and generate its traffic graph.";
                return false;
            }

            if (scene.isDirty)
            {
                blocker = "Save Prototype_Yard before generating Gley scene data.";
                return false;
            }

            CustomerFlowLayoutMarker[] markers = FindSceneComponents<CustomerFlowLayoutMarker>();
            if (markers.Length != 1)
            {
                blocker = $"Prototype_Yard must contain exactly one " +
                          $"{nameof(CustomerFlowLayoutMarker)}, but {markers.Length} were found.";
                return false;
            }

            CustomerFlowSceneLayout layout;
            try
            {
                layout = markers[0].Layout;
            }
            catch (Exception exception)
            {
                blocker = $"Customer route authoring is invalid: {exception.Message}";
                return false;
            }

            RouteDefinition[] routes = CollectForwardTrafficGraphRoutes(layout);
            GameObject sourceVehiclePrefab = RequirePrefabAsset(SourceVehiclePrefabPath);
            if (!TryValidateForwardOnlyRoutes(
                    routes,
                    sourceVehiclePrefab,
                    out blocker))
                return false;

            RouteDefinition[] gleyRoutes = ConvertToGleyWaypointRoutes(
                routes,
                CustomerVehicleProviderPoseUtility
                    .ResolveFrontAxleFromProviderRootLocalXZ(sourceVehiclePrefab));
            BuildWaypointGraph(scene, gleyRoutes);
            blocker = null;
            return true;
        }

        private static RouteDefinition[] ConvertToGleyWaypointRoutes(
            IReadOnlyList<RouteDefinition> providerRootRoutes,
            Vector3 frontAxleFromProviderRootLocalXZ)
        {
            var result = new RouteDefinition[providerRootRoutes.Count];
            for (int routeIndex = 0;
                 routeIndex < providerRootRoutes.Count;
                 routeIndex++)
            {
                RouteDefinition route = providerRootRoutes[routeIndex];
                var poses = new Pose[route.Poses.Length];
                for (int poseIndex = 0; poseIndex < route.Poses.Length; poseIndex++)
                {
                    poses[poseIndex] =
                        CustomerVehicleProviderPoseUtility.ToGleyWaypointPose(
                            route.Poses[poseIndex],
                            frontAxleFromProviderRootLocalXZ);
                }

                result[routeIndex] = new RouteDefinition(route.Name, poses);
            }

            return result;
        }

        private static RouteDefinition[] CollectForwardTrafficGraphRoutes(
            CustomerFlowSceneLayout layout)
        {
            var routes = new List<RouteDefinition>();
            foreach (CustomerParkingSpotSceneLayout spot in layout.ParkingSpots)
            {
                routes.Add(new RouteDefinition(
                    $"Parking {spot.Index + 1} arrival",
                    spot.VehicleArrivalRoute));
                routes.Add(new RouteDefinition(
                    $"Parking {spot.Index + 1} to loading",
                    spot.VehicleToLoadingRoute));
                routes.Add(new RouteDefinition(
                    $"Parking {spot.Index + 1} street departure",
                    spot.VehicleParkingDepartureRoute));
            }

            routes.Add(new RouteDefinition(
                "Loading departure",
                layout.LoadingDepartureRoute));
            return routes.ToArray();
        }

        private static bool TryValidateForwardOnlyRoutes(
            IEnumerable<RouteDefinition> routes,
            GameObject sourceVehiclePrefab,
            out string blocker)
        {
            Vector3 rearAxleLocalXZ =
                CustomerVehicleProviderPoseUtility.ResolveRearAxleLocalXZ(
                    sourceVehiclePrefab);
            foreach (RouteDefinition route in routes)
            {
                for (int index = 0; index < route.Poses.Length - 1; index++)
                {
                    Pose current = route.Poses[index];
                    Pose next = route.Poses[index + 1];
                    Vector3 currentVisualPosition =
                        CustomerVehicleProviderPoseUtility.ToVisualPose(
                            current,
                            rearAxleLocalXZ).position;
                    Vector3 nextVisualPosition =
                        CustomerVehicleProviderPoseUtility.ToVisualPose(
                            next,
                            rearAxleLocalXZ).position;
                    Vector3 movement = nextVisualPosition - currentVisualPosition;
                    movement.y = 0f;
                    if (movement.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
                        continue;

                    float forwardDot = Vector3.Dot(
                        current.rotation * Vector3.forward,
                        movement.normalized);
                    if (forwardDot < 0.99f)
                    {
                        blocker =
                            $"Route '{route.Name}', segment {index + 1}->{index + 2}, " +
                            "has no forward-only visual centerline. The generated Gley graph " +
                            "must never encode a reverse manoeuvre.";
                        return false;
                    }
                }
            }

            blocker = null;
            return true;
        }

        private static void BuildWaypointGraph(
            Scene scene,
            IReadOnlyList<RouteDefinition> routes)
        {
            int roadLayer = ResolveRoadLayer();
            SetSceneObjectLayer(AsphaltGroundName, roadLayer);
            SetSceneObjectLayer(CustomerAccessRoadName, roadLayer);

            ConnectionPool connectionPool =
                MonoBehaviourUtilities.GetOrCreateSceneInstance<ConnectionPool>(
                    TrafficSystemConstants.EditorWaypointsHolder,
                    true);
            Transform oldRoot = connectionPool.transform.Find(GeneratedRoutesRootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot.gameObject);

            Transform routesRoot = new GameObject(GeneratedRoutesRootName).transform;
            routesRoot.SetParent(connectionPool.transform, false);
            routesRoot.gameObject.tag = UrbanSystemConstants.EDITOR_TAG;

            var creator = new TrafficWaypointCreator().Initialize();
            var nodes = new Dictionary<Vector3Int, WaypointSettings>();
            var allowedCars = new List<int> { (int)VehicleTypes.Car };
            foreach (RouteDefinition route in routes)
                AddRoute(route, routesRoot, creator, nodes, allowedCars);

            Require(nodes.Count >= 2,
                "Customer traffic graph must contain at least two distinct waypoints.");

            var settings = new SettingsLoader(TrafficSystemConstants.windowSettingsPath)
                .LoadSettingsAsset<TrafficSettingsWindowData>();
            settings.PathFindingEnabled = true;
            EditorUtility.SetDirty(settings);

            var gridData = new GridEditorData();
            var gridCreator = new GridCreator(gridData);
            // The scene builder creates and scales road colliders immediately before
            // this call. Sync the physics world so Gley reads their authored bounds,
            // not the temporary unit-cube bounds from the creation frame.
            Physics.SyncTransforms();
            gridCreator.GenerateGrid(GridCellSize, 1 << roadLayer);

            var waypointConverter = new TrafficWaypointsConverter();
            waypointConverter.ConvertWaypoints();
            var intersectionConverter = new IntersectionConverter(waypointConverter, null);
            intersectionConverter.ConvertAllIntersections();

            ValidateConvertedSceneData(
                nodes.Count,
                nodes.Values.Select(node => node.transform.position));
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, PrototypeScenePath),
                $"Could not save Gley traffic data into '{PrototypeScenePath}'.");
        }

        private static void AddRoute(
            RouteDefinition route,
            Transform routesRoot,
            TrafficWaypointCreator creator,
            IDictionary<Vector3Int, WaypointSettings> nodes,
            List<int> allowedCars)
        {
            WaypointSettings previous = null;
            for (int segmentIndex = 0;
                 segmentIndex < route.Poses.Length - 1;
                 segmentIndex++)
            {
                Vector3 start = route.Poses[segmentIndex].position;
                Vector3 end = route.Poses[segmentIndex + 1].position;
                float distance = Vector3.Distance(start, end);
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / WaypointSpacing));
                int firstStep = segmentIndex == 0 ? 0 : 1;
                for (int step = firstStep; step <= steps; step++)
                {
                    Vector3 position = Vector3.Lerp(start, end, step / (float)steps);
                    WaypointSettings current = GetOrCreateWaypoint(
                        position,
                        routesRoot,
                        creator,
                        nodes,
                        allowedCars);
                    if (previous != null && previous != current &&
                        !previous.neighbors.Contains(current))
                    {
                        previous.neighbors.Add(current);
                        current.prev.Add(previous);
                    }
                    previous = current;
                }
            }
        }

        private static WaypointSettings GetOrCreateWaypoint(
            Vector3 position,
            Transform routesRoot,
            TrafficWaypointCreator creator,
            IDictionary<Vector3Int, WaypointSettings> nodes,
            List<int> allowedCars)
        {
            Vector3Int key = Quantize(position);
            if (nodes.TryGetValue(key, out WaypointSettings existing))
                return existing;

            string name = $"HS-Customer-Waypoint-{nodes.Count + 1:000}";
            Transform waypointTransform = creator.CreateWaypoint(
                routesRoot,
                position,
                name,
                allowedCars,
                YardSpeedKilometresPerHour,
                LaneWidth);
            WaypointSettings waypoint =
                waypointTransform.GetComponent<WaypointSettings>();
            waypoint.carsLocked = true;
            waypoint.speedLocked = true;
            waypoint.priority = 1;
            nodes.Add(key, waypoint);
            return waypoint;
        }

        private static void ValidateConvertedSceneData(
            int expectedMinimumWaypoints,
            IEnumerable<Vector3> authoredWaypointPositions)
        {
            GridData[] grids = FindSceneComponents<GridData>();
            Require(grids.Length == 1,
                $"Expected one Gley GridData component, found {grids.Length}.");
            Require(grids[0].IsValid(out string gridError), gridError);
            ValidateGridCoverage(grids[0], authoredWaypointPositions);

            TrafficWaypointsData[] waypointData =
                FindSceneComponents<TrafficWaypointsData>();
            Require(waypointData.Length == 1,
                $"Expected one Gley TrafficWaypointsData component, found " +
                $"{waypointData.Length}.");
            Require(waypointData[0].IsValid(out string waypointError), waypointError);
            Require(waypointData[0].AllTrafficWaypoints.Length >= expectedMinimumWaypoints,
                "Converted traffic waypoint count is lower than the authored graph count.");

            IntersectionsData[] intersections = FindSceneComponents<IntersectionsData>();
            Require(intersections.Length == 1,
                $"Expected one Gley IntersectionsData component, found " +
                $"{intersections.Length}.");
            Require(intersections[0].IsValid(out string intersectionError), intersectionError);

            TrafficModules[] modules = FindSceneComponents<TrafficModules>();
            Require(modules.Length == 1 && modules[0].PathFinding,
                "Gley path finding must be enabled for directed customer destinations.");

            PathFindingData[] pathFinding = FindSceneComponents<PathFindingData>();
            Require(pathFinding.Length == 1,
                $"Expected one Gley PathFindingData component, found " +
                $"{pathFinding.Length}.");
            Require(pathFinding[0].IsValid(out string pathFindingError), pathFindingError);
        }

        private static void ValidateGridCoverage(
            GridData grid,
            IEnumerable<Vector3> authoredWaypointPositions)
        {
            Require(grid.GridCellSize > 0,
                "Gley grid cell size must be greater than zero.");
            Require(grid.Grid.All(row => row.Row != null && row.Row.Length > 0),
                "Gley grid must contain at least one cell in every row.");

            foreach (Vector3 position in authoredWaypointPositions)
            {
                int row = Mathf.FloorToInt(
                    (position.z - grid.GridCorner.z) / grid.GridCellSize);
                int column = Mathf.FloorToInt(
                    (position.x - grid.GridCorner.x) / grid.GridCellSize);
                bool covered = row >= 0 && row < grid.Grid.Length &&
                               column >= 0 && column < grid.Grid[row].Row.Length;
                Require(covered,
                    $"Generated Gley grid does not cover customer waypoint at " +
                    $"{position}. Regenerate the grid after configuring the complete " +
                    "customer road colliders.");
            }
        }

        private static int ResolveTrafficLayer()
        {
            LayerSetup setup = RequireLayerSetup();
            return ResolveFirstLayer(setup.trafficLayers, "traffic");
        }

        private static int ResolveRoadLayer()
        {
            LayerSetup setup = RequireLayerSetup();
            return ResolveFirstLayer(setup.roadLayers, "road");
        }

        private static LayerSetup RequireLayerSetup()
        {
            LayerSetup setup = Resources.Load<LayerSetup>(
                TrafficSystemConstants.layerSetupData);
            Require(setup != null,
                "Gley LayerSetupData is missing. Configure Traffic System layers first.");
            return setup;
        }

        private static int ResolveFirstLayer(LayerMask mask, string role)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask.value & (1 << layer)) != 0)
                    return layer;
            }

            throw new InvalidOperationException(
                $"Gley LayerSetupData contains no {role} layer.");
        }

        private static void SetSceneObjectLayer(string objectName, int layer)
        {
            GameObject[] matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate => candidate.scene == SceneManager.GetActiveScene() &&
                                    candidate.name == objectName)
                .ToArray();
            Require(matches.Length == 1,
                $"Prototype_Yard must contain exactly one '{objectName}', but " +
                $"{matches.Length} were found.");
            Require(matches[0].GetComponent<Collider>() != null,
                $"Road surface '{objectName}' requires a collider for Gley wheel raycasts.");
            matches[0].layer = layer;
            EditorUtility.SetDirty(matches[0]);
        }

        private static T[] FindSceneComponents<T>() where T : Component
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(component => component.gameObject.scene == activeScene)
                .ToArray();
        }

        private static Transform EnsureDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child;
                Transform nested = FindDeepChild(child, name);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static SerializedProperty SetRequiredProperty(
            SerializedProperty parent,
            string propertyName)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            Require(property != null,
                $"Serialized Gley CarType property '{propertyName}' is missing.");
            return property;
        }

        private static Vector3Int Quantize(Vector3 position) =>
            new(
                Mathf.RoundToInt(position.x / PositionMergeTolerance),
                Mathf.RoundToInt(position.y / PositionMergeTolerance),
                Mathf.RoundToInt(position.z / PositionMergeTolerance));

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, $"Required asset is missing at '{path}'.");
            return asset;
        }

        private static GameObject RequirePrefabAsset(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) &&
                    path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf('\\') < 0,
                $"Unity prefab path must be an Assets-relative normalized .prefab path: " +
                $"'{path}'.");

            GameObject prefab = RequireAsset<GameObject>(path);
            Require(PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab,
                $"Required GameObject at '{path}' is not a prefab asset.");
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            Require(segments.Length > 0 && segments[0] == "Assets",
                $"Unity asset folder must start with 'Assets': '{path}'.");
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct WheelDefinition
        {
            public WheelDefinition(string visualName, string anchorName)
            {
                VisualName = visualName;
                AnchorName = anchorName;
            }

            public string VisualName { get; }
            public string AnchorName { get; }
        }

        private readonly struct RouteDefinition
        {
            public RouteDefinition(string name, Pose[] poses)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                Poses = poses ?? throw new ArgumentNullException(nameof(poses));
                if (poses.Length < 2)
                {
                    throw new ArgumentException(
                        "A vehicle route must contain at least two poses.",
                        nameof(poses));
                }
            }

            public string Name { get; }
            public Pose[] Poses { get; }
        }
    }
}
