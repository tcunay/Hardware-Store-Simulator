using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class ToonIndustriesConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Toon Industries";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Cars/Pick_Up_1A.prefab"),
            new("Prefabs/Cars/Pick_Up_1B.prefab"),
            new("Prefabs/Cars/Truck_1A.prefab"),
            new("Prefabs/Cars/Truck_1B.prefab"),
            new("Prefabs/Cars/Truck_1C.prefab"),
            new("Prefabs/Cars/Van_1A.prefab"),
            new("Prefabs/Cars/Van_1B.prefab"),
            new("Prefabs/Cars/Cistern_1A.prefab"),
            new("Prefabs/Cars/Cistern_1B.prefab"),
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

        protected override void CustomVehicleSetup(GameObject instance)
        {
            if (instance.name.Contains("Truck_1C"))
            {
                var objToDelete = instance.transform.Find("Truck_1C");
                MoveObjectsToParent(objToDelete, instance);
                instance.transform.Find("Truck_1C_Container").GetComponent<MeshCollider>().convex = true;

                base.CustomVehicleSetup(instance);
                return;
            }
            if (instance.name.Contains("Cistern_1"))
            {
                var name = instance.name.Substring(0, instance.name.Length - 4);
                var objToDelete = instance.transform.Find(name + "_Body");
                MoveObjectsToParent(objToDelete, instance);
                var toDestroy = instance.transform.Find(name + "_Addon");
                if (toDestroy != null)
                {
                    Object.DestroyImmediate(toDestroy.gameObject);
                }
                toDestroy = instance.transform.Find(name + "_Addon.");
                if (toDestroy != null)
                {
                    Object.DestroyImmediate(toDestroy.gameObject);
                }
                base.CustomVehicleSetup(instance);
                return;
            }


            base.CustomVehicleSetup(instance);

        }

        void MoveObjectsToParent(Transform objToDelete, GameObject instance)
        {
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
        }
    }
}
