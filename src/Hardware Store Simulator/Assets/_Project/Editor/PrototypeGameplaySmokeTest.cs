using System;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Movement.Systems;
using HardwareStore.Gameplay.Features.Orders.Systems;
using HardwareStore.Gameplay.Features.Products.Systems;
using HardwareStore.Gameplay.Views;
using HardwareStore.Infrastructure.States.GameStates;
using HardwareStore.Infrastructure.States.StateMachine;
using HardwareStore.Infrastructure.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace HardwareStore.Editor
{
    public static class PrototypeGameplaySmokeTest
    {
        [MenuItem("Tools/Hardware Store/Prepare Carry Visual Check")]
        public static void PrepareCarryVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            RequireExactlyOnePlayer(runtime.Game);
            GameEntity order = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Order,
                GameMatcher.WalletEntityId,
                GameMatcher.RequiredProductCount,
                GameMatcher.OrderWaiting)), "order");
            GameEntity orderCounter = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.OrderEntityId,
                GameMatcher.OrderCounter)), "order counter");
            GameEntity player = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Player,
                GameMatcher.OrderEntityId,
                GameMatcher.CharacterController,
                GameMatcher.Transform,
                GameMatcher.View,
                GameMatcher.CarryAnchor)), "player");
            GameEntity product = FindProducts(runtime.Game).First();

            Require(order.isOrderWaiting, "The carry visual check must start with a fresh order.");
            RequestInteraction(player, orderCounter);
            runtime.Systems.Create<AcceptOrderSystem>().Execute();
            RequestInteraction(player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();

            ValidateRuntimePlayerView(player);
            CharacterController controller = player.CharacterController;
            controller.enabled = false;
            player.Transform.SetPositionAndRotation(
                new Vector3(0f, 0.02f, -4f), Quaternion.Euler(0f, 25f, 0f));
            controller.enabled = true;
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();

            Debug.Log("[Hardware Store] ECS carry visual check prepared. The held bag must follow the camera.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Require(runtime.StateMachine.ActiveStateType == typeof(StoreLoopState),
                $"The smoke test requires {nameof(StoreLoopState)}, but the active state is " +
                $"{runtime.StateMachine.ActiveStateType?.Name ?? "none"}.");

            RequireExactlyOnePlayer(runtime.Game);
            GameEntity player = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Player,
                GameMatcher.OrderEntityId,
                GameMatcher.CharacterController,
                GameMatcher.Transform,
                GameMatcher.View,
                GameMatcher.CarryAnchor,
                GameMatcher.MovementSpeed,
                GameMatcher.WalkSpeed,
                GameMatcher.SprintSpeed,
                GameMatcher.CarryingSpeed)), "player");
            GameEntity order = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Order,
                GameMatcher.WalletEntityId,
                GameMatcher.RequiredProductCount,
                GameMatcher.OrderReward,
                GameMatcher.LoadedProductCount,
                GameMatcher.OrderWaiting)), "order");
            GameEntity wallet = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Wallet,
                GameMatcher.Money)), "wallet");
            GameEntity orderCounter = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.OrderEntityId,
                GameMatcher.OrderCounter)), "order counter");
            GameEntity loadingZone = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.OrderEntityId,
                GameMatcher.LoadingZone,
                GameMatcher.LoadingSlots)), "loading zone");
            InputEntity input = RequireSingle(runtime.Input.GetGroup(InputMatcher.InputState), "input state");
            GameEntity[] products = FindProducts(runtime.Game);
            int requiredProductCount = order.RequiredProductCount;
            int initialMoney = wallet.Money;

            Require(products.Length > requiredProductCount,
                "The scene must contain enough cement bags plus one spare product.");
            ValidateRuntimePlayerView(player);
            Require(order.isOrderWaiting, "The smoke test must start with a fresh order.");
            Require(player.CarryingSpeed < player.WalkSpeed && player.WalkSpeed < player.SprintSpeed,
                "Player movement config must define carrying < walking < sprinting speeds.");

            GameEntity spare = products[requiredProductCount];
            ProductView spareView = spare.ProductView;
            spare.isProductPhysicsConfigured = false;
            spare.Rigidbody.mass = spare.ProductMass + 1f;
            runtime.Systems.Create<ApplyProductPhysicsConfigSystem>().Execute();
            Require(spare.isProductPhysicsConfigured,
                "The spare product was not marked as physics-configured.");
            Require(Mathf.Approximately(spare.Rigidbody.mass, spare.ProductMass),
                "The spare product did not receive its ECS-configured mass.");

            input.isSprintHeld = false;
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(player.MovementSpeed, player.WalkSpeed),
                "The player did not receive walking speed.");
            input.isSprintHeld = true;
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(player.MovementSpeed, player.SprintSpeed),
                "The player did not receive sprinting speed.");

            RequestInteraction(player, orderCounter);
            runtime.Systems.Create<AcceptOrderSystem>().Execute();
            runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
            Require(order.isOrderActive, "The order was not accepted.");

            RequestInteraction(player, products[0]);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
            Require(player.hasHeldProductId, "The first cement bag was not picked up.");
            ProductView firstView = products[0].ProductView;
            Rigidbody carriedBody = firstView.GetComponent<Rigidbody>();
            Require(carriedBody.isKinematic && !carriedBody.detectCollisions &&
                    carriedBody.interpolation == RigidbodyInterpolation.None,
                "The carried bag is still controlled by interpolated physics.");
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(player.MovementSpeed, player.CarryingSpeed),
                "Carrying a bag did not reduce the player's movement speed.");

            Vector3 originalPlayerPosition = player.Transform.position;
            CharacterController controller = player.CharacterController;
            controller.enabled = false;
            player.Transform.position += new Vector3(2f, 0f, 1f);
            controller.enabled = true;
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();
            Require(Vector3.Distance(firstView.transform.position, player.CarryAnchor.position) < 0.001f,
                "The carried bag did not follow the moving player.");
            controller.enabled = false;
            player.Transform.position = originalPlayerPosition;
            controller.enabled = true;
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();

            input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            input.isDropPressed = false;
            Require(!player.hasHeldProductId && !products[0].isCarried,
                "The dropped bag is still assigned to the player.");
            Require(!carriedBody.isKinematic && carriedBody.detectCollisions,
                "The dropped bag did not return to dynamic physics.");

            for (int i = 0; i < requiredProductCount; i++)
            {
                GameEntity product = products[i];
                RequestInteraction(player, product);
                runtime.Systems.Create<PickUpProductSystem>().Execute();
                runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
                Require(player.hasHeldProductId, $"{product.ProductView.name} was not picked up.");

                RequestInteraction(player, loadingZone);
                runtime.Systems.Create<LoadHeldProductSystem>().Execute();
                runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
                runtime.Systems.Create<CompleteOrderSystem>().Execute();
                runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
                runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
            }

            Require(order.isOrderCompleted, "The order did not complete.");
            Require(order.LoadedProductCount == requiredProductCount,
                "The loaded bag counter is incorrect.");
            Require(wallet.Money == initialMoney + order.OrderReward,
                "The order reward is incorrect.");
            Require(!player.hasHeldProductId, "The player's hands remained occupied after loading.");
            Require(!products[requiredProductCount].isLoaded, "The spare bag was loaded after order completion.");
            Require(products.Take(requiredProductCount)
                    .Select(product => product.ProductView.transform.parent)
                    .Distinct().Count() == requiredProductCount,
                "Loaded bags were not snapped to unique truck slots.");

            int rewardedMoney = wallet.Money;
            runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
            Require(wallet.Money == rewardedMoney, "The completed order reward was paid more than once.");

            spare.isDestructed = true;
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(!spareView.HasEntity, "The destructed spare product view is still bound to its ECS entity.");
            Require(!spare.isEnabled, "The destructed spare product entity is still enabled.");

            Debug.Log($"[Hardware Store] ECS gameplay smoke test passed: order accepted, bag dropped and recovered, " +
                      $"{requiredProductCount} bags loaded into unique slots, {order.OrderReward:N0} ₽ paid exactly once.");
        }

        private static Runtime ResolveRuntime()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before running the gameplay smoke test.");

            ProjectContext[] projectContexts = Resources.FindObjectsOfTypeAll<ProjectContext>()
                .Where(context => context.gameObject.scene.IsValid())
                .ToArray();
            Require(projectContexts.Length == 1,
                $"Expected exactly one runtime ProjectContext, found {projectContexts.Length}.");
            DiContainer container = projectContexts[0].Container;
            Require(container != null,
                "The runtime ProjectContext container is not initialized yet.");
            return new Runtime(container.Resolve<GameContext>(),
                container.Resolve<InputContext>(), container.Resolve<ISystemFactory>(),
                container.Resolve<IGameStateMachine>());
        }

        private static GameEntity[] FindProducts(GameContext context) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Product,
                    GameMatcher.ProductType,
                    GameMatcher.ProductView,
                    GameMatcher.ProductMass,
                    GameMatcher.View,
                    GameMatcher.Rigidbody))
                .GetEntities()
                .OrderBy(product => product.ProductView.name)
                .ToArray();

        private static void RequireExactlyOnePlayer(GameContext context)
        {
            int playerCount = context.GetGroup(GameMatcher.Player).GetEntities().Length;
            Require(playerCount == 1, $"Expected exactly one Player entity, found {playerCount}.");
        }

        private static void ValidateRuntimePlayerView(GameEntity player)
        {
            Require(player.Transform == player.View.gameObject.transform,
                "The player's Transform component does not reference its runtime view root.");
            Require(player.View.gameObject.scene == SceneManager.GetActiveScene(),
                "The runtime player view was not instantiated in the active gameplay scene.");
        }

        private static GameEntity RequireSingle(IGroup<GameEntity> group, string role)
        {
            GameEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException($"Expected exactly one {role}, found {entities.Length}.");
        }

        private static InputEntity RequireSingle(IGroup<InputEntity> group, string role)
        {
            InputEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException($"Expected exactly one {role}, found {entities.Length}.");
        }

        private static void RequestInteraction(GameEntity player, GameEntity target)
        {
            GameEntity request = CreateEntity.Empty();
            request.isInteractionRequest = true;
            request.AddSourceEntityId(player.EntityId);
            request.AddTargetEntityId(target.EntityId);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct Runtime
        {
            public Runtime(GameContext game, InputContext input, ISystemFactory systems,
                IGameStateMachine stateMachine)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
        }
    }
}
