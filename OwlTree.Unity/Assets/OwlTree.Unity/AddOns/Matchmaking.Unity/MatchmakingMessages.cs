using System;
using OwlTree;

namespace OwlTree.Matchmaking
{
    public static partial class Uris
    {
        public const string GetTicket = "/matchmaking";
        public const string TicketStatus = "/tickets";
    }

    /// <summary>
    /// Submit a matchmaking request to have a ticket queued
    /// for the given party of players
    /// </summary>
    [Serializable]
    public class MatchmakingTicketRequest : HttpRequest<MatchmakingTicketRequest>
    {
        public string appId;
        public string partyId;
        public string[] playerIds;
    }

    /// <summary>
    /// If ticket was successfully queued, contains the ticket id
    /// and time to completion and expiration.
    /// </summary>
    [Serializable]
    public class MatchmakingTicketResponse : HttpResponse<MatchmakingTicketResponse>
    {
        public string ticketId;
        public int expectedQueueTime;
        public long lifetime;
    }

    /// <summary>
    /// Request the current status of the given ticket.
    /// </summary>
    [Serializable]
    public class TicketStatusRequest : HttpRequest<TicketStatusRequest>
    {
        public string ticketId;
    }

    /// <summary>
    /// Contains the current status of the requested ticket in the 
    /// response code. If ticket is complete, contains connection
    /// info for server.
    /// </summary>
    [Serializable]
    public class TicketStatusResponse : HttpResponse<TicketStatusResponse>
    {
        public string serverAddr;
        public int tcpPort;
        public int udpPort;
        public SimulationSystem simulationSystem;
        public int tickRate;
    }
}