using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OwlTree.Unity
{

    [CreateAssetMenu(fileName = "ConnectionArgs", menuName = "OwlTree/ConnectionArgs")]
    public class ConnectionArgs : ScriptableObject
    {
        [Tooltip("A unique, max 64 ASCII character id used for simple client verification.")]
        public string appId = "MyOwlTreeApp";

        [Tooltip("A unique, max 64 ASCII character id used to distinguish different sessions of the same app.")]
        public string sessionId = "MyAppSession";

        [Tooltip("True if this connection represents a client, false if this connection is an authoritative server.")]
        public bool isClient = true;

        [Tooltip("The server IP address.")]
        public string serverAddr = "127.0.0.1";

        [Tooltip("The server TCP port.")]
        public int tcpPort = 8000;

        [Tooltip("The server UDP port.")]
        public int udpPort = 9000;

        [Tooltip("The maximum number of clients the server will allow to be connected at once.")]
        public int maxClients = 4;

        [Tooltip(@"Whether or not a relayed peer-to-peer session can migrate hosts. 
A session that is migratable will re-assign the host if the current host disconnects.
A session that is not migratable will shutdown if the current host disconnects.")]
        public bool migratable = false;

        [Tooltip(@"Whether or not to automatically shutdown a relay connection if it becomes empty after
all clients disconnect. If false, then the relay must also allow host migration. This is 
controlled with the migratable argument, and will be set to true for you.")]
        public bool shutdownWhenEmpty = true;

        [Tooltip("The number of milliseconds clients will wait before sending another connection request to the server.")]
        public int connectionRequestRate = 5000;

        [Tooltip("The number of connection attempts clients will make before ending the connection in failure.")]
        public int connectionRequestLimit = 10;

        [Tooltip("The number of milliseconds servers will wait for clients to make the TCP handshake before timing out their connection request.")]
        public int connectionRequestTimeout = 20000;

        [Tooltip("The byte length of read and write buffers.")]
        public int bufferSize = 2048;

        [Tooltip(@"The version of Owl Tree this connection is running on. 
This value can be lowered from the default to use older formats of Owl Tree.")]
        public ushort owlTreeVersion = 1;

        [Tooltip("The minimum Owl Tree version that will be supported. If clients using an older version attempt to connect, they will be rejected.")]
        public ushort minOwlTreeVersion = 0;

        [Tooltip("The version of your app this connection is running on.")]
        public ushort appVersion = 1;

        [Tooltip("The minimum app version that will be supported. If clients using an older version attempt to connect, they will be rejected.")]
        public ushort minAppVersion = 0;

        [Tooltip("Adds Huffman encoding and decoding to the connection's read and send steps, with a priority of 100.")]
        public bool useCompression = true;

        [Tooltip(@"If false, Reading and writing to sockets will need to called by your program with Read()
and Send(). These operations will be done synchronously.

If true (Default), reading and writing will be handled autonomously in a separate, dedicated thread. 
Reading will fill a queue of RPCs to be executed in the main program thread by calling ExecuteQueue().
Reading and writing will be done at a regular frequency, as defined by the threadUpdateDelta arg.")]
        public bool threaded = true;

        [Tooltip("If the connection is threaded, specify the number of milliseconds the read/write thread will spend sleeping between updates.")]
        public int threadUpdateDelta = 40;

        [Tooltip("The list of prefabs this connection can spawn synchronously. Order matters.")]
        public GameObject[] prefabs;

        [Serializable]
        public struct LoggerIncludes
        {
            [Tooltip("Output when a NetworkObject or NetworkGameObject is spawned or despawned.")]
            public bool spawnEvents;
            [Tooltip("Output when a client connects, disconnects, or migrates authority.")]
            public bool clientEvents;
            [Tooltip(@"Output any connection attempts received if this connection is a server.
Or any connection attempts made if this connection is a client.")]
            public bool connectionAttempts;
            [Tooltip("On creating this connection, output all of the NetworkObject type ids it is aware of.")]
            public bool allTypeIds;
            [Tooltip("On creating this connection, output all of the RPC protocols it is aware of.")]
            public bool allRpcProtocols;
            [Tooltip("Output when an RPC is called on the local connection.")]
            public bool rpcCalls;
            [Tooltip("Output when an RPC call is received.")]
            public bool rpcReceives;
            [Tooltip("Output the argument byte encodings of called RPCs.")]
            public bool rpcCallEncodings;
            [Tooltip("Output the argument byte encodings received on incoming RPC calls.")]
            public bool rpcReceiveEncodings;
            [Tooltip("Output TCP packets in full, before any transformer steps are applied.")]
            public bool tcpPreTransform;
            [Tooltip("Output TCP packets in full, after all transformer steps are applied.")]
            public bool tcpPostTransform;
            [Tooltip("Output UDP packets in full, before any transformer steps are applied.")]
            public bool udpPreTransform;
            [Tooltip("Output UDP packets in full, after all transformer steps are applied.")]
            public bool udpPostTransform;
            [Tooltip("Output any exceptions thrown during this connection's runtime.")]
            public bool exceptions;
            [Tooltip("Output bars '===' to visually separate logs.")]
            public bool logSeparators;
            [Tooltip("Output a timestamp with each log message.")]
            public bool logTimestamp;

            public Logger.IncludeRules ToRules()
            {
                var rules = Logger.Includes();
                if (spawnEvents) rules = rules.SpawnEvents();
                if (clientEvents) rules = rules.ClientEvents();
                if (connectionAttempts) rules = rules.ConnectionAttempts();
                if (allTypeIds) rules = rules.AllTypeIds();
                if (allRpcProtocols) rules = rules.AllRpcProtocols();
                if (rpcCalls) rules = rules.RpcCalls();
                if (rpcReceives) rules = rules.RpcReceives();
                if (rpcCallEncodings) rules = rules.RpcCallEncodings();
                if (rpcReceiveEncodings) rules = rules.RpcReceiveEncodings();
                if (tcpPreTransform) rules = rules.TcpPreTransform();
                if (tcpPostTransform) rules = rules.TcpPostTransform();
                if (udpPreTransform) rules = rules.UdpPreTransform();
                if (udpPostTransform) rules = rules.UdpPostTransform();
                if (exceptions) rules = rules.Exceptions();
                if (logSeparators) rules = rules.LogSeparators();
                if (logTimestamp) rules = rules.LogTimestamp();
                return rules;
            }
        }

        [Tooltip("Records how much data is being send and received. Adds a read step with a priority of 0, and a send step with a priority of 200.")]
        public bool measureBandwidth = false;

        [Tooltip("Enable different logger rules to customize the diagnostics displayed.")]
        public LoggerIncludes verbosity;

        public UnityConnection.Args GetArgs()
        {
            return new UnityConnection.Args{
                appId = appId,
                sessionId = sessionId,
                role = isClient ? NetRole.Client : NetRole.Server,
                serverAddr = serverAddr,
                tcpPort = tcpPort,
                udpPort = udpPort,
                maxClients = maxClients,
                migratable = migratable,
                shutdownWhenEmpty = shutdownWhenEmpty,
                connectionRequestLimit = connectionRequestLimit,
                connectionRequestTimeout = connectionRequestTimeout,
                bufferSize = bufferSize,
                owlTreeVersion = owlTreeVersion,
                minOwlTreeVersion = minOwlTreeVersion,
                appVersion = appVersion,
                minAppVersion = minAppVersion,
                measureBandwidth = measureBandwidth,
                useCompression = useCompression,
                threaded = threaded,
                threadUpdateDelta = threadUpdateDelta,
                logger = Debug.Log,
                verbosity = verbosity.ToRules(),
                prefabs = prefabs
            };
        }
    }
}
