using System;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Common.Economy;
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
using HardwareStore.Gameplay.Features.Player.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Features.Procurement;
using HardwareStore.Gameplay.Features.Procurement.Systems;
using HardwareStore.Gameplay.Features.Products;
using HardwareStore.Gameplay.Features.StorageState;
using HardwareStore.Gameplay.Features.StoreSceneBindings.Systems;
using HardwareStore.Gameplay.Features.Trolley.Systems;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Presentation;
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
                $"{visit.Entity.EntityId}, project '" +
                $"{runtime.Localization.Resolve(LocalizedTexts.ProjectTitle(visit.Entity.CustomerProjectType))}' and " +
                $"{GetConsultationOffers(runtime.Game, visit.Entity).Length} offers.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Mixed Consultation Visual Check")]
        public static void PrepareMixedConsultationVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int mixedProjectIndex = runtime.StaticData.ProjectTypes
                .Select((projectType, index) => (projectType, index))
                .Single(item =>
                    item.projectType == CustomerProjectTypeId.WorkbenchFoundation)
                .index;
            scenario.Store.ReplaceNextProjectSequenceIndex(mixedProjectIndex);

            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            Require(visit.Entity.CustomerProjectType ==
                    CustomerProjectTypeId.WorkbenchFoundation,
                "The mixed consultation visual check did not spawn the workbench project.");
            OpenConsultation(runtime, scenario, visit.Entity);
            runtime.Systems.Create<PresentConsultationSystem>().Execute();

            Debug.Log(
                $"[Hardware Store] Mixed consultation visual check prepared: customer visit " +
                $"{visit.Entity.EntityId}, project '" +
                $"{runtime.Localization.Resolve(LocalizedTexts.ProjectTitle(visit.Entity.CustomerProjectType))}' and " +
                $"capacity {runtime.StaticData.CustomerVehicle.CargoCapacity}.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Procurement Visual Check")]
        public static void PrepareProcurementVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            CustomerProjectConfig project = runtime.StaticData.GetProject(
                visit.Entity.CustomerProjectType);

            OpenConsultation(runtime, scenario, visit.Entity);
            GameEntity selectedOffer = SelectedConsultationOffer(runtime.Game, visit.Entity);
            ConfirmConsultation(
                runtime,
                scenario,
                visit.Entity,
                project.Offers[selectedOffer.OfferIndex]);
            OpenProcurement(runtime, scenario);
            runtime.Systems.Create<PresentProcurementSystem>().Execute();

            Debug.Log(
                $"[Hardware Store] Procurement visual check prepared: customer project " +
                $"'{runtime.Localization.Resolve(LocalizedTexts.ProjectTitle(visit.Entity.CustomerProjectType))}', selected product " +
                $"{scenario.ProcurementTerminal.SelectedProductType}.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Platform Trolley Visual Check")]
        public static void PreparePlatformTrolleyVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            scenario.Store.ReplaceCompletedOrderCount(
                runtime.StaticData.PlatformTrolley.RequiredCompletedOrderCount);
            UnlockTrolleyUpgrade(runtime, scenario);
            GameEntity trolley = PurchaseTrolley(runtime, scenario);
            Selection.activeGameObject = trolley.View.gameObject;

            Debug.Log(
                $"[Hardware Store] Platform trolley visual check prepared: entity " +
                $"{trolley.EntityId}, capacity {trolley.TrolleyCapacity}, movement " +
                $"{trolley.TrolleyMovementSpeed:0.##}.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int initialMoney = scenario.Store.Money;
            ProductTypeId cement = ProductTypeId.CementBag;
            ProductTypeId boards = ProductTypeId.BoardBundle;
            DeliveryConfig cementDelivery = runtime.StaticData.GetDelivery(cement);
            DeliveryConfig boardDelivery = runtime.StaticData.GetDelivery(boards);
            CustomerProjectConfig cementProject = runtime.StaticData.GetProject(
                CustomerProjectTypeId.CementFoundation);
            CustomerProjectConfig boardProject = runtime.StaticData.GetProject(
                CustomerProjectTypeId.LumberShelving);
            CustomerProjectConfig mixedProject = runtime.StaticData.GetProject(
                CustomerProjectTypeId.WorkbenchFoundation);
            CustomerProjectOfferDefinition cementOffer = cementProject.Offers[2];
            CustomerProjectOfferDefinition boardOffer = boardProject.Offers[2];
            CustomerProjectOfferDefinition mixedOffer = mixedProject.Offers[1];
            int cementReward = CalculateReward(runtime, cementOffer);
            int boardReward = CalculateReward(runtime, boardOffer);
            int mixedReward = CalculateReward(runtime, mixedOffer);
            int cementOrderCount = RequiredCount(cementOffer, cement);
            int boardOrderCount = RequiredCount(boardOffer, boards);
            int mixedCementCount = RequiredCount(mixedOffer, cement);
            int mixedBoardCount = RequiredCount(mixedOffer, boards);

            ValidateRussianLocalization(CreateRussianLocalization());
            ValidateRussianLocalization(runtime.Localization);
            ValidateRuntimePlayerView(scenario.Player);
            Require(scenario.Player.WalkSpeed < scenario.Player.SprintSpeed,
                "Player movement config must define walking < sprinting speeds.");
            Require(runtime.StaticData.ProductTypes.SequenceEqual(new[] { cement, boards }),
                "The smoke test requires the stable CementBag -> BoardBundle order sequence.");
            Require(runtime.StaticData.ProjectTypes.SequenceEqual(new[]
                {
                    CustomerProjectTypeId.CementFoundation,
                    CustomerProjectTypeId.LumberShelving,
                    CustomerProjectTypeId.WorkbenchFoundation
                }),
                "The smoke test requires the stable cement -> lumber -> workbench sequence.");
            Require(ReferenceEquals(
                    runtime.ProcurementSolvency,
                    runtime.EconomySolvency),
                "Procurement and generic debit evaluation must share one solvency singleton.");
            Require(scenario.Store.NextProjectSequenceIndex == 0,
                "A fresh store must begin with the first configured project type.");
            Require(runtime.StaticData.CustomerVehicle.CargoCapacity == 3,
                "The mixed-order smoke requires a three-slot customer vehicle.");
            Require(Mathf.Approximately(
                    runtime.StaticData.ProductRecovery.MinimumWorldY,
                    -10f),
                "The prototype smoke requires product recovery below world Y -10.");
            Require(runtime.StaticData.PlatformTrolley.PurchasePrice == 200 &&
                    runtime.StaticData.PlatformTrolley.RequiredCompletedOrderCount == 2 &&
                    runtime.StaticData.PlatformTrolley.Capacity == 3 &&
                    Mathf.Approximately(
                        runtime.StaticData.PlatformTrolley.MovementSpeed,
                        3.8f) &&
                    Mathf.Approximately(
                        runtime.StaticData.PlatformTrolley.FollowDistance,
                        1.7f),
                "The trolley smoke requires price 200, two-order unlock, capacity 3, " +
                "movement speed 3.8 and follow distance 1.7.");
            ValidateTrolleyLockedAtProgress(runtime, scenario, expectedCompletedOrders: 0);
            ValidateCooldownPresentation(runtime, scenario);
            ValidateRejectedDeliveryPurchase(
                runtime,
                scenario,
                cement,
                boards,
                ProcurementDemandKind.ProjectForecast,
                LocalizationKey.NotificationPurchaseWouldBlockForecast,
                "An unsafe wrong-SKU forecast purchase changed money or delivery state.");

            DeliveryArrival firstArrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                cement,
                validateModalControls: true);
            Require(scenario.Store.Money == initialMoney - cementDelivery.TotalCost,
                "The no-customer cement pre-purchase did not deduct its cost exactly once.");
            ValidateInboundProductRecovery(runtime, scenario, firstArrival);
            TestCarryDropAndRepick(runtime, scenario, firstArrival.Products[0]);
            StoreCompleteDelivery(runtime, scenario, null, firstArrival);
            CleanupCompletedDelivery(runtime, scenario, firstArrival);
            ValidatePhysicalStockFocus(runtime, scenario, firstArrival.Products[0]);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) ==
                    cementDelivery.ProductCount,
                "The no-customer pre-purchase did not enter storage.");
            ValidateTrolleyPurchaseSafetyReserve(runtime, scenario, cement);

            CustomerVisit firstVisit = SpawnAndParkCustomer(runtime, scenario);
            int firstCustomerActorId = firstVisit.Actor.EntityId;
            Require(firstVisit.Entity.CustomerProjectType ==
                    CustomerProjectTypeId.CementFoundation &&
                    scenario.Store.NextProjectSequenceIndex == 1,
                "The first customer visit did not receive the configured cement project.");
            Require(GetConsultationOffers(runtime.Game, firstVisit.Entity)
                        .SelectMany(offer => GetConsultationOfferLines(runtime.Game, offer))
                        .All(line => line.ProductType == cement &&
                                     line.AvailableProductCount ==
                                     cementDelivery.ProductCount),
                "Pre-purchased cement stock was not counted by the next consultation.");

            OpenConsultation(runtime, scenario, firstVisit.Entity);
            CancelConsultation(runtime, scenario, firstVisit.Entity);
            OpenConsultation(runtime, scenario, firstVisit.Entity);
            SelectConsultationOfferWithWraparound(
                runtime,
                scenario,
                firstVisit.Entity,
                selectedIndex: 2);
            GameEntity[] firstOrderLines = ConfirmConsultation(
                runtime, scenario, firstVisit.Entity, cementOffer);

            Require(firstVisit.Entity.isCustomerVisitLoading,
                "Confirming the stocked cement offer did not activate its order immediately.");

            GameEntity[] firstOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == cement)
                .Take(TotalRequiredCount(cementOffer))
                .ToArray();
            Require(firstOutboundProducts.Length == TotalRequiredCount(cementOffer),
                "The first cycle could not resolve enough stock for its order.");
            ValidateStockReservationAndRecovery(
                runtime,
                scenario,
                firstVisit.Entity,
                firstOutboundProducts[0]);
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                firstVisit.Entity,
                firstOutboundProducts);
            Require(scenario.Store.Money ==
                    initialMoney - cementDelivery.TotalCost + cementReward,
                "The first cycle balance is not purchase cost plus exactly one reward.");
            RegisterRewardedOrderForTrolleyProgression(
                runtime,
                scenario,
                firstVisit.Entity,
                expectedCompletedOrders: 1);
            ValidateTrolleyLockedAtProgress(runtime, scenario, expectedCompletedOrders: 1);

            int firstVisitId = firstVisit.Entity.EntityId;
            DepartAndCleanupCustomer(
                runtime, scenario, firstVisit, firstOrderLines, firstOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) ==
                    cementDelivery.ProductCount - cementOrderCount &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) == 0,
                "The first cement cycle did not consume the pre-purchased batch exactly.");

            CustomerVisit secondVisit = SpawnAndParkCustomer(runtime, scenario);
            Require(secondVisit.Entity.EntityId != firstVisitId,
                "The second customer visit reused the first visit identifier.");
            Require(secondVisit.Actor.EntityId != firstCustomerActorId,
                "The second customer actor reused the first actor identifier.");
            Require(secondVisit.Entity.Slots.All(slot => slot.childCount == 0),
                "The second customer vehicle inherited occupied loading slots.");
            Require(secondVisit.Entity.CustomerProjectType ==
                    CustomerProjectTypeId.LumberShelving &&
                    scenario.Store.NextProjectSequenceIndex == 2,
                "The second customer visit did not receive the configured board project.");
            Require(GetConsultationOffers(runtime.Game, secondVisit.Entity)
                        .SelectMany(offer => GetConsultationOfferLines(runtime.Game, offer))
                        .All(line => line.ProductType == boards &&
                                     line.AvailableProductCount == 0) &&
                    scenario.StorageZone.StorageProductCount ==
                    cementDelivery.ProductCount - cementOrderCount,
                "Wrong-SKU cement stock was counted as available for a board offer.");

            OpenConsultation(runtime, scenario, secondVisit.Entity);
            SelectConsultationOfferWithWraparound(
                runtime,
                scenario,
                secondVisit.Entity,
                selectedIndex: 2);
            GameEntity[] secondOrderLines = ConfirmConsultation(
                runtime, scenario, secondVisit.Entity, boardOffer);

            Require(secondVisit.Entity.isCustomerVisitLoading,
                "Confirming the board offer did not activate loading while stock was pending.");

            ProcurementSnapshot confirmedSnapshot = CaptureProcurementSnapshot(
                runtime,
                scenario);
            ProcurementProductSnapshot safeWrongSkuCard = confirmedSnapshot.Products
                .Single(product => product.ProductType == cement);
            ProcurementProductSnapshot requiredSkuCard = confirmedSnapshot.Products
                .Single(product => product.ProductType == boards);
            Require(confirmedSnapshot.DemandKind == ProcurementDemandKind.ConfirmedOrder &&
                    safeWrongSkuCard.PurchaseAvailable &&
                    safeWrongSkuCard.RemainingRequiredProductCount == 0 &&
                    safeWrongSkuCard.DeficitProductCount == 0 &&
                    requiredSkuCard.PurchaseAvailable &&
                    requiredSkuCard.MinimumRequiredProductCount == boardOrderCount &&
                    requiredSkuCard.MaximumRequiredProductCount == boardOrderCount &&
                    requiredSkuCard.RemainingRequiredProductCount == boardOrderCount &&
                    requiredSkuCard.DeficitProductCount == boardOrderCount,
                "A solvent active order did not expose exact demand or allow both safe SKUs.");
            CancelProcurement(runtime, scenario, scenario.Store.Money);

            DeliveryArrival reserveCementArrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                cement);
            Require(scenario.Store.Money ==
                    initialMoney - cementDelivery.TotalCost + cementReward -
                    cementDelivery.TotalCost,
                "A safe active-order purchase of another SKU charged incorrectly.");
            StoreCompleteDelivery(
                runtime,
                scenario,
                secondVisit.Entity,
                reserveCementArrival);
            CleanupCompletedDelivery(runtime, scenario, reserveCementArrival);

            DeliveryArrival secondArrival = PurchaseAndPrepareArrival(runtime, scenario, boards);
            Require(scenario.Store.Money ==
                    initialMoney - cementDelivery.TotalCost + cementReward -
                    cementDelivery.TotalCost -
                    boardDelivery.TotalCost,
                "The second delivery did not deduct its cost exactly once.");
            TestCarryDropAndRepick(runtime, scenario, secondArrival.Products[0]);
            StoreCompleteDelivery(runtime, scenario, secondVisit.Entity, secondArrival);
            CleanupCompletedDelivery(runtime, scenario, secondArrival);
            Require(CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) ==
                    boardDelivery.ProductCount,
                "The board delivery did not produce the configured typed stock.");

            Require(secondVisit.Entity.isCustomerVisitLoading,
                "The active board order left loading state during replenishment.");

            GameEntity wrongProduct = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .First(product => product.ProductType == cement);
            ValidateWrongSkuCannotLoad(runtime, scenario, secondVisit.Entity, wrongProduct);

            GameEntity[] secondOutboundProducts = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == boards)
                .Take(TotalRequiredCount(boardOffer))
                .ToArray();
            Require(secondOutboundProducts.Length == TotalRequiredCount(boardOffer),
                "The second cycle could not resolve enough stock for its order.");
            LoadAndRewardCustomerOrder(
                runtime,
                scenario,
                secondVisit.Entity,
                secondOutboundProducts);

            int moneyAfterTwoCyclesBeforeTrolley = initialMoney -
                                                   cementDelivery.TotalCost * 2 +
                                                   cementReward -
                                                   boardDelivery.TotalCost + boardReward;
            Require(scenario.Store.Money == moneyAfterTwoCyclesBeforeTrolley,
                "Two cycles did not produce three purchase deductions and two rewards.");
            RegisterRewardedOrderForTrolleyProgression(
                runtime,
                scenario,
                secondVisit.Entity,
                expectedCompletedOrders: 2);
            UnlockTrolleyUpgrade(runtime, scenario);
            GameEntity trolley = PurchaseTrolley(runtime, scenario);
            int moneyAfterTwoCycles = moneyAfterTwoCyclesBeforeTrolley -
                                      runtime.StaticData.PlatformTrolley.PurchasePrice;
            Require(scenario.Store.Money == moneyAfterTwoCycles,
                "The platform trolley purchase did not debit its price exactly once.");
            int stockAfterTwoCycles = cementDelivery.ProductCount * 2 +
                                      boardDelivery.ProductCount -
                                      TotalRequiredCount(cementOffer) -
                                      TotalRequiredCount(boardOffer);
            Require(scenario.StorageZone.StorageProductCount == stockAfterTwoCycles &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) ==
                    cementDelivery.ProductCount * 2 - cementOrderCount &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) ==
                    boardDelivery.ProductCount - boardOrderCount,
                "The second customer cycle left an incorrect stock count.");

            int secondVisitId = secondVisit.Entity.EntityId;
            DepartAndCleanupCustomer(
                runtime, scenario, secondVisit, secondOrderLines, secondOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);
            ValidateNoCustomerTrolleyControls(runtime, scenario, trolley);

            CustomerVisit thirdVisit = SpawnAndParkCustomer(runtime, scenario);
            Require(thirdVisit.Entity.CustomerProjectType ==
                    CustomerProjectTypeId.WorkbenchFoundation &&
                    scenario.Store.NextProjectSequenceIndex == 0,
                "The third customer visit did not receive the mixed workbench project.");
            ValidateConsultationLineAvailability(runtime, scenario, thirdVisit.Entity);

            OpenConsultation(runtime, scenario, thirdVisit.Entity);
            Require(SelectedConsultationOffer(runtime.Game, thirdVisit.Entity).OfferIndex == 1,
                "The mixed project did not select its configured C2+B1 default offer.");
            GameEntity[] thirdOrderLines = ConfirmConsultation(
                runtime, scenario, thirdVisit.Entity, mixedOffer);
            Require(thirdOrderLines.Length == mixedOffer.Lines.Count &&
                    FindOrderLine(thirdOrderLines, cement).RequiredProductCount == 2 &&
                    FindOrderLine(thirdOrderLines, boards).RequiredProductCount == 1,
                "The mixed consultation did not create the C2+B1 order lines.");

            Require(thirdVisit.Entity.isCustomerVisitLoading,
                "Confirming the mixed offer did not activate its order immediately.");
            DeliveryArrival mixedBoardArrival = PurchaseAndPrepareArrival(
                runtime, scenario, boards);
            Require(scenario.Store.Money == moneyAfterTwoCycles - boardDelivery.TotalCost,
                "The mixed cycle board delivery did not deduct its cost exactly once.");
            StoreCompleteDelivery(runtime, scenario, thirdVisit.Entity, mixedBoardArrival);
            CleanupCompletedDelivery(runtime, scenario, mixedBoardArrival);
            Require(thirdVisit.Entity.isCustomerVisitLoading,
                "The active mixed order left loading state during replenishment.");

            GameEntity mixedBoard = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .First(product => product.ProductType == boards);
            GameEntity extraBoard = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .First(product => product.ProductType == boards &&
                                  !ReferenceEquals(product, mixedBoard));
            LoadProductOnTrolley(runtime, scenario, trolley, mixedBoard);
            ValidateRejectedStockProductCannotLoad(
                runtime,
                scenario,
                thirdVisit.Entity,
                extraBoard,
                "A board beyond the trolley-reserved mixed order quota was picked up.");

            GameEntity[] mixedCement = FindStockProducts(runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == cement)
                .Take(mixedCementCount)
                .ToArray();
            Require(mixedCement.Length == mixedCementCount,
                "The mixed cycle could not resolve its two already-stocked cement bags.");
            GameEntity[] thirdOutboundProducts = new[] { mixedBoard }
                .Concat(mixedCement)
                .ToArray();
            LoadProductOnTrolley(runtime, scenario, trolley, mixedCement[0]);
            LoadProductOnTrolley(runtime, scenario, trolley, mixedCement[1]);
            ValidateLoadedTrolleyCargo(
                runtime,
                scenario,
                trolley,
                thirdOutboundProducts);
            ValidateTrolleyCargoInputRouting(
                runtime,
                scenario,
                trolley,
                thirdOutboundProducts[0]);
            ValidateFullTrolleyRejectsFourthProduct(
                runtime,
                scenario,
                trolley,
                thirdVisit.Entity,
                extraBoard);
            ValidateTrolleyPushFlow(runtime, scenario, trolley);

            for (int productIndex = 0;
                 productIndex < thirdOutboundProducts.Length;
                 productIndex++)
            {
                LoadTrolleyProductIntoOrder(
                    runtime,
                    scenario,
                    thirdVisit.Entity,
                    trolley,
                    thirdOutboundProducts[productIndex],
                    expectCompleted: productIndex == thirdOutboundProducts.Length - 1);
            }

            RewardCustomerOrder(runtime, scenario, thirdVisit.Entity, thirdOutboundProducts);
            RegisterRewardedOrderForTrolleyProgression(
                runtime,
                scenario,
                thirdVisit.Entity,
                expectedCompletedOrders: 3);
            int expectedFinalMoney = moneyAfterTwoCycles - boardDelivery.TotalCost + mixedReward;
            Require(scenario.Store.Money == expectedFinalMoney,
                "The mixed cycle did not apply one board purchase and one derived reward.");
            int expectedFinalStock = stockAfterTwoCycles + boardDelivery.ProductCount -
                                     TotalRequiredCount(mixedOffer);
            int expectedFinalCementStock = cementDelivery.ProductCount * 2 -
                                           cementOrderCount - mixedCementCount;
            int expectedFinalBoardStock = boardDelivery.ProductCount * 2 -
                                          boardOrderCount - mixedBoardCount;
            Require(scenario.StorageZone.StorageProductCount == expectedFinalStock &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, cement) ==
                    expectedFinalCementStock &&
                    CountStockProducts(runtime.Game, scenario.StorageZone.EntityId, boards) ==
                    expectedFinalBoardStock,
                "The mixed customer cycle left an incorrect typed stock count.");

            int thirdVisitId = thirdVisit.Entity.EntityId;
            DepartAndCleanupCustomer(
                runtime, scenario, thirdVisit, thirdOrderLines, thirdOutboundProducts);
            ValidateCooldownSafety(runtime, scenario);

            Require(runtime.Game.GetEntityWithEntityId(firstVisitId) == null &&
                    runtime.Game.GetEntityWithEntityId(secondVisitId) == null &&
                    runtime.Game.GetEntityWithEntityId(thirdVisitId) == null &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Order).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderLine).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOffer).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferLine).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderLineEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.DeliveryEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ReservedOrderLineEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ReservedDeliverySlotIndex).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ReservedStorageSlotIndex).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.TrolleyEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.TrolleySlotIndex).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.TrolleyPusherEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.CarryingProduct).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.PushingTrolley).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderProgressionCounted).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferVisitEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationVisitEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ProcurementTerminalEntityId).count == 1 &&
                    runtime.Game.GetGroup(GameMatcher.ModalOpen).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.PurchaseDeliveryRequest).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.PurchaseDeliverySucceeded).count == 0,
                "A completed customer cycle retained a visit, line, offer or relation index.");
            Require(scenario.Store.CompletedOrderCount == 3 &&
                    scenario.Store.isTrolleyUpgradeUnlocked &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == trolley &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 1 &&
                    trolley.OccupiedTrolleySlotCount == 0 &&
                    runtime.Game.GetEntitiesWithTrolleyEntityId(trolley.EntityId).Count == 0,
                "The completed mixed trolley flow retained cargo or lost progression state.");
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0,
                "A completed cycle retained an active delivery.");
            Require(!scenario.Player.isHandsOccupied &&
                    !scenario.Player.isCarryingProduct &&
                    !scenario.Player.isPushingTrolley &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    runtime.Game.GetEntityWithCarrierEntityId(scenario.Player.EntityId) == null,
                "The player retained a carrier relation after both cycles.");

            Debug.Log(
                $"[Hardware Store] Gameplay smoke passed: consultation and procurement modals, " +
                $"free forecast prebuy, safety-reserve rejection, arrow wrap, Enter/Esc and " +
                $"modal input capture, direct order activation, min/max offers, " +
                $"walking customer NPC lifecycle, two single-SKU cycles and one C2+B1 cycle, " +
                $"active-order replenishment and cross-SKU purchase, trolley-debit reserve, quota " +
                $"and wrong-SKU rejection, " +
                $"exact-slot product recovery, blocked/safe product drops and both physics flows, " +
                $"two-order trolley unlock, no-customer F attach/detach, E/F cargo routing, " +
                $"single purchase, three-slot C2+B1 trolley flow, " +
                $"collision-safe trolley stop/resume, " +
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
                GameMatcher.CompletedOrderCount,
                GameMatcher.OrderCounterEntityId,
                GameMatcher.ProcurementTerminalEntityId,
                GameMatcher.StorageZoneEntityId,
                GameMatcher.TrolleyUpgradeTerminalEntityId,
                GameMatcher.NextProjectSequenceIndex,
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
            GameEntity trolleyUpgradeTerminal = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.TrolleyUpgradeTerminal,
                    GameMatcher.StoreEntityId,
                    GameMatcher.TrolleySpawnPosition,
                    GameMatcher.TrolleySpawnRotation,
                    GameMatcher.View,
                    GameMatcher.InteractionView)), "trolley upgrade terminal");
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
            Require(store.TrolleyUpgradeTerminalEntityId ==
                    trolleyUpgradeTerminal.EntityId &&
                    trolleyUpgradeTerminal.StoreEntityId == store.EntityId,
                "The store and trolley upgrade terminal relations are inconsistent.");
            Require(!orderCounter.hasSceneViewKey &&
                    !procurementTerminal.hasSceneViewKey &&
                    !storageZone.hasSceneViewKey &&
                    !trolleyUpgradeTerminal.hasSceneViewKey,
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
            Require(runtime.Game.GetEntityWithTrolleyStoreEntityId(store.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 0 &&
                    store.CompletedOrderCount == 0 &&
                    !store.isTrolleyUpgradeUnlocked,
                "The smoke test must start before trolley progression or purchase.");
            Require(runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The smoke test must start without a customer actor.");
            Require(!player.isHandsOccupied &&
                    !player.isModalOpen &&
                    !player.hasConsultationVisitEntityId &&
                    !player.hasProcurementTerminalEntityId &&
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
                trolleyUpgradeTerminal,
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
                    visit.hasCustomerProjectType &&
                    !visit.isOrderRewarded &&
                    runtime.Game.GetEntitiesWithOrderEntityId(visit.EntityId).Count == 0 &&
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
                runtime.StaticData.ProjectTypes.ToArray(),
                visit.CustomerProjectType);
            Require(resetSequenceIndex >= 0,
                "The auto-spawned customer project is outside static data.");
            store.ReplaceNextProjectSequenceIndex(resetSequenceIndex);
            store.AddCustomerCooldownRemaining(
                runtime.StaticData.CustomerVehicle.FirstCustomerDelay);
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            int[] offerIds = offers.Select(offer => offer.EntityId).ToArray();
            GameEntity[] offerLines = offers
                .SelectMany(offer => GetConsultationOfferLines(runtime.Game, offer))
                .ToArray();
            int[] offerLineIds = offerLines.Select(line => line.EntityId).ToArray();
            foreach (GameEntity line in offerLines)
            {
                line.RemoveConsultationOfferEntityId();
                line.isDestructed = true;
            }
            foreach (GameEntity offer in offers)
            {
                offer.RemoveConsultationOfferVisitEntityId();
                offer.isDestructed = true;
            }
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
                    offerLineIds.All(id => runtime.Game.GetEntityWithEntityId(id) == null) &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferVisitEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferEntityId).count == 0 &&
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
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptCounterWaitCustomer)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The order counter does not present the no-customer cooldown state.");
            if (scenario.Player.hasFocusedEntityId)
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
                    visit.hasCustomerProjectType &&
                    !visit.hasProductType &&
                    !visit.hasRequiredProductCount &&
                    !visit.hasAvailableProductCount &&
                    !visit.hasLoadedProductCount &&
                    !visit.hasOrderReward &&
                    runtime.Game.GetEntitiesWithOrderEntityId(visit.EntityId).Count == 0 &&
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
                Require(offer.ConsultationOfferVisitEntityId == visit.EntityId &&
                        offer.hasOrderReward &&
                        offer.hasExpectedProfit &&
                        !offer.hasProductType &&
                        !offer.hasRequiredProductCount &&
                        !offer.hasAvailableProductCount &&
                        !offer.hasLoadedProductCount,
                    $"Consultation offer {offer.EntityId} has invalid project data.");
                GameEntity[] lines = GetConsultationOfferLines(runtime.Game, offer);
                Require(lines.Length > 0 &&
                        lines.Length <= CustomerProjectConfig.MaxLinesPerOffer,
                    $"Consultation offer {offer.EntityId} has an invalid line count.");
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    GameEntity line = lines[lineIndex];
                    Require(line.ConsultationOfferEntityId == offer.EntityId &&
                            line.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                            line.LineIndex == lineIndex &&
                            line.RequiredProductCount > 0 &&
                            line.AvailableProductCount >= 0,
                        $"Consultation offer line {line.EntityId} has invalid project data.");
                }
            }

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            EntityBehaviour view = RequireRuntimeView(
                visit,
                runtime.StaticData.CustomerVehicle.ViewPrefab,
                $"customer visit {visit.EntityId}");
            Require(visit.hasTransform && visit.hasRigidbody && visit.hasSlots &&
                    visit.Slots.Length == runtime.StaticData.CustomerVehicle.CargoCapacity,
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
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCounterCustomerApproaching)) &&
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
            ValidateConsultationLineAvailability(runtime, scenario, visit);

            return new CustomerVisit(visit, view, actor, actorView);
        }

        private static void OpenConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            Require(visit.isCustomerVisitConsulting && !visit.isOrder &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId,
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

            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
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
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
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
            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasConsultationVisitEntityId &&
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

        private static GameEntity[] ConfirmConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            CustomerProjectOfferDefinition expectedOffer)
        {
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            GameEntity selectedOffer = SelectedConsultationOffer(runtime.Game, visit);
            int[] offerIds = offers.Select(offer => offer.EntityId).ToArray();
            GameEntity[] offerLines = offers
                .SelectMany(offer => GetConsultationOfferLines(runtime.Game, offer))
                .ToArray();
            int[] offerLineIds = offerLines.Select(line => line.EntityId).ToArray();
            GameEntity[] selectedLines = GetConsultationOfferLines(
                runtime.Game, selectedOffer);
            Require(selectedLines.Length == expectedOffer.Lines.Count &&
                    selectedOffer.OrderReward == CalculateReward(runtime, expectedOffer) &&
                    selectedOffer.ExpectedProfit == CalculateExpectedProfit(
                        runtime, expectedOffer),
                "The selected offer does not match its static configuration.");
            for (int lineIndex = 0; lineIndex < selectedLines.Length; lineIndex++)
            {
                CustomerProjectLineDefinition expectedLine = expectedOffer.Lines[lineIndex];
                GameEntity line = selectedLines[lineIndex];
                Require(line.LineIndex == lineIndex &&
                        line.ProductType == expectedLine.ProductType &&
                        line.RequiredProductCount == expectedLine.RequiredCount &&
                        line.AvailableProductCount == CountStockProducts(
                            runtime.Game,
                            visit.StorageZoneEntityId,
                            expectedLine.ProductType),
                    $"Selected consultation line {lineIndex} does not match static data.");
            }

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                "Modal consultation did not capture repeated E from world interaction.");
            runtime.Systems.Create<ConsultationFeature>().Execute();

            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId &&
                    visit.isCustomerVisitConsulting &&
                    !visit.isOrder &&
                    offers.All(offer => !offer.isDestructed) &&
                    selectedOffer.isSelectedConsultationOffer,
                "Repeated E in the modal must neither interact with the world nor confirm an offer.");
            CleanupEvents(runtime);

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<ConsultationFeature>().Execute();

            GameEntity[] orderLines = GetOrderLines(runtime.Game, visit);

            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    visit.isOrder &&
                    visit.isCustomerVisitLoading &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.hasProductType &&
                    !visit.hasRequiredProductCount &&
                    !visit.hasAvailableProductCount &&
                    !visit.hasLoadedProductCount &&
                    visit.OrderReward == selectedOffer.OrderReward &&
                    visit.ExpectedProfit == selectedOffer.ExpectedProfit &&
                    orderLines.Length == expectedOffer.Lines.Count &&
                    offers.All(offer => offer.isDestructed &&
                                          !offer.hasConsultationOfferVisitEntityId) &&
                    offerLines.All(line => line.isDestructed &&
                                           !line.hasConsultationOfferEntityId),
                "Confirming an offer did not activate its loading order-line graph in the " +
                "same Enter action.");
            for (int lineIndex = 0; lineIndex < orderLines.Length; lineIndex++)
            {
                CustomerProjectLineDefinition expectedLine = expectedOffer.Lines[lineIndex];
                GameEntity line = orderLines[lineIndex];
                Require(line.OrderEntityId == visit.EntityId &&
                        line.LineIndex == lineIndex &&
                        line.ProductType == expectedLine.ProductType &&
                        line.RequiredProductCount == expectedLine.RequiredCount &&
                        line.AvailableProductCount == CountStockProducts(
                            runtime.Game,
                            visit.StorageZoneEntityId,
                            expectedLine.ProductType) &&
                        line.LoadedProductCount == 0,
                    $"Confirmed order line {lineIndex} does not match its offer line.");
            }
            RequireNotificationKey(runtime, LocalizationKey.NotificationOfferConfirmed);
            GameEntity[] audioCues = runtime.Game.GetGroup(GameMatcher.AudioCue).GetEntities();
            Require(audioCues.Length == 1 &&
                    audioCues[0].AudioCue == AudioCueId.OrderAccepted,
                "Direct offer confirmation did not emit exactly one order-accepted audio cue.");
            CleanupEvents(runtime);

            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(offerIds.All(id => runtime.Game.GetEntityWithEntityId(id) == null) &&
                    offerLineIds.All(id => runtime.Game.GetEntityWithEntityId(id) == null) &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferVisitEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ConsultationOfferEntityId).count == 0 &&
                    GetConsultationOffers(runtime.Game, visit).Length == 0,
                "Confirmed consultation offers survived the Destructed cleanup pipeline.");
            return orderLines;
        }

        private static GameEntity[] GetConsultationOffers(
            GameContext context,
            GameEntity visit) =>
            context.GetEntitiesWithConsultationOfferVisitEntityId(visit.EntityId)
                .Where(entity => entity.isConsultationOffer && !entity.isDestructed)
                .OrderBy(entity => entity.OfferIndex)
                .ToArray();

        private static GameEntity[] GetConsultationOfferLines(
            GameContext context,
            GameEntity offer) =>
            context.GetEntitiesWithConsultationOfferEntityId(offer.EntityId)
                .Where(entity => entity.isConsultationOfferLine && !entity.isDestructed)
                .OrderBy(entity => entity.LineIndex)
                .ToArray();

        private static GameEntity[] GetOrderLines(
            GameContext context,
            GameEntity visit) =>
            context.GetEntitiesWithOrderEntityId(visit.EntityId)
                .Where(entity => entity.isOrderLine && !entity.isDestructed)
                .OrderBy(entity => entity.LineIndex)
                .ToArray();

        private static void ValidateConsultationLineAvailability(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            CustomerProjectConfig project = runtime.StaticData.GetProject(
                visit.CustomerProjectType);
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            Require(offers.Length == project.Offers.Count,
                "The customer visit offer count does not match its project config.");

            for (int offerIndex = 0; offerIndex < offers.Length; offerIndex++)
            {
                CustomerProjectOfferDefinition definition = project.Offers[offerIndex];
                GameEntity[] lines = GetConsultationOfferLines(
                    runtime.Game, offers[offerIndex]);
                Require(lines.Length == definition.Lines.Count &&
                        TotalRequiredCount(definition) <=
                        runtime.StaticData.CustomerVehicle.CargoCapacity,
                    $"Consultation offer {offerIndex} does not match its configured lines.");
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    CustomerProjectLineDefinition expected = definition.Lines[lineIndex];
                    GameEntity line = lines[lineIndex];
                    Require(line.LineIndex == lineIndex &&
                            line.ProductType == expected.ProductType &&
                            line.RequiredProductCount == expected.RequiredCount &&
                            line.AvailableProductCount == CountStockProducts(
                                runtime.Game,
                                scenario.StorageZone.EntityId,
                                expected.ProductType),
                        $"Consultation offer {offerIndex} line {lineIndex} has stale stock.");
                }
            }
        }

        private static GameEntity FindOrderLine(
            GameEntity[] orderLines,
            ProductTypeId productType) =>
            orderLines.Single(line => line.ProductType == productType);

        private static int TotalRequiredCount(CustomerProjectOfferDefinition offer) =>
            offer.Lines.Sum(line => line.RequiredCount);

        private static int RequiredCount(
            CustomerProjectOfferDefinition offer,
            ProductTypeId productType) =>
            offer.Lines.Single(line => line.ProductType == productType).RequiredCount;

        private static void ResolveForecastDemandRange(
            CustomerProjectConfig project,
            ProductTypeId productType,
            out int minimumRequiredProductCount,
            out int maximumRequiredProductCount)
        {
            minimumRequiredProductCount = int.MaxValue;
            maximumRequiredProductCount = 0;
            foreach (CustomerProjectOfferDefinition offer in project.Offers)
            {
                CustomerProjectLineDefinition line = offer.Lines.SingleOrDefault(candidate =>
                    candidate.ProductType == productType);
                int requiredCount = line?.RequiredCount ?? 0;
                minimumRequiredProductCount = Math.Min(
                    minimumRequiredProductCount,
                    requiredCount);
                maximumRequiredProductCount = Math.Max(
                    maximumRequiredProductCount,
                    requiredCount);
            }
        }

        private static int CalculateReward(
            Runtime runtime,
            CustomerProjectOfferDefinition offer) =>
            offer.Lines.Aggregate(
                0,
                (total, line) => checked(
                    total + checked(runtime.StaticData.GetProduct(line.ProductType).UnitPrice *
                                    line.RequiredCount)));

        private static int CalculateExpectedProfit(
            Runtime runtime,
            CustomerProjectOfferDefinition offer) =>
            offer.Lines.Aggregate(
                CalculateReward(runtime, offer),
                (profit, line) => checked(
                    profit - checked(runtime.StaticData.GetDelivery(line.ProductType)
                        .PurchaseUnitPrice * line.RequiredCount)));

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
            ProductTypeId productType,
            bool validateModalControls = false)
        {
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "A new delivery cannot be purchased while another is active.");

            OpenProcurement(runtime, scenario);
            if (validateModalControls)
            {
                ValidateProcurementModalControls(runtime, scenario);
                OpenProcurement(runtime, scenario);
            }
            SelectProcurementProduct(runtime, scenario, productType);
            var deliveryConfig = runtime.StaticData.GetDelivery(productType);
            var productConfig = runtime.StaticData.GetProduct(productType);
            int moneyBeforePurchase = scenario.Store.Money;
            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<ProcurementFeature>().Execute();
            GameEntity[] purchaseRequests = runtime.Game.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PurchaseDeliveryRequest,
                    GameMatcher.SourceEntityId,
                    GameMatcher.TargetEntityId))
                .GetEntities();
            Require(purchaseRequests.Length == 1 &&
                    purchaseRequests[0].SourceEntityId == scenario.Player.EntityId &&
                    purchaseRequests[0].TargetEntityId ==
                    scenario.ProcurementTerminal.EntityId &&
                    !purchaseRequests[0].isPurchaseDeliverySucceeded &&
                    scenario.Player.isModalOpen,
                "Enter did not emit exactly one modal purchase request.");

            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            GameEntity delivery = runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                scenario.ProcurementTerminal.EntityId);
            Require(delivery != null &&
                    purchaseRequests[0].isPurchaseDeliverySucceeded &&
                    delivery.hasDeliveryProcurementTerminalEntityId &&
                    delivery.DeliveryProcurementTerminalEntityId ==
                    scenario.ProcurementTerminal.EntityId &&
                    delivery.isDeliveryActive &&
                    delivery.ProductType == productType &&
                    delivery.DeliveryProductCount == deliveryConfig.ProductCount &&
                    delivery.DeliveryCost == deliveryConfig.TotalCost &&
                    scenario.Store.Money == moneyBeforePurchase - deliveryConfig.TotalCost,
                "Purchasing did not create an indexed active delivery.");

            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            Require(ReferenceEquals(
                        runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                            scenario.ProcurementTerminal.EntityId),
                        delivery) &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 1 &&
                    scenario.Store.Money == moneyBeforePurchase - deliveryConfig.TotalCost,
                "One Enter confirmation purchased or charged the selected delivery twice.");
            runtime.Systems.Create<CloseProcurementAfterPurchaseSystem>().Execute();
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.MoveDirection == Vector3.zero &&
                    scenario.Player.isCursorLocked,
                "A successful purchase did not close and release the procurement modal.");
            runtime.Systems.Create<PresentProcurementSystem>().Execute();
            CleanupEvents(runtime);

            scenario.Player.ReplaceFocusedEntityId(
                scenario.ProcurementTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(!scenario.Player.isFocusInteractionAvailable &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptDeliveryBeingStocked,
                            LocalizedTexts.ProductName(productType),
                            0,
                            deliveryConfig.ProductCount,
                            LocalizedTexts.ProductUnit(productType))),
                "An active delivery did not expose its localized procurement-blocking prompt.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);

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
                        !product.hasReservedDeliverySlotIndex &&
                        !product.hasReservedStorageSlotIndex &&
                        !product.hasReservedOrderLineEntityId &&
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
            ProductTypeId safeProductType,
            ProductTypeId selectedProductType,
            ProcurementDemandKind expectedDemandKind,
            LocalizationKey expectedNotificationKey,
            string failureMessage)
        {
            OpenProcurement(runtime, scenario);
            SelectProcurementProduct(runtime, scenario, selectedProductType);
            ProcurementSnapshot snapshot = CaptureProcurementSnapshot(runtime, scenario);
            ProcurementProductSnapshot selectedCard = snapshot.Products.Single(product =>
                product.ProductType == selectedProductType);
            ProcurementProductSnapshot safeCard = snapshot.Products.Single(product =>
                product.ProductType == safeProductType);
            Require(snapshot.DemandKind == expectedDemandKind &&
                    selectedCard.PurchaseState ==
                    ProcurementPurchaseState.PlanWouldBecomeUnfulfillable &&
                    !selectedCard.PurchaseAvailable,
                "An unsafe purchase did not expose its plan-safety reason in the card.");
            if (expectedDemandKind == ProcurementDemandKind.ProjectForecast)
            {
                CustomerProjectConfig project = runtime.StaticData.GetProject(
                    snapshot.ProjectType);
                ResolveForecastDemandRange(
                    project,
                    safeProductType,
                    out int safeMinimum,
                    out int safeMaximum);
                ResolveForecastDemandRange(
                    project,
                    selectedProductType,
                    out int selectedMinimum,
                    out int selectedMaximum);
                Require(snapshot.ProjectType == CustomerProjectTypeId.CementFoundation &&
                        safeCard.PurchaseAvailable &&
                        safeCard.MinimumRequiredProductCount == safeMinimum &&
                        safeCard.MaximumRequiredProductCount == safeMaximum &&
                        selectedCard.MinimumRequiredProductCount == selectedMinimum &&
                        selectedCard.MaximumRequiredProductCount == selectedMaximum &&
                        safeCard.RemainingRequiredProductCount == 0 &&
                        safeCard.DeficitProductCount == 0 &&
                        selectedCard.RemainingRequiredProductCount == 0 &&
                        selectedCard.DeficitProductCount == 0,
                    "The no-customer forecast cards did not show safe prebuy availability " +
                    "and the configured min-max demand.");
            }

            int moneyBefore = scenario.Store.Money;
            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<ProcurementFeature>().Execute();
            GameEntity request = RequireSingle(
                runtime.Game.GetGroup(GameMatcher.AllOf(
                    GameMatcher.PurchaseDeliveryRequest,
                    GameMatcher.SourceEntityId,
                    GameMatcher.TargetEntityId)),
                "purchase request");
            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            runtime.Systems.Create<CloseProcurementAfterPurchaseSystem>().Execute();
            RequireNotificationKey(runtime, expectedNotificationKey);
            Require(runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    scenario.Store.Money == moneyBefore &&
                    scenario.Player.isModalOpen &&
                    scenario.Player.hasProcurementTerminalEntityId &&
                    !request.isPurchaseDeliverySucceeded &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count > 0,
                failureMessage);
            CleanupEvents(runtime);
            CancelProcurement(runtime, scenario, moneyBefore);
        }

        private static void OpenProcurement(Runtime runtime, Scenario scenario)
        {
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "Procurement can only open from a closed, delivery-free player state.");

            int moneyBefore = scenario.Store.Money;
            scenario.Player.ReplaceFocusedEntityId(
                scenario.ProcurementTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptOpenProcurement)),
                "A hands-free, delivery-free terminal did not expose the procurement prompt.");

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 1,
                "E did not emit the world request used to open procurement.");
            runtime.Systems.Create<ProcurementFeature>().Execute();
            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasProcurementTerminalEntityId &&
                    scenario.Player.ProcurementTerminalEntityId ==
                    scenario.ProcurementTerminal.EntityId &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.MoveDirection == Vector3.zero &&
                    scenario.Player.isCursorLocked &&
                    !scenario.Player.hasFocusedEntityId &&
                    !scenario.Player.isFocusInteractionAvailable &&
                    runtime.Game.GetGroup(GameMatcher.PurchaseDeliveryRequest).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    scenario.Store.Money == moneyBefore,
                "E must open procurement without purchasing or charging anything.");
            runtime.Systems.Create<PresentProcurementSystem>().Execute();
            CleanupEvents(runtime);
        }

        private static ProcurementSnapshot CaptureProcurementSnapshot(
            Runtime runtime,
            Scenario scenario)
        {
            if (!scenario.Player.isModalOpen)
                OpenProcurement(runtime, scenario);

            var capture = new CaptureHudService();
            new PresentProcurementSystem(
                    runtime.Game,
                    runtime.StaticData,
                    runtime.ProcurementSolvency,
                    capture)
                .Execute();
            return capture.Procurement ?? throw new InvalidOperationException(
                "An open procurement modal did not produce a presentation snapshot.");
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
            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasProcurementTerminalEntityId &&
                    scenario.Player.ProcurementTerminalEntityId ==
                    scenario.ProcurementTerminal.EntityId,
                "Procurement selection requires the terminal's open modal.");

            int stepCount = 0;
            while (scenario.ProcurementTerminal.SelectedProductType != productType &&
                   stepCount < runtime.StaticData.ProductTypes.Count)
            {
                CycleProcurementProduct(runtime, scenario, next: true);
                stepCount++;
            }

            Require(scenario.ProcurementTerminal.SelectedProductType == productType,
                $"Procurement selection did not reach {productType}.");
        }

        private static void CycleProcurementProduct(
            Runtime runtime,
            Scenario scenario,
            bool next)
        {
            scenario.Input.isPreviousPressed = !next;
            scenario.Input.isNextPressed = next;
            runtime.Systems.Create<ProcurementFeature>().Execute();
            runtime.Systems.Create<PresentProcurementSystem>().Execute();
            CleanupEvents(runtime);
        }

        private static void ValidateProcurementModalControls(
            Runtime runtime,
            Scenario scenario)
        {
            ProductTypeId[] productTypes = runtime.StaticData.ProductTypes.ToArray();
            Require(productTypes.Length == 2,
                "The procurement modal smoke requires exactly two product cards.");

            SelectProcurementProduct(runtime, scenario, productTypes[0]);
            CycleProcurementProduct(runtime, scenario, next: false);
            Require(scenario.ProcurementTerminal.SelectedProductType == productTypes[^1],
                "Previous did not wrap procurement from the first to the last product.");
            CycleProcurementProduct(runtime, scenario, next: true);
            Require(scenario.ProcurementTerminal.SelectedProductType == productTypes[0],
                "Next did not wrap procurement from the last to the first product.");
            SelectProcurementProduct(runtime, scenario, productTypes[^1]);
            CycleProcurementProduct(runtime, scenario, next: true);
            Require(scenario.ProcurementTerminal.SelectedProductType == productTypes[0],
                "Next did not wrap procurement from the last product card.");

            int moneyBefore = scenario.Store.Money;
            ProductTypeId selectionBefore =
                scenario.ProcurementTerminal.SelectedProductType;
            scenario.Player.ReplaceFocusedEntityId(
                scenario.ProcurementTerminal.EntityId);
            scenario.Player.ReplaceFocusedInteractionType(
                InteractionTypeId.ProcurementTerminal);
            scenario.Player.isFocusInteractionAvailable = true;
            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            runtime.Systems.Create<ProcurementFeature>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.PurchaseDeliveryRequest).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    scenario.Player.isModalOpen &&
                    scenario.Player.hasProcurementTerminalEntityId &&
                    scenario.ProcurementTerminal.SelectedProductType == selectionBefore &&
                    scenario.Store.Money == moneyBefore,
                "Repeated E inside procurement escaped to world interaction or purchased.");
            scenario.Player.RemoveFocusedEntityId();
            scenario.Player.RemoveFocusedInteractionType();
            scenario.Player.isFocusInteractionAvailable = false;
            CleanupEvents(runtime);

            Vector3 playerRotationBefore = scenario.Player.Transform.eulerAngles;
            Quaternion pivotRotationBefore = scenario.Player.ViewPivot.localRotation;
            float pitchBefore = scenario.Player.ViewPitch;
            scenario.Input.ReplaceMoveInput(Vector2.one);
            scenario.Input.ReplaceLookInput(new Vector2(17f, -11f));
            scenario.Input.isPointerLook = true;
            runtime.Systems.Create<SetMoveDirectionFromInputSystem>().Execute();
            runtime.Systems.Create<ApplyLookInputSystem>().Execute();
            Require(scenario.Player.MoveDirection == Vector3.zero &&
                    Vector3.Distance(
                        scenario.Player.Transform.eulerAngles,
                        playerRotationBefore) < 0.001f &&
                    Quaternion.Angle(
                        scenario.Player.ViewPivot.localRotation,
                        pivotRotationBefore) < 0.001f &&
                    Mathf.Approximately(scenario.Player.ViewPitch, pitchBefore),
                "Procurement modal did not capture movement or look input.");
            scenario.Input.ReplaceMoveInput(Vector2.zero);
            scenario.Input.ReplaceLookInput(Vector2.zero);
            scenario.Input.isPointerLook = false;

            GameEntity dropSentinel = CreateEntity.Empty();
            dropSentinel.isProduct = true;
            dropSentinel.AddCarrierEntityId(scenario.Player.EntityId);
            scenario.Player.isHandsOccupied = true;
            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            Require(scenario.Player.isHandsOccupied &&
                    dropSentinel.hasCarrierEntityId &&
                    dropSentinel.CarrierEntityId == scenario.Player.EntityId,
                "Procurement modal did not capture product drop input.");
            scenario.Input.isDropPressed = false;
            scenario.Player.isHandsOccupied = false;
            dropSentinel.RemoveCarrierEntityId();
            dropSentinel.isDestructed = true;
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            scenario.ProcurementTerminal.isHighlighted = true;
            runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
            Require(!scenario.ProcurementTerminal.isHighlighted,
                "Procurement modal did not clear the world focus highlight.");

            CancelProcurement(runtime, scenario, moneyBefore);
        }

        private static void CancelProcurement(
            Runtime runtime,
            Scenario scenario,
            int expectedMoney)
        {
            scenario.Input.isToggleCursorPressed = true;
            runtime.Systems.Create<ProcurementFeature>().Execute();
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.MoveDirection == Vector3.zero &&
                    scenario.Player.isCursorLocked &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    scenario.Store.Money == expectedMoney,
                "Esc did not close procurement without charging the store.");
            runtime.Systems.Create<PresentProcurementSystem>().Execute();
            CleanupEvents(runtime);
        }

        private static void TestCarryDropAndRepick(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            int deliverySlotIndex = product.DeliverySlotIndex;
            PickUpProduct(runtime, scenario, product);
            Require(scenario.Player.isHandsOccupied &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    product.hasReservedDeliverySlotIndex &&
                    product.ReservedDeliverySlotIndex == deliverySlotIndex &&
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

            ValidateBlockedProductDrop(runtime, scenario, product);
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
                    product.hasReservedDeliverySlotIndex &&
                    product.ReservedDeliverySlotIndex == deliverySlotIndex &&
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
                    product.hasReservedDeliverySlotIndex &&
                    product.ReservedDeliverySlotIndex == deliverySlotIndex &&
                    !product.isLooseProduct &&
                    !product.hasWorldPosition &&
                    !product.hasWorldRotation,
                "The dropped product could not be picked up again.");
            scenario.Input.isSprintHeld = false;
        }

        private static void ValidateInboundProductRecovery(
            Runtime runtime,
            Scenario scenario,
            DeliveryArrival arrival)
        {
            Require(arrival.Products.Length >= 2,
                "Inbound recovery must exercise two independent delivery slots.");
            GameEntity[] products = arrival.Products.Take(2).ToArray();
            int[] productIds = products.Select(product => product.EntityId).ToArray();
            int[] reservedSlots = products.Select(product => product.DeliverySlotIndex).ToArray();
            int productCountBefore = FindProducts(runtime.Game).Length;
            int deliveryProductCountBefore = FindDeliveryProducts(
                runtime.Game,
                arrival.Delivery.EntityId).Length;
            int moneyBefore = scenario.Store.Money;

            for (int index = 0; index < products.Length; index++)
            {
                GameEntity product = products[index];
                PickUpProduct(runtime, scenario, product);
                Require(product.hasReservedDeliverySlotIndex &&
                        product.ReservedDeliverySlotIndex == reservedSlots[index] &&
                        !product.hasDeliverySlotIndex,
                    $"Inbound product {product.EntityId} did not reserve its exact delivery slot.");

                DropHeldProduct(runtime, scenario);
                Require(product.isLooseProduct &&
                        product.hasReservedDeliverySlotIndex &&
                        product.ReservedDeliverySlotIndex == reservedSlots[index],
                    $"Dropped inbound product {product.EntityId} lost its delivery-slot reservation.");
                MoveLooseProductBelowRecoveryBoundary(runtime, product);
            }

            Require(reservedSlots.Distinct().Count() == products.Length,
                "Two lost inbound products reserved the same delivery slot.");
            RecoverLostProducts(runtime);
            RequireNotificationKey(runtime, LocalizationKey.NotificationProductsRecovered);
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);

            for (int index = 0; index < products.Length; index++)
            {
                GameEntity product = products[index];
                Require(ReferenceEquals(
                            runtime.Game.GetEntityWithEntityId(productIds[index]),
                            product) &&
                        product.isInboundProduct &&
                        product.hasDeliverySlotIndex &&
                        product.DeliverySlotIndex == reservedSlots[index] &&
                        !product.hasReservedDeliverySlotIndex &&
                        !product.isLooseProduct &&
                        !product.hasWorldPosition &&
                        !product.hasWorldRotation &&
                        product.Transform.parent == arrival.Delivery.Slots[reservedSlots[index]] &&
                        !product.isProductPlacementDirty,
                    $"Inbound product {product.EntityId} did not recover to its exact delivery slot.");
            }

            Require(FindProducts(runtime.Game).Length == productCountBefore &&
                    FindDeliveryProducts(runtime.Game, arrival.Delivery.EntityId).Length ==
                    deliveryProductCountBefore &&
                    scenario.Store.Money == moneyBefore,
                "Inbound recovery changed product identity, count or money.");

            RecoverLostProducts(runtime);
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0 &&
                    products.Select(product => product.DeliverySlotIndex)
                        .SequenceEqual(reservedSlots),
                "Inbound recovery was not idempotent after restoring both products.");
        }

        private static void ValidateStockReservationAndRecovery(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity product)
        {
            GameEntity orderLine = FindOrderLine(
                GetOrderLines(runtime.Game, visit),
                product.ProductType);
            Require(visit.isCustomerVisitLoading &&
                    orderLine.RequiredProductCount > 0 &&
                    orderLine.LoadedProductCount == 0 &&
                    product.isInStock &&
                    product.hasStorageSlotIndex,
                "Stock recovery requires an unloaded order line and one slotted product.");

            int productId = product.EntityId;
            int storageSlotIndex = product.StorageSlotIndex;
            int stockCountBefore = scenario.StorageZone.StorageProductCount;
            int productCountBefore = FindProducts(runtime.Game).Length;
            int moneyBefore = scenario.Store.Money;

            PickUpProduct(runtime, scenario, product);
            Require(product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId &&
                    !product.hasStorageSlotIndex &&
                    runtime.Game.GetEntitiesWithReservedOrderLineEntityId(orderLine.EntityId)
                        .Single() == product,
                "Picking stock did not reserve its exact slot and order-line quota.");
            ExecuteStorageState(runtime);
            Require(scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    scenario.StorageZone.OccupiedStorageSlotCount == stockCountBefore,
                "Held stock made its reserved storage slot appear free.");

            ValidateBlockedProductDrop(runtime, scenario, product);
            DropHeldProduct(runtime, scenario);
            Require(product.isLooseProduct &&
                    product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId,
                "Dropped stock lost its storage-slot or order-line reservation.");
            ExecuteStorageState(runtime);
            Require(scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    scenario.StorageZone.OccupiedStorageSlotCount == stockCountBefore,
                "Loose reserved stock made its storage slot appear free.");

            MoveLooseProductBelowRecoveryBoundary(runtime, product);
            RecoverLostProducts(runtime);
            RequireNotificationKey(runtime, LocalizationKey.NotificationProductsRecovered);
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            Require(ReferenceEquals(runtime.Game.GetEntityWithEntityId(productId), product) &&
                    product.isInStock &&
                    product.hasStorageSlotIndex &&
                    product.StorageSlotIndex == storageSlotIndex &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.isLooseProduct &&
                    !product.hasWorldPosition &&
                    !product.hasWorldRotation &&
                    product.Transform.parent == scenario.StorageZone.Slots[storageSlotIndex] &&
                    scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    FindProducts(runtime.Game).Length == productCountBefore &&
                    scenario.Store.Money == moneyBefore,
                "Lost stock did not recover to the same entity and exact storage slot.");

            RecoverLostProducts(runtime);
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0 &&
                    product.StorageSlotIndex == storageSlotIndex &&
                    scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    scenario.Store.Money == moneyBefore,
                "Stock recovery changed stable state when executed twice.");

            PickUpProduct(runtime, scenario, product);
            Require(product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId,
                "Held stock did not retain the exact return reservation.");
            scenario.Player.ReplaceFocusedEntityId(scenario.StorageZone.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptReturnStockProduct,
                            LocalizedTexts.ProductName(product.ProductType))),
                "Held stock did not expose the storage return prompt.");
            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 1,
                "E did not emit the held-stock storage return request.");
            runtime.Systems.Create<StoreInboundProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            Require(product.isInStock &&
                    product.hasStorageSlotIndex &&
                    product.StorageSlotIndex == storageSlotIndex &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.hasCarrierEntityId &&
                    !product.isProductStocked &&
                    !scenario.Player.isHandsOccupied &&
                    product.Transform.parent == scenario.StorageZone.Slots[storageSlotIndex] &&
                    scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    FindProducts(runtime.Game).Length == productCountBefore &&
                    scenario.Store.Money == moneyBefore,
                "Held stock did not return to its exact reserved storage slot cleanly.");
        }

        private static void ValidateBlockedProductDrop(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isCarryingProduct &&
                    !scenario.Player.isPushingTrolley &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    product.hasProductDropCollisionRadius &&
                    product.ProductDropCollisionRadius > 0f &&
                    product.ProductDropCollisionRadius <= product.DropForwardDistance,
                "Blocked-drop smoke requires one valid collision-safe carried product.");

            int entityCountBefore = runtime.Game.count;
            int productCountBefore = FindProducts(runtime.Game).Length;
            int moneyBefore = scenario.Store.Money;
            int? deliveryEntityId = product.hasDeliveryEntityId
                ? product.DeliveryEntityId
                : null;
            int? reservedDeliverySlotIndex = product.hasReservedDeliverySlotIndex
                ? product.ReservedDeliverySlotIndex
                : null;
            int? storageZoneEntityId = product.hasStorageZoneEntityId
                ? product.StorageZoneEntityId
                : null;
            int? reservedStorageSlotIndex = product.hasReservedStorageSlotIndex
                ? product.ReservedStorageSlotIndex
                : null;
            int? reservedOrderLineEntityId = product.hasReservedOrderLineEntityId
                ? product.ReservedOrderLineEntityId
                : null;
            bool wasInbound = product.isInboundProduct;
            bool wasInStock = product.isInStock;
            bool wasPlacementDirty = product.isProductPlacementDirty;
            Vector3 transformPosition = product.Transform.position;
            Quaternion transformRotation = product.Transform.rotation;
            Vector3 bodyPosition = product.Rigidbody.position;
            Quaternion bodyRotation = product.Rigidbody.rotation;
            bool bodyWasKinematic = product.Rigidbody.isKinematic;
            bool bodyUsedGravity = product.Rigidbody.useGravity;
            bool bodyDetectedCollisions = product.Rigidbody.detectCollisions;
            bool[] colliderEnabledStates = product.Colliders
                .Select(collider => collider.enabled)
                .ToArray();

            Transform dropOrigin = scenario.Player.DropOrigin;
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Smoke Collision-Safe Drop Obstacle";
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            Require(ignoreRaycastLayer >= 0,
                "The built-in Ignore Raycast layer is required for collision-safe drop smoke.");
            obstacle.layer = ignoreRaycastLayer;
            obstacle.transform.SetPositionAndRotation(
                dropOrigin.position +
                dropOrigin.forward.normalized * 0.2f,
                Quaternion.identity);
            obstacle.transform.localScale = Vector3.one * 0.2f;
            Physics.SyncTransforms();

            try
            {
                scenario.Input.isDropPressed = true;
                runtime.Systems.Create<DropHeldProductSystem>().Execute();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
            }

            RequireNotificationKey(runtime, LocalizationKey.NotificationProductDropBlocked);
            Require(runtime.Game.GetGroup(GameMatcher.AudioCue).count == 0,
                "A blocked product drop emitted the successful drop audio cue.");
            Require(runtime.Game.count == entityCountBefore + 1 &&
                    FindProducts(runtime.Game).Length == productCountBefore &&
                    scenario.Store.Money == moneyBefore,
                "A blocked product drop changed product count or store economy.");
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isCarryingProduct &&
                    !scenario.Player.isPushingTrolley &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithCarrierEntityId(scenario.Player.EntityId),
                        product),
                "A blocked product drop changed the player's carrying relation.");
            Require(product.isInboundProduct == wasInbound &&
                    product.isInStock == wasInStock &&
                    product.isProductPlacementDirty == wasPlacementDirty &&
                    !product.isLooseProduct &&
                    !product.hasWorldPosition &&
                    !product.hasWorldRotation &&
                    !product.hasDeliverySlotIndex &&
                    !product.hasStorageSlotIndex &&
                    !product.hasOrderLineEntityId &&
                    !product.hasLoadingSlotIndex &&
                    !product.hasTrolleyEntityId &&
                    !product.hasTrolleySlotIndex,
                "A blocked product drop changed product placement state.");
            Require((deliveryEntityId.HasValue == product.hasDeliveryEntityId) &&
                    (!deliveryEntityId.HasValue ||
                     product.DeliveryEntityId == deliveryEntityId.Value) &&
                    (reservedDeliverySlotIndex.HasValue ==
                     product.hasReservedDeliverySlotIndex) &&
                    (!reservedDeliverySlotIndex.HasValue ||
                     product.ReservedDeliverySlotIndex == reservedDeliverySlotIndex.Value) &&
                    (storageZoneEntityId.HasValue == product.hasStorageZoneEntityId) &&
                    (!storageZoneEntityId.HasValue ||
                     product.StorageZoneEntityId == storageZoneEntityId.Value) &&
                    (reservedStorageSlotIndex.HasValue ==
                     product.hasReservedStorageSlotIndex) &&
                    (!reservedStorageSlotIndex.HasValue ||
                     product.ReservedStorageSlotIndex == reservedStorageSlotIndex.Value) &&
                    (reservedOrderLineEntityId.HasValue ==
                     product.hasReservedOrderLineEntityId) &&
                    (!reservedOrderLineEntityId.HasValue ||
                     product.ReservedOrderLineEntityId == reservedOrderLineEntityId.Value),
                "A blocked product drop changed an exact-slot or order-line reservation.");
            Require(Vector3.Distance(product.Transform.position, transformPosition) < 0.001f &&
                    Quaternion.Angle(product.Transform.rotation, transformRotation) < 0.001f &&
                    Vector3.Distance(product.Rigidbody.position, bodyPosition) < 0.001f &&
                    Quaternion.Angle(product.Rigidbody.rotation, bodyRotation) < 0.001f &&
                    product.Rigidbody.isKinematic == bodyWasKinematic &&
                    product.Rigidbody.useGravity == bodyUsedGravity &&
                    product.Rigidbody.detectCollisions == bodyDetectedCollisions &&
                    product.Colliders.Select(collider => collider.enabled)
                        .SequenceEqual(colliderEnabledStates),
                "A blocked product drop changed the carried view or physics state.");

            CleanupEvents(runtime);
            Require(scenario.Player.isHandsOccupied &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId,
                "Blocked-drop event cleanup changed the retained carrying state.");
        }

        private static void DropHeldProduct(Runtime runtime, Scenario scenario)
        {
            Require(scenario.Player.isHandsOccupied,
                "A product can only be dropped while the player's hands are occupied.");
            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DropHeldProductSystem>().Execute();
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);
        }

        private static void MoveLooseProductBelowRecoveryBoundary(
            Runtime runtime,
            GameEntity product)
        {
            Require(product.isLooseProduct &&
                    product.hasWorldPosition &&
                    product.hasWorldRotation,
                $"Product {product.EntityId} must be loose before simulating a lost product.");
            Vector3 lostPosition = product.WorldPosition;
            lostPosition.y = runtime.StaticData.ProductRecovery.MinimumWorldY - 1f;
            product.ReplaceWorldPosition(lostPosition);
            product.Rigidbody.position = lostPosition;
            product.Transform.position = lostPosition;
            Physics.SyncTransforms();
        }

        private static void RecoverLostProducts(Runtime runtime) =>
            runtime.Systems.Create<ProductRecoveryFeature>().Execute();

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
                        product) &&
                        product.hasReservedDeliverySlotIndex &&
                        !product.hasDeliverySlotIndex,
                    $"Inbound product {product.EntityId} is not carried before storage.");

                RequestInteraction(scenario.Player, scenario.StorageZone);
                runtime.Systems.Create<StoreInboundProductSystem>().Execute();
                Require(product.isProductStocked &&
                        product.isInStock &&
                        product.hasDeliveryEntityId &&
                        !product.hasReservedDeliverySlotIndex &&
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
                        (visit == null || GetOrderLines(runtime.Game, visit).All(line =>
                             line.AvailableProductCount == CountStockProducts(
                                 runtime.Game,
                                 scenario.StorageZone.EntityId,
                                 line.ProductType))),
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
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 0 &&
                    runtime.Game.GetEntitiesWithDeliveryEntityId(deliveryId).Count == 0,
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
                    GetOrderLines(runtime.Game, visit)
                        .All(line => line.ProductType != wrongProduct.ProductType) &&
                    wrongProduct.hasStorageSlotIndex,
                "Wrong-SKU loading check requires a slotted stock product of another type.");

            ValidateRejectedStockProductCannotLoad(
                runtime,
                scenario,
                visit,
                wrongProduct,
                "A wrong-SKU stock product was loaded into the customer vehicle.");
        }

        private static void ValidateRejectedStockProductCannotLoad(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity rejectedProduct,
            string failureMessage)
        {
            GameEntity[] orderLines = GetOrderLines(runtime.Game, visit);
            GameEntity matchingLine = orderLines
                .SingleOrDefault(line => line.ProductType == rejectedProduct.ProductType);
            int matchingReservationCount = matchingLine == null
                ? 0
                : runtime.Game.GetEntitiesWithReservedOrderLineEntityId(
                    matchingLine.EntityId).Count;
            Require(visit.isCustomerVisitLoading &&
                    rejectedProduct.isInStock &&
                    rejectedProduct.hasStorageSlotIndex &&
                    (matchingLine == null ||
                     matchingLine.LoadedProductCount + matchingReservationCount ==
                     matchingLine.RequiredProductCount),
                "Rejected loading requires either a wrong SKU or an order line whose loaded " +
                "and reserved quota is already complete.");

            int storageSlotIndex = rejectedProduct.StorageSlotIndex;
            int[] loadedBefore = orderLines
                .Select(line => line.LoadedProductCount)
                .ToArray();
            int[] reservedBefore = orderLines
                .Select(line => runtime.Game
                    .GetEntitiesWithReservedOrderLineEntityId(line.EntityId).Count)
                .ToArray();
            Require(!scenario.Player.isHandsOccupied,
                "Rejected stock pickup requires empty player hands.");
            RequestInteraction(scenario.Player, rejectedProduct);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            CleanupEvents(runtime);
            Require(orderLines.Select(line => line.LoadedProductCount)
                        .SequenceEqual(loadedBefore) &&
                    orderLines.Select(line => runtime.Game
                            .GetEntitiesWithReservedOrderLineEntityId(line.EntityId).Count)
                        .SequenceEqual(reservedBefore) &&
                    !rejectedProduct.isProductLoaded &&
                    !rejectedProduct.isLoaded &&
                    !rejectedProduct.hasOrderLineEntityId &&
                    !rejectedProduct.hasLoadingSlotIndex &&
                    rejectedProduct.isInStock &&
                    !rejectedProduct.hasCarrierEntityId &&
                    !rejectedProduct.hasReservedStorageSlotIndex &&
                    !rejectedProduct.hasReservedOrderLineEntityId &&
                    rejectedProduct.hasStorageSlotIndex &&
                    rejectedProduct.StorageSlotIndex == storageSlotIndex &&
                    !scenario.Player.isHandsOccupied,
                failureMessage);

            Require(rejectedProduct.isInStock &&
                    rejectedProduct.hasStorageSlotIndex &&
                    rejectedProduct.StorageSlotIndex == storageSlotIndex &&
                    rejectedProduct.Transform.parent ==
                    scenario.StorageZone.Slots[storageSlotIndex] &&
                    !rejectedProduct.isProductPlacementDirty &&
                    runtime.Game.GetEntityWithCarrierEntityId(
                        scenario.Player.EntityId) == null,
                "Rejected loading check did not restore the stock product cleanly.");
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

        private static void ValidateTrolleyLockedAtProgress(
            Runtime runtime,
            Scenario scenario,
            int expectedCompletedOrders)
        {
            PlatformTrolleyConfig config = runtime.StaticData.PlatformTrolley;
            Require(expectedCompletedOrders < config.RequiredCompletedOrderCount &&
                    scenario.Store.CompletedOrderCount == expectedCompletedOrders &&
                    !scenario.Store.isTrolleyUpgradeUnlocked &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == null,
                "Locked trolley validation requires incomplete order progression.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.TrolleyUpgradeTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyUpgradeLocked,
                            expectedCompletedOrders,
                            config.RequiredCompletedOrderCount)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The locked trolley terminal did not present exact order progress.");

            int moneyBefore = scenario.Store.Money;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationTrolleyUpgradeLocked);
            Require(scenario.Store.Money == moneyBefore &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 0,
                "A locked trolley purchase changed money or created a trolley.");
            CleanupEvents(runtime);
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void ValidateTrolleyPurchaseSafetyReserve(
            Runtime runtime,
            Scenario scenario,
            ProductTypeId stockedProductType)
        {
            PlatformTrolleyConfig config = runtime.StaticData.PlatformTrolley;
            Require(!scenario.Store.isTrolleyUpgradeUnlocked &&
                    scenario.Store.CompletedOrderCount == 0 &&
                    scenario.Store.Money >= config.PurchasePrice &&
                    CountStockProducts(
                        runtime.Game,
                        scenario.StorageZone.EntityId,
                        stockedProductType) > 0 &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == null,
                "Trolley safety-reserve smoke requires affordable nominal money, " +
                "pre-purchased stock and no trolley.");

            scenario.Store.isTrolleyUpgradeUnlocked = true;
            scenario.Player.ReplaceFocusedEntityId(
                scenario.TrolleyUpgradeTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyPurchaseWouldBlockProjects)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The trolley prompt did not explain its protected project reserve.");

            int moneyBefore = scenario.Store.Money;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects);
            Require(scenario.Store.Money == moneyBefore &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 0,
                "A trolley debit rejected by the project reserve changed money or entities.");
            CleanupEvents(runtime);

            scenario.Store.isTrolleyUpgradeUnlocked = false;
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void RegisterRewardedOrderForTrolleyProgression(
            Runtime runtime,
            Scenario scenario,
            GameEntity rewardedOrder,
            int expectedCompletedOrders)
        {
            Require(rewardedOrder.isOrderRewarded &&
                    !rewardedOrder.isOrderProgressionCounted &&
                    scenario.Store.CompletedOrderCount == expectedCompletedOrders - 1,
                "Trolley progression requires one newly rewarded, uncounted order.");

            runtime.Systems.Create<RegisterCompletedOrderForProgressionSystem>().Execute();
            Require(rewardedOrder.isOrderProgressionCounted &&
                    scenario.Store.CompletedOrderCount == expectedCompletedOrders,
                "A rewarded order did not increment trolley progression exactly once.");
            runtime.Systems.Create<RegisterCompletedOrderForProgressionSystem>().Execute();
            Require(scenario.Store.CompletedOrderCount == expectedCompletedOrders,
                "The same rewarded order incremented trolley progression more than once.");
        }

        private static void UnlockTrolleyUpgrade(Runtime runtime, Scenario scenario)
        {
            PlatformTrolleyConfig config = runtime.StaticData.PlatformTrolley;
            Require(scenario.Store.CompletedOrderCount >=
                    config.RequiredCompletedOrderCount &&
                    !scenario.Store.isTrolleyUpgradeUnlocked,
                "Trolley unlock requires the configured rewarded-order count.");

            runtime.Systems.Create<UnlockPlatformTrolleyUpgradeSystem>().Execute();
            Require(scenario.Store.isTrolleyUpgradeUnlocked,
                "The trolley upgrade did not unlock at its exact progression threshold.");
            RequireNotificationKey(runtime, LocalizationKey.NotificationTrolleyUnlocked);
            runtime.Systems.Create<UnlockPlatformTrolleyUpgradeSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 1,
                "The trolley upgrade emitted its unlock notification more than once.");
            CleanupEvents(runtime);
        }

        private static GameEntity PurchaseTrolley(Runtime runtime, Scenario scenario)
        {
            PlatformTrolleyConfig config = runtime.StaticData.PlatformTrolley;
            Require(scenario.Store.isTrolleyUpgradeUnlocked &&
                    scenario.Store.Money >= config.PurchasePrice &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == null,
                "Trolley purchase requires one unlocked, affordable store without a trolley.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.TrolleyUpgradeTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPurchaseTrolley,
                            config.PurchasePrice)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "The unlocked trolley terminal did not present its purchase action.");

            int moneyBefore = scenario.Store.Money;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            GameEntity trolley = runtime.Game.GetEntityWithTrolleyStoreEntityId(
                scenario.Store.EntityId);
            Require(trolley != null &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 1 &&
                    scenario.Store.Money == moneyBefore - config.PurchasePrice,
                "The first trolley purchase did not create one entity and debit once.");
            RequireNotificationKey(runtime, LocalizationKey.NotificationTrolleyPurchased);

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            CleanupEvents(runtime);
            EntityBehaviour trolleyView = RequireRuntimeView(
                trolley,
                config.ViewPrefab,
                "platform trolley");
            Require(trolleyView is HardwareStore.Gameplay.Views.InteractionView &&
                    trolley.hasTransform && trolley.Transform == trolleyView.transform &&
                    trolley.hasRigidbody && trolley.Rigidbody.isKinematic &&
                    !trolley.Rigidbody.useGravity &&
                    trolley.hasSlots && trolley.Slots.Length == config.Capacity &&
                    trolley.Slots.Distinct().Count() == config.Capacity &&
                    trolley.TrolleyCapacity == config.Capacity &&
                    trolley.OccupiedTrolleySlotCount == 0 &&
                    Mathf.Approximately(
                        trolley.TrolleyMovementSpeed,
                        config.MovementSpeed) &&
                    Mathf.Approximately(
                        trolley.TrolleyFollowDistance,
                        config.FollowDistance) &&
                    Vector3.Distance(
                        trolley.Transform.position,
                        scenario.TrolleyUpgradeTerminal.TrolleySpawnPosition) < 0.001f &&
                    Quaternion.Angle(
                        trolley.Transform.rotation,
                        scenario.TrolleyUpgradeTerminal.TrolleySpawnRotation) < 0.01f,
                "The purchased trolley was not bound at its authored pose with three slots.");

            int moneyAfterPurchase = scenario.Store.Money;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationTrolleyAlreadyPurchased);
            Require(scenario.Store.Money == moneyAfterPurchase &&
                    runtime.Game.GetEntityWithTrolleyStoreEntityId(
                        scenario.Store.EntityId) == trolley &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 1,
                "A repeated trolley purchase created another entity or debited twice.");
            CleanupEvents(runtime);

            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptTrolleyPurchased)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "A purchased trolley terminal remained actionable.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
            return trolley;
        }

        private static void ValidateNoCustomerTrolleyControls(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley)
        {
            Require(runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0 &&
                    !scenario.Player.isHandsOccupied &&
                    !scenario.Player.isPushingTrolley &&
                    !trolley.hasTrolleyPusherEntityId &&
                    trolley.isInteractable,
                "No-customer trolley controls require cooldown, empty hands and a parked trolley.");

            CharacterController controller = scenario.Player.CharacterController;
            controller.enabled = false;
            scenario.Player.Transform.SetPositionAndRotation(
                new Vector3(0f, 0.02f, -5f),
                Quaternion.identity);
            controller.enabled = true;
            Vector3 parkedPosition = new(0f, 0.01f, -2.6f);
            trolley.Rigidbody.position = parkedPosition;
            trolley.Rigidbody.rotation = Quaternion.identity;
            trolley.Transform.SetPositionAndRotation(parkedPosition, Quaternion.identity);
            Physics.SyncTransforms();

            scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.hasFocusedInteractionType &&
                    scenario.Player.FocusedInteractionType ==
                    InteractionTypeId.PlatformTrolley &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPushTrolley,
                            trolley.OccupiedTrolleySlotCount,
                            trolley.TrolleyCapacity)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The single trolley handle point did not expose an F-only push prompt.");

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            runtime.Systems.Create<StartPushingTrolleySystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0 &&
                    !scenario.Player.isHandsOccupied &&
                    !scenario.Player.isPushingTrolley &&
                    !trolley.hasTrolleyPusherEntityId,
                "E attached an empty-handed player to the trolley instead of remaining a " +
                "world/product action.");
            CleanupEvents(runtime);

            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<StartPushingTrolleySystem>().Execute();
            CleanupEvents(runtime);
            runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    trolley.hasTrolleyPusherEntityId &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId &&
                    runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null,
                "F did not attach the purchased trolley during no-customer cooldown.");

            scenario.Input.isDropPressed = true;
            runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    trolley.hasTrolleyPusherEntityId,
                "G detached the trolley even though it is reserved for product drop.");
            CleanupEvents(runtime);

            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
            CleanupEvents(runtime);
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            Require(!scenario.Player.isHandsOccupied &&
                    !scenario.Player.isPushingTrolley &&
                    !trolley.hasTrolleyPusherEntityId &&
                    trolley.isInteractable &&
                    Mathf.Approximately(
                        scenario.Player.MovementSpeed,
                        scenario.Player.WalkSpeed) &&
                    runtime.Game.GetEntityWithCustomerVisitStoreEntityId(
                        scenario.Store.EntityId) == null,
                "The second F press did not detach the trolley cleanly without a customer.");
            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void LoadProductOnTrolley(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            GameEntity product)
        {
            Require(product.isInStock && product.hasStorageSlotIndex &&
                    trolley.OccupiedTrolleySlotCount < trolley.TrolleyCapacity,
                "Trolley loading requires slotted stock and a free cargo slot.");
            int storageSlotIndex = product.StorageSlotIndex;
            int occupiedBefore = trolley.OccupiedTrolleySlotCount;
            int storageCountBefore = scenario.StorageZone.StorageProductCount;

            PickUpProduct(runtime, scenario, product);
            int orderLineEntityId = product.ReservedOrderLineEntityId;
            Require(product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId,
                "Picked stock did not retain exact trolley recovery reservations.");

            scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPlaceProductOnTrolley,
                            LocalizedTexts.ProductName(product.ProductType),
                            occupiedBefore,
                            trolley.TrolleyCapacity)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "Held stock did not present the trolley cargo action.");
            RequestInteraction(scenario.Player, trolley);
            runtime.Systems.Create<LoadHeldProductOnTrolleySystem>().Execute();
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
            CleanupEvents(runtime);

            Require(!scenario.Player.isHandsOccupied &&
                    !scenario.Player.isCarryingProduct &&
                    !product.hasCarrierEntityId &&
                    product.hasTrolleyEntityId &&
                    product.TrolleyEntityId == trolley.EntityId &&
                    product.hasTrolleySlotIndex &&
                    product.TrolleySlotIndex == occupiedBefore &&
                    product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLineEntityId &&
                    !product.hasStorageSlotIndex &&
                    trolley.OccupiedTrolleySlotCount == occupiedBefore + 1 &&
                    product.Transform.parent == trolley.Slots[product.TrolleySlotIndex] &&
                    scenario.StorageZone.StorageProductCount == storageCountBefore,
                "Trolley loading changed reservation ownership, stock count or exact slot.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void ValidateLoadedTrolleyCargo(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            GameEntity[] products)
        {
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            ExecuteStorageState(runtime);
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            Require(products.Length == runtime.StaticData.PlatformTrolley.Capacity &&
                    trolley.OccupiedTrolleySlotCount == products.Length &&
                    runtime.Game.GetEntitiesWithTrolleyEntityId(trolley.EntityId).Count ==
                    products.Length &&
                    products.Select(product => product.TrolleySlotIndex)
                        .Distinct().Count() == products.Length &&
                    products.All(product =>
                        product.isInStock &&
                        product.hasReservedStorageSlotIndex &&
                        product.hasReservedOrderLineEntityId &&
                        product.hasTrolleyEntityId &&
                        product.TrolleyEntityId == trolley.EntityId &&
                        product.Transform.parent == trolley.Slots[product.TrolleySlotIndex]) &&
                    scenario.StorageZone.OccupiedStorageSlotCount ==
                    scenario.StorageZone.StorageProductCount,
                "Three trolley products did not retain distinct cargo and storage reservations.");
        }

        private static void ValidateTrolleyCargoInputRouting(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            GameEntity product)
        {
            Require(product.hasTrolleyEntityId &&
                    product.TrolleyEntityId == trolley.EntityId &&
                    product.hasTrolleySlotIndex &&
                    product.hasReservedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.isInteractable && product.hasColliders &&
                    !scenario.Player.isHandsOccupied &&
                    !trolley.hasTrolleyPusherEntityId,
                "Trolley input routing requires parked, interactable reserved cargo.");
            Vector3 flatYardPosition = new(0f, 0.01f, -2.6f);
            trolley.Rigidbody.position = flatYardPosition;
            trolley.Rigidbody.rotation = Quaternion.identity;
            trolley.Transform.SetPositionAndRotation(
                flatYardPosition,
                Quaternion.identity);
            Physics.SyncTransforms();

            Collider productCollider = product.Colliders.Single(collider => !collider.isTrigger);
            Transform cameraTransform = scenario.Player.Camera.transform;
            Vector3 originalPosition = cameraTransform.position;
            Quaternion originalRotation = cameraTransform.rotation;
            int trolleySlotIndex = product.TrolleySlotIndex;
            int reservedStorageSlotIndex = product.ReservedStorageSlotIndex;
            int reservedOrderLineEntityId = product.ReservedOrderLineEntityId;
            int occupiedSlotCount = trolley.OccupiedTrolleySlotCount;
            int storageProductCount = scenario.StorageZone.StorageProductCount;

            try
            {
                Vector3 target = productCollider.bounds.center;
                cameraTransform.SetPositionAndRotation(
                    target - Vector3.right * 1.6f,
                    Quaternion.LookRotation(Vector3.right, Vector3.up));
                Physics.SyncTransforms();
                if (scenario.Player.hasFocusedEntityId)
                    scenario.Player.RemoveFocusedEntityId();
                if (scenario.Player.hasFocusedInteractionType)
                    scenario.Player.RemoveFocusedInteractionType();

                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                runtime.Systems.Create<ClassifyFocusedInteractionSystem>().Execute();
                runtime.Systems.Create<InteractionPromptFeature>().Execute();
                Require(scenario.Player.hasFocusedEntityId &&
                        scenario.Player.FocusedEntityId == product.EntityId &&
                        scenario.Player.hasFocusedInteractionType &&
                        scenario.Player.FocusedInteractionType == InteractionTypeId.Product &&
                        scenario.Player.isFocusInteractionAvailable &&
                        product.isHighlighted &&
                        PromptMatches(
                            runtime,
                            scenario.Player,
                            LocalizedTexts.Text(
                                LocalizationKey.PromptProductAndTrolleyActions,
                                LocalizedTexts.Text(
                                    LocalizationKey.PromptPickStockProduct,
                                    LocalizedTexts.ProductName(product.ProductType)))),
                    "The single trolley point occluded its cargo or replaced the cargo E action.");

                scenario.Input.isTrolleyPressed = true;
                runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
                runtime.Systems.Create<PickUpProductSystem>().Execute();
                runtime.Systems.Create<StartPushingTrolleySystem>().Execute();
                Require(scenario.Player.isHandsOccupied &&
                        scenario.Player.isPushingTrolley &&
                        !scenario.Player.isCarryingProduct &&
                        trolley.hasTrolleyPusherEntityId &&
                        trolley.TrolleyPusherEntityId == scenario.Player.EntityId &&
                        !product.hasCarrierEntityId &&
                        product.hasTrolleyEntityId &&
                        product.TrolleyEntityId == trolley.EntityId &&
                        product.TrolleySlotIndex == trolleySlotIndex &&
                        product.ReservedStorageSlotIndex == reservedStorageSlotIndex &&
                        product.ReservedOrderLineEntityId == reservedOrderLineEntityId &&
                        !product.isHighlighted &&
                        !trolley.isHighlighted &&
                        runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                    "F on trolley cargo picked the product or lost its slot/reservation instead " +
                    "of attaching the owner trolley.");
                CleanupEvents(runtime);

                scenario.Input.isTrolleyPressed = true;
                runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
                CleanupEvents(runtime);
                Require(!scenario.Player.isHandsOccupied &&
                        !scenario.Player.isPushingTrolley &&
                        trolley.isInteractable &&
                        !trolley.hasTrolleyPusherEntityId &&
                        product.hasTrolleyEntityId &&
                        product.TrolleyEntityId == trolley.EntityId,
                    "F cargo-proxy detach changed product ownership.");

                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                runtime.Systems.Create<ClassifyFocusedInteractionSystem>().Execute();
                runtime.Systems.Create<InteractionPromptFeature>().Execute();
                Require(scenario.Player.hasFocusedEntityId &&
                        scenario.Player.FocusedEntityId == product.EntityId &&
                        scenario.Player.FocusedInteractionType == InteractionTypeId.Product &&
                        scenario.Player.isFocusInteractionAvailable,
                    "Cargo product focus was not restored after F detach.");

                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
                GameEntity[] productRequests = runtime.Game
                    .GetGroup(GameMatcher.InteractionRequest)
                    .GetEntities();
                Require(productRequests.Length == 1 &&
                        productRequests[0].SourceEntityId == scenario.Player.EntityId &&
                        productRequests[0].TargetEntityId == product.EntityId,
                    "E on trolley cargo did not target the focused product exactly once.");
                runtime.Systems.Create<PickUpProductSystem>().Execute();
                runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
                ExecuteProductPlacement(runtime);
                runtime.Systems.Create<FollowHeldProductSystem>().Execute();
                ExecuteStorageState(runtime);
                runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
                runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
                CleanupEvents(runtime);
                Require(scenario.Player.isHandsOccupied &&
                        scenario.Player.isCarryingProduct &&
                        !scenario.Player.isPushingTrolley &&
                        product.hasCarrierEntityId &&
                        product.CarrierEntityId == scenario.Player.EntityId &&
                        !product.hasTrolleyEntityId &&
                        !product.hasTrolleySlotIndex &&
                        product.ReservedStorageSlotIndex == reservedStorageSlotIndex &&
                        product.ReservedOrderLineEntityId == reservedOrderLineEntityId &&
                        trolley.OccupiedTrolleySlotCount == occupiedSlotCount - 1 &&
                        scenario.StorageZone.StorageProductCount == storageProductCount,
                    "E did not pick trolley cargo while preserving its recovery reservations.");

                scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(scenario.Player.FocusedInteractionType ==
                        InteractionTypeId.PlatformTrolley &&
                        scenario.Player.isFocusInteractionAvailable &&
                        PromptMatches(
                            runtime,
                            scenario.Player,
                            LocalizedTexts.Text(
                                LocalizationKey.PromptPlaceProductOnTrolley,
                                LocalizedTexts.ProductName(product.ProductType),
                                occupiedSlotCount - 1,
                                trolley.TrolleyCapacity)),
                    "The E-picked product could not be returned to its trolley.");
                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
                GameEntity[] trolleyRequests = runtime.Game
                    .GetGroup(GameMatcher.InteractionRequest)
                    .GetEntities();
                Require(trolleyRequests.Length == 1 &&
                        trolleyRequests[0].TargetEntityId == trolley.EntityId,
                    "Returning cargo with E did not target the trolley exactly once.");
                runtime.Systems.Create<LoadHeldProductOnTrolleySystem>().Execute();
                runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
                runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
                CleanupEvents(runtime);
                Require(!scenario.Player.isHandsOccupied &&
                        !scenario.Player.isCarryingProduct &&
                        !product.hasCarrierEntityId &&
                        product.hasTrolleyEntityId &&
                        product.TrolleyEntityId == trolley.EntityId &&
                        product.hasTrolleySlotIndex &&
                        product.TrolleySlotIndex == trolleySlotIndex &&
                        product.ReservedStorageSlotIndex == reservedStorageSlotIndex &&
                        product.ReservedOrderLineEntityId == reservedOrderLineEntityId &&
                        trolley.OccupiedTrolleySlotCount == occupiedSlotCount &&
                        product.Transform.parent == trolley.Slots[trolleySlotIndex] &&
                        scenario.StorageZone.StorageProductCount == storageProductCount,
                    "E cargo round-trip did not restore the exact trolley slot and reservations.");
            }
            finally
            {
                cameraTransform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                if (scenario.Player.hasFocusedEntityId)
                    scenario.Player.RemoveFocusedEntityId();
                if (scenario.Player.hasFocusedInteractionType)
                    scenario.Player.RemoveFocusedInteractionType();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
            }
        }

        private static void ValidateFullTrolleyRejectsFourthProduct(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            GameEntity visit,
            GameEntity product)
        {
            Require(trolley.OccupiedTrolleySlotCount == trolley.TrolleyCapacity &&
                    product.isInStock && product.hasStorageSlotIndex &&
                    !scenario.Player.isHandsOccupied,
                "Full-trolley rejection requires one extra slotted stock product.");
            GameEntity orderLine = FindOrderLine(
                GetOrderLines(runtime.Game, visit),
                product.ProductType);
            int storageSlotIndex = product.StorageSlotIndex;
            int stockCountBefore = scenario.StorageZone.StorageProductCount;

            product.RemoveStorageSlotIndex();
            product.AddReservedStorageSlotIndex(storageSlotIndex);
            product.AddReservedOrderLineEntityId(orderLine.EntityId);
            product.AddCarrierEntityId(scenario.Player.EntityId);
            product.isInteractable = false;
            scenario.Player.isCarryingProduct = true;
            scenario.Player.isHandsOccupied = true;

            scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptTrolleyFull,
                            trolley.OccupiedTrolleySlotCount,
                            trolley.TrolleyCapacity)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "A full trolley did not disable its fourth cargo action.");
            RequestInteraction(scenario.Player, trolley);
            runtime.Systems.Create<LoadHeldProductOnTrolleySystem>().Execute();
            RequireNotificationKey(runtime, LocalizationKey.NotificationTrolleyFull);
            Require(product.hasCarrierEntityId &&
                    !product.hasTrolleyEntityId &&
                    !product.hasTrolleySlotIndex &&
                    trolley.OccupiedTrolleySlotCount == trolley.TrolleyCapacity &&
                    runtime.Game.GetEntitiesWithTrolleyEntityId(trolley.EntityId).Count ==
                    trolley.TrolleyCapacity,
                "A fourth product bypassed the trolley capacity guard.");

            product.RemoveCarrierEntityId();
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddStorageSlotIndex(storageSlotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
            scenario.Player.isCarryingProduct = false;
            scenario.Player.isHandsOccupied = false;
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            CleanupEvents(runtime);
            Require(product.hasStorageSlotIndex &&
                    product.StorageSlotIndex == storageSlotIndex &&
                    product.Transform.parent ==
                    scenario.StorageZone.Slots[storageSlotIndex] &&
                    scenario.StorageZone.StorageProductCount == stockCountBefore &&
                    !scenario.Player.isHandsOccupied,
                "The rejected fourth product did not restore its exact storage state.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void ValidateTrolleyPushFlow(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley)
        {
            Require(!scenario.Player.isHandsOccupied && trolley.isInteractable &&
                    !trolley.hasTrolleyPusherEntityId,
                "Trolley push smoke requires a parked trolley and empty hands.");
            CharacterController controller = scenario.Player.CharacterController;
            controller.enabled = false;
            scenario.Player.Transform.SetPositionAndRotation(
                new Vector3(0f, 0.02f, -5f),
                Quaternion.identity);
            controller.enabled = true;
            Vector3 parkedTrolleyPosition = new(0f, 0.01f, -2.6f);
            trolley.Rigidbody.position = parkedTrolleyPosition;
            trolley.Rigidbody.rotation = Quaternion.identity;
            trolley.Transform.SetPositionAndRotation(
                parkedTrolleyPosition,
                Quaternion.identity);
            Physics.SyncTransforms();

            scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPushTrolley,
                            trolley.OccupiedTrolleySlotCount,
                            trolley.TrolleyCapacity)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "A parked trolley did not expose its dedicated F push action.");

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                "E emitted a trolley interaction request while empty-handed.");
            CleanupEvents(runtime);

            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<StartPushingTrolleySystem>().Execute();
            CleanupEvents(runtime);
            runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    !scenario.Player.isCarryingProduct &&
                    trolley.hasTrolleyPusherEntityId &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId &&
                    runtime.Game.GetEntityWithTrolleyPusherEntityId(
                        scenario.Player.EntityId) == trolley &&
                    !trolley.isInteractable,
                "Starting trolley push did not establish the exclusive handling relation.");

            scenario.Input.isSprintHeld = true;
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            Require(Mathf.Approximately(
                    scenario.Player.MovementSpeed,
                    runtime.StaticData.PlatformTrolley.MovementSpeed),
                "Pushing did not apply the configured trolley movement speed.");
            scenario.Input.isSprintHeld = false;

            runtime.Systems.Create<FollowPushedTrolleySystem>().Execute();
            Physics.SyncTransforms();
            Vector3 expectedPosition = scenario.Player.Transform.position +
                                       scenario.Player.Transform.forward *
                                       trolley.TrolleyFollowDistance;
            Collider trolleyBody = trolley.Colliders.Single(collider => !collider.isTrigger);
            Collider[] cargoBodies = runtime.Game
                .GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                .SelectMany(product => product.Colliders.Where(collider => !collider.isTrigger))
                .ToArray();
            Require(Vector3.Distance(trolley.Transform.position, expectedPosition) < 0.001f &&
                    Quaternion.Angle(
                        trolley.Transform.rotation,
                        scenario.Player.Transform.rotation) < 0.01f &&
                    !trolleyBody.bounds.Intersects(
                        scenario.Player.CharacterController.bounds) &&
                    cargoBodies.All(collider =>
                        trolleyBody.bounds.max.y <= collider.bounds.min.y + 0.001f),
                "Trolley follow pose overlaps the player capsule or its reserved cargo.");

            ValidateBlockedTrolleyMotion(runtime, scenario, trolley, trolleyBody);

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            scenario.Player.isFocusInteractionAvailable = true;
            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0 &&
                    scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    trolley.hasTrolleyPusherEntityId &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId,
                "E emitted a world request or detached the pushed trolley.");
            CleanupEvents(runtime);

            RequestInteraction(scenario.Player, scenario.ProcurementTerminal);
            runtime.Systems.Create<OpenProcurementSystem>().Execute();
            RequireNotificationKey(runtime, LocalizationKey.NotificationReleaseTrolleyFirst);
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasProcurementTerminalEntityId,
                "Procurement opened while the player was pushing the trolley.");
            CleanupEvents(runtime);

            RequestInteraction(scenario.Player, scenario.OrderCounter);
            runtime.Systems.Create<OpenConsultationSystem>().Execute();
            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId,
                "Consultation opened while the player was pushing the trolley.");
            CleanupEvents(runtime);

            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
            CleanupEvents(runtime);
            runtime.Systems.Create<ResolveMovementSpeedSystem>().Execute();
            runtime.Systems.Create<ValidatePlayerHandlingStateSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            Require(!scenario.Player.isHandsOccupied &&
                    !scenario.Player.isPushingTrolley &&
                    !trolley.hasTrolleyPusherEntityId &&
                    runtime.Game.GetEntityWithTrolleyPusherEntityId(
                        scenario.Player.EntityId) == null &&
                    trolley.isInteractable &&
                    Mathf.Approximately(
                        scenario.Player.MovementSpeed,
                        scenario.Player.WalkSpeed),
                "Dropping the trolley did not restore empty-hand walking state.");
            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            scenario.Player.isFocusInteractionAvailable = false;
            ExecuteInteractionPrompts(runtime);
        }

        private static void ValidateBlockedTrolleyMotion(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            Collider trolleyBody)
        {
            GameEntity[] cargo = runtime.Game
                .GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                .OrderBy(product => product.TrolleySlotIndex)
                .ToArray();
            Require(cargo.Length == trolley.TrolleyCapacity &&
                    cargo.All(product =>
                        product.hasReservedStorageSlotIndex &&
                        product.hasReservedOrderLineEntityId),
                "Blocked trolley motion requires a full cart with preserved stock reservations.");

            Vector3 trolleyPositionBefore = trolley.Transform.position;
            Quaternion trolleyRotationBefore = trolley.Transform.rotation;
            Vector3 rigidbodyPositionBefore = trolley.Rigidbody.position;
            Quaternion rigidbodyRotationBefore = trolley.Rigidbody.rotation;
            Vector3[] cargoPositionsBefore = cargo
                .Select(product => product.Transform.position)
                .ToArray();
            int[] trolleySlotsBefore = cargo
                .Select(product => product.TrolleySlotIndex)
                .ToArray();
            int[] storageSlotsBefore = cargo
                .Select(product => product.ReservedStorageSlotIndex)
                .ToArray();
            int[] orderLinesBefore = cargo
                .Select(product => product.ReservedOrderLineEntityId)
                .ToArray();

            CharacterController controller = scenario.Player.CharacterController;
            controller.enabled = false;
            scenario.Player.Transform.position += Vector3.back * 1.4f;
            controller.enabled = true;
            Vector3 blockedTargetPosition = scenario.Player.Transform.position +
                                            scenario.Player.Transform.forward *
                                            trolley.TrolleyFollowDistance;
            Quaternion blockedTargetRotation = scenario.Player.Transform.rotation;
            BoxCollider bodyBox = trolleyBody as BoxCollider;
            Require(bodyBox != null,
                "Collision-safe trolley smoke requires the authored BoxCollider hull.");
            Vector3 blockedHullCenter = blockedTargetPosition +
                                        blockedTargetRotation * bodyBox.center;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Smoke Collision-Safe Trolley Wall";
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            Require(ignoreRaycastLayer >= 0,
                "The built-in Ignore Raycast layer is required for trolley motion smoke.");
            wall.layer = ignoreRaycastLayer;
            wall.transform.SetPositionAndRotation(
                blockedHullCenter,
                blockedTargetRotation);
            wall.transform.localScale = new Vector3(
                bodyBox.size.x + 0.2f,
                bodyBox.size.y + 0.2f,
                0.2f);
            Physics.SyncTransforms();

            try
            {
                runtime.Systems.Create<FollowPushedTrolleySystem>().Execute();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
            }

            Require(Vector3.Distance(trolley.Transform.position, trolleyPositionBefore) <
                    0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, trolleyRotationBefore) <
                    0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, rigidbodyPositionBefore) <
                    0.001f &&
                    Quaternion.Angle(trolley.Rigidbody.rotation, rigidbodyRotationBefore) <
                    0.001f,
                "A blocked trolley collision query changed its Transform or Rigidbody pose.");
            Require(cargo.Select(product => product.TrolleySlotIndex)
                        .SequenceEqual(trolleySlotsBefore) &&
                    cargo.Select(product => product.ReservedStorageSlotIndex)
                        .SequenceEqual(storageSlotsBefore) &&
                    cargo.Select(product => product.ReservedOrderLineEntityId)
                        .SequenceEqual(orderLinesBefore) &&
                    cargo.Select(product => product.Transform.position)
                        .Zip(cargoPositionsBefore, Vector3.Distance)
                        .All(distance => distance < 0.001f) &&
                    scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    !scenario.Player.isCarryingProduct &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId,
                "Blocked trolley motion changed cargo, reservations or pushing relations.");

            runtime.Systems.Create<FollowPushedTrolleySystem>().Execute();
            Physics.SyncTransforms();
            Require(Vector3.Distance(trolley.Transform.position, blockedTargetPosition) <
                    0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, blockedTargetRotation) <
                    0.001f &&
                    cargo.All(product =>
                        product.hasTrolleyEntityId &&
                        product.TrolleyEntityId == trolley.EntityId &&
                        product.hasReservedStorageSlotIndex &&
                        product.hasReservedOrderLineEntityId),
                "Trolley did not resume collision-safe follow after the wall was removed.");
        }

        private static void LoadTrolleyProductIntoOrder(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity trolley,
            GameEntity product,
            bool expectCompleted)
        {
            GameEntity[] orderLines = GetOrderLines(runtime.Game, visit);
            GameEntity orderLine = FindOrderLine(orderLines, product.ProductType);
            int totalLoadedBefore = orderLines.Sum(line => line.LoadedProductCount);
            int lineLoadedBefore = orderLine.LoadedProductCount;
            int trolleyCargoBefore = trolley.OccupiedTrolleySlotCount;
            int reservedStorageSlotIndex = product.ReservedStorageSlotIndex;
            Require(visit.isCustomerVisitLoading &&
                    product.hasTrolleyEntityId &&
                    product.TrolleyEntityId == trolley.EntityId &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId,
                "Trolley order loading requires reserved cargo for the active order.");

            PickUpProduct(runtime, scenario, product);
            Require(!product.hasTrolleyEntityId &&
                    !product.hasTrolleySlotIndex &&
                    product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == reservedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId,
                "Picking cargo from the trolley lost its storage or order reservation.");
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            Require(trolley.OccupiedTrolleySlotCount == trolleyCargoBefore - 1,
                "Picking trolley cargo did not release its exact cargo slot.");

            RequestInteraction(scenario.Player, visit);
            runtime.Systems.Create<LoadHeldProductSystem>().Execute();
            Require(product.isProductLoaded && product.isLoaded &&
                    product.hasOrderLineEntityId &&
                    product.OrderLineEntityId == orderLine.EntityId &&
                    product.hasLoadingSlotIndex &&
                    product.LoadingSlotIndex == totalLoadedBefore &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.hasCarrierEntityId &&
                    !scenario.Player.isHandsOccupied,
                "Trolley cargo did not convert its reservation into loaded order ownership.");

            runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
            Require(!product.isProductLoaded &&
                    orderLine.LoadedProductCount == lineLoadedBefore + 1,
                "Trolley cargo ProductLoaded was not consumed exactly once.");
            runtime.Systems.Create<CompleteOrderSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            runtime.Systems.Create<RefreshTrolleyOccupiedSlotCountSystem>().Execute();
            runtime.Systems.Create<ValidatePlatformTrolleyStateSystem>().Execute();
            CleanupEvents(runtime);

            Require(visit.isCustomerVisitCompleted == expectCompleted &&
                    visit.isCustomerVisitLoading != expectCompleted &&
                    product.isLoaded && !product.isInStock &&
                    product.hasOrderLineEntityId &&
                    product.OrderLineEntityId == orderLine.EntityId &&
                    product.Transform.parent == visit.Slots[product.LoadingSlotIndex] &&
                    !product.hasTrolleyEntityId &&
                    !product.hasTrolleySlotIndex,
                expectCompleted
                    ? "The mixed trolley order did not complete after its final cargo item."
                    : "The mixed trolley order completed before its final cargo item.");
        }

        private static void LoadAndRewardCustomerOrder(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity[] products)
        {
            Require(visit.isCustomerVisitLoading,
                "Products can only be loaded during CustomerVisitLoading.");
            Require(products.Length == GetOrderLines(runtime.Game, visit)
                    .Sum(line => line.RequiredProductCount),
                "The smoke must load exactly the configured number of order products.");

            for (int productIndex = 0; productIndex < products.Length; productIndex++)
            {
                LoadOrderProduct(
                    runtime,
                    scenario,
                    visit,
                    products[productIndex],
                    expectCompleted: productIndex == products.Length - 1);
            }

            RewardCustomerOrder(runtime, scenario, visit, products);
        }

        private static void LoadOrderProduct(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity product,
            bool expectCompleted)
        {
            Require(visit.isCustomerVisitLoading,
                "Products can only be loaded during CustomerVisitLoading.");
            GameEntity[] orderLines = GetOrderLines(runtime.Game, visit);
            GameEntity orderLine = FindOrderLine(orderLines, product.ProductType);
            int totalLoadedBefore = orderLines.Sum(line => line.LoadedProductCount);
            int lineLoadedBefore = orderLine.LoadedProductCount;
            int storageSlotIndex = product.StorageSlotIndex;
            Require(lineLoadedBefore < orderLine.RequiredProductCount,
                $"Order line {orderLine.EntityId} is already complete.");

            scenario.Player.ReplaceFocusedEntityId(product.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPickStockProduct,
                            LocalizedTexts.ProductName(product.ProductType))),
                $"Stock product {product.EntityId} has no active-order prompt.");
            scenario.Player.RemoveFocusedEntityId();

            PickUpProduct(runtime, scenario, product);
            Require(product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    product.isInStock &&
                    !product.hasStorageSlotIndex &&
                    product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == storageSlotIndex &&
                    product.hasReservedOrderLineEntityId &&
                    product.ReservedOrderLineEntityId == orderLine.EntityId,
                $"Stock product {product.EntityId} was not picked for loading.");

            RequestInteraction(scenario.Player, visit);
            runtime.Systems.Create<LoadHeldProductSystem>().Execute();
            Require(product.isProductLoaded &&
                    product.isLoaded &&
                    product.hasOrderLineEntityId &&
                    product.OrderLineEntityId == orderLine.EntityId &&
                    product.hasLoadingSlotIndex &&
                    product.LoadingSlotIndex == totalLoadedBefore &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.hasCarrierEntityId &&
                    !scenario.Player.isHandsOccupied,
                $"ProductLoaded was not raised on product {product.EntityId}.");

            runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
            Require(!product.isProductLoaded &&
                    orderLine.LoadedProductCount == lineLoadedBefore + 1 &&
                    runtime.Game.GetEntitiesWithOrderLineEntityId(orderLine.EntityId)
                        .Count == orderLine.LoadedProductCount,
                $"ProductLoaded was not consumed for product {product.EntityId}.");
            runtime.Systems.Create<CompleteOrderSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);

            Require(visit.isCustomerVisitCompleted == expectCompleted &&
                    visit.isCustomerVisitLoading != expectCompleted,
                expectCompleted
                    ? "The order did not complete after its final product."
                    : "The order completed before its final product.");
            Require(product.isLoaded &&
                    !product.isInStock &&
                    product.hasOrderLineEntityId &&
                    product.OrderLineEntityId == orderLine.EntityId &&
                    product.Transform.parent == visit.Slots[product.LoadingSlotIndex] &&
                    !product.isProductPlacementDirty,
                $"Loaded product {product.EntityId} has invalid visit placement.");
        }

        private static void RewardCustomerOrder(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit,
            GameEntity[] products)
        {
            int moneyBeforeReward = scenario.Store.Money;
            Require(visit.isCustomerVisitCompleted &&
                    !visit.isCustomerVisitLoading &&
                    !visit.isOrderRewarded &&
                    GetOrderLines(runtime.Game, visit).All(line =>
                        line.LoadedProductCount == line.RequiredProductCount),
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
            GameEntity[] orderLines,
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
            int[] orderLineIds = orderLines
                .Select(line => line.EntityId)
                .ToArray();
            int visitId = entity.EntityId;
            runtime.Systems.Create<CompleteCustomerVehicleDepartureSystem>().Execute();
            Require(!entity.isDestructed && !entity.isOrderContentReleased &&
                    entity.hasCustomerVisitStoreEntityId,
                "Vehicle departure completed before order content was released.");

            runtime.Systems.Create<ReleaseDepartedOrderContentSystem>().Execute();
            Require(entity.isOrderContentReleased &&
                    loadedProducts.All(product => product.isDestructed &&
                                                  !product.hasOrderLineEntityId) &&
                    orderLines.All(line => line.isDestructed &&
                                           !line.hasOrderEntityId) &&
                    runtime.Game.GetEntitiesWithOrderEntityId(visitId).Count == 0 &&
                    orderLineIds.All(lineId =>
                        runtime.Game.GetEntitiesWithOrderLineEntityId(lineId).Count == 0),
                "Departed order content did not release relation indices before Destructed.");

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
                    orderLineIds.All(lineId =>
                        runtime.Game.GetEntityWithEntityId(lineId) == null) &&
                    runtime.Game.GetGroup(GameMatcher.Loaded).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderLine).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.OrderLineEntityId).count == 0 &&
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
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCounterCustomerReturning)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The order counter remained interactive while the customer was returning.");

            scenario.Player.ReplaceFocusedEntityId(scenario.ProcurementTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptOpenProcurement)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "A delivery-free terminal did not stay available while the customer returned.");

            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
            int moneyBeforeCatalog = scenario.Store.Money;
            OpenProcurement(runtime, scenario);
            ProcurementSnapshot snapshot = CaptureProcurementSnapshot(runtime, scenario);
            Require(snapshot.DemandKind == ProcurementDemandKind.ProjectForecast &&
                    scenario.Store.Money == moneyBeforeCatalog &&
                    runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                        scenario.ProcurementTerminal.EntityId) == null,
                "Returning-customer procurement did not present the upcoming project forecast.");
            CancelProcurement(runtime, scenario, moneyBeforeCatalog);

            Require(!visit.isInteractable,
                "The loading zone remained interactable while the customer was returning.");

            GameEntity stockProduct = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .FirstOrDefault();
            if (stockProduct != null)
            {
                scenario.Player.ReplaceFocusedEntityId(stockProduct.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(PromptMatches(
                            runtime,
                            scenario.Player,
                            LocalizedTexts.Text(
                                LocalizationKey.PromptCustomerReturningWait)) &&
                        !scenario.Player.isFocusInteractionAvailable,
                    "Stock remained available while the customer was returning.");
            }

            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void ValidateCooldownSafety(Runtime runtime, Scenario scenario)
        {
            ValidateCooldownPresentation(runtime, scenario);
            GameEntity remainingStock = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .FirstOrDefault();
            if (remainingStock != null)
            {
                scenario.Player.ReplaceFocusedEntityId(remainingStock.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(PromptMatches(
                            runtime,
                            scenario.Player,
                            LocalizedTexts.Text(
                                LocalizationKey.PromptNoCustomerProductNotRequired)) &&
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
            }

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

        private static void ExecuteStorageState(Runtime runtime) =>
            runtime.Systems.Create<StorageStateFeature>().Execute();

        private static void ExecuteInteractionPrompts(Runtime runtime)
        {
            runtime.Systems.Create<ClassifyFocusedInteractionSystem>().Execute();
            runtime.Systems.Create<InteractionPromptFeature>().Execute();
        }

        private static bool PromptMatches(Runtime runtime, GameEntity player,
            LocalizedText expected) =>
            player.hasInteractionPrompt &&
            player.InteractionPrompt.Key == expected.Key &&
            runtime.Localization.Resolve(player.InteractionPrompt) ==
            runtime.Localization.Resolve(expected);

        private static void RequireNotificationKey(Runtime runtime,
            LocalizationKey expectedKey)
        {
            GameEntity[] notifications = runtime.Game
                .GetGroup(GameMatcher.NotificationMessage)
                .GetEntities();
            Require(notifications.Length == 1 &&
                    notifications[0].NotificationMessage.Key == expectedKey &&
                    !string.IsNullOrWhiteSpace(runtime.Localization.Resolve(
                        notifications[0].NotificationMessage)),
                $"Expected one resolvable {expectedKey} notification event.");
        }

        private static void ValidateRussianLocalization(
            ILocalizationService localization)
        {
            Require(localization.Language == LanguageId.Russian &&
                    localization.Culture.Name == "ru-RU",
                "The smoke localization service did not load Russian with ru-RU culture.");
            Require(localization.Resolve(
                        LocalizedTexts.ProductName(ProductTypeId.CementBag)) ==
                    "Цемент 25 кг",
                "The Russian catalog did not resolve representative product content.");
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptPickStockProduct,
                        LocalizedTexts.ProductName(ProductTypeId.BoardBundle))) ==
                    "E — взять со склада • товар: Пачка досок",
                "Nested localized product content did not resolve inside a prompt.");
            string formattedMoney = string.Format(localization.Culture, "{0:N0}", 1550);
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationOrderCompleted,
                        1550)) ==
                    $"Заказ выполнен: +{formattedMoney} ₽",
                "Russian money formatting did not use the loaded localization culture.");
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptTrolleyUpgradeLocked,
                        1,
                        2)) ==
                    "Тележка откроется после заказов • выполнено 1/2" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.WorldTrolleyUpgrade,
                        200)) ==
                    "ПЛАТФОРМЕННАЯ ТЕЛЕЖКА • 200 ₽",
                "Russian trolley localization did not preserve key arity or price formatting.");
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationProductDropBlocked)) ==
                    "Недостаточно места, чтобы бросить товар",
                "Russian collision-safe drop localization did not preserve its zero-argument text.");
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.ProcurementStatusPlanWouldBlockForecast)) ==
                    "Не хватит денег или мест на складе для ближайших проектов" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationPurchaseWouldBlockOrder)) ==
                    "Покупка отменена: не хватит денег или мест для заказа и ближайших проектов" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptTrolleyPurchaseWouldBlockProjects)) ==
                    "Покупка недоступна: деньги нужны для ближайших проектов" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects)) ==
                    "Покупка тележки отменена: деньги нужны для ближайших проектов",
                "Russian project-reserve localization changed or gained arguments.");
        }

        private static ILocalizationService CreateRussianLocalization()
        {
            var localization = new LocalizationService(new ILocalizationCatalog[]
            {
                new RussianLocalizationCatalog()
            });
            localization.Load(LanguageId.Russian);
            return localization;
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
            Require(runtime.Game.GetGroup(GameMatcher.PurchaseDeliveryRequest).count == 0,
                "A purchase-delivery request survived cleanup.");
            Require(runtime.Game.GetGroup(GameMatcher.PurchaseDeliverySucceeded).count == 0,
                "A purchase success marker survived its request cleanup.");
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
                container.Resolve<IStaticDataService>(),
                container.Resolve<IProcurementSolvencyService>(),
                container.Resolve<IEconomySolvencyService>(),
                container.Resolve<ILocalizationService>());
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
            context.GetEntitiesWithDeliveryEntityId(deliveryEntityId)
                .Where(product => product.hasEntityId &&
                                  product.isProduct &&
                                  product.isInboundProduct)
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
                IStaticDataService staticData,
                IProcurementSolvencyService procurementSolvency,
                IEconomySolvencyService economySolvency,
                ILocalizationService localization)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
                StaticData = staticData;
                ProcurementSolvency = procurementSolvency;
                EconomySolvency = economySolvency;
                Localization = localization;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
            public IStaticDataService StaticData { get; }
            public IProcurementSolvencyService ProcurementSolvency { get; }
            public IEconomySolvencyService EconomySolvency { get; }
            public ILocalizationService Localization { get; }
        }

        private sealed class CaptureHudService : IHudService
        {
            public ProcurementSnapshot? Procurement { get; private set; }

            public void Present(HudSnapshot snapshot)
            {
            }

            public void PresentConsultation(ConsultationSnapshot? snapshot)
            {
            }

            public void PresentProcurement(ProcurementSnapshot? snapshot) =>
                Procurement = snapshot;
        }

        private readonly struct Scenario
        {
            public Scenario(
                GameEntity player,
                GameEntity store,
                GameEntity orderCounter,
                GameEntity procurementTerminal,
                GameEntity storageZone,
                GameEntity trolleyUpgradeTerminal,
                InputEntity input)
            {
                Player = player;
                Store = store;
                OrderCounter = orderCounter;
                ProcurementTerminal = procurementTerminal;
                StorageZone = storageZone;
                TrolleyUpgradeTerminal = trolleyUpgradeTerminal;
                Input = input;
            }

            public GameEntity Player { get; }
            public GameEntity Store { get; }
            public GameEntity OrderCounter { get; }
            public GameEntity ProcurementTerminal { get; }
            public GameEntity StorageZone { get; }
            public GameEntity TrolleyUpgradeTerminal { get; }
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
