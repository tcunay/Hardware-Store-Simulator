using System.Collections.Generic;
using Entitas;

namespace HardwareStore.Gameplay.Features.Products.Systems
{
    public sealed class ApplyProductMassSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _products;
        private readonly List<GameEntity> _buffer = new(32);

        public ApplyProductMassSystem(GameContext gameContext) =>
            _products = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.ProductMass,
                    GameMatcher.Rigidbody)
                .NoneOf(GameMatcher.ProductMassApplied));

        public void Execute()
        {
            foreach (GameEntity product in _products.GetEntities(_buffer))
            {
                product.Rigidbody.mass = product.ProductMass;
                product.isProductMassApplied = true;
            }
        }
    }
}
