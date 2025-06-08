using System;
using UnityEngine;

namespace OwlTree.Unity
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class NetworkRigidbody2D : NetworkBehaviour
    {
        internal Rigidbody2DNetcode netcode = null;

        public override void OnSpawn()
        {
            if (Connection.IsAuthority)
            {
                netcode = Connection.Spawn<Rigidbody2DNetcode>();
                netcode.rb = this;
            }
        }

        public override void OnDespawn()
        {
            if (Connection.IsAuthority && netcode != null)
                Connection.Despawn(netcode);
        }

        /// <summary>
        /// The authority of this rigidbody. This can be an client, not just the connection authority.
        /// </summary>
        public ClientId Authority => netcode.Authority;

        /// <summary>
        /// Assign a client as the authority of this rigidbody. This can only be done
        /// by the connection authority.
        /// </summary>
        public void SetAuthority(ClientId authority)
        {
            if (Connection.IsAuthority)
                netcode.SetAuthority(authority);
            else
                throw new InvalidOperationException("Non-authority clients cannot assign shared authority of NetworkRigidbodys.");
        }

        /// <summary>
        /// Invoke to transfer shared authority of this rigidbody to
        /// the given client.
        /// </summary>
        public void OnEnterAreaOfInfluence(ClientId owner)
        {
            if (owner != Authority)
                SetAuthority(owner);
        }

        /// <summary>
        /// Invoke to transfer authority off of the given client.
        /// if that client is the current authority of this rigidbody, authority will be
        /// given back to the session authority.
        /// </summary>
        public void OnExitAreaOfInfluence(ClientId owner)
        {
            if (owner == Authority)
                SetAuthority(Connection.Authority);
        }

        /// <summary>
        /// The rigidbody this component is synchronizing.
        /// </summary>
        public Rigidbody2D Rb
        {
            get
            {
                if (_rb == null)
                    _rb = GetComponent<Rigidbody2D>();
                return _rb;
            }
        }
        private Rigidbody2D _rb = null;

        /// <summary>
        /// Whether or not the rigidbody is currently active and moving.
        /// If it is resting, then it will not be sending updates.
        /// </summary>
        public bool Resting
        {
            get => _resting;
            set => _resting = value;
        }
        private bool _resting = false;

        [HideInInspector] internal Vector3 lastPos;
        [HideInInspector] internal float lastRot;

        [SerializeField] float updateRequestThreshold = 1f;
        private bool updateRequestSent = false;

        internal float lastUpdate = 0;

        void FixedUpdate()
        {
            if (netcode == null)
                return;

            // the assigned authority sends state updates
            if (Connection.LocalId == netcode.Authority)
            {
                var posDelta = (transform.localPosition - lastPos).magnitude;
                var rotDelta = transform.localEulerAngles.z - lastRot;

                if (posDelta > 0 || rotDelta > 0)
                {
                    netcode.SendState(
                        transform.localPosition.ToNetVec2(), Rb.linearVelocity.ToNetVec2(),
                        transform.localEulerAngles.z, Rb.angularVelocity
                    );
                    Resting = false;
                
                // if rb hasn't moved, send special resting message for clients to lock object in place
                }
                else if (!Resting)
                {
                    netcode.SendResting(transform.localPosition.ToNetVec2(), transform.localEulerAngles.z);
                    Resting = true;
                }

                lastPos = transform.localPosition;
                lastRot = transform.localEulerAngles.z;

            // clients lock object in place until authority sends a new update
            }
            else if (Resting)
            {
                transform.localPosition = lastPos;
                transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, lastRot);
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = 0;
                updateRequestSent = false;

                // if the client hasn't received the resting message, but hasn't gotten any updates
                // request a resend on the resting message
            }
            else if (Time.time - lastUpdate > updateRequestThreshold && !updateRequestSent)
            {
                netcode.RequestUpdate(netcode.Authority);
                updateRequestSent = true;
            }
        }
    }

    public class Rigidbody2DNetcode : NetworkObject
    {
        internal NetworkRigidbody2D rb = null;

        public ClientId Authority { get; private set; }

        public override void OnSpawn()
        {
            if (!Connection.IsAuthority)
                RequestRb();
            Authority = Connection.Authority;
        }

        [Rpc(RpcPerms.ClientsToAuthority)]
        public virtual void RequestRb([CallerId] ClientId caller = default) {
            SendRb(caller, rb.NetObject.Id,
                rb.transform.localPosition.ToNetVec3(),
                rb.transform.localEulerAngles.ToNetVec3(),
                rb.Resting,
                Authority);
        }

        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void SendRb([CalleeId] ClientId callee, GameObjectId id, NetworkVec3 pos,
            NetworkVec3 rot, bool resting, ClientId authority)
        {
            Connection.WaitForObject<GameObjectId, NetworkGameObject>(id, (obj) => {
                rb = obj.GetComponent<NetworkRigidbody2D>();
                rb.netcode = this;
                Authority = authority;
                rb.transform.localPosition = pos.ToVec3();
                rb.transform.localEulerAngles = rot.ToVec3();
                rb.lastPos = rb.transform.localPosition;
                rb.lastRot = rb.transform.localEulerAngles.z;
                rb.Resting = resting;
                rb.lastUpdate = Time.time;
            });
        }

        internal void OnEnterAreaOfInfluence(ClientId owner)
        {
            if (owner != Authority)
                SetAuthority(owner);
        }

        // return authority to the session authority
        internal void OnExitAreaOfInfluence(ClientId owner)
        {
            if (owner == Authority)
                SetAuthority(Connection.Authority);
        }

        internal void SetAuthority(ClientId newAuthority)
        {
            if (Connection.IsAuthority)
            {
                Authority = newAuthority;
                SendAuthority(newAuthority);
            }
        }

        [Rpc(RpcPerms.AuthorityToClients)]
        public virtual void SendAuthority(ClientId newAuthority)
        {
            Authority = newAuthority;
        }
        
        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendState(NetworkVec2 pos, NetworkVec2 vel, float rot, float angVel, 
            [CallerId] ClientId caller = default)
        {
            if (rb == null) return;

            // state updates will be sent from the client who was given authority
            if (caller != Authority) return;

            rb.transform.localPosition = pos.ToVec2();
            rb.Rb.linearVelocity = vel.ToVec2();
            rb.transform.localEulerAngles = new Vector3(
                rb.transform.localEulerAngles.x,
                rb.transform.localEulerAngles.y,
                rot);
            rb.Rb.angularVelocity = angVel;
            rb.Resting = false;
            rb.lastUpdate = Time.time;
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void SendResting(NetworkVec2 pos, float rot, [CallerId] ClientId caller = default)
        {
            if (rb == null || caller != Authority) return;

            rb.transform.localPosition = pos.ToVec2();
            rb.lastPos = rb.transform.localPosition;
            rb.transform.localEulerAngles = new Vector3(
                rb.transform.localEulerAngles.x,
                rb.transform.localEulerAngles.y,
                rot);
            rb.lastRot = rot;

            rb.Rb.linearVelocity = Vector3.zero;
            rb.Rb.angularVelocity = 0;
            rb.Resting = true;
            rb.lastUpdate = Time.time;
        }

        [Rpc(RpcPerms.AnyToAll, RpcProtocol = Protocol.Udp)]
        public virtual void RequestUpdate([CalleeId] ClientId callee, [CallerId] ClientId caller = default)
        {
            if (rb.Resting)
            {
                SendResting(
                    rb.transform.localPosition.ToNetVec2(), 
                    rb.transform.localEulerAngles.z);
            }
        }
    }
}

