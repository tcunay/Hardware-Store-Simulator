using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public abstract class SicsConverter : GenericConverter
    {
        protected override void CustomVehicleSetup(GameObject instance)
        {
            base.CustomVehicleSetup(instance);

            var graphics = new GameObject("Graphics");
            graphics.transform.SetParent(instance.transform);
            var body = new GameObject("Body");
            body.transform.SetParent(graphics.transform);

            CopyComponentToTarget<MeshFilter>(instance, body);
            CopyComponentToTarget<MeshRenderer>(instance, body);

            MoveGraphics(instance.transform, graphics.transform);

            // remove originals from root after copying
            Object.DestroyImmediate(instance.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(instance.GetComponent<MeshFilter>());
            Object.DestroyImmediate(instance.GetComponent<Animator>());
        }

        protected virtual void MoveGraphics(Transform instance, Transform graphics)
        {

        }

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            base.MoveWheelsInsideParent(root, wheelsParent);
            MoveChildToParent(root, "Wheel_Front_L", wheelsParent);
            MoveChildToParent(root, "Wheel_Front_R", wheelsParent);
            MoveChildToParent(root, "Wheel_Rear_L", wheelsParent);
            MoveChildToParent(root, "Wheel_Rear_R", wheelsParent);
            MoveChildToParent(root, "Wheel_Middle_L", wheelsParent);
            MoveChildToParent(root, "Wheel_Middle_R", wheelsParent);
            MoveChildToParent(root.Find("Handlebars"), "Wheel_Front", wheelsParent);
            MoveChildToParent(root, "Wheel_Rear", wheelsParent);
        }

        protected override void CreateWheelStructure(Transform wheelRoot)
        {
            base.CreateWheelStructure(wheelRoot);
            GameObject parent = new GameObject(wheelRoot.name);
            parent.transform.SetParent(wheelRoot.parent);
            GameObject suspension = new GameObject("Suspension");
            suspension.transform.SetParent(parent.transform);
            wheelRoot.SetParent(suspension.transform);
            parent.transform.localPosition = new Vector3(wheelRoot.localPosition.x, 0, wheelRoot.localPosition.z);
            suspension.transform.localPosition = new Vector3(0, wheelRoot.localPosition.y, 0);
            wheelRoot.localPosition = Vector3.zero;
            Object.DestroyImmediate(wheelRoot.GetComponent<WheelCollider>());
            Object.DestroyImmediate(wheelRoot.GetComponent<Rigidbody>());
        }

        protected override void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            BoxCollider[] sourceColliders = root.GetComponents<BoxCollider>();
            if (sourceColliders.Length == 0)
            {
                Debug.LogWarning($"No BoxColliders found on '{root.name}'");
                return;
            }

            if (colliderCenter != Vector3.zero)
            {
                base.AddCollider(root, colliderCenter, colliderSize, colliderName);
                foreach (BoxCollider sourceCollider in sourceColliders)
                {
                    Object.DestroyImmediate(sourceCollider);
                }
                return;
            }


            var colliderHolder = SetCollidersParent(root);
            foreach (BoxCollider sourceCollider in sourceColliders)
            {
                BoxCollider targetCollider = colliderHolder.gameObject.AddComponent<BoxCollider>();
                UnityEditorInternal.ComponentUtility.CopyComponent(sourceCollider);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(targetCollider);
                Object.DestroyImmediate(sourceCollider);
            }
        }
    }
}
