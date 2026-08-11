using Entitas;
using HardwareStore.Gameplay.Common.Physics;

namespace HardwareStore.Gameplay.Features.Interaction.Systems
{
    public sealed class DetectFocusedInteractableSystem : IExecuteSystem
    {
        private const int MaxFocusCandidates = 64;

        private readonly GameContext _gameContext;
        private readonly IInteractionPhysicsService _physics;
        private readonly IGroup<GameEntity> _players;
        private readonly InteractionFocusCandidate[] _focusCandidates =
            new InteractionFocusCandidate[MaxFocusCandidates];

        public DetectFocusedInteractableSystem(GameContext gameContext, IInteractionPhysicsService physics)
        {
            _gameContext = gameContext;
            _physics = physics;
            _players = gameContext.GetGroup(GameMatcher.AllOf(
                GameMatcher.Player,
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.Camera,
                GameMatcher.InteractionDistance,
                GameMatcher.AimAssistRadius)
                .NoneOf(GameMatcher.ModalOpen, GameMatcher.PushingTrolley));
        }

        public void Execute()
        {
            foreach (GameEntity player in _players)
            {
                int candidateCount = _physics.GetFocusCandidates(player.Camera,
                    player.InteractionDistance, player.AimAssistRadius, _focusCandidates);
                if (TrySelectFocusedEntity(player, candidateCount, out int entityId))
                {
                    player.ReplaceFocusedEntityId(entityId);
                    continue;
                }

                if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
            }
        }

        private bool TrySelectFocusedEntity(GameEntity player, int candidateCount, out int entityId)
        {
            bool carryingProduct = player.isHandsOccupied && player.isCarryingProduct;
            int storageZoneEntityId = carryingProduct
                ? GetPlayerStorageZoneEntityId(player)
                : default;
            int bestPriority = int.MaxValue;
            bool bestIsDirect = false;
            float bestScore = float.PositiveInfinity;
            int bestEntityId = int.MaxValue;
            bool found = false;

            for (int i = 0; i < candidateCount; i++)
            {
                InteractionFocusCandidate candidate = _focusCandidates[i];
                GameEntity target = _gameContext.GetEntityWithEntityId(candidate.EntityId);
                if (!IsValidTarget(target))
                    continue;

                ConsiderCandidate(target, candidate, carryingProduct,
                    ref found, ref bestPriority, ref bestIsDirect, ref bestScore, ref bestEntityId);

                if (carryingProduct && target.isProduct && target.isInStock &&
                    target.hasStorageSlotIndex && target.hasStorageZoneEntityId &&
                    target.StorageZoneEntityId == storageZoneEntityId)
                {
                    GameEntity storageZone =
                        _gameContext.GetEntityWithEntityId(target.StorageZoneEntityId);
                    if (IsValidTarget(storageZone) && storageZone.isStorageZone)
                    {
                        InteractionFocusCandidate storageProxy = new(storageZone.EntityId,
                            candidate.Score, candidate.IsDirect);
                        ConsiderCandidate(storageZone, storageProxy, carryingProduct,
                            ref found, ref bestPriority, ref bestIsDirect,
                            ref bestScore, ref bestEntityId);
                    }
                }
            }

            entityId = bestEntityId;
            return found;
        }

        private int GetPlayerStorageZoneEntityId(GameEntity player)
        {
            GameEntity store = _gameContext.GetEntityWithEntityId(player.StoreEntityId);
            if (store == null || !store.isStore || !store.hasStorageZoneEntityId ||
                store.isDestructed)
            {
                throw new System.InvalidOperationException(
                    $"Player {player.EntityId} is linked to an invalid store.");
            }

            return store.StorageZoneEntityId;
        }

        private static void ConsiderCandidate(GameEntity target,
            InteractionFocusCandidate candidate, bool carryingProduct,
            ref bool found, ref int bestPriority, ref bool bestIsDirect,
            ref float bestScore, ref int bestEntityId)
        {
            int priority = GetContextPriority(target, carryingProduct);
            if (!IsBetterCandidate(priority, candidate, found, bestPriority,
                    bestIsDirect, bestScore, bestEntityId))
            {
                return;
            }

            found = true;
            bestPriority = priority;
            bestIsDirect = candidate.IsDirect;
            bestScore = candidate.Score;
            bestEntityId = candidate.EntityId;
        }

        private static bool IsBetterCandidate(int priority, InteractionFocusCandidate candidate,
            bool found, int bestPriority, bool bestIsDirect, float bestScore, int bestEntityId)
        {
            if (!found || priority != bestPriority)
                return !found || priority < bestPriority;
            if (candidate.IsDirect != bestIsDirect)
                return candidate.IsDirect;
            if (candidate.Score != bestScore)
                return candidate.Score < bestScore;

            return candidate.EntityId < bestEntityId;
        }

        private static int GetContextPriority(GameEntity target, bool carryingProduct)
        {
            bool isDropTarget = target.isStorageZone || target.isLoadingZone ||
                                target.isPlatformTrolley;
            if (carryingProduct)
            {
                if (isDropTarget)
                    return 0;
                return target.isProduct ? 2 : 1;
            }

            return target.isProduct ? 0 : isDropTarget ? 1 : 0;
        }

        private static bool IsValidTarget(GameEntity target) =>
            target != null && target.isInteractable && !target.isDestructed;
    }
}
