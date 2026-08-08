using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Product : IComponent { }
    [Game] public class Carried : IComponent { }
    [Game] public class Loaded : IComponent { }
    [Game] public class ProductLoaded : IComponent { }
    [Game] public class ProductEntityId : IComponent { public int Value; }
    [Game] public class ProductViewComponent : IComponent { public HardwareStore.Gameplay.Views.ProductView Value; }
    [Game] public class RigidbodyComponent : IComponent { public UnityEngine.Rigidbody Value; }
    [Game] public class ProductType : IComponent { public ProductTypeId Value; }
    [Game] public class ProductMass : IComponent { public float Value; }
    [Game] public class ProductPhysicsConfigured : IComponent { }
    [Game] public class UnitPrice : IComponent { public int Value; }
}
