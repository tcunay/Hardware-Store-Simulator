using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class ProcurementTerminal : IComponent { }
    [Game] public class StorageZone : IComponent { }
    [Game] public class DeliveryEntityId : IComponent { public int Value; }
    [Game] public class PurchaseDeliveryRequest : IComponent { }
    [Game] public class PurchaseDeliverySucceeded : IComponent { }
    [Game] public class Delivery : IComponent { }
    [Game] public class DeliveryActive : IComponent { }
    [Game] public class DeliveryCompleted : IComponent { }
    [Game] public class DeliveryProductsSpawned : IComponent { }
    [Game] public class DeliveryProductCount : IComponent { public int Value; }
    [Game] public class StockedProductCount : IComponent { public int Value; }
    [Game] public class OccupiedStorageSlotCount : IComponent { public int Value; }
    [Game] public class DeliveryCost : IComponent { public int Value; }
    [Game] public class DeliveryProcurementTerminalEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ProcurementTerminalEntityId : IComponent { public int Value; }
}
