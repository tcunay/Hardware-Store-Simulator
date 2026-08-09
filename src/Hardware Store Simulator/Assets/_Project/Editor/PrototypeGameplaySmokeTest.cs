using System;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Delivery.Systems;
using HardwareStore.Gameplay.Features.Interaction;
using HardwareStore.Gameplay.Features.Interaction.Systems;
using HardwareStore.Gameplay.Features.Movement.Systems;
using HardwareStore.Gameplay.Features.Orders.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Features.Products;
using HardwareStore.Gameplay.Features.StorageState;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.States.GameStates;
using HardwareStore.Infrastructure.States.StateMachine;
using HardwareStore.Infrastructure.Systems;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace HardwareStore.Editor
{
    public static class PrototypeGameplaySmokeTest
    {
        [MenuItem("Tools/Hardware Store/Prepare Supply Chain Visual Check")]
        public static void PrepareSupplyChainVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            DeliveryArrival arrival = PurchaseAndPrepareArrival(runtime, scenario);

            Debug.Log(
                $"[Hardware Store] Supply-chain visual check prepared: delivery {arrival.Delivery.EntityId} " +
                $"and {arrival.Products.Length} runtime cement bags are waiting in the delivery vehicle.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int initialMoney = scenario.Store.Money;
            int deliveryCount = runtime.StaticData.Delivery.ProductCount;
            int deliveryCost = runtime.StaticData.Delivery.TotalCost;
            int requiredProductCount = scenario.Order.RequiredProductCount;

            ValidateRuntimePlayerView(scenario.Player);
            Require(scenario.Player.CarryingSpeed < scenario.Player.WalkSpeed &&
                    scenario.Player.WalkSpeed < scenario.Player.SprintSpeed,
                "Player movement config must define carrying < walking < sprinting speeds.");
            Require(deliveryCount > requiredProductCount,
                "The delivery must leave at least one product in stock after the customer order.");
            Require(scenario.StorageZone.Slots.Length >= deliveryCount,
                "The storage zone does not have enough slots for the configured delivery.");
            Require(scenario.LoadingZone.Slots.Length >= requiredProductCount,
                "The customer loading zone does not have enough slots for the order.");

            ValidateInsufficientFunds(runtime, scenario);
            ValidateStorageCapacityRejection(runtime, scenario);
            ValidateForeignStorageDoesNotUnlockOrder(runtime, scenario);
            AttemptOrderAcceptance(runtime, scenario);
            Require(scenario.Order.isOrderWaiting,
                "The customer order was accepted while the store had no stock.");

            DeliveryArrival arrival = PurchaseAndPrepareArrival(runtime, scenario);
            GameEntity delivery = arrival.Delivery;
            EntityBehaviour deliveryView = arrival.DeliveryView;
            GameEntity[] products = arrival.Products;

            Require(scenario.Store.Money == initialMoney - deliveryCost,
                "The delivery purchase did not deduct its cost exactly once.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(scenario.Order.isOrderWaiting,
                "The customer order was accepted before any delivered product entered stock.");

            TestCarryDropAndRepick(runtime, scenario, products[0]);
            StoreCompleteDelivery(runtime, scenario, delivery, products);

            Require(!scenario.ProcurementTerminal.hasDeliveryEntityId,
                "The procurement terminal still references the completed delivery.");
            Require(delivery.isDeliveryCompleted && delivery.isDestructed,
                "The fully stocked delivery was not completed and scheduled for cleanup.");

            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(!deliveryView.HasEntity,
                "The completed delivery view is still bound to its ECS entity.");
            Require(!delivery.isEnabled,
                "The completed delivery entity survived the destructed pipeline.");
            Require(runtime.Game.GetGroup(GameMatcher.Delivery).GetEntities().Length == 0,
                "A delivery entity remained after truck cleanup.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(scenario.Order.isOrderActive,
                "The customer order was not accepted after the delivery entered stock.");

            GameEntity[] outboundProducts = products.Take(requiredProductCount).ToArray();
            TestStockCarryDropAndRepick(runtime, scenario, outboundProducts[0]);
            LoadCustomerOrder(runtime, scenario, outboundProducts);

            Require(scenario.Order.isOrderCompleted,
                "The customer order did not complete.");
            Require(scenario.Order.LoadedProductCount == requiredProductCount,
                "The loaded product counter is incorrect.");
            Require(scenario.Store.Money == initialMoney - deliveryCost + scenario.Order.OrderReward,
                "The final balance does not equal initial money minus delivery cost plus order reward.");
            Require(!scenario.Player.hasHeldProductId,
                "The player's hands remained occupied after the order was loaded.");

            int finalStock = products.Count(product => product.isInStock);
            Require(finalStock == deliveryCount - requiredProductCount,
                "The remaining stock does not equal delivery count minus the fulfilled order.");
            Require(products.All(product => product.isEnabled &&
                    product.hasView && ((EntityBehaviour)product.View).HasEntity),
                "A delivered product or its runtime view was destroyed unexpectedly.");
            Require(products.All(product =>
                    product.View.gameObject.scene == SceneManager.GetActiveScene()),
                "A delivered product view is outside the active gameplay scene.");

            int rewardedMoney = scenario.Store.Money;
            runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Store.Money == rewardedMoney,
                "The completed order reward was paid more than once.");

            Debug.Log(
                $"[Hardware Store] P0 supply-chain smoke test passed: {deliveryCount} products purchased and stocked, " +
                $"{requiredProductCount} loaded into unique customer slots, final stock {finalStock}, " +
                $"balance {scenario.Store.Money:N0} ₽.");
        }

        private static Scenario ResolveFreshScenario(Runtime runtime)
        {
            Require(runtime.StateMachine.ActiveStateType == typeof(StoreLoopState),
                $"The smoke test requires {nameof(StoreLoopState)}, but the active state is " +
                $"{runtime.StateMachine.ActiveStateType?.Name ?? "none"}.");
            RequireExactlyOnePlayer(runtime.Game);
            ExecuteStorageState(runtime);

            GameEntity player = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.CharacterController,
                GameMatcher.Transform,
                GameMatcher.View,
                GameMatcher.CarryAnchor,
                GameMatcher.MovementSpeed,
                GameMatcher.WalkSpeed,
                GameMatcher.SprintSpeed,
                GameMatcher.CarryingSpeed)), "player");
            GameEntity store = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Store,
                GameMatcher.Money,
                GameMatcher.OrderEntityId,
                GameMatcher.ProcurementTerminalEntityId,
                GameMatcher.StorageZoneEntityId)), "store");
            GameEntity order = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Order,
                GameMatcher.StoreEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.RequiredProductType,
                GameMatcher.RequiredProductCount,
                GameMatcher.AvailableProductCount,
                GameMatcher.OrderReward,
                GameMatcher.LoadedProductCount,
                GameMatcher.OrderWaiting)), "order");
            GameEntity orderCounter = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.OrderEntityId,
                GameMatcher.OrderCounter)), "order counter");
            GameEntity loadingZone = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.OrderEntityId,
                GameMatcher.LoadingZone,
                GameMatcher.Slots)), "customer loading zone");
            GameEntity procurementTerminal = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.ProcurementTerminal,
                GameMatcher.StoreEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ProductType,
                GameMatcher.DeliveryProductCount,
                GameMatcher.DeliveryCost,
                GameMatcher.SpawnPosition,
                GameMatcher.SpawnRotation)), "procurement terminal");
            GameEntity storageZone = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.StorageZone,
                GameMatcher.OccupiedStorageSlotCount,
                GameMatcher.Slots)), "storage zone");
            InputEntity input = RequireSingle(runtime.Input.GetGroup(InputMatcher.InputState), "input state");

            Require(player.StoreEntityId == store.EntityId,
                "The player does not reference the scenario store.");
            Require(store.OrderEntityId == order.EntityId &&
                    orderCounter.OrderEntityId == order.EntityId &&
                    loadingZone.OrderEntityId == order.EntityId,
                "The store, order counter and customer loading zone do not reference the same order.");
            Require(store.ProcurementTerminalEntityId == procurementTerminal.EntityId &&
                    procurementTerminal.StoreEntityId == store.EntityId,
                "The store and procurement terminal do not reference each other correctly.");
            Require(store.StorageZoneEntityId == storageZone.EntityId &&
                    procurementTerminal.StorageZoneEntityId == storageZone.EntityId &&
                    order.StorageZoneEntityId == storageZone.EntityId,
                "The store graph does not reference one storage zone.");
            Require(order.StoreEntityId == store.EntityId,
                "The order does not reference the scenario store.");
            Require(!procurementTerminal.hasDeliveryEntityId,
                "The smoke test must start without an active delivery.");
            Require(FindProducts(runtime.Game).Length == 0,
                "The smoke test must start with zero products; all stock must arrive at runtime.");
            Require(runtime.Game.GetGroup(GameMatcher.Delivery).GetEntities().Length == 0,
                "The smoke test must start without a delivery entity.");
            Require(order.isOrderWaiting && order.LoadedProductCount == 0 &&
                    order.AvailableProductCount == 0,
                "The smoke test must start with a fresh customer order.");
            Require(storageZone.OccupiedStorageSlotCount == 0,
                "The smoke test must start with an empty storage occupancy snapshot.");
            Require(procurementTerminal.DeliveryProductCount == runtime.StaticData.Delivery.ProductCount &&
                    procurementTerminal.DeliveryCost == runtime.StaticData.Delivery.TotalCost &&
                    procurementTerminal.ProductType == runtime.StaticData.Delivery.ProductType,
                "The procurement terminal does not match DeliveryConfig.");
            Require(store.Money == runtime.StaticData.Economy.InitialMoney,
                "The store does not contain EconomyConfig.InitialMoney.");

            return new Scenario(player, store, order, orderCounter, loadingZone,
                procurementTerminal, storageZone, input);
        }

        private static void ValidateInsufficientFunds(Runtime runtime, Scenario scenario)
        {
            int originalMoney = scenario.Store.Money;
            int deliveryCost = scenario.ProcurementTerminal.DeliveryCost;
            Require(deliveryCost > 0,
                "Delivery cost must be positive to test the insufficient-funds path.");

            int insufficientMoney = deliveryCost - 1;
            scenario.Store.ReplaceMoney(insufficientMoney);
            ExecuteStorageState(runtime);
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            CleanupEvents(runtime);

            Require(scenario.Store.Money == insufficientMoney,
                "An unaffordable delivery changed the store balance.");
            Require(!scenario.ProcurementTerminal.hasDeliveryEntityId,
                "An unaffordable delivery created a terminal relation.");
            Require(runtime.Game.GetGroup(GameMatcher.Delivery).GetEntities().Length == 0,
                "An unaffordable delivery created a delivery entity.");
            scenario.Store.ReplaceMoney(originalMoney);
        }

        private static void ValidateStorageCapacityRejection(Runtime runtime, Scenario scenario)
        {
            int occupiedSlotCount = scenario.StorageZone.Slots.Length -
                                    scenario.ProcurementTerminal.DeliveryProductCount + 1;
            Require(occupiedSlotCount > 0,
                "The configured delivery must fit an empty storage zone for the capacity test.");

            GameEntity[] temporaryStock = CreateTemporaryStock(
                runtime,
                scenario.StorageZone,
                occupiedSlotCount,
                scenario.ProcurementTerminal.ProductType);
            ExecuteStorageState(runtime);
            int moneyBeforeRequest = scenario.Store.Money;
            Require(moneyBeforeRequest >= scenario.ProcurementTerminal.DeliveryCost,
                "The store must afford the delivery to isolate storage-capacity rejection.");
            Require(CountStockInZone(runtime.Game, scenario.StorageZone.EntityId) == occupiedSlotCount,
                "Temporary stock did not occupy the expected number of scenario storage slots.");
            Require(scenario.StorageZone.OccupiedStorageSlotCount == occupiedSlotCount,
                "Storage occupancy snapshot did not include temporary stock.");

            try
            {
                RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
                runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
                CleanupEvents(runtime);

                Require(scenario.Store.Money == moneyBeforeRequest,
                    "A delivery rejected by storage capacity changed the store balance.");
                Require(!scenario.ProcurementTerminal.hasDeliveryEntityId,
                    "A delivery rejected by storage capacity created a terminal relation.");
                Require(runtime.Game.GetGroup(GameMatcher.Delivery).GetEntities().Length == 0,
                    "A delivery rejected by storage capacity created a delivery entity.");
            }
            finally
            {
                DestroyTemporaryEntities(temporaryStock);
                ExecuteStorageState(runtime);
            }

            Require(CountStockInZone(runtime.Game, scenario.StorageZone.EntityId) == 0,
                "Temporary capacity-test stock survived before the real delivery flow.");
        }

        private static void ValidateForeignStorageDoesNotUnlockOrder(Runtime runtime, Scenario scenario)
        {
            GameEntity foreignStorageZone = CreateEntity.Empty(runtime.Identifiers.Next())
                .AddSlots(scenario.StorageZone.Slots.ToArray())
                .AddOccupiedStorageSlotCount(0);
            foreignStorageZone.isStorageZone = true;
            GameEntity[] foreignStock = CreateTemporaryStock(
                runtime,
                foreignStorageZone,
                scenario.Order.RequiredProductCount,
                scenario.Order.RequiredProductType);

            try
            {
                ExecuteStorageState(runtime);
                AttemptOrderAcceptance(runtime, scenario);
                Require(scenario.Order.isOrderWaiting && !scenario.Order.isOrderActive,
                    "Stock from another storage zone unlocked the scenario customer order.");
                Require(CountStockInZone(runtime.Game, foreignStorageZone.EntityId) ==
                        scenario.Order.RequiredProductCount,
                    "The foreign-storage rejection test did not expose the required stock count.");
                Require(CountStockInZone(runtime.Game, scenario.StorageZone.EntityId) == 0,
                    "The scenario storage unexpectedly contained stock during the foreign-stock test.");
            }
            finally
            {
                DestroyTemporaryEntities(foreignStock);
                if (foreignStorageZone.isEnabled)
                    foreignStorageZone.Destroy();
                ExecuteStorageState(runtime);
            }
        }

        private static GameEntity[] CreateTemporaryStock(Runtime runtime, GameEntity storageZone,
            int count, ProductTypeId productType)
        {
            Require(storageZone.isStorageZone && storageZone.hasSlots,
                "Temporary stock requires a configured storage zone.");
            Require(count > 0 && count <= storageZone.Slots.Length,
                "Temporary stock count must fit the target storage zone.");

            var products = new GameEntity[count];
            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                GameEntity product = CreateEntity.Empty(runtime.Identifiers.Next())
                    .AddProductType(productType)
                    .AddStorageZoneEntityId(storageZone.EntityId)
                    .AddStorageSlotIndex(slotIndex);
                product.isProduct = true;
                product.isInStock = true;
                products[slotIndex] = product;
            }

            return products;
        }

        private static void DestroyTemporaryEntities(GameEntity[] entities)
        {
            foreach (GameEntity entity in entities)
            {
                Require(!entity.hasView,
                    $"Temporary test entity {entity.EntityId} unexpectedly owns a runtime view.");
                if (entity.isEnabled)
                    entity.Destroy();
            }
        }

        private static int CountStockInZone(GameContext context, int storageZoneEntityId) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.Product,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId,
                    GameMatcher.StorageSlotIndex))
                .GetEntities()
                .Count(product => product.StorageZoneEntityId == storageZoneEntityId);

        private static DeliveryArrival PurchaseAndPrepareArrival(Runtime runtime, Scenario scenario)
        {
            ExecuteStorageState(runtime);
            int moneyBeforePurchase = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            CleanupEvents(runtime);

            Require(scenario.ProcurementTerminal.hasDeliveryEntityId,
                "The procurement terminal did not link the purchased delivery.");
            GameEntity delivery = runtime.Game.GetEntityWithEntityId(
                scenario.ProcurementTerminal.DeliveryEntityId);
            Require(delivery.isDelivery && delivery.isDeliveryActive,
                "The purchased entity is not an active delivery.");
            Require(delivery.StoreEntityId == scenario.Store.EntityId,
                "The purchased delivery does not reference the scenario store.");
            Require(delivery.DeliveryProductCount == runtime.StaticData.Delivery.ProductCount &&
                    delivery.DeliveryCost == runtime.StaticData.Delivery.TotalCost &&
                    delivery.ProductType == runtime.StaticData.Delivery.ProductType,
                "The delivery entity does not match DeliveryConfig.");
            Require(scenario.Store.Money == moneyBeforePurchase - delivery.DeliveryCost,
                "The delivery cost was not deducted from the store.");
            Require(!delivery.hasView,
                "The delivery view appeared before the generic view-binding system executed.");

            int moneyAfterPurchase = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Store.Money == moneyAfterPurchase,
                "A second interaction charged the active delivery twice.");
            Require(scenario.ProcurementTerminal.DeliveryEntityId == delivery.EntityId &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).GetEntities().Length == 1,
                "A second interaction created a duplicate delivery.");

            BindEntityViewFromPrefabSystem bindDeliveryView =
                runtime.Systems.Create<BindEntityViewFromPrefabSystem>();
            Require(typeof(IExecuteSystem).IsAssignableFrom(typeof(BindEntityViewFromPrefabSystem)) &&
                    !typeof(IInitializeSystem).IsAssignableFrom(typeof(BindEntityViewFromPrefabSystem)),
                $"{nameof(BindEntityViewFromPrefabSystem)} must be execute-only.");
            bindDeliveryView.Execute();
            EntityBehaviour deliveryView = RequireRuntimeView(
                delivery,
                runtime.StaticData.Delivery.ViewPrefab,
                "delivery vehicle");
            Require(delivery.hasSlots && delivery.Slots.Length >= delivery.DeliveryProductCount,
                "The delivery vehicle did not register enough cargo slots.");

            runtime.Systems.Create<SpawnDeliveryProductsSystem>().Execute();
            Require(delivery.isDeliveryProductsSpawned,
                "The delivery was not marked after spawning its products.");
            GameEntity[] productsBeforeBinding = FindProducts(runtime.Game);
            Require(productsBeforeBinding.Length == runtime.StaticData.Delivery.ProductCount,
                "The delivery did not create exactly DeliveryConfig.ProductCount products.");
            Require(productsBeforeBinding.All(product =>
                    product.isInboundProduct && product.hasDeliveryEntityId &&
                    product.DeliveryEntityId == delivery.EntityId && !product.hasView),
                "A spawned delivery product has an invalid inbound relation or a prematurely bound view.");

            runtime.Systems.Create<SpawnDeliveryProductsSystem>().Execute();
            Require(FindProducts(runtime.Game).Length == productsBeforeBinding.Length,
                "Delivery products were spawned more than once.");

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            ExecuteProductPlacement(runtime);
            GameEntity[] products = FindProducts(runtime.Game);
            foreach (GameEntity product in products)
            {
                EntityBehaviour productView = RequireRuntimeView(
                    product,
                    runtime.StaticData.Product.ViewPrefab,
                    $"product {product.EntityId}");
                Require(productView.GetType().Name == "InteractionView" &&
                        product.hasRigidbody && product.hasTransform && product.hasColliders,
                    $"Runtime product {product.EntityId} did not register its Unity references.");
                Require(product.isProductMassApplied &&
                        Mathf.Approximately(product.Rigidbody.mass, product.ProductMass),
                    $"Runtime product {product.EntityId} did not receive its ECS physics config.");
                Require(product.isInboundProduct && product.DeliveryEntityId == delivery.EntityId &&
                        product.hasDeliverySlotIndex &&
                        product.DeliverySlotIndex >= 0 &&
                        product.DeliverySlotIndex < delivery.Slots.Length &&
                        product.Transform.parent == delivery.Slots[product.DeliverySlotIndex] &&
                        !product.isProductPlacementDirty &&
                        !product.isCarried && !product.isInStock &&
                        !product.isLooseProduct && !product.isLoaded,
                    $"Runtime product {product.EntityId} has an invalid arrival state.");
                Require(product.Rigidbody.isKinematic && product.Rigidbody.detectCollisions &&
                        product.Colliders.All(collider => collider.enabled),
                    $"Inbound product {product.EntityId} is not interactive in its delivery slot.");
            }

            return new DeliveryArrival(delivery, deliveryView, products);
        }

        private static void TestCarryDropAndRepick(Runtime runtime, Scenario scenario, GameEntity product)
        {
            EntityBehaviour productView = (EntityBehaviour)product.View;
            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Player.hasHeldProductId &&
                    scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                "The first inbound product was not picked up.");

            Rigidbody body = product.Rigidbody;
            Require(body.isKinematic && !body.detectCollisions &&
                    body.interpolation == RigidbodyInterpolation.None &&
                    product.Colliders.All(collider => !collider.enabled) &&
                    !product.hasDeliverySlotIndex &&
                    !product.isLooseProduct && !product.hasWorldPosition && !product.hasWorldRotation &&
                    !product.isProductPlacementDirty,
                "The carried inbound product is still controlled by interpolated physics.");
            Require(product.Transform.position == scenario.Player.CarryAnchor.position,
                "The carried inbound product did not follow the ECS carry anchor.");

            scenario.Input.isSprintHeld = true;
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(scenario.Player.MovementSpeed, scenario.Player.CarryingSpeed),
                "Carrying an inbound product did not reduce movement speed.");

            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);
            Require(!scenario.Player.hasHeldProductId && !product.isCarried,
                "The dropped inbound product remained assigned to the player.");
            Require(product.isLooseProduct && product.hasWorldPosition && product.hasWorldRotation &&
                    !product.isProductPlacementDirty &&
                    !body.isKinematic && body.detectCollisions &&
                    product.Colliders.All(collider => collider.enabled),
                "The dropped inbound product did not return to dynamic physics.");

            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Player.hasHeldProductId &&
                    scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                "The dropped inbound product could not be picked up again.");
            Require(ReferenceEquals(product.View, productView) &&
                    !product.isLooseProduct && !product.hasWorldPosition && !product.hasWorldRotation,
                "Re-picking an inbound product replaced its runtime view.");
        }

        private static void TestStockCarryDropAndRepick(Runtime runtime, Scenario scenario,
            GameEntity product)
        {
            Require(scenario.Order.isOrderActive,
                "A stock product cannot be tested before the customer order is active.");
            Require(product.isInStock && product.hasStorageZoneEntityId && product.hasStorageSlotIndex &&
                    product.StorageZoneEntityId == scenario.StorageZone.EntityId,
                $"Stock product {product.EntityId} has no complete scenario storage relation.");

            EntityBehaviour productView = (EntityBehaviour)product.View;
            Rigidbody body = product.Rigidbody;
            PickUpStockThroughFocusPipeline(runtime, scenario, product);

            Require(scenario.Player.hasHeldProductId &&
                    scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                "The first stock product was not picked up after order acceptance.");
            Require(product.isInStock && product.hasStorageZoneEntityId &&
                    product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                    !product.hasStorageSlotIndex,
                "Picking up stock did not release only its storage slot while preserving ownership.");

            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);

            Require(!scenario.Player.hasHeldProductId && !product.isCarried,
                "The dropped stock product remained assigned to the player.");
            Require(product.isInStock && product.hasStorageZoneEntityId &&
                    product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                    !product.hasStorageSlotIndex,
                "Dropped stock lost its storage ownership or regained a storage slot.");
            Require(product.isLooseProduct && product.hasWorldPosition && product.hasWorldRotation &&
                    !product.isProductPlacementDirty &&
                    !body.isKinematic && body.detectCollisions &&
                    product.Colliders.All(collider => collider.enabled),
                "The dropped stock product did not return to dynamic physics.");

            scenario.Player.ReplaceFocusedEntityId(product.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable &&
                    scenario.Player.hasInteractionPrompt,
                "The dropped loose stock product is no longer a valid interaction target for its store.");
            scenario.Player.RemoveFocusedEntityId();

            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);

            Require(scenario.Player.hasHeldProductId &&
                    scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                "The dropped stock product could not be picked up again.");
            Require(product.isInStock && product.hasStorageZoneEntityId &&
                    product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                    !product.hasStorageSlotIndex,
                "Re-picking stock changed its loose in-stock ownership state.");
            Require(ReferenceEquals(product.View, productView) &&
                    !product.isLooseProduct && !product.hasWorldPosition && !product.hasWorldRotation,
                "Re-picking a stock product replaced its runtime view.");
        }

        private static void PickUpStockThroughFocusPipeline(Runtime runtime, Scenario scenario,
            GameEntity product)
        {
            GameEntity player = scenario.Player;
            Camera playerCamera = player.Camera;
            Transform cameraTransform = playerCamera.transform;
            Vector3 originalCameraPosition = cameraTransform.position;
            Quaternion originalCameraRotation = cameraTransform.rotation;
            bool hadFocusedEntity = player.hasFocusedEntityId;
            int originalFocusedEntityId = hadFocusedEntity ? player.FocusedEntityId : default;

            try
            {
                player.ReplaceFocusedEntityId(scenario.StorageZone.EntityId);
                ExecuteStorageState(runtime);
                ExecuteInteractionPrompts(runtime);
                Require(player.hasInteractionPrompt,
                    "The storage zone has no prompt after the completed delivery was accepted.");
                Require(player.InteractionPrompt.IndexOf(
                            "машины поставщика", StringComparison.OrdinalIgnoreCase) < 0,
                    "The completed-delivery storage prompt still asks for a bag from the supplier vehicle.");
                Require(player.InteractionPrompt.IndexOf(
                            "мешок", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The completed-delivery storage prompt does not direct the player to a stock bag.");

                Collider interactionCollider = product.Colliders.Single(collider => collider.isTrigger);
                Vector3 aimPoint = interactionCollider.bounds.center;
                Vector3 approachDirection = -scenario.StorageZone.InteractionView.transform.forward;
                Vector3 cameraPosition = aimPoint + approachDirection * 3f + Vector3.up;
                cameraTransform.SetPositionAndRotation(
                    cameraPosition,
                    Quaternion.LookRotation(aimPoint - cameraPosition, Vector3.up));
                Physics.SyncTransforms();

                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                Require(player.hasFocusedEntityId,
                    "A stocked product was not detected by the real focus pipeline.");
                Require(player.FocusedEntityId == product.EntityId,
                    $"The real focus pipeline selected entity {player.FocusedEntityId} instead of " +
                    $"stock product {product.EntityId}.");

                ExecuteInteractionPrompts(runtime);
                Require(player.isFocusInteractionAvailable,
                    "The focused stock product is not available for interaction after order acceptance.");
                Require(player.hasInteractionPrompt &&
                        player.InteractionPrompt == "E — взять мешок со склада",
                    $"The focused stock product has an unexpected prompt: " +
                    $"'{(player.hasInteractionPrompt ? player.InteractionPrompt : "none")}'.");

                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
                GameEntity request = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                    GameMatcher.InteractionRequest,
                    GameMatcher.SourceEntityId,
                    GameMatcher.TargetEntityId)), "focused stock interaction request");
                Require(request.SourceEntityId == player.EntityId &&
                        request.TargetEntityId == product.EntityId,
                    "The focus pipeline emitted an interaction request with incorrect entity relations.");

                runtime.Systems.Create<PickUpProductSystem>().Execute();
                ExecuteProductPlacement(runtime);
                runtime.Systems.Create<FollowHeldProductSystem>().Execute();
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);
            }
            finally
            {
                cameraTransform.SetPositionAndRotation(
                    originalCameraPosition,
                    originalCameraRotation);
                Physics.SyncTransforms();

                if (hadFocusedEntity)
                    player.ReplaceFocusedEntityId(originalFocusedEntityId);
                else if (player.hasFocusedEntityId)
                    player.RemoveFocusedEntityId();
            }
        }

        private static void StoreCompleteDelivery(Runtime runtime, Scenario scenario,
            GameEntity delivery, GameEntity[] products)
        {
            for (int index = 0; index < products.Length; index++)
            {
                GameEntity product = products[index];
                if (!scenario.Player.hasHeldProductId)
                {
                    RequestInteraction(scenario.Player, product);
                    runtime.Systems.Create<PickUpProductSystem>().Execute();
                    ExecuteProductPlacement(runtime);
                    runtime.Systems.Create<FollowHeldProductSystem>().Execute();
                    CleanupEvents(runtime);
                }

                Require(scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                    $"Inbound product {product.EntityId} was not in the player's hands before storage.");
                RequestInteraction(scenario.Player, scenario.StorageZone);
                runtime.Systems.Create<StoreInboundProductSystem>().Execute();
                runtime.Systems.Create<RegisterStockedProductSystem>().Execute();
                runtime.Systems.Create<CompleteDeliverySystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);

                Require(!scenario.Player.hasHeldProductId,
                    $"The player's hands remained occupied after storing product {product.EntityId}.");
                Require(product.isInStock && !product.isInboundProduct && !product.isCarried &&
                        product.hasStorageZoneEntityId && product.hasStorageSlotIndex,
                    $"Product {product.EntityId} did not enter stock correctly.");
                Require(product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                        product.StorageSlotIndex >= 0 &&
                        product.StorageSlotIndex < scenario.StorageZone.Slots.Length,
                    $"Product {product.EntityId} has an invalid storage slot relation.");
                Require(!product.hasDeliveryEntityId && !product.hasDeliverySlotIndex &&
                        !product.isLooseProduct && !product.hasWorldPosition && !product.hasWorldRotation &&
                        !product.isProductPlacementDirty,
                    $"Product {product.EntityId} retained stale placement state after stocking.");
                Require(product.Transform.parent ==
                        scenario.StorageZone.Slots[product.StorageSlotIndex],
                    $"Product {product.EntityId} was not snapped to its registered storage slot.");
                Require(product.Rigidbody.isKinematic && product.Rigidbody.detectCollisions &&
                        product.Colliders.All(collider => collider.enabled),
                    $"Stored product {product.EntityId} is not interactable in its slot.");

                int expectedStockedCount = index + 1;
                Require(delivery.StockedProductCount == expectedStockedCount,
                    $"Delivery stocked count is not {expectedStockedCount}.");
                Require(scenario.StorageZone.OccupiedStorageSlotCount == expectedStockedCount &&
                        scenario.Order.AvailableProductCount == expectedStockedCount,
                    $"Derived storage state was not refreshed after stocking product {product.EntityId}.");

                if (expectedStockedCount == scenario.Order.RequiredProductCount - 1)
                {
                    AttemptOrderAcceptance(runtime, scenario);
                    Require(scenario.Order.isOrderWaiting,
                        "The customer order was accepted before enough products entered stock.");
                }
            }

            Require(products.Select(product => product.StorageSlotIndex).Distinct().Count() == products.Length,
                "Delivered products occupy duplicate storage slots.");
        }

        private static void LoadCustomerOrder(Runtime runtime, Scenario scenario, GameEntity[] products)
        {
            foreach (GameEntity product in products)
            {
                Require(product.isInStock,
                    $"Product {product.EntityId} was selected for the order without being in stock.");
                if (!scenario.Player.hasHeldProductId)
                {
                    RequestInteraction(scenario.Player, product);
                    runtime.Systems.Create<PickUpProductSystem>().Execute();
                    ExecuteProductPlacement(runtime);
                    runtime.Systems.Create<FollowHeldProductSystem>().Execute();
                    ExecuteStorageState(runtime);
                    CleanupEvents(runtime);
                }

                Require(scenario.Player.hasHeldProductId &&
                        scenario.Player.HeldProductId == product.EntityId && product.isCarried,
                    $"Stock product {product.EntityId} was not picked up for the customer order.");
                Require(product.hasStorageZoneEntityId &&
                        product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                        !product.hasStorageSlotIndex,
                    $"Picked product {product.EntityId} did not preserve only its storage ownership.");

                RequestInteraction(scenario.Player, scenario.LoadingZone);
                runtime.Systems.Create<LoadHeldProductSystem>().Execute();
                runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
                runtime.Systems.Create<CompleteOrderSystem>().Execute();
                runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);

                Require(product.isLoaded && !product.isInStock && !product.isCarried,
                    $"Product {product.EntityId} did not enter the customer vehicle.");
                Require(!product.hasStorageZoneEntityId && !product.hasStorageSlotIndex,
                    $"Loaded product {product.EntityId} kept its storage ownership relation.");
                Require(product.hasLoadingZoneEntityId && product.hasLoadingSlotIndex &&
                        product.LoadingZoneEntityId == scenario.LoadingZone.EntityId &&
                        product.LoadingSlotIndex >= 0 &&
                        product.LoadingSlotIndex < scenario.LoadingZone.Slots.Length &&
                        !product.isLooseProduct && !product.hasWorldPosition && !product.hasWorldRotation &&
                        !product.isProductPlacementDirty,
                    $"Loaded product {product.EntityId} has an invalid loading placement relation.");
                Require(!scenario.Player.hasHeldProductId,
                    $"The player's hands remained occupied after loading product {product.EntityId}.");
                Require(product.Transform.parent ==
                        scenario.LoadingZone.Slots[product.LoadingSlotIndex],
                    $"Product {product.EntityId} was not snapped to a customer loading slot.");
                Require(!product.Rigidbody.detectCollisions &&
                        product.Colliders.All(collider => !collider.enabled),
                    $"Loaded product {product.EntityId} retained interactive colliders.");
            }

            Require(products.Select(product => product.LoadingSlotIndex).Distinct().Count() ==
                    products.Length,
                "Customer products were not snapped to unique outbound slots.");
        }

        private static void AttemptOrderAcceptance(Runtime runtime, Scenario scenario)
        {
            ExecuteStorageState(runtime);
            RequestInteraction(scenario.Player, scenario.OrderCounter);
            runtime.Systems.Create<AcceptOrderSystem>().Execute();
            CleanupEvents(runtime);
        }

        private static void ExecuteStorageState(Runtime runtime) =>
            runtime.Systems.Create<StorageStateFeature>().Execute();

        private static void ExecuteInteractionPrompts(Runtime runtime) =>
            runtime.Systems.Create<InteractionPromptFeature>().Execute();

        private static void ExecuteProductPlacement(Runtime runtime) =>
            runtime.Systems.Create<ProductPlacementFeature>().Execute();

        private static void CleanupEvents(Runtime runtime)
        {
            runtime.Systems.Create<PresentNotificationsSystem>().Execute();
            runtime.Systems.Create<PlayAudioCuesSystem>().Execute();
            runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();

            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).GetEntities().Length == 0,
                "An interaction request survived event cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.ProductLoaded).GetEntities().Length == 0,
                "A ProductLoaded event survived event cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.ProductStocked).GetEntities().Length == 0,
                "A ProductStocked event survived event cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.OrderCompletedEvent).GetEntities().Length == 0,
                "An OrderCompleted event survived event cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).GetEntities().Length == 0,
                "A notification event survived event cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.AudioCue).GetEntities().Length == 0,
                "An audio event survived event cleanup.");
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
            return new Runtime(
                container.Resolve<GameContext>(),
                container.Resolve<InputContext>(),
                container.Resolve<ISystemFactory>(),
                container.Resolve<IGameStateMachine>(),
                container.Resolve<IStaticDataService>(),
                container.Resolve<IIdentifierService>());
        }

        private static GameEntity[] FindProducts(GameContext context) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Product,
                    GameMatcher.ProductType,
                    GameMatcher.ProductMass))
                .GetEntities()
                .OrderBy(product => product.EntityId)
                .ToArray();

        private static EntityBehaviour RequireRuntimeView(GameEntity entity,
            EntityBehaviour expectedPrefab, string role)
        {
            Require(entity.hasViewPrefab && entity.ViewPrefab == expectedPrefab,
                $"The {role} entity does not reference its configured prefab.");
            Require(entity.hasView,
                $"The {role} entity has no runtime view.");
            EntityBehaviour view = entity.View as EntityBehaviour ??
                                   throw new InvalidOperationException(
                                       $"The {role} view is not an {nameof(EntityBehaviour)}.");
            Require(view.HasEntity && ReferenceEquals(view.Entity, entity),
                $"The {role} runtime view is not bound back to its ECS entity.");
            Require(view.gameObject != expectedPrefab.gameObject,
                $"The {role} is using the prefab asset instead of a runtime instance.");
            Require(view.gameObject.scene == SceneManager.GetActiveScene(),
                $"The {role} runtime view is outside the active gameplay scene.");
            return view;
        }

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
                IGameStateMachine stateMachine, IStaticDataService staticData,
                IIdentifierService identifiers)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
                StaticData = staticData;
                Identifiers = identifiers;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
            public IStaticDataService StaticData { get; }
            public IIdentifierService Identifiers { get; }
        }

        private readonly struct Scenario
        {
            public Scenario(GameEntity player, GameEntity store, GameEntity order,
                GameEntity orderCounter, GameEntity loadingZone, GameEntity procurementTerminal,
                GameEntity storageZone, InputEntity input)
            {
                Player = player;
                Store = store;
                Order = order;
                OrderCounter = orderCounter;
                LoadingZone = loadingZone;
                ProcurementTerminal = procurementTerminal;
                StorageZone = storageZone;
                Input = input;
            }

            public GameEntity Player { get; }
            public GameEntity Store { get; }
            public GameEntity Order { get; }
            public GameEntity OrderCounter { get; }
            public GameEntity LoadingZone { get; }
            public GameEntity ProcurementTerminal { get; }
            public GameEntity StorageZone { get; }
            public InputEntity Input { get; }
        }

        private readonly struct DeliveryArrival
        {
            public DeliveryArrival(GameEntity delivery, EntityBehaviour deliveryView, GameEntity[] products)
            {
                Delivery = delivery;
                DeliveryView = deliveryView;
                Products = products;
            }

            public GameEntity Delivery { get; }
            public EntityBehaviour DeliveryView { get; }
            public GameEntity[] Products { get; }
        }
    }
}
