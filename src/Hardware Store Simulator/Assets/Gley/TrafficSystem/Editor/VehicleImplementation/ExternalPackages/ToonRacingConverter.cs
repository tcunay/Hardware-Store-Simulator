using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonRacingConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon Racing";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Cars/Rally/Rally_Car_1A.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_1B.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_1C.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_2A.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_2B.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_2C.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_3A.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_3B.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_3C.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_4A.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_4B.prefab"),
            new("Prefabs/Cars/Rally/Rally_Car_4C.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_5A.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_5B.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_5C.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_6A.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_6B.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_6C.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_7A.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_7B.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_7C.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_8A.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_8B.prefab"),
            new("Prefabs/Cars/Racing/Race_Car_8C.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_9A.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_9B.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_10A.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_10B.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_11A.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_11B.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_12A.prefab"),
            new("Prefabs/Cars/Monster Trucks/Monster_Truck_12B.prefab"),

        };

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            var name = root.name.Split('_')[2];

            if (root.name.Contains("Monster"))
            {
                if (name.Contains("12"))
                {
                    var suspension = root.Find($"Monster_Truck_{name}_Suspension.F.L");
                    MoveChildToParent(suspension, $"Wheel_{name}_F.L", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension.F.R");
                    MoveChildToParent(suspension, $"Wheel_{name}_F.R", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension.R.L");
                    MoveChildToParent(suspension, $"Wheel_{name}_R.L", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension.R.R");
                    MoveChildToParent(suspension, $"Wheel_{name}_R.R", wheelsParent);
                }
                else
                {
                    var suspension = root.Find($"Monster_Truck_{name}_Suspension_F.L");
                    MoveChildToParent(suspension, $"Wheel_{name}_F.L", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension_F.R");
                    MoveChildToParent(suspension, $"Wheel_{name}_F.R", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension_R.L");
                    MoveChildToParent(suspension, $"Wheel_{name}_R.L", wheelsParent);
                    suspension = root.Find($"Monster_Truck_{name}_Suspension_R.R");
                    MoveChildToParent(suspension, $"Wheel_{name}_R.R", wheelsParent);
                }
            }

            else
            {
                base.MoveWheelsInsideParent(root, wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_F.L", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_F.R", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_R.L", wheelsParent);
                MoveChildToParent(root, $"Wheel_{name}_R.R", wheelsParent);
            }

            foreach (Transform child in wheelsParent)
            {
                string childName = child.name;
                childName = childName.Replace("F.L", "Front_L");
                childName = childName.Replace("F.R", "Front_R");
                childName = childName.Replace("R.L", "Rear_L");
                childName = childName.Replace("R.R", "Rear_R");
                child.name = childName;
            }
        }

        protected override void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            if (root.name.Contains("Monster") && (root.name.Contains("10") || root.name.Contains("11") || root.name.Contains("12")))
            {
                var meshColliders = root.GetComponentsInChildren<MeshCollider>();
                foreach (var sourceCollider in meshColliders)
                {
                    if (sourceCollider.name.Contains("Body"))
                    {
                        continue;
                    }
                    Object.DestroyImmediate(sourceCollider);
                }
                return;
            }

            if (root.name.Contains("Monster_Truck_9B"))
            {
                Object.DestroyImmediate(root.Find("Monster_Truck_9B_Body").GetComponent<MeshCollider>());
            }


            if (root.name.Contains("Race"))
            {
                var meshColliders = root.GetComponentsInChildren<MeshCollider>();
                foreach (var sourceCollider in meshColliders)
                {
                    if (sourceCollider.name.Contains("Body"))
                    {
                        continue;
                    }
                    Object.DestroyImmediate(sourceCollider);
                }
                return;
            }
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
                targetCollider.isTrigger = false;
                Object.DestroyImmediate(sourceCollider);
            }
        }
    }
}
