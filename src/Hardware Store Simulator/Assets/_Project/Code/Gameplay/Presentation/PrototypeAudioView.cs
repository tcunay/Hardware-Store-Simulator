using System;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PrototypeAudioView : MonoBehaviour, IAudioService
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private AudioClip _pickUp;
        private AudioClip _drop;
        private AudioClip _load;
        private AudioClip _orderAccepted;
        private AudioClip _deliveryPurchased;
        private AudioClip _productStocked;
        private AudioClip _deliveryCompleted;
        private AudioClip _reward;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _pickUp = CreateTone("PickUp", 150f, 0.12f, 0.18f, -120f);
            _drop = CreateTone("Drop", 90f, 0.18f, 0.22f, -45f);
            _load = CreateTone("Load", 115f, 0.22f, 0.25f, -35f);
            _orderAccepted = CreateTone("OrderAccepted", 420f, 0.18f, 0.16f, 280f);
            _deliveryPurchased = CreateTone("DeliveryPurchased", 310f, 0.24f, 0.17f, 190f);
            _productStocked = CreateTone("ProductStocked", 185f, 0.16f, 0.18f, 120f);
            _deliveryCompleted = CreateTone("DeliveryCompleted", 440f, 0.32f, 0.18f, 300f);
            _reward = CreateRewardTone();
        }

        public void Play(AudioCueId cue)
        {
            AudioClip clip = cue switch
            {
                AudioCueId.PickUp => _pickUp,
                AudioCueId.Drop => _drop,
                AudioCueId.Load => _load,
                AudioCueId.OrderAccepted => _orderAccepted,
                AudioCueId.DeliveryPurchased => _deliveryPurchased,
                AudioCueId.ProductStocked => _productStocked,
                AudioCueId.DeliveryCompleted => _deliveryCompleted,
                AudioCueId.Reward => _reward,
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown prototype audio cue.")
            };

            _source.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            Destroy(_pickUp);
            Destroy(_drop);
            Destroy(_load);
            Destroy(_orderAccepted);
            Destroy(_deliveryPurchased);
            Destroy(_productStocked);
            Destroy(_deliveryCompleted);
            Destroy(_reward);
        }

        private static AudioClip CreateTone(string name, float startFrequency, float duration, float volume,
            float frequencySweep)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;
                float frequency = startFrequency + frequencySweep * progress;
                float envelope = Mathf.Sin(Mathf.PI * progress);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateRewardTone()
        {
            const float duration = 0.48f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = i / (float)sampleCount;
                float frequency = progress < 0.5f ? 520f : 780f;
                float envelope = Mathf.Sin(Mathf.PI * progress);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.18f;
            }

            AudioClip clip = AudioClip.Create("Reward", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
