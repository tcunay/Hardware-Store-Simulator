using System;
using System.Linq;
using UnityEngine;

namespace HardwareStore.Editor
{
    /// <summary>
    /// Converts visual-origin route authoring into the rear-axle root poses used by
    /// the Gley vehicle provider and into Gley's front-axle waypoint convention.
    /// </summary>
    internal static class CustomerVehicleProviderPoseUtility
    {
        private const string RearLeftWheelName = "Rear Left Wheel";
        private const string RearRightWheelName = "Rear Right Wheel";
        private const string FrontLeftWheelName = "Front Left Wheel";
        private const string FrontRightWheelName = "Front Right Wheel";

        public static Pose[] BuildProviderRoute(
            GameObject sourceVehiclePrefab,
            params Vector3[] visualWaypointPositions)
        {
            if (visualWaypointPositions == null || visualWaypointPositions.Length < 2)
            {
                throw new ArgumentException(
                    "A customer-vehicle route requires at least two visual waypoints.",
                    nameof(visualWaypointPositions));
            }

            Vector3 rearAxleLocalXZ = ResolveRearAxleLocalXZ(sourceVehiclePrefab);
            var poses = new Pose[visualWaypointPositions.Length];
            for (int index = 0; index < visualWaypointPositions.Length; index++)
            {
                Vector3 direction = index < visualWaypointPositions.Length - 1
                    ? visualWaypointPositions[index + 1] -
                      visualWaypointPositions[index]
                    : visualWaypointPositions[index] -
                      visualWaypointPositions[index - 1];
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.000001f)
                {
                    throw new ArgumentException(
                        $"A customer-vehicle route contains duplicate XZ waypoint " +
                        $"{index}.",
                        nameof(visualWaypointPositions));
                }

                Quaternion rotation =
                    Quaternion.LookRotation(direction.normalized, Vector3.up);
                poses[index] = ToProviderPose(
                    new Pose(visualWaypointPositions[index], rotation),
                    rearAxleLocalXZ);
            }

            return poses;
        }

        public static Pose ToProviderPose(
            GameObject sourceVehiclePrefab,
            Pose visualPose) =>
            ToProviderPose(visualPose, ResolveRearAxleLocalXZ(sourceVehiclePrefab));

        public static Pose ToVisualPose(
            Pose providerPose,
            Vector3 rearAxleLocalXZ) =>
            new(
                providerPose.position - providerPose.rotation * rearAxleLocalXZ,
                providerPose.rotation);

        public static Vector3 ResolveRearAxleLocalXZ(GameObject sourceVehiclePrefab)
        {
            if (sourceVehiclePrefab == null)
                throw new ArgumentNullException(nameof(sourceVehiclePrefab));

            return ResolveAxleLocalXZ(
                sourceVehiclePrefab,
                RearLeftWheelName,
                RearRightWheelName,
                "rear");
        }

        public static Vector3 ResolveFrontAxleFromProviderRootLocalXZ(
            GameObject sourceVehiclePrefab)
        {
            if (sourceVehiclePrefab == null)
                throw new ArgumentNullException(nameof(sourceVehiclePrefab));

            Vector3 rearAxle = ResolveRearAxleLocalXZ(sourceVehiclePrefab);
            Vector3 frontAxle = ResolveAxleLocalXZ(
                sourceVehiclePrefab,
                FrontLeftWheelName,
                FrontRightWheelName,
                "front");
            Vector3 offset = frontAxle - rearAxle;
            offset.y = 0f;
            if (offset.z <= 0.01f || Mathf.Abs(offset.x) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle front axle {frontAxle} must be centered and " +
                    $"in front of rear axle {rearAxle}.");
            }

            return offset;
        }

        public static Pose ToGleyWaypointPose(
            Pose providerRootPose,
            Vector3 frontAxleFromProviderRootLocalXZ) =>
            new(
                providerRootPose.position +
                providerRootPose.rotation * frontAxleFromProviderRootLocalXZ,
                providerRootPose.rotation);

        public static Transform RequireUniqueDescendant(
            GameObject root,
            string childName)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(childName))
                throw new ArgumentException("A child name is required.", nameof(childName));

            Transform[] matches = root.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == childName)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{root.name}' must contain exactly one descendant named " +
                    $"'{childName}', found {matches.Length}.");
            }

            return matches[0];
        }

        private static Pose ToProviderPose(Pose visualPose, Vector3 rearAxleLocalXZ) =>
            new(
                visualPose.position + visualPose.rotation * rearAxleLocalXZ,
                visualPose.rotation);

        private static Vector3 ResolveAxleLocalXZ(
            GameObject sourceVehiclePrefab,
            string leftWheelName,
            string rightWheelName,
            string axleRole)
        {
            Transform left = RequireUniqueDescendant(
                sourceVehiclePrefab,
                leftWheelName);
            Transform right = RequireUniqueDescendant(
                sourceVehiclePrefab,
                rightWheelName);
            Vector3 leftLocal = sourceVehiclePrefab.transform
                .InverseTransformPoint(left.position);
            Vector3 rightLocal = sourceVehiclePrefab.transform
                .InverseTransformPoint(right.position);
            ValidateFinite(leftLocal, leftWheelName);
            ValidateFinite(rightLocal, rightWheelName);
            if (Vector2.Distance(
                    new Vector2(leftLocal.x, leftLocal.z),
                    new Vector2(rightLocal.x, rightLocal.z)) < 0.01f)
            {
                throw new InvalidOperationException(
                    $"Customer vehicle {axleRole} wheels '{leftWheelName}' and " +
                    $"'{rightWheelName}' do not define an axle.");
            }

            Vector3 axle = (leftLocal + rightLocal) * 0.5f;
            return new Vector3(axle.x, 0f, axle.z);
        }

        private static void ValidateFinite(Vector3 value, string role)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                throw new InvalidOperationException(
                    $"Customer vehicle '{role}' has a non-finite local position.");
            }
        }
    }
}
