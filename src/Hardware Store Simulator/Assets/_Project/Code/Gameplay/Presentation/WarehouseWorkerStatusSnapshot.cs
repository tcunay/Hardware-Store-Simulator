using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct WarehouseWorkerStatusSnapshot
    {
        public WarehouseWorkerStatusSnapshot(
            WarehouseWorkerStatusId status,
            ProductTypeId? productType)
        {
            if (!Enum.IsDefined(typeof(WarehouseWorkerStatusId), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (status is WarehouseWorkerStatusId.MovingToPickup or
                WarehouseWorkerStatusId.MovingToStorage or
                WarehouseWorkerStatusId.MovingToCustomerLoading)
            {
                if (!productType.HasValue ||
                    !Enum.IsDefined(typeof(ProductTypeId), productType.Value))
                {
                    throw new ArgumentException(
                        "A moving warehouse worker requires a valid task product.",
                        nameof(productType));
                }
            }
            else if (productType.HasValue)
            {
                throw new ArgumentException(
                    "Only a moving warehouse worker may expose a task product.",
                    nameof(productType));
            }

            Status = status;
            ProductType = productType;
        }

        public WarehouseWorkerStatusId Status { get; }
        public ProductTypeId? ProductType { get; }
    }
}
