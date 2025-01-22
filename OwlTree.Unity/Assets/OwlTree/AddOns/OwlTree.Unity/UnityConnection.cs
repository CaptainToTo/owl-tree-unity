using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OwlTree;
using UnityEngine.Events;
using System;

namespace OwlTree.Unity
{

    /// <summary>
    /// Thin wrapper around an OwlTree Connection instance
    /// that hooks into Unity's runtime.
    /// </summary>
    public class UnityConnection : MonoBehaviour
    {
        private static List<UnityConnection> _instances = new();

        /// <summary>
        /// Iterable of all unity connections current available.
        /// </summary>
        public static IEnumerable<UnityConnection> Instances => _instances;

        /// <summary>
        /// The OwlTree Connection this component wraps.
        /// </summary>
        public Connection Connection { get; private set; } = null;

        /// <summary>
        /// Initialization arguments for building a new Unity connection.
        /// </summary>
        public class Args : Connection.Args
        {
            public GameObject[] prefabs = null;
        }

        [Tooltip("Will be used as a default if no args are provided in Connect(). This can used for easier testing.")]
        [SerializeField] private ConnectionArgs _args;
        
        /// <summary>
        /// Invoked when the connection is started.
        /// </summary>
        public UnityEvent<UnityConnection> OnStart;

        /// <summary>
        /// Invoked when the connection is ready. On the server, this will be immediately.
        /// On clients, this will be when they have connected to the server.
        /// </summary>
        public UnityEvent<ClientId> OnReady;
        /// <summary>
        /// Invoked when a remote client joins the session.
        /// </summary>
        public UnityEvent<ClientId> OnClientConnected;
        /// <summary>
        /// Invoke when a remote client leaves the session.
        /// </summary>
        public UnityEvent<ClientId> OnClientDisconnected;
        /// <summary>
        /// Invoked when the local connection ends.
        /// </summary>
        public UnityEvent<ClientId> OnLocalDisconnect;
        /// <summary>
        /// Invoked when the authority is migrated to a new client
        /// in a relayed peer-to-peer session.
        /// </summary>
        public UnityEvent<ClientId> OnHostMigration;
        
        /// <summary>
        /// Invoked when a new NetworkObject is spawned.
        /// </summary>
        public UnityEvent<NetworkObject> OnObjectSpawn;
        /// <summary>
        /// Invoked when a NetworkObject is despawned.
        /// </summary>
        public UnityEvent<NetworkObject> OnObjectDespawn;

        /// <summary>
        /// Invoked when a Unity GameObject is instantiated synchronously.
        /// </summary>
        public UnityEvent<NetworkGameObject> OnGameObjectSpawn;
        /// <summary>
        /// Invoked when a Unity GameObject is destroyed synchronously.
        /// </summary>
        public UnityEvent<NetworkGameObject> OnGameObjectDespawn;

        /// <summary>
        /// Invoked if bandwidth measurement is enabled.
        /// </summary>
        [HideInInspector] public UnityEvent<Bandwidth> OnBandwidthReport;

        /// <summary>
        /// Whether this connection is active. Will be false for clients if 
        /// they have been disconnected from the server.
        /// </summary>
        public bool IsActive => Connection.IsActive;

        /// <summary>
        /// Whether this connection has established a link to the server. 
        /// This is true for clients once they've been assigned a local id.
        /// </summary>
        public bool IsReady => Connection.IsReady && _spawner != null;

        void Awake()
        {
            _instances.Add(this);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Connection.IsActive)
                Connection.Disconnect();
            _instances.Remove(this);
        }

        /// <summary>
        /// Starts the connection. Provide args to configure the connection.
        /// </summary>
        public void Connect(Args args = null)
        {
            if (args == null)
                args = _args.GetArgs();
            if (args.bandwidthReporter == null)
                args.bandwidthReporter = (bandwidth) => OnBandwidthReport.Invoke(bandwidth);
            Connection = new Connection(args);

            Connection.OnClientConnected += (id) => OnClientConnected.Invoke(id);
            Connection.OnClientDisconnected += (id) => OnClientDisconnected.Invoke(id);
            Connection.OnLocalDisconnect += (id) => {
                OnLocalDisconnect.Invoke(id);
                _spawner?.DespawnAll();
                Destroy(gameObject);
            };
            Connection.OnHostMigration += (id) => OnHostMigration.Invoke(id);
            Connection.OnObjectSpawn += (id) => OnObjectSpawn.Invoke(id);
            Connection.OnObjectDespawn += (id) => OnObjectDespawn.Invoke(id);

            StartCoroutine(WaitForReady(args));

            OnStart?.Invoke(this);
        }

        private IEnumerator WaitForReady(Args args)
        {
            while (!Connection.IsReady)
                yield return null;

            if (Connection.IsAuthority)
            {
                _spawner = Connection.Spawn<PrefabSpawner>();
                _spawner.Initialize(this, args.prefabs);
            }

            while (_spawner == null)
            {
                foreach (var s in PrefabSpawner.Instances)
                {
                    if (s.Connection == Connection)
                    {
                        _spawner = s;
                        _spawner.Initialize(this, _args.prefabs);
                        Debug.Log("got spawner");
                        break;
                    }
                }
                yield return null;
            }

            _spawner.OnObjectSpawn = (obj) => {
                if (Connection.Logger.includes.spawnEvents)
                    Connection.Logger.Write($"Spawned new NetworkGameObject \"{obj.name}\": {obj.Id}");
                OnGameObjectSpawn?.Invoke(obj);
            };
            _spawner.OnObjectDespawn = (obj) => {
                if (Connection.Logger.includes.spawnEvents)
                    Connection.Logger.Write($"Despawned NetworkGameObject \"{obj.name}\": {obj.Id}");
                OnGameObjectDespawn?.Invoke(obj);
            };

            OnReady?.Invoke(Connection.LocalId);
        }

        void FixedUpdate()
        {
            Connection.ExecuteQueue();
        }

        private PrefabSpawner _spawner = null;

        /// <summary>
        /// Iterable of synchronized GameObjects this connection is managing.
        /// </summary>
        public IEnumerable<NetworkGameObject> GameObjects => _spawner?.Objects;

        /// <summary>
        /// Instantiates a new GameObject synchronously. This Gameobject must be a prefab included
        /// in the prefabs list of the <c>ConnectionArgs</c> Scriptable Object provided to this connection.
        /// This can only be called by the authority.
        /// </summary>
        public NetworkGameObject Spawn(GameObject prefab)
        {
            if (!Connection.IsAuthority)
                throw new InvalidOperationException("Non-authority clients cannot spawn new objects.");
            var spawned = _spawner.Spawn(prefab);
            return spawned;
        }

        /// <summary>
        /// Destroys the provided GameObject synchronously.
        /// This can only be called by the authority.
        /// </summary>
        public void Despawn(NetworkGameObject target)
        {
            if (!Connection.IsAuthority)
                throw new InvalidOperationException("Non-authority clients cannot despawn objects.");
            _spawner.Despawn(target);
        }

        /// <summary>
        /// Get a NetworkGameObject using it's synchronized GameObjectId.
        /// Returns null if no object is found.
        /// </summary>
        public NetworkGameObject GetGameObject(GameObjectId id) => _spawner.GetGameObject(id);

        /// <summary>
        /// Try to get a NetworkGameObject using it's synchronized GameObjectId. Returns false if
        /// no object is found.
        /// </summary>
        public bool TryGetObject(GameObjectId id, out NetworkGameObject obj) => _spawner.TryGetObject(id, out obj);

        /// <summary>
        /// Access metadata about RPC encodings and generated protocols.
        /// </summary>
        public RpcProtocols Protocols => Connection.Protocols;

        /// <summary>
        /// Uses this connection's logger to output a message.
        /// </summary>
        public void Log(string message) => Connection.Log(message);

        /// <summary>
        /// Access bandwidth data about this connection. 
        /// If <c>measureBandwidth</c> was not enabled during configuration, then this will be null.
        /// </summary>
        public Bandwidth Bandwidth => Connection.Bandwidth;

        /// <summary>
        /// Whether or not this connection is using a send/recv thread.
        /// </summary>
        public bool Threaded => Connection.Threaded;

        /// <summary>
        /// Whether this connection represents a server or client.
        /// </summary>
        public NetRole NetRole => Connection.NetRole;

        /// <summary>
        /// Returns true if this connection is configured to be a server.
        /// </summary>
        public bool IsServer => Connection.IsServer;

        /// <summary>
        /// Returns true if this connection is configured to be a client.
        /// </summary>
        public bool IsClient => Connection.IsClient;

        /// <summary>
        /// Returns true if this connection is configured to be a host client.
        /// </summary>
        public bool IsHost => Connection.IsHost;

        /// <summary>
        /// Returns true if this connection is configured to be a relay server.
        /// </summary>
        public bool IsRelay => Connection.IsRelay;

        /// <summary>
        /// The TCP port the server connection managing this session is listening to.
        /// </summary>
        public int ServerTcpPort => Connection.ServerTcpPort;
        /// <summary>
        /// The UDP port this server connection managing this session is listening to.
        /// </summary>
        public int ServerUdpPort => Connection.ServerUdpPort;
        /// <summary>
        /// The local TCP port this connection is listening to.
        /// </summary>
        public int LocalTcpPort => Connection.LocalTcpPort;
        /// <summary>
        /// The local UDP port this connection is listening to.
        /// </summary>
        public int LocalUdpPort => Connection.LocalUdpPort;

        /// <summary>
        /// The app this connection is associated with.
        /// </summary>
        public StringId AppId => Connection.AppId;
        /// <summary>
        /// The session this connection is associated with.
        /// </summary>
        public StringId SessionId => Connection.SessionId;

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        public int ClientCount => Connection.ClientCount;
        /// <summary>
        /// The maximum number of clients allowed to be connected at once in this session.
        /// This value will not be accurate on clients until the connection is ready.
        /// </summary>
        public int MaxClients => Connection.MaxClients;

        /// <summary>
        /// Iterable of all connected clients.
        /// </summary>
        public IEnumerable<ClientId> Clients => Connection.Clients;

        /// <summary>
        /// Returns true if the given client id currently exists in this session.
        /// </summary>
        public bool ContainsClient(ClientId id) => Connection.ContainsClient(id);

        /// <summary>
        /// The client id assigned to this local instance. Servers will have a LocalId of <c>ClientId.None</c>.
        /// </summary>
        public ClientId LocalId => Connection.LocalId;

        /// <summary>
        /// the client id of the instance assigned as the authority of the session. 
        /// Servers will have an id of <c>ClientId.None</c>.
        /// </summary>
        public ClientId Authority => Connection.Authority;

        /// <summary>
        /// Returns true if the local connection is the authority of this session.
        /// </summary>
        public bool IsAuthority => Connection.IsAuthority;

        /// <summary>
        /// Returns true if the current session supports host migration.
        /// This can only be the case for relayed sessions.
        /// </summary>
        public bool Migratable => Connection.Migratable;

        /// <summary>
        /// Receive any packets that have been sent to this connection. Execute them with <c>ExecuteQueue()</c>.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void Recv() => Connection.Recv();

        /// <summary>
        /// Block until the connection is ready.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void AwaitConnection() => Connection.AwaitConnection();

        /// <summary>
        /// Execute any RPCs that have been received in the last <c>Recv()</c> call.
        /// </summary>
        public void ExecuteQueue() => Connection.ExecuteQueue();

        /// <summary>
        /// Send current outgoing packets.
        /// This can only be called on non-threaded connections.
        /// </summary>
        public void Send() => Connection.Send();

        /// <summary>
        /// Ping the target client. A target of <c>ClientId.None</c> will ping the server.
        /// Returns a PingRequest, which is similar to a promise. The ping value will only be known
        /// once the ping request has been resolved.
        /// </summary>
        public PingRequest Ping(ClientId target) => Connection.Ping(target);

        /// <summary>
        /// Disconnect the local connection. If this is a server, the server is shut down.
        /// if this is a client, disconnect from the server.
        /// </summary>
        public void Disconnect() => Connection.Disconnect();

        /// <summary>
        /// Disconnect a specific client from the server.
        /// This can only be called by the authority.
        /// </summary>
        public void Disconnect(ClientId id) => Connection.Disconnect(id);

        /// <summary>
        /// Reassign the authority client (a.k.a. host) to a new client.
        /// This can only be called by the authority.
        /// </summary>
        public void MigrateHost(ClientId id) => Connection.MigrateHost(id);

        /// <summary>
        /// Iterable of all currently spawned network objects
        /// </summary>
        public IEnumerable<NetworkObject> NetworkObjects => Connection.NetworkObjects;

        /// <summary>
        /// Try to get an object with the given id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject(NetworkId id, out NetworkObject obj) => Connection.TryGetObject(id, out obj);
        /// <summary>
        /// Try to get an object of the given type, with the give id. Returns true if one was found, false otherwise.
        /// </summary>
        public bool TryGetObject<T>(NetworkId id, out T obj) where T : NetworkObject => Connection.TryGetObject<T>(id, out obj);
        /// <summary>
        /// Get an object with the given id. Returns null if none exist.
        /// </summary>
        public NetworkObject GetNetworkObject(NetworkId id) => Connection.GetNetworkObject(id);
        /// <summary>
        /// Get an object with the given type and id. Returns null if none exists.
        /// </summary>
        public T GetNetworkObject<T>(NetworkId id) where T : NetworkObject => Connection.GetNetworkObject<T>(id);
        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public T Spawn<T>() where T : NetworkObject, new() => Connection.Spawn<T>();
        /// <summary>
        /// Spawns a new instance of the given NetworkObject sub-type across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public NetworkObject Spawn(Type t) => Connection.Spawn(t);
        /// <summary>
        /// Despawns the given NetworkObject across all clients.
        /// This can only be called by the authority.
        /// </summary>
        public void Despawn(NetworkObject target) => Connection.Despawn(target);

        /// <summary>
        /// Use to associate objects with key-value pairs, and make accessible to all
        /// objects with a reference to this connection. This is not synchronized across clients.
        /// Addons that use this may handle synchronization for you.
        /// </summary>
        public GenericObjectMaps Maps => Connection.Maps;

        /// <summary>
        /// Enqueue a callback that will wait for a NetworkObject with the given NetworkId
        /// to exist.
        /// </summary>
        public void WaitForObject(NetworkId id, Action<NetworkObject> callback) => Connection.WaitForObject(id, callback);
        /// <summary>
        /// Enqueue a callback that will wait for a given key to exist in this connection's
        /// generic object maps.
        /// </summary>
        public void WaitForObject<K, V>(K id, Action<V> callback) => Connection.WaitForObject(id, callback);
    }

}

