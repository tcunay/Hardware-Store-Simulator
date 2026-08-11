using System;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Consultation;
using HardwareStore.Gameplay.Features.Consultation.Systems;
using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Gameplay.Features.Delivery.Systems;
using HardwareStore.Gameplay.Features.Interaction;
using HardwareStore.Gameplay.Features.Interaction.Systems;
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
        [MenuItem("Tools/Hardware Store/Prepare Consultation Visual Check")]
        public static void PrepareSupplyChainVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            OpenConsultation(runtime, scenario, visit.Entity);
            runtime.Systems.Create<PresentConsultationSystem>().Execute();

            Debug.Log(
                $"[Hardware Store] Consultation visual check prepared: customer visit " +
                $"{visit.Entity.EntityId}, project '{visit.Entity.CustomerProjectTitle}' and " +
                $"{GetConsultationOffers(runtime.Game, visit.Entity).Length} offers.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int initialMoney = scenario.Store.Money;
            ProductTypeId cement = ProductTypeId.CementBag;
            ProductTypeId boards = ProductTypeId.BoardBundle;
            var cementDelivery = runtime.StaticData.GetDelivery(cement);
            var boardDelivery = runtime.StaticData.GetDelivery(boards);
            var cementOrder = runtime.StaticData.GetOrder(cement);
            var boardOrder = runtime.StaticData.GetOrder(boards);
            var cementOffer = cementOrder.Offers[0];
            var boardOffer = boardOrder.Offers[2];

            ValidateRuntimePlayerView(scenario.Player);
            Require(scenario.Player.WalkSpeed < scenario.Player.SprintSpeed,
                "Player movement config must define walking < sprinting speeds.");
            Require(runtime.StaticData.ProductTypes.SequenceEqual(new[] { cement, boards }),
                "The smoke test requires the stable CementBag -> BoardBundle order sequence.");
            Require(scenario.Store.NextOrderSequenceIndex == 0,
                "A fresh store must begin with the first configured order type.");
            ValidateCooldownPresentation(runtime, scenario);
            ValidateRejectedDeliveryPurchase(
                runtime,
                scenario,
                cement,
                "A delivery was purchased without a current customer visit.");

            CustomerVisit firstVisit = SpawnAndParkCustomer(runtime, scenario);
            int firstCustomerActorId = firstVisit.Actor.EntityId;
            Require(firstVisit.Entity.RequestedProductType == cement &&
                    firstVisit.Entity.CustomerProjectTitle == cementOrder.CustomerProjectTitle &&
                    firstVisit.Entity.CustomerRequest == cementOrder.CustomerRequest &&
                    scenario.Store.NextOrderSequenceIndex == 1,
                "The first customer visit did not receive the configured cement project.");

            ValidateRejectedDeliveryPurchase(
                runtime,
                scenario,
                cement,
                "A delivery was purchased before the cement offer was confirmed.");
            OpenConsultation(runtime, scenario, firstVisit.Entity);
            CancelConsultation(runtime, scenario, firstVisit.Entity);
            OpenConsultation(runtime, scenario, firstVisit.Entity);
            SelectConsultationOfferWithWraparound(
                runtime,
                scenario,
                firstVisit.Entity,
                selectedIndex: 0);
            ConfirmConsultation(runtime, scenario, firstVisit.Entity, cementOffer, cementDelivery);
            Require(firstVisit.Entity.RequiredProductType == cement &&
                    firstVisit.Entity.RequiredProductCount == cementOffer.RequiredProductCount &&
                    firstVisit.Entity.OrderReward == cementOffer.Reward,
                "The confirmed cement offer did not form the configured order.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(firstVisit.Entity.isCustomerVisitWaiting,
                "A customer order was accepted without stock.");
            ValidateRejectedDeliveryPurchase(
                runtime,
                scenario,
                boards,
                "A wrong-SKU board delivery was purchased for the cement customer.");

            DeliveryArrival firstArrival = PurchaseAndPrepareArrival(runtime, scenario, cement);
            Require(scenario.Store.Money == initialMoney - cementDelivery.TotalCost,
                "The first delivery did not deduct its cost exactly once.");
            TestCarryDropAndRepick(runtime, scenario, firstArrival.Products[0]);
            StoreCompleteDelivery(runtime, scenario, firstVisit.Entity, firstArrival);
            CleanupCompletedDelivery(runtime, scenario, firstArrival);
            ValidatePhysicalStockFocus(runtime, scenario, firstArrival.Products[0]);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) ==
                    cementDelivery.ProductCount,
                "The cement delivery did not produce the configured typed stock.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(firstVisit.Entity.isCustomerVisitLoading &&
                    !firstVisit.Entity.isCustomerVisitWaiting,
                "The first customer visit did not enter loading after acceptance.");

            GameEntity[] firstOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == cement)
                .Take(cementOffer.RequiredProductCount)
                .ToArray();
            Require(firstOutboundProducts.Length == cementOffer.RequiredProductCount,
                "The first cycle could not resolve enough stock for its order.");
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                firstVisit.Entity,
                firstOutboundProducts);
            Require(scenario.Store.Money ==
                    initialMoney - cementDelivery.TotalCost + cementOffer.Reward,
                "The first cycle balance is not purchase cost plus exactly one reward.");

            int firstVisitId = firstVisit.Entity.EntityId;
            DepartAndCleanupCustomer(runtime, scenario, firstVisit, firstOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) == 2 &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) == 0,
                "The economy cement cycle did not leave exactly two cement bags.");

            CustomerVisit secondVisit = SpawnAndParkCustomer(runtime, scenario);
            Require(secondVisit.Entity.EntityId != firstVisitId,
                "The second customer visit reused the first visit identifier.");
            Require(secondVisit.Actor.EntityId != firstCustomerActorId,
                "The second customer actor reused the first actor identifier.");
            Require(secondVisit.Entity.Slots.All(slot => slot.childCount == 0),
                "The second customer vehicle inherited occupied loading slots.");
            Require(secondVisit.Entity.RequestedProductType == boards &&
                    secondVisit.Entity.CustomerProjectTitle == boardOrder.CustomerProjectTitle &&
                    secondVisit.Entity.CustomerRequest == boardOrder.CustomerRequest &&
                    scenario.Store.NextOrderSequenceIndex == 0,
                "The second customer visit did not receive the configured board project.");
            Require(GetConsultationOffers(runtime.Game, secondVisit.Entity)
                        .All(offer => offer.AvailableProductCount == 0) &&
                    scenario.StorageZone.StorageProductCount == 2,
                "Wrong-SKU cement stock was counted as available for a board offer.");

            ValidateRejectedDeliveryPurchase(
                runtime,
                scenario,
                boards,
                "A delivery was purchased before the board offer was confirmed.");
            OpenConsultation(runtime, scenario, secondVisit.Entity);
            SelectConsultationOfferWithWraparound(
                runtime,
                scenario,
                secondVisit.Entity,
                selectedIndex: 2);
            ConfirmConsultation(runtime, scenario, secondVisit.Entity, boardOffer, boardDelivery);
            Require(secondVisit.Entity.RequiredProductType == boards &&
                    secondVisit.Entity.RequiredProductCount == boardOffer.RequiredProductCount &&
                    secondVisit.Entity.OrderReward == boardOffer.Reward,
                "The confirmed board offer did not form the configured order.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(secondVisit.Entity.isCustomerVisitWaiting &&
                    !secondVisit.Entity.isCustomerVisitLoading,
                "The board order was accepted using only wrong-SKU cement stock.");

            DeliveryArrival secondArrival = PurchaseAndPrepareArrival(runtime, scenario, boards);
            Require(scenario.Store.Money ==
                    initialMoney - cementDelivery.TotalCost + cementOffer.Reward -
                    boardDelivery.TotalCost,
                "The second delivery did not deduct its cost exactly once.");
            TestCarryDropAndRepick(runtime, scenario, secondArrival.Products[0]);
            StoreCompleteDelivery(runtime, scenario, secondVisit.Entity, secondArrival);
            CleanupCompletedDelivery(runtime, scenario, secondArrival);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) ==
                    boardDelivery.ProductCount,
                "The board delivery did not produce the configured typed stock.");

            AttemptOrderAcceptance(runtime, scenario);
            Require(secondVisit.Entity.isCustomerVisitLoading,
                "The second customer visit did not enter loading after replenishment.");

            GameEntity wrongProduct = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .First(product => product.ProductType == cement);
            ValidateWrongSkuCannotLoad(runtime, scenario, secondVisit.Entity, wrongProduct);

            GameEntity[] secondOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == boards)
                .Take(secondVisit.Entity.RequiredProductCount)
                .ToArray();
            Require(secondOutboundProducts.Length == secondVisit.Entity.RequiredProductCount,
                "The second cycle could not resolve enough stock for its order.");
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                secondVisit.Entity,
                secondOutboundProducts);

            int expectedFinalMoney = initialMoney - cementDelivery.TotalCost +
                                     cementOffer.Reward - boardDelivery.TotalCost +
                                     boardOffer.Reward;
            Require(scenario.Store.Money == expectedFinalMoney,
                "Two cycles did not produce exactly two purchase deductions and two rewards.");
            int expectedFinalStock = cementDelivery.ProductCount + boardDelivery.ProductCount -
                                     cementOffer.RequiredProductCount -
                                     boardOffer.RequiredProductCount;
            Require(scenario.StorageZone.StorageProductCount == expectedFinalStock &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) == 2 &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) == 0,
                "The second customer cycle left an incorrect stock count.");

            int secondVisitId = secondVisit.Entity.EntityId;
            DepartAndCleanupCustomer(runtime, scenario, secondVisit, secondOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);

            Require(runtime.Game.GetEntityWithEntityId(firstVisitId) == null &&
                    runtime.Game.GetEntityWithEntityId(secondVisitId) == null &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Order).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOffer).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationVisitEntityId).count == 0,
                "A completed customer cycle retained a visit, offer or modal relation.");
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0,
                "A completed cycle retained an active delivery.");
            Require(!scenario.Player.isHandsOccupied &&
                    runtime.Game.GetEntityWithCarrierEntityId(scenario.Player.EntityId) == null,
                "The player retained a carrier relation after both cycles.");

            Debug.Log(
                $"[Hardware Store] Gameplay smoke passed: modal consultation, min/max offers, " +
                $"walking customer NPC lifecycle, cement and boards cycles, " +
                $"wrong-SKU rejection, both physics flows, " +
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
                GameMatcher.SprintSpeed)), "player");
            GameEntity store = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.EntityId,
                GameMatcher.Store,
                GameMatcher.Money,
                GameMatcher.OrderCounterEntityId,
                GameMatcher.ProcurementTerminalEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.NextOrderSequenceIndex,
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
                GameMatcher.SelectedProductType,
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
            Require(runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The smoke test must start without a customer actor.");
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
                    !visit.isOrder &&
                    visit.hasRequestedProductType &&
                    visit.hasCustomerProjectTitle &&
                    visit.hasCustomerRequest &&
                    !visit.isOrderRewarded &&
                    !visit.hasLoadedProductCount &&
                    (visit.isCustomerVisitArriving || visit.isCustomerVisitConsulting) &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationVisitEntityId).count == 0,
                "The smoke test can only reset an untouched auto-spawned customer visit.");

            int visitId = visit.EntityId;
            GameEntity actor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            EntityBehaviour view = visit.hasView
                ? visit.View as EntityBehaviour ?? throw new InvalidOperationException(
                    "The auto-spawned customer view is not an EntityBehaviour.")
                : null;
            EntityBehaviour actorView = actor != null && actor.hasView
                ? actor.View as EntityBehaviour ?? throw new InvalidOperationException(
                    "The auto-spawned customer actor view is not an EntityBehaviour.")
                : null;

            int resetSequenceIndex = Array.IndexOf(
                runtime.StaticData.ProductTypes.ToArray(),
                visit.RequestedProductType);
            Require(resetSequenceIndex >= 0,
                "The auto-spawned customer requests a product outside static data.");
            store.ReplaceNextOrderSequenceIndex(resetSequenceIndex);
            store.AddCustomerCooldownRemaining(
                runtime.StaticData.CustomerVehicle.FirstCustomerDelay);
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            int[] offerIds = offers.Select(offer => offer.EntityId).ToArray();
            foreach (GameEntity offer in offers)
                offer.isDestructed = true;
            if (actor != null)
            {
                actor.RemoveCustomerActorVisitEntityId();
                actor.isDestructed = true;
            }
            visit.RemoveCustomerVisitStoreEntityId();
            visit.isDestructed = true;
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(store.EntityId) == null &&
                    !visit.hasCustomerVisitStoreEntityId &&
                    runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    offerIds.All(id => runtime.Game.GetEntityWithEntityId(id) == null) &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0 &&
                    (view == null || !view.HasEntity) &&
                    (actorView == null || !actorView.HasEntity),
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
                    scenario.Store.hasCustomerCooldownRemaining &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
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
            Require(visit.isCustomerVisit && visit.isCustomerVehicle && !visit.isOrder &&
                    visit.isLoadingZone && visit.isCustomerVisitArriving &&
                    visit.hasRequestedProductType &&
                    visit.hasCustomerProjectTitle &&
                    visit.hasCustomerRequest &&
                    !visit.hasRequiredProductType &&
                    !visit.hasRequiredProductCount &&
                    !visit.hasAvailableProductCount &&
                    !visit.hasLoadedProductCount &&
                    !visit.hasOrderReward &&
                    !visit.isInteractable &&
                    !visit.hasView,
                "The spawned unified customer visit has an invalid arrival state.");

            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            Require(offers.Length == 3 &&
                    offers.Select(offer => offer.OfferIndex).SequenceEqual(new[] { 0, 1, 2 }) &&
                    offers.Count(offer => offer.isSelectedConsultationOffer) == 1,
                "The customer visit did not create three indexed offers with one selection.");
            foreach (GameEntity offer in offers)
            {
                Require(offer.CustomerVisitEntityId == visit.EntityId &&
                        offer.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                        offer.RequiredProductType == visit.RequestedProductType &&
                        offer.RequiredProductCount == offer.OfferIndex + 1 &&
                        !string.IsNullOrWhiteSpace(offer.OfferTitle) &&
                        !string.IsNullOrWhiteSpace(offer.OfferDescription),
                    $"Consultation offer {offer.EntityId} has invalid project data.");
            }

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour view = RequireRuntimeView(
                visit,
                runtime.StaticData.CustomerVehicle.ViewPrefab,
                $"customer visit {visit.EntityId}");
            Require(visit.hasTransform && visit.hasRigidbody && visit.hasSlots &&
                    visit.Slots.Length >= offers.Max(offer => offer.RequiredProductCount),
                "The customer visit view did not register movement and loading data.");
            Require(visit.Rigidbody.isKinematic &&
                    visit.Rigidbody.interpolation == RigidbodyInterpolation.None,
                "The customer vehicle has invalid route physics.");

            ForceRouteEndpoint(runtime, visit);
            runtime.Systems.Create<CompleteCustomerVehicleArrivalSystem>().Execute();
            Require(visit.isCustomerVisitArriving &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.isInteractable &&
                    !visit.isRouteCompleted &&
                    !visit.hasRoute &&
                    !visit.hasRouteWaypointIndex,
                "The parked vehicle did not wait for its customer to reach the counter.");

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt.IndexOf(
                        "направляется",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The parked arrival state did not present the walk to the counter.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);

            GameEntity actor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            Require(actor != null &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 1 &&
                    actor.EntityId != visit.EntityId &&
                    actor.isCustomer &&
                    actor.isCustomerApproachingCounter &&
                    !actor.isCustomerWaitingAtCounter &&
                    !actor.isCustomerReturningToVehicle &&
                    actor.isRouteMover &&
                    actor.CustomerActorVisitEntityId == visit.EntityId &&
                    actor.hasRoute &&
                    actor.hasCustomerReturnRoute &&
                    actor.hasRouteWaypointIndex &&
                    actor.hasMovementSpeed &&
                    actor.hasRotationSpeed &&
                    actor.hasWaypointTolerance &&
                    !actor.hasView,
                "Vehicle arrival did not create exactly one approaching customer actor.");

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour actorView = RequireRuntimeView(
                actor,
                runtime.StaticData.Customer.ViewPrefab,
                $"customer actor {actor.EntityId}");
            Require(actor.hasTransform && actor.hasRigidbody && actor.Rigidbody.isKinematic,
                "The customer actor view did not register route-movement data.");

            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerApproachSystem>().Execute();
            ExecuteStorageState(runtime);
            Require(visit.isCustomerVisitConsulting &&
                    !visit.isCustomerVisitArriving &&
                    !visit.isCustomerVisitWaiting &&
                    !visit.isOrder &&
                    !visit.isRouteCompleted &&
                    !visit.hasRoute &&
                    !visit.hasRouteWaypointIndex &&
                    visit.isInteractable,
                "The customer visit did not enter its parked consultation state.");
            Require(actor.isCustomerWaitingAtCounter &&
                    !actor.isCustomerApproachingCounter &&
                    !actor.isCustomerReturningToVehicle &&
                    !actor.isRouteCompleted &&
                    !actor.hasRoute &&
                    !actor.hasRouteWaypointIndex &&
                    actor.hasCustomerActorVisitEntityId &&
                    actor.CustomerActorVisitEntityId == visit.EntityId &&
                    !actor.isDestructed,
                "The customer actor did not enter its counter waiting state.");
            int typedStock = CountStockProducts(
                runtime.Game,
                scenario.StorageZone.EntityId,
                visit.RequestedProductType);
            Require(offers.All(offer => offer.AvailableProductCount == typedStock),
                "The parked consultation offers do not observe current typed stock.");

            return new CustomerVisit(visit, view, actor, actorView);
        }

        private static void OpenConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            Require(visit.isCustomerVisitConsulting && !visit.isOrder &&
                    !scenario.Player.hasConsultationVisitEntityId,
                "Only a closed pre-order consultation can be opened.");
            int[] offerIds = GetConsultationOffers(runtime.Game, visit)
                .Select(offer => offer.EntityId)
                .ToArray();

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable,
                "The consulting customer does not expose an order-counter interaction.");
            RequestInteraction(scenario.Player, scenario.OrderCounter);
            runtime.Systems.Create<ConsultationFeature>().Execute();

            Require(scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId &&
                    scenario.Player.MoveDirection == Vector3.zero &&
                    scenario.Player.isCursorLocked &&
                    visit.isCustomerVisitConsulting &&
                    !visit.isOrder &&
                    offerIds.SequenceEqual(GetConsultationOffers(runtime.Game, visit)
                        .Select(offer => offer.EntityId)),
                "The first order-counter interaction must only open the modal consultation.");
            CleanupEvents(runtime);
        }

        private static void CancelConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            Require(scenario.Player.ConsultationVisitEntityId == visit.EntityId,
                "Cannot cancel a consultation that is not open for this visit.");

            scenario.Input.isToggleCursorPressed = true;
            runtime.Systems.Create<ConsultationFeature>().Execute();
            Require(!scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.MoveDirection == Vector3.zero &&
                    scenario.Player.isCursorLocked &&
                    visit.isCustomerVisitConsulting &&
                    !visit.isOrder &&
                    offers.All(offer => !offer.isDestructed) &&
                    GetConsultationOffers(runtime.Game, visit).Length == offers.Length,
                "Cancelling the modal must preserve the consulting visit and all offers.");
            CleanupEvents(runtime);
        }

        private static void SelectConsultationOfferWithWraparound(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            int selectedIndex)
        {
            Require(scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId,
                "Offer selection requires the visit's modal consultation.");
            Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == 1,
                "Every configured consultation must start from the standard offer.");

            if (selectedIndex == 0)
            {
                CycleConsultationOffer(runtime, scenario, next: false);
                Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == 0,
                    "Previous did not select the economy offer.");
                CycleConsultationOffer(runtime, scenario, next: false);
                Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == 2,
                    "Previous did not wrap from economy to professional.");
                CycleConsultationOffer(runtime, scenario, next: true);
            }
            else if (selectedIndex == 2)
            {
                CycleConsultationOffer(runtime, scenario, next: true);
                Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == 2,
                    "Next did not select the professional offer.");
                CycleConsultationOffer(runtime, scenario, next: true);
                Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == 0,
                    "Next did not wrap from professional to economy.");
                CycleConsultationOffer(runtime, scenario, next: false);
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(selectedIndex),
                    "The smoke test explicitly covers the minimum and maximum offers.");
            }

            Require(SelectedConsultationOffer(runtime.Game, visit).OfferIndex == selectedIndex,
                $"Consultation selection did not finish on offer {selectedIndex}.");
        }

        private static void CycleConsultationOffer(
            Runtime runtime,
            Scenario scenario,
            bool next)
        {
            scenario.Input.isPreviousPressed = !next;
            scenario.Input.isNextPressed = next;
            runtime.Systems.Create<CycleConsultationOfferSystem>().Execute();
            CleanupEvents(runtime);
        }

        private static void ConfirmConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            OrderOfferDefinition expectedOffer,
            DeliveryConfig delivery)
        {
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            GameEntity selectedOffer = SelectedConsultationOffer(runtime.Game, visit);
            int[] offerIds = offers.Select(offer => offer.EntityId).ToArray();
            Require(selectedOffer.RequiredProductCount == expectedOffer.RequiredProductCount &&
                    selectedOffer.OrderReward == expectedOffer.Reward &&
                    selectedOffer.ExpectedProfit == checked(
                        expectedOffer.Reward -
                        delivery.PurchaseUnitPrice * expectedOffer.RequiredProductCount),
                "The selected offer does not match its static configuration.");

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                "Modal consultation did not capture repeated E from world interaction.");
            runtime.Systems.Create<ConsultationFeature>().Execute();

            Require(scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId &&
                    visit.isCustomerVisitConsulting &&
                    !visit.isOrder &&
                    offers.All(offer => !offer.isDestructed) &&
                    selectedOffer.isSelectedConsultationOffer,
                "Repeated E in the modal must neither interact with the world nor confirm an offer.");
            CleanupEvents(runtime);

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<ConsultationFeature>().Execute();

            Require(!scenario.Player.hasConsultationVisitEntityId &&
                    visit.isOrder &&
                    visit.isCustomerVisitWaiting &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.hasRequestedProductType &&
                    visit.RequiredProductType == selectedOffer.RequiredProductType &&
                    visit.RequiredProductCount == selectedOffer.RequiredProductCount &&
                    visit.AvailableProductCount == selectedOffer.AvailableProductCount &&
                    visit.LoadedProductCount == 0 &&
                    visit.OrderReward == selectedOffer.OrderReward &&
                    visit.ExpectedProfit == selectedOffer.ExpectedProfit &&
                    offers.All(offer => offer.isDestructed),
                "Confirming an offer did not create exactly one waiting order on the visit.");
            CleanupEvents(runtime);

            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(offerIds.All(id => runtime.Game.GetEntityWithEntityId(id) == null) &&
                    GetConsultationOffers(runtime.Game, visit).Length == 0,
                "Confirmed consultation offers survived the Destructed cleanup pipeline.");
        }

        private static GameEntity[] GetConsultationOffers(
            GameContext context,
            GameEntity visit) =>
            context.GetEntitiesWithCustomerVisitEntityId(visit.EntityId)
                .Where(entity => entity.isConsultationOffer && !entity.isDestructed)
                .OrderBy(entity => entity.OfferIndex)
                .ToArray();

        private static GameEntity SelectedConsultationOffer(
            GameContext context,
            GameEntity visit)
        {
            GameEntity[] selected = GetConsultationOffers(context, visit)
                .Where(offer => offer.isSelectedConsultationOffer)
                .ToArray();
            Require(selected.Length == 1,
                $"Customer visit {visit.EntityId} must have exactly one selected offer.");
            return selected[0];
        }

        private static void ForceRouteEndpoint(Runtime runtime, GameEntity routeMover)
        {
            Require(routeMover.isRouteMover &&
                    routeMover.hasRoute &&
                    routeMover.Route.Length >= 2,
                $"Route mover {routeMover.EntityId} has no route to force.");
            Pose destination = routeMover.Route[^1];
            routeMover.ReplaceRouteWaypointIndex(routeMover.Route.Length - 1);
            routeMover.Rigidbody.position = destination.position;
            routeMover.Rigidbody.rotation = destination.rotation;
            routeMover.Transform.SetPositionAndRotation(destination.position, destination.rotation);
            Physics.SyncTransforms();
            runtime.Systems.Create<MoveRouteSystem>().Execute();
            Require(routeMover.isRouteCompleted,
                $"Route mover {routeMover.EntityId} did not complete its forced route.");
        }

        private static DeliveryArrival PurchaseAndPrepareArrival(
            Runtime runtime,
            Scenario scenario,
            ProductTypeId productType)
        {
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "A new delivery cannot be purchased while another is active.");

            SelectProcurementProduct(runtime, scenario, productType);
            var deliveryConfig = runtime.StaticData.GetDelivery(productType);
            var productConfig = runtime.StaticData.GetProduct(productType);
            int moneyBeforePurchase = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            GameEntity delivery = runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                scenario.ProcurementTerminal.EntityId);
            Require(delivery != null &&
                    delivery.hasDeliveryProcurementTerminalEntityId &&
                    delivery.DeliveryProcurementTerminalEntityId ==
                    scenario.ProcurementTerminal.EntityId &&
                    delivery.isDeliveryActive &&
                    delivery.ProductType == productType &&
                    delivery.DeliveryProductCount == deliveryConfig.ProductCount &&
                    delivery.DeliveryCost == deliveryConfig.TotalCost &&
                    scenario.Store.Money == moneyBeforePurchase - deliveryConfig.TotalCost,
                "Purchasing did not create an indexed active delivery.");
            CleanupEvents(runtime);

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour deliveryView = RequireRuntimeView(
                delivery,
                deliveryConfig.ViewPrefab,
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
                    productConfig.ViewPrefab,
                    $"product {product.EntityId}");
                Require(product.ProductType == productType &&
                        product.UnitPrice == productConfig.UnitPrice &&
                        Mathf.Approximately(product.ProductMass, productConfig.Mass) &&
                        Mathf.Approximately(
                            product.CarryMovementSpeed,
                            productConfig.CarryMovementSpeed) &&
                        product.isInboundProduct &&
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

        private static void ValidateRejectedDeliveryPurchase(
            Runtime runtime,
            Scenario scenario,
            ProductTypeId selectedProductType,
            string failureMessage)
        {
            SelectProcurementProduct(runtime, scenario, selectedProductType);
            int moneyBefore = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    scenario.Store.Money == moneyBefore,
                failureMessage);
            CleanupEvents(runtime);
        }

        private static void SelectProcurementProduct(
            Runtime runtime,
            Scenario scenario,
            ProductTypeId productType)
        {
            Require(runtime.StaticData.ProductTypes.Contains(productType),
                $"Product type {productType} is absent from static data.");
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "Procurement selection cannot change during an active delivery.");

            int stepCount = 0;
            while (scenario.ProcurementTerminal.SelectedProductType != productType &&
                   stepCount < runtime.StaticData.ProductTypes.Count)
            {
                scenario.Player.ReplaceFocusedEntityId(scenario.ProcurementTerminal.EntityId);
                scenario.Player.ReplaceFocusedInteractionType(
                    InteractionTypeId.ProcurementTerminal);
                scenario.Input.isNextPressed = true;
                runtime.Systems.Create<ChangeProcurementSelectionSystem>().Execute();
                CleanupEvents(runtime);
                stepCount++;
            }

            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            if (scenario.Player.hasFocusedInteractionType)
                scenario.Player.RemoveFocusedInteractionType();

            Require(scenario.ProcurementTerminal.SelectedProductType == productType,
                $"Procurement selection did not reach {productType}.");
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
                    product.CarryMovementSpeed),
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
                    product.Rigidbody.useGravity &&
                    product.Rigidbody.detectCollisions &&
                    Mathf.Approximately(product.Rigidbody.mass, product.ProductMass) &&
                    product.Rigidbody.interpolation == product.RigidbodyInterpolationMode &&
                    product.Rigidbody.collisionDetectionMode ==
                    product.RigidbodyCollisionDetectionMode,
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
                        visit.AvailableProductCount == CountStockProducts(
                            runtime.Game,
                            scenario.StorageZone.EntityId,
                            visit.RequiredProductType),
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

        private static void ValidateWrongSkuCannotLoad(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity wrongProduct)
        {
            Require(visit.isCustomerVisitLoading &&
                    wrongProduct.isInStock &&
                    wrongProduct.ProductType != visit.RequiredProductType &&
                    wrongProduct.hasStorageSlotIndex,
                "Wrong-SKU loading check requires a slotted stock product of another type.");

            int storageSlotIndex = wrongProduct.StorageSlotIndex;
            int loadedBefore = visit.LoadedProductCount;
            wrongProduct.RemoveStorageSlotIndex();
            wrongProduct.AddCarrierEntityId(scenario.Player.EntityId);
            wrongProduct.isInteractable = false;
            wrongProduct.isProductPlacementDirty = true;
            scenario.Player.isHandsOccupied = true;
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowHeldProductSystem>().Execute();

            RequestInteraction(scenario.Player, visit);
            runtime.Systems.Create<LoadHeldProductSystem>().Execute();
            Require(visit.LoadedProductCount == loadedBefore &&
                    !wrongProduct.isProductLoaded &&
                    !wrongProduct.isLoaded &&
                    wrongProduct.isInStock &&
                    wrongProduct.hasCarrierEntityId &&
                    wrongProduct.CarrierEntityId == scenario.Player.EntityId &&
                    scenario.Player.isHandsOccupied,
                "A wrong-SKU stock product was loaded into the customer vehicle.");
            CleanupEvents(runtime);

            wrongProduct.RemoveCarrierEntityId();
            wrongProduct.AddStorageSlotIndex(storageSlotIndex);
            wrongProduct.isInteractable = true;
            wrongProduct.isProductPlacementDirty = true;
            scenario.Player.isHandsOccupied = false;
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);

            Require(wrongProduct.isInStock &&
                    wrongProduct.hasStorageSlotIndex &&
                    wrongProduct.StorageSlotIndex == storageSlotIndex &&
                    wrongProduct.Transform.parent ==
                    scenario.StorageZone.Slots[storageSlotIndex] &&
                    !wrongProduct.isProductPlacementDirty &&
                    runtime.Game.GetEntityWithCarrierEntityId(
                        scenario.Player.EntityId) == null,
                "Wrong-SKU loading check did not restore the stock product cleanly.");
        }

        private static void ValidatePhysicalStockFocus(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            Require(product.isInStock && product.isInteractable && product.hasColliders,
                "Physical focus check requires an interactable stock product.");
            Collider solidCollider = product.Colliders.Single(collider => !collider.isTrigger);
            Transform cameraTransform = scenario.Player.Camera.transform;
            Vector3 originalPosition = cameraTransform.position;
            Quaternion originalRotation = cameraTransform.rotation;

            try
            {
                Vector3 target = solidCollider.bounds.center;
                cameraTransform.SetPositionAndRotation(
                    target - Vector3.forward * 1.8f,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));
                Physics.SyncTransforms();
                if (scenario.Player.hasFocusedEntityId)
                    scenario.Player.RemoveFocusedEntityId();
                if (scenario.Player.hasFocusedInteractionType)
                    scenario.Player.RemoveFocusedInteractionType();

                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<ClassifyFocusedInteractionSystem>().Execute();
                Require(scenario.Player.hasFocusedEntityId &&
                        scenario.Player.FocusedEntityId == product.EntityId &&
                        scenario.Player.hasFocusedInteractionType &&
                        scenario.Player.FocusedInteractionType == InteractionTypeId.Product,
                    "Physical interaction focus did not resolve the stocked product; " +
                    "a storage-zone trigger may be occluding it.");
            }
            finally
            {
                cameraTransform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                if (scenario.Player.hasFocusedEntityId)
                    scenario.Player.RemoveFocusedEntityId();
                if (scenario.Player.hasFocusedInteractionType)
                    scenario.Player.RemoveFocusedInteractionType();
            }
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
                string displayName = runtime.StaticData
                    .GetProduct(product.ProductType)
                    .DisplayName;
                scenario.Player.ReplaceFocusedEntityId(product.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(scenario.Player.isFocusInteractionAvailable &&
                        scenario.Player.InteractionPrompt ==
                        $"E — взять со склада • товар: {displayName}",
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
            GameEntity actor = visit.Actor;
            Require(entity.isCustomerVisitCompleted &&
                    entity.isOrderRewarded &&
                    !entity.hasCustomerDepartureDelayRemaining &&
                    actor.isCustomerWaitingAtCounter &&
                    actor.CustomerActorVisitEntityId == entity.EntityId,
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
            runtime.Systems.Create<BeginCustomerReturnSystem>().Execute();
            Require(entity.isCustomerVisitReturning &&
                    !entity.isCustomerVisitCompleted &&
                    !entity.isCustomerVisitDeparting &&
                    !entity.isInteractable &&
                    !entity.hasCustomerDepartureDelayRemaining &&
                    !entity.hasRoute &&
                    actor.isCustomerReturningToVehicle &&
                    !actor.isCustomerWaitingAtCounter &&
                    actor.hasRoute &&
                    actor.hasRouteWaypointIndex &&
                    actor.hasCustomerActorVisitEntityId &&
                    !actor.isDestructed,
                "The customer did not begin returning to the parked vehicle.");

            ValidateReturningInteractionPrompts(runtime, scenario, entity);

            runtime.Systems.Create<CompleteCustomerReturnSystem>().Execute();
            runtime.Systems.Create<CompleteCustomerVehicleDepartureSystem>().Execute();
            Require(entity.isCustomerVisitReturning &&
                    !entity.isCustomerVisitDeparting &&
                    !entity.hasRoute &&
                    actor.hasCustomerActorVisitEntityId &&
                    !actor.isDestructed,
                "The vehicle departed before its customer completed the return route.");

            int actorId = actor.EntityId;
            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerReturnSystem>().Execute();
            Require(entity.isCustomerVisitDeparting &&
                    !entity.isCustomerVisitReturning &&
                    entity.hasRoute &&
                    entity.hasRouteWaypointIndex &&
                    actor.isDestructed &&
                    !actor.hasCustomerActorVisitEntityId &&
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(entity.EntityId) == null,
                "The completed customer return did not release the primary relation before " +
                "starting vehicle departure.");

            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            Require(!visit.ActorView.HasEntity && visit.View.HasEntity,
                "Customer return cleanup did not release only the actor view.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(actorId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The returned customer actor survived the Destructed pipeline.");

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
                    !visit.ActorView.HasEntity &&
                    loadedViews.All(view => !view.HasEntity),
                "Customer cleanup retained a nested view binding.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            CleanupEvents(runtime);

            Require(runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    loadedProductIds.All(productId =>
                        runtime.Game.GetEntityWithEntityId(productId) == null) &&
                    runtime.Game.GetGroup(GameMatcher.Loaded).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0 &&
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(visitId) == null,
                "A departed customer graph survived cleanup.");
            ExecuteStorageState(runtime);
        }

        private static void ValidateReturningInteractionPrompts(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt.IndexOf(
                        "возвращается",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The order counter remained interactive while the customer was returning.");

            scenario.Player.ReplaceFocusedEntityId(scenario.ProcurementTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt.IndexOf(
                        "заказ выполнен",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Procurement remained available while the customer was returning.");

            int moneyBeforeRejectedPurchase = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            Require(scenario.Store.Money == moneyBeforeRejectedPurchase &&
                    runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "A delivery was purchased while the customer was returning.");
            CleanupEvents(runtime);

            Require(!visit.isInteractable,
                "The loading zone remained interactable while the customer was returning.");

            GameEntity stockProduct = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .First();
            scenario.Player.ReplaceFocusedEntityId(stockProduct.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasInteractionPrompt &&
                    scenario.Player.InteractionPrompt.IndexOf(
                        "возвращается",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Stock remained available while the customer was returning.");

            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
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
                        scenario.Store.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
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

        private static void ExecuteInteractionPrompts(Runtime runtime)
        {
            runtime.Systems.Create<ClassifyFocusedInteractionSystem>().Execute();
            runtime.Systems.Create<InteractionPromptFeature>().Execute();
        }

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

        private static int CountStockProducts(
            GameContext context,
            int storageZoneEntityId,
            ProductTypeId productType) =>
            FindStockProducts(context, storageZoneEntityId)
                .Count(product => product.ProductType == productType);

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
            public CustomerVisit(
                GameEntity entity,
                EntityBehaviour view,
                GameEntity actor,
                EntityBehaviour actorView)
            {
                Entity = entity;
                View = view;
                Actor = actor;
                ActorView = actorView;
            }

            public GameEntity Entity { get; }
            public EntityBehaviour View { get; }
            public GameEntity Actor { get; }
            public EntityBehaviour ActorView { get; }
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
