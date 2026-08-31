using Gley.UrbanSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class GridlockConverter : GenericConverter
    {
        protected override string ExpectedFolderName => "GRIDLOCK -  City Traffic Vehicle Pack";

        protected override List<VehicleData> VehiclesToConvert => new()
        {
            new("Graphics/Prefabs/Hatchback/Clean/Hatchback_Clean.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.97f,0), new Vector3(1.8f,1.15f,4.25f),"Body")}),

            new("Graphics/Prefabs/Hatchback/Dirty/Hatchback_Dirty.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.97f,0), new Vector3(1.8f,1.15f,4.25f),"Body") }),

            new("Graphics/Prefabs/Sedan_Large/Clean/Sedan_Large_Clean.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.9f,0), new Vector3(1.9f,1.1f,4.95f),"Body") }),

            new("Graphics/Prefabs/Sedan_Large/Dirty/Sedan_Large_Dirty.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.9f,0), new Vector3(1.9f,1.1f,4.95f),"Body") }),

            new("Graphics/Prefabs/Sedan_Small/Clean/Sedan_Small_Clean.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.75f,0), new Vector3(1.75f,0.9f,4.35f),"Body") }),

            new("Graphics/Prefabs/Sedan_Small/Dirty/Sedan_Small_Dirty.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.75f,0), new Vector3(1.75f,0.9f,4.35f),"Body") }),

            new("Graphics/Prefabs/Sportscar/Clean/Sportscar_Clean.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.65f,0), new Vector3(1.86f,0.83f,4.7f),"Body") }),

            new("Graphics/Prefabs/Sportscar/Dirty/Sportscar_Dirty.prefab",
                new ColliderProperties[]{new ( new Vector3(0,0.65f,0), new Vector3(1.86f,0.83f,4.7f),"Body") }),

            new("Graphics/Prefabs/SUV_Large/Clean/SUV_Large_Clean.prefab",
                new ColliderProperties[] { new(new Vector3(0, 0.85f, 0), new Vector3(2, 0.8f, 4.7f), "Body") }),

            new("Graphics/Prefabs/SUV_Large/Dirty/SUV_Large_Dirty.prefab",
                new ColliderProperties[] { new(new Vector3(0, 0.85f, 0), new Vector3(2, 0.8f, 4.7f), "Body") }),

            new("Graphics/Prefabs/Truck_Medium/Clean/Truck_Medium_Clean.prefab",
                new ColliderProperties[] { new(new Vector3(0, 2.2f, 0), new Vector3(2.5f, 3.25f, 9f), "Body") }),

            new("Graphics/Prefabs/Truck_Medium/Dirty/Truck_Medium_Truck_Dirty.prefab",
                new ColliderProperties[] { new(new Vector3(0, 2.2f, 0), new Vector3(2.5f, 3.25f, 9f), "Body") }),

            new("Graphics/Prefabs/Van_Commercial/Clean/Van_Commercial_Clean.prefab",
                new ColliderProperties[] { new(new Vector3(0, 1.4f, 0), new Vector3(2, 2, 6.1f), "Body") }),

            new("Graphics/Prefabs/Van_Commercial/Dirty/Van_Commercial_Van_Dirty.prefab",
                new ColliderProperties[] { new(new Vector3(0, 1.4f, 0), new Vector3(2, 2, 6.1f), "Body") }),
        };

        protected override void SetCustomComponentValues(VehicleComponent vehicleComponent)
        {
            base.SetCustomComponentValues(vehicleComponent);
            if (vehicleComponent.name.Contains("Truck"))
            {
                vehicleComponent.forwardLeaningFactor = 0.1f;
            }
        }

        protected override void SetLightsComponent(GameObject instance)
        {
            AddComponent(instance, typeof(VehicleLightsComponent));
        }

        protected override void CreateWheelStructure(Transform wheelRoot)
        {
            base.CreateWheelStructure(wheelRoot);
            wheelRoot.localEulerAngles = Vector3.zero;
            var yPoz = wheelRoot.transform.localPosition.y;
            wheelRoot.transform.localPosition = new Vector3(wheelRoot.transform.localPosition.x, 0, wheelRoot.transform.localPosition.z);
            wheelRoot.GetChild(0).transform.localPosition = new Vector3(0, yPoz, 0);
            if(wheelRoot.name.Contains("Right"))
            {
                wheelRoot.transform.GetChild(0).GetChild(0).localEulerAngles = new Vector3(0, 180, 0);
            }
        }
    }
}
