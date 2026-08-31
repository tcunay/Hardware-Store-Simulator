using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonVehiclesConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon Vehicles";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Cars/Car_1A.prefab"),
            new("Prefabs/Cars/Car_1B.prefab"),
            new("Prefabs/Cars/Car_2A.prefab"),
            new("Prefabs/Cars/Car_2B.prefab"),
            new("Prefabs/Cars/Car_2C.prefab"),
            new("Prefabs/Cars/Car_3A.prefab"),
            new("Prefabs/Cars/Car_4A.prefab"),
            new("Prefabs/Cars/Car_4C.prefab"),
            new("Prefabs/Cars/Car_6A.prefab"),
            new("Prefabs/Cars/Car_6B.prefab"),
            new("Prefabs/Cars/Car_6C.prefab"),
            new("Prefabs/Cars/Car_7A.prefab"),
            new("Prefabs/Cars/Car_7B.prefab"),
            new("Prefabs/Cars/Car_7C.prefab"),
            new("Prefabs/Cars/Car_8A.prefab"),
            new("Prefabs/Cars/Car_8B.prefab"),
            new("Prefabs/Cars/Car_8C.prefab"),
            new("Prefabs/Cars/Car_8D.prefab"),
            new("Prefabs/Cars/Car_8E.prefab"),
            new("Prefabs/Cars/Car_9A.prefab"),
            new("Prefabs/Cars/Car_10A.prefab"),
            new("Prefabs/Cars/Car_10B.prefab"),
            new("Prefabs/Cars/Car_14A.prefab"),
            new("Prefabs/Cars/Car_14B.prefab"),
            new("Prefabs/Cars/Car_14E.prefab"),
            new("Prefabs/Cars/Car_14F.prefab"),
            new("Prefabs/Cars/Car_14G.prefab"),
            new("Prefabs/Cars/Car_14H.prefab"),
            new("Prefabs/Cars/Car_15A.prefab"),
            new("Prefabs/Cars/Car_18A.prefab"),
            new("Prefabs/Cars/Car_18B.prefab"),
            new("Prefabs/Cars/Car_19A.prefab"),
            new("Prefabs/Cars/Car_19B.prefab"),
            new("Prefabs/Cars/Car_20A.prefab"),
            new("Prefabs/Cars/Car_20B.prefab"),
            new("Prefabs/Cars/Car_22A.prefab"),
            new("Prefabs/Cars/Car_22B.prefab"),
            new("Prefabs/Cars/Car_22C.prefab"),
            new("Prefabs/Cars/Car_23A.prefab"),
            new("Prefabs/Cars/Car_23B.prefab"),
        };

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
            if(name == "18A")
            {
                name = "18B";
            }

            if(name == "18B")
            {
                MoveChildToParent(root, $"Wheel_{name}_F.L.001", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_F.R.001", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_R.L.001", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_R.R.001", wheelsParent);
            }

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

        protected override void MoveGraphics(Transform instance, Transform graphics)
        {
            base.MoveGraphics(instance, graphics);
            var name = instance.name.Split('_')[1];
            MoveChildToParent(instance, "Smoke_Grey", graphics);
            MoveChildToParent(instance, "Smoke_Grey (1)", graphics);
            MoveChildToParent(instance, "Smoke_Grey (2)", graphics);
            MoveChildToParent(instance, "Smoke_Grey (3)", graphics);
            MoveChildToParent(instance, "Smoke_Grey (4)", graphics);
            MoveChildToParent(instance, "Smoke_Grey (5)", graphics);
            MoveChildToParent(instance, $"Door_{name}_R.L", graphics);
            MoveChildToParent(instance, $"Door_{name}_R.R", graphics);
            MoveChildToParent(instance, "Light_Red", graphics);
            MoveChildToParent(instance, "Light_Red_1", graphics);
            MoveChildToParent(instance, "Light_Red_2", graphics);
            MoveChildToParent(instance, "Light_Blue", graphics);
            MoveChildToParent(instance, "Cylinder_Pivot", graphics);
            MoveChildToParent(instance, "Antenna", graphics);
            MoveChildToParent(instance, $"Tailgate_{name}", graphics);
            MoveChildToParent(instance, $"Car_{name}_Livery", graphics);
            MoveChildToParent(instance, $"Car_{name}_Spoiler", graphics);
            MoveChildToParent(instance, $"Car_{name}_Bench", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.L", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.R", graphics);
            MoveChildToParent(instance, $"Car_{name}_Steering_Wheel", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.F.L", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.F.R", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.R.L", graphics);
            MoveChildToParent(instance, $"Car_{name}_Seat.R.R", graphics);
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            if(vehicleComponent.name== "Car_15A_MTS")
            {
                vehicleComponent.triggerLength = 8;
                vehicleComponent.distanceToStop = 5;
                vehicleComponent.steeringTime = 0.7f;
            }
        }
    }
}
