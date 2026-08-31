using Gley.UrbanSystem;
using Gley.UrbanSystem.Editor;
using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonSuburbanPackConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon Suburban Pack";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
             new("Prefabs/Vehicles/TSP_Garbage_Truck_01A.prefab"),
             new("Prefabs/Vehicles/TSP_Minivan_01A.prefab"),
             new("Prefabs/Vehicles/TSP_Minivan_01B.prefab"),
             new("Prefabs/Vehicles/TSP_Minivan_01C.prefab"),
             new("Prefabs/Vehicles/TSP_Minivan_01C.prefab"),
             new("Prefabs/Vehicles/TSP_Sedan_01A.prefab"),
             new("Prefabs/Vehicles/TSP_Sedan_01B.prefab"),
             new("Prefabs/Vehicles/TSP_Sedan_01C.prefab"),
             new("Prefabs/Vehicles/TSP_Sportscar_01A.prefab"),
             new("Prefabs/Vehicles/TSP_Sportscar_01B.prefab"),
             new("Prefabs/Vehicles/TSP_Sportscar_01C.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_01A.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_01B.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_01C.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_02A.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_02B.prefab"),
             new("Prefabs/Vehicles/TSP_SUV_02C.prefab"),
        };

        protected override void SetLightsComponent(GameObject instance)
        {
            AddComponent(instance, typeof(VehicleLightsComponent));
            var lightsComponent = instance.GetComponent<VehicleLightsComponent>();
            var name = instance.name.Substring(0, instance.name.Length - 4);
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
    }
}
