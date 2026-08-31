using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class CompleteVehiclePackV2Converter : SicsConverter
    {
        protected override string ExpectedFolderName => "Complete Vehicle Pack V2";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Prefabs/Bus_2A.prefab"),
            new("Prefabs/Bus_2B.prefab"),
            new("Prefabs/Bus_2C.prefab"),
            new("Prefabs/Compact_3A.prefab"),
            new("Prefabs/Compact_3B.prefab"),
            new("Prefabs/Compact_3C.prefab"),
            new("Prefabs/Compact_4A.prefab"),
            new("Prefabs/Compact_4B.prefab"),
            new("Prefabs/Compact_4C.prefab"),
            new("Prefabs/Hybrid_1A.prefab"),
            new("Prefabs/Hybrid_1B.prefab"),
            new("Prefabs/PickUp_2A.prefab"),
            new("Prefabs/PickUp_3A.prefab"),
            new("Prefabs/PickUp_3A_naked.prefab"),
            new("Prefabs/PickUp_3B.prefab"),
            new("Prefabs/PickUp_3B_naked.prefab"),
            new("Prefabs/PickUp_3C.prefab"),
            new("Prefabs/PickUp_3C_naked.prefab"),
            new("Prefabs/Sedan_5A.prefab"),
            new("Prefabs/Sedan_5B.prefab"),
            new("Prefabs/Sedan_5B_naked.prefab"),
            new("Prefabs/Sedan_6A.prefab"),
            new("Prefabs/Sedan_6B.prefab"),
            new("Prefabs/Sedan_6B_naked.prefab"),
            new("Prefabs/SUV_1A.prefab"),
            new("Prefabs/SUV_1B.prefab"),
            new("Prefabs/Taxi_2A.prefab"),
            new("Prefabs/Taxi_2B.prefab"),
            new("Prefabs/Truck_1A.prefab"),
            new("Prefabs/Truck_1B.prefab"),
            new("Prefabs/Van_2A.prefab"),
            new("Prefabs/Van_2B.prefab"),
        };

        protected override List<TruckData> TrucksToConvert => new()
        {
             new(new VehicleData("Prefabs/Truck_1A.prefab"),
                new VehicleData("Prefabs/Cargo_Box.prefab")),
             new(new VehicleData("Prefabs/Truck_1B.prefab"),
                new VehicleData("Prefabs/Cargo_Box.prefab")),
              new(new VehicleData("Prefabs/Truck_1A.prefab"),
                new VehicleData("Prefabs/Cistern.prefab")),
             new(new VehicleData("Prefabs/Truck_1B.prefab"),
                new VehicleData("Prefabs/Cistern.prefab")),
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

        protected override void CustomVehicleSetup(GameObject instance)
        {
            if (instance.name.Contains("+"))
            {
                var truckConnectionPoint = new GameObject("Anchor_Point_Traktor");
                truckConnectionPoint.transform.SetParent(instance.transform);
            }
            base.CustomVehicleSetup(instance);
            MakeAllMeshCollidersConvex(instance);
        }

        private void MakeAllMeshCollidersConvex(GameObject instance)
        {
            var meshColliders = instance.GetComponentsInChildren<MeshCollider>();
            foreach (var collider in meshColliders)
            {
                collider.convex = true;
            }
        }

        protected override void MoveGraphics(Transform instance, Transform graphics)
        {
            base.MoveGraphics(instance, graphics);
            MoveChildToParent(instance, "Body_Kit_1A", graphics);
            MoveChildToParent(instance, "Spoiler_1A", graphics);
            MoveChildToParent(instance, "Body_Kit_1B", graphics);
            MoveChildToParent(instance, "Roof_Scoop_1A", graphics);
            MoveChildToParent(instance, "Spoiler_1B", graphics);
            MoveChildToParent(instance, "Body_Kit_2A", graphics);
            MoveChildToParent(instance, "Spoiler_2A", graphics);
            MoveChildToParent(instance, "Body_Kit_2B", graphics);
            MoveChildToParent(instance, "Roof_Scoop_2A", graphics);
            MoveChildToParent(instance, "Spoiler_2B", graphics);
            MoveChildToParent(instance, "TailGate", graphics);
            MoveChildToParent(instance, "Cover", graphics);
            MoveChildToParent(instance, "Lights", graphics);
            MoveChildToParent(instance, "Trunk", graphics);
            MoveChildToParent(instance, "Cabin", graphics);
            MoveChildToParent(instance, "Antenna", graphics);
            MoveChildToParent(instance, "Satellite_Dish", graphics);
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            if (vehicleComponent.name.Contains("Truck")||
                vehicleComponent.name.Contains("Bus"))
            {
                vehicleComponent.steeringTime = 0.7f;
                vehicleComponent.forwardLeaningFactor = 0.2f;
                vehicleComponent.sideLeaningFactor = 0.2f;
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
