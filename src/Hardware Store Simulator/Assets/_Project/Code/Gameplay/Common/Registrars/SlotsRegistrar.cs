using System;
using System.Collections.Generic;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;

namespace HardwareStore.Gameplay.Common.Registrars
{
    [DisallowMultipleComponent]
    public sealed class SlotsRegistrar : EntityComponentRegistrar
    {
        [SerializeField] private Transform[] _slots;

        public void Configure(Transform[] slots)
        {
            Validate(slots);
            _slots = (Transform[])slots.Clone();
        }

        public override void RegisterComponents()
        {
            Validate(_slots);
            Entity.AddSlots((Transform[])_slots.Clone());
        }

        public override void UnregisterComponents()
        {
            if (Entity.hasSlots)
                Entity.RemoveSlots();
        }

        internal void ValidateConfiguration(
            EntityBehaviour entityBehaviour,
            int minimumSlotCount)
        {
            if (entityBehaviour == null)
                throw new ArgumentNullException(nameof(entityBehaviour));
            if (minimumSlotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSlotCount));
            if (transform != entityBehaviour.transform)
            {
                throw new InvalidOperationException(
                    "SlotsRegistrar must be on its EntityBehaviour root.");
            }

            Validate(_slots);
            if (_slots.Length < minimumSlotCount)
            {
                throw new InvalidOperationException(
                    $"SlotsRegistrar exposes {_slots.Length} slots, but at least " +
                    $"{minimumSlotCount} are required.");
            }

            for (int index = 0; index < _slots.Length; index++)
            {
                if (!_slots[index].IsChildOf(entityBehaviour.transform))
                {
                    throw new InvalidOperationException(
                        $"Registered slot {index} is outside the entity hierarchy.");
                }
            }
        }

        private static void Validate(Transform[] slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));
            if (slots.Length == 0)
                throw new ArgumentException("At least one slot is required.", nameof(slots));

            var uniqueSlots = new HashSet<Transform>();
            for (int index = 0; index < slots.Length; index++)
            {
                Transform slot = slots[index];
                if (slot == null)
                    throw new ArgumentException($"Slot {index} is missing.", nameof(slots));
                if (!uniqueSlots.Add(slot))
                    throw new ArgumentException($"Slot {index} is registered more than once.", nameof(slots));
            }
        }
    }
}
