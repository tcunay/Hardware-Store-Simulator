using System;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Gameplay.Features.Delivery.Systems;
using HardwareStore.Gameplay.Features.Interaction;
using HardwareStore.Gameplay.Features.Movement.Systems;
using HardwareStore.Gameplay.Features.Orders.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Features.Products;
using HardwareStore.Gameplay.Features.StorageState;
using HardwareStore.Gameplay.Features.StoreSceneBindings.Systems;
using HardwareStore.Gameplay.StaticData;
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
            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            DeliveryArrival arrival = PurchaseAndPrepareArrival(runtime, scenario);

            Debug.Log(
                $"[Hardware Store] Visual check prepared: customer visit " +
                $"{visit.Entity.EntityId}, delivery {arrival.Delivery.EntityId} and " +
                $"{arrival.Products.Length} cement bags.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int initialMoney = scenario.Store.Money;
            int deliveryCount = runtime.StaticData.Delivery.ProductCount;
            int deliveryCost = runtime.StaticData.Delivery.TotalCost;
            int reward = runtime.StaticData.Order.Reward;

            ValidateRuntimePlayerView(scenario.Player);
            Require(scenario.Player.CarryingSpeed < scenario.Player.WalkSpeed &&
                    scenario.Player.WalkSpeed < scenario.Player.SprintSpeed,
                "Player movement config must define carrying < walking < sprinting speeds.");
            ValidateCooldownPresentation(runtime, scenario);

            CustomerVisit firstVisit = SpawnAndParkCustomer(runtime, scenario);
            int requiredProductCount = firstVisit.Entity.RequiredProductCount;
            Require(deliveryCount > requiredProductCount,
                "One delivery must leave stock for the cooldown safety check.");
            Require(scenario.StorageZone.Slots.Length >=
                    deliveryCount * 2 - requiredProductCount,
                "The storage zone cannot hold the remaining first delivery and the second delivery.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(firstVisit.Entity.isCustomerVisitWaiting,
                "A customer order was accepted without stock.");

            DeliveryArrival firstArrival = PurchaseAndPrepareArrival(runtime, scenario);
            Require(scenario.Store.Money == initialMoney - deliveryCost,
                "The first delivery did not deduct its cost exactly once.");
            TestCarryDropAndRepick(runtime, scenario, firstArrival.Products[0]);
            StoreCompleteDelivery(runtime, scenario, firstVisit.Entity, firstArrival);
            CleanupCompletedDelivery(runtime, scenario, firstArrival);

            AttemptOrderAcceptance(runtime, scenario);
            Require(firstVisit.Entity.isCustomerVisitLoading &&
                    !firstVisit.Entity.isCustomerVisitWaiting,
                "The first customer visit did not enter loading after acceptance.");

            GameEntity[] firstOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Take(requiredProductCount)
                .ToArray();
            Require(firstOutboundProducts.Length == requiredProductCount,
                "The first cycle could not resolve enough stock for its order.");
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                firstVisit.Entity,
                firstOutboundProducts);
            Require(scenario.Store.Money == initialMoney - deliveryCost + reward,
                "The first cycle balance is not purchase cost plus exactly one reward.");

            int firstVisitId = firstVisit.Entity.EntityId;
            DepartAndCleanupCustomer(runtime, scenario, firstVisit, firstOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);
            int stockAfterFirstCycle = scenario.StorageZone.StorageProductCount;
            Require(stockAfterFirstCycle == deliveryCount - requiredProductCount,
                "The first cycle left an incorrect stock count.");

            CustomerVisit secondVisit = SpawnAndParkCustomer(runtime, scenario);
            Require(secondVisit.Entity.EntityId != firstVisitId,
                "The second customer visit reused the first visit identifier.");
            Require(secondVisit.Entity.Slots.All(slot => slot.childCount == 0),
                "The second customer vehicle inherited occupied loading slots.");
            Require(secondVisit.Entity.AvailableProductCount == stockAfterFirstCycle,
                "The second customer visit did not observe remaining stock.");

            DeliveryArrival secondArrival = PurchaseAndPrepareArrival(runtime, scenario);
            Require(scenario.Store.Money ==
                    initialMoney - deliveryCost + reward - deliveryCost,
                "The second delivery did not deduct its cost exactly once.");
            StoreCompleteDelivery(runtime, scenario, secondVisit.Entity, secondArrival);
            CleanupCompletedDelivery(runtime, scenario, secondArrival);

            AttemptOrderAcceptance(runtime, scenario);
            Require(secondVisit.Entity.isCustomerVisitLoading,
                "The second customer visit did not enter loading after replenishment.");

            GameEntity[] secondOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Take(secondVisit.Entity.RequiredProductCount)
                .ToArray();
            Require(secondOutboundProducts.Length == secondVisit.Entity.RequiredProductCount,
                "The second cycle could not resolve enough stock for its order.");
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                secondVisit.Entity,
                secondOutboundProducts);

            int expectedFinalMoney = initialMoney - deliveryCost * 2 + reward * 2;
            Require(scenario.Store.Money == expectedFinalMoney,
                "Two cycles did not produce exactly two purchase deductions and two rewards.");
            int expectedFinalStock = deliveryCount * 2 - requiredProductCount * 2;
            Require(scenario.StorageZone.StorageProductCount == expectedFinalStock,
                "The second customer cycle left an incorrect stock count.");

            int secondVisitId = secondVisit.Entity.EntityId;
            DepartAndCleanupCustomer(runtime, scenario, secondVisit, secondOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);

            Require(runtime.Game.GetEntityWithEntityId(firstVisitId) == null &&
                    runtime.Game.GetEntityWithEntityId(secondVisitId) == null &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Order).count == 0,
                "A completed customer cycle retained a visit entity.");
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0,
                "A completed cycle retained an active delivery.");
            Require(!scenario.Player.isHandsOccupied &&
                    runtime.Game.GetEntityWithCarrierEntityId(scenario.Player.EntityId) == null,
                "The player retained a carrier relation after both cycles.");

            Debug.Log(
                $"[Hardware Store] Gameplay smoke passed: two complete customer cycles, " +
                $"stock {expectedFinalStock}, balance {expectedFinalMoney:N0} ₽.");
        }

        private static Scenario ResolveFreshScenario(Runtime runtime)
        {
            Require(runtime.StateMachine.ActiveStateType == typeof(StoreLoopState),
                $"The smoke test requires {nameof(StoreLoopState)}, but the active state is " +
                $"{runtime.StateMachine.ActiveStateType?.Name ?? "none"}.");

            runtime.Systems.Create<BindEntityViewFromSceneSystem>().Execute();
            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            runtime.Systems.Create<ValidateStoreSceneBindingsSystem>().Execute();
            RequireExactlyOnePlayer(runtime.Game);

            GameEntity player = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Player,
                GameMatcher.StoreEntityId,
                GameMatcher.CharacterController,
                GameMatcher.Transform,
                GameMatcher.View,
                GameMatcher.CarryAnchor,
                GameMatcher.DropOrigin,
                GameMatcher.MovementSpeed,
                GameMatcher.WalkSpeed,
                GameMatcher.SprintSpeed,
                GameMatcher.CarryingSpeed)), "player");
            GameEntity store = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Store,
                GameMatcher.Money,
                GameMatcher.OrderCounterEntityId,
                GameMatcher.ProcurementTerminalEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.StoreSceneBindingsValidated)), "store");
            GameEntity orderCounter = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.StoreEntityId,
                GameMatcher.OrderCounter,
                GameMatcher.View,
                GameMatcher.InteractionView)), "order counter");
            GameEntity procurementTerminal = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.ProcurementTerminal,
                GameMatcher.StoreEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.ProductType,
                GameMatcher.DeliveryProductCount,
                GameMatcher.DeliveryCost,
                GameMatcher.DeliverySpawnPosition,
                GameMatcher.DeliverySpawnRotation,
                GameMatcher.View,
                GameMatcher.InteractionView)), "procurement terminal");
            GameEntity storageZone = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.StorageZone,
                GameMatcher.OccupiedStorageSlotCount,
                GameMatcher.StorageProductCount,
                GameMatcher.Slots,
                GameMatcher.View,
                GameMatcher.InteractionView)), "storage zone");
            InputEntity input = RequireSingle(
                runtime.Input.GetGroup(InputMatcher.InputState),
                "input state");

            NormalizeFreshCustomerCooldown(runtime, store);
            ExecuteStorageState(runtime);

            Require(player.StoreEntityId == store.EntityId,
                "The player does not reference the scenario store.");
            Require(store.OrderCounterEntityId == orderCounter.EntityId &&
                    orderCounter.StoreEntityId == store.EntityId,
                "The store and order counter relations are inconsistent.");
            Require(store.ProcurementTerminalEntityId == procurementTerminal.EntityId &&
                    procurementTerminal.StoreEntityId == store.EntityId,
                "The store and procurement terminal relations are inconsistent.");
            Require(store.StorageZoneEntityId == storageZone.EntityId &&
                    procurementTerminal.StorageZoneEntityId == storageZone.EntityId,
                "The store graph does not reference one storage zone.");
            Require(!orderCounter.hasSceneViewKey &&
                    !procurementTerminal.hasSceneViewKey &&
                    !storageZone.hasSceneViewKey,
                "SceneViewKey binder did not consume all static scene-view requests.");
            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(store.EntityId) == null &&
                    store.hasCustomerCooldownRemaining &&
                    store.CustomerCooldownRemaining > 0f,
                "The smoke test did not start in customer cooldown.");
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        procurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0,
                "The smoke test must start without an active delivery.");
            Require(FindProducts(runtime.Game).Length == 0,
                "The smoke test must start without runtime products.");
            Require(!player.isHandsOccupied &&
                    runtime.Game.GetEntityWithCarrierEntityId(player.EntityId) == null,
                "The smoke test must start with empty hands.");
            Require(storageZone.OccupiedStorageSlotCount == 0 &&
                    storageZone.StorageProductCount == 0,
                "The smoke test must start with empty storage.");
            Require(store.Money == runtime.StaticData.Economy.InitialMoney,
                "The store does not contain EconomyConfig.InitialMoney.");

            return new Scenario(
                player,
                store,
                orderCounter,
                procurementTerminal,
                storageZone,
                input);
        }

        private static void NormalizeFreshCustomerCooldown(Runtime runtime, GameEntity store)
        {
            GameEntity visit = runtime.Game.GetEntityWithCustomerVisitStoreEntityId(store.EntityId);
            if (visit == null)
            {
                Require(store.hasCustomerCooldownRemaining,
                    "The fresh store has neither a customer visit nor a cooldown.");
                if (store.CustomerCooldownRemaining <= 0f)
                {
                    store.ReplaceCustomerCooldownRemaining(
                        runtime.StaticData.CustomerVehicle.FirstCustomerDelay);
                }

                return;
            }

            Require(!store.hasCustomerCooldownRemaining &&
                    FindProducts(runtime.Game).Length == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    visit.LoadedProductCount == 0 &&
                    !visit.isOrderRewarded &&
                    (visit.isCustomerVisitArriving || visit.isCustomerVisitWaiting),
                "The smoke test can only reset an untouched auto-spawned customer visit.");

            int visitId = visit.EntityId;
            EntityBehaviour view = visit.hasView
                ? visit.View as EntityBehaviour ?? throw new InvalidOperationException(
                    "The auto-spawned customer view is not an EntityBehaviour.")
                : null;

            store.AddCustomerCooldownRemaining(
                runtime.StaticData.CustomerVehicle.FirstCustomerDelay);
            visit.RemoveCustomerVisitStoreEntityId();
            visit.isDestructed = true;
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(store.EntityId) == null &&
                    !visit.hasCustomerVisitStoreEntityId &&
                    runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    (view == null || !view.HasEntity),
                "The untouched auto-spawned customer did not reset to cooldown.");
        }

        private static void ValidateCooldownPresentation(Runtime runtime, Scenario scenario)
        {
            runtime.Systems.Create<PresentHudSystem>().Execute();
            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt == "Ожидаем следующего клиента" &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The order counter does not present the no-customer cooldown state.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static CustomerVisit SpawnAndParkCustomer(Runtime runtime, Scenario scenario)
        {
            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    scenario.Store.hasCustomerCooldownRemaining,
                "A customer can only spawn from between-visits cooldown.");

            scenario.Store.ReplaceCustomerCooldownRemaining(0f);
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            GameEntity visit =
                runtime.Game.GetEntityWithCustomerVisitStoreEntityId(scenario.Store.EntityId);
            Require(visit != null &&
                    visit.hasCustomerVisitStoreEntityId &&
                    visit.CustomerVisitStoreEntityId == scenario.Store.EntityId &&
                    !scenario.Store.hasCustomerCooldownRemaining,
                "The ready store did not create a customer visit.");
            Require(visit.isCustomerVisit && visit.isCustomerVehicle && visit.isOrder &&
                    visit.isLoadingZone && visit.isCustomerVisitArriving &&
                    !visit.isInteractable && visit.LoadedProductCount == 0 &&
                    !visit.hasView,
                "The spawned unified customer visit has an invalid arrival state.");

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour view = RequireRuntimeView(
                visit,
                runtime.StaticData.CustomerVehicle.ViewPrefab,
                $"customer visit {visit.EntityId}");
            Require(visit.hasTransform && visit.hasRigidbody && visit.hasSlots &&
                    visit.Slots.Length >= visit.RequiredProductCount,
                "The customer visit view did not register movement and loading data.");
            Require(visit.Rigidbody.isKinematic &&
                    visit.Rigidbody.interpolation == RigidbodyInterpolation.None,
                "The customer vehicle has invalid route physics.");

            ForceRouteEndpoint(runtime, visit);
            runtime.Systems.Create<CompleteCustomerVehicleArrivalSystem>().Execute();
            ExecuteStorageState(runtime);
            Require(visit.isCustomerVisitWaiting &&
                    !visit.isCustomerVisitArriving &&
                    !visit.isRouteCompleted &&
                    !visit.hasRoute &&
                    !visit.hasRouteWaypointIndex &&
                    visit.isInteractable,
                "The customer visit did not enter its parked waiting state.");
            Require(visit.AvailableProductCount ==
                    scenario.StorageZone.StorageProductCount,
                "The parked customer visit does not observe current stock.");

            return new CustomerVisit(visit, view);
        }

        private static void ForceRouteEndpoint(Runtime runtime, GameEntity visit)
        {
            Require(visit.hasRoute && visit.Route.Length >= 2,
                $"Customer visit {visit.EntityId} has no route to force.");
            Pose destination = visit.Route[^1];
            visit.ReplaceRouteWaypointIndex(visit.Route.Length - 1);
            visit.Rigidbody.position = destination.position;
            visit.Rigidbody.rotation = destination.rotation;
            visit.Transform.SetPositionAndRotation(destination.position, destination.rotation);
            Physics.SyncTransforms();
            runtime.Systems.Create<MoveCustomerVehicleRouteSystem>().Execute();
            Require(visit.isRouteCompleted,
                $"Customer visit {visit.EntityId} did not complete its forced route.");
        }

        private static DeliveryArrival PurchaseAndPrepareArrival(
            Runtime runtime,
            Scenario scenario)
        {
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "A new delivery cannot be purchased while another is active.");

            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            GameEntity delivery = runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                scenario.ProcurementTerminal.EntityId);
            Require(delivery != null &&
                    delivery.hasDeliveryProcurementTerminalEntityId &&
                    delivery.DeliveryProcurementTerminalEntityId ==
                    scenario.ProcurementTerminal.EntityId &&
                    delivery.isDeliveryActive,
                "Purchasing did not create an indexed active delivery.");
            CleanupEvents(runtime);

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour deliveryView = RequireRuntimeView(
                delivery,
                runtime.StaticData.Delivery.ViewPrefab,
                $"delivery {delivery.EntityId}");
            Require(delivery.hasSlots &&
                    delivery.Slots.Length >= delivery.DeliveryProductCount,
                "The delivery view did not register enough cargo slots.");

            runtime.Systems.Create<SpawnDeliveryProductsSystem>().Execute();
            Require(delivery.isDeliveryProductsSpawned,
                "The active delivery did not spawn its products.");
            runtime.Systems.Create<SpawnDeliveryProductsSystem>().Execute();
            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            ExecuteProductPlacement(runtime);

            GameEntity[] products = FindDeliveryProducts(runtime.Game, delivery.EntityId);
            Require(products.Length == delivery.DeliveryProductCount,
                "The delivery spawned an incorrect number of products.");
            foreach (GameEntity product in products)
            {
                RequireRuntimeView(
                    product,
                    runtime.StaticData.Product.ViewPrefab,
                    $"product {product.EntityId}");
                Require(product.isInboundProduct &&
                        product.DeliveryEntityId == delivery.EntityId &&
                        product.hasDeliverySlotIndex &&
                        !product.hasCarrierEntityId &&
                        !product.isInStock &&
                        !product.isLooseProduct &&
                        !product.isLoaded &&
                        !product.isProductPlacementDirty,
                    $"Inbound product {product.EntityId} has an invalid arrival state.");
                Require(product.Transform.parent ==
                        delivery.Slots[product.DeliverySlotIndex],
                    $"Inbound product {product.EntityId} is not in its delivery slot.");
            }

            return new DeliveryArrival(delivery, deliveryView, products);
        }

        private static void TestCarryDropAndRepick(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            PickUpProduct(runtime, scenario, product);
            Require(scenario.Player.isHandsOccupied &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithCarrierEntityId(scenario.Player.EntityId),
                        product),
                "Carrier index did not resolve the picked inbound product.");

            scenario.Input.isSprintHeld = true;
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(
                    scenario.Player.MovementSpeed,
                    scenario.Player.CarryingSpeed),
                "HandsOccupied did not select carrying movement speed.");

            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);
            Require(!scenario.Player.isHandsOccupied &&
                    !product.hasCarrierEntityId &&
                    runtime.Game.GetEntityWithCarrierEntityId(
                        scenario.Player.EntityId) == null,
                "Dropping did not clear the carrier relation.");
            Require(product.isLooseProduct &&
                    product.hasWorldPosition &&
                    product.hasWorldRotation &&
                    !product.Rigidbody.isKinematic &&
                    product.Rigidbody.detectCollisions,
                "The dropped product did not return to loose physics.");

            PickUpProduct(runtime, scenario, product);
            Require(scenario.Player.isHandsOccupied &&
                    product.hasCarrierEntityId &&
                    !product.isLooseProduct &&
                    !product.hasWorldPosition &&
                    !product.hasWorldRotation,
                "The dropped product could not be picked up again.");
            scenario.Input.isSprintHeld = false;
        }

        private static void StoreCompleteDelivery(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            DeliveryArrival arrival)
        {
            int initialStockCount = scenario.StorageZone.StorageProductCount;
            for (int index = 0; index < arrival.Products.Length; index++)
            {
                GameEntity product = arrival.Products[index];
                if (!scenario.Player.isHandsOccupied)
                    PickUpProduct(runtime, scenario, product);

                Require(ReferenceEquals(
                        runtime.Game.GetEntityWithCarrierEntityId(
                            scenario.Player.EntityId),
                        product),
                    $"Inbound product {product.EntityId} is not carried before storage.");

                RequestInteraction(scenario.Player, scenario.StorageZone);
                runtime.Systems.Create<StoreInboundProductSystem>().Execute();
                Require(product.isProductStocked &&
                        product.isInStock &&
                        product.hasDeliveryEntityId &&
                        !product.hasCarrierEntityId &&
                        !scenario.Player.isHandsOccupied,
                    $"ProductStocked was not raised on product {product.EntityId}.");

                runtime.Systems.Create<RegisterStockedProductSystem>().Execute();
                Require(!product.isProductStocked &&
                        !product.hasDeliveryEntityId,
                    $"ProductStocked was not consumed for product {product.EntityId}.");
                runtime.Systems.Create<CompleteDeliverySystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);

                int expectedStock = initialStockCount + index + 1;
                Require(product.isInStock &&
                        !product.isInboundProduct &&
                        product.hasStorageZoneEntityId &&
                        product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                        product.hasStorageSlotIndex &&
                        !product.hasCarrierEntityId &&
                        !product.isProductPlacementDirty,
                    $"Product {product.EntityId} did not enter storage correctly.");
                Require(scenario.StorageZone.StorageProductCount == expectedStock &&
                        scenario.StorageZone.OccupiedStorageSlotCount == expectedStock &&
                        visit.AvailableProductCount == expectedStock,
                    $"Derived storage state is incorrect after product {product.EntityId}.");

                bool isLast = index == arrival.Products.Length - 1;
                Require((runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                                scenario.ProcurementTerminal.EntityId) == null) == isLast &&
                        arrival.Delivery.hasDeliveryProcurementTerminalEntityId != isLast,
                    $"{nameof(DeliveryProcurementTerminalEntityId)} changed at the wrong " +
                    "delivery progress.");
            }

            Require(arrival.Delivery.isDeliveryCompleted &&
                    arrival.Delivery.isDestructed &&
                    !arrival.Delivery.hasDeliveryProcurementTerminalEntityId,
                "The fully stocked delivery did not complete.");
            Require(arrival.Products
                    .Select(product => product.StorageSlotIndex)
                    .Distinct()
                    .Count() == arrival.Products.Length,
                "Delivered products occupy duplicate storage slots.");
        }

        private static void CleanupCompletedDelivery(
            Runtime runtime,
            Scenario scenario,
            DeliveryArrival arrival)
        {
            int deliveryId = arrival.Delivery.EntityId;
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    !arrival.Delivery.hasDeliveryProcurementTerminalEntityId,
                "The completed delivery retained its procurement-terminal index relation.");

            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            Require(!arrival.DeliveryView.HasEntity,
                "The completed delivery view is still bound.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            Require(runtime.Game.GetEntityWithEntityId(deliveryId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0,
                "The completed delivery survived the destructed pipeline.");
        }

        private static void LoadAndRewardCustomerOrder(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity[] products)
        {
            Require(visit.isCustomerVisitLoading,
                "Products can only be loaded during CustomerVisitLoading.");
            int moneyBeforeReward = scenario.Store.Money;

            foreach (GameEntity product in products)
            {
                scenario.Player.ReplaceFocusedEntityId(product.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(scenario.Player.isFocusInteractionAvailable &&
                        scenario.Player.InteractionPrompt ==
                        "E — взять мешок со склада",
                    $"Stock product {product.EntityId} has no active-order prompt.");
                scenario.Player.RemoveFocusedEntityId();

                PickUpProduct(runtime, scenario, product);
                Require(product.hasCarrierEntityId &&
                        product.CarrierEntityId == scenario.Player.EntityId &&
                        product.isInStock &&
                        !product.hasStorageSlotIndex,
                    $"Stock product {product.EntityId} was not picked for loading.");

                int loadedBefore = visit.LoadedProductCount;
                RequestInteraction(scenario.Player, visit);
                runtime.Systems.Create<LoadHeldProductSystem>().Execute();
                Require(product.isProductLoaded &&
                        product.isLoaded &&
                        product.hasCustomerVisitEntityId &&
                        product.CustomerVisitEntityId == visit.EntityId &&
                        !product.hasCarrierEntityId &&
                        !scenario.Player.isHandsOccupied,
                    $"ProductLoaded was not raised on product {product.EntityId}.");

                runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
                Require(!product.isProductLoaded &&
                        visit.LoadedProductCount == loadedBefore + 1,
                    $"ProductLoaded was not consumed for product {product.EntityId}.");
                runtime.Systems.Create<CompleteOrderSystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);

                Require(product.isLoaded &&
                        !product.isInStock &&
                        product.hasCustomerVisitEntityId &&
                        product.CustomerVisitEntityId == visit.EntityId &&
                        product.hasLoadingSlotIndex &&
                        product.Transform.parent == visit.Slots[product.LoadingSlotIndex] &&
                        !product.isProductPlacementDirty,
                    $"Loaded product {product.EntityId} has invalid visit placement.");
            }

            Require(visit.isCustomerVisitCompleted &&
                    !visit.isCustomerVisitLoading &&
                    !visit.isOrderRewarded &&
                    visit.LoadedProductCount == visit.RequiredProductCount,
                "The customer visit did not complete after all products were loaded.");

            runtime.Systems.Create<BeginCustomerVehicleDepartureDelaySystem>().Execute();
            Require(!visit.hasCustomerDepartureDelayRemaining,
                "An unrewarded customer visit began its departure delay.");

            runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
            Require(visit.isOrderRewarded &&
                    scenario.Store.Money == moneyBeforeReward + visit.OrderReward,
                "The completed order was not rewarded exactly once.");
            runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
            Require(scenario.Store.Money == moneyBeforeReward + visit.OrderReward,
                "The same completed order was rewarded more than once.");
            CleanupEvents(runtime);

            Require(products
                    .Select(product => product.LoadingSlotIndex)
                    .Distinct()
                    .Count() == products.Length,
                "Loaded products occupy duplicate customer slots.");
        }

        private static void DepartAndCleanupCustomer(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit visit,
            GameEntity[] loadedProducts)
        {
            GameEntity entity = visit.Entity;
            Require(entity.isCustomerVisitCompleted &&
                    entity.isOrderRewarded &&
                    !entity.hasCustomerDepartureDelayRemaining,
                "Only a completed and rewarded customer visit may depart.");

            runtime.Systems.Create<BeginCustomerVehicleDepartureDelaySystem>().Execute();
            Require(entity.hasCustomerDepartureDelayRemaining &&
                    Mathf.Approximately(
                        entity.CustomerDepartureDelayRemaining,
                        runtime.StaticData.CustomerVehicle.CompletedDwellDuration),
                "The completed visit did not begin its configured departure delay.");

            runtime.Systems.Create<TickCustomerVehicleDepartureDelaySystem>().Execute();
            Require(entity.CustomerDepartureDelayRemaining >= 0f,
                "The departure delay ticked below zero.");
            entity.ReplaceCustomerDepartureDelayRemaining(0f);
            runtime.Systems.Create<BeginCustomerVehicleDepartureSystem>().Execute();
            Require(entity.isCustomerVisitDeparting &&
                    !entity.isCustomerVisitCompleted &&
                    !entity.isInteractable &&
                    !entity.hasCustomerDepartureDelayRemaining &&
                    entity.hasRoute,
                "The customer visit did not begin departure.");

            ForceRouteEndpoint(runtime, entity);
            EntityBehaviour[] loadedViews = loadedProducts
                .Select(product => (EntityBehaviour)product.View)
                .ToArray();
            int[] loadedProductIds = loadedProducts
                .Select(product => product.EntityId)
                .ToArray();
            int visitId = entity.EntityId;
            runtime.Systems.Create<CompleteCustomerVehicleDepartureSystem>().Execute();

            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    !entity.hasCustomerVisitStoreEntityId &&
                    scenario.Store.hasCustomerCooldownRemaining &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        runtime.StaticData.CustomerVehicle.NextCustomerDelay),
                "Customer departure did not start the next cooldown.");
            Require(entity.isDestructed &&
                    loadedProducts.All(product => product.isDestructed),
                "Customer departure did not destruct the visit and its loaded products.");

            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            Require(!visit.View.HasEntity &&
                    loadedViews.All(view => !view.HasEntity),
                "Customer cleanup retained a nested view binding.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            CleanupEvents(runtime);

            Require(runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    loadedProductIds.All(productId =>
                        runtime.Game.GetEntityWithEntityId(productId) == null) &&
                    runtime.Game.GetGroup(GameMatcher.Loaded).count == 0,
                "A departed customer graph survived cleanup.");
            ExecuteStorageState(runtime);
        }

        private static void ValidateCooldownSafety(Runtime runtime, Scenario scenario)
        {
            ValidateCooldownPresentation(runtime, scenario);
            GameEntity remainingStock = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .First();

            scenario.Player.ReplaceFocusedEntityId(remainingStock.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt.IndexOf(
                        "следующего клиента",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Stock remained available during customer cooldown.");
            scenario.Player.RemoveFocusedEntityId();

            RequestInteraction(scenario.Player, remainingStock);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            CleanupEvents(runtime);
            Require(!scenario.Player.isHandsOccupied &&
                    !remainingStock.hasCarrierEntityId &&
                    remainingStock.isInStock,
                "Stock was picked up without a current customer visit.");

            float cooldown = scenario.Store.CustomerCooldownRemaining;
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(cooldown > 0f &&
                    runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null,
                "A customer spawned before cooldown elapsed.");
        }

        private static void PickUpProduct(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();
            CleanupEvents(runtime);

            Require(scenario.Player.isHandsOccupied &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    !product.isProductPlacementDirty,
                $"Product {product.EntityId} was not assigned to the player carrier.");
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

            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                "An interaction request survived cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.ProductLoaded).count == 0,
                "A ProductLoaded marker survived its consumer.");
            Require(runtime.Game.GetGroup(GameMatcher.ProductStocked).count == 0,
                "A ProductStocked marker survived its consumer.");
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "A notification event survived presentation.");
            Require(runtime.Game.GetGroup(GameMatcher.AudioCue).count == 0,
                "An audio event survived presentation.");
        }

        private static Runtime ResolveRuntime()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Enter Play Mode before running the gameplay smoke test.");

            ProjectContext[] projectContexts = Resources.FindObjectsOfTypeAll<ProjectContext>()
                .Where(context => context.gameObject.scene.IsValid())
                .ToArray();
            Require(projectContexts.Length == 1,
                $"Expected exactly one runtime ProjectContext, found " +
                $"{projectContexts.Length}.");
            DiContainer container = projectContexts[0].Container;
            Require(container != null,
                "The runtime ProjectContext container is not initialized yet.");

            return new Runtime(
                container.Resolve<GameContext>(),
                container.Resolve<InputContext>(),
                container.Resolve<ISystemFactory>(),
                container.Resolve<IGameStateMachine>(),
                container.Resolve<IStaticDataService>());
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

        private static GameEntity[] FindDeliveryProducts(
            GameContext context,
            int deliveryEntityId) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Product,
                    GameMatcher.InboundProduct,
                    GameMatcher.DeliveryEntityId))
                .GetEntities()
                .Where(product => product.DeliveryEntityId == deliveryEntityId)
                .OrderBy(product => product.EntityId)
                .ToArray();

        private static GameEntity[] FindStockProducts(
            GameContext context,
            int storageZoneEntityId) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Product,
                    GameMatcher.InStock,
                    GameMatcher.StorageZoneEntityId))
                .GetEntities()
                .Where(product => product.StorageZoneEntityId == storageZoneEntityId)
                .OrderBy(product => product.EntityId)
                .ToArray();

        private static EntityBehaviour RequireRuntimeView(
            GameEntity entity,
            EntityBehaviour expectedPrefab,
            string role)
        {
            Require(entity.hasViewPrefab && entity.ViewPrefab == expectedPrefab,
                $"The {role} entity does not reference its configured prefab.");
            Require(entity.hasView,
                $"The {role} entity has no runtime view.");
            EntityBehaviour view = entity.View as EntityBehaviour ??
                                   throw new InvalidOperationException(
                                       $"The {role} view is not an EntityBehaviour.");
            Require(view.HasEntity && ReferenceEquals(view.Entity, entity),
                $"The {role} view is not bound back to its ECS entity.");
            Require(view.gameObject != expectedPrefab.gameObject,
                $"The {role} uses the prefab asset instead of a runtime instance.");
            Require(view.gameObject.scene == SceneManager.GetActiveScene(),
                $"The {role} runtime view is outside the active gameplay scene.");
            return view;
        }

        private static void RequireExactlyOnePlayer(GameContext context)
        {
            int playerCount = context.GetGroup(GameMatcher.Player).count;
            Require(playerCount == 1,
                $"Expected exactly one Player entity, found {playerCount}.");
        }

        private static void ValidateRuntimePlayerView(GameEntity player)
        {
            Require(player.Transform == player.View.gameObject.transform,
                "The player's Transform does not reference its runtime view root.");
            Require(player.View.gameObject.scene == SceneManager.GetActiveScene(),
                "The runtime player view is outside the active gameplay scene.");
        }

        private static GameEntity RequireSingle(
            IGroup<GameEntity> group,
            string role)
        {
            GameEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one {role}, found {entities.Length}.");
        }

        private static InputEntity RequireSingle(
            IGroup<InputEntity> group,
            string role)
        {
            InputEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one {role}, found {entities.Length}.");
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
            public Runtime(
                GameContext game,
                InputContext input,
                ISystemFactory systems,
                IGameStateMachine stateMachine,
                IStaticDataService staticData)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
                StaticData = staticData;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
            public IStaticDataService StaticData { get; }
        }

        private readonly struct Scenario
        {
            public Scenario(
                GameEntity player,
                GameEntity store,
                GameEntity orderCounter,
                GameEntity procurementTerminal,
                GameEntity storageZone,
                InputEntity input)
            {
                Player = player;
                Store = store;
                OrderCounter = orderCounter;
                ProcurementTerminal = procurementTerminal;
                StorageZone = storageZone;
                Input = input;
            }

            public GameEntity Player { get; }
            public GameEntity Store { get; }
            public GameEntity OrderCounter { get; }
            public GameEntity ProcurementTerminal { get; }
            public GameEntity StorageZone { get; }
            public InputEntity Input { get; }
        }

        private readonly struct CustomerVisit
        {
            public CustomerVisit(GameEntity entity, EntityBehaviour view)
            {
                Entity = entity;
                View = view;
            }

            public GameEntity Entity { get; }
            public EntityBehaviour View { get; }
        }

        private readonly struct DeliveryArrival
        {
            public DeliveryArrival(
                GameEntity delivery,
                EntityBehaviour deliveryView,
                GameEntity[] products)
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
