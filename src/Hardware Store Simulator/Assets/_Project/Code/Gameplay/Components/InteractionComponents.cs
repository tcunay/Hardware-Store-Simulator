using Entitas;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class OrderCounter : IComponent { }
    [Game] public class LoadingZone : IComponent { }
    [Game] public class Interactable : IComponent { }
    [Game] public class InteractionRequest : IComponent { }
    [Game] public class Highlighted : IComponent { }
    [Game] public class FocusInteractionAvailable : IComponent { }
    [Game] public class FocusedInteractionType : IComponent { public InteractionTypeId Value; }
    [Game] public class InteractionViewComponent : IComponent { public HardwareStore.Gameplay.Views.InteractionView Value; }
    [Game] public class SlotsComponent : IComponent { public UnityEngine.Transform[] Value; }
    [Game] public class InteractionDistance : IComponent { public float Value; }
    [Game] public class AimAssistRadius : IComponent { public float Value; }
    [Game] public class FocusedEntityId : IComponent { public int Value; }
    [Game] public class SourceEntityId : IComponent { public int Value; }
    [Game] public class TargetEntityId : IComponent { public int Value; }
    [Game] public class InteractionPrompt : IComponent { public string Value; }
}
