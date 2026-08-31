using Gley.UrbanSystem.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace Gley.TrafficSystem.Editor
{
    public abstract class GenericConverter : IVehicleConverter
    {
        private readonly Dictionary<string, string> _processedTrailers = new Dictionary<string, string>();

        protected abstract string ExpectedFolderName { get; }
        protected virtual List<TruckData> TrucksToConvert => new();
        protected virtual List<BikeData> BikesToConvert => new();
        protected virtual List<VehicleData> VehiclesToConvert => new();


        public void Convert(string sourceFolder, string destinationFolder)
        {
            if (!ValidateSourceFolder(sourceFolder))
                return;

            ConvertInternal(sourceFolder, destinationFolder);
        }

        private void ConvertInternal(string sourceFolder, string destinationFolder)
        {
            _processedTrailers.Clear();
            ClearDestinationFolder(destinationFolder);
            Debug.Log("Start Converting");
            List<string> convertedPrefabPaths = new List<string>();
            foreach (var vehicleData in VehiclesToConvert)
            {
                string sourcePath = $"{sourceFolder}/{vehicleData.PrefabPath}";
                string fileName = Path.GetFileNameWithoutExtension(vehicleData.PrefabPath);
                string destinationPath = $"{destinationFolder}/{ExpectedFolderName}/{fileName}_MTS.prefab";
                CopyPrefab(sourcePath, destinationPath);
                ProcessVehicle(destinationPath, vehicleData);
                convertedPrefabPaths.Add(destinationPath);
            }

            foreach (var truckData in TrucksToConvert)
            {
                string tractorSourcePath = $"{sourceFolder}/{truckData.TractorData.PrefabPath}";
                string tractorFileName = Path.GetFileNameWithoutExtension(truckData.TractorData.PrefabPath);
                string trailerFileName = Path.GetFileNameWithoutExtension(truckData.TrailerData.PrefabPath);
                string tractorDestinationPath = $"{destinationFolder}/{ExpectedFolderName}/{tractorFileName}+{trailerFileName}_MTS.prefab";
                string trailerDestinationPath = $"{destinationFolder}/{ExpectedFolderName}/{trailerFileName}_MTS.prefab";

                CopyPrefab(tractorSourcePath, tractorDestinationPath);
                ProcessVehicle(tractorDestinationPath, truckData.TractorData);

                // only process the trailer once
                if (!_processedTrailers.ContainsKey(truckData.TrailerData.PrefabPath))
                {
                    CopyPrefab($"{sourceFolder}/{truckData.TrailerData.PrefabPath}", trailerDestinationPath);
                    ProcessTrailer(trailerDestinationPath, truckData.TrailerData);
                    _processedTrailers[truckData.TrailerData.PrefabPath] = trailerDestinationPath;
                }
                else
                {
                    Debug.Log($"Trailer '{trailerFileName}' already processed, reusing existing prefab.");
                }

                ConnectTruckWithTrailer(tractorDestinationPath, _processedTrailers[truckData.TrailerData.PrefabPath], truckData);
                convertedPrefabPaths.Add(tractorDestinationPath);
            }

            foreach (var bikeData in BikesToConvert)
            {
                string sourcePath = $"{sourceFolder}/{bikeData.PrefabPath}";
                string fileName = Path.GetFileNameWithoutExtension(bikeData.PrefabPath);
                string destinationPath = $"{destinationFolder}/{ExpectedFolderName}/{fileName}_MTS.prefab";
                CopyPrefab(sourcePath, destinationPath);
                ProcessBike(destinationPath, bikeData);
                convertedPrefabPaths.Add(destinationPath);
            }

            CreateVehiclePool(destinationFolder, ExpectedFolderName, convertedPrefabPaths);
        }


        private void ProcessVehicle(string prefabPath, VehicleData vehicleData)
        {
            var instance = PreparePrefab(prefabPath, GetPrefabUnpackMode());
            if (instance == null)
                return;
            try
            {
                CustomVehicleSetup(instance);
                SetupWheels(instance.transform);
                SetupColliders(instance.transform, vehicleData.Colliders);
                AddComponent(instance, typeof(VehicleComponent));

                SetLightsComponent(instance);

                var vehicleHolder = WrapContentsInRoot(instance, "VehicleHolder");
                SetTrafficLayer(instance);

                VehicleComponent vehicleComponent = instance.GetComponent<VehicleComponent>();

                SetCustomRigidbodyValues(instance.GetComponent<Rigidbody>());
                SetCustomComponentValues(vehicleComponent);

                ConfigureVehicleComponent(vehicleComponent);
                // apply all changes back to the prefab asset
                ApplyPrefab(instance, prefabPath);
            }
            finally
            {
                // always clean up the instance from the scene
                GameObject.DestroyImmediate(instance);
            }
        }


        private void ProcessBike(string prefabPath, BikeData bikeData)
        {
            var instance = PreparePrefab(prefabPath, GetPrefabUnpackMode());
            if (instance == null)
                return;
            try
            {
                CustomVehicleSetup(instance);
                SetupWheels(instance.transform);
                SetupColliders(instance.transform, bikeData.Colliders);
                AddComponent(instance, typeof(TwoWheelComponent));

                SetupHandlebar(instance);

                var vehicleHolder = WrapContentsInRoot(instance, "VehicleHolder");
                SetTrafficLayer(instance);

                var twoWheelComponent = instance.GetComponent<TwoWheelComponent>();

                SetCustomRigidbodyValues(instance.GetComponent<Rigidbody>());
                SetCustomComponentValues(twoWheelComponent);

                ConfigureVehicleComponent(twoWheelComponent);
                // apply all changes back to the prefab asset
                ApplyPrefab(instance, prefabPath);
            }
            finally
            {
                // always clean up the instance from the scene
                GameObject.DestroyImmediate(instance);
            }
        }


        private void ProcessTrailer(string trailerDestinationPath, VehicleData trailerData)
        {
            var trailerInstance = PreparePrefab(trailerDestinationPath, GetPrefabUnpackMode());
            if (trailerInstance == null)
                return;
            try
            {
                CustomVehicleSetup(trailerInstance);
                SetupWheels(trailerInstance.transform);
                SetupColliders(trailerInstance.transform, trailerData.Colliders);
                AddComponent(trailerInstance, typeof(TrailerComponent));

                var trailerHolder = WrapContentsInRoot(trailerInstance, "TrailerHolder");
                SetTrafficLayer(trailerInstance);
                
                var trailerComponent = trailerInstance.GetComponent<TrailerComponent>();

                SetCustomRigidbodyValues(trailerInstance.GetComponent<Rigidbody>());
                SetCustomComponentValues(trailerComponent);

                ConfigureVehicleComponent(trailerComponent);
               
                // apply all changes back to the prefab asset
                ApplyPrefab(trailerInstance, trailerDestinationPath);
            }
            finally
            {
                // always clean up the instance from the scene
                GameObject.DestroyImmediate(trailerInstance);
            }
        }


        protected virtual void ConfigureVehicleComponent(TrailerComponent component)
        {
            TrailerComponentEditor.ConfigureTrailer(component);
            
        }


        protected virtual void ConfigureVehicleComponent(TwoWheelComponent component)
        {
            TwoWheelComponentEditor.ConfigureCar(component);
        }


        protected virtual void ConfigureVehicleComponent(VehicleComponent component)
        {
            VehicleComponentEditor.ConfigureCar(component);
        }


        protected virtual void SetLightsComponent(GameObject instance)
        {

        }


        private void SetupHandlebar(GameObject instance)
        {
            AddComponent(instance, typeof(UpdateHandlebar));
            AssignHandlebar(instance, instance.GetComponent<UpdateHandlebar>());
        }


        protected virtual void AssignHandlebar(GameObject instance, UpdateHandlebar updateHandlebar)
        {
            var handlebar = instance.transform.FindDeepChild("Handlebars");
            updateHandlebar.handlebar = handlebar;
        }


        protected virtual void ApplyPrefab(GameObject instance, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }


        protected virtual void SetCustomRigidbodyValues(Rigidbody rb)
        {
            rb.mass = 1000;
        }


        protected virtual void SetCustomComponentValues(TwoWheelComponent twoWheelComponent)
        {

        }

        protected virtual void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {

        }

        protected virtual void SetCustomComponentValues(TrailerComponent trailerComponent)
        {
            var truckConnectionPoint = trailerComponent.transform.FindDeepChild("Anchor_Point_Trailer");
            if (truckConnectionPoint != null)
            {
                trailerComponent.truckConnectionPoint = truckConnectionPoint;
            }
        }

        protected virtual void CustomVehicleSetup(GameObject instance)
        {

        }

        protected void CopyComponentToTarget<T>(GameObject source, GameObject target) where T : Component
        {
            T sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
            {
                return;
            }

            T targetComponent = target.AddComponent<T>();
            UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent);
            UnityEditorInternal.ComponentUtility.PasteComponentValues(targetComponent);
        }

        protected void MoveChildToParent(Transform instance, string childName, Transform newParent)
        {
            if (instance == null)
                return;

            Transform child = instance.Find(childName);
            if (child == null)
            {
                //Debug.LogWarning($"Child '{childName}' not found on '{instance.name}'");
                return;
            }
            child.SetParent(newParent, true);
        }

        private void ConnectTruckWithTrailer(string tractorPath, string trailerPath, TruckData truckData)
        {
            GameObject tractorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(tractorPath);
            GameObject trailerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(trailerPath);

            if (tractorAsset == null)
            {
                Debug.LogError($"Failed to load tractor prefab at: '{tractorPath}'");
                return;
            }
            if (trailerAsset == null)
            {
                Debug.LogError($"Failed to load trailer prefab at: '{trailerPath}'");
                return;
            }

            GameObject tractorInstance = (GameObject)PrefabUtility.InstantiatePrefab(tractorAsset);
            PrefabUtility.UnpackPrefabInstance(tractorInstance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            try
            {
                // nest trailer as a prefab instance inside the tractor
                GameObject trailerInstance = (GameObject)PrefabUtility.InstantiatePrefab(trailerAsset);
                trailerInstance.transform.SetParent(tractorInstance.transform, false);
                trailerInstance.name = "Trailer";

                // connect trailer to tractor on VehicleComponent
                VehicleComponent vehicleComponent = tractorInstance.GetComponent<VehicleComponent>();
                TrailerComponent trailerComponent = trailerInstance.GetComponent<TrailerComponent>();
                Transform vehicleHolder = tractorInstance.transform.Find("VehicleHolder");

                vehicleComponent.trailer = trailerComponent;
                vehicleComponent.trailerConnectionPoint = GetTractorAnchorPoint(vehicleHolder, truckData);

                VehicleComponentEditor.ConfigureCar(vehicleComponent);

                PrefabUtility.SaveAsPrefabAsset(tractorInstance, tractorPath);
            }
            finally
            {
                GameObject.DestroyImmediate(tractorInstance);
            }
        }

        private void ClearDestinationFolder(string destinationFolder)
        {
            string folderPath = $"{destinationFolder}/{ExpectedFolderName}";
            if (!Directory.Exists(folderPath))
                return;

            // delete via AssetDatabase to properly handle .meta files
            bool deleted = AssetDatabase.DeleteAsset(folderPath);
            if (deleted)
            {
                Debug.Log($"Cleared destination folder: '{folderPath}'");
            }
            else
            {
                Debug.LogWarning($"Failed to clear destination folder: '{folderPath}'");
            }

            AssetDatabase.Refresh();
        }

        protected virtual Transform GetTractorAnchorPoint(Transform vehicleHolder, TruckData truckData)
        {
            var anchorPoint = vehicleHolder.Find("Anchor_Point_Traktor");
            if (anchorPoint == null)
            {
                Debug.LogError("Anchor point not found.");
            }
            return anchorPoint;
        }


        private bool ValidateSourceFolder(string sourceFolder)
        {
            if (!sourceFolder.Contains(ExpectedFolderName))
            {
                Debug.LogError($"Source folder {sourceFolder} does not match the expected '{ExpectedFolderName}'. Please ensure you have the correct folder selected.");
                return false;
            }
            return true;
        }


        private void CopyPrefab(string sourcePath, string destinationPath)
        {
            if (!ValidateSourcePath(sourcePath))
                return;
            if (!ValidateDestinationPath(destinationPath))
                return;


            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            AssetDatabase.CopyAsset(sourcePath, destinationPath);
            AssetDatabase.Refresh();
        }


        private bool ValidateSourcePath(string sourcePath)
        {
            if (!sourcePath.StartsWith("Assets"))
            {
                Debug.LogError($"Source path must be inside the Unity project: '{sourcePath}'");
                return false;
            }

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                Debug.LogError($"Source path does not exist: '{sourcePath}'");
                return false;
            }

            return true;
        }


        private bool ValidateDestinationPath(string destinationPath)
        {
            if (!destinationPath.StartsWith("Assets"))
            {
                Debug.LogError($"Destination path must be inside the Unity project: '{destinationPath}'");
                return false;
            }

            if (!destinationPath.EndsWith(".prefab"))
            {
                Debug.LogError($"Destination path must point to a .prefab file: '{destinationPath}'");
                return false;
            }

            return true;
        }


        protected virtual PrefabUnpackMode GetPrefabUnpackMode()
        {
            return PrefabUnpackMode.OutermostRoot;
        }


        private GameObject PreparePrefab(string prefabPath, PrefabUnpackMode unpackMpde)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"Failed to load prefab at: '{prefabPath}'");
                return null;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject truckInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            PrefabUtility.UnpackPrefabInstance(truckInstance, unpackMpde, InteractionMode.AutomatedAction);
            return truckInstance;
        }


        protected void AddComponent(GameObject root, Type componentType)
        {
            if (componentType == null)
                return;
            if (root.GetComponent(componentType) == null)
            {
                root.AddComponent(componentType);
            }
            else
            {
                Debug.LogWarning($"Component of type '{componentType.Name}' already exists on: '{root.name}'");
            }
        }


        private GameObject WrapContentsInRoot(GameObject root, string wrapperName)
        {
            GameObject wrapper = new GameObject(wrapperName);
            wrapper.transform.SetParent(root.transform, false);

            // collect all direct children first to avoid modifying the collection while iterating
            List<Transform> children = new List<Transform>();
            foreach (Transform child in root.transform)
            {
                if (child != wrapper.transform)
                {
                    children.Add(child);
                }
            }

            foreach (Transform child in children)
            {
                child.SetParent(wrapper.transform, true);
            }
            return wrapper;
        }


        private void SetupColliders(Transform root, ColliderProperties[] colliders)
        {   
            foreach (var collider in colliders)
            {
                AddCollider(root, collider.Center, collider.Size, collider.Name);
            }
        }


        protected Transform SetCollidersParent(Transform root)
        {
            var colliderHolder = root.transform.Find("Colliders");
            if (colliderHolder == null)
            {
                var go = new GameObject("Colliders");
                colliderHolder = go.transform;
                colliderHolder.SetParent(root.transform, false);
            }
            return colliderHolder;
        }


        private void SetupWheels(Transform root)
        {
            var wheelsParent = SetWheelParent(root);

            MoveWheelsInsideParent(root, wheelsParent);
            var wheelArray = new List<Transform>();
            foreach (Transform wheel in wheelsParent)
            {
                wheelArray.Add(wheel);
            }

            foreach (Transform wheel in wheelArray)
            {
                CreateWheelStructure(wheel);
            }
        }


        protected virtual void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {

        }


        protected virtual void CreateWheelStructure(Transform wheelRoot)
        {

        }


        protected virtual Transform SetWheelParent(Transform root)
        {
            var wheelsParent = root.Find("Wheels");
            if (wheelsParent == null)
            {
                wheelsParent = new GameObject("Wheels").transform;
                wheelsParent.SetParent(root);
            }
            return wheelsParent;
        }


        private void CreateVehiclePool(string destinationFolder, string poolName, List<string> prefabPaths)
        {
            VehiclePool pool = ScriptableObject.CreateInstance<VehiclePool>();

            pool.trafficCars = prefabPaths.Select(path =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"Could not load prefab at: '{path}'");
                    return null;
                }

                var carType = new CarType();
                typeof(CarType)
                    .GetField("vehiclePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(carType, prefab);

                return carType;
            })
            .Where(c => c != null)
            .ToArray();

            string poolPath = $"{destinationFolder}/{poolName}.asset";
            AssetDatabase.CreateAsset(pool, poolPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Vehicle pool created at: '{poolPath}' with {pool.trafficCars.Length} vehicles.", pool);
        }


        protected virtual void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            var colliderHolder = SetCollidersParent(root);
            GameObject colliderObject = new GameObject(colliderName);
            colliderObject.transform.SetParent(colliderHolder.transform, false);

            BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();

            // convert world-space bounds to local space of the collider object
            boxCollider.center = colliderCenter;
            boxCollider.size = colliderSize;
        }


        private bool SetTrafficLayer(GameObject root)
        {
            var layerSetup = Resources.Load<LayerSetup>(TrafficSystemConstants.layerSetupData);
            if (layerSetup == null)
            {
                Debug.LogError(TrafficSystemErrors.LayersNotConfigured);
                return false;
            }

            // get the first valid traffic layer index
            int trafficLayer = -1;
            for (int i = 0; i < 32; i++)
            {
                if ((layerSetup.trafficLayers.value & (1 << i)) != 0)
                {
                    trafficLayer = i;
                    break;
                }
            }

            if (trafficLayer == -1)
            {
                Debug.LogError("No traffic layers configured. Please set up traffic layers first.");
                return false;
            }

            // apply layer to root and all children
            SetLayerRecursively(root, trafficLayer);
            return true;
        }


        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
