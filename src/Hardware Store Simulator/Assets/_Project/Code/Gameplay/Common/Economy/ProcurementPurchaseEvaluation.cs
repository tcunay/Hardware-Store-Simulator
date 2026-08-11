using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Common.Economy
{
    public readonly struct ProcurementPurchaseEvaluation
    {
        public ProcurementPurchaseEvaluation(
            ProcurementPurchaseAvailability availability,
            ProcurementDemandKind demandKind,
            CustomerProjectTypeId projectType,
            int deliveryProductCount,
            int deliveryCost,
            int moneyAfterPurchase)
        {
            if (!Enum.IsDefined(typeof(ProcurementPurchaseAvailability), availability))
                throw new ArgumentOutOfRangeException(nameof(availability));
            if (!Enum.IsDefined(typeof(ProcurementDemandKind), demandKind))
                throw new ArgumentOutOfRangeException(nameof(demandKind));
            if (!Enum.IsDefined(typeof(CustomerProjectTypeId), projectType))
                throw new ArgumentOutOfRangeException(nameof(projectType));
            if (deliveryProductCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(deliveryProductCount));
            if (deliveryCost < 0)
                throw new ArgumentOutOfRangeException(nameof(deliveryCost));

            Availability = availability;
            DemandKind = demandKind;
            ProjectType = projectType;
            DeliveryProductCount = deliveryProductCount;
            DeliveryCost = deliveryCost;
            MoneyAfterPurchase = moneyAfterPurchase;
        }

        public ProcurementPurchaseAvailability Availability { get; }
        public ProcurementDemandKind DemandKind { get; }
        public CustomerProjectTypeId ProjectType { get; }
        public int DeliveryProductCount { get; }
        public int DeliveryCost { get; }
        public int MoneyAfterPurchase { get; }
        public bool CanPurchase =>
            Availability == ProcurementPurchaseAvailability.Available;
    }
}
