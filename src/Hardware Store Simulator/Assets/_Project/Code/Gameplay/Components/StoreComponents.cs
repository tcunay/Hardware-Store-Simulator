using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Store : IComponent { }
    [Game] public class StoreEntityId : IComponent { public int Value; }
    [Game] public class StorageZoneEntityId : IComponent { public int Value; }
    [Game] public class OrderCounterEntityId : IComponent { public int Value; }
    [Game] public class Money : IComponent { public int Value; }
    [Game] public class StorageProductCount : IComponent { public int Value; }
}
