using System;
using System.Collections.Generic;
using OwlTree;
using OwlTree.Unity;
using UnityEngine;

namespace OwlTree.Unity
{
    /// <summary>
    /// Manages synchronized GameObjects using predefined prefabs
    /// provided in the prefabs list of a ConnectionArgs Scriptable Object.
    /// </summary>
    public class PrefabSpawner : NetworkObject
    {
        private static List<PrefabSpawner> _instances = new();
        /// <summary>
        /// Iterable of all PrefabSpawner instances that exist locally.
        /// </summary>
        public static IEnumerable<PrefabSpawner> Instances => _instances;

        public override void OnSpawn()
        {
            _instances.Add(this);
            Connection.Maps.AddMap<GameObjectId, NetworkGameObject>();
        }

        public override void OnDespawn()
        {
            _instances.Remove(this);
        }

        /// <summary>
        /// Provide the prefabs this spawner will be able to use. This list of prefabs
        /// should be the same across all connections for this application.
        /// </summary>
        public void Initialize(UnityConnection connection, IEnumerable<GameObject> prefabs)
        {
            _connection = connection;

            var curId = PrefabId.FirstPrefabId;
            foreach (var prefab in prefabs)
            {
                _prefabs.Add(new PrefabId(curId), prefab);
                curId++;
            }
            Initialized = true;

            if (!Connection.IsAuthority)
                RequestObjects();
        }

        /// <summary>
        /// Returns true if this spawner is ready to start spawning prefabs.
        /// </summary>
        public bool Initialized { get; private set; } = false;
        private Dictionary<PrefabId, GameObject> _prefabs = new();

        /// <summary>
        /// The prefabs this spawner is able to use.
        /// </summary>
        public IEnumerable<GameObject> Prefabs => _prefabs.Values;

        /// <summary>
        /// The synchronized GameObjects this spawner is managing.
        /// </summary>
        public IEnumerable<NetworkGameObject> Objects => Connection.Maps.GetValues<GameObjectId, NetworkGameObject>();

        /// <summary>
        /// Try to get a prefab based on the id it was assigned at initialization.
        /// Returns true if the prefab was found, false otherwise.
        /// </summary>
        private bool TryGetPrefabId(GameObject prefab, out PrefabId id)
        {
            foreach (var pair in _prefabs)
            {
                if (pair.Value == prefab)
                {
                    id = pair.Key;
                    return true;
                }
            }
            id = PrefabId.None;
            return false;
        }

        /// <summary>
        /// Try to get a NetworkGameObject using the provided id.
        /// Returns true if the object was found, false otherwise.
        /// </summary>
        public bool TryGetObject(GameObjectId id, out NetworkGameObject obj)
        {
            return Connection.Maps.TryGet(id, out obj);
        }

        /// <summary>
        /// Get a NetworkGameObject using the provided id.
        /// Returns null if no such object was found.
        /// </summary>
        public NetworkGameObject GetGameObject(GameObjectId id)
        {
            if (!Connection.Maps.TryGet(id, out NetworkGameObject obj))
                return null;
            return obj;
        }

        private UnityConnection _connection;

        /// <summary>
        /// Invoked when a new GameObject is instantiated from a prefab.
        /// </summary>
        public Action<NetworkGameObject> OnObjectSpawn;
        /// <summary>
        /// Invoked when a synchronized GameObject is destroyed.
        /// </summary>
        public Action<NetworkGameObject> OnObjectDespawn;

        private uint _curId = GameObjectId.FirstGameObjectId;
        private GameObjectId NextGameObjectId()
        {
            var id = new GameObjectId(_curId);
            _curId++;
            return id;
        }

        /// <summary>
        /// Synchronously instantiate a new GameObject using the given prefab.
        /// </summary>
        public NetworkGameObject Spawn(GameObject prefab)
        {
            if (!TryGetPrefabId(prefab, out var id))
                throw new ArgumentException($"Prefab '{prefab.name}' is not assigned a prefab id. Make sure this prefab is in the prefab list.");
            
            var obj = GameObject.Instantiate(prefab);

            if (!obj.TryGetComponent<NetworkGameObject>(out var netObj))
                netObj = obj.AddComponent<NetworkGameObject>();

            netObj.Id = NextGameObjectId();
            netObj.Prefab = id;
            netObj.Connection = _connection;
            Connection.Maps.Add(netObj.Id, netObj);

            SendSpawn(id, netObj.Id);

            netObj.InvokeOnSpawn();

            return netObj;
        }

        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void SendSpawn(PrefabId id, GameObjectId assignedId)
        {
            if (!Initialized)
                return;

            if (!_prefabs.TryGetValue(id, out var prefab))
                throw new ArgumentException($"prefab id {id} is not assigned to a prefab.");
            
            var obj = GameObject.Instantiate(prefab);

            if (!obj.TryGetComponent<NetworkGameObject>(out var netObj))
                netObj = obj.AddComponent<NetworkGameObject>();
            
            netObj.Id = assignedId;
            netObj.Prefab = id;
            netObj.Connection = _connection;
            Connection.Maps.Add(netObj.Id, netObj);
            netObj.InvokeOnSpawn();
        }

        /// <summary>
        /// Sends all existing NetworkGameObjects to the given client.
        /// </summary>
        public void SendNetworkObjects(ClientId callee)
        {
            foreach (var obj in Objects)
                SendSpawnTo(callee, obj.Prefab, obj.Id);
        }

        /// <summary>
        /// Used by newly connected clients to request all existing NetworkGameObjects
        /// to get synchronized with the session.
        /// </summary>
        [Rpc(RpcPerms.ClientsToAuthority)]
        public virtual void RequestObjects([CallerId] ClientId caller = default)
        {
            SendNetworkObjects(caller);
        }

        /// <summary>
        /// Spawn a new NetworkGameobject for a single client.
        /// </summary>
        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void SendSpawnTo([CalleeId] ClientId callee, PrefabId id, GameObjectId assignedId)
        {
            if (!Initialized)
                return;
            
            if (!_prefabs.TryGetValue(id, out var prefab))
                throw new ArgumentException($"prefab id {id} is not assigned to a prefab.");
            if (Connection.Maps.HasKey(assignedId))
                return;
            
            var obj = GameObject.Instantiate(prefab);

            if (!obj.TryGetComponent<NetworkGameObject>(out var netObj))
                netObj = obj.AddComponent<NetworkGameObject>();
            
            netObj.Id = assignedId;
            netObj.Connection = _connection;
            Connection.Maps.Add(netObj.Id, netObj);
            netObj.InvokeOnSpawn();
        }

        /// <summary>
        /// Destroy an existing NetworkGameObject across all clients.
        /// </summary>
        public void Despawn(NetworkGameObject target)
        {
            Connection.Maps.Remove(target.Id);
            target.InvokeOnDespawn();
            OnObjectDespawn?.Invoke(target);
            target.Connection = null;
            SendDespawn(target.Id);
            GameObject.Destroy(target.gameObject);
        }

        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void SendDespawn(GameObjectId id)
        {
            if (!Connection.Maps.TryGet(id, out NetworkGameObject obj))
                throw new ArgumentException($"no network game object has the id {id}.");
            Connection.Maps.Remove(id);
            obj.InvokeOnDespawn();
            OnObjectDespawn?.Invoke(obj);
            obj.Connection = null;
            GameObject.Destroy(obj.gameObject);
        }

        /// <summary>
        /// Locally destroy all NetworkGameObjects managed by this spawner.
        /// </summary>
        public void DespawnAll()
        {
            foreach (var obj in Objects)
            {
                obj.OnDespawn.Invoke(obj);
                OnObjectDespawn?.Invoke(obj);
                obj.Connection = null;
                if (obj != null)
                    GameObject.Destroy(obj.gameObject);
            }
            Connection.Maps.Clear<GameObjectId, NetworkGameObject>();
        }
    }
}
