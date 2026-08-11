using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Order : IComponent { }
    [Game] public class OrderLine : IComponent { }
    [Game] public class OrderRewarded : IComponent { }
    [Game] public class OrderContentReleased : IComponent { }
    [Game] public class OrderEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class OrderLineEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class LineIndex : IComponent { public int Value; }
    [Game] public class RequiredProductCount : IComponent { public int Value; }
    [Game] public class AvailableProductCount : IComponent { public int Value; }
    [Game] public class LoadedProductCount : IComponent { public int Value; }
    [Game] public class OrderReward : IComponent { public int Value; }
}
