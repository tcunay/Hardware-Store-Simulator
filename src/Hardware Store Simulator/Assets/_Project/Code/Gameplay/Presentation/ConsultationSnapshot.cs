using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ConsultationSnapshot
    {
        public ConsultationSnapshot(CustomerProjectTypeId projectType,
            int cargoCapacity, ConsultationOfferSnapshot[] offers)
        {
            ProjectType = projectType;
            if (cargoCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(cargoCapacity));
            if (offers == null)
                throw new ArgumentNullException(nameof(offers));
            if (offers.Length != 1)
                throw new ArgumentException(
                    "A consultation must present exactly one customer order.",
                    nameof(offers));

            CargoCapacity = cargoCapacity;
            Offers = Array.AsReadOnly((ConsultationOfferSnapshot[])offers.Clone());
        }

        public CustomerProjectTypeId ProjectType { get; }
        public int CargoCapacity { get; }
        public IReadOnlyList<ConsultationOfferSnapshot> Offers { get; }
    }
}
