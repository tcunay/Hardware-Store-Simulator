using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Order : IComponent { }
    [Game] public class Wallet : IComponent { }
    [Game] public class OrderWaiting : IComponent { }
    [Game] public class OrderActive : IComponent { }
    [Game] public class OrderCompleted : IComponent { }
    [Game] public class OrderCompletedEvent : IComponent { }
    [Game] public class OrderEntityId : IComponent { public int Value; }
    [Game] public class WalletEntityId : IComponent { public int Value; }
    [Game] public class RequiredProductCount : IComponent { public int Value; }
    [Game] public class LoadedProductCount : IComponent { public int Value; }
    [Game] public class OrderReward : IComponent { public int Value; }
    [Game] public class Money : IComponent { public int Value; }
    [Game] public class RequiredProductType : IComponent { public ProductTypeId Value; }
}
