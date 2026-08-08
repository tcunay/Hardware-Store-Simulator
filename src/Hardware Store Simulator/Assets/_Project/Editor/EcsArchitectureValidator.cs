using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Entitas;
using HardwareStore.Gameplay.Common.Registrars;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.Registrars;
using HardwareStore.Gameplay.Scene;
using HardwareStore.Infrastructure.Installers;
using HardwareStore.Infrastructure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace HardwareStore.Editor
{
    public static class EcsArchitectureValidator
    {
        private const string MenuPath = "Tools/Hardware Store/Validate ECS Architecture";
        private const string ProjectContextPath = "Assets/Resources/ProjectContext.prefab";
        private const string PrototypeScenePath = "Assets/Scenes/Prototype_Yard.unity";
        private const string PlayerConfigPath = "Assets/Resources/Configs/PlayerConfig.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";

        private static readonly Type[] ExpectedInputComponents =
        {
            typeof(InputState),
            typeof(MoveInput),
            typeof(LookInput),
            typeof(SprintHeld),
            typeof(InteractPressed),
            typeof(DropPressed),
            typeof(ToggleCursorPressed),
            typeof(PointerLook)
        };

        private static readonly string[] ForbiddenRuntimeTypeNames =
        {
            "PrototypeSession",
            "PrototypeCompositionRoot",
            "ComponentRegistry",
            "GameComponentRegistry",
            "InputComponentRegistry",
            "GameEntityExtensions",
            "InputEntityExtensions",
            "HardwareStoreProjectInstaller",
            "PrototypeSceneInstaller",
            "HardwareStoreGameplayLoop",
            "PrototypeLevelInitializer",
            "PlayerView",
            "PlayerTransformRegistrar",
            "PlayerTransformComponent",
            "PlayerCameraRegistrar",
            "PlayerCameraComponent"
        };

        [MenuItem(MenuPath, priority = 120)]
        public static void ValidateFromMenu()
        {
            ValidationResult result = ValidateOrThrow();
            Debug.Log(
                $"ECS architecture is valid: {result.GameComponentCount} Game components, " +
                $"{result.InputComponentCount} Input components, ProjectContext and Prototype_Yard composition roots.");
        }

        public static ValidationResult ValidateOrThrow()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "ECS architecture validation must run in Edit Mode.");

            Assembly runtimeAssembly = typeof(GameEntity).Assembly;
            Require(runtimeAssembly.GetName().Name == "Assembly-CSharp",
                $"Expected GameEntity in Assembly-CSharp, but found it in {runtimeAssembly.GetName().Name}.");

            Type[] runtimeTypes = runtimeAssembly.GetTypes();
            Type[] componentTypes = FindHardwareStoreComponents(runtimeTypes);

            ValidateComponentShapes(componentTypes);
            ValidateRegistries(componentTypes);
            ValidateLegacyTypesAreAbsent(runtimeTypes);
            ValidateJennyPipeline();
            ValidateProjectContextPrefab();
            ValidatePlayerPrefab();
            ValidatePrototypeSceneComposition();
            ValidateBuildSettings();

            return new ValidationResult(
                GameComponentsLookup.componentTypes.Length,
                InputComponentsLookup.componentTypes.Length);
        }

        private static Type[] FindHardwareStoreComponents(IEnumerable<Type> runtimeTypes) =>
            runtimeTypes
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => typeof(IComponent).IsAssignableFrom(type))
                .Where(type => type.Namespace == "HardwareStore" ||
                               type.Namespace?.StartsWith("HardwareStore.", StringComparison.Ordinal) == true)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

        private static void ValidateComponentShapes(IEnumerable<Type> componentTypes)
        {
            foreach (Type componentType in componentTypes)
            {
                FieldInfo[] publicFields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);

                Require(publicFields.Length <= 1,
                    $"Component {componentType.FullName} contains {publicFields.Length} public instance fields; " +
                    "a component must be a tag or contain one Value field.");

                if (publicFields.Length == 1)
                {
                    Require(publicFields[0].Name == "Value",
                        $"The single public field of component {componentType.FullName} must be named Value, " +
                        $"but is named {publicFields[0].Name}.");
                }
            }
        }

        private static void ValidateRegistries(Type[] componentTypes)
        {
            ValidateRegistry("Game", GameComponentsLookup.componentNames, GameComponentsLookup.componentTypes);
            ValidateRegistry("Input", InputComponentsLookup.componentNames, InputComponentsLookup.componentTypes);

            var gameTypes = new HashSet<Type>(GameComponentsLookup.componentTypes);
            var inputTypes = new HashSet<Type>(InputComponentsLookup.componentTypes);
            var expectedInputTypes = new HashSet<Type>(ExpectedInputComponents);
            var discoveredTypes = new HashSet<Type>(componentTypes);

            Require(!gameTypes.Overlaps(inputTypes),
                "Game and Input component registries must not contain the same component type.");
            Require(inputTypes.SetEquals(expectedInputTypes),
                DescribeSetMismatch("Input registry", expectedInputTypes, inputTypes));

            var expectedGameTypes = new HashSet<Type>(discoveredTypes);
            expectedGameTypes.ExceptWith(expectedInputTypes);
            Require(gameTypes.SetEquals(expectedGameTypes),
                DescribeSetMismatch("Game registry", expectedGameTypes, gameTypes));

            gameTypes.UnionWith(inputTypes);
            Require(gameTypes.SetEquals(discoveredTypes),
                DescribeSetMismatch("Combined Game/Input registries", discoveredTypes, gameTypes));
        }

        private static void ValidateRegistry(string name, string[] componentNames, Type[] componentTypes)
        {
            Require(componentNames.Length == componentTypes.Length,
                $"{name} registry contains {componentNames.Length} names but {componentTypes.Length} types.");

            var uniqueTypes = new HashSet<Type>();
            var uniqueNames = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < componentTypes.Length; index++)
            {
                Type componentType = componentTypes[index];
                Require(componentType != null, $"{name} registry contains a null type at index {index}.");
                Require(typeof(IComponent).IsAssignableFrom(componentType),
                    $"{name} registry type {componentType.FullName} does not implement IComponent.");
                Require(uniqueTypes.Add(componentType),
                    $"{name} registry contains duplicate component type {componentType.FullName}.");
                string expectedName = ResolveGeneratedComponentName(componentType);
                Require(componentNames[index] == expectedName,
                    $"{name} registry name at index {index} is {componentNames[index]}, " +
                    $"but Jenny resolves {componentType.Name} as {expectedName}.");
                Require(uniqueNames.Add(componentNames[index]),
                    $"{name} registry contains duplicate component name {componentNames[index]}.");
            }
        }

        private static string ResolveGeneratedComponentName(Type componentType)
        {
            const string suffix = "Component";
            return componentType.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? componentType.Name[..^suffix.Length]
                : componentType.Name;
        }

        private static void ValidateLegacyTypesAreAbsent(IEnumerable<Type> runtimeTypes)
        {
            foreach (Type runtimeType in runtimeTypes)
            {
                Require(!ForbiddenRuntimeTypeNames.Contains(runtimeType.Name, StringComparer.Ordinal),
                    $"Legacy runtime type {runtimeType.FullName} is still present in Assembly-CSharp.");
            }
        }

        private static void ValidateJennyPipeline()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string generator = Path.Combine(repositoryRoot, "Jenny", "Jenny-Gen");
            string properties = Path.Combine(repositoryRoot, "Jenny", "JennyRoslyn.properties");
            string customGenerators = Path.Combine(repositoryRoot, "src", "CustomGenerators", "bin",
                "KSyndicate.CustomGenerators.Plugins.dll");

            Require(File.Exists(generator), $"Jenny launcher is missing at {generator}.");
            Require(File.Exists(properties), $"Jenny properties are missing at {properties}.");
            Require(File.Exists(customGenerators), $"Custom single-value generators are missing at {customGenerators}.");

            string generatedEntityPath = Path.Combine(Application.dataPath, "_Project", "Code", "Generated",
                "Game", "GameEntity.cs");
            Require(File.Exists(generatedEntityPath), $"Generated GameEntity is missing at {generatedEntityPath}.");
            Require(File.ReadAllText(generatedEntityPath).Contains("<auto-generated>", StringComparison.Ordinal),
                "GameEntity.cs does not contain the Jenny auto-generated header.");
        }

        private static void ValidateProjectContextPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectContextPath);
            Require(prefab != null, $"ProjectContext prefab is missing at {ProjectContextPath}.");

            ProjectContext[] contexts = prefab.GetComponentsInChildren<ProjectContext>(true);
            BootstrapInstaller[] installers = prefab.GetComponentsInChildren<BootstrapInstaller>(true);

            Require(contexts.Length == 1,
                $"{ProjectContextPath} must contain exactly one ProjectContext, found {contexts.Length}.");
            Require(installers.Length == 1,
                $"{ProjectContextPath} must contain exactly one BootstrapInstaller, " +
                $"found {installers.Length}.");
            Require(contexts[0].Installers.Count() == 1,
                $"{ProjectContextPath} must register exactly one MonoInstaller, " +
                $"found {contexts[0].Installers.Count()}.");
            Require(contexts[0].Installers.Contains(installers[0]),
                "BootstrapInstaller is present in ProjectContext.prefab but is not registered " +
                "in ProjectContext.Installers.");
        }

        private static void ValidatePlayerPrefab()
        {
            PlayerConfig playerConfig = AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            Require(playerConfig != null, $"Player config is missing at {PlayerConfigPath}.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Require(prefab != null, $"Player prefab is missing at {PlayerPrefabPath}.");
            Require(prefab.activeSelf, $"Player prefab root at {PlayerPrefabPath} must be active.");
            Require(prefab.transform.localPosition == Vector3.zero,
                $"Player prefab root at {PlayerPrefabPath} must have zero local position.");
            Require(prefab.transform.localRotation == Quaternion.identity,
                $"Player prefab root at {PlayerPrefabPath} must have identity local rotation.");
            Require(prefab.transform.localScale == Vector3.one,
                $"Player prefab root at {PlayerPrefabPath} must have unit local scale.");

            EntityBehaviour[] views = RequireExactlyOneInPrefab<EntityBehaviour>(prefab);
            TransformRegistrar[] transforms = RequireExactlyOneInPrefab<TransformRegistrar>(prefab);
            CharacterControllerRegistrar[] characterRegistrars =
                RequireExactlyOneInPrefab<CharacterControllerRegistrar>(prefab);
            ViewPivotRegistrar[] viewPivots = RequireExactlyOneInPrefab<ViewPivotRegistrar>(prefab);
            CameraRegistrar[] cameraRegistrars = RequireExactlyOneInPrefab<CameraRegistrar>(prefab);
            CarryAnchorRegistrar[] carryAnchors = RequireExactlyOneInPrefab<CarryAnchorRegistrar>(prefab);
            DropOriginRegistrar[] dropOrigins = RequireExactlyOneInPrefab<DropOriginRegistrar>(prefab);
            CharacterController[] controllers = RequireExactlyOneInPrefab<CharacterController>(prefab);
            Camera[] cameras = RequireExactlyOneInPrefab<Camera>(prefab);
            AudioListener[] listeners = RequireExactlyOneInPrefab<AudioListener>(prefab);

            Require(views[0].gameObject == prefab,
                $"The only EntityBehaviour in {PlayerPrefabPath} must be on the prefab root.");
            Require(transforms[0].gameObject == prefab,
                $"The only TransformRegistrar in {PlayerPrefabPath} must be on the prefab root.");
            Require(characterRegistrars[0].gameObject == prefab && controllers[0].gameObject == prefab,
                $"CharacterController and its registrar in {PlayerPrefabPath} must be on the prefab root.");
            Require(cameraRegistrars[0].gameObject == cameras[0].gameObject &&
                    listeners[0].gameObject == cameras[0].gameObject,
                $"Camera, CameraRegistrar and AudioListener in {PlayerPrefabPath} must share one object.");
            Require(viewPivots[0].transform.IsChildOf(prefab.transform),
                $"ViewPivotRegistrar in {PlayerPrefabPath} must belong to the player hierarchy.");
            Require(carryAnchors[0].transform.IsChildOf(cameras[0].transform) &&
                    dropOrigins[0].transform.IsChildOf(cameras[0].transform),
                $"Carry and drop anchors in {PlayerPrefabPath} must be children of the player camera.");

            SerializedObject serializedConfig = new(playerConfig);
            SerializedProperty viewPrefab = serializedConfig.FindProperty("_viewPrefab");
            Require(viewPrefab != null, $"{nameof(PlayerConfig)} must declare _viewPrefab.");
            Require(viewPrefab.objectReferenceValue == views[0],
                $"{PlayerConfigPath} must reference the EntityBehaviour root from {PlayerPrefabPath}.");
        }

        private static void ValidatePrototypeSceneComposition()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScenePath) != null,
                $"Prototype scene is missing at {PrototypeScenePath}.");

            Scene scene = SceneManager.GetSceneByPath(PrototypeScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Additive);

            try
            {
                SceneContext[] contexts = FindComponentsInScene<SceneContext>(scene);
                SceneInitializationInstaller[] installers =
                    FindComponentsInScene<SceneInitializationInstaller>(scene);
                PrototypeSceneInitializer[] initializers =
                    FindComponentsInScene<PrototypeSceneInitializer>(scene);
                CharacterControllerRegistrar[] characterRegistrars =
                    FindComponentsInScene<CharacterControllerRegistrar>(scene);
                CameraRegistrar[] cameraRegistrars = FindComponentsInScene<CameraRegistrar>(scene);
                TransformRegistrar[] transformRegistrars = FindComponentsInScene<TransformRegistrar>(scene);
                SpawnPointMarker[] spawnPoints = FindComponentsInScene<SpawnPointMarker>(scene);

                Require(contexts.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one SceneContext, found {contexts.Length}.");
                Require(installers.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one SceneInitializationInstaller, " +
                    $"found {installers.Length}.");
                Require(initializers.Length == 1,
                    $"{PrototypeScenePath} must contain exactly one PrototypeSceneInitializer, " +
                    $"found {initializers.Length}.");
                Require(contexts[0].Installers.Count() == 1,
                    $"{PrototypeScenePath} SceneContext must register exactly one MonoInstaller, " +
                    $"found {contexts[0].Installers.Count()}.");
                Require(contexts[0].Installers.Contains(installers[0]),
                    "SceneInitializationInstaller is present in Prototype_Yard but is not registered " +
                    "in SceneContext.Installers.");
                Require(installers[0].Initializers.Count == 1,
                    "SceneInitializationInstaller in Prototype_Yard must contain exactly one initializer, " +
                    $"found {installers[0].Initializers.Count}.");
                Require(installers[0].Initializers.Contains(initializers[0]),
                    "PrototypeSceneInitializer is present in Prototype_Yard but is not registered " +
                    "in SceneInitializationInstaller.Initializers.");
                SerializedProperty configuredSpawnPoints = new SerializedObject(initializers[0])
                    .FindProperty("_spawnPoints");
                Require(configuredSpawnPoints != null && configuredSpawnPoints.isArray,
                    $"{nameof(PrototypeSceneInitializer)} must serialize its spawn point array.");
                Require(configuredSpawnPoints.arraySize == spawnPoints.Length,
                    $"{nameof(PrototypeSceneInitializer)} must register every spawn point in " +
                    $"{PrototypeScenePath} exactly once.");
                var configuredSpawnPointSet = new HashSet<SpawnPointMarker>();
                for (int index = 0; index < configuredSpawnPoints.arraySize; index++)
                {
                    SpawnPointMarker marker = configuredSpawnPoints.GetArrayElementAtIndex(index)
                        .objectReferenceValue as SpawnPointMarker;
                    Require(marker != null && configuredSpawnPointSet.Add(marker),
                        $"{nameof(PrototypeSceneInitializer)} contains a missing or duplicate spawn point.");
                }

                Require(configuredSpawnPointSet.SetEquals(spawnPoints),
                    $"{nameof(PrototypeSceneInitializer)} does not reference the scene spawn point set.");
                Require(characterRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CharacterControllerRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                Require(cameraRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain CameraRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                Require(transformRegistrars.Length == 0,
                    $"{PrototypeScenePath} must not contain TransformRegistrar; " +
                    "the player view is instantiated from its prefab at runtime.");
                Require(spawnPoints.Select(marker => marker.Id).Distinct().Count() == spawnPoints.Length,
                    $"{PrototypeScenePath} contains duplicate spawn point ids.");
                Require(spawnPoints.Count(marker => marker.Id == SpawnPointId.Player) == 1,
                    $"{PrototypeScenePath} must contain exactly one {SpawnPointId.Player} spawn point.");
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateBuildSettings()
        {
            EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .ToArray();

            Require(enabledScenes.Length > 0,
                "Build Settings must contain at least one enabled scene.");
            Require(enabledScenes[0].path == PrototypeScenePath,
                $"{PrototypeScenePath} must be the first enabled scene in Build Settings, " +
                $"but found {enabledScenes[0].path}.");
        }

        private static TComponent[] FindComponentsInScene<TComponent>(Scene scene)
            where TComponent : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TComponent>(true))
                .ToArray();

        private static TComponent[] RequireExactlyOneInPrefab<TComponent>(GameObject prefab)
            where TComponent : Component
        {
            TComponent[] components = prefab.GetComponentsInChildren<TComponent>(true);
            Require(components.Length == 1,
                $"{PlayerPrefabPath} must contain exactly one {typeof(TComponent).Name}, " +
                $"found {components.Length}.");
            return components;
        }

        private static string DescribeSetMismatch(string owner, HashSet<Type> expected, HashSet<Type> actual)
        {
            string missing = JoinTypeNames(expected.Except(actual));
            string unexpected = JoinTypeNames(actual.Except(expected));
            return $"{owner} does not match component declarations. Missing: [{missing}]. " +
                   $"Unexpected: [{unexpected}].";
        }

        private static string JoinTypeNames(IEnumerable<Type> types) =>
            string.Join(", ", types.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"ECS architecture validation failed: {message}");
        }

        public readonly struct ValidationResult
        {
            public int GameComponentCount { get; }
            public int InputComponentCount { get; }

            public ValidationResult(int gameComponentCount, int inputComponentCount)
            {
                GameComponentCount = gameComponentCount;
                InputComponentCount = inputComponentCount;
            }
        }
    }
}
