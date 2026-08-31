using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Physics
{
    public sealed class WorkerTrolleyHitchService : IWorkerTrolleyHitchService
    {
        private static readonly Vector3 TrolleyHandleAnchor =
            new(0f, 1.76f, -1.12f);

        private const float YawLimit = 8f;
        private const float AngularSpring = 240f;
        private const float AngularDamper = 70f;
        private const float AngularMaximumForce = 1200f;
        private const float ProjectionDistance = 0.08f;
        private const float ProjectionAngle = 10f;
        private const float MaximumAnchorError = 0.25f;
        private const float AttachedLinearDamping = 1.25f;
        private const float AttachedAngularDamping = 3.5f;
        private const int AttachedSolverIterations = 12;
        private const int AttachedSolverVelocityIterations = 4;
        private const float RollingStaticFriction = 0.02f;
        private const float RollingDynamicFriction = 0.02f;

        private readonly Dictionary<Rigidbody, Hitch> _hitches = new();
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
                throw new InvalidOperationException("Trolley follow distance must be positive.");

            _maintained.Add(trolleyBody);
            if (!_hitches.TryGetValue(trolleyBody, out Hitch hitch))
            {
                hitch = Attach(trolleyBody, trolleyColliders, workerBody,
                    workerColliders, followDistance);
                _hitches.Add(trolleyBody, hitch);
            }
            else if (hitch.WorkerBody != workerBody)
            {
                throw new InvalidOperationException(
                    $"Trolley '{trolleyBody.name}' is already hitched to another worker.");
            }

            if (hitch.Joint == null || hitch.Joint.connectedBody != workerBody)
                throw new InvalidOperationException("Worker trolley hitch was destroyed externally.");
            if (trolleyBody.isKinematic || !trolleyBody.useGravity)
            {
                throw new InvalidOperationException(
                    "A hitched trolley requires a dynamic Rigidbody with gravity.");
            }

            // PhysX preserves the sleeping state when a parked kinematic body becomes
            // dynamic. A constraint and gravity do not wake it reliably, so the worker
            // could otherwise move while the newly attached trolley stays parked.
            trolleyBody.WakeUp();

            float error = Vector3.Distance(
                TransformPoint(trolleyBody, hitch.Joint.anchor),
                TransformPoint(workerBody, hitch.Joint.connectedAnchor));
            if (error > MaximumAnchorError)
            {
                throw new InvalidOperationException(
                    $"Worker trolley hitch exceeded its safe error: {error:F3} m. " +
                    $"Worker={workerBody.position}, workerRotation=" +
                    $"{workerBody.rotation.eulerAngles}, trolley={trolleyBody.position}, " +
                    $"trolleyRotation={trolleyBody.rotation.eulerAngles}, " +
                    $"trolleyVelocity={trolleyBody.linearVelocity}.");
            }
        }

        public void EndFrame()
        {
            _detachBuffer.Clear();
            foreach (KeyValuePair<Rigidbody, Hitch> pair in _hitches)
            {
                if (pair.Key == null || !_maintained.Contains(pair.Key))
                    _detachBuffer.Add(pair.Key);
            }

            foreach (Rigidbody trolleyBody in _detachBuffer)
                Detach(trolleyBody);
        }

        public void Detach(Rigidbody trolleyBody)
        {
            if (ReferenceEquals(trolleyBody, null) ||
                !_hitches.TryGetValue(trolleyBody, out Hitch hitch))
                return;

            RestoreIgnoredCollisions(hitch);
            RestoreColliderMaterials(hitch);
            if (hitch.Joint != null)
            {
                hitch.Joint.connectedBody = null;
                hitch.Joint.xMotion = ConfigurableJointMotion.Free;
                hitch.Joint.yMotion = ConfigurableJointMotion.Free;
                hitch.Joint.zMotion = ConfigurableJointMotion.Free;
                hitch.Joint.angularXMotion = ConfigurableJointMotion.Free;
                hitch.Joint.angularYMotion = ConfigurableJointMotion.Free;
                hitch.Joint.angularZMotion = ConfigurableJointMotion.Free;
            }

            if (trolleyBody != null)
            {
                trolleyBody.linearVelocity = Vector3.zero;
                trolleyBody.angularVelocity = Vector3.zero;
                trolleyBody.useGravity = false;
                trolleyBody.isKinematic = true;
                trolleyBody.interpolation = hitch.Interpolation;
                trolleyBody.collisionDetectionMode = hitch.CollisionDetection;
                trolleyBody.constraints = hitch.Constraints;
                trolleyBody.linearDamping = hitch.LinearDamping;
                trolleyBody.angularDamping = hitch.AngularDamping;
                trolleyBody.solverIterations = hitch.SolverIterations;
                trolleyBody.solverVelocityIterations = hitch.SolverVelocityIterations;
            }

            _hitches.Remove(trolleyBody);
            _maintained.Remove(trolleyBody);
            if (hitch.RollingMaterial != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(hitch.RollingMaterial);
                else
                    UnityEngine.Object.DestroyImmediate(hitch.RollingMaterial);
            }
        }

        public void DetachAll()
        {
            _detachBuffer.Clear();
            foreach (Rigidbody trolleyBody in _hitches.Keys)
                _detachBuffer.Add(trolleyBody);
            foreach (Rigidbody trolleyBody in _detachBuffer)
                Detach(trolleyBody);
            _detachBuffer.Clear();
        }

        private static Hitch Attach(Rigidbody trolleyBody,
            Collider[] trolleyColliders, Rigidbody workerBody,
            Collider[] workerColliders, float followDistance)
        {
            if (!trolleyBody.isKinematic || trolleyBody.useGravity)
            {
                throw new InvalidOperationException(
                    "A trolley must be parked as a kinematic Rigidbody before hitching.");
            }

            Hitch hitch = new(trolleyBody, workerBody, trolleyColliders,
                workerColliders);
            IgnorePairedCollisions(hitch, ignore: true);
            ApplyRollingMaterial(hitch);

            trolleyBody.linearDamping = AttachedLinearDamping;
            trolleyBody.angularDamping = AttachedAngularDamping;
            trolleyBody.solverIterations = AttachedSolverIterations;
            trolleyBody.solverVelocityIterations =
                AttachedSolverVelocityIterations;
            trolleyBody.constraints = RigidbodyConstraints.FreezeRotationX |
                                       RigidbodyConstraints.FreezeRotationZ;
            trolleyBody.interpolation = RigidbodyInterpolation.Interpolate;
            trolleyBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            trolleyBody.isKinematic = false;
            trolleyBody.useGravity = true;
            trolleyBody.linearVelocity = Vector3.zero;
            trolleyBody.angularVelocity = Vector3.zero;

            ConfigurableJoint joint = GetOrCreateDormantJoint(trolleyBody);
            joint.connectedBody = workerBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = TrolleyHandleAnchor;
            joint.connectedAnchor = new Vector3(
                0f, TrolleyHandleAnchor.y,
                followDistance + TrolleyHandleAnchor.z);
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
            SoftJointLimit lowYaw = joint.angularYLimit;
            lowYaw.limit = YawLimit;
            joint.angularYLimit = lowYaw;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            JointDrive angularDrive = joint.slerpDrive;
            angularDrive.positionSpring = AngularSpring;
            angularDrive.positionDamper = AngularDamper;
            angularDrive.maximumForce = AngularMaximumForce;
            joint.slerpDrive = angularDrive;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = ProjectionDistance;
            joint.projectionAngle = ProjectionAngle;
            hitch.Joint = joint;
            return hitch;
        }

        private static ConfigurableJoint GetOrCreateDormantJoint(
            Rigidbody trolleyBody)
        {
            ConfigurableJoint[] joints =
                trolleyBody.GetComponents<ConfigurableJoint>();
            if (joints.Length == 0)
                return trolleyBody.gameObject.AddComponent<ConfigurableJoint>();
            if (joints.Length != 1 || !IsDormant(joints[0]))
            {
                throw new InvalidOperationException(
                    $"Trolley '{trolleyBody.name}' has an external or duplicate joint.");
            }

            return joints[0];
        }

        private static bool IsDormant(ConfigurableJoint joint) =>
            joint != null &&
            joint.connectedBody == null &&
            joint.xMotion == ConfigurableJointMotion.Free &&
            joint.yMotion == ConfigurableJointMotion.Free &&
            joint.zMotion == ConfigurableJointMotion.Free &&
            joint.angularXMotion == ConfigurableJointMotion.Free &&
            joint.angularYMotion == ConfigurableJointMotion.Free &&
            joint.angularZMotion == ConfigurableJointMotion.Free;

        private static void ValidateBody(Rigidbody body, Collider[] colliders,
            string role)
        {
            if (body == null)
                throw new ArgumentNullException($"{role}Body");
            if (colliders == null || colliders.Length == 0)
                throw new InvalidOperationException(
                    $"Worker trolley {role} requires registered colliders.");
            if (!body.gameObject.activeInHierarchy || !body.detectCollisions)
                throw new InvalidOperationException(
                    $"Worker trolley {role} requires an active collision-enabled Rigidbody.");
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    throw new InvalidOperationException(
                        $"Worker trolley {role} has a missing collider.");
                if (collider.attachedRigidbody != body)
                    throw new InvalidOperationException(
                        $"Collider '{collider.name}' is not attached to the {role} Rigidbody.");
            }
        }

        private static void IgnorePairedCollisions(Hitch hitch, bool ignore)
        {
            foreach (Collider trolleyCollider in hitch.TrolleyColliders)
            foreach (Collider workerCollider in hitch.WorkerColliders)
            {
                if (!ignore && (trolleyCollider == null || workerCollider == null))
                    continue;
                UnityEngine.Physics.IgnoreCollision(
                    trolleyCollider, workerCollider, ignore);
            }
        }

        private static void RestoreIgnoredCollisions(Hitch hitch) =>
            IgnorePairedCollisions(hitch, ignore: false);

        private static void ApplyRollingMaterial(Hitch hitch)
        {
            hitch.RollingMaterial = new PhysicsMaterial(
                "Worker Trolley Rolling Contact")
            {
                staticFriction = RollingStaticFriction,
                dynamicFriction = RollingDynamicFriction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            foreach (Collider collider in hitch.TrolleyColliders)
                collider.sharedMaterial = hitch.RollingMaterial;
        }

        private static void RestoreColliderMaterials(Hitch hitch)
        {
            for (int index = 0; index < hitch.TrolleyColliders.Length; index++)
            {
                Collider collider = hitch.TrolleyColliders[index];
                if (collider != null)
                    collider.sharedMaterial = hitch.TrolleyMaterials[index];
            }
        }

        private static Vector3 TransformPoint(Rigidbody body,
            Vector3 localPoint) =>
            body.position + body.rotation * localPoint;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class Hitch
        {
            public readonly Rigidbody WorkerBody;
            public readonly Collider[] TrolleyColliders;
            public readonly Collider[] WorkerColliders;
            public readonly PhysicsMaterial[] TrolleyMaterials;
            public readonly RigidbodyInterpolation Interpolation;
            public readonly CollisionDetectionMode CollisionDetection;
            public readonly RigidbodyConstraints Constraints;
            public readonly float LinearDamping;
            public readonly float AngularDamping;
            public readonly int SolverIterations;
            public readonly int SolverVelocityIterations;
            public ConfigurableJoint Joint;
            public PhysicsMaterial RollingMaterial;

            public Hitch(Rigidbody trolleyBody, Rigidbody workerBody,
                Collider[] trolleyColliders, Collider[] workerColliders)
            {
                WorkerBody = workerBody;
                TrolleyColliders = trolleyColliders;
                WorkerColliders = workerColliders;
                TrolleyMaterials = new PhysicsMaterial[trolleyColliders.Length];
                for (int index = 0; index < trolleyColliders.Length; index++)
                    TrolleyMaterials[index] = trolleyColliders[index].sharedMaterial;
                Interpolation = trolleyBody.interpolation;
                CollisionDetection = trolleyBody.collisionDetectionMode;
                Constraints = trolleyBody.constraints;
                LinearDamping = trolleyBody.linearDamping;
                AngularDamping = trolleyBody.angularDamping;
                SolverIterations = trolleyBody.solverIterations;
                SolverVelocityIterations = trolleyBody.solverVelocityIterations;
            }
        }
    }
}
