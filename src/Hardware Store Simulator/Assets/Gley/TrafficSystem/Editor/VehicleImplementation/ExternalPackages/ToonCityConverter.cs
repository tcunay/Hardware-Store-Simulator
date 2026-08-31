using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonCityConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon City";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Vehicles/Car_2D.prefab"),
            new("Prefabs/Vehicles/Car_3B.prefab"),
            new("Prefabs/Vehicles/Car_3C.prefab"),
            new("Prefabs/Vehicles/Car_4D.prefab"),
            new("Prefabs/Vehicles/Car_4E.prefab"),
            new("Prefabs/Vehicles/Car_6D.prefab"),
            new("Prefabs/Vehicles/Car_6E.prefab"),
            new("Prefabs/Vehicles/Car_6F.prefab"),
            new("Prefabs/Vehicles/Car_7D.prefab"),
            new("Prefabs/Vehicles/Car_7E.prefab"),
            new("Prefabs/Vehicles/Car_7F.prefab"),
            new("Prefabs/Vehicles/Car_8D.prefab"),
            new("Prefabs/Vehicles/Car_9A.prefab"),
            new("Prefabs/Vehicles/Car_10C.prefab"),
            new("Prefabs/Vehicles/Car_10D.prefab"),
            new("Prefabs/Vehicles/Car_11A.prefab"),
            new("Prefabs/Vehicles/Car_14C.prefab"),
            new("Prefabs/Vehicles/Car_14D.prefab"),
            new("Prefabs/Vehicles/Car_15B.prefab"),
            new("Prefabs/Vehicles/Car_15C.prefab"),
            new("Prefabs/Vehicles/Car_15D.prefab"),
            new("Prefabs/Vehicles/Car_16A.prefab"),
            new("Prefabs/Vehicles/Car_16B.prefab"),
            new("Prefabs/Vehicles/Car_17A.prefab"),
            new("Prefabs/Vehicles/Car_17B.prefab"),
        };

        protected override void CustomVehicleSetup(GameObject instance)
        {
            if (instance.name.Contains("Car_11A"))
            {
                var objToDelete = instance.transform.Find("Car_11A");
                CopyComponentToTarget<MeshFilter>(objToDelete.gameObject, instance);
                CopyComponentToTarget<MeshRenderer>(objToDelete.gameObject, instance);
                CopyComponentToTarget<MeshCollider>(objToDelete.gameObject, instance);
                Object.DestroyImmediate(objToDelete.gameObject.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(objToDelete.gameObject.GetComponent<MeshFilter>());
                Object.DestroyImmediate(objToDelete.gameObject.GetComponent<MeshCollider>());

                List<Transform> children = new List<Transform>();
                foreach (Transform child in objToDelete)
                {
                    children.Add(child);
                }

                foreach (Transform child in children)
                {
                    child.SetParent(instance.transform, true);
                }
                base.CustomVehicleSetup(instance);
            }
            else
            {
                base.CustomVehicleSetup(instance);
            }
        }

        protected override void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            var sourceColliders = root.GetComponents<MeshCollider>();
            if (sourceColliders.Length == 0)
            {
                base.AddCollider(root, colliderCenter, colliderSize, colliderName);
                return;
            }

            if (colliderCenter != Vector3.zero)
            {
                base.AddCollider(root, colliderCenter, colliderSize, colliderName);
                foreach (var sourceCollider in sourceColliders)
                {
                    Object.DestroyImmediate(sourceCollider);
                }
                return;
            }

            var colliderHolder = SetCollidersParent(root);
            foreach (var sourceCollider in sourceColliders)
            {
                var targetCollider = colliderHolder.gameObject.AddComponent<MeshCollider>();
                UnityEditorInternal.ComponentUtility.CopyComponent(sourceCollider);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(targetCollider);
                targetCollider.convex = true;
                Object.DestroyImmediate(sourceCollider);
            }
        }

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            var name = root.name.Split('_')[1];

            base.MoveWheelsInsideParent(root, wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_F.L", wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_F.R", wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_R.L", wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_R.R", wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_M.L", wheelsParent);
            MoveChildToParent(root, $"Wheel_{name}_M.R", wheelsParent);

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
            if (vehicleComponent.name.Contains("Car_15B") ||
                vehicleComponent.name.Contains("Car_17"))
            {
                vehicleComponent.triggerLength = 8;
                vehicleComponent.distanceToStop = 5;
                vehicleComponent.steeringTime = 0.7f;
            }
        }
    }
}
