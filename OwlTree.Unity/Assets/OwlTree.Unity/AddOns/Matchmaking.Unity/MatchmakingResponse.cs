
using System;
using UnityEngine;

namespace OwlTree.Matchmaking.Unity
{
    /// <summary>
    /// Matchmaking HTTP response codes.
    /// </summary>
    public enum ResponseCodes
    {
        /// <summary>
        /// An invalid response code, this should not be returned.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The matchmaking request was accepted.
        /// </summary>
        RequestAccepted = 200,

        /// <summary>
        /// The endpoint or URI was not found.
        /// </summary>
        NotFound = 404,
        /// <summary>
        /// The request failed to send.
        /// </summary>
        ExceptionThrow = 410,
        /// <summary>
        /// The endpoint rejected the matchmaking request.
        /// </summary>
        RequestRejected = 411
    }

    /// <summary>
    /// Sent by the matchmaking endpoint in response to a matchmaking request from a client.
    /// This will contain data needed to make an OwlTree Connection.
    /// </summary>
    [Serializable]
    public struct MatchmakingResponse
    {
        /// <summary>
        /// The HTTP response code.
        /// </summary>
        public ResponseCodes responseCode;

        /// <summary>
        /// Returns true if the response has a successful response code.
        /// </summary>
        public bool RequestSuccessful => 200 <= (int)responseCode && (int)responseCode <= 299;
        /// <summary>
        /// Returns true if the response has a failure response code.
        /// </summary>
        public bool RequestFailed => 400 <= (int)responseCode && (int)responseCode <= 499;

        /// <summary>
        /// The IP address of the server or relay connection.
        /// </summary>
        public string serverAddr;

        /// <summary>
        /// The UDP port of the server or relay connection.
        /// </summary>
        public int udpPort;

        /// <summary>
        /// The TCP port of the server or relay connection.
        /// </summary>
        public int tcpPort;

        /// <summary>
        /// The session id of the server or relay connection.
        /// </summary>
        public string sessionId;
        
        /// <summary>
        /// The session id of the server or relay connection.
        /// </summary>
        public string appId;

        /// <summary>
        /// Whether the created session is server authoritative, or relayed peer-to-peer.
        /// </summary>
        public ServerType serverType;

        /// <summary>
        /// Serializes the response to a JSON string.
        /// </summary>
        public string Serialize()
        {
            return JsonUtility.ToJson(this);
        }

        /// <summary>
        /// Deserializes a response from a JSON string.
        /// </summary>
        public static MatchmakingResponse Deserialize(string data)
        {
            return JsonUtility.FromJson<MatchmakingResponse>(data);
        }

        /// <summary>
        /// Response for a not found failure.
        /// </summary>
        public static MatchmakingResponse NotFound = new MatchmakingResponse{
            responseCode = ResponseCodes.NotFound
        };

        /// <summary>
        /// Response for an exception thrown failure.
        /// </summary>
        public static MatchmakingResponse ExceptionThrown = new MatchmakingResponse{
            responseCode = ResponseCodes.ExceptionThrow,
        };

        /// <summary>
        /// Response for a rejection failure.
        /// </summary>
        public static MatchmakingResponse RequestRejected = new MatchmakingResponse{
            responseCode = ResponseCodes.RequestRejected
        };
    }
}