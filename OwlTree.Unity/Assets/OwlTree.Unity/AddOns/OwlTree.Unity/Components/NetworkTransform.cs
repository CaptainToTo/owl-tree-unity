using System;
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
            else
                throw new InvalidOperationException("Non-authority clients cannot assign shared authority of NetworkTransforms.");
        }

        public override void OnSpawn()
        {
            if (Connection.IsAuthority)
            {
                netcode = Connection.Spawn<TransformNetcode>();
                netcode.transform = this;
            }
        }

        public override void OnDespawn()
        {
            if (Connection.IsAuthority && netcode != null)
                Connection.Despawn(netcode);
        }

        [Tooltip("0 = no movement, 1 = snap to next position"), Range(0.001f, 1.0f)]
        [SerializeField] float _lerpMultiplier = 0.6f;
        [Tooltip("0 = no movement when predicting, 1 = snap to next prediction"), Range(0.001f, 1.0f)]
        [SerializeField] float _predictionEasing = 0.6f;
        [Tooltip("0 = no prediction, 1 = full prediction"), Range(0.0f, 1.0f)]
        [SerializeField] float _predictionMult = 0.6f;
        [Tooltip("the max number of ticks the client prediction is allowed to make"), Range(0, 100)]
        [SerializeField] int _maxPrediction = 10;

        [Tooltip("Send updates even if the transform hasn't changed. This will consume more bandwidth.")]
        [SerializeField] private bool _continuousSync = false;

        [Tooltip("Disable or enable local rotation synchronization. Rotation will be lerped, but not predicted.")]
        [SerializeField] private bool _syncRotation = true;
        [Tooltip("Disable or enable local scale synchronization. Scale will be lerped, but not predicted.")]
        [SerializeField] private bool _syncScale = false;

        [HideInInspector] internal Tick lastTick;
        [HideInInspector] internal Tick changeTick;

        [HideInInspector] internal Vector3 lastPos;
        [HideInInspector] internal Vector3 lastPosDelta;
        Vector3 _nextPosPrediction;
        Vector3 _predictedPos;

        [HideInInspector] internal Quaternion lastRot;

        [HideInInspector] internal Vector3 lastScale;

        void Update()
        {
            if (IsActive && Authority != Connection.LocalId)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, _predictedPos, _lerpMultiplier);
                if (_syncRotation)
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, lastRot, _lerpMultiplier);
                if (_syncScale)
                    transform.localScale = Vector3.Lerp(transform.localScale, lastScale, _lerpMultiplier);
            }
        }

        void FixedUpdate()
        {
            if (netcode == null)
                return;

            if (Connection.LocalId == netcode.Authority)
            {
                var delta = transform.localPosition - lastPos;

                if (delta.magnitude > 0f || _continuousSync)
                {
                    netcode.SendPosition(transform.localPosition.ToNetVec3());
                    lastPos = transform.localPosition;
                }

                var rotDelta = transform.localEulerAngles - lastRot.eulerAngles;
                if (_syncRotation && (rotDelta.magnitude > 0f || _continuousSync))
                {
                    netcode.SendRotation(transform.localEulerAngles.ToNetVec3());
                    lastRot = transform.localRotation;
                }

                var scaleDelta = transform.localScale - lastScale;
                if (_syncScale && (scaleDelta.magnitude > 0f || _continuousSync))
                {
                    netcode.SendScale(transform.localScale.ToNetVec3());
                    lastScale = transform.localScale;
                }
            }
            else
            {
                _nextPosPrediction = Vector3.Lerp(_nextPosPrediction,
                    lastPos + (lastPosDelta * Mathf.Min(Connection.LocalTick - lastTick, _maxPrediction)),
                    _predictionEasing * (Connection.LocalTick - changeTick));
                _predictedPos = Vector3.Lerp(lastPos, _nextPosPrediction, _predictionMult);
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

            var pos = transform.transform.localPosition.ToNetVec3();
            var rot = transform.transform.localEulerAngles.ToNetVec3();
            var scale = transform.transform.localScale.ToNetVec3();
            
            SendState(caller, pos, rot, scale);
        }

        [Rpc(RpcPerms.AnyToAll)]
        public virtual void SendState([CalleeId] ClientId callee, NetworkVec3 pos, NetworkVec3 rot, NetworkVec3 scale, [CallerId] ClientId caller = default)
        {
            if (caller != Authority)
                return;

            transform.lastPos = pos.ToVec3();
            transform.lastRot = Quaternion.Euler(rot.ToVec3());
            transform.lastScale = scale.ToVec3();
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendPosition(NetworkVec3 pos, [CallerId] ClientId caller = default)
        {
            if (caller != Authority || transform == null)
                return;

            var newDelta = pos.ToVec3() - transform.lastPos;
            if (newDelta.normalized != transform.lastPosDelta.normalized)
                transform.changeTick = Connection.LocalTick;
            transform.lastPosDelta = newDelta;
            transform.lastPos = pos.ToVec3();
            transform.lastTick = Connection.PresentTick;
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendRotation(NetworkVec3 rot, [CallerId] ClientId caller = default)
        {
            if (caller != Authority || transform == null)
                return;

            transform.lastRot = Quaternion.Euler(rot.ToVec3());
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendScale(NetworkVec3 scale, [CallerId] ClientId caller = default)
        {
            if (caller != Authority || transform == null)
                return;

            transform.lastScale = scale.ToVec3();
        }
    }
}
