using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class StorePreparing : IComponent { }
    [Game] public class StoreOpen : IComponent { }
    [Game] public class StoreClosing : IComponent { }
    [Game] public class DayReportOpen : IComponent { }
    [Game] public class StoreControlTerminal : IComponent { }
    [Game] public class DayNumber : IComponent { public int Value; }
    [Game] public class CurrentDayMinute : IComponent { public float Value; }
    [Game] public class DayOpeningBalance : IComponent { public int Value; }
    [Game] public class DayRevenue : IComponent { public int Value; }
    [Game] public class DayProcurementExpenses : IComponent { public int Value; }
    [Game] public class DayUpgradeExpenses : IComponent { public int Value; }
    [Game] public class DayPayrollExpenses : IComponent { public int Value; }
    [Game] public class DayCompletedOrderCount : IComponent { public int Value; }
    [Game] public class StoreControlTerminalEntityId : IComponent { public int Value; }
    [Game] public class DayReportStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
}
