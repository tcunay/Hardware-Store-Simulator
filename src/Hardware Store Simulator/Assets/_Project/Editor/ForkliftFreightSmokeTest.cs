using System;
using System.Collections.Generic;
using System.Linq;
using Entitas;
using HardwareStore.Common.Entity;
using HardwareStore.Gameplay.Common.Physics;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Features.Cameras.Systems;
using HardwareStore.Gameplay.Features.Cleanup.Systems;
using HardwareStore.Gameplay.Features.Forklift.Systems;
using HardwareStore.Gameplay.Features.Presentation.Systems;
using HardwareStore.Gameplay.Features.StoreSceneBindings.Systems;
using HardwareStore.Gameplay.Localization;
using HardwareStore.Infrastructure.Identifiers;
using HardwareStore.Infrastructure.States.GameStates;
using HardwareStore.Infrastructure.States.StateMachine;
using HardwareStore.Infrastructure.Systems;
using HardwareStore.Infrastructure.View;
using HardwareStore.Infrastructure.View.Systems;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace HardwareStore.Editor
{
    public static class ForkliftFreightSmokeTest
    {
        private const float PositionTolerance = 0.002f;
        private const float RotationTolerance = 0.05f;
        private const float TruckApproachDistance = 0.9f;
        private const float StagingApproachDistance = 0.55f;

        [MenuItem("Tools/Hardware Store/Run Forklift Freight Smoke Test")]
        public static void Run()
        {
            Runtime runtime = ResolveRuntime();
            Scenario scenario = ResolveFreshScenario(runtime);

            EnterForklift(runtime, scenario);
            ValidateVehicleCameraActivation(runtime, scenario);
            ValidateBlockedDriverExit(runtime, scenario);
            ValidateBlockedVerticalLift(runtime, scenario);
            ValidateFreeDrive(runtime, scenario);
            LiftForksToTruckSlot(runtime, scenario);
            PickUpTruckPallet(runtime, scenario);
            StageInboundPallet(runtime, scenario);
            ExitForklift(runtime, scenario);

            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
            ValidateRecoveryPreflightAtomicity(runtime, scenario);
            ValidateOccupiedCountPreflightAndIdempotence(
                runtime, scenario);
            ValidateMissingStagingFallback(runtime, scenario);
            ValidateBlockedDestructionDeferral(runtime, scenario);
            ValidateInverseBayRecovery();
            ValidateDestructionRecovery(runtime, scenario);
            ValidateFullStagingFallback(runtime, scenario);
            BeginOneFrameBlockedDestructionRecovery(runtime);
        }

        private static Scenario ResolveFreshScenario(Runtime runtime)
        {
            Require(runtime.StateMachine.ActiveStateType == typeof(StoreLoopState),
                $"The forklift smoke requires {nameof(StoreLoopState)}, but the active " +
                $"state is {runtime.StateMachine.ActiveStateType?.Name ?? "none"}.");

            runtime.Systems.Create<BindEntityViewFromSceneSystem>().Execute();
            runtime.Systems.Create<BindEntityViewFromPrefabSystem>().Execute();
            runtime.Systems.Create<ValidateStoreSceneBindingsSystem>().Execute();

            GameEntity player = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                    GameMatcher.EntityId,
                    GameMatcher.Player,
                    GameMatcher.StoreEntityId,
                    GameMatcher.Transform,
                    GameMatcher.CharacterController,
                    GameMatcher.MoveDirection,
                    GameMatcher.VerticalVelocity,
                    GameMatcher.HorizontalSpeed,
                    GameMatcher.Camera,
                    GameMatcher.ViewPivot,
                    GameMatcher.ViewPitch,
                    GameMatcher.CameraOrbitYaw,
                    GameMatcher.MouseSensitivity,
                    GameMatcher.GamepadLookSpeed)), "player");
            GameEntity forklift = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                        GameMatcher.EntityId,
                        GameMatcher.Forklift,
                        GameMatcher.ForkliftStoreEntityId,
                        GameMatcher.ForkliftForkHeight,
                        GameMatcher.ForkliftMinForkHeight,
                        GameMatcher.ForkliftMaxForkHeight,
                        GameMatcher.ForkliftLiftSpeed,
                        GameMatcher.ForkliftForwardSpeed,
                        GameMatcher.ForkliftReverseSpeed,
                        GameMatcher.ForkliftSteeringSpeed,
                        GameMatcher.Transform,
                        GameMatcher.Rigidbody,
                        GameMatcher.Colliders,
                        GameMatcher.DriverSeatAnchor,
                        GameMatcher.DriverExitAnchor,
                        GameMatcher.LiftTransform,
                        GameMatcher.CargoAnchor)
                    .NoneOf(GameMatcher.Destructed)), "forklift");
            GameEntity truck = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                        GameMatcher.EntityId,
                        GameMatcher.FreightTruck,
                        GameMatcher.FreightTruckStoreEntityId,
                        GameMatcher.PalletBay,
                        GameMatcher.OccupiedPalletSlotCount,
                        GameMatcher.Slots,
                        GameMatcher.Transform)
                    .NoneOf(GameMatcher.Destructed)), "freight truck");
            GameEntity staging = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                        GameMatcher.EntityId,
                        GameMatcher.FreightStagingZone,
                        GameMatcher.FreightStagingZoneStoreEntityId,
                        GameMatcher.PalletBay,
                        GameMatcher.OccupiedPalletSlotCount,
                        GameMatcher.Slots,
                        GameMatcher.Transform)
                    .NoneOf(GameMatcher.Destructed)), "freight staging zone");
            GameEntity pallet = RequireSingle(runtime.Game.GetGroup(
                GameMatcher.AllOf(
                        GameMatcher.EntityId,
                        GameMatcher.Pallet,
                        GameMatcher.InboundPallet,
                        GameMatcher.PalletStoreEntityId,
                        GameMatcher.PalletBayEntityId,
                        GameMatcher.PalletBaySlotIndex,
                        GameMatcher.Transform)
                    .NoneOf(GameMatcher.Destructed)), "inbound pallet");
            InputEntity input = RequireSingle(
                runtime.Input.GetGroup(InputMatcher.AllOf(
                    InputMatcher.InputState,
                    InputMatcher.LookInput)),
                "input state");

            var scenario = new Scenario(
                player, forklift, truck, staging, pallet, input);
            ValidateFreshFoundation(runtime, scenario);
            return scenario;
        }

        private static void ValidateFreshFoundation(Runtime runtime,
            Scenario scenario)
        {
            Require(scenario.Player.StoreEntityId ==
                    scenario.Forklift.ForkliftStoreEntityId &&
                    scenario.Truck.FreightTruckStoreEntityId ==
                    scenario.Forklift.ForkliftStoreEntityId &&
                    scenario.Staging.FreightStagingZoneStoreEntityId ==
                    scenario.Forklift.ForkliftStoreEntityId &&
                    scenario.Pallet.PalletStoreEntityId ==
                    scenario.Forklift.ForkliftStoreEntityId,
                "Forklift, freight foundation and player must belong to one store.");
            Require(!scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    !scenario.Player.isHandsOccupied &&
                    !scenario.Player.isModalOpen &&
                    !scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.isInteractable,
                "Forklift smoke requires an empty, parked and interactable forklift.");
            Require(scenario.Truck.Slots.Length == 4 &&
                    scenario.Truck.OccupiedPalletSlotCount == 1 &&
                    scenario.Staging.Slots.Length == 4 &&
                    scenario.Staging.OccupiedPalletSlotCount == 0 &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Truck.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    !scenario.Pallet.hasForkliftCarrierEntityId,
                "Forklift smoke requires one inbound pallet in truck slot 0 and four " +
                "empty staging slots.");
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0 &&
                    runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "Forklift smoke requires a clean request/event state.");

            Transform truckSlot = scenario.Truck.Slots[0];
            Pose truckPose = PrototypeYardLayoutSpec.FreightTruckPose;
            Vector3 expectedTruckSlotPosition = truckPose.position +
                                                truckPose.rotation *
                                                new Vector3(0f, 1.11f, -4.5f);
            Quaternion expectedTruckSlotRotation = truckPose.rotation *
                                                   Quaternion.Euler(0f, -90f, 0f);
            Require(Quaternion.Angle(
                        truckSlot.rotation,
                        expectedTruckSlotRotation) < 0.01f &&
                    Vector3.Distance(
                        truckSlot.position,
                        expectedTruckSlotPosition) < 0.05f,
                "Truck slot 0 must preserve its authored side-loading pose relative to " +
                $"the freight-truck layout pose at {truckPose.position}.");

            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void EnterForklift(Runtime runtime,
            Scenario scenario)
        {
            scenario.Input.isInteractPressed = false;
            scenario.Player.ReplaceFocusedEntityId(scenario.Forklift.EntityId);
            scenario.Player.ReplaceFocusedInteractionType(
                InteractionTypeId.Forklift);
            scenario.Player.isFocusInteractionAvailable = true;

            GameEntity request = CreateEntity.Empty();
            request.isInteractionRequest = true;
            request.AddSourceEntityId(scenario.Player.EntityId);
            request.AddTargetEntityId(scenario.Forklift.EntityId);
            runtime.Systems.Create<ToggleForkliftDrivingSystem>().Execute();
            CleanupRequests(runtime);

            Require(scenario.Player.isDrivingForklift &&
                    !scenario.Player.CharacterController.enabled &&
                    scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.ForkliftDriverEntityId ==
                    scenario.Player.EntityId &&
                    !scenario.Forklift.isInteractable &&
                    Vector3.Distance(
                    scenario.Player.Transform.position,
                    scenario.Forklift.DriverSeatAnchor.position) <
                    PositionTolerance,
                "Forklift interaction did not seat exactly one driver and disable the " +
                "player CharacterController.");
        }

        private static void ValidateVehicleCameraActivation(
            Runtime runtime, Scenario scenario)
        {
            Require(scenario.Forklift.Rigidbody.interpolation ==
                    RigidbodyInterpolation.None,
                "Forklift Rigidbody interpolation must be disabled for deterministic " +
                "Transform/Rigidbody lockstep movement.");

            runtime.Systems.Create<ActivateThirdPersonCameraSystem>().Execute();
            Require(scenario.Player.isThirdPersonCameraActive,
                "Entering the forklift did not activate the third-person camera mode.");

            bool pointerLookBefore = scenario.Input.isPointerLook;
            Vector2 lookBefore = scenario.Input.LookInput;
            float yawBefore = scenario.Player.CameraOrbitYaw;
            try
            {
                scenario.Input.isPointerLook = true;
                scenario.Input.ReplaceLookInput(new Vector2(18f, -8f));
                runtime.Systems.Create<UpdateThirdPersonCameraSystem>().Execute();
            }
            finally
            {
                scenario.Input.ReplaceLookInput(lookBefore);
                scenario.Input.isPointerLook = pointerLookBefore;
            }

            Transform cameraTransform = scenario.Player.Camera.transform;
            float cameraDistance = Vector3.Distance(
                scenario.Player.ViewPivot.position,
                cameraTransform.position);
            bool cameraInsideVehicle = scenario.Forklift.Colliders
                .Where(collider => collider != null && collider.enabled &&
                                   !collider.isTrigger)
                .Any(collider => collider.bounds.Contains(
                    cameraTransform.position));
            Require(cameraTransform.parent == scenario.Player.ViewPivot &&
                    cameraDistance > 1.5f &&
                    !cameraInsideVehicle &&
                    Mathf.Abs(scenario.Player.CameraOrbitYaw - yawBefore) >
                    0.01f,
                "Forklift camera did not move outside the cab or react to orbit input.");
        }

        private static void ValidateBlockedDriverExit(Runtime runtime,
            Scenario scenario)
        {
            GameObject obstacle = CreateDriverExitObstacle(
                scenario,
                "Smoke Blocked Forklift Exit");

            try
            {
                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<ToggleForkliftDrivingSystem>().Execute();
            }
            finally
            {
                scenario.Input.isInteractPressed = false;
                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
            }

            GameEntity notification = RequireSingle(
                runtime.Game.GetGroup(GameMatcher.NotificationMessage),
                "blocked forklift exit notification");
            Require(notification.NotificationMessage.Key ==
                    LocalizationKey.NotificationForkliftExitBlocked &&
                    scenario.Player.isDrivingForklift &&
                    !scenario.Player.CharacterController.enabled &&
                    scenario.Forklift.hasForkliftDriverEntityId &&
                    !scenario.Forklift.isInteractable,
                "A blocked forklift exit changed the driver relation or omitted its " +
                "notification.");
            runtime.Systems.Create<PresentNotificationsSystem>().Execute();
            Require(runtime.Game.GetGroup(GameMatcher.NotificationMessage).count == 0,
                "Blocked forklift exit notification survived presentation.");
        }

        private static GameObject CreateDriverExitObstacle(
            Scenario scenario, string name)
            => CreateDriverExitObstacle(
                scenario.Player.CharacterController,
                scenario.Player.Transform,
                scenario.Forklift.DriverExitAnchor,
                name);

        private static GameObject CreateDriverExitObstacle(
            CharacterController controller, Transform driverTransform,
            Transform exitAnchor, string name)
        {
            GameObject obstacle = new(name);
            BoxCollider obstacleCollider = obstacle.AddComponent<BoxCollider>();
            Vector3 scale = driverTransform.lossyScale;
            float horizontalScale = Mathf.Max(
                Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float verticalScale = Mathf.Abs(scale.y);
            obstacle.transform.SetPositionAndRotation(
                exitAnchor.position + exitAnchor.rotation *
                Vector3.Scale(controller.center, scale),
                exitAnchor.rotation);
            obstacleCollider.size = new Vector3(
                controller.radius * horizontalScale * 2f + 0.2f,
                controller.height * verticalScale + 0.2f,
                controller.radius * horizontalScale * 2f + 0.2f);
            Require(!Physics.GetIgnoreLayerCollision(
                        driverTransform.gameObject.layer,
                        obstacle.layer) &&
                    !Physics.GetIgnoreCollision(controller, obstacleCollider),
                "Blocked-exit smoke obstacle must collide with the player layer.");
            Physics.SyncTransforms();
            return obstacle;
        }

        private static void ValidateBlockedVerticalLift(Runtime runtime,
            Scenario scenario)
        {
            BoxCollider liftHull = scenario.Forklift.Colliders
                .OfType<BoxCollider>()
                .Where(collider => collider.enabled && !collider.isTrigger &&
                                   collider.transform.IsChildOf(
                                       scenario.Forklift.LiftTransform))
                .OrderByDescending(collider => collider.bounds.max.y)
                .FirstOrDefault();
            Require(liftHull != null,
                "Blocked-lift smoke requires one enabled solid lift BoxCollider.");

            const float gap = 0.01f;
            const float blockerHeight = 0.1f;
            GameObject obstacle = new("Smoke Blocked Forklift Lift");
            BoxCollider obstacleCollider = obstacle.AddComponent<BoxCollider>();
            obstacle.transform.SetPositionAndRotation(
                liftHull.bounds.center + Vector3.up *
                (liftHull.bounds.extents.y + gap + blockerHeight * 0.5f),
                liftHull.transform.rotation);
            obstacleCollider.size = new Vector3(
                liftHull.bounds.size.x + 0.2f,
                blockerHeight,
                liftHull.bounds.size.z + 0.2f);
            Require(!Physics.GetIgnoreLayerCollision(
                        liftHull.gameObject.layer,
                        obstacle.layer) &&
                    !Physics.GetIgnoreCollision(liftHull, obstacleCollider),
                "Blocked-lift smoke obstacle must collide with the lift hull layer.");
            Physics.SyncTransforms();

            float componentHeightBefore =
                scenario.Forklift.ForkliftForkHeight;
            float transformHeightBefore =
                scenario.Forklift.LiftTransform.localPosition.y;
            try
            {
                scenario.Input.ReplaceForkliftLiftInput(1f);
                new AdjustForkliftLiftSystem(
                        runtime.Game,
                        runtime.Input,
                        new FixedTimeService(0.4f),
                        runtime.Motion)
                    .Execute();
            }
            finally
            {
                scenario.Input.ReplaceForkliftLiftInput(0f);
                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
            }

            Require(Mathf.Abs(scenario.Forklift.ForkliftForkHeight -
                              componentHeightBefore) < PositionTolerance &&
                    Mathf.Abs(scenario.Forklift.LiftTransform.localPosition.y -
                              transformHeightBefore) < PositionTolerance,
                "A collision-blocked vertical lift changed its ECS height or view pose.");
        }

        private static void ValidateFreeDrive(Runtime runtime,
            Scenario scenario)
        {
            Vector3 positionBefore = scenario.Forklift.Transform.position;
            scenario.Input.ReplaceMoveInput(Vector2.up);
            new DriveForkliftSystem(
                    runtime.Game,
                    runtime.Input,
                    runtime.Motion,
                    new FixedTimeService(0.25f))
                .Execute();
            scenario.Input.ReplaceMoveInput(Vector2.zero);

            float travelled = Vector3.Distance(
                positionBefore, scenario.Forklift.Transform.position);
            Require(travelled > 0.25f &&
                    Vector3.Distance(
                        scenario.Forklift.Transform.position,
                        scenario.Forklift.Rigidbody.position) < PositionTolerance &&
                    Vector3.Distance(
                        scenario.Player.Transform.position,
                        scenario.Forklift.DriverSeatAnchor.position) <
                    PositionTolerance,
                "Forklift did not drive freely with a seated driver and synchronized " +
                "Transform/Rigidbody poses.");
        }

        private static void LiftForksToTruckSlot(Runtime runtime,
            Scenario scenario)
        {
            Transform slot = scenario.Truck.Slots[0];
            float targetHeight = Mathf.Clamp(
                scenario.Forklift.ForkliftForkHeight +
                slot.position.y - scenario.Forklift.CargoAnchor.position.y,
                scenario.Forklift.ForkliftMinForkHeight,
                scenario.Forklift.ForkliftMaxForkHeight);
            AdjustForkHeight(runtime, scenario, targetHeight);

            Require(Mathf.Abs(
                        scenario.Forklift.CargoAnchor.position.y -
                        slot.position.y) < 0.01f,
                "Forklift lift did not align the cargo anchor with truck slot 0.");
        }

        private static void PickUpTruckPallet(Runtime runtime,
            Scenario scenario)
        {
            Transform slot = scenario.Truck.Slots[0];
            SetForkliftCargoApproach(
                runtime,
                scenario,
                slot,
                TruckApproachDistance);
            Require(scenario.Forklift.CargoAnchor.position.x < slot.position.x &&
                    Vector3.Dot(
                        scenario.Forklift.CargoAnchor.forward,
                        Vector3.right) > 0.999f,
                "Forklift must approach truck slot 0 from the open west side while " +
                "facing world +X.");

            PulsePalletTransfer(runtime, scenario);
            Require(scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Pallet.ForkliftCarrierEntityId ==
                    scenario.Forklift.EntityId &&
                    !scenario.Pallet.hasPalletBayEntityId &&
                    !scenario.Pallet.hasPalletBaySlotIndex &&
                    scenario.Truck.OccupiedPalletSlotCount == 0 &&
                    Vector3.Distance(
                        scenario.Pallet.Transform.position,
                        scenario.Forklift.CargoAnchor.position) <
                    PositionTolerance,
                "Forklift did not pick the inbound pallet from accessible truck slot 0.");
        }

        private static void StageInboundPallet(Runtime runtime,
            Scenario scenario)
        {
            Transform stagingSlot = scenario.Staging.Slots[0];
            SetForkliftCargoApproach(
                runtime,
                scenario,
                stagingSlot,
                StagingApproachDistance);
            runtime.Systems.Create<FollowForkliftCarriedPalletSystem>()
                .Execute();
            AdjustForkHeight(
                runtime,
                scenario,
                scenario.Forklift.ForkliftMinForkHeight);
            runtime.Systems.Create<FollowForkliftCarriedPalletSystem>()
                .Execute();

            PulsePalletTransfer(runtime, scenario);
            runtime.Systems.Create<CompleteInboundPalletStagingSystem>()
                .Execute();
            runtime.Systems.Create<FollowPalletBayPlacementSystem>().Execute();

            Require(!scenario.Pallet.isInboundPallet &&
                    !scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    scenario.Staging.OccupiedPalletSlotCount == 1 &&
                    Vector3.Distance(
                        scenario.Pallet.Transform.position,
                        stagingSlot.position) < PositionTolerance,
                "Forklift did not place and complete the inbound pallet in staging slot 0.");
        }

        private static void ExitForklift(Runtime runtime,
            Scenario scenario)
        {
            scenario.Input.isInteractPressed = true;
            runtime.Systems.Create<ToggleForkliftDrivingSystem>().Execute();
            scenario.Input.isInteractPressed = false;

            runtime.Systems.Create<DeactivateThirdPersonCameraSystem>()
                .Execute();

            Require(!scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    !scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.isInteractable &&
                    !scenario.Player.isThirdPersonCameraActive &&
                    Quaternion.Angle(
                        scenario.Player.ViewPivot.localRotation,
                        Quaternion.identity) < RotationTolerance &&
                    scenario.Player.Camera.transform.localPosition.sqrMagnitude <
                    PositionTolerance * PositionTolerance &&
                    Quaternion.Angle(
                        scenario.Player.Camera.transform.localRotation,
                        Quaternion.identity) < RotationTolerance,
                "Forklift driver did not exit cleanly or restore the first-person " +
                "camera pose.");
        }

        private static void ValidateRecoveryPreflightAtomicity(
            Runtime runtime, Scenario scenario)
        {
            Require(!scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    !scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.isInteractable &&
                    !scenario.Forklift.isHighlighted &&
                    !scenario.Pallet.isDestructed &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    scenario.Staging.OccupiedPalletSlotCount == 1,
                "Recovery preflight atomicity requires the completed happy-path state.");

            int occupiedCountBefore =
                scenario.Staging.OccupiedPalletSlotCount;
            int invalidOccupiedCount = scenario.Staging.Slots.Length + 7;
            Vector3 palletPositionBefore =
                scenario.Pallet.Transform.position;
            Quaternion palletRotationBefore =
                scenario.Pallet.Transform.rotation;
            GameEntity wrongOwner =
                CreateEntity.Empty(runtime.Identifiers.Next());
            try
            {
                wrongOwner.AddForkliftCarrierEntityId(
                    scenario.Forklift.EntityId);
                scenario.Staging.ReplaceOccupiedPalletSlotCount(
                    invalidOccupiedCount);
                scenario.Forklift.isHighlighted = true;

                RequireThrows<InvalidOperationException>(
                    () => runtime.Systems
                        .Create<RecoverDestructedForkliftStateSystem>()
                        .Execute(),
                    "A live non-pallet ForkliftCarrierEntityId owner passed recovery " +
                    "preflight validation.");

                Require(wrongOwner.hasForkliftCarrierEntityId &&
                        wrongOwner.ForkliftCarrierEntityId ==
                        scenario.Forklift.EntityId &&
                        ReferenceEquals(
                            runtime.Game.GetEntityWithForkliftCarrierEntityId(
                                scenario.Forklift.EntityId),
                            wrongOwner) &&
                        scenario.Staging.OccupiedPalletSlotCount ==
                        invalidOccupiedCount &&
                        scenario.Forklift.isHighlighted &&
                        scenario.Forklift.isInteractable &&
                        !scenario.Forklift.hasForkliftDriverEntityId &&
                        !scenario.Player.isDrivingForklift &&
                        scenario.Player.CharacterController.enabled &&
                        !scenario.Pallet.isDestructed &&
                        !scenario.Pallet.hasForkliftCarrierEntityId &&
                        scenario.Pallet.PalletBayEntityId ==
                        scenario.Staging.EntityId &&
                        scenario.Pallet.PalletBaySlotIndex == 0 &&
                        Vector3.Distance(
                            scenario.Pallet.Transform.position,
                            palletPositionBefore) < PositionTolerance &&
                        Quaternion.Angle(
                            scenario.Pallet.Transform.rotation,
                            palletRotationBefore) < RotationTolerance,
                    "Recovery preflight failure partially mutated a relation, bay count, " +
                    "driver, forklift or pallet before throwing.");
            }
            finally
            {
                if (wrongOwner.isEnabled)
                    wrongOwner.Destroy();
                scenario.Staging.ReplaceOccupiedPalletSlotCount(
                    occupiedCountBefore);
                scenario.Forklift.isHighlighted = false;
            }

            Require(runtime.Game.GetEntityWithForkliftCarrierEntityId(
                        scenario.Forklift.EntityId) == null,
                "Wrong-owner preflight fixture survived cleanup.");
            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void ValidateOccupiedCountPreflightAndIdempotence(
            Runtime runtime, Scenario scenario)
        {
            int occupiedCountBefore =
                scenario.Staging.OccupiedPalletSlotCount;
            Vector3 palletPositionBefore =
                scenario.Pallet.Transform.position;
            Quaternion palletRotationBefore =
                scenario.Pallet.Transform.rotation;
            scenario.Staging.ReplaceOccupiedPalletSlotCount(-13);

            scenario.Forklift.isHighlighted = true;
            try
            {
                RequireThrows<InvalidOperationException>(
                    () => runtime.Systems
                        .Create<RecoverDestructedForkliftStateSystem>()
                        .Execute(),
                    "An out-of-range OccupiedPalletSlotCount passed recovery " +
                    "preflight validation.");
                Require(scenario.Staging.OccupiedPalletSlotCount == -13 &&
                        scenario.Forklift.isHighlighted &&
                        !scenario.Pallet.isDestructed &&
                        scenario.Pallet.PalletBayEntityId ==
                        scenario.Staging.EntityId &&
                        scenario.Pallet.PalletBaySlotIndex == 0 &&
                        Vector3.Distance(
                            scenario.Pallet.Transform.position,
                            palletPositionBefore) < PositionTolerance &&
                        Quaternion.Angle(
                            scenario.Pallet.Transform.rotation,
                            palletRotationBefore) < RotationTolerance,
                    "Invalid occupied-count preflight partially mutated the healthy " +
                    "freight graph before throwing.");
            }
            finally
            {
                scenario.Staging.ReplaceOccupiedPalletSlotCount(
                    occupiedCountBefore);
                scenario.Forklift.isHighlighted = false;
            }

            RecoverDestructedForkliftStateSystem recovery =
                runtime.Systems.Create<RecoverDestructedForkliftStateSystem>();
            recovery.Execute();
            RequireHealthyStagedPallet(
                scenario,
                palletPositionBefore,
                palletRotationBefore,
                "Occupied-count recovery Execute");

            recovery.Cleanup();
            RequireHealthyStagedPallet(
                scenario,
                palletPositionBefore,
                palletRotationBefore,
                "Occupied-count recovery Cleanup after Execute");
            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void ValidateMissingStagingFallback(
            Runtime runtime, Scenario scenario)
        {
            ValidateUnavailableStagingFallback(
                runtime,
                scenario,
                createDestructedStaging: false,
                "missing staging");
            ValidateUnavailableStagingFallback(
                runtime,
                scenario,
                createDestructedStaging: true,
                "destructed staging");
            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void ValidateUnavailableStagingFallback(
            Runtime runtime, Scenario scenario,
            bool createDestructedStaging, string role)
        {
            int missingStoreEntityId = runtime.Identifiers.Next();
            int missingCarrierEntityId = runtime.Identifiers.Next();
            Require(runtime.Game.GetEntityWithFreightStagingZoneStoreEntityId(
                        missingStoreEntityId) == null &&
                    runtime.Game.GetEntityWithEntityId(
                        missingCarrierEntityId) == null,
                "Missing-staging smoke IDs unexpectedly resolve to live entities.");

            GameObject orphanView = new("Smoke Missing Staging Pallet");
            orphanView.transform.SetPositionAndRotation(
                scenario.Staging.Slots[0].position + Vector3.forward * 6f,
                scenario.Staging.Slots[0].rotation);
            GameEntity orphan = null;
            GameEntity unavailableStaging = null;
            GameObject unavailableSlot = null;
            Vector3 mainPalletPosition = scenario.Pallet.Transform.position;
            Quaternion mainPalletRotation = scenario.Pallet.Transform.rotation;
            try
            {
                if (createDestructedStaging)
                {
                    unavailableSlot = new GameObject(
                        "Smoke Destructed Staging Slot");
                    unavailableSlot.transform.SetPositionAndRotation(
                        orphanView.transform.position + Vector3.right * 2f,
                        orphanView.transform.rotation);
                    unavailableStaging =
                        CreateEntity.Empty(runtime.Identifiers.Next());
                    unavailableStaging.isPalletBay = true;
                    unavailableStaging.isFreightStagingZone = true;
                    unavailableStaging.AddFreightStagingZoneStoreEntityId(
                        missingStoreEntityId);
                    unavailableStaging.AddSlots(new[]
                    {
                        unavailableSlot.transform
                    });
                    unavailableStaging.AddOccupiedPalletSlotCount(0);
                    unavailableStaging.isDestructed = true;
                }

                orphan = CreateEntity.Empty(runtime.Identifiers.Next());
                orphan.isPallet = true;
                orphan.AddPalletStoreEntityId(missingStoreEntityId);
                orphan.AddForkliftCarrierEntityId(missingCarrierEntityId);
                orphan.AddTransform(orphanView.transform);

                RecoverDestructedForkliftStateSystem recovery =
                    runtime.Systems
                        .Create<RecoverDestructedForkliftStateSystem>();
                recovery.Execute();
                RequireMissingStagingFallback(
                    orphan,
                    scenario,
                    mainPalletPosition,
                    mainPalletRotation,
                    $"{role} recovery Execute");
                Require(!createDestructedStaging ||
                        unavailableStaging.isDestructed &&
                        unavailableStaging.OccupiedPalletSlotCount == 0,
                    $"{role} recovery mutated its unavailable staging endpoint.");

                recovery.Cleanup();
                RequireMissingStagingFallback(
                    orphan,
                    scenario,
                    mainPalletPosition,
                    mainPalletRotation,
                    $"{role} recovery Cleanup after Execute");
                Require(!createDestructedStaging ||
                        unavailableStaging.isDestructed &&
                        unavailableStaging.OccupiedPalletSlotCount == 0,
                    $"{role} recovery Cleanup mutated its unavailable staging endpoint.");
            }
            finally
            {
                if (orphan != null && orphan.isEnabled)
                    orphan.Destroy();
                if (unavailableStaging != null && unavailableStaging.isEnabled)
                    unavailableStaging.Destroy();
                UnityEngine.Object.DestroyImmediate(orphanView);
                if (unavailableSlot != null)
                    UnityEngine.Object.DestroyImmediate(unavailableSlot);
                Physics.SyncTransforms();
            }
        }

        private static void RequireHealthyStagedPallet(
            Scenario scenario, Vector3 expectedPosition,
            Quaternion expectedRotation, string phase)
        {
            Require(scenario.Staging.OccupiedPalletSlotCount == 1 &&
                    scenario.Truck.OccupiedPalletSlotCount == 0 &&
                    !scenario.Pallet.isDestructed &&
                    !scenario.Pallet.isInboundPallet &&
                    !scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    Vector3.Distance(
                        scenario.Pallet.Transform.position,
                        expectedPosition) < PositionTolerance &&
                    Quaternion.Angle(
                        scenario.Pallet.Transform.rotation,
                        expectedRotation) < RotationTolerance &&
                    !scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    !scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.isInteractable,
                $"{phase} did not preserve the deterministic healthy staged-pallet " +
                "state.");
        }

        private static void RequireMissingStagingFallback(
            GameEntity orphan, Scenario scenario,
            Vector3 expectedMainPalletPosition,
            Quaternion expectedMainPalletRotation, string phase)
        {
            Require(orphan.isDestructed &&
                    !orphan.hasForkliftCarrierEntityId &&
                    !orphan.hasPalletBayEntityId &&
                    !orphan.hasPalletBaySlotIndex &&
                    !orphan.isInboundPallet &&
                    scenario.Staging.OccupiedPalletSlotCount == 1 &&
                    !scenario.Pallet.isDestructed &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    Vector3.Distance(
                        scenario.Pallet.Transform.position,
                        expectedMainPalletPosition) < PositionTolerance &&
                    Quaternion.Angle(
                        scenario.Pallet.Transform.rotation,
                        expectedMainPalletRotation) < RotationTolerance,
                $"{phase} did not destroy only the orphaned pallet while preserving " +
                "the healthy store freight graph.");
        }

        private static void ValidateInverseBayRecovery()
        {
            ValidateDestructedBayInverseRecovery();
            ValidateDestructedPalletInverseRecovery();
        }

        private static void ValidateDestructedBayInverseRecovery()
        {
            GameContext game = new Contexts().game;
            GameObject slotView = new("Smoke Destructed Bay Slot");
            GameObject palletView = new("Smoke Destructed Bay Pallet");
            const int storeEntityId = 7001;
            GameEntity bay = game.CreateEntity();
            GameEntity pallet = game.CreateEntity();
            try
            {
                bay.AddEntityId(1);
                bay.isPalletBay = true;
                bay.isFreightStagingZone = true;
                bay.AddFreightStagingZoneStoreEntityId(storeEntityId);
                bay.AddSlots(new[] { slotView.transform });
                bay.AddOccupiedPalletSlotCount(1);
                bay.isDestructed = true;

                pallet.AddEntityId(2);
                pallet.isPallet = true;
                pallet.AddPalletStoreEntityId(storeEntityId);
                pallet.AddPalletBayEntityId(bay.EntityId);
                pallet.AddPalletBaySlotIndex(0);
                pallet.AddTransform(palletView.transform);

                RecoverDestructedForkliftStateSystem recovery = new(
                    game, new ForkliftMotionService(
                        new TrolleyMotionService()));
                recovery.Execute();
                recovery.Cleanup();

                Require(pallet.isDestructed &&
                        !pallet.hasPalletBayEntityId &&
                        !pallet.hasPalletBaySlotIndex &&
                        !pallet.hasForkliftCarrierEntityId &&
                        bay.OccupiedPalletSlotCount == 1,
                    "Destructed bay inverse recovery did not detach and destroy " +
                    "its live related pallet idempotently.");
            }
            finally
            {
                if (pallet.isEnabled)
                    pallet.Destroy();
                if (bay.isEnabled)
                    bay.Destroy();
                UnityEngine.Object.DestroyImmediate(palletView);
                UnityEngine.Object.DestroyImmediate(slotView);
            }
        }

        private static void ValidateDestructedPalletInverseRecovery()
        {
            GameContext game = new Contexts().game;
            GameObject slotView = new("Smoke Live Bay Slot");
            GameObject palletView = new("Smoke Destructed Pallet");
            const int storeEntityId = 7002;
            GameEntity bay = game.CreateEntity();
            GameEntity pallet = game.CreateEntity();
            try
            {
                bay.AddEntityId(1);
                bay.isPalletBay = true;
                bay.isFreightStagingZone = true;
                bay.AddFreightStagingZoneStoreEntityId(storeEntityId);
                bay.AddSlots(new[] { slotView.transform });
                bay.AddOccupiedPalletSlotCount(1);

                pallet.AddEntityId(2);
                pallet.isPallet = true;
                pallet.AddPalletStoreEntityId(storeEntityId);
                pallet.AddPalletBayEntityId(bay.EntityId);
                pallet.AddPalletBaySlotIndex(0);
                pallet.AddTransform(palletView.transform);
                pallet.isDestructed = true;

                RecoverDestructedForkliftStateSystem recovery = new(
                    game, new ForkliftMotionService(
                        new TrolleyMotionService()));
                recovery.Execute();
                recovery.Cleanup();

                Require(pallet.isDestructed &&
                        !pallet.hasPalletBayEntityId &&
                        !pallet.hasPalletBaySlotIndex &&
                        !pallet.hasForkliftCarrierEntityId &&
                        bay.OccupiedPalletSlotCount == 0,
                    "Destructed pallet inverse recovery did not release its live " +
                    "bay slot and recount occupancy idempotently.");
            }
            finally
            {
                if (pallet.isEnabled)
                    pallet.Destroy();
                if (bay.isEnabled)
                    bay.Destroy();
                UnityEngine.Object.DestroyImmediate(palletView);
                UnityEngine.Object.DestroyImmediate(slotView);
            }
        }

        private static void ValidateBlockedDestructionDeferral(
            Runtime runtime, Scenario scenario)
        {
            Vector3 palletPositionBefore =
                scenario.Pallet.Transform.position;
            Quaternion palletRotationBefore =
                scenario.Pallet.Transform.rotation;
            EnterForklift(runtime, scenario);
            GameObject obstacle = CreateDriverExitObstacle(
                scenario,
                "Smoke Blocked Destructed Forklift Exit");

            try
            {
                scenario.Forklift.isDestructed = true;
                RecoverDestructedForkliftStateSystem recovery =
                    runtime.Systems
                        .Create<RecoverDestructedForkliftStateSystem>();
                recovery.Execute();
                RequireBlockedDestructionDeferralState(
                    runtime,
                    scenario,
                    palletPositionBefore,
                    palletRotationBefore,
                    "blocked-destruction recovery Execute");

                recovery.Cleanup();
                RequireBlockedDestructionDeferralState(
                    runtime,
                    scenario,
                    palletPositionBefore,
                    palletRotationBefore,
                    "blocked-destruction recovery Cleanup after Execute");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
                RestoreBlockedDestructionFixture(runtime, scenario);
            }

            RequireHealthyStagedPallet(
                scenario,
                palletPositionBefore,
                palletRotationBefore,
                "Blocked-destruction fixture restoration");
            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void RequireBlockedDestructionDeferralState(
            Runtime runtime, Scenario scenario,
            Vector3 expectedPalletPosition,
            Quaternion expectedPalletRotation, string phase)
        {
            Require(scenario.Forklift.isDestructed &&
                    !scenario.Forklift.isInteractable &&
                    scenario.Forklift.hasForkliftDriverEntityId &&
                    scenario.Forklift.ForkliftDriverEntityId ==
                    scenario.Player.EntityId &&
                    ReferenceEquals(
                        runtime.Game.GetEntityWithForkliftDriverEntityId(
                            scenario.Player.EntityId),
                        scenario.Forklift) &&
                    scenario.Player.isDrivingForklift &&
                    !scenario.Player.CharacterController.enabled &&
                    Vector3.Distance(
                        scenario.Player.Transform.position,
                        scenario.Forklift.DriverSeatAnchor.position) <
                    PositionTolerance &&
                    !scenario.Pallet.isDestructed &&
                    !scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    scenario.Staging.OccupiedPalletSlotCount == 1 &&
                    Vector3.Distance(
                        scenario.Pallet.Transform.position,
                        expectedPalletPosition) < PositionTolerance &&
                    Quaternion.Angle(
                        scenario.Pallet.Transform.rotation,
                        expectedPalletRotation) < RotationTolerance,
                $"{phase} did not defer driver release atomically while the " +
                "destructed forklift exit remained blocked.");
        }

        private static void RestoreBlockedDestructionFixture(
            Runtime runtime, Scenario scenario)
        {
            scenario.Forklift.isDestructed = false;
            scenario.Input.isInteractPressed = false;
            if (scenario.Forklift.hasForkliftDriverEntityId &&
                scenario.Player.isDrivingForklift &&
                !scenario.Player.CharacterController.enabled)
            {
                scenario.Input.isInteractPressed = true;
                runtime.Systems.Create<ToggleForkliftDrivingSystem>().Execute();
                scenario.Input.isInteractPressed = false;
            }

            if (!scenario.Player.CharacterController.enabled)
            {
                bool exited = runtime.Motion.TryExitDriver(
                    scenario.Player.CharacterController,
                    scenario.Player.Transform,
                    scenario.Forklift.DriverExitAnchor,
                    scenario.Forklift.Transform);
                Require(exited,
                    "Blocked-destruction smoke could not restore the driver " +
                    "through a collision-safe exit after removing its obstacle.");
            }
            if (scenario.Forklift.hasForkliftDriverEntityId)
                scenario.Forklift.RemoveForkliftDriverEntityId();
            scenario.Player.isDrivingForklift = false;
            scenario.Forklift.isInteractable = true;
            scenario.Forklift.isHighlighted = false;
            scenario.Input.isInteractPressed = false;
        }

        private static void ValidateDestructionRecovery(Runtime runtime,
            Scenario scenario)
        {
            EnterForklift(runtime, scenario);
            SetForkliftCargoApproach(
                runtime,
                scenario,
                scenario.Staging.Slots[0],
                StagingApproachDistance);
            PulsePalletTransfer(runtime, scenario);
            Require(scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Staging.OccupiedPalletSlotCount == 0,
                "Destruction recovery setup did not put the staged pallet back on " +
                "the forklift.");

            scenario.Forklift.isDestructed = true;
            runtime.Systems.Create<RecoverDestructedForkliftStateSystem>()
                .Execute();

            Require(!scenario.Forklift.hasForkliftDriverEntityId &&
                    !scenario.Forklift.isInteractable &&
                    !scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    !scenario.Pallet.isDestructed &&
                    !scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Pallet.PalletBayEntityId ==
                    scenario.Staging.EntityId &&
                    scenario.Pallet.PalletBaySlotIndex == 0 &&
                    scenario.Staging.OccupiedPalletSlotCount == 1,
                "Destroying an occupied forklift did not release the driver and recover " +
                "its pallet to the first free staging slot.");

            scenario.Forklift.isDestructed = false;
            scenario.Forklift.isInteractable = true;
            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void ValidateFullStagingFallback(Runtime runtime,
            Scenario scenario)
        {
            EnterForklift(runtime, scenario);
            SetForkliftCargoApproach(
                runtime,
                scenario,
                scenario.Staging.Slots[0],
                StagingApproachDistance);
            PulsePalletTransfer(runtime, scenario);
            Require(scenario.Pallet.hasForkliftCarrierEntityId &&
                    scenario.Staging.OccupiedPalletSlotCount == 0,
                "Full-staging fallback setup did not put the recovered pallet back " +
                "on the forklift.");

            var fillers = new List<GameEntity>(scenario.Staging.Slots.Length);
            var fillerViews = new List<GameObject>(scenario.Staging.Slots.Length);
            try
            {
                for (int index = 0;
                     index < scenario.Staging.Slots.Length;
                     index++)
                {
                    GameObject fillerView = new(
                        $"Smoke Full Staging Pallet {index + 1}");
                    fillerViews.Add(fillerView);
                    fillerView.transform.SetPositionAndRotation(
                        scenario.Staging.Slots[index].position,
                        scenario.Staging.Slots[index].rotation);
                    GameEntity filler =
                        CreateEntity.Empty(runtime.Identifiers.Next());
                    filler.isPallet = true;
                    filler.AddPalletStoreEntityId(
                        scenario.Forklift.ForkliftStoreEntityId);
                    filler.AddPalletBayEntityId(scenario.Staging.EntityId);
                    filler.AddPalletBaySlotIndex(index);
                    filler.AddTransform(fillerView.transform);
                    fillers.Add(filler);
                }
                scenario.Staging.ReplaceOccupiedPalletSlotCount(
                    scenario.Staging.Slots.Length);
                runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                    .Execute();

                scenario.Forklift.isDestructed = true;
                RecoverDestructedForkliftStateSystem recovery =
                    runtime.Systems
                        .Create<RecoverDestructedForkliftStateSystem>();
                recovery.Execute();
                RequireFullStagingFallbackState(
                    scenario,
                    "full-staging recovery Execute");

                recovery.Cleanup();
                RequireFullStagingFallbackState(
                    scenario,
                    "full-staging recovery Cleanup after Execute");
                runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                    .Execute();
            }
            finally
            {
                foreach (GameEntity filler in fillers)
                {
                    if (filler.isEnabled)
                        filler.Destroy();
                }
                foreach (GameObject fillerView in fillerViews)
                    UnityEngine.Object.DestroyImmediate(fillerView);
                scenario.Staging.ReplaceOccupiedPalletSlotCount(0);
                Physics.SyncTransforms();
            }

            runtime.Systems.Create<ValidateForkliftFreightStateSystem>()
                .Execute();
        }

        private static void RequireFullStagingFallbackState(
            Scenario scenario, string phase)
        {
            Require(!scenario.Forklift.hasForkliftDriverEntityId &&
                    !scenario.Forklift.isInteractable &&
                    !scenario.Player.isDrivingForklift &&
                    scenario.Player.CharacterController.enabled &&
                    scenario.Pallet.isDestructed &&
                    !scenario.Pallet.hasForkliftCarrierEntityId &&
                    !scenario.Pallet.hasPalletBayEntityId &&
                    !scenario.Pallet.hasPalletBaySlotIndex &&
                    scenario.Staging.OccupiedPalletSlotCount ==
                    scenario.Staging.Slots.Length,
                $"{phase} did not release the driver and convert the orphaned " +
                "carried pallet into an unplaced Destructed fallback.");
        }

        private static void BeginOneFrameBlockedDestructionRecovery(
            Runtime runtime)
        {
            GameContext game = new Contexts().game;
            GameObject forkliftView = new(
                "Smoke Disposable Destructed Forklift");
            forkliftView.transform.position = new Vector3(200f, 0f, 200f);
            BoxCollider forkliftHull =
                forkliftView.AddComponent<BoxCollider>();
            forkliftHull.center = new Vector3(0f, 1f, 0f);
            forkliftHull.size = new Vector3(2f, 2f, 2f);

            Transform seatAnchor = new GameObject("Driver Seat").transform;
            seatAnchor.SetParent(forkliftView.transform, false);
            seatAnchor.localPosition = new Vector3(0f, 1f, 0f);
            Transform exitAnchor = new GameObject("Driver Exit").transform;
            exitAnchor.SetParent(forkliftView.transform, false);
            exitAnchor.localPosition = new Vector3(2.2f, 1f, 0f);

            GameObject driverView = new(
                "Smoke Disposable Forklift Driver");
            CharacterController controller =
                driverView.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            runtime.Motion.EnterDriver(
                controller, driverView.transform, seatAnchor);

            const int storeEntityId = 8001;
            GameEntity forklift = game.CreateEntity();
            forklift.AddEntityId(1);
            forklift.isForklift = true;
            forklift.AddForkliftStoreEntityId(storeEntityId);
            forklift.AddTransform(forkliftView.transform);
            forklift.AddDriverExitAnchor(exitAnchor);
            forklift.AddForkliftDriverEntityId(2);
            forklift.isDestructed = true;
            _ = new SmokeEntityView(forkliftView, forklift);

            GameEntity driver = game.CreateEntity();
            driver.AddEntityId(2);
            driver.isPlayer = true;
            driver.AddStoreEntityId(storeEntityId);
            driver.AddTransform(driverView.transform);
            driver.AddCharacterController(controller);
            driver.isDrivingForklift = true;

            GameObject obstacle = CreateDriverExitObstacle(
                controller,
                driverView.transform,
                exitAnchor,
                "Smoke Disposable Blocked Exit");
            RecoverDestructedForkliftStateSystem recovery = new(
                game, runtime.Motion);
            try
            {
                recovery.Execute();
                recovery.Cleanup();
                Require(driver.isDrivingForklift &&
                        !controller.enabled &&
                        forklift.hasForkliftDriverEntityId,
                    "Disposable destruction recovery did not defer its blocked " +
                    "driver before generic cleanup.");

                new CleanupDestructedViewsSystem(game).Cleanup();
                new CleanupDestructedEntitiesSystem(game).Cleanup();
                Require(!forklift.isEnabled &&
                        driver.isDrivingForklift &&
                        !controller.enabled &&
                        game.GetEntityWithForkliftDriverEntityId(
                            driver.EntityId) == null &&
                        forkliftView != null &&
                        forkliftHull != null && forkliftHull.enabled,
                    "Generic Destructed cleanup enabled the driver before the " +
                    "forklift collision hull reached end-of-frame destruction.");
            }
            catch
            {
                CleanupDisposableRecoveryFixture(
                    forklift, driver, forkliftView, driverView, obstacle);
                throw;
            }

            EditorApplication.CallbackFunction continuation = null;
            continuation = () =>
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= continuation;
                    CleanupDisposableRecoveryFixture(
                        forklift, driver, forkliftView, driverView, obstacle);
                    Debug.LogError(
                        "[Hardware Store] Forklift freight smoke interrupted before " +
                        "the one-frame destruction recovery completed.");
                    return;
                }
                if (forkliftView != null)
                    return;

                EditorApplication.update -= continuation;
                try
                {
                    Physics.SyncTransforms();
                    recovery.Execute();
                    recovery.Cleanup();
                    Require(controller.enabled &&
                            !driver.isDrivingForklift &&
                            game.GetEntityWithForkliftDriverEntityId(
                                driver.EntityId) == null,
                        "The frame after generic forklift cleanup did not restore " +
                        "the orphaned driver's controller and clear DrivingForklift.");

                    Debug.Log(
                        "[Hardware Store] Forklift freight smoke passed: blocked exit, " +
                        "collision-blocked lift, free drive, west-side +X truck " +
                        "approach, lift/pickup/staging/exit, atomic recovery preflight, " +
                        "occupied-count validation, missing/destructed/full staging " +
                        "fallbacks, inverse bay cleanup, blocked-exit destruction " +
                        "deferral, real one-frame cleanup recovery and idempotence.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    CleanupDisposableRecoveryFixture(
                        forklift, driver, forkliftView, driverView, obstacle);
                }
            };
            EditorApplication.update += continuation;
        }

        private static void CleanupDisposableRecoveryFixture(
            GameEntity forklift, GameEntity driver,
            GameObject forkliftView, GameObject driverView,
            GameObject obstacle)
        {
            if (forklift.isEnabled && forklift.hasView)
                forklift.View.ReleaseEntity();
            if (forklift.isEnabled)
                forklift.Destroy();
            if (driver.isEnabled)
                driver.Destroy();
            if (obstacle != null)
                UnityEngine.Object.DestroyImmediate(obstacle);
            if (driverView != null)
                UnityEngine.Object.DestroyImmediate(driverView);
            if (forkliftView != null)
                UnityEngine.Object.DestroyImmediate(forkliftView);
            Physics.SyncTransforms();
        }

        private static void AdjustForkHeight(Runtime runtime,
            Scenario scenario, float targetHeight)
        {
            float currentHeight = scenario.Forklift.ForkliftForkHeight;
            float delta = targetHeight - currentHeight;
            if (Mathf.Abs(delta) <= PositionTolerance)
                return;

            scenario.Input.ReplaceForkliftLiftInput(Mathf.Sign(delta));
            new AdjustForkliftLiftSystem(
                    runtime.Game,
                    runtime.Input,
                    new FixedTimeService(
                        Mathf.Abs(delta) /
                        scenario.Forklift.ForkliftLiftSpeed),
                    runtime.Motion)
                .Execute();
            scenario.Input.ReplaceForkliftLiftInput(0f);

            Require(Mathf.Abs(scenario.Forklift.ForkliftForkHeight -
                              targetHeight) < PositionTolerance &&
                    Mathf.Abs(scenario.Forklift.LiftTransform.localPosition.y -
                              targetHeight) < PositionTolerance,
                $"Forklift lift did not reach deterministic height {targetHeight:0.###}.");
        }

        private static void SetForkliftCargoApproach(Runtime runtime,
            Scenario scenario, Transform slot, float approachDistance)
        {
            Transform forkliftTransform = scenario.Forklift.Transform;
            Transform cargoAnchor = scenario.Forklift.CargoAnchor;
            Vector3 slotForward = Vector3.ProjectOnPlane(
                slot.forward, Vector3.up).normalized;
            Quaternion desiredCargoRotation = Quaternion.LookRotation(
                slotForward, Vector3.up);
            Quaternion rotationDelta = desiredCargoRotation *
                                       Quaternion.Inverse(cargoAnchor.rotation);
            Quaternion targetForkliftRotation = rotationDelta *
                                                forkliftTransform.rotation;
            Vector3 desiredCargoPosition = slot.position -
                                           slotForward * approachDistance;
            desiredCargoPosition.y = cargoAnchor.position.y;
            Vector3 targetForkliftPosition = desiredCargoPosition -
                                              rotationDelta *
                                              (cargoAnchor.position -
                                               forkliftTransform.position);

            scenario.Forklift.Rigidbody.position = targetForkliftPosition;
            scenario.Forklift.Rigidbody.rotation = targetForkliftRotation;
            forkliftTransform.SetPositionAndRotation(
                targetForkliftPosition,
                targetForkliftRotation);
            runtime.Motion.KeepDriverSeated(
                scenario.Player.CharacterController,
                scenario.Player.Transform,
                scenario.Forklift.DriverSeatAnchor);
            Physics.SyncTransforms();

            Require(Vector3.Distance(
                        cargoAnchor.position,
                        desiredCargoPosition) < PositionTolerance &&
                    Quaternion.Angle(
                        cargoAnchor.rotation,
                        desiredCargoRotation) < RotationTolerance,
                "Forklift smoke could not author a deterministic cargo approach pose.");
        }

        private static void PulsePalletTransfer(Runtime runtime,
            Scenario scenario)
        {
            scenario.Input.isForkliftTransferPressed = true;
            runtime.Systems.Create<TransferForkliftPalletSystem>().Execute();
            scenario.Input.isForkliftTransferPressed = false;
        }

        private static void CleanupRequests(Runtime runtime)
        {
            runtime.Systems.Create<DestroyProcessedEventsSystem>().Cleanup();
            runtime.Systems.Create<CleanupInputRequestsSystem>().Cleanup();
            Require(runtime.Game.GetGroup(GameMatcher.InteractionRequest).count == 0,
                "Forklift interaction request survived cleanup.");
        }

        private static Runtime ResolveRuntime()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Enter Play Mode before running the forklift freight smoke test.");
            }

            ProjectContext[] projectContexts =
                Resources.FindObjectsOfTypeAll<ProjectContext>()
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
                container.Resolve<IForkliftMotionService>(),
                container.Resolve<IIdentifierService>());
        }

        private static GameEntity RequireSingle(
            IGroup<GameEntity> group, string role)
        {
            GameEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one {role}, found {entities.Length}.");
        }

        private static InputEntity RequireSingle(
            IGroup<InputEntity> group, string role)
        {
            InputEntity[] entities = group.GetEntities();
            return entities.Length == 1
                ? entities[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one {role}, found {entities.Length}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void RequireThrows<TException>(Action action,
            string message)
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
            public Runtime(GameContext game, InputContext input,
                ISystemFactory systems, IGameStateMachine stateMachine,
                IForkliftMotionService motion,
                IIdentifierService identifiers)
            {
                Game = game;
                Input = input;
                Systems = systems;
                StateMachine = stateMachine;
                Motion = motion;
                Identifiers = identifiers;
            }

            public GameContext Game { get; }
            public InputContext Input { get; }
            public ISystemFactory Systems { get; }
            public IGameStateMachine StateMachine { get; }
            public IForkliftMotionService Motion { get; }
            public IIdentifierService Identifiers { get; }
        }

        private readonly struct Scenario
        {
            public Scenario(GameEntity player, GameEntity forklift,
                GameEntity truck, GameEntity staging, GameEntity pallet,
                InputEntity input)
            {
                Player = player;
                Forklift = forklift;
                Truck = truck;
                Staging = staging;
                Pallet = pallet;
                Input = input;
            }

            public GameEntity Player { get; }
            public GameEntity Forklift { get; }
            public GameEntity Truck { get; }
            public GameEntity Staging { get; }
            public GameEntity Pallet { get; }
            public InputEntity Input { get; }
        }

        private sealed class SmokeEntityView : IEntityView
        {
            private GameEntity _entity;

            public SmokeEntityView(GameObject viewObject, GameEntity entity)
            {
                gameObject = viewObject ??
                    throw new ArgumentNullException(nameof(viewObject));
                SetEntity(entity);
            }

            public GameEntity Entity => _entity ??
                throw new InvalidOperationException(
                    "Disposable smoke view is not bound to an entity.");

            public GameObject gameObject { get; }

            public void SetEntity(GameEntity entity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                if (_entity != null || entity.hasView)
                {
                    throw new InvalidOperationException(
                        "Disposable smoke view is already bound.");
                }

                _entity = entity;
                _entity.AddView(this);
                _entity.Retain(this);
            }

            public void ReleaseEntity()
            {
                GameEntity entity = Entity;
                if (!entity.hasView || !ReferenceEquals(entity.View, this))
                {
                    throw new InvalidOperationException(
                        "Disposable smoke view is bound inconsistently.");
                }

                entity.RemoveView();
                entity.Release(this);
                _entity = null;
            }
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
    }
}
