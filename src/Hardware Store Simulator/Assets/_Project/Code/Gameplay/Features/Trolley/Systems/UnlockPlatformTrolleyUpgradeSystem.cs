using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.StaticData;

namespace HardwareStore.Gameplay.Features.Trolley.Systems
{
    public sealed class UnlockPlatformTrolleyUpgradeSystem : IExecuteSystem
    {
        private readonly PlatformTrolleyConfig _config;
        private readonly IGameEventFactory _events;
        private readonly IGroup<GameEntity> _stores;
        private readonly List<GameEntity> _buffer = new(4);

        public UnlockPlatformTrolleyUpgradeSystem(GameContext gameContext,
            IStaticDataService staticData, IGameEventFactory events)
        {
            _config = staticData.PlatformTrolley;
            _events = events;
            _stores = gameContext.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Store,
                    GameMatcher.EntityId,
                    GameMatcher.CompletedOrderCount,
                    GameMatcher.TrolleyUpgradeTerminalEntityId)
                .NoneOf(
                    GameMatcher.TrolleyUpgradeUnlocked,
                    GameMatcher.Destructed));
        }

        public void Execute()
        {
            foreach (GameEntity store in _stores.GetEntities(_buffer))
            {
                if (store.CompletedOrderCount < _config.RequiredCompletedOrderCount)
                    continue;

                store.isTrolleyUpgradeUnlocked = true;
                _events.EmitNotification(LocalizedTexts.Text(
                    LocalizationKey.NotificationTrolleyUnlocked,
                    _config.PurchasePrice));
            }
        }
    }
}
