using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class ProcurementCart : IComponent { }
    [Game] public class ProcurementCartLine : IComponent { }
    [Game] public class ProcurementCartTerminalEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ProcurementCartEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class ProcurementCartPackageCapacity : IComponent { public int Value; }
    [Game] public class ProcurementPackageCount : IComponent { public int Value; }

    [Game] public class PurchaseOrder : IComponent { }
    [Game] public class PurchaseOrderLine : IComponent { }
    [Game] public class PurchaseOrderProcurementTerminalEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class PurchaseOrderEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class PurchaseOrderPackageCount : IComponent { public int Value; }
    [Game] public class PurchaseOrderProductCount : IComponent { public int Value; }
    [Game] public class PurchaseOrderCost : IComponent { public int Value; }
    [Game] public class PurchaseOrderLineIndex : IComponent { public int Value; }
    [Game] public class PurchaseOrderLinePackageCount : IComponent { public int Value; }
    [Game] public class PurchaseOrderLineProductCount : IComponent { public int Value; }
    [Game] public class PurchaseOrderLineCost : IComponent { public int Value; }
    [Game] public class PurchaseOrderLineStockedProductCount : IComponent { public int Value; }
    [Game] public class DeliveryPurchaseOrderEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class PurchaseOrderLineEntityId : IComponent { [EntityIndex] public int Value; }
}
