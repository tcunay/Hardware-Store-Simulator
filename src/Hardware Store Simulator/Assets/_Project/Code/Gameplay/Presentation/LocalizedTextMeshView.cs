using System;
using HardwareStore.Gameplay.Localization;
using UnityEngine;
using Zenject;

namespace HardwareStore.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LocalizedTextMeshView : MonoBehaviour
    {
        [SerializeField] private TextMesh _label;
        [SerializeField] private LocalizationKey _key;
        [SerializeField] private int[] _numberArguments = Array.Empty<int>();

        private ILocalizationService _localization;
        private bool _started;

        public LocalizationKey Key => _key;
        public TextMesh Label => _label;
        public int[] NumberArguments => (int[])_numberArguments.Clone();

        [Inject]
        private void Construct(ILocalizationService localization) =>
            _localization = localization;

        private void Start()
        {
            _started = true;
            Refresh();
        }

        public void Configure(TextMesh label, LocalizationKey key,
            params int[] numberArguments)
        {
            _label = label != null ? label : throw new ArgumentNullException(nameof(label));
            if (key == LocalizationKey.None)
                throw new ArgumentOutOfRangeException(nameof(key));

            _key = key;
            _numberArguments = numberArguments == null
                ? throw new ArgumentNullException(nameof(numberArguments))
                : (int[])numberArguments.Clone();

            if (_started)
                Refresh();
        }

        private void Refresh()
        {
            if (_label == null)
                throw new InvalidOperationException(
                    $"{nameof(LocalizedTextMeshView)} requires a TextMesh reference.");
            if (_key == LocalizationKey.None)
                throw new InvalidOperationException(
                    $"{nameof(LocalizedTextMeshView)} requires a localization key.");

            var arguments = new LocalizationArgument[_numberArguments.Length];
            for (int index = 0; index < arguments.Length; index++)
                arguments[index] = _numberArguments[index];

            _label.text = _localization.Resolve(new LocalizedText(_key, arguments));
        }
    }
}
