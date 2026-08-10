using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class ConsultationOffer : IComponent { }
    [Game] public class ConsultationVisitEntityId : IComponent { public int Value; }
    [Game] public class OfferIndex : IComponent { public int Value; }
    [Game] public class OfferTitle : IComponent { public string Value; }
    [Game] public class OfferDescription : IComponent { public string Value; }
    [Game] public class ExpectedProfit : IComponent { public int Value; }
    [Game] public class SelectedConsultationOffer : IComponent { }
}
