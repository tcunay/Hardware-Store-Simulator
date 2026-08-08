using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class ProductView : InteractionView
    {
        private static readonly Quaternion HeldRotationOffset = Quaternion.Euler(8f, 0f, 0f);

        private Rigidbody _body;
        private Collider[] _colliders;
        private bool[] _defaultColliderStates;
        private RigidbodyInterpolation _defaultInterpolation;
        private CollisionDetectionMode _defaultCollisionDetection;

        protected override void Awake()
        {
            base.Awake();

            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _defaultColliderStates = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
                _defaultColliderStates[i] = _colliders[i].enabled;

            _defaultInterpolation = _body.interpolation;
            _defaultCollisionDetection = _body.collisionDetectionMode;
        }

        public void AttachToHands(Transform anchor)
        {
            if (anchor == null)
                throw new ArgumentNullException(nameof(anchor));

            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.detectCollisions = false;
            _body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            transform.SetParent(null, true);

            FollowHands(anchor);
        }

        public void FollowHands(Transform anchor)
        {
            if (anchor == null)
                throw new ArgumentNullException(nameof(anchor));

            Vector3 position = anchor.position;
            Quaternion rotation = anchor.rotation * HeldRotationOffset;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        public void Drop(Vector3 position, Quaternion rotation)
        {
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            _body.position = position;
            _body.rotation = rotation;

            RestoreColliderStates();
            _body.isKinematic = false;
            _body.useGravity = true;
            _body.detectCollisions = true;
            _body.interpolation = _defaultInterpolation;
            _body.collisionDetectionMode = _defaultCollisionDetection;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

        }

        public void SnapToLoadingSlot(Transform slot)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.detectCollisions = false;
            _body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);

            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _body.position = slot.position;
            _body.rotation = slot.rotation;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider productCollider in _colliders)
                productCollider.enabled = enabled;
        }

        private void RestoreColliderStates()
        {
            for (int i = 0; i < _colliders.Length; i++)
                _colliders[i].enabled = _defaultColliderStates[i];
        }
    }
}
