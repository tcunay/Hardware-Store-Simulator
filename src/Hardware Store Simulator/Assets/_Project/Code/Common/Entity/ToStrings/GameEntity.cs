using System.Linq;
using Entitas;
using HardwareStore.Common.Entity.ToStrings;
using HardwareStore.Gameplay.Components;

// Generated GameEntity is intentionally extended outside Assets/**/Generated.
public sealed partial class GameEntity : INamedEntity
{
    private EntityPrinter _printer;

    public override string ToString()
    {
        _printer ??= new EntityPrinter(this);
        _printer.InvalidateCache();
        return _printer.BuildToString();
    }

    public string EntityName(IComponent[] components)
    {
        string role = ResolveRole(components);
        string id = hasEntityId ? $"  #{EntityId}" : string.Empty;
        string details = ResolveDetails();
        return string.IsNullOrEmpty(details)
            ? $"{role}{id}"
            : $"{role}{id}  ·  {details}";
    }

    public string BaseToString() => base.ToString();

    private string ResolveRole(IComponent[] components)
    {
        if (isPlayer) return "★ PLAYER";
        if (isStore) return "◆ STORE";
        if (isPlatformTrolley) return "▣ PLATFORM TROLLEY";
        if (isCustomerVisit) return "● Customer Visit";
        if (isCustomer) return "● Customer";
        if (isDelivery) return "▶ Delivery";
        if (isProduct) return "▪ Product";
        if (isProcurementTerminal) return "◇ Procurement Terminal";
        if (isStorageZone) return "◇ Storage Zone";
        if (isOrderCounter) return "◇ Order Counter";
        if (isTrolleyUpgradeTerminal) return "◇ Trolley Upgrade Terminal";
        if (isStoreControlTerminal) return "◇ Store Control Terminal";
        if (isConsultationOffer) return "○ Consultation Offer";
        if (isConsultationOfferLine) return "· Consultation Offer Line";
        if (isOrderLine) return "· Order Line";
        if (isInteractionRequest) return "→ Interaction Request";

        IComponent identity = components.FirstOrDefault(component =>
            component is not HardwareStore.Gameplay.Components.EntityId);
        return identity == null ? "· Game Entity" : $"· {identity.GetType().Name}";
    }

    private string ResolveDetails()
    {
        if (hasProductType)
            return ProductType.ToString();
        if (hasCustomerProjectType)
            return CustomerProjectType.ToString();
        if (hasSceneViewKey)
            return SceneViewKey.ToString();
        if (hasOfferIndex)
            return $"Offer {OfferIndex}";
        if (hasLineIndex)
            return $"Line {LineIndex}";

        return string.Empty;
    }
}
