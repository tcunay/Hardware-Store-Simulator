using Gley.UrbanSystem;
using Gley.UrbanSystem.Editor;
using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonPlanesPackConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon Planes Pack";
        protected override List<VehicleData> VehiclesToConvert => new() 
        {
            new("Prefabs/Vehicles/TP_Bus_01A.prefab"),
            new("Prefabs/Vehicles/TP_Bus_01B.prefab"),
            new("Prefabs/Vehicles/TP_Bus_01C.prefab"),
            new("Prefabs/Vehicles/TP_Loading_Truck_01A.prefab"),
            new("Prefabs/Vehicles/TP_Loading_Truck_01B.prefab"),
            new("Prefabs/Vehicles/TP_Loading_Truck_02A.prefab"),
            new("Prefabs/Vehicles/TP_Loading_Truck_02B.prefab"),
            new("Prefabs/Vehicles/TP_Pick_Up_01A.prefab"),
            new("Prefabs/Vehicles/TP_Pick_Up_01B.prefab"),
            new("Prefabs/Vehicles/TP_Pick_Up_01C.prefab"),
            new("Prefabs/Vehicles/TP_SUV_01A.prefab"),
            new("Prefabs/Vehicles/TP_SUV_01B.prefab"),
            new("Prefabs/Vehicles/TP_SUV_01C.prefab"),
            new("Prefabs/Vehicles/TP_Taxi_01A.prefab"),
            new("Prefabs/Vehicles/TP_Taxi_01B.prefab"),
            new("Prefabs/Vehicles/TP_Taxi_01C.prefab"),
            new("Prefabs/Vehicles/TP_Van_01A.prefab"),
            new("Prefabs/Vehicles/TP_Van_01B.prefab"),
            new("Prefabs/Vehicles/TP_Van_01C.prefab"),
            new("Prefabs/Vehicles/TP_Van_02A.prefab"),
            new("Prefabs/Vehicles/TP_Van_02B.prefab"),
            new("Prefabs/Vehicles/TP_Van_02C.prefab"),
            new("Prefabs/Vehicles/TP_Truck_Cabin_01A.prefab"),
            new("Prefabs/Vehicles/TP_Truck_Cabin_01B.prefab"),
            new("Prefabs/Vehicles/TP_Truck_Cabin_01C.prefab"),
        };


        protected override List<TruckData> TrucksToConvert => new() 
        {
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01A.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01A.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01B.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01A.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01C.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01A.prefab")),

             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01A.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01B.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01B.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01B.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01C.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01B.prefab")),

             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01A.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01C.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01B.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01C.prefab")),
             new(new VehicleData("Prefabs/Vehicles/TP_Truck_Cabin_01C.prefab"),
                new VehicleData("Prefabs/Vehicles/TP_Cistern_01C.prefab")),
        };

        protected override void SetLightsComponent(GameObject instance)
        {
            var name = instance.name.Substring(0, instance.name.Length - 4);
            if (name.Contains("+"))
            {
                name = name.Split("+")[0];
            }
            if (instance.name.Contains("SUV"))
            {
                AddComponent(instance, typeof(VehicleLightsComponentV2));
                var lightsComponentV2 = instance.GetComponent<VehicleLightsComponentV2>();

                var lightV2 = instance.transform.FindDeepChild(name + "_Headlights");
                if (lightV2 != null)
                {
                    lightsComponentV2.frontLights =new GameObject[] { lightV2.gameObject };
                }
                lightV2 = instance.transform.FindDeepChild(name + "_Lights");
                if (lightV2 != null)
                {
                    lightsComponentV2.frontLights = new GameObject[] { lightV2.gameObject };
                }

                lightV2 = instance.transform.FindDeepChild(name + "_Taillights");
                if (lightV2 != null)
                {
                    lightsComponentV2.stopLights =new GameObject[] { lightV2.gameObject };
                }

                lightV2 = instance.transform.FindDeepChild(name + "_Blinker_F.L");
                var secondLight = instance.transform.FindDeepChild(name + "_Blinker_R.L");
                if (lightV2 != null)
                {
                    lightsComponentV2.blinkerLeft = new GameObject[] { lightV2.gameObject, secondLight.gameObject };
                }

                lightV2 = instance.transform.FindDeepChild(name + "_Blinker_F.R");
                secondLight = instance.transform.FindDeepChild(name + "_Blinker_R.R");
                if (lightV2 != null)
                {
                    lightsComponentV2.blinkerRight = new GameObject[] { lightV2.gameObject, secondLight.gameObject };
                }
                return;
            }


            AddComponent(instance, typeof(VehicleLightsComponent));
            var lightsComponent = instance.GetComponent<VehicleLightsComponent>();
            
            var light = instance.transform.FindDeepChild(name + "_Headlights");
            if (light != null)
            {
                lightsComponent.frontLights = light.gameObject;
            }
            light = instance.transform.FindDeepChild(name + "_Lights");
            if (light != null)
            {
                lightsComponent.frontLights = light.gameObject;
            }

            light = instance.transform.FindDeepChild(name + "_Taillights");
            if (light != null)
            {
                lightsComponent.stopLights = light.gameObject;
            }
            light = instance.transform.FindDeepChild(name + "_Reverse_Lights");
            if (light != null)
            {
                lightsComponent.reverseLights = light.gameObject;
            }

            light = instance.transform.FindDeepChild(name + "_Blinkers.L");
            if (light != null)
            {
                lightsComponent.blinkerLeft = light.gameObject;
            }

            light = instance.transform.FindDeepChild(name + "_Blinkers.R");
            if (light != null)
            {
                lightsComponent.blinkerRight = light.gameObject;
            }
        }

        protected override void CustomVehicleSetup(GameObject instance)
        {
            //base.CustomVehicleSetup(instance);
            if(instance.name.Contains("+"))
            {
                var truckConnectionPoint = new GameObject("Anchor_Point_Traktor");
                truckConnectionPoint.transform.SetParent(instance.transform);
            }

        }

        protected override void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            //base.AddCollider(root, colliderHolder, colliderCenter, colliderSize, colliderName);
            var allMeshColliders = root.GetComponentsInChildren<MeshCollider>();
            foreach (var collider in allMeshColliders)
            {
                collider.convex = true;
            }
        }

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            
            var name = root.name.Substring(0, root.name.Length - 4);
            if(name.Contains("+"))
            {
                name = name.Split("+")[0];
            }
            
            base.MoveWheelsInsideParent(root, wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_F.L", wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_F.R", wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_R.L", wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_R.R", wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_M.L", wheelsParent);
            MoveChildToParent(root, $"{name}_Wheel_M.R", wheelsParent);

            foreach (Transform child in wheelsParent)
            {
                string childName = child.name;
                childName = childName.Replace("F.L", "Front_L");
                childName = childName.Replace("F.R", "Front_R");
                childName = childName.Replace("R.L", "Rear_L");
                childName = childName.Replace("R.R", "Rear_R");
                childName = childName.Replace("M.L", "Rear_L2");
                childName = childName.Replace("M.R", "Rear_R2");
                child.name = childName;
            }
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            if (vehicleComponent.name.Contains("Bus")||
                vehicleComponent.name.Contains("Loading_Truck"))
            {
                vehicleComponent.triggerLength = 10;
                vehicleComponent.distanceToStop = 6;
                vehicleComponent.steeringTime = 0.7f;
                vehicleComponent.forwardLeaningFactor = 0.1f;
            }
        }

        protected override void SetCustomComponentValues(TrailerComponent trailerComponent)
        {
            base.SetCustomComponentValues(trailerComponent);
            var truckConnectionPoint = new GameObject("TruckConnectionPoint");
            truckConnectionPoint.transform.SetParent(trailerComponent.transform.Find("TrailerHolder"));
            trailerComponent.truckConnectionPoint = truckConnectionPoint.transform;
        }
    }
}
