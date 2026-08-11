using System;
using System.Collections.Generic;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct ConsultationSnapshot
    {
        public ConsultationSnapshot(string projectTitle, string customerRequest,
            int cargoCapacity, ConsultationOfferSnapshot[] offers)
        {
            ProjectTitle = projectTitle ?? throw new ArgumentNullException(nameof(projectTitle));
            CustomerRequest = customerRequest ??
                              throw new ArgumentNullException(nameof(customerRequest));
            if (cargoCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(cargoCapacity));
            if (offers == null)
                throw new ArgumentNullException(nameof(offers));
            if (offers.Length != 3)
                throw new ArgumentException(
                    "A consultation must present exactly three offers.", nameof(offers));

            CargoCapacity = cargoCapacity;
            Offers = Array.AsReadOnly((ConsultationOfferSnapshot[])offers.Clone());
        }

        public string ProjectTitle { get; }
        public string CustomerRequest { get; }
        public int CargoCapacity { get; }
        public IReadOnlyList<ConsultationOfferSnapshot> Offers { get; }
    }
}
