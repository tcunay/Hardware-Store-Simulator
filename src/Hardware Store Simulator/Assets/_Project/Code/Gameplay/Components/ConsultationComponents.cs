using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class ConsultationOffer : IComponent { }
    [Game] public class ConsultationOfferLine : IComponent { }
    [Game] public class ConsultationVisitEntityId : IComponent { public int Value; }
    [Game] public class ConsultationOfferVisitEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class ConsultationOfferEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class OfferIndex : IComponent { public int Value; }
    [Game] public class ExpectedProfit : IComponent { public int Value; }
    [Game] public class SelectedConsultationOffer : IComponent { }
}
