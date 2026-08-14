using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Views
{
    [DisallowMultipleComponent]
    public sealed class CustomerDissatisfactionView : MonoBehaviour
    {
        public const float DissatisfiedColorBlend = 0.55f;
        public const float RaisedArmAngle = 145f;
        public const float ArmWaveAmplitude = 12f;
        public const float ArmWaveAngularSpeed = 7f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _rightShoulder;
        [SerializeField] private TextMesh _worldLabel;

        private MaterialPropertyBlock[][] _propertyBlocks;
        private Color[][] _baseColors;
        private Quaternion _leftNeutralRotation;
        private Quaternion _rightNeutralRotation;
        private Quaternion _worldLabelNeutralRotation;
        private Transform _cameraTransform;
        private bool _initialized;
        private bool _isDissatisfied;

        public bool IsDissatisfied => _isDissatisfied;
        public Renderer[] Renderers => (Renderer[])_renderers.Clone();
        public Transform LeftShoulder => _leftShoulder;
        public Transform RightShoulder => _rightShoulder;
        public TextMesh WorldLabel => _worldLabel;

        public void Configure(Renderer[] renderers,
            Transform leftShoulder,
            Transform rightShoulder,
            TextMesh worldLabel)
        {
            ValidateConfiguration(
                renderers,
                leftShoulder,
                rightShoulder,
                worldLabel);
            _renderers = (Renderer[])renderers.Clone();
            _leftShoulder = leftShoulder;
            _rightShoulder = rightShoulder;
            _worldLabel = worldLabel;
        }

        public void SetDissatisfied(
            bool dissatisfied,
            string localizedWorldLabel)
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    $"{nameof(CustomerDissatisfactionView)} must be initialized before use.");
            if (dissatisfied && string.IsNullOrWhiteSpace(localizedWorldLabel))
                throw new ArgumentException(
                    "A dissatisfied customer requires a localized world label.",
                    nameof(localizedWorldLabel));

            if (_isDissatisfied == dissatisfied)
            {
                if (dissatisfied &&
                    !string.Equals(
                        _worldLabel.text,
                        localizedWorldLabel,
                        StringComparison.Ordinal))
                {
                    _worldLabel.text = localizedWorldLabel;
                }
                return;
            }

            _isDissatisfied = dissatisfied;
            ApplyBodyColor(dissatisfied);
            if (dissatisfied)
            {
                _worldLabel.text = localizedWorldLabel;
                _worldLabel.gameObject.SetActive(true);
                ApplyArmPose(0f);
                FaceWorldLabelTowardCamera();
            }
            else
            {
                _worldLabel.text = string.Empty;
                _worldLabel.gameObject.SetActive(false);
                RestoreArmPose();
                RestoreWorldLabelRotation();
            }
        }

        private void Awake()
        {
            ValidateConfiguration(
                _renderers,
                _leftShoulder,
                _rightShoulder,
                _worldLabel);
            CacheMaterials();
            _leftNeutralRotation = _leftShoulder.localRotation;
            _rightNeutralRotation = _rightShoulder.localRotation;
            _worldLabelNeutralRotation = _worldLabel.transform.localRotation;
            Camera presentationCamera = Camera.main;
            if (presentationCamera == null)
                throw new InvalidOperationException(
                    "Customer dissatisfaction requires the tagged presentation camera.");
            _cameraTransform = presentationCamera.transform;
            _initialized = true;

            ApplyBodyColor(dissatisfied: false);
            _worldLabel.text = string.Empty;
            _worldLabel.gameObject.SetActive(false);
            RestoreArmPose();
            RestoreWorldLabelRotation();
        }

        private void Update()
        {
            if (!_isDissatisfied)
                return;

            float waveAngle = Mathf.Sin(
                Time.unscaledTime * ArmWaveAngularSpeed) * ArmWaveAmplitude;
            ApplyArmPose(waveAngle);
        }

        private void LateUpdate()
        {
            if (!_isDissatisfied)
                return;

            FaceWorldLabelTowardCamera();
        }

        private void OnDisable()
        {
            if (!_initialized)
                return;

            _isDissatisfied = false;
            ApplyBodyColor(dissatisfied: false);
            _worldLabel.text = string.Empty;
            _worldLabel.gameObject.SetActive(false);
            RestoreArmPose();
            RestoreWorldLabelRotation();
        }

        private void CacheMaterials()
        {
            _propertyBlocks = new MaterialPropertyBlock[_renderers.Length][];
            _baseColors = new Color[_renderers.Length][];
            for (int rendererIndex = 0;
                 rendererIndex < _renderers.Length;
                 rendererIndex++)
            {
                Material[] materials = _renderers[rendererIndex].sharedMaterials;
                if (materials.Length == 0)
                    throw new InvalidOperationException(
                        $"Customer renderer {_renderers[rendererIndex].name} has no materials.");

                _propertyBlocks[rendererIndex] =
                    new MaterialPropertyBlock[materials.Length];
                _baseColors[rendererIndex] = new Color[materials.Length];
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null || !material.HasProperty(BaseColorId))
                    {
                        throw new InvalidOperationException(
                            $"Customer renderer {_renderers[rendererIndex].name} material " +
                            $"{materialIndex} must expose the URP base-color property.");
                    }

                    _propertyBlocks[rendererIndex][materialIndex] =
                        new MaterialPropertyBlock();
                    _baseColors[rendererIndex][materialIndex] =
                        material.GetColor(BaseColorId);
                }
            }
        }

        private void ApplyBodyColor(bool dissatisfied)
        {
            for (int rendererIndex = 0;
                 rendererIndex < _renderers.Length;
                 rendererIndex++)
            {
                Renderer bodyRenderer = _renderers[rendererIndex];
                MaterialPropertyBlock[] rendererBlocks =
                    _propertyBlocks[rendererIndex];
                Color[] rendererBaseColors = _baseColors[rendererIndex];
                for (int materialIndex = 0;
                     materialIndex < rendererBlocks.Length;
                     materialIndex++)
                {
                    MaterialPropertyBlock propertyBlock =
                        rendererBlocks[materialIndex];
                    bodyRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                    Color bodyColor = dissatisfied
                        ? Color.Lerp(
                            rendererBaseColors[materialIndex],
                            Color.red,
                            DissatisfiedColorBlend)
                        : rendererBaseColors[materialIndex];
                    propertyBlock.SetColor(BaseColorId, bodyColor);
                    bodyRenderer.SetPropertyBlock(propertyBlock, materialIndex);
                }
            }
        }

        private void ApplyArmPose(float waveAngle)
        {
            _leftShoulder.localRotation = _leftNeutralRotation *
                Quaternion.Euler(0f, 0f, -RaisedArmAngle + waveAngle);
            _rightShoulder.localRotation = _rightNeutralRotation *
                Quaternion.Euler(0f, 0f, RaisedArmAngle - waveAngle);
        }

        private void RestoreArmPose()
        {
            _leftShoulder.localRotation = _leftNeutralRotation;
            _rightShoulder.localRotation = _rightNeutralRotation;
        }

        private void FaceWorldLabelTowardCamera()
        {
            Vector3 awayFromCamera =
                _worldLabel.transform.position - _cameraTransform.position;
            if (awayFromCamera.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException(
                    "Customer dissatisfaction label cannot coincide with the camera.");

            _worldLabel.transform.rotation = Quaternion.LookRotation(
                awayFromCamera,
                _cameraTransform.up);
        }

        private void RestoreWorldLabelRotation()
        {
            _worldLabel.transform.localRotation = _worldLabelNeutralRotation;
        }

        private void ValidateConfiguration(Renderer[] renderers,
            Transform leftShoulder,
            Transform rightShoulder,
            TextMesh worldLabel)
        {
            if (renderers == null)
                throw new ArgumentNullException(nameof(renderers));
            if (renderers.Length == 0)
                throw new ArgumentException(
                    "Customer dissatisfaction requires at least one body renderer.",
                    nameof(renderers));
            if (leftShoulder == null)
                throw new ArgumentNullException(nameof(leftShoulder));
            if (rightShoulder == null)
                throw new ArgumentNullException(nameof(rightShoulder));
            if (worldLabel == null)
                throw new ArgumentNullException(nameof(worldLabel));
            if (leftShoulder == transform || rightShoulder == transform ||
                leftShoulder == rightShoulder ||
                !leftShoulder.IsChildOf(transform) ||
                !rightShoulder.IsChildOf(transform))
            {
                throw new ArgumentException(
                    "Customer dissatisfaction requires two distinct shoulder pivots below " +
                    "the customer root.");
            }
            if (worldLabel.gameObject == gameObject ||
                !worldLabel.transform.IsChildOf(transform))
            {
                throw new ArgumentException(
                    "Customer dissatisfaction world label must be a child of the customer root.",
                    nameof(worldLabel));
            }

            Renderer labelRenderer = worldLabel.GetComponent<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer bodyRenderer = renderers[index];
                if (bodyRenderer == null)
                    throw new ArgumentException(
                        $"Customer body renderer {index} is missing.",
                        nameof(renderers));
                if (!bodyRenderer.transform.IsChildOf(transform) ||
                    ReferenceEquals(bodyRenderer, labelRenderer))
                {
                    throw new ArgumentException(
                        $"Customer body renderer {index} must be a non-label child renderer.",
                        nameof(renderers));
                }
                for (int previous = 0; previous < index; previous++)
                {
                    if (ReferenceEquals(renderers[previous], bodyRenderer))
                    {
                        throw new ArgumentException(
                            $"Customer body renderer {index} is duplicated.",
                            nameof(renderers));
                    }
                }
            }
        }
    }
}
