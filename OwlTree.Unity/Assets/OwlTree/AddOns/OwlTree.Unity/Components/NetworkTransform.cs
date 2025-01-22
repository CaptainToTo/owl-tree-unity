using UnityEngine;

namespace OwlTree.Unity
{
    /// <summary>
    /// Synchronizes local transform state across clients.
    /// </summary>
    public class NetworkTransform : NetworkBehaviour
    {
        internal TransformNetcode netcode = null;

        /// <summary>
        /// The authority of this transform. This can be an client, not just the connection authority.
        /// </summary>
        public ClientId Authority => netcode.Authority;

        /// <summary>
        /// Assign a client as the authority of this transform. This can only be done
        /// by the connection authority.
        /// </summary>
        public void SetAuthority(ClientId authority)
        {
            if (Connection.IsAuthority)
                netcode.SetAuthority(authority);
        }

        public override void OnSpawn()
        {
            if (Connection.IsAuthority)
            {
                netcode = Connection.Spawn<TransformNetcode>();
                netcode.transform = this;
            }
        }

        [Tooltip("Lerp between position updates to create smoother movement.")]
        [SerializeField] private bool _interpolate = false;
        public bool Interpolate => _interpolate;

        [Tooltip("Send updates even if the transform hasn't changed. This will consume more bandwidth.")]
        [SerializeField] private bool _continuousSync = false;

        [Tooltip("Disable or enable local rotation synchronization.")]
        [SerializeField] private bool _syncRotation = true;
        [Tooltip("Disable or enable local scale synchronization.")]
        [SerializeField] private bool _syncScale = false;

        [HideInInspector] internal Vector3 nextPos;
        [HideInInspector] internal Quaternion nextRot;
        [HideInInspector] internal Vector3 nextScale;

        void FixedUpdate()
        {
            if (netcode == null)
                return;

            if (Connection.LocalId == netcode.Authority)
            {
                var pos = new NetworkVec3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z);
                var rot = new NetworkVec4(transform.localRotation.x, transform.localRotation.y, transform.localRotation.z, transform.localRotation.w);
                var scale = new NetworkVec3(transform.localScale.x, transform.localScale.y, transform.localScale.z);

                if (_continuousSync || (nextPos - transform.localPosition).magnitude > 0.001f)
                    netcode.SendPosition(pos);
                if (_syncRotation && (_continuousSync || (nextRot.eulerAngles - transform.eulerAngles).magnitude > 0.001f))
                    netcode.SendRotation(rot);
                if (_syncScale && (_continuousSync || (nextScale - transform.localScale).magnitude > 0.001f))
                    netcode.SendScale(scale);
                
                nextPos = transform.localPosition;
                nextRot = transform.localRotation;
                nextScale = transform.localScale;
            }
            else if (_interpolate)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, nextPos, 0.5f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, nextRot, 0.7f);
            }
        }
    }

    // RPCs for network transform, handles state transfer
    public class TransformNetcode : NetworkObject
    {
        internal NetworkTransform transform = null;

        /// <summary>
        /// The authority of this transform. This can be an client, not just the connection authority.
        /// </summary>
        public ClientId Authority { get; private set; }
        private ClientId _original = ClientId.None;

        public override void OnSpawn()
        {
            Authority = Connection.Authority;
            _original = Connection.Authority;
            if (!Connection.IsAuthority)
                RequestTransform();
            Connection.OnClientDisconnected += (id) => {
                if (id == Authority && Connection.IsAuthority)
                    SetAuthority(Connection.Authority);
            };
            Connection.OnHostMigration += (id) => {
                if (Authority == _original && Connection.IsAuthority)
                    SetAuthority(Connection.Authority);
                _original = id;
            };
        }

        [Rpc(RpcPerms.AuthorityToClients, InvokeOnCaller = true)]
        public virtual void SetAuthority(ClientId authority)
        {
            Authority = authority;
        }

        [Rpc(RpcPerms.ClientsToAuthority)]
        public virtual void RequestTransform([CallerId] ClientId caller = default)
        {
            CacheTransform(caller, transform.NetObject.Id, Authority);
        }

        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void CacheTransform([CalleeId] ClientId callee, GameObjectId id, ClientId authority)
        {
            Authority = authority;
            Connection.WaitForObject<GameObjectId, NetworkGameObject>(id, (obj) => {
                transform = obj.GetComponent<NetworkTransform>();
                transform.netcode = this;
                RequestState(Authority);
            });
        }

        [Rpc(RpcPerms.AnyToAll)]
        public virtual void RequestState([CalleeId] ClientId callee, [CallerId] ClientId caller = default)
        {
            if (Connection.LocalId != Authority)
                return;

            var pos = new NetworkVec3(
                transform.transform.localPosition.x, 
                transform.transform.localPosition.y, 
                transform.transform.localPosition.z);
            var rot = new NetworkVec4(
                transform.transform.localRotation.x, 
                transform.transform.localRotation.y, 
                transform.transform.localRotation.z, 
                transform.transform.localRotation.w);
            var scale = new NetworkVec3(
                transform.transform.localScale.x, 
                transform.transform.localScale.y, 
                transform.transform.localScale.z);
            
            SendState(caller, pos, rot, scale);
        }

        [Rpc(RpcPerms.AnyToAll)]
        public virtual void SendState([CalleeId] ClientId callee, NetworkVec3 pos, NetworkVec4 rot, NetworkVec3 scale, [CallerId] ClientId caller = default)
        {
            if (caller != Authority)
                return;

            transform.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);
            transform.nextPos = transform.transform.localPosition;

            transform.transform.localRotation = new Quaternion(rot.x, rot.y, rot.z, rot.w);
            transform.nextRot = transform.transform.localRotation;

            transform.transform.localScale = new Vector3(scale.x, scale.y, scale.z);
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendPosition(NetworkVec3 pos, [CallerId] ClientId caller = default)
        {
            if (caller != Authority)
                return;

            if (transform == null)
                return;
            
            if (transform.Interpolate)
                transform.nextPos = new Vector3(pos.x, pos.y, pos.z);
            else
                transform.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendRotation(NetworkVec4 rot, [CallerId] ClientId caller = default)
        {
            if (caller != Authority)
                return;

            if (transform == null)
                return;

            if (transform.Interpolate)
                transform.nextRot = new Quaternion(rot.x, rot.y, rot.z, rot.w);
            else
                transform.transform.localRotation = new Quaternion(rot.x, rot.y, rot.z, rot.w);
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendScale(NetworkVec3 scale, [CallerId] ClientId caller = default)
        {
            if (caller != Authority)
                return;

            if (transform == null)
                return;
            
            transform.transform.localScale = new Vector3(scale.x, scale.y, scale.z);
        }
    }
}
