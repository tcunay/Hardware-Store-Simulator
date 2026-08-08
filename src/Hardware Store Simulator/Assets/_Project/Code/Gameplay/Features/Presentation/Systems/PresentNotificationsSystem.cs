using System.Collections.Generic;
using Entitas;
using HardwareStore.Gameplay.Presentation;

namespace HardwareStore.Gameplay.Features.Presentation.Systems
{
    public sealed class PresentNotificationsSystem : IExecuteSystem
    {
        private readonly INotificationService _notifications;
        private readonly IGroup<GameEntity> _notificationEvents;
        private readonly List<GameEntity> _buffer = new(8);

        public PresentNotificationsSystem(GameContext gameContext, INotificationService notifications)
        {
            _notifications = notifications;
            _notificationEvents = gameContext.GetGroup(GameMatcher.AllOf(GameMatcher.NotificationMessage));
        }

        public void Execute()
        {
            foreach (GameEntity notificationEvent in _notificationEvents.GetEntities(_buffer))
            {
                _notifications.Show(notificationEvent.NotificationMessage);
                notificationEvent.Destroy();
            }
        }
    }
}
