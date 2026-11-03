using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    /// <summary>Where <see cref="World"/> takes the mass tree from (stage 2c.W).</summary>
    public enum WorldSource
    {
        /// <summary>The shared world file Assets/_game/Config/world.json, the same file the server reads (default).</summary>
        SharedFile = 0,
        /// <summary>The tree stored in the scene (field tree): offline tests without the server only.</summary>
        Scene = 1,
    }

    /// <summary>
    /// Scene entry point of the world. On load it builds the <see cref="WorldModel"/> from the shared world file (default, stage 2c.W) or from the
    /// tree stored in the scene, binds it to the body components and injects the hierarchy. Views are initialized afterwards (<see cref="IViewInitializer"/>).
    /// </summary>
    public class World : MonoBehaviour, ISceneReady, IBindMe
    {
        private const string EditorWorldFile = "Assets/_game/Config/" + WorldFileHash.FileName;

        [Tooltip("SharedFile: Assets/_game/Config/world.json, the world of the server. Scene: the tree below, for offline tests without the server.")]
        [SerializeField] private WorldSource source = WorldSource.SharedFile;
        [Tooltip("The shared world file (Assets/_game/Config/world.json). Required for a player build; the Editor falls back to that path.")]
        [SerializeField] private TextAsset worldFile;
        [SerializeField] private TreeContainer tree;
        [Inject] private DiContainer _container;
        // Radius, rotation and the other per-body data; optional: without it bodies have no data (radius 0).
        [Inject(Optional = true)] private IBodyDataSource _bodyData;

        public WorldModel Model { get; private set; }
        public bool IsLoaded => Model != null;
        /// <summary>The source the loaded world really came from (the scene when the shared file could not be read).</summary>
        public WorldSource LoadedFrom { get; private set; }
        /// <summary>SHA-256 of the shared world file (<see cref="WorldFileHash"/>, compared with hello_ok.worldHash of the server); null for the scene tree.</summary>
        public string WorldHash { get; private set; }
        /// <summary>World zero epoch (stage 2c.W2): orbit epochs and planet rotation count from it, as on the server (world file epochZero, hello_ok.worldEpochZero).
        /// For the scene tree: the time the world was loaded at (the old rule).</summary>
        public double EpochZero { get; private set; }

        /// <summary>Sets the source of the body data. Call before <see cref="Load"/> where there is no DI container (edit mode).</summary>
        public void SetBodyData(IBodyDataSource data)
        {
            _bodyData = data;
        }

        void ISceneReady.OnSceneReady()
        {
            Load();
        }

        private void OnDestroy()
        {
            Debug.Log("World destroyed");
        }

        /// <summary>Does nothing if the world is loaded already or has nothing to load.</summary>
        public void Load()
        {
            if (IsLoaded)
            {
                if (Application.isPlaying)
                {
                    Debug.LogError("World has been already loaded!");
                }
                return;
            }

            LoadedWorld file = source == WorldSource.SharedFile ? ReadSharedFile() : null;
            IMassSystem root;
            if (file != null)
            {
                root = file.Root;
            }
            else
            {
                if (tree == null)
                {
                    tree = new TreeContainer();
                }
                if (tree.Root == null)
                {
                    tree.Load();
                }
                root = tree.Root;
            }
            if (root == null) return;

            // The file world is built at its zero epoch, so the planets are where the server has them whatever the client clock says.
            double epochZero = file != null ? file.EpochZero : TimeProvider.WorldTime;
            WorldModel model = new WorldModel(root, epochZero);
            if (file != null)
            {
                (int errors, int warnings) = new WorldSceneBinder().BindByName(transform, model, file.BodyOf, _bodyData, this);
                LoadedFrom = WorldSource.SharedFile;
                WorldHash = file.Hash;
                Debug.Log($"World: {file.Definition.Bodies.Count} bodies from the shared world file (sha256 {file.Hash}, epochZero {file.EpochZero}); " +
                          $"scene binding: {errors} errors, {warnings + file.Warnings.Count} warnings", this);
            }
            else
            {
                new WorldSceneBinder().Bind(transform, model, _bodyData);
                LoadedFrom = WorldSource.Scene;
                WorldHash = null;
            }
            EpochZero = epochZero;
            Model = model;
            InjectHierarchy();
        }

        /// <summary>The shared world file, or null (with an error in the log) when it is missing or broken: the scene tree is used then.</summary>
        private LoadedWorld ReadSharedFile()
        {
            TextAsset asset = worldFile;
#if UNITY_EDITOR
            if (asset == null) asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(EditorWorldFile);
#endif
            if (asset == null)
            {
                Debug.LogError($"World: no shared world file (field worldFile, expected {EditorWorldFile}); the scene tree is used", this);
                return null;
            }

            try
            {
                LoadedWorld loaded = WorldFileLoader.Load(asset.text);
                foreach (string warning in loaded.Warnings)
                {
                    Debug.LogWarning($"World: {asset.name}: {warning}", this);
                }
                return loaded;
            }
            catch (WorldFileException e)
            {
                Debug.LogError($"World: cannot use the shared world file {asset.name}: {e.Message}; the scene tree is used", this);
                return null;
            }
        }

        private void InjectHierarchy()
        {
            if (_container == null)
            {
                // Edit mode has no container.
                if (Application.isPlaying) Debug.LogError("Has no DI container in World component");
                return;
            }
            _container.InjectGameObject(gameObject);
        }
    }
}
