using System;
using System.Collections.Generic;
using Gley.TrafficSystem;
using Gley.UrbanSystem;
using HardwareStore.Gameplay.Common.Time;
using HardwareStore.Gameplay.Common.VehicleTraffic;
using UnityEngine;
using Zenject;
using TrafficApi = Gley.TrafficSystem.API;
using TrafficEvents = Gley.TrafficSystem.Events;
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;

namespace HardwareStore.Infrastructure.VehicleTraffic.Gley
{
    public sealed class GleyTrafficService : IVehicleTrafficService
    {
        private const int InvalidRuntimeVehicleId = -1;

        private readonly GleyTrafficConfig _config;
        private readonly ITimeService _time;
        private readonly DiContainer _container;
        private readonly Queue<VehicleTrafficSignal> _signals = new();
        private readonly Dictionary<int, PendingSpawn> _pendingSpawnsByOwner = new();
        private readonly HashSet<int> _reservedRuntimeVehicleIds = new();
        private readonly HashSet<int> _despawnOnActivation = new();
        private readonly Dictionary<int, VehicleState> _vehiclesByRuntimeId = new();
        private readonly Dictionary<int, int> _runtimeIdByOwner = new();
        private readonly List<int> _expiredSpawnOwners = new(8);

        private bool _initialized;
        private bool _ownsTrafficRuntime;
        private Vector3 _frontAxleLocalXZ;
        private Transform _observer;
        private Transform _observerAnchor;

        public GleyTrafficService(GleyTrafficConfig config, ITimeService time,
            DiContainer container)
        {
            _config = config;
            _time = time;
            _container = container;
        }

        public void Initialize(Transform observer)
        {
            if (_initialized)
                throw new InvalidOperationException("Vehicle traffic is already initialized.");
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            _config.Validate();
            _frontAxleLocalXZ = ResolveFrontAxleLocalXZ();
            _signals.Clear();
            _observer = observer;
            Transform trafficObserver = GetOrCreateObserverAnchor();
            if (TrafficApi.IsInitialized())
            {
                if (!_ownsTrafficRuntime)
                {
                    throw new InvalidOperationException(
                        "Gley Traffic System was initialized outside IVehicleTrafficService.");
                }

                TrafficApi.SetCamera(trafficObserver);
            }
            else
            {
                TrafficApi.Initialize(trafficObserver, _config.MaximumVehicleCount,
                    _config.VehiclePool, _config.CreateOptions());
            }

            if (!TrafficApi.IsInitialized())
            {
                throw new InvalidOperationException(
                    "Gley Traffic System failed to initialize. Verify converted scene data and layers.");
            }

            _ownsTrafficRuntime = true;
            InjectPooledVehicleViews();

            TrafficEvents.OnVehicleActivated += HandleVehicleActivated;
            TrafficEvents.OnDestinationReached += HandleDestinationReached;
            TrafficEvents.OnVehicleDisabled += HandleVehicleDisabled;
            _initialized = true;
            UpdateTrafficDensity();
            Enqueue(VehicleTrafficSignalKind.Initialized, 0, 0,
                InvalidRuntimeVehicleId, null, VehicleTrafficFailure.None);
        }

        public void RequestSpawn(int ownerEntityId, int commandSequence, Pose start,
            Pose destination)
        {
            EnsureInitialized();
            EnsureFinite(start, nameof(start));
            EnsureFinite(destination, nameof(destination));
            Vector3 providerStart = ToGleyWaypointPosition(start);
            Vector3 providerDestination = ToGleyWaypointPosition(destination);

            if (_pendingSpawnsByOwner.ContainsKey(ownerEntityId) ||
                _runtimeIdByOwner.ContainsKey(ownerEntityId))
            {
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    commandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.OwnerAlreadyHasVehicle);
                return;
            }

            TrafficWaypoint spawnWaypoint = start.position == Vector3.zero
                ? null
                : TrafficApi.GetClosestSpawnWaypoint(providerStart, VehicleTypes.Car);
            if (spawnWaypoint == null)
            {
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    commandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.SpawnUnavailable);
                return;
            }

            List<int> path = TrafficApi.GetPath(
                spawnWaypoint.Position, providerDestination, VehicleTypes.Car);
            if (path == null || path.Count == 0)
            {
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    commandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.NoPath);
                return;
            }

            if (!TryReserveRuntimeVehicle(out int runtimeVehicleId))
            {
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    commandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.CapacityReached);
                return;
            }

            _pendingSpawnsByOwner.Add(ownerEntityId,
                new PendingSpawn(commandSequence, runtimeVehicleId, providerDestination,
                    _time.UnscaledTime));
            _reservedRuntimeVehicleIds.Add(runtimeVehicleId);
            UpdateTrafficDensity();

            try
            {
                bool accepted = TrafficApi.TryInstantiateIgnoredVehicle(
                    runtimeVehicleId, providerStart,
                    (vehicle, waypointIndex) => HandleVehicleInstantiated(
                        ownerEntityId, commandSequence, vehicle, waypointIndex));
                if (!accepted)
                {
                    _pendingSpawnsByOwner.Remove(ownerEntityId);
                    _reservedRuntimeVehicleIds.Remove(runtimeVehicleId);
                    UpdateTrafficDensity();
                    Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                        commandSequence, InvalidRuntimeVehicleId, null,
                        VehicleTrafficFailure.SpawnUnavailable);
                }
            }
            catch
            {
                _pendingSpawnsByOwner.Remove(ownerEntityId);
                _reservedRuntimeVehicleIds.Remove(runtimeVehicleId);
                UpdateTrafficDensity();
                throw;
            }
        }

        public void CancelSpawn(int ownerEntityId)
        {
            EnsureInitialized();
            if (!_pendingSpawnsByOwner.TryGetValue(ownerEntityId,
                    out PendingSpawn pending))
            {
                return;
            }

            TrafficApi.CancelIgnoredVehicleRequest(pending.RuntimeVehicleId);
            _pendingSpawnsByOwner.Remove(ownerEntityId);
            _reservedRuntimeVehicleIds.Remove(pending.RuntimeVehicleId);
            UpdateTrafficDensity();
        }

        public void RequestDestination(int runtimeVehicleId, int ownerEntityId,
            int commandSequence, Pose destination)
        {
            EnsureInitialized();
            EnsureFinite(destination, nameof(destination));
            Vector3 providerDestination = ToGleyWaypointPosition(destination);
            if (!TryGetOwnedVehicle(runtimeVehicleId, ownerEntityId, out VehicleState state,
                    out VehicleTrafficFailure failure))
            {
                Enqueue(VehicleTrafficSignalKind.DestinationRejected, ownerEntityId,
                    commandSequence, runtimeVehicleId, null, failure);
                return;
            }

            VehicleComponent vehicle = TrafficApi.GetVehicleComponent(runtimeVehicleId);
            if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
            {
                Enqueue(VehicleTrafficSignalKind.DestinationRejected, ownerEntityId,
                    commandSequence, runtimeVehicleId, null,
                    VehicleTrafficFailure.RuntimeVehicleNotFound);
                return;
            }

            int currentWaypointIndex = vehicle.MovementInfo.GetWaypointIndex(0);
            TrafficWaypoint currentWaypoint =
                TrafficApi.GetWaypointFromIndex(currentWaypointIndex);
            List<int> path = currentWaypoint == null
                ? null
                : TrafficApi.GetPath(currentWaypoint.Position, providerDestination,
                    vehicle.VehicleType);
            if (path == null || path.Count == 0)
            {
                Enqueue(VehicleTrafficSignalKind.DestinationRejected, ownerEntityId,
                    commandSequence, runtimeVehicleId, vehicle.gameObject,
                    VehicleTrafficFailure.NoPath);
                return;
            }

            int previousCommandSequence = state.ActiveCommandSequence;
            int previousTerminalWaypoint = state.TerminalWaypointIndex;
            state.ActiveCommandSequence = commandSequence;
            state.TerminalWaypointIndex = path[^1];
            try
            {
                TrafficApi.SetVehiclePath(runtimeVehicleId, path);
            }
            catch
            {
                state.ActiveCommandSequence = previousCommandSequence;
                state.TerminalWaypointIndex = previousTerminalWaypoint;
                throw;
            }
        }

        public void RequestDespawn(int runtimeVehicleId, int ownerEntityId,
            int commandSequence)
        {
            EnsureInitialized();
            if (!TryGetOwnedVehicle(runtimeVehicleId, ownerEntityId, out VehicleState state,
                    out VehicleTrafficFailure failure))
            {
                Enqueue(VehicleTrafficSignalKind.DespawnRejected, ownerEntityId,
                    commandSequence, runtimeVehicleId, null, failure);
                return;
            }

            _vehiclesByRuntimeId.Remove(runtimeVehicleId);
            _runtimeIdByOwner.Remove(ownerEntityId);
            UpdateTrafficDensity();

            try
            {
                // Mapping is removed first, therefore Gley's synchronous Disabled callback is
                // intentionally ignored and cannot produce a duplicate lifecycle signal.
                TrafficApi.RemoveVehicle(runtimeVehicleId);
            }
            catch
            {
                _vehiclesByRuntimeId.Add(runtimeVehicleId, state);
                _runtimeIdByOwner.Add(ownerEntityId, runtimeVehicleId);
                UpdateTrafficDensity();
                throw;
            }

            Enqueue(VehicleTrafficSignalKind.VehicleDespawned, ownerEntityId,
                commandSequence, runtimeVehicleId, state.VehicleRoot,
                VehicleTrafficFailure.None);
        }

        public bool TryDequeue(out VehicleTrafficSignal signal)
        {
            SyncObserverAnchor();
            ExpirePendingSpawns();
            return _signals.TryDequeue(out signal);
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                _signals.Clear();
                return;
            }

            TrafficEvents.OnVehicleActivated -= HandleVehicleActivated;
            TrafficEvents.OnDestinationReached -= HandleDestinationReached;
            TrafficEvents.OnVehicleDisabled -= HandleVehicleDisabled;
            _initialized = false;
            _observer = null;

            int[] runtimeVehicleIds = new int[_vehiclesByRuntimeId.Count];
            _vehiclesByRuntimeId.Keys.CopyTo(runtimeVehicleIds, 0);
            foreach (PendingSpawn pending in _pendingSpawnsByOwner.Values)
                TrafficApi.CancelIgnoredVehicleRequest(pending.RuntimeVehicleId);
            _vehiclesByRuntimeId.Clear();
            _runtimeIdByOwner.Clear();
            _pendingSpawnsByOwner.Clear();
            _reservedRuntimeVehicleIds.Clear();
            _despawnOnActivation.Clear();

            if (TrafficApi.IsInitialized())
            {
                TrafficApi.SetTrafficDensity(0);
                foreach (int runtimeVehicleId in runtimeVehicleIds)
                {
                    VehicleComponent vehicle = TrafficApi.GetVehicleComponent(runtimeVehicleId);
                    if (vehicle != null && vehicle.gameObject.activeSelf)
                        TrafficApi.RemoveVehicle(runtimeVehicleId);
                }
            }

            _signals.Clear();
        }

        private void HandleVehicleInstantiated(int ownerEntityId, int commandSequence,
            VehicleComponent vehicle, int spawnWaypointIndex)
        {
            if (!_initialized ||
                !_pendingSpawnsByOwner.TryGetValue(ownerEntityId, out PendingSpawn pending) ||
                pending.CommandSequence != commandSequence ||
                pending.RuntimeVehicleId != vehicle.ListIndex)
            {
                if (vehicle != null)
                {
                    if (_pendingSpawnsByOwner.TryGetValue(ownerEntityId,
                            out PendingSpawn currentPending) &&
                        currentPending.RuntimeVehicleId == vehicle.ListIndex)
                    {
                        _pendingSpawnsByOwner.Remove(ownerEntityId);
                    }
                    _reservedRuntimeVehicleIds.Remove(vehicle.ListIndex);
                    if (_initialized)
                    {
                        // The Gley callback runs before ActivateVehicle. Removing here would be
                        // undone when its request resumes, so cleanup is deferred to Activated.
                        _despawnOnActivation.Add(vehicle.ListIndex);
                        UpdateTrafficDensity();
                    }
                }
                return;
            }

            _pendingSpawnsByOwner.Remove(ownerEntityId);
            _reservedRuntimeVehicleIds.Remove(vehicle.ListIndex);
            int runtimeVehicleId = vehicle.ListIndex;
            TrafficWaypoint spawnWaypoint =
                TrafficApi.GetWaypointFromIndex(spawnWaypointIndex);
            List<int> path = spawnWaypoint == null
                ? null
                : TrafficApi.GetPath(spawnWaypoint.Position, pending.Destination,
                    vehicle.VehicleType);
            if (path == null || path.Count == 0)
            {
                _despawnOnActivation.Add(runtimeVehicleId);
                UpdateTrafficDensity();
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    commandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.NoPath);
                return;
            }

            if (_vehiclesByRuntimeId.ContainsKey(runtimeVehicleId))
            {
                throw new InvalidOperationException(
                    $"Gley reused active runtime vehicle {runtimeVehicleId}.");
            }

            TrafficApi.SetVehiclePath(runtimeVehicleId, path);
            var state = new VehicleState(ownerEntityId, commandSequence,
                vehicle.gameObject, path[^1]);
            _vehiclesByRuntimeId.Add(runtimeVehicleId, state);
            _runtimeIdByOwner.Add(ownerEntityId, runtimeVehicleId);
            TrafficApi.DontRemoveVehicle(runtimeVehicleId, true);
            UpdateTrafficDensity();
        }

        private void HandleVehicleActivated(int runtimeVehicleId, int _)
        {
            if (_initialized && _despawnOnActivation.Remove(runtimeVehicleId))
            {
                TrafficApi.RemoveVehicle(runtimeVehicleId);
                UpdateTrafficDensity();
                return;
            }

            if (!_initialized ||
                !_vehiclesByRuntimeId.TryGetValue(runtimeVehicleId, out VehicleState state) ||
                state.Activated)
            {
                return;
            }

            state.Activated = true;
            Enqueue(VehicleTrafficSignalKind.VehicleActivated, state.OwnerEntityId,
                state.ActiveCommandSequence, runtimeVehicleId, state.VehicleRoot,
                VehicleTrafficFailure.None);
        }

        private void HandleDestinationReached(int runtimeVehicleId)
        {
            if (!_initialized ||
                !_vehiclesByRuntimeId.TryGetValue(runtimeVehicleId, out VehicleState state) ||
                !state.Activated ||
                state.LastReachedCommandSequence == state.ActiveCommandSequence)
            {
                return;
            }

            VehicleComponent vehicle = TrafficApi.GetVehicleComponent(runtimeVehicleId);
            if (vehicle == null ||
                vehicle.MovementInfo.GetWaypointIndex(0) != state.TerminalWaypointIndex)
            {
                return;
            }

            state.LastReachedCommandSequence = state.ActiveCommandSequence;
            Enqueue(VehicleTrafficSignalKind.DestinationReached, state.OwnerEntityId,
                state.ActiveCommandSequence, runtimeVehicleId, state.VehicleRoot,
                VehicleTrafficFailure.None);
        }

        private void HandleVehicleDisabled(int runtimeVehicleId)
        {
            if (!_initialized ||
                !_vehiclesByRuntimeId.Remove(runtimeVehicleId, out VehicleState state))
            {
                return;
            }

            _runtimeIdByOwner.Remove(state.OwnerEntityId);
            UpdateTrafficDensity();
            Enqueue(VehicleTrafficSignalKind.VehicleRemoved, state.OwnerEntityId,
                state.ActiveCommandSequence, runtimeVehicleId, state.VehicleRoot,
                VehicleTrafficFailure.None);
        }

        private bool TryGetOwnedVehicle(int runtimeVehicleId, int ownerEntityId,
            out VehicleState state, out VehicleTrafficFailure failure)
        {
            if (!_vehiclesByRuntimeId.TryGetValue(runtimeVehicleId, out state))
            {
                failure = VehicleTrafficFailure.RuntimeVehicleNotFound;
                return false;
            }
            if (state.OwnerEntityId != ownerEntityId)
            {
                failure = VehicleTrafficFailure.OwnerMismatch;
                return false;
            }

            failure = VehicleTrafficFailure.None;
            return true;
        }

        private void UpdateTrafficDensity()
        {
            if (TrafficApi.IsInitialized())
            {
                TrafficApi.SetTrafficDensity(
                    _vehiclesByRuntimeId.Count + _pendingSpawnsByOwner.Count);
            }
        }

        private void ExpirePendingSpawns()
        {
            if (!_initialized || _pendingSpawnsByOwner.Count == 0)
                return;

            float now = _time.UnscaledTime;
            _expiredSpawnOwners.Clear();
            foreach (KeyValuePair<int, PendingSpawn> pair in _pendingSpawnsByOwner)
            {
                if (now - pair.Value.RequestedAt >= _config.SpawnRequestTimeout)
                    _expiredSpawnOwners.Add(pair.Key);
            }

            foreach (int ownerEntityId in _expiredSpawnOwners)
            {
                PendingSpawn pending = _pendingSpawnsByOwner[ownerEntityId];
                CancelSpawn(ownerEntityId);
                Enqueue(VehicleTrafficSignalKind.SpawnRejected, ownerEntityId,
                    pending.CommandSequence, InvalidRuntimeVehicleId, null,
                    VehicleTrafficFailure.SpawnTimedOut);
            }
            _expiredSpawnOwners.Clear();
        }

        private bool TryReserveRuntimeVehicle(out int runtimeVehicleId)
        {
            VehicleComponent[] vehicles = TrafficApi.GetAllVehicles();
            for (int i = 0; i < vehicles.Length; i++)
            {
                VehicleComponent vehicle = vehicles[i];
                if (vehicle.Ignored && vehicle.VehicleType == VehicleTypes.Car &&
                    !vehicle.gameObject.activeSelf &&
                    !_vehiclesByRuntimeId.ContainsKey(vehicle.ListIndex) &&
                    !_reservedRuntimeVehicleIds.Contains(vehicle.ListIndex))
                {
                    runtimeVehicleId = vehicle.ListIndex;
                    return true;
                }
            }

            runtimeVehicleId = InvalidRuntimeVehicleId;
            return false;
        }

        private void InjectPooledVehicleViews()
        {
            VehicleComponent[] vehicles = TrafficApi.GetAllVehicles();
            if (vehicles == null || vehicles.Length == 0)
            {
                throw new InvalidOperationException(
                    "Gley Traffic System initialized without pooled vehicles.");
            }

            foreach (VehicleComponent vehicle in vehicles)
            {
                if (vehicle == null || vehicle.gameObject.activeSelf)
                {
                    throw new InvalidOperationException(
                        "Gley pooled vehicles must exist and remain inactive until their " +
                        "Unity views are injected.");
                }

                _container.InjectGameObject(vehicle.gameObject);
            }
        }

        private Vector3 ResolveFrontAxleLocalXZ()
        {
            Vector3? expected = null;
            CarType[] trafficCars = _config.VehiclePool.trafficCars;
            for (int index = 0; index < trafficCars.Length; index++)
            {
                GameObject prefab = trafficCars[index].VehiclePrefab;
                VehicleComponent vehicle = prefab.GetComponent<VehicleComponent>();
                Transform frontTrigger = vehicle.frontTrigger;
                if (frontTrigger == null)
                {
                    throw new InvalidOperationException(
                        $"Gley vehicle '{prefab.name}' has no front trigger.");
                }

                Vector3 triggerLocal = prefab.transform
                    .InverseTransformPoint(frontTrigger.position);
                EnsureFinite(triggerLocal, nameof(frontTrigger));
                float activationOffset = triggerLocal.magnitude;
                if (activationOffset <= 0.01f)
                {
                    throw new InvalidOperationException(
                        $"Gley vehicle '{prefab.name}' has an invalid front-axle offset.");
                }

                // Gley places the root behind a waypoint by the scalar distance from
                // the root to frontTrigger, always along the vehicle forward axis.
                Vector3 current = Vector3.forward * activationOffset;
                if (expected.HasValue &&
                    Vector3.Distance(expected.Value, current) > 0.001f)
                {
                    throw new InvalidOperationException(
                        "All vehicles in the Hardware Store traffic pool must use the " +
                        "same front-axle offset because ECS routes describe root poses.");
                }

                expected = current;
            }

            return expected ?? throw new InvalidOperationException(
                "Gley vehicle pool has no front-axle geometry.");
        }

        private Vector3 ToGleyWaypointPosition(Pose rootPose) =>
            rootPose.position + rootPose.rotation * _frontAxleLocalXZ;

        private void Enqueue(VehicleTrafficSignalKind kind, int ownerEntityId,
            int commandSequence, int runtimeVehicleId, GameObject vehicleRoot,
            VehicleTrafficFailure failure) =>
            _signals.Enqueue(new VehicleTrafficSignal(kind, ownerEntityId,
                commandSequence, runtimeVehicleId, vehicleRoot, failure));

        private void EnsureInitialized()
        {
            if (!_initialized || !TrafficApi.IsInitialized())
                throw new InvalidOperationException("Vehicle traffic is not initialized.");
        }

        private Transform GetOrCreateObserverAnchor()
        {
            if (_observerAnchor == null)
            {
                var anchor = new GameObject("[Hardware Store] Traffic Observer");
                _observerAnchor = anchor.transform;
            }

            SyncObserverAnchor();
            return _observerAnchor;
        }

        private void SyncObserverAnchor()
        {
            if (_observer == null || _observerAnchor == null)
                return;

            _observerAnchor.SetPositionAndRotation(
                _observer.position, _observer.rotation);
        }

        private static void EnsureFinite(Vector3 value, string parameter)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
                throw new ArgumentException("Vector must contain finite values.", parameter);
        }

        private static void EnsureFinite(Pose value, string parameter)
        {
            EnsureFinite(value.position, parameter);
            Quaternion rotation = value.rotation;
            if (!IsFinite(rotation.x) || !IsFinite(rotation.y) ||
                !IsFinite(rotation.z) || !IsFinite(rotation.w) ||
                Quaternion.Dot(rotation, rotation) < 0.000001f)
            {
                throw new ArgumentException(
                    "Pose must contain a finite, non-zero rotation.", parameter);
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class PendingSpawn
        {
            public PendingSpawn(int commandSequence, int runtimeVehicleId,
                Vector3 destination, float requestedAt)
            {
                CommandSequence = commandSequence;
                RuntimeVehicleId = runtimeVehicleId;
                Destination = destination;
                RequestedAt = requestedAt;
            }

            public int CommandSequence { get; }
            public int RuntimeVehicleId { get; }
            public Vector3 Destination { get; }
            public float RequestedAt { get; }
        }

        private sealed class VehicleState
        {
            public VehicleState(int ownerEntityId, int commandSequence,
                GameObject vehicleRoot, int terminalWaypointIndex)
            {
                OwnerEntityId = ownerEntityId;
                ActiveCommandSequence = commandSequence;
                LastReachedCommandSequence = int.MinValue;
                VehicleRoot = vehicleRoot;
                TerminalWaypointIndex = terminalWaypointIndex;
            }

            public int OwnerEntityId { get; }
            public GameObject VehicleRoot { get; }
            public int TerminalWaypointIndex { get; set; }
            public int ActiveCommandSequence { get; set; }
            public int LastReachedCommandSequence { get; set; }
            public bool Activated { get; set; }
        }
    }
}
