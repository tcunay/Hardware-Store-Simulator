using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class WorkerTrolleyHitchService : IWorkerTrolleyHitchService
    {
        private readonly HashSet<Rigidbody> _tracked = new();
        private readonly HashSet<Rigidbody> _maintained = new();
        private readonly List<Rigidbody> _detachBuffer = new();

        public void BeginFrame() => _maintained.Clear();

        public void Maintain(Rigidbody trolleyBody,
            Collider[] trolleyColliders, Rigidbody workerBody,
            Collider[] workerColliders, float followDistance)
        {
            ValidateBody(trolleyBody, trolleyColliders, "trolley");
            ValidateBody(workerBody, workerColliders, "worker");
            if (!workerBody.isKinematic || workerBody.useGravity)
            {
                throw new InvalidOperationException(
                    "A trolley pusher requires a kinematic Rigidbody without gravity.");
            }
            if (!IsFinite(followDistance) || followDistance <= 0f)
            {
                throw new InvalidOperationException(
                    "Trolley follow distance must be finite and positive.");
            }

            GhostMoverCollisionProfile.Apply(
                workerBody,
                workerColliders,
                GhostMoverCollisionProfile.GhostMover);
            GhostMoverCollisionProfile.Apply(
                trolleyBody,
                trolleyColliders,
                GhostMoverCollisionProfile.GhostMover);

            ConfigureKinematic(trolleyBody);
            Vector3 targetPosition = workerBody.position +
                                     workerBody.rotation * Vector3.forward *
                                     followDistance;
            Quaternion targetRotation = workerBody.rotation;
            if (!IsFinite(targetPosition) || !IsFinite(targetRotation))
            {
                throw new InvalidOperationException(
                    "Worker trolley deterministic follow produced an invalid pose.");
            }

            trolleyBody.position = targetPosition;
            trolleyBody.rotation = targetRotation;
            trolleyBody.transform.SetPositionAndRotation(
                targetPosition, targetRotation);
            _tracked.Add(trolleyBody);
            _maintained.Add(trolleyBody);
        }

        public void EndFrame()
        {
            _detachBuffer.Clear();
            foreach (Rigidbody trolleyBody in _tracked)
            {
                if (trolleyBody == null || !_maintained.Contains(trolleyBody))
                    _detachBuffer.Add(trolleyBody);
            }

            foreach (Rigidbody trolleyBody in _detachBuffer)
                Detach(trolleyBody);
        }

        public void Detach(Rigidbody trolleyBody)
        {
            if (ReferenceEquals(trolleyBody, null))
                return;

            ConfigureKinematic(trolleyBody);
            _tracked.Remove(trolleyBody);
            _maintained.Remove(trolleyBody);
        }

        public void DetachAll()
        {
            _detachBuffer.Clear();
            foreach (Rigidbody trolleyBody in _tracked)
                _detachBuffer.Add(trolleyBody);
            foreach (Rigidbody trolleyBody in _detachBuffer)
                Detach(trolleyBody);
            _detachBuffer.Clear();
        }

        private static void ConfigureKinematic(Rigidbody body)
        {
            if (body == null)
                return;

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            body.isKinematic = true;
        }

        private static void ValidateBody(Rigidbody body,
            Collider[] colliders, string role)
        {
            if (body == null)
                throw new ArgumentNullException($"{role}Body");
            if (colliders == null || colliders.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Worker trolley {role} requires registered colliders.");
            }
            if (!body.gameObject.activeInHierarchy || !body.detectCollisions)
            {
                throw new InvalidOperationException(
                    $"Worker trolley {role} requires an active queryable Rigidbody.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) &&
            IsFinite(value.z) && IsFinite(value.w);
    }
}
