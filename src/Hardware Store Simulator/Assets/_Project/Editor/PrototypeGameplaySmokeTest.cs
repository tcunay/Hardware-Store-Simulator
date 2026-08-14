using System;
using System.Collections.Generic;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Common.Customers;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Common.Navigation;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Features.Carrying.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Consultation;
using HardwareStore.Gameplay.Features.Consultation.Systems;
using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Gameplay.Features.Delivery.Systems;
using HardwareStore.Gameplay.Features.Employees;
using HardwareStore.Gameplay.Features.Employees.Systems;
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
using HardwareStore.Gameplay.Features.StoreDay;
using HardwareStore.Gameplay.Features.StoreDay.Systems;
using HardwareStore.Gameplay.Features.StoreSceneBindings.Systems;
using HardwareStore.Gameplay.Features.Trolley.Systems;
using HardwareStore.Gameplay.Factories;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Gameplay.Views;
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
        [MenuItem("Tools/Hardware Store/Prepare Store Day Morning Visual Check")]
        public static void PrepareStoreDayMorningVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            runtime.Systems.Create<PresentHudSystem>().Execute();
            runtime.Systems.Create<PresentDayNightSystem>().Execute();

            Debug.Log(
                $"[Hardware Store] Morning visual check prepared: day " +
                $"{scenario.Store.DayNumber}, minute {scenario.Store.CurrentDayMinute:0}.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Store Day Night Visual Check")]
        public static void PrepareStoreDayNightVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            PrepareClosingVisualState(runtime, scenario);
            runtime.Systems.Create<PresentHudSystem>().Execute();
            runtime.Systems.Create<PresentDayNightSystem>().Execute();

            Debug.Log(
                $"[Hardware Store] Night visual check prepared: day " +
                $"{scenario.Store.DayNumber}, minute {scenario.Store.CurrentDayMinute:0}.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Store Day Report Visual Check")]
        public static void PrepareStoreDayReportVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            PrepareClosingVisualState(runtime, scenario);
            scenario.Store.ReplaceDayOpeningBalance(1000);
            scenario.Store.ReplaceDayRevenue(900);
            scenario.Store.ReplaceDayProcurementExpenses(600);
            scenario.Store.ReplaceDayUpgradeExpenses(200);
            scenario.Store.ReplaceDayPayrollExpenses(200);
            scenario.Store.ReplaceDayCompletedOrderCount(2);
            scenario.Store.ReplaceDayLostCustomerCount(3);
            scenario.Store.ReplaceMoney(900);
            Require(scenario.Store.Money ==
                    scenario.Store.DayOpeningBalance + scenario.Store.DayRevenue -
                    scenario.Store.DayProcurementExpenses -
                    scenario.Store.DayUpgradeExpenses -
                    scenario.Store.DayPayrollExpenses,
                "Representative report values must satisfy the complete day ledger.");

            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            runtime.Systems.Create<PresentHudSystem>().Execute();
            runtime.Systems.Create<PresentDayNightSystem>().Execute();
            runtime.Systems.Create<PresentDayReportSystem>().Execute();
            CleanupEvents(runtime);

            Debug.Log(
                "[Hardware Store] Store day report visual check prepared with representative " +
                "revenue, expenses, balance, order and stock rows.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Store Day Fade Visual Check")]
        public static void PrepareStoreDayFadeVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            PrepareClosingVisualState(runtime, scenario);
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            runtime.Systems.Create<PresentHudSystem>().Execute();
            runtime.Systems.Create<PresentDayReportSystem>().Execute();
            CleanupEvents(runtime);

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<StoreDayFeature>().Execute();
            runtime.Systems.Create<PresentHudSystem>().Execute();
            runtime.Systems.Create<PresentDayNightSystem>().Execute();
            runtime.Systems.Create<PresentDayReportSystem>().Execute();
            CleanupEvents(runtime);

            Require(scenario.Store.DayNumber == 2 &&
                    scenario.Store.isStorePreparing &&
                    !scenario.Player.isModalOpen,
                "The fade visual check did not enter Day 2 preparation.");
            Debug.Log(
                "[Hardware Store] Next-day fade visual check prepared. The HUD now holds black " +
                "for 0.2 seconds and fades back to Day 2 over 0.8 seconds.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Consultation Visual Check")]
        public static void PrepareSupplyChainVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
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
            OpenStoreForSmoke(runtime, scenario);
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

        [MenuItem("Tools/Hardware Store/Prepare Customer Queue Visual Check")]
        public static void PrepareCustomerQueueVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            CustomerVisit[] visits = PrepareThreeCustomerQueue(runtime, scenario);
            runtime.Systems.Create<PresentHudSystem>().Execute();
            Selection.activeGameObject = visits[0].ActorView.gameObject;

            Debug.Log(
                "[Hardware Store] Customer queue visual check prepared: three parked vehicles, " +
                "one customer at the counter and two customers in the visible FIFO queue.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Dissatisfied Customer Visual Check")]
        public static void PrepareDissatisfiedCustomerVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            visit.Entity.ReplaceCustomerPatienceRemaining(
                runtime.StaticData.CustomerFlow.PatienceWarningThreshold + 1f);
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(2f),
                    runtime.StaticData,
                    new GameEventFactory())
                .Execute();
            runtime.Systems.Create<PresentCustomerDissatisfactionSystem>().Execute();
            runtime.Systems.Create<PresentCustomerPatienceEventsSystem>().Execute();
            RequireCustomerDissatisfactionVisual(
                visit.Actor,
                expectedDissatisfied: true,
                "The dissatisfied-customer visual check did not activate its prefab mood.");

            Selection.activeGameObject = visit.ActorView.gameObject;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.LookAt(
                    visit.Actor.Transform.position + Vector3.up * 1.15f,
                    Quaternion.Euler(8f, 180f, 0f),
                    2.2f,
                    false,
                    true);
                sceneView.Repaint();
            }

            Debug.Log(
                "[Hardware Store] Dissatisfied customer visual check prepared: the selected " +
                "customer is framed close-up with red tint, raised arms and warning label.");
        }

        [MenuItem("Tools/Hardware Store/Run Customer Queue Smoke Test")]
        public static void RunCustomerQueueSmokeTest()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            CustomerVisit[] visits = PrepareThreeCustomerQueue(runtime, scenario);
            ValidateQueueSnapshot(
                runtime,
                total: 3,
                queued: 2,
                consulting: 1,
                loadingPipeline: 0,
                leaving: 0);

            scenario.Store.ReplaceCustomerCooldownRemaining(0f);
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(FindCustomerVisits(runtime.Game, scenario.Store.EntityId).Length == 3 &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        runtime.CustomerArrivalSchedule.GetDelay(
                            scenario.Store.CurrentDayMinute)),
                "A fourth customer spawned before any of the three parking reservations " +
                "was released, or the blocked arrival attempt did not schedule a breather.");

            CustomerVisit first = visits[0];
            OpenConsultation(runtime, scenario, first.Entity);
            GameEntity selectedOffer = SelectedConsultationOffer(
                runtime.Game,
                first.Entity);
            CustomerProjectOfferDefinition selectedDefinition = runtime.StaticData
                .GetProject(first.Entity.CustomerProjectType)
                .Offers[selectedOffer.OfferIndex];
            ConfirmConsultation(
                runtime,
                scenario,
                first.Entity,
                selectedDefinition);

            GameEntity secondActor = visits[1].Actor;
            GameEntity thirdActor = visits[2].Actor;
            Require(secondActor.isCustomerApproachingCounter &&
                    thirdActor.isCustomerApproachingCounter &&
                    QueueSpotIndex(runtime.Game, secondActor) == 0 &&
                    QueueSpotIndex(runtime.Game, thirdActor) == 1,
                "Releasing the counter did not advance both waiting customers in FIFO order.");
            ForceRouteEndpoint(runtime, secondActor);
            ForceRouteEndpoint(runtime, thirdActor);
            runtime.Systems.Create<CompleteCustomerApproachSystem>().Execute();
            runtime.Systems.Create<PromoteCustomerAtCounterSystem>().Execute();
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();

            Require(first.Entity.isCustomerVisitLoading &&
                    visits[1].Entity.isCustomerVisitConsulting &&
                    visits[2].Entity.isCustomerVisitQueued &&
                    visits[1].Entity.hasServingOrderCounterEntityId &&
                    QueueSpotIndex(runtime.Game, secondActor) == 0 &&
                    QueueSpotIndex(runtime.Game, thirdActor) == 1,
                "The second customer did not become the sole service head after FIFO advance.");
            ValidateQueueSnapshot(
                runtime,
                total: 3,
                queued: 1,
                consulting: 1,
                loadingPipeline: 1,
                leaving: 0);

            float cooldownBeforeParkingReleaseAttempt =
                scenario.Store.CustomerCooldownRemaining;
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(FindCustomerVisits(runtime.Game, scenario.Store.EntityId).Length == 3 &&
                    cooldownBeforeParkingReleaseAttempt > 0f &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        cooldownBeforeParkingReleaseAttempt),
                "Releasing a parking spot bypassed the scheduled customer breather.");

            scenario.Store.ReplaceCustomerCooldownRemaining(0f);
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            GameEntity[] visitsAfterParkingRelease = FindCustomerVisits(
                runtime.Game,
                scenario.Store.EntityId);
            GameEntity fourth = visitsAfterParkingRelease.Single(visit =>
                visit.CustomerArrivalSequence == 3);
            Require(visitsAfterParkingRelease.Length == 4 &&
                    fourth.isCustomerVisitArriving &&
                    !first.Entity.hasReservedCustomerParkingSpotEntityId &&
                    visitsAfterParkingRelease.Count(visit =>
                        visit.hasReservedCustomerParkingSpotEntityId) == 3 &&
                    visitsAfterParkingRelease
                        .Where(visit => visit.hasReservedCustomerParkingSpotEntityId)
                        .Select(visit => visit.ReservedCustomerParkingSpotEntityId)
                        .Distinct().Count() == 3 &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        runtime.CustomerArrivalSchedule.GetDelay(
                            scenario.Store.CurrentDayMinute)),
                "A fourth customer did not reserve the parking spot released by the first " +
                "loading-bay transition or restart the arrival schedule.");
            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            ForceRouteEndpoint(runtime, fourth);
            runtime.Systems.Create<CompleteCustomerVehicleArrivalSystem>().Execute();
            GameEntity fourthActor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(fourth.EntityId);
            Require(fourthActor != null && !fourthActor.hasView,
                "The fourth parked vehicle did not create one unbound customer actor.");
            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            Require(fourth.isCustomerVisitQueued && fourthActor != null &&
                    fourthActor.hasTransform && fourthActor.hasRigidbody &&
                    QueueSpotIndex(runtime.Game, fourthActor) == 2 &&
                    !fourth.hasReservedCustomerTrafficLaneEntityId,
                "The fourth customer did not bind its actor, join the FIFO tail or release " +
                "the traffic lane.");

            CustomerVisit second = visits[1];
            OpenConsultation(runtime, scenario, second.Entity);
            GameEntity secondSelectedOffer = SelectedConsultationOffer(
                runtime.Game,
                second.Entity);
            CustomerProjectOfferDefinition secondDefinition = runtime.StaticData
                .GetProject(second.Entity.CustomerProjectType)
                .Offers[secondSelectedOffer.OfferIndex];
            GameEntity[] secondOrderLines = ConfirmConsultation(
                runtime,
                scenario,
                second.Entity,
                secondDefinition,
                advanceToLoadingBay: false);
            BeginCustomerReturnSystem beginSecondCustomerReturn =
                runtime.Systems.Create<BeginCustomerReturnSystem>();
            beginSecondCustomerReturn.Execute();
            beginSecondCustomerReturn.Execute();
            runtime.Systems.Create<AdvanceCustomerQueueSystem>().Execute();
            ForceRouteEndpoint(runtime, secondActor);
            runtime.Systems.Create<CompleteCustomerReturnSystem>().Execute();
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            runtime.Systems.Create<ReserveCustomerLoadingBaySystem>().Execute();
            runtime.Systems.Create<MoveCustomerVehicleToLoadingBaySystem>().Execute();
            Require(second.Entity.isCustomerVisitWaitingForLoadingBay &&
                    !second.Entity.hasReservedCustomerLoadingBayEntityId &&
                    second.Entity.hasReservedCustomerParkingSpotEntityId &&
                    !second.Entity.hasReservedCustomerTrafficLaneEntityId &&
                    !second.Entity.isInteractable &&
                    !second.Entity.hasRoute &&
                    runtime.Game.GetEntityWithReservedCustomerLoadingBayEntityId(
                        runtime.Game.GetEntityWithCustomerLoadingBayStoreEntityId(
                            scenario.Store.EntityId).EntityId) == first.Entity,
                "Second accepted customer bypassed the first vehicle occupying the loading bay.");

            GameEntity[] firstOrderLines = GetOrderLines(runtime.Game, first.Entity);
            GameEntity[] syntheticLoadedProducts = CreateSyntheticLoadedProducts(
                runtime,
                firstOrderLines);
            foreach (GameEntity line in firstOrderLines)
                line.ReplaceLoadedProductCount(line.RequiredProductCount);
            first.Entity.isCustomerVisitLoading = false;
            first.Entity.isCustomerVisitCompleted = true;
            first.Entity.isOrderRewarded = true;
            DepartAndCleanupCustomer(
                runtime,
                scenario,
                first,
                firstOrderLines,
                syntheticLoadedProducts);

            runtime.Systems.Create<ReserveCustomerLoadingBaySystem>().Execute();
            Require(second.Entity.hasReservedCustomerLoadingBayEntityId,
                "The oldest waiting customer did not claim the bay released by the first " +
                "departure.");
            runtime.Systems.Create<MoveCustomerVehicleToLoadingBaySystem>().Execute();
            Require(second.Entity.isCustomerVisitMovingToLoadingBay &&
                    second.Entity.hasReservedCustomerTrafficLaneEntityId,
                "The oldest waiting vehicle did not acquire the shared lane after bay release.");
            ValidateReverseLoadingMovement(runtime, second.Entity);
            ForceRouteEndpoint(runtime, second.Entity);
            runtime.Systems.Create<CompleteCustomerLoadingBayArrivalSystem>().Execute();
            Require(second.Entity.isCustomerVisitLoading &&
                    !second.Entity.hasReservedCustomerParkingSpotEntityId &&
                    second.Entity.isInteractable,
                "Second vehicle did not release parking when it reached the loading bay.");
            ValidateReverseLoadingPose(second.Entity);

            GameEntity[] visitsAfterFourthSpawn = FindCustomerVisits(
                runtime.Game,
                scenario.Store.EntityId);
            Require(visitsAfterFourthSpawn.Length == 3 &&
                    visitsAfterFourthSpawn.Count(visit =>
                        visit.CustomerArrivalSequence == 3 &&
                        visit.isCustomerVisitQueued) == 1,
                "The fourth FIFO customer was not preserved while the loading bay advanced.");
            Require(secondOrderLines.All(line => !line.isDestructed),
                "First departure damaged the second accepted order graph.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();

            int[] activeIds = FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                .Select(visit => visit.EntityId)
                .ToArray();
            new TickStoreDayClockSystem(
                runtime.Game,
                runtime.StaticData,
                new FixedTimeService(
                    runtime.StaticData.StoreDay.DayDurationSeconds)).Execute();
            runtime.Systems.Create<ReachStoreClosingTimeSystem>().Execute();
            CleanupEvents(runtime);
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .Select(visit => visit.EntityId)
                        .SequenceEqual(activeIds),
                "Closing time changed the draining queue or spawned a fourth customer.");
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(!scenario.Store.isDayReportOpen &&
                    !scenario.Player.isModalOpen,
                "Day report opened before all three queued/loading visits drained.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            CleanupEvents(runtime);

            Debug.Log(
                "[Hardware Store] Customer queue smoke passed: three unique parking " +
                "reservations, FIFO service advance, loading-bay transition, aggregate HUD, " +
                "time schedule and closing drain guard.");
        }

        [MenuItem("Tools/Hardware Store/Run Customer Patience Smoke Test")]
        public static void RunCustomerPatienceSmokeTest()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            CustomerVisit[] visits = PrepareThreeCustomerQueue(runtime, scenario);

            ValidateCustomerPatienceTickAndModal(runtime, scenario, visits);
            AbandonQueuedCustomerAndValidateDeparture(
                runtime,
                scenario,
                visits[1],
                visits[2]);
            CustomerVisit fourth = SpawnCustomerToQueue(
                runtime,
                scenario,
                promoteAtCounter: false);
            AcceptModalCustomerAndValidateNoPatience(
                runtime,
                scenario,
                visits[0]);
            AbandonConsultingCustomerAndPromoteFollower(
                runtime,
                scenario,
                visits[2],
                fourth);
            OpenConsultation(runtime, scenario, fourth.Entity);
            AcceptModalCustomerAndValidateNoPatience(
                runtime,
                scenario,
                fourth);
            ValidatePatienceDayReportAndStartNextDay(runtime, scenario);

            Debug.Log(
                "[Hardware Store] Customer patience smoke passed: queued/consulting timers, " +
                "same-frame modal protection, one-shot warning, FIFO abandonment, actor and " +
                "parking departure, lane wait, accepted-order cutoff, report and Day 2 reset.");
        }

        private static void ValidateCustomerPatienceTickAndModal(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit[] visits)
        {
            GameEntity consulting = visits[0].Entity;
            GameEntity queued = visits[1].Entity;
            float initialPatience = runtime.StaticData.CustomerFlow.DefaultPatienceDuration;
            var events = new GameEventFactory();

            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(10f),
                    runtime.StaticData,
                    events)
                .Execute();
            Require(Mathf.Approximately(consulting.CustomerPatienceRemaining, 110f) &&
                    Mathf.Approximately(queued.CustomerPatienceRemaining, 110f) &&
                    Mathf.Approximately(
                        visits[2].Entity.CustomerPatienceRemaining,
                        110f),
                "Queued and consulting customer patience did not tick together.");

            OpenConsultation(runtime, scenario, consulting);
            float consultingBeforePause = consulting.CustomerPatienceRemaining;
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(5f),
                    runtime.StaticData,
                    events)
                .Execute();
            Require(Mathf.Approximately(
                        consulting.CustomerPatienceRemaining,
                        consultingBeforePause) &&
                    Mathf.Approximately(queued.CustomerPatienceRemaining, 105f),
                "The active consultation modal did not pause only its related customer.");
            CancelConsultation(runtime, scenario, consulting);

            consulting.ReplaceCustomerPatienceRemaining(0.25f);
            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            RequestInteraction(scenario.Player, scenario.OrderCounter);
            runtime.Systems.Create<ConsultationFeature>().Execute();
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(1f),
                    runtime.StaticData,
                    events)
                .Execute();
            runtime.Systems.Create<BeginCustomerAbandonmentSystem>().Execute();
            Require(scenario.Player.isModalOpen &&
                    scenario.Player.ConsultationVisitEntityId == consulting.EntityId &&
                    consulting.isCustomerVisitConsulting &&
                    !consulting.isCustomerVisitAbandoning &&
                    Mathf.Approximately(
                        consulting.CustomerPatienceRemaining,
                        0.25f),
                "Opening consultation in the same frame did not win over patience expiry.");
            consulting.isCustomerPatienceWarningIssued = true;
            runtime.Systems.Create<PresentCustomerDissatisfactionSystem>().Execute();
            RequireCustomerDissatisfactionVisual(
                visits[0].Actor,
                expectedDissatisfied: true,
                "A warned consulting customer did not expose dissatisfaction before " +
                "acceptance.");

            queued.ReplaceCustomerPatienceRemaining(
                runtime.StaticData.CustomerFlow.PatienceWarningThreshold + 1f);
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(2f),
                    runtime.StaticData,
                    events)
                .Execute();
            GameEntity[] warningEvents = runtime.Game
                .GetGroup(GameMatcher.AllOf(
                    GameMatcher.CustomerPatienceWarningEvent,
                    GameMatcher.CustomerEventVisitEntityId))
                .GetEntities();
            Require(queued.isCustomerPatienceWarningIssued &&
                    Mathf.Approximately(queued.CustomerPatienceRemaining, 29f) &&
                    warningEvents.Length == 1 &&
                    warningEvents[0].CustomerEventVisitEntityId == queued.EntityId,
                "Crossing the patience threshold did not emit one warning event.");
            runtime.Systems.Create<PresentCustomerDissatisfactionSystem>().Execute();
            RequireCustomerDissatisfactionVisual(
                visits[1].Actor,
                expectedDissatisfied: true,
                "A patience warning did not immediately tint the customer, raise both arms " +
                "and show its localized world label.");
            var warningCapture = new CaptureNotificationService();
            new PresentCustomerPatienceEventsSystem(runtime.Game, warningCapture).Execute();
            Require(warningCapture.Message?.Key ==
                        LocalizationKey.NotificationCustomerPatienceLow &&
                    runtime.Localization.Resolve(warningCapture.Message) ==
                    "Клиент недоволен — он может уйти" &&
                    runtime.Game.GetGroup(GameMatcher.CustomerPatienceWarningEvent).count == 0,
                "The patience warning bridge did not present and consume its exact event.");
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(1f),
                    runtime.StaticData,
                    events)
                .Execute();
            Require(runtime.Game.GetGroup(GameMatcher.CustomerPatienceWarningEvent).count == 0,
                "A warned customer emitted the same patience warning twice.");

            queued.ReplaceCustomerPatienceRemaining(1f);
            visits[2].Entity.ReplaceCustomerPatienceRemaining(initialPatience);
            CleanupEvents(runtime);
        }

        private static void AbandonQueuedCustomerAndValidateDeparture(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit abandoned,
            CustomerVisit follower)
        {
            GameEntity visit = abandoned.Entity;
            GameEntity actor = abandoned.Actor;
            GameEntity followerActor = follower.Actor;
            int visitId = visit.EntityId;
            int actorId = actor.EntityId;
            int parkingSpotId = visit.ReservedCustomerParkingSpotEntityId;
            GameEntity abandonedQueueSpot = runtime.Game.GetEntityWithEntityId(
                actor.ReservedCustomerQueueSpotEntityId);
            Require(abandonedQueueSpot != null &&
                    abandonedQueueSpot.isCustomerQueueSpot &&
                    abandonedQueueSpot.hasCustomerQueueAbandonRoute &&
                    actor.hasCustomerReturnRoute,
                "The queued abandonment smoke requires authored queue-exit and return routes.");
            Pose actorStart = new(actor.Rigidbody.position, actor.Rigidbody.rotation);
            Pose[] queueAbandonRoute =
                (Pose[])abandonedQueueSpot.CustomerQueueAbandonRoute.Clone();
            Pose[] customerReturnRoute = (Pose[])actor.CustomerReturnRoute.Clone();
            var expectedActorRoute = new Pose[
                1 + queueAbandonRoute.Length + customerReturnRoute.Length - 2];
            expectedActorRoute[0] = actorStart;
            Array.Copy(
                queueAbandonRoute,
                0,
                expectedActorRoute,
                1,
                queueAbandonRoute.Length);
            Array.Copy(
                customerReturnRoute,
                2,
                expectedActorRoute,
                1 + queueAbandonRoute.Length,
                customerReturnRoute.Length - 2);
            int queueSpotBefore = QueueSpotIndex(runtime.Game, followerActor);
            GameEntity[] offers = GetConsultationOffers(runtime.Game, visit);
            GameEntity[] offerLines = offers
                .SelectMany(offer => GetConsultationOfferLines(runtime.Game, offer))
                .ToArray();

            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(1f),
                    runtime.StaticData,
                    new GameEventFactory())
                .Execute();
            runtime.Systems.Create<BeginCustomerAbandonmentSystem>().Execute();
            runtime.Systems.Create<AdvanceCustomerQueueSystem>().Execute();
            runtime.Systems.Create<PresentCustomerDissatisfactionSystem>().Execute();
            Require(visit.isCustomerVisitAbandoning &&
                    !visit.isCustomerVisitQueued &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.isOrder &&
                    !visit.hasCustomerPatienceRemaining &&
                    !visit.hasServingOrderCounterEntityId &&
                    !visit.hasReservedCustomerLoadingBayEntityId &&
                    actor.isCustomerAbandonReturningToVehicle &&
                    actor.hasRoute &&
                    actor.Route.Length == expectedActorRoute.Length &&
                    actor.Route.Select((pose, index) =>
                            PoseMatches(pose, expectedActorRoute[index]))
                        .All(matches => matches) &&
                    actor.RouteWaypointIndex == 1 &&
                    !actor.hasReservedCustomerQueueSpotEntityId &&
                    follower.Entity.isCustomerVisitQueued &&
                    followerActor.isCustomerApproachingCounter &&
                    queueSpotBefore == 2 &&
                    QueueSpotIndex(runtime.Game, followerActor) == 1 &&
                    scenario.Store.DayLostCustomerCount == 1 &&
                    offers.All(offer => offer.isDestructed &&
                                                !offer.hasConsultationOfferVisitEntityId) &&
                    offerLines.All(line => line.isDestructed &&
                                              !line.hasConsultationOfferEntityId),
                "An expired middle customer did not leave without order/bay state or compact " +
                "the FIFO queue exactly once.");
            RequireCustomerDissatisfactionVisual(
                actor,
                expectedDissatisfied: true,
                "An abandoning customer cleared its visible dissatisfaction before returning " +
                "to the vehicle.");

            var abandonmentCapture = new CaptureNotificationService();
            new PresentCustomerPatienceEventsSystem(
                    runtime.Game,
                    abandonmentCapture)
                .Execute();
            Require(abandonmentCapture.Message?.Key ==
                        LocalizationKey.NotificationCustomerLeftImpatient &&
                    runtime.Game.GetGroup(GameMatcher.CustomerAbandonedEvent).count == 0,
                "The abandonment bridge did not present and consume the leave event.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerAbandonReturnSystem>().Execute();
            Require(visit.isCustomerVisitWaitingForAbandonDeparture &&
                    actor.isDestructed &&
                    !actor.hasCustomerActorVisitEntityId &&
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(visitId) == null,
                "The abandoned customer actor did not return to its parked vehicle.");
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(actorId) == null &&
                    !abandoned.ActorView.HasEntity,
                "The abandoned customer actor survived the Destructed cleanup pipeline.");

            GameEntity trafficLane = runtime.Game
                .GetEntityWithCustomerTrafficLaneStoreEntityId(scenario.Store.EntityId);
            GameEntity laneBlocker = follower.Entity;
            laneBlocker.AddReservedCustomerTrafficLaneEntityId(trafficLane.EntityId);
            runtime.Systems.Create<BeginCustomerAbandonDepartureSystem>().Execute();
            Require(visit.isCustomerVisitWaitingForAbandonDeparture &&
                    !visit.isCustomerVisitAbandonDeparting &&
                    !visit.hasRoute &&
                    !visit.hasReservedCustomerTrafficLaneEntityId,
                "An impatient vehicle entered the occupied shared traffic lane.");
            laneBlocker.RemoveReservedCustomerTrafficLaneEntityId();

            runtime.Systems.Create<BeginCustomerAbandonDepartureSystem>().Execute();
            Pose[] expectedDeparture = runtime.Game.GetEntityWithEntityId(parkingSpotId)
                .CustomerVehicleParkingDepartureRoute;
            Require(visit.isCustomerVisitAbandonDeparting &&
                    visit.hasReservedCustomerTrafficLaneEntityId &&
                    visit.hasRoute &&
                    visit.Route.Length == 5 &&
                    visit.Route.Select((pose, index) =>
                            PoseMatches(pose, expectedDeparture[index]))
                        .All(matches => matches),
                "The impatient vehicle did not acquire the lane with its authored parking " +
                "departure route.");
            ForceRouteEndpoint(runtime, visit);
            runtime.Systems.Create<CompleteCustomerAbandonDepartureSystem>().Execute();
            Require(visit.isDestructed &&
                    !visit.hasCustomerVisitStoreEntityId &&
                    !visit.hasReservedCustomerParkingSpotEntityId &&
                    !visit.hasReservedCustomerTrafficLaneEntityId &&
                    runtime.Game.GetEntityWithReservedCustomerParkingSpotEntityId(
                        parkingSpotId) == null &&
                    runtime.Game.GetEntityWithReservedCustomerTrafficLaneEntityId(
                        trafficLane.EntityId) == null,
                "The impatient vehicle did not release parking and lane ownership on exit.");
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    !abandoned.View.HasEntity,
                "The impatient vehicle survived its full Destructed cleanup pipeline.");

            ProcurementPurchaseEvaluation first = runtime.ProcurementSolvency.EvaluatePurchase(
                scenario.ProcurementTerminal.EntityId,
                ProductTypeId.CementBag);
            ProcurementPurchaseEvaluation second = runtime.ProcurementSolvency.EvaluatePurchase(
                scenario.ProcurementTerminal.EntityId,
                ProductTypeId.CementBag);
            Require(first.Availability == second.Availability &&
                    first.DemandKind == second.DemandKind &&
                    first.ProjectType == second.ProjectType &&
                    first.DemandVisitEntityId == second.DemandVisitEntityId &&
                    first.DeliveryProductCount == second.DeliveryProductCount &&
                    first.DeliveryCost == second.DeliveryCost &&
                    first.MoneyAfterPurchase == second.MoneyAfterPurchase,
                "Procurement solvency became non-deterministic after an abandoned arrival " +
                "created a sequence gap.");
        }

        private static void AcceptModalCustomerAndValidateNoPatience(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit accepted)
        {
            GameEntity visit = accepted.Entity;
            Require(scenario.Player.isModalOpen &&
                    scenario.Player.hasConsultationVisitEntityId &&
                    scenario.Player.ConsultationVisitEntityId == visit.EntityId &&
                    visit.isCustomerVisitConsulting &&
                    visit.hasCustomerPatienceRemaining,
                "The accepted-customer phase requires the protected open consultation.");
            if (visit.isCustomerPatienceWarningIssued)
            {
                RequireCustomerDissatisfactionVisual(
                    accepted.Actor,
                    expectedDissatisfied: true,
                    "The accepted-customer reset regression requires a visibly dissatisfied " +
                    "actor before confirmation.");
            }
            GameEntity selectedOffer = SelectedConsultationOffer(runtime.Game, visit);
            CustomerProjectOfferDefinition selectedDefinition = runtime.StaticData
                .GetProject(visit.CustomerProjectType)
                .Offers[selectedOffer.OfferIndex];
            GameEntity[] orderLines = ConfirmConsultation(
                runtime,
                scenario,
                visit,
                selectedDefinition);
            Require(visit.isOrder &&
                    visit.isCustomerVisitLoading &&
                    !visit.hasCustomerPatienceRemaining &&
                    !visit.isCustomerPatienceWarningIssued,
                "An accepted order retained patience or entered a non-loading lifecycle.");

            GameEntity[] loadedProducts = CreateSyntheticLoadedProducts(runtime, orderLines);
            foreach (GameEntity line in orderLines)
                line.ReplaceLoadedProductCount(line.RequiredProductCount);
            visit.isCustomerVisitLoading = false;
            visit.isCustomerVisitCompleted = true;
            visit.isOrderRewarded = true;
            DepartAndCleanupCustomer(
                runtime,
                scenario,
                accepted,
                orderLines,
                loadedProducts);
        }

        private static void AbandonConsultingCustomerAndPromoteFollower(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit abandoned,
            CustomerVisit follower)
        {
            GameEntity visit = abandoned.Entity;
            GameEntity actor = abandoned.Actor;
            GameEntity followerVisit = follower.Entity;
            GameEntity followerActor = follower.Actor;
            int visitId = visit.EntityId;
            int actorId = actor.EntityId;
            int parkingSpotId = visit.ReservedCustomerParkingSpotEntityId;
            Require(visit.isCustomerVisitQueued &&
                    !visit.isOrder &&
                    visit.hasCustomerPatienceRemaining &&
                    followerVisit.isCustomerVisitQueued &&
                    !followerVisit.isOrder &&
                    !scenario.Player.isModalOpen,
                "The consulting abandonment branch requires two unaccepted queued customers.");

            ForceRouteEndpoint(runtime, actor);
            ForceRouteEndpoint(runtime, followerActor);
            runtime.Systems.Create<CompleteCustomerApproachSystem>().Execute();
            runtime.Systems.Create<PromoteCustomerAtCounterSystem>().Execute();
            Require(visit.isCustomerVisitConsulting &&
                    visit.hasServingOrderCounterEntityId &&
                    followerVisit.isCustomerVisitQueued &&
                    QueueSpotIndex(runtime.Game, followerActor) == 1,
                "The oldest remaining customer did not become the consulting FIFO head.");
            OpenConsultation(runtime, scenario, visit);
            CancelConsultation(runtime, scenario, visit);
            Require(visit.isCustomerVisitConsulting &&
                    !scenario.Player.isModalOpen,
                "Cancelling the modal did not return the customer to exposed consulting " +
                "patience.");

            visit.ReplaceCustomerPatienceRemaining(1f);
            new TickCustomerPatienceSystem(
                    runtime.Game,
                    new FixedTimeService(1f),
                    runtime.StaticData,
                    new GameEventFactory())
                .Execute();
            runtime.Systems.Create<BeginCustomerAbandonmentSystem>().Execute();
            runtime.Systems.Create<AdvanceCustomerQueueSystem>().Execute();
            Require(visit.isCustomerVisitAbandoning &&
                    scenario.Store.DayLostCustomerCount == 2 &&
                    !visit.isOrder &&
                    !visit.hasServingOrderCounterEntityId &&
                    !visit.hasReservedCustomerLoadingBayEntityId &&
                    followerVisit.isCustomerVisitQueued &&
                    followerActor.isCustomerApproachingCounter &&
                    QueueSpotIndex(runtime.Game, followerActor) == 0,
                "The cancelled consulting customer did not release its counter and advance " +
                "the FIFO follower.");
            ForceRouteEndpoint(runtime, followerActor);
            runtime.Systems.Create<CompleteCustomerApproachSystem>().Execute();
            runtime.Systems.Create<PromoteCustomerAtCounterSystem>().Execute();
            Require(followerVisit.isCustomerVisitConsulting &&
                    followerVisit.hasServingOrderCounterEntityId &&
                    followerActor.isCustomerWaitingAtCounter,
                "The FIFO follower did not claim the counter after consulting abandonment.");
            var notificationCapture = new CaptureNotificationService();
            new PresentCustomerPatienceEventsSystem(
                    runtime.Game,
                    notificationCapture)
                .Execute();
            Require(notificationCapture.Message?.Key ==
                    LocalizationKey.NotificationCustomerLeftImpatient,
                "The last impatient customer did not emit its leave notification.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerAbandonReturnSystem>().Execute();
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(actorId) == null &&
                    visit.isCustomerVisitWaitingForAbandonDeparture,
                "The consulting impatient actor did not finish returning to the vehicle.");

            runtime.Systems.Create<BeginCustomerAbandonDepartureSystem>().Execute();
            Require(visit.isCustomerVisitAbandonDeparting &&
                    visit.Route.Length == 5,
                "The consulting impatient vehicle did not start its parking departure.");
            ForceRouteEndpoint(runtime, visit);
            runtime.Systems.Create<CompleteCustomerAbandonDepartureSystem>().Execute();
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(visitId) == null &&
                    runtime.Game.GetEntityWithReservedCustomerParkingSpotEntityId(
                        parkingSpotId) == null &&
                    !abandoned.ActorView.HasEntity &&
                    !abandoned.View.HasEntity &&
                    FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .SequenceEqual(new[] { followerVisit }),
                "The consulting impatient customer retained an entity, view or parking " +
                "relation, or damaged its promoted follower.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
        }

        private static void ValidatePatienceDayReportAndStartNextDay(
            Runtime runtime,
            Scenario scenario)
        {
            Require(scenario.Store.isStoreOpen &&
                    scenario.Store.DayLostCustomerCount == 2 &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
                "The patience report requires two lost customers and an otherwise empty store.");
            new TickStoreDayClockSystem(
                    runtime.Game,
                    runtime.StaticData,
                    new FixedTimeService(runtime.StaticData.StoreDay.DayDurationSeconds))
                .Execute();
            runtime.Systems.Create<ReachStoreClosingTimeSystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.hasCustomerCooldownRemaining,
                "The empty patience scenario did not enter closing at 20:00.");

            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            CleanupEvents(runtime);
            var capture = new CaptureHudService();
            new PresentHudSystem(runtime.Game, runtime.StaticData, capture).Execute();
            new PresentDayReportSystem(runtime.Game, capture).Execute();
            Require(capture.DayReport.HasValue &&
                    capture.DayReport.Value.DayNumber == 1 &&
                    capture.DayReport.Value.LostCustomerCount == 2 &&
                    capture.DayReport.Value.CompletedOrderCount == 0 &&
                    capture.DayReport.Value.ClosingBalance == scenario.Store.Money,
                "The Day 1 report did not include the exact lost-customer total.");

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<StoreDayFeature>().Execute();
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();
            Require(scenario.Store.DayNumber == 2 &&
                    scenario.Store.isStorePreparing &&
                    !scenario.Store.isDayReportOpen &&
                    scenario.Store.DayLostCustomerCount == 0 &&
                    scenario.Store.DayCompletedOrderCount == 0 &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasDayReportStoreEntityId,
                "Day 2 did not reset the daily lost-customer total and report modal.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
        }

        [MenuItem("Tools/Hardware Store/Prepare Procurement Visual Check")]
        public static void PrepareProcurementVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
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

        [MenuItem("Tools/Hardware Store/Prepare Warehouse Worker Visual Check")]
        public static void PrepareWarehouseWorkerVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            UnlockWarehouseWorkerHiring(runtime, scenario);
            GameEntity worker = HireWarehouseWorker(runtime, scenario);
            DeliveryArrival arrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                ProductTypeId.CementBag);

            runtime.Systems.Create<WarehouseWorkerFeature>().Execute();
            GameEntity task = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                GameMatcher.WarehouseTask,
                GameMatcher.WarehouseTaskProductEntityId,
                GameMatcher.AssignedWorkerEntityId)), "assigned warehouse task");
            Selection.activeGameObject = worker.View.gameObject;
            Debug.Log(
                $"[Hardware Store] Warehouse worker visual check prepared: worker " +
                $"{worker.EntityId}, task {task.EntityId}, delivery " +
                $"{arrival.Delivery.EntityId}. The worker is walking to the inbound bay.");
        }

        [MenuItem("Tools/Hardware Store/Prepare Warehouse Worker Customer Loading Visual Check")]
        public static void PrepareWarehouseWorkerCustomerLoadingVisualCheck()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            UnlockWarehouseWorkerHiring(runtime, scenario);
            GameEntity worker = HireWarehouseWorker(runtime, scenario);
            DeliveryArrival stock = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                ProductTypeId.CementBag);
            StoreDeliveryWithWarehouseWorker(runtime, scenario, worker, stock);
            CleanupCompletedDelivery(runtime, scenario, stock);

            CustomerVisit visit = SpawnAndParkCustomer(runtime, scenario);
            OpenConsultation(runtime, scenario, visit.Entity);
            ConfirmConsultation(
                runtime,
                scenario,
                visit.Entity,
                runtime.StaticData.GetProject(
                    CustomerProjectTypeId.CementFoundation).Offers[1]);
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowWorkerCarriedProductSystem>().Execute();
            runtime.Systems.Create<PresentHudSystem>().Execute();

            GameEntity task = FindLiveCustomerLoadingTasks(runtime.Game).Single();
            Require(task.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToCustomerLoading &&
                    worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.MovingToCustomerLoading,
                "Customer-loading visual check did not reach the carrying step.");
            Selection.activeGameObject = worker.View.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log(
                $"[Hardware Store] Customer-loading worker visual check prepared: " +
                $"worker {worker.EntityId} carries product " +
                $"{task.WarehouseTaskProductEntityId} from storage to visit " +
                $"{visit.Entity.EntityId}.");
        }

        [MenuItem("Tools/Hardware Store/Run Warehouse Worker Smoke Test")]
        public static void RunWarehouseWorkerSmokeTest()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            WarehouseWorkerConfig config = runtime.StaticData.WarehouseWorker;
            OpenStoreForSmoke(runtime, scenario);
            UnlockWarehouseWorkerHiring(runtime, scenario);
            int moneyBeforeHire = scenario.Store.Money;
            GameEntity worker = HireWarehouseWorker(runtime, scenario);
            int workerEntityId = worker.EntityId;
            int expectedMoneyAfterHire = moneyBeforeHire - config.HirePrice;
            Require(scenario.Store.Money == expectedMoneyAfterHire &&
                    scenario.Store.DayUpgradeExpenses == config.HirePrice,
                "Warehouse worker hire did not debit its one-time cost exactly once.");

            DeliveryArrival arrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                ProductTypeId.CementBag);
            int expectedMoneyAfterDelivery = expectedMoneyAfterHire -
                                             runtime.StaticData.GetDelivery(
                                                 ProductTypeId.CementBag).TotalCost;
            Require(scenario.Store.Money == expectedMoneyAfterDelivery,
                "Warehouse worker smoke delivery was not charged exactly once.");

            ValidateWarehouseWorkerStorageFull(runtime, scenario, worker);
            ValidateWarehouseWorkerTimeoutRecovery(
                runtime,
                scenario,
                worker,
                arrival);
            StoreDeliveryWithWarehouseWorker(
                runtime,
                scenario,
                worker,
                arrival);
            CleanupCompletedDelivery(runtime, scenario, arrival);
            ValidateWarehouseWorkerDayTwoWage(
                runtime,
                scenario,
                worker,
                expectedMoneyAfterDelivery);

            Require(worker.EntityId == workerEntityId &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithWarehouseWorkerStoreEntityId(
                            scenario.Store.EntityId),
                        worker) &&
                    CountStockProducts(
                        runtime.Game,
                        scenario.StorageZone.EntityId,
                        ProductTypeId.CementBag) == arrival.Products.Length,
                "Worker smoke lost its employee identity or the automatically stocked batch.");
            Debug.Log(
                $"[Hardware Store] Warehouse worker smoke passed: unlock 4, single hire, " +
                $"blocked-reservation intake, full-storage wait, exact-slot timeout recovery, " +
                $"active-task report guard, blocked diagnostic report release, automatic " +
                $"three-product stocking and Day 2 wage {config.DailyWage:N0} ₽.");
        }

        [MenuItem("Tools/Hardware Store/Run Warehouse Worker Customer Loading Smoke Test")]
        public static void RunWarehouseWorkerCustomerLoadingSmokeTest()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            OpenStoreForSmoke(runtime, scenario);
            UnlockWarehouseWorkerHiring(runtime, scenario);
            GameEntity worker = HireWarehouseWorker(runtime, scenario);

            ValidateWarehouseWorkerCustomerLoadingFlow(
                runtime,
                scenario,
                worker);

            Debug.Log(
                "[Hardware Store] Warehouse-worker customer-loading smoke passed: bay A/B " +
                "ownership, outbound priority, no preemption, player/worker cooperative " +
                "slots, StorageFull and closing work, recovery branches, exact reward and " +
                "clean departure.");
        }

        [MenuItem("Tools/Hardware Store/Run Gameplay Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int initialMoney = scenario.Store.Money;
            ValidateEditorMoneyOverride(runtime, scenario);
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
            Require(runtime.StaticData.CustomerFlow.ParkingCapacity == 3 &&
                    Mathf.Approximately(
                        runtime.StaticData.CustomerFlow.FirstArrivalDelay,
                        10f) &&
                    Mathf.Approximately(
                        runtime.StaticData.CustomerFlow.DefaultPatienceDuration,
                        120f) &&
                    Mathf.Approximately(
                        runtime.StaticData.CustomerFlow.PatienceWarningThreshold,
                        30f),
                "The customer-flow smoke requires the frozen 3/10/120/30 pacing and " +
                "patience values.");
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
            Require(runtime.StaticData.WarehouseWorker.RequiredCompletedOrderCount == 4 &&
                    runtime.StaticData.WarehouseWorker.HirePrice == 400 &&
                    runtime.StaticData.WarehouseWorker.DailyWage == 100 &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.MovementSpeed,
                        2.8f) &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.Acceleration,
                        12f) &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.AngularSpeed,
                        720f) &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.StoppingDistance,
                        0.2f) &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.NavigationSampleRadius,
                        2f) &&
                    Mathf.Approximately(
                        runtime.StaticData.WarehouseWorker.TaskTimeout,
                        20f),
                "The worker smoke requires the frozen unlock, economy and navigation values.");
            Require(runtime.StaticData.StoreDay.StartMinute == 8 * 60 &&
                    runtime.StaticData.StoreDay.ClosingMinute == 20 * 60 &&
                    Mathf.Approximately(
                        runtime.StaticData.StoreDay.DayDurationSeconds,
                        480f),
                "The store day smoke requires 08:00-20:00 mapped to 480 real seconds.");
            ValidateDayClockLayoutAt1280x720(runtime);
            ValidateNewDayFadeContract();
            ValidateTrolleyLockedAtProgress(runtime, scenario, expectedCompletedOrders: 0);
            ValidatePreparingPresentationAndFrozenClock(runtime, scenario);
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
            OpenStoreForSmoke(runtime, scenario);
            ValidateCooldownPresentation(runtime, scenario);

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
            StoreCompleteDelivery(
                runtime,
                scenario,
                secondVisit.Entity,
                secondArrival,
                validateContextAwareStorageIntake: true);
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
            ReachClosingTimeWithActiveCustomer(
                runtime,
                scenario,
                thirdVisit.Entity);
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
            ValidateClosingPreventsCustomerSpawn(runtime, scenario);

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

            ValidateDayReportAndStartNextDay(
                runtime,
                scenario,
                trolley,
                expectedFinalMoney,
                expectedFinalStock,
                expectedRevenue: cementReward + boardReward + mixedReward,
                expectedProcurementExpenses:
                    cementDelivery.TotalCost * 2 + boardDelivery.TotalCost * 2,
                expectedUpgradeExpenses:
                    runtime.StaticData.PlatformTrolley.PurchasePrice,
                expectedCompletedOrders: 3);

            Debug.Log(
                $"[Hardware Store] Gameplay smoke passed: consultation and procurement modals, " +
                $"free forecast prebuy, safety-reserve rejection, arrow wrap, Enter/Esc and " +
                $"modal input capture, direct order activation, min/max offers, " +
                $"walking customer NPC lifecycle, two single-SKU cycles and one C2+B1 cycle, " +
                $"active-order replenishment and cross-SKU purchase, trolley-debit reserve, quota " +
                $"and wrong-SKU rejection, " +
                $"exact-slot product recovery, blocked/safe product drops and both physics flows, " +
                $"context-aware whole-storage intake, foreign-storage isolation, product focus, " +
                $"foreign-trigger/solid-wall occlusion and bounded candidate saturation, " +
                $"two-order trolley unlock, no-customer F attach/detach, E/F cargo routing, " +
                $"single purchase, three-slot C2+B1 trolley flow, " +
                $"collision-safe trolley stop/resume, " +
                $"08:00-20:00 day, mandatory report, fade and Day 2 persistence, " +
                $"stock {expectedFinalStock}, balance " +
                $"{expectedFinalMoney - cementDelivery.TotalCost:N0} ₽.");
        }

        private static void ValidateEditorMoneyOverride(Runtime runtime,
            Scenario scenario)
        {
            const int testGrant = 500;
            GameEntity store = scenario.Store;
            int originalMoney = store.Money;
            int originalOpeningBalance = store.DayOpeningBalance;
            int originalRevenue = store.DayRevenue;
            int originalProcurementExpenses = store.DayProcurementExpenses;
            int originalUpgradeExpenses = store.DayUpgradeExpenses;
            int originalPayrollExpenses = store.DayPayrollExpenses;

            store.money.Value = checked(originalMoney + testGrant);
            runtime.Systems.Create<ReconcileEditorMoneyOverrideSystem>().Execute();

            Require(store.Money == originalMoney + testGrant &&
                    store.DayOpeningBalance == originalOpeningBalance + testGrant &&
                    store.DayRevenue == originalRevenue &&
                    store.DayProcurementExpenses == originalProcurementExpenses &&
                    store.DayUpgradeExpenses == originalUpgradeExpenses &&
                    store.DayPayrollExpenses == originalPayrollExpenses,
                "An Editor Money component override was not reconciled through the opening " +
                "balance without polluting real day income or expenses.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();

            store.money.Value = originalMoney;
            store.dayOpeningBalance.Value = originalOpeningBalance;
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
        }

        private static void UnlockWarehouseWorkerHiring(
            Runtime runtime,
            Scenario scenario)
        {
            WarehouseWorkerConfig config = runtime.StaticData.WarehouseWorker;
            Require(!scenario.Store.isWarehouseWorkerHiringUnlocked &&
                    runtime.Game.GetEntityWithWarehouseWorkerStoreEntityId(
                        scenario.Store.EntityId) == null,
                "Warehouse worker unlock smoke requires a fresh employee state.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            scenario.Store.ReplaceCompletedOrderCount(
                config.RequiredCompletedOrderCount - 1);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptWarehouseWorkerLocked,
                            config.RequiredCompletedOrderCount,
                            config.RequiredCompletedOrderCount - 1)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Worker hire unlocked before its exact completed-order threshold.");
            runtime.Systems.Create<UnlockWarehouseWorkerHiringSystem>().Execute();
            Require(!scenario.Store.isWarehouseWorkerHiringUnlocked &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "Worker hire unlocked or notified one order too early.");

            scenario.Store.ReplaceCompletedOrderCount(
                config.RequiredCompletedOrderCount);
            runtime.Systems.Create<UnlockWarehouseWorkerHiringSystem>().Execute();
            Require(scenario.Store.isWarehouseWorkerHiringUnlocked,
                "Worker hire did not unlock at its exact completed-order threshold.");
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationWarehouseWorkerUnlocked);
            runtime.Systems.Create<UnlockWarehouseWorkerHiringSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 1,
                "Worker hire unlock was not idempotent.");
            CleanupEvents(runtime);

            ExecuteInteractionPrompts(runtime);
            EconomyDebitEvaluation unsafeHire = runtime.EconomySolvency.EvaluateDebit(
                scenario.Store.EntityId,
                config.HirePrice);
            Require(unsafeHire.Availability ==
                    EconomyDebitAvailability.DemandWouldBecomeInsolvent &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptWarehouseWorkerHireWouldBlockProjects)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Fresh post-unlock finances did not expose the unsafe hire rejection.");
            int moneyBeforeRejectedHire = scenario.Store.Money;
            int expensesBeforeRejectedHire = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<HireWarehouseWorkerSystem>().Execute();
            Require(runtime.Game.GetEntityWithWarehouseWorkerStoreEntityId(
                        scenario.Store.EntityId) == null &&
                    scenario.Store.Money == moneyBeforeRejectedHire &&
                    scenario.Store.DayUpgradeExpenses == expensesBeforeRejectedHire &&
                    runtime.Game.GetGroup(GameMatcher.WarehouseWorker).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "Unsafe warehouse-worker hire mutated money, ledger, employee or events.");
            CleanupEvents(runtime);

            const int representativeCompletedOrderRevenue = 2000;
            scenario.Store.ReplaceDayRevenue(checked(
                scenario.Store.DayRevenue + representativeCompletedOrderRevenue));
            scenario.Store.ReplaceMoney(checked(
                scenario.Store.Money + representativeCompletedOrderRevenue));
            Require(runtime.EconomySolvency.EvaluateDebit(
                        scenario.Store.EntityId,
                        config.HirePrice).CanDebit,
                "Representative four-order proceeds did not make worker hire solvent.");
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptHireWarehouseWorker,
                            config.HirePrice,
                            config.DailyWage)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "Unlocked store-control terminal did not offer worker hire.");
        }

        private static GameEntity HireWarehouseWorker(
            Runtime runtime,
            Scenario scenario)
        {
            WarehouseWorkerConfig config = runtime.StaticData.WarehouseWorker;
            int moneyBefore = scenario.Store.Money;
            int upgradeExpensesBefore = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<HireWarehouseWorkerSystem>().Execute();
            GameEntity worker = runtime.Game.GetEntityWithWarehouseWorkerStoreEntityId(
                scenario.Store.EntityId);
            Require(worker != null &&
                    runtime.Game.GetGroup(GameMatcher.WarehouseWorker).count == 1 &&
                    scenario.Store.Money == moneyBefore - config.HirePrice &&
                    scenario.Store.DayUpgradeExpenses == checked(
                        upgradeExpensesBefore + config.HirePrice) &&
                    worker.isWorkerShiftActive &&
                    worker.WorkerPaidDayNumber == scenario.Store.DayNumber &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    worker.hasWarehouseWorkerCustomerLoadingPosition &&
                    Vector3.Distance(
                        worker.WarehouseWorkerCustomerLoadingPosition,
                        new Vector3(6f, 0.02f, 1.62f)) < 0.001f &&
                    worker.hasWarehouseWorkerCustomerLoadingRotation &&
                    Quaternion.Angle(
                        worker.WarehouseWorkerCustomerLoadingRotation,
                        Quaternion.Euler(0f, 180f, 0f)) < 0.01f,
                "Worker hire did not create one paid Day 1 employee and debit once.");
            RequireNotificationKey(runtime, LocalizationKey.NotificationWarehouseWorkerHired);
            CleanupEvents(runtime);

            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            runtime.Systems.Create<ConfigureWarehouseWorkerNavigationSystem>().Execute();
            EntityBehaviour view = RequireRuntimeView(
                worker,
                config.ViewPrefab,
                "warehouse worker");
            Require(worker.hasTransform && worker.Transform == view.transform &&
                    worker.hasNavigationAgent &&
                    worker.NavigationAgent.gameObject == view.gameObject &&
                    worker.NavigationAgent.isOnNavMesh &&
                    worker.hasCarryAnchor &&
                    worker.CarryAnchor.IsChildOf(view.transform) &&
                    Mathf.Approximately(worker.NavigationAgent.speed,
                        config.MovementSpeed) &&
                    Mathf.Approximately(worker.NavigationAgent.acceleration,
                        config.Acceleration) &&
                    Mathf.Approximately(worker.NavigationAgent.angularSpeed,
                        config.AngularSpeed) &&
                    Mathf.Approximately(worker.NavigationAgent.stoppingDistance,
                        config.StoppingDistance),
                "Hired worker view did not bind its authored NavMesh and carry adapters.");

            int moneyAfterHire = scenario.Store.Money;
            int expensesAfterHire = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<HireWarehouseWorkerSystem>().Execute();
            Require(ReferenceEquals(
                        runtime.Game.GetEntityWithWarehouseWorkerStoreEntityId(
                            scenario.Store.EntityId),
                        worker) &&
                    runtime.Game.GetGroup(GameMatcher.WarehouseWorker).count == 1 &&
                    scenario.Store.Money == moneyAfterHire &&
                    scenario.Store.DayUpgradeExpenses == expensesAfterHire,
                "Repeated hire created a second worker or debited twice.");
            CleanupEvents(runtime);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptWarehouseWorkerActive,
                            config.DailyWage)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Hired worker terminal remained actionable.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
            return worker;
        }

        private static void ValidateWarehouseWorkerCustomerLoadingFlow(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker)
        {
            ProductTypeId cement = ProductTypeId.CementBag;
            ProductTypeId boards = ProductTypeId.BoardBundle;
            DeliveryArrival cementArrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                cement);
            StoreDeliveryWithWarehouseWorker(
                runtime,
                scenario,
                worker,
                cementArrival);
            CleanupCompletedDelivery(runtime, scenario, cementArrival);
            Require(CountStockProducts(runtime.Game,
                        scenario.StorageZone.EntityId,
                        cement) == 3,
                "Customer-loading smoke requires a complete three-unit cement batch.");

            DeliveryArrival boardArrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                boards);
            runtime.Systems.Create<GenerateInboundStorageTaskSystem>().Execute();
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            GameEntity assignedInbound = FindLiveWarehouseTasks(runtime.Game).Single();
            Require(assignedInbound.hasAssignedWorkerEntityId &&
                    assignedInbound.AssignedWorkerEntityId == worker.EntityId &&
                    assignedInbound.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToPickup,
                "The no-preemption setup did not assign its inbound task.");

            CustomerVisit bayA = SpawnAndParkCustomer(runtime, scenario);
            OpenConsultation(runtime, scenario, bayA.Entity);
            GameEntity[] bayALines = ConfirmConsultation(
                runtime,
                scenario,
                bayA.Entity,
                runtime.StaticData.GetProject(
                    CustomerProjectTypeId.CementFoundation).Offers[1]);
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0 &&
                    assignedInbound.hasAssignedWorkerEntityId &&
                    assignedInbound.AssignedWorkerEntityId == worker.EntityId &&
                    assignedInbound.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToPickup &&
                    worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.MovingToPickup,
                "A newly available outbound order preempted an assigned inbound task.");

            GameEntity firstBoard = CompleteAssignedInboundTask(
                runtime,
                scenario,
                worker,
                assignedInbound);
            Require(firstBoard.ProductType == boards && firstBoard.isInStock,
                "The no-preemption inbound task did not finish before outbound work.");

            CustomerVisit bayB = SpawnCustomerToQueue(
                runtime,
                scenario,
                promoteAtCounter: true);
            OpenConsultation(runtime, scenario, bayB.Entity);
            SelectConsultationOfferWithWraparound(
                runtime,
                scenario,
                bayB.Entity,
                selectedIndex: 2);
            GameEntity[] bayBLines = ConfirmConsultation(
                runtime,
                scenario,
                bayB.Entity,
                runtime.StaticData.GetProject(
                    CustomerProjectTypeId.LumberShelving).Offers[2],
                advanceToLoadingBay: false);
            ReturnAcceptedCustomerToParkingWait(runtime, scenario, bayB);

            GameEntity[] cementStock = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .Where(product => product.ProductType == cement)
                .OrderBy(product => product.StorageSlotIndex)
                .ToArray();
            Require(cementStock.Length == 3,
                "Cooperative loading requires three deterministic cement candidates.");
            GameEntity playerProduct = cementStock[0];
            GameEntity looseExcluded = cementStock[1];
            GameEntity trolleyExcluded = cementStock[2];
            RequestInteraction(scenario.Player, playerProduct);
            looseExcluded.isLooseProduct = true;
            trolleyExcluded.AddTrolleyEntityId(int.MinValue + 101);
            trolleyExcluded.AddTrolleySlotIndex(0);
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Outbound generation selected player-requested, loose or trolley cargo.");
            looseExcluded.isLooseProduct = false;
            looseExcluded.AddCarrierEntityId(int.MinValue + 102);
            trolleyExcluded.RemoveTrolleyEntityId();
            trolleyExcluded.RemoveTrolleySlotIndex();
            trolleyExcluded.isLooseProduct = true;
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Outbound generation selected player-requested, carried or loose cargo.");
            looseExcluded.RemoveCarrierEntityId();
            trolleyExcluded.isLooseProduct = false;

            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            GameEntity outboundA = FindLiveCustomerLoadingTasks(runtime.Game).Single();
            GameEntity workerProduct = runtime.Game.GetEntityWithEntityId(
                outboundA.WarehouseTaskProductEntityId);
            Require(outboundA.WarehouseTaskCustomerVisitEntityId == bayA.Entity.EntityId &&
                    outboundA.WarehouseTaskOrderLineEntityId == bayALines.Single().EntityId &&
                    outboundA.WarehouseTaskReservedLoadingSlotIndex == 0 &&
                    workerProduct != playerProduct &&
                    !workerProduct.isInteractable &&
                    workerProduct.hasReservedStorageSlotIndex &&
                    workerProduct.hasReservedOrderLineEntityId &&
                    runtime.Game.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                        bayB.Entity.EntityId).Count == 0,
                "Outbound generation ignored bay ownership, FIFO line or player-first cargo.");

            GameEntity priorityInboundProduct = boardArrival.Products
                .Where(product => product.isInboundProduct &&
                                  product.hasDeliverySlotIndex &&
                                  product.isInteractable)
                .OrderBy(product => product.DeliverySlotIndex)
                .First();
            int priorityStorageSlot = FindFirstFreeStorageSlot(
                runtime.Game,
                scenario.StorageZone);
            GameEntity priorityInbound = runtime.WarehouseTasks.CreateInboundToStorage(
                scenario.Store.EntityId,
                priorityInboundProduct.EntityId,
                scenario.StorageZone.EntityId,
                priorityStorageSlot);
            priorityInboundProduct.isInteractable = false;
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            Require(outboundA.hasAssignedWorkerEntityId &&
                    outboundA.AssignedWorkerEntityId == worker.EntityId &&
                    outboundA.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToPickup &&
                    !priorityInbound.hasAssignedWorkerEntityId &&
                    priorityInbound.WarehouseTaskStep ==
                    WarehouseTaskStepId.Available,
                "An idle worker did not prioritize outbound work over available inbound work.");
            priorityInbound.isDestructed = true;
            priorityInboundProduct.isInteractable = true;

            runtime.Systems.Create<PickUpProductSystem>().Execute();
            Require(playerProduct.hasCarrierEntityId &&
                    playerProduct.CarrierEntityId == scenario.Player.EntityId &&
                    scenario.Player.isHandsOccupied &&
                    playerProduct.hasReservedOrderLineEntityId &&
                    playerProduct.ReservedOrderLineEntityId == bayALines.Single().EntityId,
                "The player's same-frame request did not reserve its exact product.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            CleanupEvents(runtime);

            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowWorkerCarriedProductSystem>().Execute();
            Require(outboundA.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToCustomerLoading &&
                    worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.MovingToCustomerLoading &&
                    workerProduct.hasCarrierEntityId &&
                    workerProduct.CarrierEntityId == worker.EntityId,
                "The outbound task did not pick its reserved shelf product.");

            RequestInteraction(scenario.Player, bayA.Entity);
            runtime.Systems.Create<LoadHeldProductSystem>().Execute();
            WarpWarehouseWorker(worker,
                worker.WarehouseWorkerCustomerLoadingPosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            GameEntity[] cooperativeProducts = { playerProduct, workerProduct };
            Require(cooperativeProducts.All(product =>
                        product.isLoaded && product.isProductLoaded &&
                        product.hasLoadingSlotIndex &&
                        product.hasOrderLineEntityId) &&
                    cooperativeProducts.Select(product => product.LoadingSlotIndex)
                        .OrderBy(index => index).SequenceEqual(new[] { 0, 1 }) &&
                    outboundA.isDestructed &&
                    !worker.isHandsOccupied && !worker.isCarryingProduct,
                "Player and worker did not produce distinct deterministic pending slots.");
            runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
            Require(!playerProduct.isProductLoaded &&
                    !workerProduct.isProductLoaded &&
                    bayALines.Single().LoadedProductCount == 2,
                "One registration pass did not consume both cooperative pending products.");
            runtime.Systems.Create<CompleteOrderSystem>().Execute();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            Require(bayA.Entity.isCustomerVisitCompleted &&
                    !bayA.Entity.isCustomerVisitLoading,
                "The cooperative two-product order did not complete exactly once.");
            RewardCustomerOrder(runtime, scenario, bayA.Entity, cooperativeProducts);
            DepartAndCleanupCustomer(
                runtime,
                scenario,
                bayA,
                bayALines,
                cooperativeProducts);

            ValidateWaitingBayPromotion(runtime, scenario, bayB);
            ValidateOutboundRecoveryAndClosing(
                runtime,
                scenario,
                worker,
                boardArrival,
                bayB,
                bayBLines,
                firstBoard);
        }

        private static void ValidateOutboundRecoveryAndClosing(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            DeliveryArrival boardArrival,
            CustomerVisit visit,
            GameEntity[] orderLines,
            GameEntity initialShelfProduct)
        {
            GameEntity line = orderLines.Single();
            Require(visit.Entity.isCustomerVisitLoading &&
                    line.ProductType == ProductTypeId.BoardBundle &&
                    line.RequiredProductCount == 3 &&
                    line.LoadedProductCount == 0 &&
                    initialShelfProduct.isInStock &&
                    initialShelfProduct.hasStorageSlotIndex,
                "Bay-B recovery matrix requires one stocked board for a three-board order.");

            GameEntity[] sentinels = FillFreeStorageSlotsWithSentinels(
                runtime,
                scenario.StorageZone);
            ExecuteStorageState(runtime);
            Require(scenario.StorageZone.OccupiedStorageSlotCount ==
                    scenario.StorageZone.Slots.Length,
                "StorageFull outbound smoke did not occupy every authored shelf slot.");
            runtime.Systems.Create<GenerateInboundStorageTaskSystem>().Execute();
            Require(worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.StorageFull &&
                    FindLiveWarehouseTasks(runtime.Game).Length == 0,
                "Full storage did not hold inbound work in StorageFull state.");
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            GameEntity timeoutTask = FindLiveCustomerLoadingTasks(runtime.Game).Single();
            Require(worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    timeoutTask.WarehouseTaskProductEntityId ==
                    initialShelfProduct.EntityId,
                "StorageFull prevented an eligible outbound task.");
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            RecoverTimedOutCustomerLoadingTask(
                runtime,
                scenario,
                worker,
                timeoutTask,
                initialShelfProduct);

            GameEntity workerMissingBeforePickup = CreateAssignedCustomerLoadingTask(
                runtime,
                worker,
                visit.Entity);
            RecoverWorkerMissingCustomerLoadingTask(
                runtime,
                scenario,
                worker,
                workerMissingBeforePickup,
                initialShelfProduct,
                afterPickup: false);

            GameEntity workerMissingAfterPickup = CreateAssignedCustomerLoadingTask(
                runtime,
                worker,
                visit.Entity);
            RecoverWorkerMissingCustomerLoadingTask(
                runtime,
                scenario,
                worker,
                workerMissingAfterPickup,
                initialShelfProduct,
                afterPickup: true);

            GameEntity noPathTask = CreateAssignedCustomerLoadingTask(
                runtime,
                worker,
                visit.Entity);
            GameEntity firstLoadedBoard = RecoverNoPathAndCompleteManualHandoff(
                runtime,
                scenario,
                worker,
                noPathTask,
                visit.Entity,
                line,
                initialShelfProduct);
            Require(line.LoadedProductCount == 1 &&
                    visit.Entity.isCustomerVisitLoading &&
                    !visit.Entity.isCustomerVisitCompleted,
                "Recovery matrix completed bay B before the final two products.");

            foreach (GameEntity sentinel in sentinels)
                sentinel.isDestructed = true;
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            ExecuteStorageState(runtime);
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Bay B generated outbound work despite missing board stock.");

            runtime.Systems.Create<GenerateInboundStorageTaskSystem>().Execute();
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            GameEntity missingStockInbound = FindLiveWarehouseTasks(runtime.Game).Single();
            GameEntity secondShelfBoard = CompleteAssignedInboundTask(
                runtime,
                scenario,
                worker,
                missingStockInbound);
            Require(secondShelfBoard.ProductType == ProductTypeId.BoardBundle &&
                    secondShelfBoard.isInStock,
                "Missing stock did not flow through inbound work into an outbound candidate.");

            GameEntity thirdShelfBoard = boardArrival.Products.Single(product =>
                product.isInboundProduct && product.hasDeliverySlotIndex &&
                product.isInteractable);
            StockInboundProductManually(
                runtime,
                scenario,
                thirdShelfBoard);
            CleanupCompletedDelivery(runtime, scenario, boardArrival);
            Require(CountStockProducts(runtime.Game,
                        scenario.StorageZone.EntityId,
                        ProductTypeId.BoardBundle) == 2,
                "Closing setup did not preserve exactly two outstanding board products.");

            AdvanceStoreFrom1959ToClosing(runtime, scenario);
            var loadedProducts = new List<GameEntity> { firstLoadedBoard };
            loadedProducts.Add(CompleteOneCustomerLoadingTask(
                runtime,
                scenario,
                worker,
                visit.Entity,
                line,
                expectCompleted: false));
            loadedProducts.Add(CompleteOneCustomerLoadingTask(
                runtime,
                scenario,
                worker,
                visit.Entity,
                line,
                expectCompleted: true));
            Require(scenario.Store.isStoreClosing &&
                    scenario.Store.CurrentDayMinute == 20 * 60 &&
                    line.LoadedProductCount == 3 &&
                    visit.Entity.isCustomerVisitCompleted &&
                    !visit.Entity.isCustomerVisitLoading,
                "Closing did not allow multiple sequential outbound tasks to finish bay B.");

            RewardCustomerOrder(
                runtime,
                scenario,
                visit.Entity,
                loadedProducts.ToArray());
            DepartAndCleanupCustomer(
                runtime,
                scenario,
                visit,
                orderLines,
                loadedProducts.ToArray());
            runtime.Systems.Create<ValidateWarehouseWorkerStateSystem>().Execute();
            Require(FindLiveWarehouseTasks(runtime.Game).Length == 0 &&
                    FindLiveCustomerLoadingTasks(runtime.Game).Length == 0 &&
                    runtime.Game.GetGroup(GameMatcher.AssignedWorkerEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.WarehouseTaskReservedLoadingSlotIndex)
                        .count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.CarrierEntityId).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.ReservedOrderLineEntityId).count == 0 &&
                    !worker.isHandsOccupied && !worker.isCarryingProduct &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Outbound smoke left task, carrier, loading-slot or order reservations behind.");
        }

        private static void ValidateWarehouseWorkerStorageFull(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker)
        {
            Require(scenario.StorageZone.Slots.Length == 9 &&
                    scenario.StorageZone.StorageProductCount == 0 &&
                    scenario.StorageZone.OccupiedStorageSlotCount == 0,
                "Warehouse worker storage-full smoke requires nine empty authored slots.");
            int firstSentinelId = runtime.Game
                .GetGroup(GameMatcher.EntityId)
                .GetEntities()
                .Min(entity => entity.EntityId) - 9;
            GameEntity[] occupyingProducts = Enumerable.Range(0, 9)
                .Select(slotIndex => CreateEntity.Empty(firstSentinelId + slotIndex)
                    .AddProductType(ProductTypeId.CementBag)
                    .AddStorageZoneEntityId(scenario.StorageZone.EntityId)
                    .AddStorageSlotIndex(slotIndex))
                .ToArray();
            foreach (GameEntity product in occupyingProducts)
            {
                product.isProduct = true;
                product.isInStock = true;
                product.isInteractable = true;
            }

            ExecuteStorageState(runtime);
            try
            {
                Require(scenario.StorageZone.StorageProductCount == 9 &&
                        scenario.StorageZone.OccupiedStorageSlotCount == 9,
                    "Nine real stocked products did not occupy all authored storage slots.");
                runtime.Systems.Create<GenerateInboundStorageTaskSystem>().Execute();
                Require(worker.WarehouseWorkerStatus ==
                        WarehouseWorkerStatusId.StorageFull &&
                        FindLiveWarehouseTasks(runtime.Game).Length == 0,
                    "Worker did not enter a non-destructive StorageFull wait state.");
            }
            finally
            {
                foreach (GameEntity product in occupyingProducts)
                    product.isDestructed = true;
                runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
                ExecuteStorageState(runtime);
            }

            Require(scenario.StorageZone.StorageProductCount == 0 &&
                    scenario.StorageZone.OccupiedStorageSlotCount == 0,
                "Storage-full smoke did not release its nine occupied slots.");
            runtime.Systems.Create<GenerateInboundStorageTaskSystem>().Execute();
            GameEntity task = RequireSingle(runtime.Game.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskReservedStorageSlotIndex)
                .NoneOf(GameMatcher.Destructed)), "available warehouse task");
            Require(worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    task.WarehouseTaskStep == WarehouseTaskStepId.Available &&
                    task.WarehouseTaskReservedStorageSlotIndex == 0,
                "Restoring storage did not create one deterministic first-slot worker task.");
        }

        private static void ValidateWarehouseWorkerTimeoutRecovery(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            DeliveryArrival arrival)
        {
            GameEntity task = FindLiveWarehouseTasks(runtime.Game).Single();
            GameEntity reservedProduct = runtime.Game.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            int reservedDeliverySlot = reservedProduct.DeliverySlotIndex;
            Require(task.WarehouseTaskReservedStorageSlotIndex == 0 &&
                    !reservedProduct.isInteractable,
                "Worker task did not reserve its product and first storage slot.");

            RequestInteraction(scenario.Player, reservedProduct);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            Require(!scenario.Player.isHandsOccupied &&
                    !reservedProduct.hasCarrierEntityId &&
                    reservedProduct.hasDeliverySlotIndex &&
                    reservedProduct.DeliverySlotIndex == reservedDeliverySlot,
                "Player took worker-reserved inbound cargo through a direct interaction request.");
            CleanupEvents(runtime);

            GameEntity otherProduct = arrival.Products.First(product =>
                product != reservedProduct && product.isInteractable);
            int otherDeliverySlot = otherProduct.DeliverySlotIndex;
            PickUpProduct(runtime, scenario, otherProduct);
            RequestInteraction(scenario.Player, scenario.StorageZone);
            runtime.Systems.Create<StoreInboundProductSystem>().Execute();
            Require(otherProduct.isInStock && otherProduct.hasStorageSlotIndex &&
                    otherProduct.StorageSlotIndex !=
                    task.WarehouseTaskReservedStorageSlotIndex,
                "Player storage intake consumed the worker task's reserved storage slot.");
            RestoreSmokeProductToDeliverySlot(
                otherProduct,
                otherDeliverySlot,
                arrival.Delivery);
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);

            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            Require(task.hasAssignedWorkerEntityId &&
                    task.AssignedWorkerEntityId == worker.EntityId &&
                    task.WarehouseTaskStep == WarehouseTaskStepId.MovingToPickup &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.MovingToPickup,
                "Worker did not take the oldest available inbound task.");

            WarpWarehouseWorker(worker, worker.WarehouseWorkerPickupPosition);
            runtime.Systems.Create<ExecuteInboundStorageTaskSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowWorkerCarriedProductSystem>().Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.MovingToStorage &&
                    task.hasAssignedWorkerEntityId &&
                    task.AssignedWorkerEntityId == worker.EntityId &&
                    task.hasWarehouseTaskReservedStorageSlotIndex &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.MovingToStorage &&
                    worker.isHandsOccupied && worker.isCarryingProduct &&
                    reservedProduct.hasCarrierEntityId &&
                    reservedProduct.CarrierEntityId == worker.EntityId &&
                    reservedProduct.hasReservedDeliverySlotIndex &&
                    reservedProduct.ReservedDeliverySlotIndex == reservedDeliverySlot &&
                    !reservedProduct.hasDeliverySlotIndex &&
                    !reservedProduct.isInteractable,
                "Timeout smoke did not reach the real carried-product reservation state.");
            ValidateWarehouseWorkerActiveTaskBlocksReport(
                runtime,
                scenario,
                task);

            task.ReplaceWarehouseTaskTimeoutRemaining(0f);
            runtime.Systems.Create<TickWarehouseTaskTimeoutSystem>().Execute();
            runtime.Systems.Create<RecoverBlockedInboundTaskSystem>().Execute();
            runtime.Systems.Create<ValidateWarehouseWorkerStateSystem>().Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.Blocked &&
                    task.WarehouseTaskBlockReason == WarehouseTaskBlockReasonId.TimedOut &&
                    !task.hasAssignedWorkerEntityId &&
                    !task.hasWarehouseTaskReservedStorageSlotIndex &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Blocked &&
                    !worker.isHandsOccupied && !worker.isCarryingProduct &&
                    reservedProduct.isInteractable &&
                    reservedProduct.hasDeliverySlotIndex &&
                    reservedProduct.DeliverySlotIndex == reservedDeliverySlot &&
                    !reservedProduct.hasReservedDeliverySlotIndex &&
                    !reservedProduct.hasCarrierEntityId,
                "Timed-out worker task did not restore its exact delivery slot and release state.");
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationWarehouseWorkerTaskBlocked);
            CleanupEvents(runtime);
            ValidateBlockedWarehouseDiagnosticAllowsReport(runtime, scenario);

            PickUpProduct(runtime, scenario, reservedProduct);
            runtime.Systems.Create<CleanupBlockedWarehouseTaskSystem>().Execute();
            Require(task.isDestructed &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Player recovery did not release the blocked diagnostic or worker.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            DropHeldProduct(runtime, scenario);
            MoveLooseProductBelowRecoveryBoundary(runtime, reservedProduct);
            RecoverLostProducts(runtime);
            RequireNotificationKey(runtime, LocalizationKey.NotificationProductsRecovered);
            ExecuteProductPlacement(runtime);
            CleanupEvents(runtime);
            Require(reservedProduct.isInboundProduct &&
                    reservedProduct.isInteractable &&
                    reservedProduct.hasDeliverySlotIndex &&
                    reservedProduct.DeliverySlotIndex == reservedDeliverySlot &&
                    runtime.Game.GetEntityWithWarehouseTaskProductEntityId(
                        reservedProduct.EntityId) == null,
                "Timeout recovery left the inbound product locked after exact-slot restoration.");
        }

        private static void StoreDeliveryWithWarehouseWorker(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            DeliveryArrival arrival)
        {
            int initialStock = scenario.StorageZone.StorageProductCount;
            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            Require((worker.Transform.position -
                     worker.WarehouseWorkerPickupPosition).sqrMagnitude >
                    runtime.StaticData.WarehouseWorker.StoppingDistance *
                    runtime.StaticData.WarehouseWorker.StoppingDistance,
                "Automatic stocking smoke requires the worker away from the pickup point.");
            for (int productIndex = 0;
                 productIndex < arrival.Products.Length;
                 productIndex++)
            {
                runtime.Systems.Create<WarehouseWorkerFeature>().Execute();
                GameEntity task = FindLiveWarehouseTasks(runtime.Game).Single();
                GameEntity product = runtime.Game.GetEntityWithEntityId(
                    task.WarehouseTaskProductEntityId);
                int expectedDeliverySlot = arrival.Products
                    .Where(candidate => candidate.isInboundProduct &&
                                        candidate.hasDeliverySlotIndex)
                    .Min(candidate => candidate.DeliverySlotIndex);
                Require(task.hasAssignedWorkerEntityId &&
                        task.AssignedWorkerEntityId == worker.EntityId &&
                        task.WarehouseTaskStep == WarehouseTaskStepId.MovingToPickup &&
                        product.DeliverySlotIndex == expectedDeliverySlot &&
                        !product.isInteractable,
                    "Worker did not reserve exactly one lowest-slot inbound product.");

                WarpWarehouseWorker(worker, worker.WarehouseWorkerPickupPosition);
                runtime.Systems.Create<WarehouseWorkerFeature>().Execute();
                ExecuteProductPlacement(runtime);
                runtime.Systems.Create<FollowWorkerCarriedProductSystem>().Execute();
                Require(task.WarehouseTaskStep == WarehouseTaskStepId.MovingToStorage &&
                        worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.MovingToStorage &&
                        worker.isHandsOccupied && worker.isCarryingProduct &&
                        product.hasCarrierEntityId &&
                        product.CarrierEntityId == worker.EntityId &&
                        product.hasReservedDeliverySlotIndex &&
                        product.ReservedDeliverySlotIndex == expectedDeliverySlot,
                    "Worker did not pick and carry its exact inbound product.");

                WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
                runtime.Systems.Create<WarehouseWorkerFeature>().Execute();
                Require(task.isDestructed && product.isProductStocked &&
                        product.isInStock && !product.isInboundProduct &&
                        product.isInteractable &&
                        product.hasStorageZoneEntityId &&
                        product.StorageZoneEntityId == scenario.StorageZone.EntityId &&
                        product.hasStorageSlotIndex &&
                        !product.hasCarrierEntityId &&
                        !worker.isHandsOccupied && !worker.isCarryingProduct &&
                        worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                    "Worker did not complete one inbound-to-storage task cleanly.");

                runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
                runtime.Systems.Create<RegisterStockedProductSystem>().Execute();
                runtime.Systems.Create<CompleteDeliverySystem>().Execute();
                ExecuteProductPlacement(runtime);
                ExecuteStorageState(runtime);
                CleanupEvents(runtime);
                Require(scenario.StorageZone.StorageProductCount ==
                        initialStock + productIndex + 1 &&
                        scenario.StorageZone.OccupiedStorageSlotCount ==
                        initialStock + productIndex + 1,
                    "Worker-stocked product did not update derived storage state exactly once.");
            }

            Require(arrival.Delivery.isDeliveryCompleted &&
                    arrival.Delivery.isDestructed &&
                    !arrival.Delivery.hasDeliveryProcurementTerminalEntityId &&
                    FindLiveWarehouseTasks(runtime.Game).Length == 0 &&
                    arrival.Products.All(product => product.isInStock) &&
                    arrival.Products.Select(product => product.StorageSlotIndex)
                        .Distinct().Count() == arrival.Products.Length,
                "Worker did not complete and uniquely store the full three-product delivery.");
        }

        private static void ValidateWarehouseWorkerActiveTaskBlocksReport(
            Runtime runtime,
            Scenario scenario,
            GameEntity task)
        {
            EnterWarehouseWorkerReportSmokeClosing(scenario);
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    !scenario.Player.isModalOpen &&
                    task.hasAssignedWorkerEntityId &&
                    task.hasWarehouseTaskReservedStorageSlotIndex,
                "Day report opened while the worker owned active task state.");
            CleanupEvents(runtime);
            ExitWarehouseWorkerReportSmokeClosing(scenario);
        }

        private static void ValidateBlockedWarehouseDiagnosticAllowsReport(
            Runtime runtime,
            Scenario scenario)
        {
            EnterWarehouseWorkerReportSmokeClosing(scenario);
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(scenario.Store.isDayReportOpen &&
                    !scenario.Store.isStoreClosing &&
                    scenario.Player.isModalOpen &&
                    scenario.Player.DayReportStoreEntityId == scenario.Store.EntityId,
                "Released blocked-task diagnostic prevented the mandatory report.");
            CleanupEvents(runtime);

            scenario.Store.isDayReportOpen = false;
            scenario.Store.isStoreOpen = true;
            scenario.Store.ReplaceCurrentDayMinute(runtime.StaticData.StoreDay.StartMinute);
            scenario.Store.AddCustomerCooldownRemaining(
                runtime.StaticData.CustomerFlow.FirstArrivalDelay);
            scenario.Player.isModalOpen = false;
            scenario.Player.RemoveDayReportStoreEntityId();
            scenario.Player.isCursorLocked = true;
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
        }

        private static void EnterWarehouseWorkerReportSmokeClosing(Scenario scenario)
        {
            Require(scenario.Store.isStoreOpen &&
                    scenario.Store.hasCustomerCooldownRemaining &&
                    !scenario.Player.isModalOpen,
                "Worker report smoke requires an open scheduled-customer phase.");
            scenario.Store.isStoreOpen = false;
            scenario.Store.isStoreClosing = true;
            scenario.Store.ReplaceCurrentDayMinute(20 * 60);
            scenario.Store.RemoveCustomerCooldownRemaining();
        }

        private static void ExitWarehouseWorkerReportSmokeClosing(Scenario scenario)
        {
            scenario.Store.isStoreClosing = false;
            scenario.Store.isStoreOpen = true;
            scenario.Store.ReplaceCurrentDayMinute(8 * 60);
            scenario.Store.AddCustomerCooldownRemaining(1f);
        }

        private static void RestoreSmokeProductToDeliverySlot(
            GameEntity product,
            int deliverySlotIndex,
            GameEntity delivery)
        {
            Require(product.isInStock && product.isProductStocked &&
                    product.hasStorageZoneEntityId && product.hasStorageSlotIndex &&
                    product.hasDeliveryEntityId &&
                    product.DeliveryEntityId == delivery.EntityId &&
                    !product.hasReservedDeliverySlotIndex &&
                    !product.hasCarrierEntityId,
                "Smoke product cannot be restored from its temporary storage intake state.");
            product.isProductStocked = false;
            product.isInStock = false;
            product.isInboundProduct = true;
            product.RemoveStorageZoneEntityId();
            product.RemoveStorageSlotIndex();
            product.AddDeliverySlotIndex(deliverySlotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
        }

        private static void WarpWarehouseWorker(GameEntity worker, Vector3 position)
        {
            Require(worker.hasNavigationAgent && worker.NavigationAgent.isOnNavMesh,
                "Warehouse worker must be on NavMesh before smoke warping.");
            worker.NavigationAgent.ResetPath();
            Require(worker.NavigationAgent.Warp(position),
                $"Warehouse worker could not warp to authored access point {position}.");
            Physics.SyncTransforms();
        }

        private static GameEntity[] FindLiveWarehouseTasks(GameContext context) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.InboundToStorageTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskStep)
                .NoneOf(GameMatcher.Destructed))
                .GetEntities()
                .OrderBy(task => task.EntityId)
                .ToArray();

        private static GameEntity[] FindLiveCustomerLoadingTasks(
            GameContext context) =>
            context.GetGroup(GameMatcher.AllOf(
                    GameMatcher.WarehouseTask,
                    GameMatcher.StockToCustomerLoadingTask,
                    GameMatcher.EntityId,
                    GameMatcher.WarehouseTaskProductEntityId,
                    GameMatcher.WarehouseTaskCustomerVisitEntityId,
                    GameMatcher.WarehouseTaskOrderLineEntityId,
                    GameMatcher.WarehouseTaskStep)
                .NoneOf(GameMatcher.Destructed))
                .GetEntities()
                .OrderBy(task => task.EntityId)
                .ToArray();

        private static int FindFirstFreeStorageSlot(
            GameContext context,
            GameEntity storageZone)
        {
            var occupied = new HashSet<int>();
            foreach (GameEntity product in FindStockProducts(
                         context,
                         storageZone.EntityId))
            {
                if (product.hasStorageSlotIndex)
                    occupied.Add(product.StorageSlotIndex);
                if (product.hasReservedStorageSlotIndex)
                    occupied.Add(product.ReservedStorageSlotIndex);
            }
            foreach (GameEntity task in context.GetGroup(GameMatcher.AllOf(
                         GameMatcher.WarehouseTask,
                         GameMatcher.WarehouseTaskStorageZoneEntityId,
                         GameMatcher.WarehouseTaskReservedStorageSlotIndex)
                     .NoneOf(GameMatcher.Destructed)).GetEntities())
            {
                if (task.WarehouseTaskStorageZoneEntityId == storageZone.EntityId)
                    occupied.Add(task.WarehouseTaskReservedStorageSlotIndex);
            }

            for (int slotIndex = 0;
                 slotIndex < storageZone.Slots.Length;
                 slotIndex++)
            {
                if (!occupied.Contains(slotIndex))
                    return slotIndex;
            }
            throw new InvalidOperationException(
                $"Storage zone {storageZone.EntityId} has no free smoke-test slot.");
        }

        private static GameEntity CompleteAssignedInboundTask(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity task)
        {
            Require(task.isInboundToStorageTask &&
                    !task.isStockToCustomerLoadingTask &&
                    task.hasAssignedWorkerEntityId &&
                    task.AssignedWorkerEntityId == worker.EntityId &&
                    task.WarehouseTaskStep == WarehouseTaskStepId.MovingToPickup,
                "Only an assigned inbound task can be completed by this smoke helper.");
            GameEntity product = runtime.Game.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            WarpWarehouseWorker(worker, worker.WarehouseWorkerPickupPosition);
            runtime.Systems.Create<ExecuteInboundStorageTaskSystem>().Execute();
            ExecuteProductPlacement(runtime);
            runtime.Systems.Create<FollowWorkerCarriedProductSystem>().Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.MovingToStorage &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == worker.EntityId,
                "Assigned inbound task did not pick its exact delivery product.");
            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            runtime.Systems.Create<ExecuteInboundStorageTaskSystem>().Execute();
            Require(task.isDestructed && product.isProductStocked &&
                    product.isInStock && product.hasStorageSlotIndex &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    !worker.isHandsOccupied && !worker.isCarryingProduct,
                "Assigned inbound task did not settle product and worker state.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            runtime.Systems.Create<RegisterStockedProductSystem>().Execute();
            runtime.Systems.Create<CompleteDeliverySystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            return product;
        }

        private static void ReturnAcceptedCustomerToParkingWait(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit visit)
        {
            GameEntity entity = visit.Entity;
            GameEntity actor = runtime.Game.GetEntityWithCustomerActorVisitEntityId(
                entity.EntityId);
            Require(entity.isCustomerVisitReturning && actor != null &&
                    actor.isCustomerWaitingAtCounter &&
                    !entity.hasReservedCustomerLoadingBayEntityId,
                "Bay-B customer did not begin from the accepted counter state.");
            runtime.Systems.Create<ReserveCustomerLoadingBaySystem>().Execute();
            Require(!entity.hasReservedCustomerLoadingBayEntityId,
                "Bay-B customer stole the loading bay from active bay A.");
            runtime.Systems.Create<BeginCustomerReturnSystem>().Execute();
            runtime.Systems.Create<AdvanceCustomerQueueSystem>().Execute();
            Require(actor.isCustomerReturningToVehicle &&
                    actor.hasRoute && actor.hasRouteWaypointIndex,
                "Bay-B actor did not start its authored return route.");
            int actorId = actor.EntityId;
            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerReturnSystem>().Execute();
            Require(entity.isCustomerVisitWaitingForLoadingBay &&
                    !entity.hasReservedCustomerLoadingBayEntityId &&
                    actor.isDestructed,
                "Bay-B customer did not settle into parking-bay wait.");
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(runtime.Game.GetEntityWithEntityId(actorId) == null &&
                    !visit.ActorView.HasEntity,
                "Bay-B returning actor survived cleanup.");
            runtime.Systems.Create<MoveCustomerVehicleToLoadingBaySystem>().Execute();
            Require(entity.isCustomerVisitWaitingForLoadingBay &&
                    !entity.isCustomerVisitMovingToLoadingBay &&
                    !entity.isCustomerVisitLoading,
                "Bay-B vehicle moved before owning the loading bay.");
            Require(runtime.Game.GetEntitiesWithWarehouseTaskCustomerVisitEntityId(
                        entity.EntityId).Count == 0,
                "Waiting bay-B customer already owned outbound work before its loading-bay " +
                "eligibility check.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
        }

        private static void ValidateWaitingBayPromotion(
            Runtime runtime,
            Scenario scenario,
            CustomerVisit visit)
        {
            GameEntity entity = visit.Entity;
            Require(entity.isCustomerVisitWaitingForLoadingBay &&
                    !entity.hasReservedCustomerLoadingBayEntityId &&
                    FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Bay-B promotion did not start from a clean waiting state.");
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Bay-B waiting state generated outbound work before reservation.");
            runtime.Systems.Create<ReserveCustomerLoadingBaySystem>().Execute();
            Require(entity.hasReservedCustomerLoadingBayEntityId &&
                    entity.isCustomerVisitWaitingForLoadingBay,
                "Oldest bay-B customer did not reserve the released loading bay.");
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Reserved but parked bay-B customer generated outbound work.");
            runtime.Systems.Create<MoveCustomerVehicleToLoadingBaySystem>().Execute();
            Require(entity.isCustomerVisitMovingToLoadingBay &&
                    !entity.isCustomerVisitWaitingForLoadingBay,
                "Bay-B customer did not start its authored loading-bay route.");
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            Require(FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Moving bay-B customer generated outbound work before arrival.");
            ForceRouteEndpoint(runtime, entity);
            runtime.Systems.Create<CompleteCustomerLoadingBayArrivalSystem>().Execute();
            Require(entity.isCustomerVisitLoading &&
                    !entity.isCustomerVisitMovingToLoadingBay &&
                    entity.isInteractable,
                "Bay-B customer did not become loadable at the authored endpoint.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
        }

        private static GameEntity[] FillFreeStorageSlotsWithSentinels(
            Runtime runtime,
            GameEntity storageZone)
        {
            var occupied = new HashSet<int>();
            foreach (GameEntity product in FindStockProducts(
                         runtime.Game,
                         storageZone.EntityId))
            {
                if (product.hasStorageSlotIndex)
                    occupied.Add(product.StorageSlotIndex);
                if (product.hasReservedStorageSlotIndex)
                    occupied.Add(product.ReservedStorageSlotIndex);
            }
            int[] freeSlots = Enumerable.Range(0, storageZone.Slots.Length)
                .Where(slotIndex => !occupied.Contains(slotIndex))
                .ToArray();
            int firstEntityId = runtime.Game.GetGroup(GameMatcher.EntityId)
                .GetEntities().Min(entity => entity.EntityId) - freeSlots.Length - 1;
            var sentinels = new GameEntity[freeSlots.Length];
            for (int index = 0; index < freeSlots.Length; index++)
            {
                GameEntity sentinel = CreateEntity.Empty(firstEntityId + index)
                    .AddProductType(ProductTypeId.CementBag)
                    .AddStorageZoneEntityId(storageZone.EntityId)
                    .AddStorageSlotIndex(freeSlots[index]);
                sentinel.isProduct = true;
                sentinel.isInStock = true;
                sentinel.isInteractable = true;
                sentinels[index] = sentinel;
            }
            return sentinels;
        }

        private static GameEntity CreateAssignedCustomerLoadingTask(
            Runtime runtime,
            GameEntity worker,
            GameEntity visit)
        {
            Require(worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    visit.isCustomerVisitLoading &&
                    FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Assigned outbound helper requires an idle worker and clean loading visit.");
            runtime.Systems.Create<GenerateCustomerLoadingTaskSystem>().Execute();
            GameEntity task = FindLiveCustomerLoadingTasks(runtime.Game).Single();
            runtime.Systems.Create<AssignWarehouseTaskSystem>().Execute();
            Require(task.hasAssignedWorkerEntityId &&
                    task.AssignedWorkerEntityId == worker.EntityId &&
                    task.WarehouseTaskStep == WarehouseTaskStepId.MovingToPickup &&
                    worker.WarehouseWorkerStatus ==
                    WarehouseWorkerStatusId.MovingToPickup,
                "Outbound helper did not assign its single generated task.");
            return task;
        }

        private static void RecoverTimedOutCustomerLoadingTask(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity task,
            GameEntity product)
        {
            int storageSlotIndex = product.ReservedStorageSlotIndex;
            task.ReplaceWarehouseTaskTimeoutRemaining(0f);
            runtime.Systems.Create<TickWarehouseTaskTimeoutSystem>().Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.Blocked &&
                    task.WarehouseTaskBlockReason ==
                    WarehouseTaskBlockReasonId.TimedOut,
                "Outbound timeout did not block the assigned task.");
            runtime.Systems.Create<RecoverBlockedCustomerLoadingTaskSystem>().Execute();
            RequireRecoveredCustomerLoadingTask(
                worker,
                task,
                product,
                storageSlotIndex,
                WarehouseTaskBlockReasonId.TimedOut);
            CompleteBlockedManualHandoffAndRestoreShelf(
                runtime,
                scenario,
                worker,
                task,
                product,
                storageSlotIndex);
        }

        private static void RecoverWorkerMissingCustomerLoadingTask(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity task,
            GameEntity product,
            bool afterPickup)
        {
            int storageSlotIndex = product.ReservedStorageSlotIndex;
            if (afterPickup)
            {
                WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
                runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
                Require(task.WarehouseTaskStep ==
                        WarehouseTaskStepId.MovingToCustomerLoading &&
                        product.hasCarrierEntityId &&
                        product.CarrierEntityId == worker.EntityId,
                    "Post-pickup WorkerMissing setup did not reach carried outbound state.");
            }

            worker.isDestructed = true;
            runtime.Systems.Create<DetectOrphanedWarehouseTaskSystem>().Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.Blocked &&
                    task.WarehouseTaskBlockReason ==
                    WarehouseTaskBlockReasonId.WorkerMissing,
                afterPickup
                    ? "Post-pickup missing worker did not block its outbound task."
                    : "Pre-pickup missing worker did not block its outbound task.");
            runtime.Systems.Create<RecoverBlockedCustomerLoadingTaskSystem>().Execute();
            worker.isDestructed = false;
            worker.isHandsOccupied = false;
            worker.isCarryingProduct = false;
            worker.ReplaceWarehouseWorkerStatus(WarehouseWorkerStatusId.Blocked);
            RequireRecoveredCustomerLoadingTask(
                worker,
                task,
                product,
                storageSlotIndex,
                WarehouseTaskBlockReasonId.WorkerMissing);
            CompleteBlockedManualHandoffAndRestoreShelf(
                runtime,
                scenario,
                worker,
                task,
                product,
                storageSlotIndex);
        }

        private static GameEntity RecoverNoPathAndCompleteManualHandoff(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity task,
            GameEntity visit,
            GameEntity line,
            GameEntity product)
        {
            int storageSlotIndex = product.ReservedStorageSlotIndex;
            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            Require(task.WarehouseTaskStep ==
                    WarehouseTaskStepId.MovingToCustomerLoading &&
                    product.hasCarrierEntityId &&
                    product.CarrierEntityId == worker.EntityId,
                "No-path recovery setup did not pick the outbound product.");
            new ExecuteCustomerLoadingTaskSystem(
                runtime.Game,
                runtime.StaticData,
                new RejectDestinationWorkerNavigationService()).Execute();
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.Blocked &&
                    task.WarehouseTaskBlockReason ==
                    WarehouseTaskBlockReasonId.NoCustomerLoadingPath,
                "Unavailable loading path did not block the carried outbound task.");
            runtime.Systems.Create<RecoverBlockedCustomerLoadingTaskSystem>().Execute();
            RequireRecoveredCustomerLoadingTask(
                worker,
                task,
                product,
                storageSlotIndex,
                WarehouseTaskBlockReasonId.NoCustomerLoadingPath);

            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            Require(product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    scenario.Player.isHandsOccupied,
                "Player did not take the recovered no-path product.");
            runtime.Systems.Create<CleanupBlockedWarehouseTaskSystem>().Execute();
            Require(task.isDestructed &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Manual no-path handoff did not release diagnostic task and worker.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            RequestInteraction(scenario.Player, visit);
            runtime.Systems.Create<LoadHeldProductSystem>().Execute();
            Require(product.isProductLoaded && product.isLoaded &&
                    product.OrderLineEntityId == line.EntityId &&
                    product.hasLoadingSlotIndex &&
                    !scenario.Player.isHandsOccupied,
                "Recovered no-path product did not load through the player handoff.");
            runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
            runtime.Systems.Create<CompleteOrderSystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            return product;
        }

        private static void RequireRecoveredCustomerLoadingTask(
            GameEntity worker,
            GameEntity task,
            GameEntity product,
            int storageSlotIndex,
            WarehouseTaskBlockReasonId expectedReason)
        {
            Require(task.WarehouseTaskStep == WarehouseTaskStepId.Blocked &&
                    task.WarehouseTaskBlockReason == expectedReason &&
                    !task.hasAssignedWorkerEntityId &&
                    !task.hasWarehouseTaskReservedLoadingSlotIndex &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Blocked &&
                    !worker.isHandsOccupied && !worker.isCarryingProduct &&
                    product.isInStock && product.isInteractable &&
                    product.hasStorageSlotIndex &&
                    product.StorageSlotIndex == storageSlotIndex &&
                    !product.hasReservedStorageSlotIndex &&
                    !product.hasReservedOrderLineEntityId &&
                    !product.hasCarrierEntityId,
                $"{expectedReason} recovery did not restore the exact shelf slot and release " +
                "worker/task reservations.");
        }

        private static void CompleteBlockedManualHandoffAndRestoreShelf(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity task,
            GameEntity product,
            int expectedStorageSlotIndex)
        {
            RequestInteraction(scenario.Player, product);
            runtime.Systems.Create<PickUpProductSystem>().Execute();
            Require(product.hasCarrierEntityId &&
                    product.CarrierEntityId == scenario.Player.EntityId &&
                    product.hasReservedStorageSlotIndex &&
                    product.ReservedStorageSlotIndex == expectedStorageSlotIndex &&
                    product.hasReservedOrderLineEntityId,
                "Manual blocked-task handoff did not reserve the recovered shelf product.");
            runtime.Systems.Create<CleanupBlockedWarehouseTaskSystem>().Execute();
            Require(task.isDestructed &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Manual handoff did not clean the blocked task or release its worker.");
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();

            product.RemoveCarrierEntityId();
            scenario.Player.isHandsOccupied = false;
            scenario.Player.isCarryingProduct = false;
            product.RemoveReservedStorageSlotIndex();
            product.RemoveReservedOrderLineEntityId();
            product.AddStorageSlotIndex(expectedStorageSlotIndex);
            product.isInteractable = true;
            product.isProductPlacementDirty = true;
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            Require(product.isInStock && product.hasStorageSlotIndex &&
                    product.StorageSlotIndex == expectedStorageSlotIndex &&
                    !product.hasCarrierEntityId && !product.isProductPlacementDirty &&
                    FindLiveCustomerLoadingTasks(runtime.Game).Length == 0,
                "Smoke handback did not restore a reusable exact shelf state.");
        }

        private static void StockInboundProductManually(
            Runtime runtime,
            Scenario scenario,
            GameEntity product)
        {
            Require(product.isInboundProduct && product.hasDeliverySlotIndex &&
                    product.isInteractable,
                "Manual stocking helper requires an available inbound product.");
            PickUpProduct(runtime, scenario, product);
            RequestInteraction(scenario.Player, scenario.StorageZone);
            runtime.Systems.Create<StoreInboundProductSystem>().Execute();
            Require(product.isProductStocked && product.isInStock &&
                    product.hasStorageSlotIndex && !scenario.Player.isHandsOccupied,
                "Player did not stock the final closing-setup product.");
            runtime.Systems.Create<RegisterStockedProductSystem>().Execute();
            runtime.Systems.Create<CompleteDeliverySystem>().Execute();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
        }

        private static void AdvanceStoreFrom1959ToClosing(
            Runtime runtime,
            Scenario scenario)
        {
            StoreDayConfig config = runtime.StaticData.StoreDay;
            scenario.Store.ReplaceCurrentDayMinute(19 * 60 + 59);
            float secondsPerMinute = config.DayDurationSeconds /
                                     (config.ClosingMinute - config.StartMinute);
            new TickStoreDayClockSystem(
                runtime.Game,
                runtime.StaticData,
                new FixedTimeService(secondsPerMinute)).Execute();
            runtime.Systems.Create<ReachStoreClosingTimeSystem>().Execute();
            Require(scenario.Store.isStoreClosing && !scenario.Store.isStoreOpen &&
                    scenario.Store.CurrentDayMinute == config.ClosingMinute &&
                    !scenario.Store.hasCustomerCooldownRemaining,
                "19:59 -> 20:00 did not enter Closing without scheduling customers.");
            CleanupEvents(runtime);
        }

        private static GameEntity CompleteOneCustomerLoadingTask(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            GameEntity visit,
            GameEntity line,
            bool expectCompleted)
        {
            Require(scenario.Store.isStoreClosing &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Closing outbound helper requires an idle active-shift worker.");
            GameEntity task = CreateAssignedCustomerLoadingTask(
                runtime,
                worker,
                visit);
            GameEntity product = runtime.Game.GetEntityWithEntityId(
                task.WarehouseTaskProductEntityId);
            WarpWarehouseWorker(worker, worker.WarehouseWorkerStoragePosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            WarpWarehouseWorker(worker,
                worker.WarehouseWorkerCustomerLoadingPosition);
            runtime.Systems.Create<ExecuteCustomerLoadingTaskSystem>().Execute();
            Require(task.isDestructed && product.isProductLoaded &&
                    product.isLoaded && product.OrderLineEntityId == line.EntityId &&
                    product.hasLoadingSlotIndex &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle,
                "Closing outbound task did not pick and load its exact product.");
            runtime.Systems.Create<RegisterLoadedProductSystem>().Execute();
            runtime.Systems.Create<CompleteOrderSystem>().Execute();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            ExecuteProductPlacement(runtime);
            ExecuteStorageState(runtime);
            CleanupEvents(runtime);
            Require(visit.isCustomerVisitCompleted == expectCompleted &&
                    visit.isCustomerVisitLoading != expectCompleted &&
                    !product.isProductLoaded,
                expectCompleted
                    ? "Final closing outbound task did not complete the order."
                    : "First closing outbound task completed the order too early.");
            return product;
        }

        private static void ValidateWarehouseWorkerDayTwoWage(
            Runtime runtime,
            Scenario scenario,
            GameEntity worker,
            int expectedDayOneClosingMoney)
        {
            EnterWarehouseWorkerReportSmokeClosing(scenario);
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(scenario.Store.isDayReportOpen && scenario.Player.isModalOpen,
                "Idle worker prevented the Day 1 report.");
            CleanupEvents(runtime);

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<StoreDayFeature>().Execute();
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();
            runtime.Systems.Create<SyncWarehouseWorkerShiftSystem>().Execute();
            Require(scenario.Store.DayNumber == 2 && scenario.Store.isStorePreparing &&
                    scenario.Store.Money == expectedDayOneClosingMoney &&
                    scenario.Store.DayOpeningBalance == expectedDayOneClosingMoney &&
                    scenario.Store.DayPayrollExpenses == 0 &&
                    !worker.isWorkerShiftActive &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.OffShift &&
                    worker.WorkerPaidDayNumber == 1,
                "Starting Day 2 did not retain the employee off shift with a reset payroll ledger.");

            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenStoreSystem>().Execute();
            CleanupEvents(runtime);
            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);

            int solventMoney = scenario.Store.Money;
            int solventOpeningBalance = scenario.Store.DayOpeningBalance;
            int solventProjectSequence = scenario.Store.NextProjectSequenceIndex;
            int unsafeProjectSequence = runtime.StaticData.ProjectTypes
                .Select((projectType, index) => (projectType, index))
                .Single(pair =>
                    pair.projectType == CustomerProjectTypeId.LumberShelving)
                .index;
            int dailyWage = runtime.StaticData.WarehouseWorker.DailyWage;
            scenario.Store.ReplaceNextProjectSequenceIndex(unsafeProjectSequence);
            scenario.Store.ReplaceMoney(dailyWage);
            scenario.Store.ReplaceDayOpeningBalance(dailyWage);
            EconomyDebitEvaluation unsafeWage = runtime.EconomySolvency.EvaluateDebit(
                scenario.Store.EntityId,
                dailyWage);
            ExecuteInteractionPrompts(runtime);
            Require(unsafeWage.Availability ==
                    EconomyDebitAvailability.DemandWouldBecomeInsolvent &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptWarehouseWorkerWageWouldBlockProjects)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "Underfunded Day 2 did not expose the unsafe wage rejection.");
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<PayWarehouseWorkerShiftSystem>().Execute();
            Require(scenario.Store.Money == dailyWage &&
                    scenario.Store.DayOpeningBalance == dailyWage &&
                    scenario.Store.DayPayrollExpenses == 0 &&
                    !worker.isWorkerShiftActive &&
                    worker.WorkerPaidDayNumber == 1 &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.OffShift &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "Unsafe Day 2 wage mutated money, payroll, shift or events.");
            CleanupEvents(runtime);

            scenario.Store.ReplaceNextProjectSequenceIndex(solventProjectSequence);
            scenario.Store.ReplaceMoney(solventMoney);
            scenario.Store.ReplaceDayOpeningBalance(solventOpeningBalance);
            Require(runtime.EconomySolvency.EvaluateDebit(
                        scenario.Store.EntityId,
                        dailyWage).CanDebit,
                "Restored Day 2 finances did not make the employee shift solvent.");
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptPayWarehouseWorkerShift,
                            dailyWage)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "Day 2 terminal did not offer the unpaid worker shift.");

            int moneyBeforeWage = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<PayWarehouseWorkerShiftSystem>().Execute();
            Require(worker.isWorkerShiftActive &&
                    worker.WorkerPaidDayNumber == scenario.Store.DayNumber &&
                    worker.WarehouseWorkerStatus == WarehouseWorkerStatusId.Idle &&
                    scenario.Store.Money == moneyBeforeWage -
                    dailyWage &&
                    scenario.Store.DayPayrollExpenses ==
                    dailyWage,
                "Day 2 wage did not activate one paid shift and update payroll once.");
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationWarehouseWorkerShiftPaid);
            CleanupEvents(runtime);

            int moneyAfterWage = scenario.Store.Money;
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<PayWarehouseWorkerShiftSystem>().Execute();
            Require(scenario.Store.Money == moneyAfterWage &&
                    scenario.Store.DayPayrollExpenses ==
                    dailyWage &&
                    worker.WorkerPaidDayNumber == scenario.Store.DayNumber,
                "Repeated Day 2 wage request debited the same shift twice.");
            CleanupEvents(runtime);
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            runtime.Systems.Create<ValidateWarehouseWorkerStateSystem>().Execute();
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
                GameMatcher.StoreControlTerminalEntityId,
                GameMatcher.NextProjectSequenceIndex,
                GameMatcher.DayNumber,
                GameMatcher.CurrentDayMinute,
                GameMatcher.DayOpeningBalance,
                GameMatcher.DayRevenue,
                GameMatcher.DayProcurementExpenses,
                GameMatcher.DayUpgradeExpenses,
                GameMatcher.DayPayrollExpenses,
                GameMatcher.DayCompletedOrderCount,
                GameMatcher.DayLostCustomerCount,
                GameMatcher.StorePreparing,
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
            GameEntity storeControlTerminal = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.StoreControlTerminal,
                    GameMatcher.StoreEntityId,
                    GameMatcher.View,
                    GameMatcher.InteractionView)), "store control terminal");
            InputEntity input = RequireSingle(
                runtime.Input.GetGroup(InputMatcher.InputState),
                "input state");

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
            Require(store.StoreControlTerminalEntityId ==
                    storeControlTerminal.EntityId &&
                    storeControlTerminal.StoreEntityId == store.EntityId,
                "The store and control terminal relations are inconsistent.");
            GameEntity[] parkingSpots = runtime.Game
                .GetEntitiesWithCustomerParkingSpotStoreEntityId(store.EntityId)
                .OrderBy(spot => spot.ParkingSpotIndex)
                .ToArray();
            GameEntity[] queueSpots = runtime.Game
                .GetEntitiesWithCustomerQueueSpotStoreEntityId(store.EntityId)
                .OrderBy(spot => spot.QueueSpotIndex)
                .ToArray();
            GameEntity loadingBay = runtime.Game
                .GetEntityWithCustomerLoadingBayStoreEntityId(store.EntityId);
            GameEntity trafficLane = runtime.Game
                .GetEntityWithCustomerTrafficLaneStoreEntityId(store.EntityId);
            Require(parkingSpots.Length == runtime.StaticData.CustomerFlow.ParkingCapacity &&
                    queueSpots.Length == runtime.StaticData.CustomerFlow.ParkingCapacity &&
                    parkingSpots.Select(spot => spot.ParkingSpotIndex)
                        .SequenceEqual(Enumerable.Range(0, parkingSpots.Length)) &&
                    queueSpots.Select(spot => spot.QueueSpotIndex)
                        .SequenceEqual(Enumerable.Range(0, queueSpots.Length)) &&
                    loadingBay != null && loadingBay.isCustomerLoadingBay &&
                    trafficLane != null && trafficLane.isCustomerTrafficLane,
                "The store does not expose the configured parking, FIFO queue, loading bay " +
                "and shared traffic lane resources.");
            Pose[] expectedQueueAbandonExitRoute =
            {
                new(new Vector3(-8f, 0.02f, 0.55f),
                    Quaternion.Euler(0f, 180f, 0f)),
                new(new Vector3(-8f, 0.02f, -0.75f),
                    Quaternion.Euler(0f, 180f, 0f)),
                new(new Vector3(-8f, 0.02f, -2.05f),
                    Quaternion.Euler(0f, 180f, 0f)),
                new(new Vector3(-8f, 0.02f, -3f),
                    Quaternion.Euler(0f, 180f, 0f))
            };
            foreach (GameEntity queueSpot in queueSpots)
            {
                Pose[] expectedSlice = expectedQueueAbandonExitRoute
                    .Skip(queueSpot.QueueSpotIndex)
                    .ToArray();
                Require(queueSpot.hasCustomerQueueAbandonRoute &&
                        queueSpot.CustomerQueueAbandonRoute.Length ==
                        expectedSlice.Length &&
                        queueSpot.CustomerQueueAbandonRoute
                            .Select((pose, index) => PoseMatches(
                                pose,
                                expectedSlice[index]))
                            .All(matches => matches),
                    $"Queue spot {queueSpot.QueueSpotIndex} did not materialize the exact " +
                    "authored abandonment-route slice.");
            }
            Require(!ReferenceEquals(
                        queueSpots[0].CustomerQueueAbandonRoute,
                        queueSpots[1].CustomerQueueAbandonRoute) &&
                    !ReferenceEquals(
                        queueSpots[0].CustomerQueueAbandonRoute,
                        queueSpots[2].CustomerQueueAbandonRoute) &&
                    !ReferenceEquals(
                        queueSpots[1].CustomerQueueAbandonRoute,
                        queueSpots[2].CustomerQueueAbandonRoute),
                "Queue spots must own independent abandonment-route slices.");
            foreach (GameEntity parkingSpot in parkingSpots)
            {
                float parkingX = new[] { -10.8f, -7.4f, -4f }[
                    parkingSpot.ParkingSpotIndex];
                Pose[] expectedParkingDepartureRoute =
                {
                    new(new Vector3(parkingX, 0.02f, -21.5f), Quaternion.identity),
                    new(new Vector3(parkingX, 0.02f, -26.5f), Quaternion.identity),
                    new(new Vector3(parkingX, 0.02f, -30f), Quaternion.identity),
                    new(new Vector3(1.5f, 0.02f, -30f),
                        Quaternion.Euler(0f, 90f, 0f)),
                    new(new Vector3(1.5f, 0.02f, -35f),
                        Quaternion.Euler(0f, 180f, 0f))
                };
                Require(parkingSpot.hasCustomerVehicleParkingDepartureRoute &&
                        parkingSpot.CustomerVehicleParkingDepartureRoute.Length ==
                        expectedParkingDepartureRoute.Length &&
                        parkingSpot.CustomerVehicleParkingDepartureRoute
                            .Select((pose, index) => PoseMatches(
                                pose,
                                expectedParkingDepartureRoute[index]))
                            .All(matches => matches),
                    $"Parking spot {parkingSpot.ParkingSpotIndex} did not materialize its " +
                    "exact impatient-customer vehicle departure route.");
            }
            Require(!orderCounter.hasSceneViewKey &&
                    !procurementTerminal.hasSceneViewKey &&
                    !storageZone.hasSceneViewKey &&
                    !trolleyUpgradeTerminal.hasSceneViewKey &&
                    !storeControlTerminal.hasSceneViewKey,
                "SceneViewKey binder did not consume all static scene-view requests.");
            Require(HasNoCustomerVisits(runtime.Game, store.EntityId) &&
                    !store.hasCustomerCooldownRemaining &&
                    store.isStorePreparing && !store.isStoreOpen &&
                    !store.isStoreClosing && !store.isDayReportOpen &&
                    store.DayNumber == 1 &&
                    Mathf.Approximately(
                        store.CurrentDayMinute,
                        runtime.StaticData.StoreDay.StartMinute) &&
                    store.DayOpeningBalance == store.Money &&
                    store.DayRevenue == 0 &&
                    store.DayProcurementExpenses == 0 &&
                    store.DayUpgradeExpenses == 0 &&
                    store.DayPayrollExpenses == 0 &&
                    store.DayCompletedOrderCount == 0 &&
                    store.DayLostCustomerCount == 0,
                "The smoke test must start on Day 1 in the 08:00 preparation phase " +
                "with a zeroed ledger and no customer schedule.");
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
                storeControlTerminal,
                input);
        }

        private static void PrepareClosingVisualState(Runtime runtime, Scenario scenario)
        {
            Require(scenario.Store.isStorePreparing &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
                "Closing visual preparation requires a fresh preparing store.");
            scenario.Store.isStorePreparing = false;
            scenario.Store.isStoreClosing = true;
            scenario.Store.ReplaceCurrentDayMinute(
                runtime.StaticData.StoreDay.ClosingMinute);
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
        }

        private static void ValidateDayClockLayoutAt1280x720(Runtime runtime)
        {
            const float viewportWidth = 1280f;
            const float viewportHeight = 720f;
            float scale = Mathf.Max(
                0.65f,
                Mathf.Min(viewportWidth / 1600f, viewportHeight / 900f));
            float canvasWidth = viewportWidth / scale;
            float canvasHeight = viewportHeight / scale;
            float panelWidth = Mathf.Min(420f, canvasWidth - 48f);
            const float panelY = 24f;
            const float phaseTopOffset = 44f;
            const float horizontalPadding = 16f;
            string phaseText = runtime.Localization.Resolve(
                LocalizedTexts.StoreDayPhase(StoreDayPhase.Closing));
            var promptStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            float phaseHeight = Mathf.Max(
                28f,
                promptStyle.CalcHeight(
                    new GUIContent(phaseText),
                    panelWidth - horizontalPadding * 2f));
            float panelHeight = phaseTopOffset + phaseHeight + 10f;
            Rect panel = new(
                canvasWidth - panelWidth - 24f,
                panelY,
                panelWidth,
                panelHeight);
            Rect phase = new(
                panel.x + horizontalPadding,
                panel.y + phaseTopOffset,
                panel.width - horizontalPadding * 2f,
                phaseHeight);
            string customerFlowText = runtime.Localization.Resolve(
                LocalizedTexts.Text(
                    LocalizationKey.HudCustomerFlow,
                    3,
                    3,
                    0,
                    0));
            var customerFlowStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            Rect statusPanel = new(24f, 24f, 740f, 212f);
            Rect customerFlowLabel = new(42f, 106f, 700f, 46f);
            float customerFlowHeight = customerFlowStyle.CalcHeight(
                new GUIContent(customerFlowText),
                customerFlowLabel.width);
            float reportPanelWidth = Mathf.Min(780f, canvasWidth - 48f);
            float reportPanelHeight = Mathf.Min(650f, canvasHeight - 64f);
            Rect reportPanel = new(
                (canvasWidth - reportPanelWidth) * 0.5f,
                (canvasHeight - reportPanelHeight) * 0.5f,
                reportPanelWidth,
                reportPanelHeight);
            Rect lostCustomerRow = new(
                reportPanel.x + 52f,
                reportPanel.y + 124f,
                reportPanel.width - 104f,
                32f);

            Require(Mathf.Approximately(scale, 0.8f) &&
                    Mathf.Approximately(canvasWidth, 1600f) &&
                    Mathf.Approximately(canvasHeight, 900f) &&
                    phaseText == "НОВЫЕ КЛИЕНТЫ БОЛЬШЕ НЕ ПРИЕДУТ" &&
                    phaseHeight >= 28f &&
                    panel.x >= 0f && panel.y >= 0f &&
                    panel.xMax <= canvasWidth - 24f &&
                    panel.yMax <= canvasHeight &&
                    phase.x >= panel.x && phase.y >= panel.y &&
                    phase.xMax <= panel.xMax && phase.yMax <= panel.yMax &&
                    !customerFlowText.Contains("\n", StringComparison.Ordinal) &&
                    !customerFlowText.Contains(" с", StringComparison.Ordinal) &&
                    customerFlowHeight <= customerFlowLabel.height &&
                    statusPanel.Contains(customerFlowLabel.min) &&
                    statusPanel.Contains(customerFlowLabel.max) &&
                    !statusPanel.Overlaps(panel) &&
                    reportPanel.x >= 0f && reportPanel.y >= 0f &&
                    reportPanel.xMax <= canvasWidth &&
                    reportPanel.yMax <= canvasHeight &&
                    reportPanel.Contains(lostCustomerRow.min) &&
                    reportPanel.Contains(lostCustomerRow.max),
                "The closing phase, countdown-free customer HUD or lost-customer report " +
                "row does not fit at 1280x720.");
        }

        private static void ValidateNewDayFadeContract()
        {
            DayClockSnapshot report = new(1, 20 * 60, StoreDayPhase.Report);
            DayClockSnapshot nextPreparation = new(2, 8 * 60, StoreDayPhase.Preparing);
            Require(PrototypeHudView.ShouldStartNewDayFade(report, nextPreparation) &&
                    !PrototypeHudView.ShouldStartNewDayFade(
                        new DayClockSnapshot(1, 20 * 60, StoreDayPhase.Closing),
                        nextPreparation) &&
                    !PrototypeHudView.ShouldStartNewDayFade(
                        report,
                        new DayClockSnapshot(1, 8 * 60, StoreDayPhase.Preparing)) &&
                    !PrototypeHudView.ShouldStartNewDayFade(
                        report,
                        new DayClockSnapshot(3, 8 * 60, StoreDayPhase.Preparing)) &&
                    !PrototypeHudView.ShouldStartNewDayFade(
                        nextPreparation,
                        nextPreparation),
                "The new-day fade must start only for Report N -> Preparing N+1.");

            Require(Mathf.Approximately(
                        PrototypeHudView.EvaluateNewDayFadeAlpha(0f), 1f) &&
                    Mathf.Approximately(
                        PrototypeHudView.EvaluateNewDayFadeAlpha(
                            PrototypeHudView.NewDayFadeHoldSeconds), 1f) &&
                    Mathf.Approximately(
                        PrototypeHudView.EvaluateNewDayFadeAlpha(0.6f), 0.5f) &&
                    Mathf.Approximately(
                        PrototypeHudView.EvaluateNewDayFadeAlpha(
                            PrototypeHudView.NewDayFadeHoldSeconds +
                            PrototypeHudView.NewDayFadeOutSeconds), 0f) &&
                    Mathf.Approximately(
                        PrototypeHudView.EvaluateNewDayFadeAlpha(10f), 0f),
                "The new-day fade must hold black for 0.2 seconds and fade to clear by 1.0.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => PrototypeHudView.EvaluateNewDayFadeAlpha(-0.001f),
                "The fade accepted negative elapsed time.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => PrototypeHudView.EvaluateNewDayFadeAlpha(float.NaN),
                "The fade accepted NaN elapsed time.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => PrototypeHudView.EvaluateNewDayFadeAlpha(float.PositiveInfinity),
                "The fade accepted infinite elapsed time.");
        }

        private static void ValidatePreparingPresentationAndFrozenClock(
            Runtime runtime,
            Scenario scenario)
        {
            var capture = new CaptureHudService();
            new PresentHudSystem(runtime.Game, runtime.StaticData, capture).Execute();
            Require(capture.Hud.HasValue &&
                    capture.Hud.Value.DayClock.DayNumber == 1 &&
                    capture.Hud.Value.DayClock.CurrentDayMinute ==
                    runtime.StaticData.StoreDay.StartMinute &&
                    capture.Hud.Value.DayClock.Phase == StoreDayPhase.Preparing,
                "The morning HUD did not present Day 1, 08:00 and preparation state.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptOpenStore)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "The store control terminal did not offer E to open the preparing store.");

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCounterOpenStoreAtControlTerminal)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The preparing order counter did not direct the player to the control terminal.");

            new TickStoreDayClockSystem(
                runtime.Game,
                runtime.StaticData,
                new FixedTimeService(480f)).Execute();
            runtime.Systems.Create<TickCustomerCooldownSystem>().Execute();
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(Mathf.Approximately(
                        scenario.Store.CurrentDayMinute,
                        runtime.StaticData.StoreDay.StartMinute) &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId) &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The store clock advanced or a customer spawned before opening.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void OpenStoreForSmoke(Runtime runtime, Scenario scenario)
        {
            Require(scenario.Store.isStorePreparing &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
                "Only a fresh preparing store can be opened by the smoke test.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(LocalizationKey.PromptOpenStore)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "The preparing store control terminal is not actionable.");

            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 1,
                "E did not emit one store-opening request.");
            runtime.Systems.Create<OpenStoreSystem>().Execute();
            RequireNotificationKey(runtime, LocalizationKey.NotificationStoreOpened);
            float firstDelay = runtime.StaticData.CustomerFlow.FirstArrivalDelay;
            Require(scenario.Store.isStoreOpen &&
                    !scenario.Store.isStorePreparing &&
                    !scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    scenario.Store.hasCustomerCooldownRemaining &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        firstDelay) &&
                    Mathf.Approximately(
                        scenario.Store.CurrentDayMinute,
                        runtime.StaticData.StoreDay.StartMinute),
                "E did not open the store with exactly the configured first-customer delay.");

            runtime.Systems.Create<OpenStoreSystem>().Execute();
            Require(Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        firstDelay) &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 1,
                "One store-opening request scheduled or notified more than once.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            CleanupEvents(runtime);
            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
        }

        private static void ReachClosingTimeWithActiveCustomer(
            Runtime runtime,
            Scenario scenario,
            GameEntity activeVisit)
        {
            Require(scenario.Store.isStoreOpen &&
                    !scenario.Store.isStoreClosing &&
                    FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .Contains(activeVisit),
                "Closing-time smoke requires one active customer in an open store.");

            new TickStoreDayClockSystem(
                runtime.Game,
                runtime.StaticData,
                new FixedTimeService(
                    runtime.StaticData.StoreDay.DayDurationSeconds)).Execute();
            runtime.Systems.Create<ReachStoreClosingTimeSystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationStoreClosingTime);
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isStoreOpen &&
                    !scenario.Store.isStorePreparing &&
                    !scenario.Store.isDayReportOpen &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    Mathf.Approximately(
                        scenario.Store.CurrentDayMinute,
                        runtime.StaticData.StoreDay.ClosingMinute) &&
                    FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .Contains(activeVisit),
                "The 480-second day did not clamp to 20:00 while preserving its active customer.");

            runtime.Systems.Create<ReachStoreClosingTimeSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 1,
                "Closing time emitted more than one notification.");
            new TickStoreDayClockSystem(
                runtime.Game,
                runtime.StaticData,
                new FixedTimeService(480f)).Execute();
            Require(Mathf.Approximately(
                    scenario.Store.CurrentDayMinute,
                    runtime.StaticData.StoreDay.ClosingMinute),
                "The day clock advanced after the store entered closing state.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCloseStoreCustomerActive,
                            FindCustomerVisits(
                                runtime.Game,
                                scenario.Store.EntityId).Length)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The closing terminal did not explain that the active customer must finish.");
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasDayReportStoreEntityId,
                "The report opened before the active customer completed their visit.");
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .Contains(activeVisit) &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 1,
                "Closing time spawned another customer while the existing visit continued.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            CleanupEvents(runtime);
            if (scenario.Player.hasFocusedEntityId)
                scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
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

        private static CustomerVisit[] PrepareThreeCustomerQueue(
            Runtime runtime,
            Scenario scenario)
        {
            Require(runtime.StaticData.CustomerFlow.ParkingCapacity == 3 &&
                    Mathf.Approximately(
                        runtime.StaticData.CustomerFlow.FirstArrivalDelay,
                        10f) &&
                    Mathf.Approximately(
                        runtime.CustomerArrivalSchedule.GetDelay(8 * 60),
                        45f) &&
                    Mathf.Approximately(
                        runtime.CustomerArrivalSchedule.GetDelay(9 * 60),
                        40.5f) &&
                    Mathf.Approximately(
                        runtime.CustomerArrivalSchedule.GetDelay(13 * 60),
                        26f) &&
                    Mathf.Approximately(
                        runtime.CustomerArrivalSchedule.GetDelay(20 * 60),
                        70f),
                "Customer flow config does not expose the frozen morning, midday and closing " +
                "arrival schedule.");

            CustomerVisit first = SpawnCustomerToQueue(
                runtime,
                scenario,
                promoteAtCounter: true);
            CustomerVisit second = SpawnCustomerToQueue(
                runtime,
                scenario,
                promoteAtCounter: false);
            CustomerVisit third = SpawnCustomerToQueue(
                runtime,
                scenario,
                promoteAtCounter: false);
            CustomerVisit[] visits = { first, second, third };

            int[] parkingReservations = visits
                .Select(visit => visit.Entity.ReservedCustomerParkingSpotEntityId)
                .ToArray();
            int[] queueReservations = visits
                .Select(visit => visit.Actor.ReservedCustomerQueueSpotEntityId)
                .ToArray();
            Require(visits.Select(visit => visit.Entity.CustomerArrivalSequence)
                        .SequenceEqual(new[] { 0, 1, 2 }) &&
                    parkingReservations.Distinct().Count() == visits.Length &&
                    queueReservations.Distinct().Count() == visits.Length &&
                    visits.Select(visit => QueueSpotIndex(runtime.Game, visit.Actor))
                        .SequenceEqual(new[] { 0, 1, 2 }) &&
                    first.Entity.isCustomerVisitConsulting &&
                    second.Entity.isCustomerVisitQueued &&
                    third.Entity.isCustomerVisitQueued &&
                    runtime.Game.GetEntityWithReservedCustomerTrafficLaneEntityId(
                        runtime.Game.GetEntityWithCustomerTrafficLaneStoreEntityId(
                            scenario.Store.EntityId).EntityId) == null &&
                    runtime.Game.GetEntityWithReservedCustomerLoadingBayEntityId(
                        runtime.Game.GetEntityWithCustomerLoadingBayStoreEntityId(
                            scenario.Store.EntityId).EntityId) == null,
                "Three customers did not retain unique parking/queue reservations in FIFO " +
                "arrival order.");
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
            return visits;
        }

        private static int QueueSpotIndex(GameContext gameContext, GameEntity actor)
        {
            Require(actor != null && actor.isCustomer &&
                    actor.hasReservedCustomerQueueSpotEntityId,
                "Customer actor has no queue reservation.");
            GameEntity queueSpot = gameContext.GetEntityWithEntityId(
                actor.ReservedCustomerQueueSpotEntityId);
            Require(queueSpot != null && queueSpot.isCustomerQueueSpot &&
                    queueSpot.hasQueueSpotIndex,
                "Customer actor references an invalid queue spot.");
            return queueSpot.QueueSpotIndex;
        }

        private static void ValidateQueueSnapshot(
            Runtime runtime,
            int total,
            int queued,
            int consulting,
            int loadingPipeline,
            int leaving)
        {
            var capture = new CaptureHudService();
            new PresentHudSystem(runtime.Game, runtime.StaticData, capture).Execute();
            Require(capture.Hud.HasValue,
                "Customer queue HUD did not publish a snapshot.");
            CustomerFlowSnapshot flow = capture.Hud.Value.CustomerFlow;
            Require(flow.TotalActiveCount == total &&
                    flow.QueuedCount == queued &&
                    flow.ConsultingCount == consulting &&
                    flow.LoadingPipelineCount == loadingPipeline &&
                    flow.LeavingCount == leaving &&
                    flow.ArrivingCount ==
                    total - queued - consulting - loadingPipeline - leaving,
                "Customer queue HUD categories do not account for every active visit.");
        }

        private static CustomerVisit SpawnAndParkCustomer(Runtime runtime, Scenario scenario)
        {
            Require(HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId) &&
                    scenario.Store.hasCustomerCooldownRemaining &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The sequential customer smoke requires an empty queue.");

            return SpawnCustomerToQueue(runtime, scenario, promoteAtCounter: true);
        }

        private static CustomerVisit SpawnCustomerToQueue(
            Runtime runtime,
            Scenario scenario,
            bool promoteAtCounter)
        {
            Require(scenario.Store.isStoreOpen &&
                    scenario.Store.hasCustomerCooldownRemaining,
                "A customer can only spawn from an open scheduled store.");

            int[] previousVisitIds = FindCustomerVisits(
                    runtime.Game,
                    scenario.Store.EntityId)
                .Select(existing => existing.EntityId)
                .ToArray();
            float expectedNextDelay = runtime.CustomerArrivalSchedule.GetDelay(
                scenario.Store.CurrentDayMinute);

            scenario.Store.ReplaceCustomerCooldownRemaining(0f);
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            GameEntity[] spawnedVisits = FindCustomerVisits(
                    runtime.Game,
                    scenario.Store.EntityId)
                .Where(candidate => !previousVisitIds.Contains(candidate.EntityId))
                .ToArray();
            Require(spawnedVisits.Length == 1,
                $"Ready store created {spawnedVisits.Length} customer visits instead of one.");
            GameEntity visit = spawnedVisits[0];
            Require(
                    visit.hasCustomerVisitStoreEntityId &&
                    visit.CustomerVisitStoreEntityId == scenario.Store.EntityId &&
                    scenario.Store.hasCustomerCooldownRemaining &&
                    Mathf.Approximately(
                        scenario.Store.CustomerCooldownRemaining,
                        expectedNextDelay),
                "The ready store did not create a customer visit.");
            Require(visit.isCustomerVisit && visit.isCustomerVehicle && !visit.isOrder &&
                    visit.isLoadingZone && visit.isCustomerVisitArriving &&
                    visit.hasCustomerArrivalSequence &&
                    visit.hasReservedCustomerParkingSpotEntityId &&
                    visit.hasReservedCustomerTrafficLaneEntityId &&
                    visit.hasCustomerProjectType &&
                    visit.hasCustomerPatienceRemaining &&
                    Mathf.Approximately(
                        visit.CustomerPatienceRemaining,
                        runtime.StaticData.CustomerFlow.DefaultPatienceDuration) &&
                    !visit.isCustomerPatienceWarningIssued &&
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
            Require(visit.isCustomerVisitQueued &&
                    !visit.isCustomerVisitArriving &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.isInteractable &&
                    visit.hasReservedCustomerParkingSpotEntityId &&
                    !visit.hasReservedCustomerTrafficLaneEntityId &&
                    !visit.isRouteCompleted &&
                    !visit.hasRoute &&
                    !visit.hasRouteWaypointIndex,
                "The parked vehicle did not release the lane and join the customer queue.");

            if (previousVisitIds.Length == 0)
            {
                scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
                ExecuteInteractionPrompts(runtime);
                Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                                LocalizationKey.PromptCounterNextCustomerApproaching,
                                1)) &&
                        !scenario.Player.isFocusInteractionAvailable,
                    "The order counter did not remain unavailable while the queue head " +
                    "approached it.");
                scenario.Player.RemoveFocusedEntityId();
                ExecuteInteractionPrompts(runtime);
            }

            GameEntity actor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            Require(actor != null &&
                    actor.EntityId != visit.EntityId &&
                    actor.isCustomer &&
                    actor.isCustomerApproachingCounter &&
                    !actor.isCustomerWaitingInQueue &&
                    !actor.isCustomerWaitingAtCounter &&
                    !actor.isCustomerReturningToVehicle &&
                    actor.isRouteMover &&
                    actor.CustomerActorVisitEntityId == visit.EntityId &&
                    actor.hasReservedCustomerQueueSpotEntityId &&
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
            RequireCustomerDissatisfactionVisual(
                actor,
                expectedDissatisfied: false,
                "A freshly bound customer actor did not begin with neutral color, arms and " +
                "hidden mood label.");

            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerApproachSystem>().Execute();
            ExecuteStorageState(runtime);
            Require(visit.isCustomerVisitQueued &&
                    !visit.isOrder &&
                    !visit.isRouteCompleted &&
                    !visit.hasRoute &&
                    !visit.hasRouteWaypointIndex &&
                    !visit.isInteractable,
                "The customer visit left its parked queued state during the walk.");
            Require(actor.isCustomerWaitingInQueue &&
                    !actor.isCustomerApproachingCounter &&
                    !actor.isCustomerWaitingAtCounter &&
                    !actor.isCustomerReturningToVehicle &&
                    !actor.isRouteCompleted &&
                    !actor.hasRoute &&
                    !actor.hasRouteWaypointIndex &&
                    actor.hasCustomerActorVisitEntityId &&
                    actor.CustomerActorVisitEntityId == visit.EntityId &&
                    !actor.isDestructed,
                "The customer actor did not enter its reserved FIFO queue spot.");

            if (promoteAtCounter)
            {
                runtime.Systems.Create<PromoteCustomerAtCounterSystem>().Execute();
                Require(visit.isCustomerVisitConsulting &&
                        !visit.isCustomerVisitQueued &&
                        visit.hasServingOrderCounterEntityId &&
                        visit.ServingOrderCounterEntityId ==
                        scenario.OrderCounter.EntityId &&
                        actor.isCustomerWaitingAtCounter &&
                        !actor.isCustomerWaitingInQueue,
                    "The FIFO head did not claim the free order counter.");
            }

            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
            ValidateConsultationLineAvailability(runtime, scenario, visit);

            return new CustomerVisit(visit, view, actor, actorView);
        }

        private static void OpenConsultation(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            Require(visit.isCustomerVisitConsulting && !visit.isOrder &&
                    visit.hasCustomerPatienceRemaining &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId,
                "Only a closed pre-order consultation can be opened.");
            int[] offerIds = GetConsultationOffers(runtime.Game, visit)
                .Select(offer => offer.EntityId)
                .ToArray();

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(scenario.Player.isFocusInteractionAvailable &&
                    PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptDiscussProject,
                            LocalizedTexts.ProjectTitle(visit.CustomerProjectType))),
                "The consulting customer does not expose its localized project at the order " +
                "counter.");
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
            CustomerProjectOfferDefinition expectedOffer,
            bool advanceToLoadingBay = true)
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
            runtime.Systems.Create<FinalizeAcceptedCustomerPatienceSystem>().Execute();
            runtime.Systems.Create<PresentCustomerDissatisfactionSystem>().Execute();

            GameEntity[] orderLines = GetOrderLines(runtime.Game, visit);
            GameEntity returningActor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);

            Require(!scenario.Player.isModalOpen &&
                    !scenario.Player.hasConsultationVisitEntityId &&
                    !scenario.Player.hasProcurementTerminalEntityId &&
                    visit.isOrder &&
                    visit.isCustomerVisitReturning &&
                    !visit.isCustomerVisitConsulting &&
                    !visit.hasCustomerPatienceRemaining &&
                    !visit.isCustomerPatienceWarningIssued &&
                    !visit.hasServingOrderCounterEntityId &&
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
                "Confirming an offer did not create its order graph and release the service " +
                "counter in the same Enter action.");
            Require(returningActor != null &&
                    returningActor.isCustomerWaitingAtCounter &&
                    !returningActor.hasReservedCustomerQueueSpotEntityId,
                "Confirming an offer did not release the actor's FIFO queue reservation.");
            RequireCustomerDissatisfactionVisual(
                returningActor,
                expectedDissatisfied: false,
                "Accepting a dissatisfied customer did not immediately restore its neutral " +
                "color, arms and hidden world label.");
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
            int[] survivingOfferIds = offerIds
                .Where(id => runtime.Game.GetEntityWithEntityId(id) != null)
                .ToArray();
            int[] survivingOfferLineIds = offerLineIds
                .Where(id => runtime.Game.GetEntityWithEntityId(id) != null)
                .ToArray();
            int visitOfferRelationCount = runtime.Game
                .GetEntitiesWithConsultationOfferVisitEntityId(visit.EntityId)
                .Count;
            int survivingLineRelationCount = offerIds.Sum(id => runtime.Game
                .GetEntitiesWithConsultationOfferEntityId(id)
                .Count);
            Require(survivingOfferIds.Length == 0 &&
                    survivingOfferLineIds.Length == 0 &&
                    visitOfferRelationCount == 0 &&
                    survivingLineRelationCount == 0 &&
                    GetConsultationOffers(runtime.Game, visit).Length == 0,
                $"Confirmed consultation offers survived the Destructed cleanup pipeline: " +
                $"offers=[{string.Join(",", survivingOfferIds)}], " +
                $"lines=[{string.Join(",", survivingOfferLineIds)}], " +
                $"visit relations={visitOfferRelationCount}, " +
                $"line relations={survivingLineRelationCount}.");

            if (advanceToLoadingBay)
                AdvanceAcceptedCustomerToLoadingBay(runtime, scenario, visit);
            return orderLines;
        }

        private static void AdvanceAcceptedCustomerToLoadingBay(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            GameEntity actor =
                runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId);
            Require(visit.isCustomerVisitReturning && actor != null &&
                    actor.isCustomerWaitingAtCounter &&
                    !actor.hasReservedCustomerQueueSpotEntityId,
                "Accepted customer must start from the released service point.");

            runtime.Systems.Create<ReserveCustomerLoadingBaySystem>().Execute();
            Require(visit.hasReservedCustomerLoadingBayEntityId,
                "The oldest accepted customer did not reserve the free loading bay.");
            BeginCustomerReturnSystem beginReturn =
                runtime.Systems.Create<BeginCustomerReturnSystem>();
            beginReturn.Execute();
            Require(visit.isCustomerVisitReturning &&
                    actor.isCustomerReturningToVehicle &&
                    !actor.isCustomerWaitingAtCounter &&
                    actor.hasRoute && actor.hasRouteWaypointIndex,
                "Accepted customer did not begin walking back to the parked vehicle.");
            Pose[] activeReturnRoute = actor.Route;
            int activeReturnWaypointIndex = actor.RouteWaypointIndex;
            beginReturn.Execute();
            Require(ReferenceEquals(actor.Route, activeReturnRoute) &&
                    actor.RouteWaypointIndex == activeReturnWaypointIndex &&
                    actor.isCustomerReturningToVehicle &&
                    !actor.hasCustomerReturnRoute,
                "Repeated customer-return execution restarted or mutated an active route.");
            runtime.Systems.Create<AdvanceCustomerQueueSystem>().Execute();
            ValidateReturningInteractionPrompts(runtime, scenario, visit);

            int actorId = actor.EntityId;
            EntityBehaviour actorView = (EntityBehaviour)actor.View;
            ForceRouteEndpoint(runtime, actor);
            runtime.Systems.Create<CompleteCustomerReturnSystem>().Execute();
            Require(visit.isCustomerVisitWaitingForLoadingBay &&
                    !visit.isCustomerVisitReturning &&
                    actor.isDestructed &&
                    !actor.hasCustomerActorVisitEntityId &&
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(visit.EntityId) == null,
                "The returned actor did not release its visit relation at the vehicle.");
            runtime.Systems.Create<CleanupDestructedViewsSystem>().Cleanup();
            runtime.Systems.Create<CleanupDestructedEntitiesSystem>().Cleanup();
            Require(!actorView.HasEntity &&
                    runtime.Game.GetEntityWithEntityId(actorId) == null,
                "Returned customer actor survived the Destructed pipeline.");

            runtime.Systems.Create<MoveCustomerVehicleToLoadingBaySystem>().Execute();
            Require(visit.isCustomerVisitMovingToLoadingBay &&
                    visit.hasReservedCustomerParkingSpotEntityId &&
                    visit.hasReservedCustomerLoadingBayEntityId &&
                    visit.hasReservedCustomerTrafficLaneEntityId &&
                    visit.hasRoute && visit.hasRouteWaypointIndex,
                "Accepted vehicle did not leave parking for its reserved loading bay.");
            ValidateReverseLoadingMovement(runtime, visit);
            ForceRouteEndpoint(runtime, visit);
            runtime.Systems.Create<CompleteCustomerLoadingBayArrivalSystem>().Execute();
            Require(visit.isCustomerVisitLoading &&
                    !visit.isCustomerVisitMovingToLoadingBay &&
                    visit.hasReservedCustomerLoadingBayEntityId &&
                    !visit.hasReservedCustomerParkingSpotEntityId &&
                    !visit.hasReservedCustomerTrafficLaneEntityId &&
                    visit.isInteractable &&
                    !visit.hasRoute && !visit.hasRouteWaypointIndex,
                "Vehicle did not release parking and traffic lane at the loading bay.");
            ValidateReverseLoadingPose(visit);
            runtime.Systems.Create<ValidateCustomerFlowStateSystem>().Execute();
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

        private static GameEntity[] CreateSyntheticLoadedProducts(
            Runtime runtime,
            GameEntity[] orderLines)
        {
            int productCount = orderLines.Sum(line => line.RequiredProductCount);
            int firstEntityId = runtime.Game.GetGroup(GameMatcher.EntityId)
                .GetEntities()
                .Min(entity => entity.EntityId) - productCount;
            var products = new List<GameEntity>(productCount);
            int loadingSlotIndex = 0;
            foreach (GameEntity line in orderLines)
            {
                for (int index = 0; index < line.RequiredProductCount; index++)
                {
                    GameEntity product = CreateEntity.Empty(
                            firstEntityId + loadingSlotIndex)
                        .AddProductType(line.ProductType)
                        .AddOrderLineEntityId(line.EntityId)
                        .AddLoadingSlotIndex(loadingSlotIndex);
                    product.isProduct = true;
                    product.isLoaded = true;
                    products.Add(product);
                    loadingSlotIndex++;
                }
            }

            return products.ToArray();
        }

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

        private static bool PoseMatches(Pose actual, Pose expected) =>
            Vector3.Distance(actual.position, expected.position) < 0.001f &&
            Quaternion.Angle(actual.rotation, expected.rotation) < 0.01f;

        private static void RequireCustomerDissatisfactionVisual(
            GameEntity actor,
            bool expectedDissatisfied,
            string message)
        {
            Require(actor != null && actor.isCustomer && !actor.isDestructed &&
                    actor.hasCustomerDissatisfactionView,
                message + " The actor has no bound dissatisfaction view.");
            var moodView = actor.CustomerDissatisfactionView;
            Renderer[] renderers = moodView.Renderers;
            TextMesh label = moodView.WorldLabel;
            int baseColorId = Shader.PropertyToID("_BaseColor");
            bool colorsMatch = renderers.Length == 9;
            foreach (Renderer bodyRenderer in renderers)
            {
                Material[] materials = bodyRenderer.sharedMaterials;
                colorsMatch &= materials.Length > 0;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material = materials[materialIndex];
                    var propertyBlock = new MaterialPropertyBlock();
                    bodyRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                    Color baseColor = material.GetColor(baseColorId);
                    Color expectedColor = expectedDissatisfied
                        ? Color.Lerp(
                            baseColor,
                            Color.red,
                            CustomerDissatisfactionView.DissatisfiedColorBlend)
                        : baseColor;
                    Color actualColor = propertyBlock.GetColor(baseColorId);
                    colorsMatch &= Mathf.Abs(actualColor.r - expectedColor.r) < 0.001f &&
                                   Mathf.Abs(actualColor.g - expectedColor.g) < 0.001f &&
                                   Mathf.Abs(actualColor.b - expectedColor.b) < 0.001f &&
                                   Mathf.Abs(actualColor.a - expectedColor.a) < 0.001f;
                }
            }

            Quaternion expectedLeft = expectedDissatisfied
                ? Quaternion.Euler(
                    0f,
                    0f,
                    -CustomerDissatisfactionView.RaisedArmAngle)
                : Quaternion.identity;
            Quaternion expectedRight = expectedDissatisfied
                ? Quaternion.Euler(
                    0f,
                    0f,
                    CustomerDissatisfactionView.RaisedArmAngle)
                : Quaternion.identity;
            float armTolerance = expectedDissatisfied
                ? CustomerDissatisfactionView.ArmWaveAmplitude + 0.1f
                : 0.01f;
            bool armsMatch =
                Quaternion.Angle(
                    moodView.LeftShoulder.localRotation,
                    expectedLeft) <= armTolerance &&
                Quaternion.Angle(
                    moodView.RightShoulder.localRotation,
                    expectedRight) <= armTolerance;
            bool labelMatches = expectedDissatisfied
                ? label.gameObject.activeSelf &&
                  label.text == "НЕДОВОЛЕН • МОЖЕТ УЙТИ" &&
                  label.color.r >= 0.95f &&
                  label.color.g <= 0.1f &&
                  label.color.b <= 0.1f &&
                  Vector3.Dot(
                      label.transform.forward,
                      (label.transform.position -
                       Camera.main.transform.position).normalized) > 0.999f
                : !label.gameObject.activeSelf &&
                  string.IsNullOrEmpty(label.text) &&
                  Quaternion.Angle(
                      label.transform.localRotation,
                      Quaternion.identity) < 0.01f;

            Require(moodView.IsDissatisfied == expectedDissatisfied &&
                    colorsMatch && armsMatch && labelMatches,
                message);
        }

        private static void ValidateReverseLoadingMovement(
            Runtime runtime,
            GameEntity customerVisit)
        {
            Require(customerVisit.isCustomerVisitMovingToLoadingBay &&
                    customerVisit.hasRoute && customerVisit.Route.Length == 10 &&
                    customerVisit.hasRouteWaypointIndex &&
                    customerVisit.hasTransform && customerVisit.hasRigidbody,
                $"Customer visit {customerVisit.EntityId} has no reverse loading route.");

            Pose reverseStart = customerVisit.Route[^2];
            Pose loadingPose = customerVisit.Route[^1];
            Require(Quaternion.Angle(
                        reverseStart.rotation,
                        Quaternion.Euler(0f, 180f, 0f)) < 0.001f &&
                    Quaternion.Angle(
                        loadingPose.rotation,
                        Quaternion.Euler(0f, 180f, 0f)) < 0.001f &&
                    loadingPose.position.z > reverseStart.position.z,
                "The final loading segment must begin rear-facing and lead north into the bay.");

            customerVisit.ReplaceRouteWaypointIndex(customerVisit.Route.Length - 1);
            customerVisit.Rigidbody.position = reverseStart.position;
            customerVisit.Rigidbody.rotation = reverseStart.rotation;
            customerVisit.Transform.SetPositionAndRotation(
                reverseStart.position,
                reverseStart.rotation);
            Physics.SyncTransforms();

            Vector3 positionBefore = customerVisit.Rigidbody.position;
            Vector3 forwardBefore = customerVisit.Rigidbody.rotation * Vector3.forward;
            new MoveRouteSystem(
                runtime.Game,
                new FixedTimeService(0.25f)).Execute();
            Vector3 movement = customerVisit.Rigidbody.position - positionBefore;
            Require(movement.z > 0f && movement.sqrMagnitude > 0.01f &&
                    Vector3.Dot(movement.normalized, forwardBefore) < -0.95f,
                "The customer vehicle did not drive backwards on the final loading segment.");
        }

        private static void ValidateReverseLoadingPose(GameEntity customerVisit)
        {
            Require(customerVisit.isCustomerVisitLoading &&
                    customerVisit.hasTransform && customerVisit.hasRigidbody &&
                    Quaternion.Angle(
                        customerVisit.Transform.rotation,
                        Quaternion.Euler(0f, 180f, 0f)) < 0.001f,
                $"Customer visit {customerVisit.EntityId} did not finish rear-facing.");

            Transform loadingTarget = customerVisit.Transform.Find("Loading Target");
            Require(loadingTarget != null &&
                    Vector3.Distance(
                        loadingTarget.position,
                        new Vector3(6f, 1.02f, 0.96f)) < 0.05f,
                "The rear loading target did not face the warehouse at the loading pose.");
        }

        private static void ValidateForwardLoadingDepartureMovement(
            Runtime runtime,
            GameEntity customerVisit)
        {
            Require(customerVisit.isCustomerVisitDeparting &&
                    customerVisit.hasRoute && customerVisit.Route.Length == 7 &&
                    customerVisit.hasRouteWaypointIndex &&
                    customerVisit.hasTransform && customerVisit.hasRigidbody,
                $"Customer visit {customerVisit.EntityId} has no loading departure route.");

            Pose departureStart = customerVisit.Route[0];
            customerVisit.ReplaceRouteWaypointIndex(1);
            customerVisit.Rigidbody.position = departureStart.position;
            customerVisit.Rigidbody.rotation = departureStart.rotation;
            customerVisit.Transform.SetPositionAndRotation(
                departureStart.position,
                departureStart.rotation);
            Physics.SyncTransforms();

            Vector3 positionBefore = customerVisit.Rigidbody.position;
            Vector3 forwardBefore = customerVisit.Rigidbody.rotation * Vector3.forward;
            new MoveRouteSystem(
                runtime.Game,
                new FixedTimeService(0.25f)).Execute();
            Vector3 movement = customerVisit.Rigidbody.position - positionBefore;
            Require(movement.z < 0f && movement.sqrMagnitude > 0.01f &&
                    Vector3.Dot(movement.normalized, forwardBefore) > 0.95f,
                "The rear-facing customer vehicle did not leave the loading bay forwards.");
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
            int expensesBeforePurchase = scenario.Store.DayProcurementExpenses;
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
                    scenario.Store.Money == moneyBeforePurchase - deliveryConfig.TotalCost &&
                    scenario.Store.DayProcurementExpenses == checked(
                        expensesBeforePurchase + deliveryConfig.TotalCost),
                "Purchasing did not create an indexed active delivery.");

            runtime.Systems.Create<PurchaseDeliverySystem>().Execute();
            Require(ReferenceEquals(
                        runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                            scenario.ProcurementTerminal.EntityId),
                        delivery) &&
                    runtime.Game.GetGroup(GameMatcher.Delivery).count == 1 &&
                    scenario.Store.Money == moneyBeforePurchase - deliveryConfig.TotalCost &&
                    scenario.Store.DayProcurementExpenses == checked(
                        expensesBeforePurchase + deliveryConfig.TotalCost),
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
            int expensesBefore = scenario.Store.DayProcurementExpenses;
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
                    scenario.Store.DayProcurementExpenses == expensesBefore &&
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
            DeliveryArrival arrival,
            bool validateContextAwareStorageIntake = false)
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

                bool validateIntake = validateContextAwareStorageIntake &&
                                      index == arrival.Products.Length - 1;
                if (validateIntake)
                {
                    ValidateContextAwareStorageIntake(
                        runtime,
                        scenario,
                        product,
                        FindStockProducts(runtime.Game, scenario.StorageZone.EntityId)
                            .Single(stockProduct =>
                                stockProduct.hasStorageSlotIndex &&
                                stockProduct.StorageSlotIndex == 0));
                }
                else
                {
                    RequestInteraction(scenario.Player, scenario.StorageZone);
                    runtime.Systems.Create<StoreInboundProductSystem>().Execute();
                }
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

        private static void ValidateContextAwareStorageIntake(
            Runtime runtime,
            Scenario scenario,
            GameEntity heldInboundProduct,
            GameEntity occludingStockProduct)
        {
            Require(scenario.StorageZone.OccupiedStorageSlotCount >= 4 &&
                    scenario.StorageZone.OccupiedStorageSlotCount <
                    scenario.StorageZone.Slots.Length &&
                    scenario.Player.isHandsOccupied &&
                    scenario.Player.isCarryingProduct &&
                    heldInboundProduct.isInboundProduct &&
                    heldInboundProduct.hasCarrierEntityId &&
                    heldInboundProduct.CarrierEntityId == scenario.Player.EntityId &&
                    occludingStockProduct.isInStock &&
                    occludingStockProduct.isInteractable &&
                    occludingStockProduct.hasColliders,
                "Context-aware storage focus requires a crowded, non-full storage, one held " +
                "inbound product and one physically occluding stock product.");

            Collider stockCollider = occludingStockProduct.Colliders
                .Single(collider => !collider.isTrigger);
            Transform cameraTransform = scenario.Player.Camera.transform;
            Vector3 originalPosition = cameraTransform.position;
            Quaternion originalRotation = cameraTransform.rotation;
            GameObject foreignTrigger = null;
            GameObject wall = null;

            try
            {
                Vector3 target = stockCollider.bounds.center;
                cameraTransform.SetPositionAndRotation(
                    target - Vector3.forward * 0.65f,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));
                Physics.SyncTransforms();

                GameEntity foreignStorage = scenario.TrolleyUpgradeTerminal;
                int owningStorageZoneEntityId = occludingStockProduct.StorageZoneEntityId;
                Require(!foreignStorage.isStorageZone &&
                        foreignStorage.EntityId != owningStorageZoneEntityId,
                    "Foreign storage proxy smoke requires a distinct non-storage interactable.");
                foreignStorage.isStorageZone = true;
                occludingStockProduct.ReplaceStorageZoneEntityId(foreignStorage.EntityId);
                try
                {
                    ClearPhysicalFocus(runtime, scenario.Player);
                    runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                    Require(scenario.Player.hasFocusedEntityId &&
                            scenario.Player.FocusedEntityId == occludingStockProduct.EntityId,
                        "A stock product linked to another store proxied into the player's " +
                        "storage zone.");
                }
                finally
                {
                    occludingStockProduct.ReplaceStorageZoneEntityId(
                        owningStorageZoneEntityId);
                    foreignStorage.isStorageZone = false;
                    ClearPhysicalFocus(runtime, scenario.Player);
                }

                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                ExecuteInteractionPrompts(runtime);
                Require(scenario.Player.hasFocusedEntityId &&
                        scenario.Player.FocusedEntityId == scenario.StorageZone.EntityId &&
                        scenario.Player.hasFocusedInteractionType &&
                        scenario.Player.FocusedInteractionType == InteractionTypeId.StorageZone &&
                        scenario.Player.isFocusInteractionAvailable &&
                        PromptMatches(
                            runtime,
                            scenario.Player,
                            LocalizedTexts.Text(
                                LocalizationKey.PromptStoreInboundProduct,
                                LocalizedTexts.ProductName(heldInboundProduct.ProductType))),
                    "A held inbound product aimed through stored cargo did not contextually " +
                    "select the full-storage intake proxy.");

                bool candidateOverflowRejected = false;
                try
                {
                    cameraTransform.position = target - Vector3.forward * 1.8f;
                    Physics.SyncTransforms();
                    try
                    {
                        runtime.InteractionPhysics.GetFocusCandidates(
                            scenario.Player.Camera,
                            scenario.Player.InteractionDistance,
                            scenario.Player.AimAssistRadius,
                            new InteractionFocusCandidate[1]);
                    }
                    catch (InvalidOperationException exception)
                    {
                        candidateOverflowRejected = exception.Message.Contains(
                            "Interaction focus candidate buffer saturated at 1 entries",
                            StringComparison.Ordinal);
                    }
                }
                finally
                {
                    cameraTransform.position = target - Vector3.forward * 0.65f;
                    Physics.SyncTransforms();
                }

                Require(candidateOverflowRejected,
                    "Interaction physics silently truncated an undersized candidate buffer.");

                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<EmitInteractionRequestSystem>().Execute();
                GameEntity[] storageRequests = runtime.Game
                    .GetGroup(GameMatcher.InteractionRequest)
                    .GetEntities();
                Require(storageRequests.Length == 1 &&
                        storageRequests[0].SourceEntityId == scenario.Player.EntityId &&
                        storageRequests[0].TargetEntityId == scenario.StorageZone.EntityId,
                    "E did not target the context-selected storage intake exactly once.");
                runtime.Systems.Create<StoreInboundProductSystem>().Execute();
                Require(heldInboundProduct.isProductStocked &&
                        heldInboundProduct.isInStock &&
                        !heldInboundProduct.isInboundProduct &&
                        !heldInboundProduct.hasCarrierEntityId &&
                        !scenario.Player.isHandsOccupied &&
                        !scenario.Player.isCarryingProduct,
                    "E did not store the held inbound product through the contextual intake.");

                ClearPhysicalFocus(runtime, scenario.Player);
                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                ExecuteInteractionPrompts(runtime);
                Require(scenario.Player.hasFocusedEntityId &&
                        scenario.Player.FocusedEntityId == occludingStockProduct.EntityId &&
                        scenario.Player.hasFocusedInteractionType &&
                        scenario.Player.FocusedInteractionType == InteractionTypeId.Product,
                    "With empty hands, the same aim selected the storage proxy instead of the " +
                    "visible stock product.");

                int defaultLayer = LayerMask.NameToLayer("Default");
                Require(defaultLayer >= 0,
                    "The built-in Default layer is required for interaction focus smoke.");
                foreignTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foreignTrigger.name = "Smoke Foreign Interaction Trigger";
                foreignTrigger.layer = defaultLayer;
                foreignTrigger.transform.SetPositionAndRotation(
                    Vector3.Lerp(cameraTransform.position, target, 0.25f),
                    Quaternion.identity);
                foreignTrigger.transform.localScale = new Vector3(2.4f, 2.4f, 0.15f);
                foreignTrigger.GetComponent<BoxCollider>().isTrigger = true;
                Physics.SyncTransforms();
                ClearPhysicalFocus(runtime, scenario.Player);
                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                ExecuteInteractionPrompts(runtime);
                Require(!scenario.Player.hasFocusedEntityId &&
                        !scenario.Player.hasFocusedInteractionType &&
                        !scenario.Player.isFocusInteractionAvailable,
                    "A foreign Default-layer trigger did not occlude product and storage focus " +
                    "candidates.");

                UnityEngine.Object.DestroyImmediate(foreignTrigger);
                foreignTrigger = null;
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Smoke Interaction Focus Occlusion Wall";
                wall.layer = defaultLayer;
                wall.transform.SetPositionAndRotation(
                    Vector3.Lerp(cameraTransform.position, target, 0.25f),
                    Quaternion.identity);
                wall.transform.localScale = new Vector3(2.4f, 2.4f, 0.15f);
                Physics.SyncTransforms();
                ClearPhysicalFocus(runtime, scenario.Player);
                runtime.Systems.Create<DetectFocusedInteractableSystem>().Execute();
                runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
                ExecuteInteractionPrompts(runtime);
                Require(!scenario.Player.hasFocusedEntityId &&
                        !scenario.Player.hasFocusedInteractionType &&
                        !scenario.Player.isFocusInteractionAvailable,
                    "A solid wall did not occlude product and storage focus candidates.");
            }
            finally
            {
                if (foreignTrigger != null)
                    UnityEngine.Object.DestroyImmediate(foreignTrigger);
                if (wall != null)
                    UnityEngine.Object.DestroyImmediate(wall);
                cameraTransform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                ClearPhysicalFocus(runtime, scenario.Player);
            }
        }

        private static void ClearPhysicalFocus(Runtime runtime, GameEntity player)
        {
            if (player.hasFocusedEntityId)
                player.RemoveFocusedEntityId();
            if (player.hasFocusedInteractionType)
                player.RemoveFocusedInteractionType();
            runtime.Systems.Create<UpdateFocusHighlightSystem>().Execute();
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
            int upgradeExpensesBefore = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationTrolleyUpgradeLocked);
            Require(scenario.Store.Money == moneyBefore &&
                    scenario.Store.DayUpgradeExpenses == upgradeExpensesBefore &&
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
            int upgradeExpensesBefore = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            RequireNotificationKey(
                runtime,
                LocalizationKey.NotificationTrolleyPurchaseWouldBlockProjects);
            Require(scenario.Store.Money == moneyBefore &&
                    scenario.Store.DayUpgradeExpenses == upgradeExpensesBefore &&
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
            int upgradeExpensesBefore = scenario.Store.DayUpgradeExpenses;
            RequestInteraction(
                scenario.Player,
                scenario.TrolleyUpgradeTerminal);
            runtime.Systems.Create<PurchasePlatformTrolleySystem>().Execute();
            GameEntity trolley = runtime.Game.GetEntityWithTrolleyStoreEntityId(
                scenario.Store.EntityId);
            Require(trolley != null &&
                    runtime.Game.GetGroup(GameMatcher.PlatformTrolley).count == 1 &&
                    scenario.Store.Money == moneyBefore - config.PurchasePrice &&
                    scenario.Store.DayUpgradeExpenses == checked(
                        upgradeExpensesBefore + config.PurchasePrice),
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
                    scenario.Store.DayUpgradeExpenses == checked(
                        upgradeExpensesBefore + config.PurchasePrice) &&
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
            Require(HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId) &&
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
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
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
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
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

            ValidateTrolleyThresholdTraversal(
                runtime,
                scenario,
                trolley,
                trolleyBody);
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

        private static void ValidateTrolleyThresholdTraversal(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            Collider trolleyBody)
        {
            BoxCollider bodyBox = trolleyBody as BoxCollider;
            Require(bodyBox != null && bodyBox.attachedRigidbody == trolley.Rigidbody &&
                    trolley.Colliders.Count(collider =>
                        collider.enabled && !collider.isTrigger) == 1,
                "Threshold traversal requires the single authored trolley BoxCollider hull.");
            Require(Mathf.Abs(scenario.Player.CharacterController.stepOffset - 0.32f) <
                    0.0001f,
                "Threshold traversal requires the authored 0.32m player step offset.");

            BoxCollider[] storagePads = Resources.FindObjectsOfTypeAll<BoxCollider>()
                .Where(collider =>
                    collider.gameObject.scene == SceneManager.GetActiveScene() &&
                    collider.name == "Storage Pad")
                .ToArray();
            Require(storagePads.Length == 1,
                $"Expected one authored Storage Pad collider, found {storagePads.Length}.");
            BoxCollider storagePad = storagePads[0];
            Require(storagePad.enabled && !storagePad.isTrigger &&
                    Mathf.Abs(storagePad.bounds.min.y) < 0.001f &&
                    Mathf.Abs(storagePad.bounds.max.y - 0.2f) < 0.001f &&
                    Mathf.Abs(storagePad.bounds.size.x - 7.5f) < 0.001f &&
                    Mathf.Abs(storagePad.bounds.size.z - 6.5f) < 0.001f,
                "The authored Storage Pad must be one solid 0.20m warehouse threshold.");

            GameEntity[] cargo = runtime.Game
                .GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                .OrderBy(product => product.TrolleySlotIndex)
                .ToArray();
            int[] cargoEntityIds = cargo.Select(product => product.EntityId).ToArray();
            int[] trolleySlots = cargo.Select(product => product.TrolleySlotIndex).ToArray();
            int[] storageSlots = cargo
                .Select(product => product.ReservedStorageSlotIndex)
                .ToArray();
            int[] orderLines = cargo
                .Select(product => product.ReservedOrderLineEntityId)
                .ToArray();
            Require(cargo.Length == trolley.TrolleyCapacity &&
                    cargo.All(product =>
                        product.hasReservedStorageSlotIndex &&
                        product.hasReservedOrderLineEntityId &&
                        product.Transform.IsChildOf(trolley.Transform)),
                "Threshold traversal requires a full trolley with preserved cargo relations.");

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            Require(ignoreRaycastLayer >= 0,
                "The built-in Ignore Raycast layer is required for trolley threshold smoke.");
            const float groundY = 0.01f;
            float thresholdHeight = storagePad.bounds.size.y;
            Vector3 south = new(0f, groundY, -11f);
            Vector3 north = new(0f, groundY, -5f);
            Require(Vector3.Distance(south, north) >
                    trolley.TrolleyFollowDistance * 3f,
                "Threshold smoke must stretch the trolley tether beyond a normal frame move.");

            float hullCenterZOffset = bodyBox.bounds.center.z -
                                      trolley.Transform.position.z;
            float hullHalfDepth = bodyBox.bounds.extents.z;
            float storagePadTangentZ = storagePad.bounds.min.z -
                                       hullCenterZOffset - hullHalfDepth;
            ValidateSuccessfulTrolleyMove(
                runtime,
                scenario,
                trolley,
                new Vector3(3f, groundY, storagePadTangentZ),
                new Vector3(7f, groundY, storagePadTangentZ),
                "authored Storage Pad tangent slide");
            ValidateSuccessfulTrolleyMove(
                runtime,
                scenario,
                trolley,
                new Vector3(3f, groundY, storagePadTangentZ),
                new Vector3(3f, groundY, storagePadTangentZ - 2f),
                "authored Storage Pad zero-distance contact recovery");

            GameObject lowThreshold = CreateTrolleyMotionObstacle(
                "Smoke Authored 0.20m Warehouse Threshold",
                new Vector3(0f, thresholdHeight * 0.5f, -8f),
                new Vector3(4f, thresholdHeight, 0.12f),
                ignoreRaycastLayer);
            try
            {
                ValidateSuccessfulTrolleyMove(
                    runtime,
                    scenario,
                    trolley,
                    south,
                    north + Vector3.up * thresholdHeight,
                    "asphalt-to-pad 0.20m climb");
                ValidateSuccessfulTrolleyMove(
                    runtime,
                    scenario,
                    trolley,
                    north + Vector3.up * thresholdHeight,
                    south,
                    "pad-to-asphalt 0.20m descent");
                ValidateSuccessfulTrolleyMove(
                    runtime,
                    scenario,
                    trolley,
                    south,
                    north,
                    "same-level 0.20m threshold recovery");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lowThreshold);
                Physics.SyncTransforms();
            }

            GameObject highThreshold = CreateTrolleyMotionObstacle(
                "Smoke Blocking 0.40m Threshold",
                new Vector3(0f, 0.2f, -8f),
                new Vector3(4f, 0.4f, 0.12f),
                ignoreRaycastLayer);
            try
            {
                ValidateBlockedTrolleyMove(
                    runtime,
                    scenario,
                    trolley,
                    south,
                    north,
                    "0.40m threshold above the authored step limit");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(highThreshold);
                Physics.SyncTransforms();
            }

            GameObject thinWall = CreateTrolleyMotionObstacle(
                "Smoke Blocking Thin Trolley Wall",
                new Vector3(0f, 0.6f, -8f),
                new Vector3(4f, 1.2f, 0.04f),
                ignoreRaycastLayer);
            try
            {
                ValidateBlockedTrolleyMove(
                    runtime,
                    scenario,
                    trolley,
                    south,
                    north,
                    "thin 1.20m wall with the target fully beyond it");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(thinWall);
                Physics.SyncTransforms();
            }

            GameEntity[] cargoAfter = runtime.Game
                .GetEntitiesWithTrolleyEntityId(trolley.EntityId)
                .OrderBy(product => product.TrolleySlotIndex)
                .ToArray();
            Require(cargoAfter.Select(product => product.EntityId)
                        .SequenceEqual(cargoEntityIds) &&
                    cargoAfter.Select(product => product.TrolleySlotIndex)
                        .SequenceEqual(trolleySlots) &&
                    cargoAfter.Select(product => product.ReservedStorageSlotIndex)
                        .SequenceEqual(storageSlots) &&
                    cargoAfter.Select(product => product.ReservedOrderLineEntityId)
                        .SequenceEqual(orderLines) &&
                    cargoAfter.All(product =>
                        product.TrolleyEntityId == trolley.EntityId &&
                        product.Transform.IsChildOf(trolley.Transform)) &&
                    scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId,
                "Threshold traversal changed cargo, reservations or pushing relations.");

            SetTrolleyMotionSmokePose(
                scenario,
                trolley,
                new Vector3(0f, 0.02f, -3.3f),
                new Vector3(0f, 0.02f, -3.3f),
                Quaternion.identity);
        }

        private static void ValidateSuccessfulTrolleyMove(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            Vector3 startPosition,
            Vector3 targetPosition,
            string operation)
        {
            Quaternion rotation = Quaternion.identity;
            SetTrolleyMotionSmokePose(
                scenario,
                trolley,
                startPosition,
                targetPosition,
                rotation);
            Vector3 transformPositionBefore = trolley.Transform.position;
            Quaternion transformRotationBefore = trolley.Transform.rotation;
            Vector3 bodyPositionBefore = trolley.Rigidbody.position;
            Quaternion bodyRotationBefore = trolley.Rigidbody.rotation;

            bool resolved = runtime.TrolleyMotion.TryResolveMove(
                trolley.Rigidbody,
                trolley.Colliders,
                scenario.Player.CharacterController,
                targetPosition,
                rotation,
                out Pose resolvedPose);
            Require(resolved &&
                    Vector3.Distance(resolvedPose.position, targetPosition) < 0.001f &&
                    Quaternion.Angle(resolvedPose.rotation, rotation) < 0.001f &&
                    Vector3.Distance(trolley.Transform.position, transformPositionBefore) <
                    0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, transformRotationBefore) <
                    0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, bodyPositionBefore) <
                    0.001f &&
                    Quaternion.Angle(trolley.Rigidbody.rotation, bodyRotationBefore) <
                    0.001f,
                $"Trolley motion did not resolve the exact {operation} target without mutation.");

            runtime.Systems.Create<FollowPushedTrolleySystem>().Execute();
            Physics.SyncTransforms();
            Require(Vector3.Distance(trolley.Transform.position, targetPosition) < 0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, rotation) < 0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, targetPosition) < 0.001f &&
                    Quaternion.Angle(trolley.Rigidbody.rotation, rotation) < 0.001f,
                $"FollowPushedTrolleySystem did not apply the exact {operation} pose.");
        }

        private static void ValidateBlockedTrolleyMove(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            Vector3 startPosition,
            Vector3 targetPosition,
            string operation)
        {
            Quaternion rotation = Quaternion.identity;
            SetTrolleyMotionSmokePose(
                scenario,
                trolley,
                startPosition,
                targetPosition,
                rotation);
            Pose startPose = new(trolley.Rigidbody.position, trolley.Rigidbody.rotation);

            bool resolved = runtime.TrolleyMotion.TryResolveMove(
                trolley.Rigidbody,
                trolley.Colliders,
                scenario.Player.CharacterController,
                targetPosition,
                rotation,
                out Pose resolvedPose);
            Require(!resolved &&
                    Vector3.Distance(resolvedPose.position, startPose.position) < 0.001f &&
                    Quaternion.Angle(resolvedPose.rotation, startPose.rotation) < 0.001f &&
                    Vector3.Distance(trolley.Transform.position, startPose.position) < 0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, startPose.rotation) < 0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, startPose.position) < 0.001f &&
                    Quaternion.Angle(trolley.Rigidbody.rotation, startPose.rotation) < 0.001f,
                $"Trolley motion accepted or partially mutated the blocked {operation}.");

            runtime.Systems.Create<FollowPushedTrolleySystem>().Execute();
            Physics.SyncTransforms();
            Require(Vector3.Distance(trolley.Transform.position, startPose.position) < 0.001f &&
                    Quaternion.Angle(trolley.Transform.rotation, startPose.rotation) < 0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, startPose.position) < 0.001f &&
                    Quaternion.Angle(trolley.Rigidbody.rotation, startPose.rotation) < 0.001f,
                $"FollowPushedTrolleySystem partially applied the blocked {operation}.");
        }

        private static void SetTrolleyMotionSmokePose(
            Scenario scenario,
            GameEntity trolley,
            Vector3 trolleyPosition,
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            CharacterController controller = scenario.Player.CharacterController;
            controller.enabled = false;
            scenario.Player.Transform.SetPositionAndRotation(
                targetPosition - targetRotation * Vector3.forward *
                trolley.TrolleyFollowDistance,
                targetRotation);
            trolley.Rigidbody.position = trolleyPosition;
            trolley.Rigidbody.rotation = targetRotation;
            trolley.Transform.SetPositionAndRotation(trolleyPosition, targetRotation);
            controller.enabled = true;
            Physics.SyncTransforms();

            Vector3 followTarget = scenario.Player.Transform.position +
                                   scenario.Player.Transform.forward *
                                   trolley.TrolleyFollowDistance;
            Require(Vector3.Distance(followTarget, targetPosition) < 0.001f &&
                    Vector3.Distance(trolley.Rigidbody.position, trolleyPosition) <
                    0.001f &&
                    Vector3.Distance(trolley.Transform.position, trolleyPosition) <
                    0.001f,
                "Trolley threshold smoke could not author its exact start and follow target.");
        }

        private static GameObject CreateTrolleyMotionObstacle(
            string name,
            Vector3 position,
            Vector3 scale,
            int layer)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.layer = layer;
            obstacle.transform.SetPositionAndRotation(position, Quaternion.identity);
            obstacle.transform.localScale = scale;
            BoxCollider collider = obstacle.GetComponent<BoxCollider>();
            Require(collider.enabled && !collider.isTrigger,
                $"Trolley smoke obstacle '{name}' must be a solid BoxCollider.");
            Physics.SyncTransforms();
            return obstacle;
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
            int revenueBeforeReward = scenario.Store.DayRevenue;
            int completedOrdersBeforeReward = scenario.Store.DayCompletedOrderCount;
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
                    scenario.Store.Money == moneyBeforeReward + visit.OrderReward &&
                    scenario.Store.DayRevenue == checked(
                        revenueBeforeReward + visit.OrderReward) &&
                    scenario.Store.DayCompletedOrderCount == checked(
                        completedOrdersBeforeReward + 1),
                "The completed order was not rewarded exactly once.");
            runtime.Systems.Create<RewardCompletedOrderSystem>().Execute();
            Require(scenario.Store.Money == moneyBeforeReward + visit.OrderReward &&
                    scenario.Store.DayRevenue == checked(
                        revenueBeforeReward + visit.OrderReward) &&
                    scenario.Store.DayCompletedOrderCount == checked(
                        completedOrdersBeforeReward + 1),
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
            bool storeRemainsOpen = scenario.Store.isStoreOpen;
            int activeVisitCountBeforeDeparture = FindCustomerVisits(
                runtime.Game,
                scenario.Store.EntityId).Length;
            Require(storeRemainsOpen || scenario.Store.isStoreClosing,
                "Customer departure requires an open or closing store.");
            float scheduledDelay = storeRemainsOpen
                ? scenario.Store.CustomerCooldownRemaining
                : 0f;
            Require(entity.isCustomerVisitCompleted &&
                    entity.isOrderRewarded &&
                    !entity.hasCustomerDepartureDelayRemaining &&
                    entity.hasReservedCustomerLoadingBayEntityId &&
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(
                        entity.EntityId) == null &&
                    !visit.ActorView.HasEntity,
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
                    entity.hasRoute &&
                    entity.hasRouteWaypointIndex &&
                    entity.hasReservedCustomerLoadingBayEntityId &&
                    entity.hasReservedCustomerTrafficLaneEntityId,
                "The rewarded vehicle did not acquire the lane for departure.");

            ValidateForwardLoadingDepartureMovement(runtime, entity);
            ForceRouteEndpoint(runtime, entity);
            EntityBehaviour[] loadedViews = loadedProducts
                .Where(product => product.hasView)
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
                    entity.hasCustomerVisitStoreEntityId &&
                    entity.hasReservedCustomerLoadingBayEntityId &&
                    entity.hasReservedCustomerTrafficLaneEntityId,
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

            Require(FindCustomerVisits(runtime.Game, scenario.Store.EntityId).Length ==
                    activeVisitCountBeforeDeparture - 1 &&
                    !FindCustomerVisits(runtime.Game, scenario.Store.EntityId)
                        .Contains(entity) &&
                    !entity.hasCustomerVisitStoreEntityId &&
                    !entity.hasReservedCustomerLoadingBayEntityId &&
                    !entity.hasReservedCustomerTrafficLaneEntityId &&
                    (storeRemainsOpen
                        ? scenario.Store.hasCustomerCooldownRemaining &&
                          Mathf.Approximately(
                              scenario.Store.CustomerCooldownRemaining,
                              scheduledDelay)
                        : !scenario.Store.hasCustomerCooldownRemaining),
                storeRemainsOpen
                    ? "Customer departure changed the independent arrival schedule."
                    : "A closing store scheduled another customer after departure.");
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
                    runtime.Game.GetEntityWithCustomerActorVisitEntityId(visitId) == null,
                "The departed customer graph survived cleanup or damaged another visit.");
            ExecuteStorageState(runtime);
        }

        private static void ValidateReturningInteractionPrompts(
            Runtime runtime,
            Scenario scenario,
            GameEntity visit)
        {
            int queuedCustomerCount = FindCustomerVisits(
                    runtime.Game,
                    scenario.Store.EntityId)
                .Count(candidate => candidate.isCustomerVisitQueued);
            LocalizationKey noCustomerPromptKey;
            if (scenario.Store.isStorePreparing)
            {
                noCustomerPromptKey =
                    LocalizationKey.PromptCounterOpenStoreAtControlTerminal;
            }
            else if (scenario.Store.isStoreOpen)
            {
                noCustomerPromptKey = LocalizationKey.PromptCounterWaitCustomer;
            }
            else if (scenario.Store.isStoreClosing)
            {
                noCustomerPromptKey =
                    LocalizationKey.PromptCounterFinishDayAtControlTerminal;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Store {scenario.Store.EntityId} has no valid order-counter day phase.");
            }
            LocalizedText expectedCounterPrompt = queuedCustomerCount > 0
                ? LocalizedTexts.Text(
                    LocalizationKey.PromptCounterNextCustomerApproaching,
                    queuedCustomerCount)
                : LocalizedTexts.Text(noCustomerPromptKey);
            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        expectedCounterPrompt) &&
                    !scenario.Player.isFocusInteractionAvailable,
                $"The released order counter did not present " +
                $"{(queuedCustomerCount > 0 ? "the remaining FIFO queue" : "its day phase")}.");
            if (queuedCustomerCount == 0 && scenario.Store.isStoreClosing)
            {
                Require(expectedCounterPrompt.Key ==
                        LocalizationKey.PromptCounterFinishDayAtControlTerminal,
                    "The last accepted closing-time customer did not leave the counter in its " +
                    "finish-day state.");
            }

            scenario.Player.ReplaceFocusedEntityId(scenario.ProcurementTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            GameEntity activeDelivery =
                runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                    scenario.ProcurementTerminal.EntityId);
            LocalizedText expectedTerminalPrompt = activeDelivery == null
                ? LocalizedTexts.Text(LocalizationKey.PromptOpenProcurement)
                : LocalizedTexts.Text(
                    LocalizationKey.PromptDeliveryBeingStocked,
                    LocalizedTexts.ProductName(activeDelivery.ProductType),
                    activeDelivery.StockedProductCount,
                    activeDelivery.DeliveryProductCount,
                    LocalizedTexts.ProductUnit(activeDelivery.ProductType));
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        expectedTerminalPrompt) &&
                    scenario.Player.isFocusInteractionAvailable == (activeDelivery == null),
                activeDelivery == null
                    ? "A delivery-free terminal did not stay available while the customer " +
                      "returned."
                    : "An active delivery did not keep its procurement terminal blocked " +
                      "while the customer returned.");

            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
            if (activeDelivery == null)
            {
                int moneyBeforeCatalog = scenario.Store.Money;
                OpenProcurement(runtime, scenario);
                ProcurementSnapshot snapshot = CaptureProcurementSnapshot(runtime, scenario);
                Require(snapshot.DemandKind == ProcurementDemandKind.ConfirmedOrder &&
                        snapshot.ProjectType == visit.CustomerProjectType &&
                        scenario.Store.Money == moneyBeforeCatalog &&
                        runtime.Game.GetEntityWithDeliveryProcurementTerminalEntityId(
                            scenario.ProcurementTerminal.EntityId) == null,
                    "Returning-customer procurement did not preserve the confirmed order " +
                    "demand.");
                CancelProcurement(runtime, scenario, moneyBeforeCatalog);
            }

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
                                LocalizationKey.PromptNoCustomerProductNotRequired)) &&
                        !scenario.Player.isFocusInteractionAvailable,
                    "Stock became pickable before the customer reached the loading bay.");
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
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId) &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "A customer spawned before cooldown elapsed.");
        }

        private static void ValidateClosingPreventsCustomerSpawn(
            Runtime runtime,
            Scenario scenario)
        {
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isStoreOpen &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
                "Closing customer gate requires no active visit or cooldown.");

            runtime.Systems.Create<TickCustomerCooldownSystem>().Execute();
            runtime.Systems.Create<SpawnCustomerVisitSystem>().Execute();
            Require(!scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId) &&
                    runtime.Game.GetGroup(GameMatcher.CustomerVisit).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.Customer).count == 0,
                "The closing store scheduled or spawned another customer.");

            scenario.Player.ReplaceFocusedEntityId(scenario.OrderCounter.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCounterFinishDayAtControlTerminal)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The empty closing order counter did not direct the player to finish the day.");
            scenario.Player.RemoveFocusedEntityId();
            ExecuteInteractionPrompts(runtime);
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
        }

        private static void ValidateDayReportAndStartNextDay(
            Runtime runtime,
            Scenario scenario,
            GameEntity trolley,
            int expectedMoneyBeforeOvernightDelivery,
            int expectedStock,
            int expectedRevenue,
            int expectedProcurementExpenses,
            int expectedUpgradeExpenses,
            int expectedCompletedOrders)
        {
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.isHandsOccupied &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    HasNoCustomerVisits(runtime.Game, scenario.Store.EntityId),
                "The day report requires an empty closing store.");

            DeliveryConfig overnightConfig = runtime.StaticData.GetDelivery(
                ProductTypeId.CementBag);
            DeliveryArrival overnightDelivery = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                ProductTypeId.CementBag);
            int expectedClosingMoney = checked(
                expectedMoneyBeforeOvernightDelivery - overnightConfig.TotalCost);
            int expectedClosingProcurementExpenses = checked(
                expectedProcurementExpenses + overnightConfig.TotalCost);
            int expectedPayrollExpenses = scenario.Store.DayPayrollExpenses;
            Require(scenario.Store.Money == expectedClosingMoney &&
                    scenario.Store.DayOpeningBalance + expectedRevenue -
                    expectedClosingProcurementExpenses - expectedUpgradeExpenses -
                    expectedPayrollExpenses ==
                    expectedClosingMoney &&
                    scenario.Store.DayRevenue == expectedRevenue &&
                    scenario.Store.DayProcurementExpenses ==
                    expectedClosingProcurementExpenses &&
                    scenario.Store.DayUpgradeExpenses == expectedUpgradeExpenses &&
                    scenario.Store.DayPayrollExpenses == expectedPayrollExpenses &&
                    scenario.Store.DayCompletedOrderCount == expectedCompletedOrders,
                "The final daily ledger does not reconcile successful rewards and purchases.");

            scenario.Player.ReplaceFocusedEntityId(trolley.EntityId);
            ExecuteInteractionPrompts(runtime);
            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<StartPushingTrolleySystem>().Execute();
            CleanupEvents(runtime);
            Require(scenario.Player.isHandsOccupied &&
                    scenario.Player.isPushingTrolley &&
                    trolley.TrolleyPusherEntityId == scenario.Player.EntityId,
                "The report hands guard requires a safely attached trolley.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCloseStoreHandsOccupied)) &&
                    !scenario.Player.isFocusInteractionAvailable,
                "The closing terminal did not explain that hands must be free.");
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            runtime.Systems.Create<OpenDayReportSystem>().Execute();
            Require(scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasDayReportStoreEntityId,
                "The report opened while the player was handling the trolley.");
            CleanupEvents(runtime);

            scenario.Input.isTrolleyPressed = true;
            runtime.Systems.Create<DetachPushedTrolleySystem>().Execute();
            CleanupEvents(runtime);
            Require(!scenario.Player.isHandsOccupied &&
                    !scenario.Player.isPushingTrolley &&
                    !trolley.hasTrolleyPusherEntityId && trolley.isInteractable,
                "The trolley did not detach before opening the report.");

            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            Require(PromptMatches(
                        runtime,
                        scenario.Player,
                        LocalizedTexts.Text(
                            LocalizationKey.PromptCloseStoreForReport)) &&
                    scenario.Player.isFocusInteractionAvailable,
                "The empty closing store did not offer its mandatory report.");
            RequestInteraction(scenario.Player, scenario.StoreControlTerminal);
            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<StoreDayFeature>().Execute();
            Require(scenario.Store.isDayReportOpen &&
                    !scenario.Store.isStoreClosing &&
                    scenario.Store.DayNumber == 1 &&
                    scenario.Player.isModalOpen &&
                    scenario.Player.hasDayReportStoreEntityId &&
                    scenario.Player.DayReportStoreEntityId == scenario.Store.EntityId &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithDayReportStoreEntityId(
                            scenario.Store.EntityId),
                        scenario.Player),
                "Simultaneous E and Enter skipped or failed to open the indexed mandatory " +
                "report modal.");
            CleanupEvents(runtime);

            var capture = new CaptureHudService();
            new PresentHudSystem(runtime.Game, runtime.StaticData, capture).Execute();
            new PresentDayReportSystem(runtime.Game, capture).Execute();
            Require(capture.Hud.HasValue &&
                    capture.Hud.Value.DayClock.Phase == StoreDayPhase.Report &&
                    capture.DayReport.HasValue,
                "Report phase did not reach HUD presentation.");
            DayClockSnapshot reportClock = capture.Hud.Value.DayClock;
            DayReportSnapshot report = capture.DayReport.Value;
            Require(report.DayNumber == 1 &&
                    report.OpeningBalance == scenario.Store.DayOpeningBalance &&
                    report.Revenue == expectedRevenue &&
                    report.ProcurementExpenses ==
                    expectedClosingProcurementExpenses &&
                    report.UpgradeExpenses == expectedUpgradeExpenses &&
                    report.PayrollExpenses == expectedPayrollExpenses &&
                    report.NetCashFlow == expectedRevenue -
                    expectedClosingProcurementExpenses - expectedUpgradeExpenses -
                    expectedPayrollExpenses &&
                    report.ClosingBalance == expectedClosingMoney &&
                    report.OpeningBalance + report.NetCashFlow ==
                    report.ClosingBalance &&
                    report.CompletedOrderCount == expectedCompletedOrders &&
                    report.LostCustomerCount == 0 &&
                    report.StorageProductCount == expectedStock,
                "The day report does not reconcile orders, revenue, expenses, cash flow, " +
                "balance and stock.");

            scenario.Input.isToggleCursorPressed = true;
            runtime.Systems.Create<ToggleCursorSystem>().Execute();
            runtime.Systems.Create<StoreDayFeature>().Execute();
            Require(scenario.Store.isDayReportOpen &&
                    scenario.Store.DayNumber == 1 &&
                    scenario.Player.isModalOpen &&
                    scenario.Player.hasDayReportStoreEntityId,
                "Esc closed or advanced the mandatory day report.");
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();

            int storeEntityId = scenario.Store.EntityId;
            int moneyBeforeNextDay = scenario.Store.Money;
            int stockBeforeNextDay = scenario.StorageZone.StorageProductCount;
            int[] stockProductIds = FindStockProducts(
                    runtime.Game,
                    scenario.StorageZone.EntityId)
                .Select(product => product.EntityId)
                .ToArray();
            int deliveryEntityId = overnightDelivery.Delivery.EntityId;
            int[] deliveryProductIds = overnightDelivery.Products
                .Select(product => product.EntityId)
                .ToArray();
            int trolleyEntityId = trolley.EntityId;
            int progressionBeforeNextDay = scenario.Store.CompletedOrderCount;
            bool trolleyUnlockedBeforeNextDay = scenario.Store.isTrolleyUpgradeUnlocked;
            int projectIndexBeforeNextDay = scenario.Store.NextProjectSequenceIndex;

            scenario.Input.isConfirmPressed = true;
            runtime.Systems.Create<StoreDayFeature>().Execute();
            Require(scenario.Store.DayNumber == 2 &&
                    scenario.Store.isStorePreparing &&
                    !scenario.Store.isStoreOpen &&
                    !scenario.Store.isStoreClosing &&
                    !scenario.Store.isDayReportOpen &&
                    Mathf.Approximately(
                        scenario.Store.CurrentDayMinute,
                        runtime.StaticData.StoreDay.StartMinute) &&
                    scenario.Store.DayOpeningBalance == moneyBeforeNextDay &&
                    scenario.Store.DayRevenue == 0 &&
                    scenario.Store.DayProcurementExpenses == 0 &&
                    scenario.Store.DayUpgradeExpenses == 0 &&
                    scenario.Store.DayPayrollExpenses == 0 &&
                    scenario.Store.DayCompletedOrderCount == 0 &&
                    scenario.Store.DayLostCustomerCount == 0 &&
                    !scenario.Store.hasCustomerCooldownRemaining &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Player.hasDayReportStoreEntityId,
                "Enter did not start Day 2 in clean 08:00 preparation state.");
            runtime.Systems.Create<StoreDayFeature>().Execute();
            Require(scenario.Store.DayNumber == 2,
                "One Enter advanced more than one store day.");
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();

            Require(scenario.Store.EntityId == storeEntityId &&
                    scenario.Store.Money == moneyBeforeNextDay &&
                    scenario.StorageZone.StorageProductCount == stockBeforeNextDay &&
                    FindStockProducts(runtime.Game, scenario.StorageZone.EntityId)
                        .Select(product => product.EntityId)
                        .SequenceEqual(stockProductIds) &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithEntityId(deliveryEntityId),
                        overnightDelivery.Delivery) &&
                    overnightDelivery.Delivery.isDeliveryActive &&
                    FindDeliveryProducts(runtime.Game, deliveryEntityId)
                        .Select(product => product.EntityId)
                        .SequenceEqual(deliveryProductIds) &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithEntityId(trolleyEntityId),
                        trolley) &&
                    scenario.Store.CompletedOrderCount == progressionBeforeNextDay &&
                    scenario.Store.isTrolleyUpgradeUnlocked ==
                    trolleyUnlockedBeforeNextDay &&
                    scenario.Store.NextProjectSequenceIndex == projectIndexBeforeNextDay,
                "Starting Day 2 changed money, stock, delivery, trolley, progression or project index.");
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();
            capture = new CaptureHudService();
            new PresentHudSystem(runtime.Game, runtime.StaticData, capture).Execute();
            new PresentDayReportSystem(runtime.Game, capture).Execute();
            Require(capture.Hud.HasValue &&
                    PrototypeHudView.ShouldStartNewDayFade(
                        reportClock,
                        capture.Hud.Value.DayClock) &&
                    !capture.DayReport.HasValue,
                "The real Report Day 1 -> Preparing Day 2 transition did not trigger the " +
                "semantic fade or left the report visible.");
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
                        LocalizationKey.HudDayClock,
                        1,
                        8,
                        0)) ==
                    "ДЕНЬ 1 • 08:00" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptStoreOpenUntil,
                        20,
                        0)) ==
                    "Магазин открыт до 20:00" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.WorldStoreControlTerminal)) ==
                    "УПРАВЛЕНИЕ МАГАЗИНОМ" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptCounterOpenStoreAtControlTerminal)) ==
                    "Магазин закрыт — откройте его у терминала управления" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptCounterFinishDayAtControlTerminal)) ==
                    "Новые клиенты не приедут — завершите день у терминала управления",
                "Russian store-day clock, phase prompt or neutral world label changed " +
                "arity/content.");
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
            string patienceProjectTitle = localization.Resolve(
                LocalizedTexts.ProjectTitle(CustomerProjectTypeId.CementFoundation));
            Require(localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.HudCustomerFlow,
                        3,
                        3,
                        0,
                        0)) ==
                    "Клиенты: 3 • очередь: 3 • ждут погрузки: 0 • уезжают: 0" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptCounterNextCustomerApproaching,
                        2)) ==
                    "Следующий клиент подходит • в очереди 2" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptDiscussProject,
                        LocalizedTexts.ProjectTitle(
                            CustomerProjectTypeId.CementFoundation))) ==
                    $"E — обсудить проект • {patienceProjectTitle}" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptFreeHandsForConsultation)) ==
                    "Освободите руки перед консультацией" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.PromptCustomerLeftImpatient)) ==
                    "Клиент ушёл из-за ожидания — машина покидает парковку" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.HudDayReportLostCustomers,
                        2)) ==
                    "Ушли из-за ожидания: 2" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationCustomerPatienceLow)) ==
                    "Клиент недоволен — он может уйти" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.NotificationCustomerLeftImpatient)) ==
                    "Клиент ушёл из-за долгого ожидания" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.WorldCustomerDissatisfied)) ==
                    "НЕДОВОЛЕН • МОЖЕТ УЙТИ" &&
                    localization.Resolve(LocalizedTexts.Text(
                        LocalizationKey.HudWarehouseWorkerMovingToCustomerLoading,
                        LocalizedTexts.ProductName(ProductTypeId.CementBag))) ==
                    "Грузчик несёт в машину клиента: Цемент 25 кг",
                "Russian customer-mood HUD, prompt, report, notification or world-label " +
                "localization, or the outbound-worker status, changed content or argument " +
                "arity.");
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
                container.Resolve<IInteractionPhysicsService>(),
                container.Resolve<ITrolleyMotionService>(),
                container.Resolve<ICustomerArrivalSchedule>(),
                container.Resolve<ILocalizationService>(),
                container.Resolve<IWarehouseTaskFactory>());
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

        private static GameEntity[] FindCustomerVisits(
            GameContext context,
            int storeEntityId) =>
            context.GetEntitiesWithCustomerVisitStoreEntityId(storeEntityId)
                .Where(visit => visit.isCustomerVisit && !visit.isDestructed)
                .OrderBy(visit => visit.CustomerArrivalSequence)
                .ToArray();

        private static bool HasNoCustomerVisits(
            GameContext context,
            int storeEntityId) =>
            FindCustomerVisits(context, storeEntityId).Length == 0;

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

        private static void RequireThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

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
                IInteractionPhysicsService interactionPhysics,
                ITrolleyMotionService trolleyMotion,
                ICustomerArrivalSchedule customerArrivalSchedule,
                ILocalizationService localization,
                IWarehouseTaskFactory warehouseTasks)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
                StaticData = staticData;
                ProcurementSolvency = procurementSolvency;
                EconomySolvency = economySolvency;
                InteractionPhysics = interactionPhysics;
                TrolleyMotion = trolleyMotion;
                CustomerArrivalSchedule = customerArrivalSchedule;
                Localization = localization;
                WarehouseTasks = warehouseTasks;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
            public IStaticDataService StaticData { get; }
            public IProcurementSolvencyService ProcurementSolvency { get; }
            public IEconomySolvencyService EconomySolvency { get; }
            public IInteractionPhysicsService InteractionPhysics { get; }
            public ITrolleyMotionService TrolleyMotion { get; }
            public ICustomerArrivalSchedule CustomerArrivalSchedule { get; }
            public ILocalizationService Localization { get; }
            public IWarehouseTaskFactory WarehouseTasks { get; }
        }

        private sealed class CaptureHudService : IHudService
        {
            public HudSnapshot? Hud { get; private set; }
            public ProcurementSnapshot? Procurement { get; private set; }
            public DayReportSnapshot? DayReport { get; private set; }

            public void Present(HudSnapshot snapshot) =>
                Hud = snapshot;

            public void PresentDayReport(DayReportSnapshot? snapshot) =>
                DayReport = snapshot;

            public void PresentConsultation(ConsultationSnapshot? snapshot)
            {
            }

            public void PresentProcurement(ProcurementSnapshot? snapshot) =>
                Procurement = snapshot;
        }

        private sealed class CaptureNotificationService : INotificationService
        {
            public LocalizedText Message { get; private set; }

            public void Show(LocalizedText message) =>
                Message = message ?? throw new ArgumentNullException(nameof(message));
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
                GameEntity storeControlTerminal,
                InputEntity input)
            {
                Player = player;
                Store = store;
                OrderCounter = orderCounter;
                ProcurementTerminal = procurementTerminal;
                StorageZone = storageZone;
                TrolleyUpgradeTerminal = trolleyUpgradeTerminal;
                StoreControlTerminal = storeControlTerminal;
                Input = input;
            }

            public GameEntity Player { get; }
            public GameEntity Store { get; }
            public GameEntity OrderCounter { get; }
            public GameEntity ProcurementTerminal { get; }
            public GameEntity StorageZone { get; }
            public GameEntity TrolleyUpgradeTerminal { get; }
            public GameEntity StoreControlTerminal { get; }
            public InputEntity Input { get; }
        }

        private sealed class FixedTimeService : ITimeService
        {
            public FixedTimeService(float deltaTime)
            {
                if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                    deltaTime < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(deltaTime));
                }

                DeltaTime = deltaTime;
            }

            public float DeltaTime { get; }
            public float UnscaledTime => 0f;
        }

        private sealed class RejectDestinationWorkerNavigationService :
            IWorkerNavigationService
        {
            public void Configure(UnityEngine.AI.NavMeshAgent agent,
                float speed, float acceleration, float angularSpeed,
                float stoppingDistance)
            {
            }

            public bool TryEnsurePlacedOnNavMesh(
                UnityEngine.AI.NavMeshAgent agent,
                Vector3 position,
                float sampleRadius) => true;

            public bool TrySetDestination(
                UnityEngine.AI.NavMeshAgent agent,
                Vector3 destination,
                float sampleRadius) => false;

            public WorkerNavigationStateId GetState(
                UnityEngine.AI.NavMeshAgent agent) =>
                WorkerNavigationStateId.PathUnavailable;

            public bool HasReachedDestination(
                UnityEngine.AI.NavMeshAgent agent,
                Vector3 currentPosition,
                Vector3 destination,
                float fallbackTolerance) => false;

            public void Stop(UnityEngine.AI.NavMeshAgent agent)
            {
            }
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
