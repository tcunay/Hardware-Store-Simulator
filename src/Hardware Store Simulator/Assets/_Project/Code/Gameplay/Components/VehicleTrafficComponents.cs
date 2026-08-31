using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class VehicleTrafficControlled : IComponent { }
    [Game] public class VehicleTrafficReady : IComponent { }
    [Game] public class VehicleTrafficSpawnPending : IComponent { }
    [Game] public class VehicleTrafficMoving : IComponent { }
    [Game] public class VehicleTrafficRuntimeId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class VehicleTrafficCommandSequence : IComponent { public int Value; }
}
