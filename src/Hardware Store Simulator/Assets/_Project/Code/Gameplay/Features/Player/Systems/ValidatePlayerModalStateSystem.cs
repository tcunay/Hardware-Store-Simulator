using System;
using Entitas;

namespace HardwareStore.Gameplay.Features.Player.Systems
{
    public sealed class ValidatePlayerModalStateSystem : IExecuteSystem
    {
        private readonly IGroup<GameEntity> _players;

        public ValidatePlayerModalStateSystem(GameContext gameContext) =>
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId));

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                int modalRelationCount =
                    (player.hasConsultationVisitEntityId ? 1 : 0) +
                    (player.hasProcurementTerminalEntityId ? 1 : 0) +
                    (player.hasDayReportStoreEntityId ? 1 : 0);
                int expectedRelationCount = player.isModalOpen ? 1 : 0;
                if (modalRelationCount != expectedRelationCount)
                {
                    throw new InvalidOperationException(
                        $"Player {player.EntityId} modal state expects " +
                        $"{expectedRelationCount} relation, but has {modalRelationCount}.");
                }
            }
        }
    }
}
