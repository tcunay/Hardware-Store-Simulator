using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ProductFactory : IProductFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ProductFactory(IIdentifierService identifiers, IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity Create(EntityBehaviour view)
        {
            int entityId = _identifiers.Next();
            ProductConfig config = _staticData.Product;
            GameEntity entity = CreateEntity.Empty(entityId)
                .AddProductType(config.ProductType)
                .AddUnitPrice(config.UnitPrice)
                .AddProductMass(config.Mass)
                .With(x => x.isProduct = true)
                .With(x => x.isInteractable = true);

            view.SetEntity(entity);
            return entity;
        }
    }
}
