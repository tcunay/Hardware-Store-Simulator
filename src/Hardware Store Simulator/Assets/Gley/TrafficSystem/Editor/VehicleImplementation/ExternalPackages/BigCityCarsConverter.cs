using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class BigCityCarsConverter : GenericConverter
    {
        protected override string ExpectedFolderName => "Big City Traffic Cars Pack - Modern Land Vehicles";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Car01.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.1f), new Vector3(1.75f,1.16f,3.9f), "Body")}),
            new("Prefabs/Car01_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.1f), new Vector3(1.75f,1.16f,3.9f), "Body")}),
            new("Prefabs/Car01_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.1f), new Vector3(1.75f,1.16f,3.9f), "Body")}),
            new("Prefabs/Car01_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.1f), new Vector3(1.75f,1.16f,3.9f), "Body")}),
            new("Prefabs/Car01_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.1f), new Vector3(1.75f,1.16f,3.9f), "Body")}),
            new("Prefabs/Car02.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.16f), new Vector3(1.85f,1.15f,4.95f), "Body")}),
            new("Prefabs/Car02_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.16f), new Vector3(1.85f,1.15f,4.95f), "Body")}),
            new("Prefabs/Car02_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.16f), new Vector3(1.85f,1.15f,4.95f), "Body")}),
            new("Prefabs/Car02_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.16f), new Vector3(1.85f,1.15f,4.95f), "Body")}),
            new("Prefabs/Car02_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.16f), new Vector3(1.85f,1.15f,4.95f), "Body")}),
            new("Prefabs/Car03.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car03_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car03_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car03_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car03_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car04.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car04_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car04_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car04_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car04_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,4.5f), "Body")}),
            new("Prefabs/Car05.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.025f), new Vector3(1.85f,1.15f,4.7f), "Body")}),
            new("Prefabs/Car05_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.025f), new Vector3(1.85f,1.15f,4.7f), "Body")}),
            new("Prefabs/Car05_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.025f), new Vector3(1.85f,1.15f,4.7f), "Body")}),
            new("Prefabs/Car05_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.025f), new Vector3(1.85f,1.15f,4.7f), "Body")}),
            new("Prefabs/Car05_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,0.025f), new Vector3(1.85f,1.15f,4.7f), "Body")}),
            new("Prefabs/Car06.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car06_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car06_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car06_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car06_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car07.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car07_Police.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car07_Taxi.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car07_With Flashing light_Blue.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
            new("Prefabs/Car07_With Flashing light_Yellow.prefab", new ColliderProperties[]{new ColliderProperties(new Vector3(0,0.9f,-0.05f), new Vector3(1.85f,1.15f,5f), "Body")}),
        };

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            base.MoveWheelsInsideParent(root, wheelsParent);
            MoveChildToParent(root, $"FL", wheelsParent);
            MoveChildToParent(root, $"FR", wheelsParent);
            MoveChildToParent(root, $"RL", wheelsParent);
            MoveChildToParent(root, $"RR", wheelsParent);

            foreach (Transform child in wheelsParent)
            {
                string childName = child.name;
                childName = childName.Replace("FL", "Front_L");
                childName = childName.Replace("FR", "Front_R");
                childName = childName.Replace("RL", "Rear_L");
                childName = childName.Replace("RR", "Rear_R");
                child.name = childName;
            }
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
            if (wheelRoot.name.Contains("L"))
            {
                wheelRoot.localEulerAngles = new Vector3(0, -90, 0);
            }
            else
            {
                wheelRoot.localEulerAngles = new Vector3(0, 90, 0);
            }
        }

        protected override PrefabUnpackMode GetPrefabUnpackMode()
        {
            return PrefabUnpackMode.Completely;
        }
    }
}
