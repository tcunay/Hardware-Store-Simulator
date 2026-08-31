using UnityEngine;

namespace Gley.TrafficSystem
{
    public struct Obstacle
    {
        private readonly Collider _collider;
        private readonly bool _isConvex;
        private readonly ObstacleTypes _obstacleType;
        private readonly ITrafficParticipant _vehicleScript;
        private readonly string _name;

        public readonly Collider Collider => _collider;
        public readonly bool IsConvex => _isConvex;
        public readonly ObstacleTypes ObstacleType => _obstacleType;
        public readonly ITrafficParticipant VehicleScript => _vehicleScript;
        public readonly string Name => _name;

        public Obstacle(Collider collider, bool isConvex, ObstacleTypes obstacleTypes, ITrafficParticipant vehicleScript)
        {
            _collider = collider;
            _isConvex = isConvex;
            _obstacleType = obstacleTypes;
            _vehicleScript = vehicleScript;
            _name = _collider.name;
        }
    }
}