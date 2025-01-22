using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Super class that declares the interface for client and server buffers.
    /// </summary>
    public abstract class NetworkBuffer
    {
        /// <summary>
        /// Function signature used to decode raw byte arrays into Message structs.
        /// </summary>
        public delegate bool Decoder(ClientId caller, ReadOnlySpan<byte> bytes, out Message message);

        /// <summary>
        /// Function signature used to encode a Message struct into raw bytes.
        /// </summary>
        public delegate void Encoder(Message message, Packet buffer);

        public struct Args
        {
            public ushort owlTreeVer;
            public ushort minOwlTreeVer;
            public ushort appVer;
            public ushort minAppVer;
            public string appId;
            public string sessionId;

            public string addr;
            public int serverTcpPort;
            public int serverUdpPort;

            public int bufferSize;

            public Decoder decoder;
            public Encoder encoder;

            public Logger logger;
        }

        /// <summary>
        /// The size of read and write buffers in bytes.
        /// Exceeding this size will result in lost data.
        /// </summary>
        public int BufferSize { get; private set; }

        protected byte[] ReadBuffer;
        protected Packet ReadPacket;

        // ip and port number this client is bound to
        public int ServerTcpPort { get; protected set; } 
        public int ServerUdpPort { get; protected set; }
        public IPAddress Address { get; private set; }

        public abstract int LocalTcpPort();

        public abstract int LocalUdpPort();

        public int MaxClients { get; protected set; } = int.MaxValue;

        public ushort OwlTreeVersion { get; private set; }
        public ushort MinOwlTreeVersion { get; private set; }
        public ushort AppVersion { get; private set; }
        public ushort MinAppVersion { get; private set; }
        public StringId ApplicationId { get; private set; }
        public StringId SessionId { get; private set; }

        protected Logger Logger { get; private set; }

        public NetworkBuffer(Args args)
        {
            if (args.owlTreeVer < args.minOwlTreeVer)
            {
                throw new ArgumentException("The local connection instance is using an older version of Owl Tree than the minimum requirement.");
            }

            if (args.appVer < args.minAppVer)
            {
                throw new ArgumentException("The local connection instance is using an older app version than the minimum requirement.");
            }

            OwlTreeVersion = args.owlTreeVer;
            MinOwlTreeVersion = args.minOwlTreeVer;
            AppVersion = args.appVer;
            MinAppVersion = args.minAppVer;
            ApplicationId = new StringId(args.appId);
            SessionId = new StringId(args.sessionId);

            Address = IPAddress.Parse(args.addr);
            ServerTcpPort = args.serverTcpPort;
            ServerUdpPort = args.serverUdpPort;
            BufferSize = args.bufferSize;
            ReadBuffer = new byte[BufferSize];
            TryDecode = args.decoder;
            Encode = args.encoder;
            ReadPacket = new Packet(BufferSize);

            _pingRequests = new PingRequestList(3000);

            Logger = args.logger;
            IsActive = true;
        }

        protected void PacketToString(Packet p, StringBuilder str)
        {
            var packet = p.GetPacket();
            for (int i = 0; i < packet.Length; i++)
            {
                str.Append(packet[i].ToString("X2"));
                if (i < packet.Length - 1)
                    str.Append('-');
                if (i % 32 == 0 && i != 0)
                    str.Append('\n');
            }
        }

        protected string PacketToString(Packet p)
        {
            var str = "";
            var packet = p.GetPacket();
            for (int i = 0; i < packet.Length; i++)
            {
                str += packet[i].ToString("X2");
                if (i < packet.Length - 1)
                    str += '-';
                if (i % 32 == 0 && i != 0)
                    str += '\n';
            }
            return str;
        }

        /// <summary>
        /// Invoked when a new client connects.
        /// </summary>
        public ClientId.Delegate OnClientConnected;

        /// <summary>
        /// Invoked when a client disconnects.
        /// </summary>
        public ClientId.Delegate OnClientDisconnected;

        /// <summary>
        /// Invoked when the local connection is ready. Provides the local ClientId.
        /// If this is a server instance, then the ClientId will be <c>ClientId.None</c>.
        /// </summary>
        public ClientId.Delegate OnReady;

        /// <summary>
        /// Invoked when the session authority changes. Provides the client id
        /// of the new authority.
        /// </summary>
        public ClientId.Delegate OnHostMigration;

        /// <summary>
        /// Injected decoding scheme for messages.
        /// </summary>
        protected Decoder TryDecode;

        /// <summary>
        /// Injected encoding scheme for messages.
        /// </summary>
        protected Encoder Encode;

        /// <summary>
        /// Whether or not the connection is ready. 
        /// For clients, this means the server has assigned it a ClientId.
        /// </summary>
        public bool IsReady { get; protected set; } = false;

        /// <summary>
        /// Whether or not the connection is still active. If a client has disconnected from the server,
        /// or failed to connect to the server, IsActive will be set to false.
        /// </summary>
        public bool IsActive { get; protected set; } = false;

        /// <summary>
        /// The client id for the local instance. A server's local id will be <c>ClientId.None</c>
        /// </summary>
        public ClientId LocalId { get; protected set; } = ClientId.None;

        /// <summary>
        /// The client id of the authority in this session. 
        /// If this session is server authoritative, then this will be <c>ClientId.None</c>.
        /// </summary>
        public ClientId Authority { get; protected set; } = ClientId.None;

        // currently read messages
        protected ConcurrentQueue<Message> _incoming = new ConcurrentQueue<Message>();

        protected ConcurrentQueue<Message> _outgoing = new ConcurrentQueue<Message>();

        /// <summary>
        /// True if there are messages that are waiting to be sent.
        /// </summary>
        public bool HasOutgoing { get { return _outgoing.Count > 0 || HasClientEvent || HasRelayMessages; } }

        protected bool HasClientEvent = false;

        protected bool HasRelayMessages = false;
        
        /// <summary>
        /// Get the next message in the read queue.
        /// </summary>
        /// <param name="message">The next message.</param>
        /// <returns>True if there is a message, false if the queue is empty.</returns>
        public bool GetNextMessage(out Message message)
        {
            if (_incoming.Count == 0)
            {
                message = Message.Empty;
                return false;
            }
            return _incoming.TryDequeue(out message);
        }

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public abstract void Recv();

        /// <summary>
        /// Add message to outgoing message queue.
        /// Actually send buffers to sockets with <c>Send()</c>.
        /// </summary>
        public void AddMessage(Message message)
        {
            _outgoing.Enqueue(message);
        }

        /// <summary>
        /// Send current buffers to associated sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public abstract void Send();

        protected PingRequestList _pingRequests;

        /// <summary>
        /// Send a ping to the targeted client.
        /// </summary>
        public PingRequest Ping(ClientId target)
        {
            var request = _pingRequests.Add(LocalId, target);
            var message = new Message(LocalId, target, new RpcId(RpcId.PingRequestId), NetworkId.None, Protocol.Tcp, RpcPerms.AnyToAll);
            message.bytes = new byte[PingRequestLength];
            PingRequestEncode(message.bytes, request);
            AddMessage(message);
            return request;
        }

        /// <summary>
        /// Used by connections receiving a ping request to send the response back to ping sender.
        /// </summary>
        protected void PingResponse(PingRequest request)
        {
            request.PingReceived();
            var message = new Message(LocalId, request.Source, new RpcId(RpcId.PingRequestId), NetworkId.None, Protocol.Tcp, RpcPerms.AnyToAll);
            message.bytes = new byte[PingRequestLength];
            PingRequestEncode(message.bytes, request);
            AddMessage(message);
        }

        protected void PingTimeout(PingRequest request)
        {
            request.PingFailed();
            var message = new Message(LocalId, LocalId, new RpcId(RpcId.PingRequestId), NetworkId.None, Protocol.Tcp, RpcPerms.AnyToAll, new object[]{request});
            _incoming.Enqueue(message);
        }
        
        /// <summary>
        /// Function signature for transformer steps. Should return the same span of bytes
        /// provided as an argument.
        /// </summary>
        /// <returns></returns>
        public delegate void BufferAction(Packet packet);

        /// <summary>
        /// Use to add transformer steps to sending and reading.
        /// Specify the priority to sort the order of transformers.
        /// Sorted in ascending order.
        /// </summary>
        public struct Transformer
        {
            public int priority;
            public BufferAction step;
        }

        // buffer transformer steps
        private List<Transformer> _sendProcess = new List<Transformer>();
        private List<Transformer> _readProcess = new List<Transformer>();

        /// <summary>
        /// Adds the given transformer step to the send process.
        /// The provided BufferAction will be applied to all buffers sent.
        /// </summary>
        public void AddSendStep(Transformer step)
        {
            for (int i = 0; i < _sendProcess.Count; i++)
            {
                if (_sendProcess[i].priority > step.priority)
                {
                    _sendProcess.Insert(i, step);
                    return;
                }
            }
            _sendProcess.Add(step);
        }

        /// <summary>
        /// Apply all of the currently added send transformer steps. Returns the 
        /// same span, with transformations applied to the underlying bytes.
        /// </summary>
        protected void ApplySendSteps(Packet packet)
        {
            foreach (var step in _sendProcess)
            {
                step.step(packet);
            }
        }

        /// <summary>
        /// Adds the given transformer step to the read process.
        /// The provided BufferAction will be applied to all buffers that are received.
        /// </summary>
        public void AddReadStep(Transformer step)
        {
            for (int i = 0; i < _readProcess.Count; i++)
            {
                if (_readProcess[i].priority > step.priority)
                {
                    _readProcess.Insert(i, step);
                    return;
                }
            }
            _readProcess.Add(step);
        }
        
        /// <summary>
        /// Apply all of the currently added read transformer steps. Returns the 
        /// same span, with transformations applied to the underlying bytes.
        /// </summary>
        protected void ApplyReadSteps(Packet packet)
        {
            foreach (var step in _readProcess)
            {
                step.step(packet);
            }
        }

        /// <summary>
        /// Close the local connection.
        /// Invokes <c>OnClientDisconnected</c> with the local ClientId.
        /// </summary>
        public abstract void Disconnect();
        
        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public abstract void Disconnect(ClientId id);
        
        /// <summary>
        /// Change the authority of the session to the given new host.
        /// The previous host will be down-graded to a client if they are still connected.
        /// </summary>
        public abstract void MigrateHost(ClientId newHost);

        // * Connection and Disconnection Message Protocols

        /// <summary>
        /// The number of bytes required to encode client events.
        /// </summary>
        protected static int ClientMessageLength => RpcId.MaxByteLength + ClientId.MaxByteLength;

        /// <summary>
        /// The number of bytes required to encode the local client connected event.
        /// </summary>
        protected static int LocalClientConnectLength => RpcId.MaxByteLength + ClientIdAssignment.MaxLength();

        /// <summary>
        /// The number of bytes required to encode a new connection request.
        /// </summary>
        protected static int ConnectionRequestLength => RpcId.MaxByteLength + ConnectionRequest.MaxLength();

        /// <summary>
        /// The number of bytes required to encode a ping request.
        /// </summary>
        protected static int PingRequestLength => RpcId.MaxByteLength + PingRequest.MaxLength();

        protected static void ClientConnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.ClientConnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        protected static void LocalClientConnectEncode(Span<byte> bytes, ClientIdAssignment assignment)
        {
            var rpcId = new RpcId(RpcId.LocalClientConnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            assignment.InsertBytes(bytes.Slice(ind));
        }

        protected static void ClientDisconnectEncode(Span<byte> bytes, ClientId id)
        {
            var rpcId = new RpcId(RpcId.ClientDisconnectedId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            id.InsertBytes(bytes.Slice(ind, id.ByteLength()));
        }

        protected static void ConnectionRequestEncode(Span<byte> bytes, ConnectionRequest request)
        {
            var rpc = new RpcId(RpcId.ConnectionRequestId);
            var ind = rpc.ByteLength();
            rpc.InsertBytes(bytes);
            request.InsertBytes(bytes.Slice(ind));
        }

        protected static void HostMigrationEncode(Span<byte> bytes, ClientId newHost)
        {
            var rpcId = new RpcId(RpcId.HostMigrationId);
            var ind = rpcId.ByteLength();
            rpcId.InsertBytes(bytes.Slice(0, ind));
            newHost.InsertBytes(bytes.Slice(ind, newHost.ByteLength()));
        }

        protected static void PingRequestEncode(Span<byte> bytes, PingRequest request)
        {
            var rpcId = new RpcId(RpcId.PingRequestId);
            rpcId.InsertBytes(bytes);
            request.InsertBytes(bytes.Slice(rpcId.ByteLength()));
        }

        protected static RpcId ServerMessageDecode(ReadOnlySpan<byte> bytes, out ConnectionRequest connectRequest)
        {
            RpcId result = RpcId.None;
            result.FromBytes(bytes);
            connectRequest = new ConnectionRequest();
            switch (result.Id)
            {
                case RpcId.ConnectionRequestId:
                    connectRequest.FromBytes(bytes.Slice(result.ByteLength()));
                    break;
            }
            return result;
        }

        protected static bool TryClientMessageDecode(ReadOnlySpan<byte> bytes, out RpcId rpcId)
        {
            rpcId = new RpcId(bytes);
            switch(rpcId.Id)
            {
                case RpcId.ClientConnectedId:
                case RpcId.LocalClientConnectedId:
                case RpcId.ClientDisconnectedId:
                case RpcId.HostMigrationId:
                    return true;
            }
            return false;
        }

        protected static bool TryPingRequestDecode(ReadOnlySpan<byte> bytes, out PingRequest request)
        {
            var rpcId = new RpcId(bytes);
            request = null;
            if (rpcId.Id != RpcId.PingRequestId)
                return false;
            request = new PingRequest(bytes.Slice(rpcId.ByteLength()));
            return true;
        }
    }
}