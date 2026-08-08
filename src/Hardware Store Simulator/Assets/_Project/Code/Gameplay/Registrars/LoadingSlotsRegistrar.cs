using System;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    public sealed class LoadingSlotsRegistrar : EntityComponentRegistrar
    {
        [SerializeField] private Transform[] _slots;

        public void Configure(Transform[] slots)
        {
            Validate(slots);
            _slots = slots;
        }

        public override void RegisterComponents()
        {
            Validate(_slots);
            Entity.AddLoadingSlots(_slots);
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasLoadingSlots)
                Entity.RemoveLoadingSlots();
        }

        private static void Validate(Transform[] slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));
            if (slots.Length == 0)
                throw new ArgumentException("At least one loading slot is required.", nameof(slots));

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    throw new ArgumentException($"Loading slot {i} is missing.", nameof(slots));
            }
        }
    }
}
