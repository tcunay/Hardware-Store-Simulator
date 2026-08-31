using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class CompleteVehiclePackConverter : SicsConverter
    {
        protected override string ExpectedFolderName => "Complete Vehicle Pack";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs_Mobile/Ambulance_1A.prefab"),
            new("Prefabs_Mobile/Bus_1A.prefab"),
            new("Prefabs_Mobile/Bus_1B.prefab"),
            new("Prefabs_Mobile/Bus_1C.prefab"),
            new("Prefabs_Mobile/Compact_1A.prefab"),
            new("Prefabs_Mobile/Compact_1B.prefab"),
            new("Prefabs_Mobile/Compact_2A.prefab"),
            new("Prefabs_Mobile/Compact_2B.prefab"),
            new("Prefabs_Mobile/Coupe_1A.prefab"),
            new("Prefabs_Mobile/Coupe_1B.prefab"),
            new("Prefabs_Mobile/Coupe_1C.prefab"),
            new("Prefabs_Mobile/Coupe_2A.prefab"),
            new("Prefabs_Mobile/Firetruck_1A.prefab"),
            new("Prefabs_Mobile/Interceptor_1A.prefab"),
            new("Prefabs_Mobile/Interceptor_2A.prefab"),
            new("Prefabs_Mobile/PickUp_1A.prefab"),
            new("Prefabs_Mobile/Sedan_1A.prefab"),
            new("Prefabs_Mobile/Sedan_2A.prefab"),
            new("Prefabs_Mobile/Sedan_3A.prefab"),
            new("Prefabs_Mobile/Sedan_4A.prefab"),
            new("Prefabs_Mobile/Sedan_4B.prefab"),
            new("Prefabs_Mobile/Supersport_1A.prefab"),
            new("Prefabs_Mobile/Taxi_1A.prefab"),
            new("Prefabs_Mobile/Taxi_1B.prefab"),
            new("Prefabs_Mobile/Van_1A.prefab"),

        };

        protected override void MoveGraphics(Transform instance, Transform graphics)
        {
            base.MoveGraphics(instance, graphics);
            MoveChildToParent(instance, "Door", graphics);
            MoveChildToParent(instance, "Drawer", graphics);
            MoveChildToParent(instance, "Crane_Arm1", graphics);
            MoveChildToParent(instance, "TailGate", graphics);
            MoveChildToParent(instance, "Telescopes", graphics);
            MoveChildToParent(instance, "Van_1A", graphics);
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            if (vehicleComponent.name.Contains("Truck") ||
                vehicleComponent.name.Contains("Bus"))
            {
                vehicleComponent.steeringTime = 0.7f;
                vehicleComponent.forwardLeaningFactor = 0.2f;
                vehicleComponent.sideLeaningFactor = 0.2f;
                vehicleComponent.triggerLength = 6f;
            }
        }
    }
}
