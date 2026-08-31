using Gley.UrbanSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class MobileTrafficTruckConverter : GenericConverter
    {
        protected override string ExpectedFolderName => "MobileTrafficTruck";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                new ColliderProperties[]{new( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body") , new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin")}),

            new("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                new ColliderProperties[]{new  ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),

            new("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
        };

        protected override List<TruckData> TrucksToConvert => new()
        {
            new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new  (new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin")}),
                new VehicleData("Graphics/Prefabs/Tanker/Decal/Tanker_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new  (new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin")}),
                new VehicleData("Graphics/Prefabs/Trailer/Decal/Trailer_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Decal/Tanker_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Decal/Trailer_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Decal/Tanker_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Decal/Trailer_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body") , new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin")}),
                new VehicleData("Graphics/Prefabs/Tanker/Dirty/Tanker_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body") , new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin")}),
                new VehicleData("Graphics/Prefabs/Trailer/Dirty/Trailer_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Dirty/Tanker_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Dirty/Trailer_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Dirty/Tanker_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Dirty/Trailer_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                 new VehicleData("Graphics/Prefabs/Tanker/Paint/Tanker_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                 new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Decal/Truck_Head_Decal.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                 new VehicleData("Graphics/Prefabs/Trailer/Paint/Trailer_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                 new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Paint/Tanker_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

             new(new VehicleData("Graphics/Prefabs/Truck_Head/Dirty/Truck_Head_Dirty.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Paint/Trailer_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Tanker/Paint/Tanker_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),

            new(new VehicleData("Graphics/Prefabs/Truck_Head/Paint/Truck_Head_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,0.73f,0), new Vector3(2.6f,0.75f,5.9f),"Body"), new ( new Vector3(0,2.4f,1.65f), new Vector3(2.4f,2.5f,2.5f),"Cabin") }),
                new VehicleData("Graphics/Prefabs/Trailer/Paint/Trailer_Paint.prefab",
                    new ColliderProperties[]{new ( new Vector3(0,2.52f,0), new Vector3(2.45f,2.7f,13f),"Body") }),
                new Vector3(0,1,-1.45f)),
        };

        protected override Transform GetTractorAnchorPoint(Transform vehicleHolder, TruckData truckData)
        {
            var anchor = new GameObject("TractorAnchorPoint").transform;
            anchor.SetParent(vehicleHolder);
            anchor.localPosition = truckData.TractorAnchorPoint;
            return anchor;
        }

        protected override void SetCustomRigidbodyValues(Rigidbody rb)
        {
            rb.mass = 1500;
        }

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
           vehicleComponent.forwardLeaningFactor = 0.1f;
        }

        protected override void SetLightsComponent(GameObject instance)
        {
            if(instance.name.Contains("Trailer"))
            {
                AddComponent(instance, typeof(VehicleLightsComponentV2));
            }
            else
            {
                AddComponent(instance, typeof(VehicleLightsComponent));
            }
        }

        protected override void CreateWheelStructure(Transform wheelRoot)
        {
            base.CreateWheelStructure(wheelRoot);
            wheelRoot.localEulerAngles = Vector3.zero;
            var yPoz = wheelRoot.transform.localPosition.y;
            wheelRoot.transform.localPosition = new Vector3(wheelRoot.transform.localPosition.x, 0, wheelRoot.transform.localPosition.z);
            wheelRoot.GetChild(0).transform.localPosition = new Vector3(0, yPoz, 0);
            if (wheelRoot.name.Contains("Right"))
            {
                wheelRoot.transform.GetChild(0).GetChild(0).localEulerAngles = new Vector3(0, 180, 0);
            }
        }
    }
}
