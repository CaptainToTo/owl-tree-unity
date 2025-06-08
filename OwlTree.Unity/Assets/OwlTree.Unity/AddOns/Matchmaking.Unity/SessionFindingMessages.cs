using System;
using OwlTree;

namespace OwlTree.Matchmaking
{
    public static partial class Uris
    {
        public const string CreateSession = "/create-session";
        public const string PublishSession = "/publish-session";
        public const string SessionData = "/get-session";
    }


    /// <summary>
    /// Sent by a host in a relayed peer-to-peer architecture to 
    /// have a new relay server be made for session they will host.
    /// </summary>
    [Serializable]
    public class SessionCreationRequest : HttpRequest<SessionCreationRequest>
    {
        public string appId;
        public string sessionId;
        public int maxClients;
        public bool migratable;
        public SimulationSystem simulationSystem;
        public int tickRate;
    }

    /// <summary>
    /// If the host's request was successful, will contain the endpoint
    /// info for the relay server.
    /// </summary>
    [Serializable]
    public class SessionCreationResponse : HttpResponse<SessionCreationResponse>
    {
        public string serverAddr;
        public int tcpPort;
        public int udpPort;
    }

    /// <summary>
    /// Sent by clients to request connection data for a given session
    /// from a given app.
    /// </summary>
    [Serializable]
    public class SessionDataRequest : HttpRequest<SessionDataRequest>
    {
        public string appId;
        public string sessionId;
    }

    /// <summary>
    /// If client's request was successful, will contain the connection
    /// info needed to connect the requested session.
    /// </summary>
    [Serializable]
    public class SessionDataResponse : HttpResponse<SessionDataResponse>
    {
        public string serverAddr;
        public int tcpPort;
        public int udpPort;
        public int maxClients;
        public bool migratable;
        public SimulationSystem simulationSystem;
        public int tickRate;
    }

    /// <summary>
    /// Sent by a host in a peer-to-peer architecture to publish
    /// their session to the session finding service.
    /// </summary>
    [Serializable]
    public class SessionPublishRequest : HttpRequest<SessionPublishRequest>
    {
        public string appId;
        public string sessionId;
        public int maxPlayers;
        public string hostAddr = "*";
        public int tcpPort;
        public int udpPort;
        public SimulationSystem simulationSystem;
        public int tickRate;
    }

    /// <summary>
    /// If host's publishing request was successful, will contain
    /// the endpoint the host was assign for reporting session events
    /// to the server.
    /// </summary>
    [Serializable]
    public class SessionPublishResponse : HttpResponse<SessionPublishResponse>
    {
        public string reportingEndpoint;
    }
}