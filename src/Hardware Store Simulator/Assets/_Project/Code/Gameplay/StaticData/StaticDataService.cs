using System;
using HardwareStore.Gameplay.Configs;
using UnityEngine;

namespace HardwareStore.Gameplay.StaticData
{
    public sealed class StaticDataService : IStaticDataService
    {
        private const string ConfigRoot = "Configs/";

        public PlayerConfig Player { get; private set; }
        public InteractionConfig Interaction { get; private set; }
        public OrderConfig Order { get; private set; }
        public ProductConfig Product { get; private set; }

        public void LoadAll()
        {
            Player = Load<PlayerConfig>(nameof(PlayerConfig));
            Interaction = Load<InteractionConfig>(nameof(InteractionConfig));
            Order = Load<OrderConfig>(nameof(OrderConfig));
            Product = Load<ProductConfig>(nameof(ProductConfig));

            if (Player.ViewPrefab == null)
                throw new InvalidOperationException(
                    $"Required {nameof(PlayerConfig)} must reference an {nameof(PlayerConfig.ViewPrefab)}.");
        }

        private static TConfig Load<TConfig>(string assetName) where TConfig : ScriptableObject =>
            Resources.Load<TConfig>(ConfigRoot + assetName) ??
            throw new InvalidOperationException(
                $"Required config '{ConfigRoot}{assetName}' of type {typeof(TConfig).Name} was not found in Resources.");
    }
}
