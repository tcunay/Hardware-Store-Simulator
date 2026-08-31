using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public struct TruckData
    {
        public VehicleData TractorData;
        public VehicleData TrailerData;
        public Vector3 TractorAnchorPoint;

        public TruckData(VehicleData tractorData, VehicleData trailerData, Vector3 tractorAnchorPoint = default(Vector3))
        {
            TractorData = tractorData;
            TrailerData = trailerData;
            TractorAnchorPoint = tractorAnchorPoint;
        }
    }
}
