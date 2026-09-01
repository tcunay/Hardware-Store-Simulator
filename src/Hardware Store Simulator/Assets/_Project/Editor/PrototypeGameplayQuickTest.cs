using System;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Employees.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Features.StoreDay.Systems;
using HardwareStore.Gameplay.Features.Trolley.Systems;
using UnityEditor;
using UnityEngine;

namespace HardwareStore.Editor
{
    public static partial class PrototypeGameplaySmokeTest
    {
        private const string QuickGameplayTestMenuPath =
            "Tools/Hardware Store/Prepare Quick Gameplay Test";
        private const int QuickGameplayTestMoneyGrant = 10_000;

        [MenuItem(QuickGameplayTestMenuPath, priority = 1)]
        public static void PrepareQuickGameplayTest()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);
            int moneyBeforeGrant = scenario.Store.Money;

            scenario.Store.money.Value = checked(
                moneyBeforeGrant + QuickGameplayTestMoneyGrant);
            runtime.Systems.Create<ReconcileEditorMoneyOverrideSystem>().Execute();
            runtime.Systems.Create<ValidateStoreDayStateSystem>().Execute();

            OpenStoreForSmoke(runtime, scenario);

            int unlockCompletedOrderCount = Math.Max(
                runtime.StaticData.PlatformTrolley.RequiredCompletedOrderCount,
                runtime.StaticData.WarehouseWorker.RequiredCompletedOrderCount);
            scenario.Store.ReplaceCompletedOrderCount(unlockCompletedOrderCount);
            runtime.Systems.Create<UnlockPlatformTrolleyUpgradeSystem>().Execute();
            runtime.Systems.Create<UnlockWarehouseWorkerHiringSystem>().Execute();
            Require(scenario.Store.isTrolleyUpgradeUnlocked &&
                    scenario.Store.isWarehouseWorkerHiringUnlocked,
                "Quick gameplay test did not unlock the trolley and warehouse worker.");
            CleanupEvents(runtime);

            GameEntity trolley = PurchaseTrolley(runtime, scenario);
            scenario.Player.ReplaceFocusedEntityId(
                scenario.StoreControlTerminal.EntityId);
            ExecuteInteractionPrompts(runtime);
            GameEntity worker = HireWarehouseWorker(runtime, scenario);
            DeliveryArrival arrival = PurchaseAndPrepareArrival(
                runtime,
                scenario,
                ProductTypeId.CementBag);

            runtime.Systems.Create<PresentHudSystem>().Execute();
            Selection.activeGameObject = worker.View.gameObject;

            Debug.Log(
                $"[Hardware Store] Quick gameplay test prepared: store open, " +
                $"{QuickGameplayTestMoneyGrant:N0} added, trolley {trolley.EntityId} " +
                $"purchased, worker {worker.EntityId} hired and delivery " +
                $"{arrival.Delivery.EntityId} purchased with " +
                $"{arrival.Products.Length} cement products. Balance: " +
                $"{moneyBeforeGrant:N0} -> {scenario.Store.Money:N0}.");
        }

        [MenuItem(QuickGameplayTestMenuPath, true)]
        private static bool CanPrepareQuickGameplayTest() =>
            EditorApplication.isPlaying && !EditorApplication.isCompiling;
    }
}
