using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class FantasticCityConverter : GenericConverter
    {
        protected override string ExpectedFolderName => "Fantastic City Generator";

        protected override List<VehicleData> VehiclesToConvert => new ()
        {
             new("Traffic System/Vehicles/Prefabs/BusCaio.prefab"),
             new("Traffic System/Vehicles/Prefabs/BusClimm.prefab"),
             new("Traffic System/Vehicles/Prefabs/BusMirim.prefab"),
             new("Traffic System/Vehicles/Prefabs/Car_06.prefab"),
             new("Traffic System/Vehicles/Prefabs/Fiat 147-b.prefab"),
             new("Traffic System/Vehicles/Prefabs/Fiat 147.prefab"),
             new("Traffic System/Vehicles/Prefabs/Furgao.prefab"),
             new("Traffic System/Vehicles/Prefabs/Gontijo.prefab"),
             new("Traffic System/Vehicles/Prefabs/GranFury-2.prefab"),
             new("Traffic System/Vehicles/Prefabs/GranFury.prefab"),
             new("Traffic System/Vehicles/Prefabs/Soul.prefab"),
             new("Traffic System/Vehicles/Prefabs/Tempra-2.prefab"),
             new("Traffic System/Vehicles/Prefabs/Tempra.prefab"),
             new("Traffic System/Vehicles/Prefabs/Truck-01.prefab"),
             new("Traffic System/Vehicles/Prefabs/Vesta.prefab"),
        };


        protected override void CustomVehicleSetup(GameObject instance)
        {
            var trafficCarType = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("FCG.TrafficCar"))
                .FirstOrDefault(type => type != null);

            if (trafficCarType != null)
            {
                var trafficCar = instance.GetComponent(trafficCarType);
                if (trafficCar != null)
                {
                    Object.DestroyImmediate(trafficCar);
                }
            }
            var wheelCollider = instance.GetComponentsInChildren<WheelCollider>();
            foreach(var collider in wheelCollider)
            {
                Object.DestroyImmediate(collider.gameObject);
            }    
        }

        protected override void MoveWheelsInsideParent(Transform root, Transform wheelsParent)
        {
            base.MoveWheelsInsideParent(root, wheelsParent);
            MoveChildToParent(root, "BL", wheelsParent);
            MoveChildToParent(root, "BL2", wheelsParent);
            MoveChildToParent(root, "BR", wheelsParent);
            MoveChildToParent(root, "BR2", wheelsParent);
            MoveChildToParent(root, "FL", wheelsParent);
            MoveChildToParent(root, "FR", wheelsParent);

            foreach (Transform child in wheelsParent)
            {
                string childName = child.name;
                childName = childName.Replace("FL", "FrontLeft");
                childName = childName.Replace("FR", "FrontRight");
                childName = childName.Replace("BL", "RearLeft");
                childName = childName.Replace("BR", "RearRight");
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
        }

        protected override void AddCollider(Transform root, Vector3 colliderCenter, Vector3 colliderSize, string colliderName)
        {
            //base.AddCollider(root, colliderCenter, colliderSize, colliderName);
        }

        protected override void ConfigureVehicleComponent(VehicleComponent component)
        {
            base.ConfigureVehicleComponent(component);
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            vehicleComponent.steeringTime = 0.7f;
        }
    }
}
