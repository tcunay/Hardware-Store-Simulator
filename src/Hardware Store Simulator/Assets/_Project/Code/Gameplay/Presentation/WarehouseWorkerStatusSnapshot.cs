using System;
using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct WarehouseWorkerStatusSnapshot
    {
        public WarehouseWorkerStatusSnapshot(
            WarehouseWorkerStatusId status,
            ProductTypeId? productType,
            int? batchProductCount = null)
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
                if (batchProductCount.HasValue)
                    throw new ArgumentException(
                        "A direct warehouse task cannot expose a batch count.",
                        nameof(batchProductCount));
            }
            else if (status ==
                     WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading)
            {
                if (productType.HasValue || !batchProductCount.HasValue ||
                    batchProductCount.Value < 2)
                    throw new ArgumentException(
                        "A moving worker trolley requires a batch of at least two products.",
                        nameof(batchProductCount));
            }
            else if (productType.HasValue || batchProductCount.HasValue)
            {
                throw new ArgumentException(
                    "This warehouse-worker status cannot expose task cargo.",
                    productType.HasValue
                        ? nameof(productType)
                        : nameof(batchProductCount));
            }

            Status = status;
            ProductType = productType;
            BatchProductCount = batchProductCount;
        }

        public WarehouseWorkerStatusId Status { get; }
        public ProductTypeId? ProductType { get; }
        public int? BatchProductCount { get; }
    }
}
