using UnityEngine;

namespace HardwareStore.Gameplay.Common.Traffic
{
    public readonly struct LocalTrafficPrediction
    {
        public LocalTrafficPrediction(float firstConflictTime,
            float moverArrivalTime, float obstacleArrivalTime)
        {
            FirstConflictTime = firstConflictTime;
            MoverArrivalTime = moverArrivalTime;
            ObstacleArrivalTime = obstacleArrivalTime;
        }

        public float FirstConflictTime { get; }
        public float MoverArrivalTime { get; }
        public float ObstacleArrivalTime { get; }
    }

    public readonly struct LocalTrafficWorldPrediction
    {
        public LocalTrafficWorldPrediction(float firstConflictTime,
            Collider collider)
        {
            FirstConflictTime = firstConflictTime;
            Collider = collider;
        }

        public float FirstConflictTime { get; }
        public Collider Collider { get; }
    }

    public interface ILocalTrafficPredictionService
    {
        bool TryPredictConflict(GameEntity mover, GameEntity moverMotionOwner,
            Vector3 moverVelocity, float moverIntentDistance,
            GameEntity obstacle, GameEntity obstacleMotionOwner,
            Vector3 obstacleVelocity, float obstacleIntentDistance,
            bool useReleaseClearance, out LocalTrafficPrediction prediction);

        bool TryPredictWorldConflict(GameEntity mover,
            GameEntity moverMotionOwner, Vector3 moverVelocity,
            float moverIntentDistance, Transform additionalIgnoredRoot,
            bool useReleaseClearance,
            out LocalTrafficWorldPrediction prediction);
    }
}
