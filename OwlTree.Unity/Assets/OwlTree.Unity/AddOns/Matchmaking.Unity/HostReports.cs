using System;
using OwlTree.Matchmaking;

namespace OwlTree.Matchmaking
{
    public static partial class Uris
    {
        public const string HostConnected = "/host-connected";
        public const string ClientCount = "/client-count";
        public const string Shutdown = "/shutdown";
        public const string HostPing = "/ping";
    }

    [Serializable]
    public class HostConnectedReport : HttpRequest<HostConnectedReport>
    {
        public string appId;
        public string sessionId;
    }

    [Serializable]
    public class ClientCountReport : HttpRequest<ClientCountReport>
    {
        public string appId;
        public string sessionId;
        public int clientCount;
    }

    [Serializable]
    public class SessionShutdownReport : HttpRequest<SessionShutdownReport>
    {
        public string appId;
        public string sessionId;
    }

    [Serializable]
    public class HostPingRequest : HttpRequest<HostPingRequest>
    {
        public string appId;
        public string sessionId;
        public long timestamp;
    }

    [Serializable]
    public class HostPingResponse : HttpResponse<HostPingResponse>
    {
        public long timestamp;
    }
}