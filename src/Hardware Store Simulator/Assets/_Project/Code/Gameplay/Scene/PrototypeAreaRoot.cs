using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public enum PrototypeAreaId
    {
        SiteShell = 0,
        Storefront = 1,
        Warehouse = 2,
        Lumber = 3,
        InboundDelivery = 4,
        CustomerTraffic = 5,
        Freight = 6
    }

    /// <summary>
    /// Authoring marker for a self-contained, manually movable prototype-yard area.
    /// Gameplay systems consume the typed child markers, not this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeAreaRoot : MonoBehaviour
    {
        [SerializeField] private PrototypeAreaId _id;

        public PrototypeAreaId Id => _id;

        public void Configure(PrototypeAreaId id) =>
            _id = id;
    }
}
