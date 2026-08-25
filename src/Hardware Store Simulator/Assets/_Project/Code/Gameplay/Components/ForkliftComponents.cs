using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class Forklift : IComponent { }
    [Game] public class DrivingForklift : IComponent { }
    [Game] public class FreightTruck : IComponent { }
    [Game] public class FreightStagingZone : IComponent { }
    [Game] public class PalletBay : IComponent { }
    [Game] public class Pallet : IComponent { }
    [Game] public class InboundPallet : IComponent { }
    [Game] public class ForkliftStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class ForkliftDriverEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class FreightTruckStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class FreightStagingZoneStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class PalletStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class PalletBayEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class ForkliftCarrierEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class PalletBaySlotIndex : IComponent { public int Value; }
    [Game] public class OccupiedPalletSlotCount : IComponent { public int Value; }
    [Game] public class ForkliftForkHeight : IComponent { public float Value; }
    [Game] public class ForkliftMinForkHeight : IComponent { public float Value; }
    [Game] public class ForkliftMaxForkHeight : IComponent { public float Value; }
    [Game] public class ForkliftLiftSpeed : IComponent { public float Value; }
    [Game] public class ForkliftForwardSpeed : IComponent { public float Value; }
    [Game] public class ForkliftReverseSpeed : IComponent { public float Value; }
    [Game] public class ForkliftSteeringSpeed : IComponent { public float Value; }
    [Game] public class DriverSeatAnchorComponent : IComponent { public Transform Value; }
    [Game] public class DriverExitAnchorComponent : IComponent { public Transform Value; }
    [Game] public class LiftTransformComponent : IComponent { public Transform Value; }
    [Game] public class CargoAnchorComponent : IComponent { public Transform Value; }
}
