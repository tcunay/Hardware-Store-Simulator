using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Order : IComponent { }
    [Game] public class OrderRewarded : IComponent { }
    [Game] public class RequiredProductCount : IComponent { public int Value; }
    [Game] public class AvailableProductCount : IComponent { public int Value; }
    [Game] public class LoadedProductCount : IComponent { public int Value; }
    [Game] public class OrderReward : IComponent { public int Value; }
    [Game] public class RequiredProductType : IComponent { public ProductTypeId Value; }
}
