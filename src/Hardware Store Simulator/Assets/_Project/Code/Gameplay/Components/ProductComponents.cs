using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Product : IComponent { }
    [Game] public class InboundProduct : IComponent { }
    [Game] public class InStock : IComponent { }
    [Game] public class CarrierEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class LooseProduct : IComponent { }
    [Game] public class Loaded : IComponent { }
    [Game] public class ProductLoaded : IComponent { }
    [Game] public class ProductStocked : IComponent { }
    [Game] public class DeliverySlotIndex : IComponent { public int Value; }
    [Game] public class StorageSlotIndex : IComponent { public int Value; }
    [Game] public class LoadingSlotIndex : IComponent { public int Value; }
    [Game] public class ReservedDeliverySlotIndex : IComponent { public int Value; }
    [Game] public class ReservedStorageSlotIndex : IComponent { public int Value; }
    [Game] public class ReservedOrderLineEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class ProductType : IComponent { public ProductTypeId Value; }
    [Game] public class ProductMass : IComponent { public float Value; }
    [Game] public class CarryMovementSpeed : IComponent { public float Value; }
    [Game] public class HeldRotationOffset : IComponent { public Quaternion Value; }
    [Game] public class DropForwardDistance : IComponent { public float Value; }
    [Game] public class ProductDropCollisionRadius : IComponent { public float Value; }
    [Game] public class RigidbodyInterpolationMode : IComponent { public RigidbodyInterpolation Value; }
    [Game] public class RigidbodyCollisionDetectionMode : IComponent { public CollisionDetectionMode Value; }
    [Game] public class ProductMassApplied : IComponent { }
    [Game] public class ProductPlacementDirty : IComponent { }
    [Game] public class UnitPrice : IComponent { public int Value; }
}
