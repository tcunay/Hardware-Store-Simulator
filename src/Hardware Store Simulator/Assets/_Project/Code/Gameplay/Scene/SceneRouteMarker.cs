using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    [DisallowMultipleComponent]
    public sealed class SceneRouteMarker : MonoBehaviour
    {
        [SerializeField] private SceneRouteId _id;
        [SerializeField] private Transform[] _waypoints;

        public SceneRouteId Id => _id;
        public Pose[] Poses => CreateSnapshot();

        public void Configure(SceneRouteId id, Transform[] waypoints)
        {
            Validate(waypoints);
            _id = id;
            _waypoints = (Transform[])waypoints.Clone();
        }

        private Pose[] CreateSnapshot()
        {
            Validate(_waypoints);

            var poses = new Pose[_waypoints.Length];
            for (int index = 0; index < _waypoints.Length; index++)
            {
                Transform waypoint = _waypoints[index];
                poses[index] = new Pose(waypoint.position, waypoint.rotation);
            }

            return poses;
        }

        private static void Validate(Transform[] waypoints)
        {
            if (waypoints == null)
                throw new ArgumentNullException(nameof(waypoints));
            if (waypoints.Length == 0)
                throw new ArgumentException("A scene route must contain at least one waypoint.", nameof(waypoints));

            var uniqueWaypoints = new HashSet<Transform>();
            for (int index = 0; index < waypoints.Length; index++)
            {
                Transform waypoint = waypoints[index];
                if (waypoint == null)
                    throw new ArgumentException($"Route waypoint {index} is missing.", nameof(waypoints));
                if (!uniqueWaypoints.Add(waypoint))
                    throw new ArgumentException(
                        $"Route waypoint {index} is registered more than once.", nameof(waypoints));
            }
        }
    }
}
