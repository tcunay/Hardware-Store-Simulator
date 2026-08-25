using System;
using UnityEngine;

namespace HardwareStore.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PrototypeDayNightView : MonoBehaviour,
        IDayNightPresentationService
    {
        private const int RequiredIndoorLightCount = 2;

        [SerializeField] private Light _sun;
        [SerializeField] private Light[] _indoorLights;

        private Quaternion _initialSunRotation;
        private Color _initialSunColor;
        private float _initialSunIntensity;
        private Color _initialAmbientSkyColor;
        private Color _initialAmbientEquatorColor;
        private Color _initialAmbientGroundColor;
        private Color _initialFogColor;
        private Light _initialRenderSun;
        private float[] _indoorNightIntensities;
        private Material _originalSkybox;
        private Material _runtimeSkybox;
        private bool _runtimeStateCaptured;

        public void Configure(Light sun, Light[] indoorLights)
        {
            _sun = sun != null ? sun : throw new ArgumentNullException(nameof(sun));
            if (indoorLights == null)
                throw new ArgumentNullException(nameof(indoorLights));
            if (indoorLights.Length != RequiredIndoorLightCount)
            {
                throw new ArgumentException(
                    $"The prototype day/night view requires exactly " +
                    $"{RequiredIndoorLightCount} indoor lights.",
                    nameof(indoorLights));
            }
            for (int index = 0; index < indoorLights.Length; index++)
            {
                if (indoorLights[index] == null)
                {
                    throw new ArgumentException(
                        $"Indoor light at index {index} is missing.",
                        nameof(indoorLights));
                }
            }

            _indoorLights = (Light[])indoorLights.Clone();
        }

        public void Present(DayNightSnapshot snapshot)
        {
            EnsureRuntimeState();

            float progress = snapshot.NormalizedTime;
            float noonFactor = Mathf.Sin(progress * Mathf.PI);
            float eveningFactor = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.68f, 1f, progress));
            float daylightFactor = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.72f, 1f, progress));

            _sun.transform.rotation = Quaternion.Euler(
                Mathf.Lerp(25f, 155f, progress),
                Mathf.Lerp(-65f, 35f, progress),
                0f);
            _sun.color = progress < 0.62f
                ? Color.Lerp(
                    new Color(1f, 0.72f, 0.5f),
                    new Color(1f, 0.95f, 0.86f),
                    Mathf.InverseLerp(0f, 0.62f, progress))
                : Color.Lerp(
                    new Color(1f, 0.95f, 0.86f),
                    new Color(0.34f, 0.45f, 0.68f),
                    Mathf.InverseLerp(0.62f, 1f, progress));
            _sun.intensity = Mathf.Lerp(0.08f, 0.72f + noonFactor * 0.68f,
                daylightFactor);

            RenderSettings.ambientSkyColor = Color.Lerp(
                new Color(0.53f, 0.62f, 0.72f),
                new Color(0.045f, 0.065f, 0.12f),
                eveningFactor);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                new Color(0.34f, 0.37f, 0.38f),
                new Color(0.055f, 0.065f, 0.09f),
                eveningFactor);
            RenderSettings.ambientGroundColor = Color.Lerp(
                new Color(0.16f, 0.15f, 0.13f),
                new Color(0.018f, 0.022f, 0.035f),
                eveningFactor);
            RenderSettings.fogColor = Color.Lerp(
                new Color(0.57f, 0.64f, 0.68f),
                new Color(0.035f, 0.05f, 0.085f),
                eveningFactor);

            for (int index = 0; index < _indoorLights.Length; index++)
            {
                _indoorLights[index].intensity =
                    _indoorNightIntensities[index] * eveningFactor;
            }

            PresentSkybox(eveningFactor);
        }

        private void EnsureRuntimeState()
        {
            if (_runtimeStateCaptured)
                return;
            if (_sun == null)
                throw new InvalidOperationException("Day/night Sun reference is missing.");
            if (_sun.type != LightType.Directional)
            {
                throw new InvalidOperationException(
                    "The day/night Sun must be a directional light.");
            }
            if (_indoorLights == null ||
                _indoorLights.Length != RequiredIndoorLightCount)
            {
                throw new InvalidOperationException(
                    $"Day/night presentation requires exactly " +
                    $"{RequiredIndoorLightCount} indoor lights.");
            }

            _indoorNightIntensities = new float[_indoorLights.Length];
            for (int index = 0; index < _indoorLights.Length; index++)
            {
                Light indoorLight = _indoorLights[index];
                if (indoorLight == null || indoorLight.type != LightType.Point ||
                    indoorLight.intensity <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Indoor light at index {index} must be an authored point light " +
                        "with positive night intensity.");
                }

                _indoorNightIntensities[index] = indoorLight.intensity;
            }

            _initialSunRotation = _sun.transform.rotation;
            _initialSunColor = _sun.color;
            _initialSunIntensity = _sun.intensity;
            _initialAmbientSkyColor = RenderSettings.ambientSkyColor;
            _initialAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            _initialAmbientGroundColor = RenderSettings.ambientGroundColor;
            _initialFogColor = RenderSettings.fogColor;
            _initialRenderSun = RenderSettings.sun;
            _originalSkybox = RenderSettings.skybox;
            if (_originalSkybox != null)
            {
                _runtimeSkybox = new Material(_originalSkybox)
                {
                    name = $"{_originalSkybox.name} (Runtime Day Night)",
                    hideFlags = HideFlags.DontSave
                };
                RenderSettings.skybox = _runtimeSkybox;
            }

            RenderSettings.sun = _sun;
            _runtimeStateCaptured = true;
        }

        private void PresentSkybox(float eveningFactor)
        {
            if (_runtimeSkybox == null)
                return;

            if (_runtimeSkybox.HasProperty("_Exposure"))
                _runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(1.15f, 0.12f, eveningFactor));
            if (_runtimeSkybox.HasProperty("_SkyTint"))
            {
                _runtimeSkybox.SetColor("_SkyTint", Color.Lerp(
                    new Color(0.52f, 0.64f, 0.78f),
                    new Color(0.055f, 0.075f, 0.16f),
                    eveningFactor));
            }
            if (_runtimeSkybox.HasProperty("_GroundColor"))
            {
                _runtimeSkybox.SetColor("_GroundColor", Color.Lerp(
                    new Color(0.32f, 0.3f, 0.26f),
                    new Color(0.018f, 0.022f, 0.04f),
                    eveningFactor));
            }
            if (_runtimeSkybox.HasProperty("_AtmosphereThickness"))
            {
                _runtimeSkybox.SetFloat(
                    "_AtmosphereThickness",
                    Mathf.Lerp(0.9f, 0.2f, eveningFactor));
            }
        }

        private void OnDisable() =>
            ReleaseRuntimeState();

        private void OnDestroy() =>
            ReleaseRuntimeState();

        private void ReleaseRuntimeState()
        {
            if (!_runtimeStateCaptured)
                return;

            if (_sun != null)
            {
                _sun.transform.rotation = _initialSunRotation;
                _sun.color = _initialSunColor;
                _sun.intensity = _initialSunIntensity;
            }
            RenderSettings.ambientSkyColor = _initialAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _initialAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _initialAmbientGroundColor;
            RenderSettings.fogColor = _initialFogColor;
            RenderSettings.sun = _initialRenderSun;
            for (int index = 0; index < _indoorLights.Length; index++)
            {
                if (_indoorLights[index] != null)
                {
                    _indoorLights[index].intensity =
                        _indoorNightIntensities[index];
                }
            }

            if (RenderSettings.skybox == _runtimeSkybox)
                RenderSettings.skybox = _originalSkybox;
            if (_runtimeSkybox != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeSkybox);
                else
                    DestroyImmediate(_runtimeSkybox);
            }

            _runtimeSkybox = null;
            _originalSkybox = null;
            _initialRenderSun = null;
            _indoorNightIntensities = null;
            _runtimeStateCaptured = false;
        }
    }
}
