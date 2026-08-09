using UnityEngine;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    internal static class ProductPhysicsUtility
    {
        public static void ConfigureCarried(GameEntity product)
        {
            Rigidbody body = product.Rigidbody;
            StopMotion(body);
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(product.Colliders, false);
            product.Transform.SetParent(null, worldPositionStays: true);
        }

        public static void ConfigureLoose(GameEntity product)
        {
            Transform transform = product.Transform;
            Rigidbody body = product.Rigidbody;
            transform.SetParent(null, worldPositionStays: true);
            transform.SetPositionAndRotation(product.WorldPosition, product.WorldRotation);
            body.position = product.WorldPosition;
            body.rotation = product.WorldRotation;
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.interpolation = product.RigidbodyInterpolationMode;
            body.collisionDetectionMode = product.RigidbodyCollisionDetectionMode;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            SetCollidersEnabled(product.Colliders, true);
        }

        public static void ConfigureInteractiveSlot(GameEntity product, Transform slot)
        {
            ConfigureKinematic(product, collisionsEnabled: true, collidersEnabled: true);
            SnapToSlot(product, slot);
        }

        public static void ConfigureLockedSlot(GameEntity product, Transform slot)
        {
            ConfigureKinematic(product, collisionsEnabled: false, collidersEnabled: false);
            SnapToSlot(product, slot);
        }

        private static void ConfigureKinematic(GameEntity product, bool collisionsEnabled,
            bool collidersEnabled)
        {
            Rigidbody body = product.Rigidbody;
            StopMotion(body);
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = collisionsEnabled;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = product.RigidbodyCollisionDetectionMode;
            SetCollidersEnabled(product.Colliders, collidersEnabled);
        }

        private static void SnapToSlot(GameEntity product, Transform slot)
        {
            Transform transform = product.Transform;
            transform.SetParent(slot, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            product.Rigidbody.position = slot.position;
            product.Rigidbody.rotation = slot.rotation;
        }

        private static void StopMotion(Rigidbody body)
        {
            if (body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void SetCollidersEnabled(Collider[] colliders, bool enabled)
        {
            foreach (Collider productCollider in colliders)
                productCollider.enabled = enabled;
        }
    }
}
