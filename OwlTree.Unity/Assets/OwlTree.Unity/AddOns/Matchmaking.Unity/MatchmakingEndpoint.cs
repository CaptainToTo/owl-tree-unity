
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OwlTree.Matchmaking
{
    /// <summary>
    /// Run on the server to listen for matchmaking requests from a matchmaking client.
    /// </summary>
    public class MatchmakingEndpoint
    {
        public delegate Task<SessionCreationResponse> ProcessCreationRequest(IPAddress client, SessionCreationRequest request);

        public delegate Task<SessionPublishResponse> ProcessPublishRequest(IPAddress client, SessionPublishRequest request);

        public delegate Task<SessionDataResponse> ProcessSessionRequest(IPAddress client, SessionDataRequest request);

        public delegate Task<MatchmakingTicketResponse> ProcessMatchmakingRequest(IPAddress client, MatchmakingTicketRequest request);

        public delegate Task<TicketStatusResponse> ProcessTicketRequest(IPAddress client, TicketStatusRequest request);

        private HttpListener _listener;

        private ProcessCreationRequest createSession = null;
        private ProcessPublishRequest publishSession = null;
        private ProcessSessionRequest getSession = null;
        private ProcessMatchmakingRequest getTicket = null;
        private ProcessTicketRequest getTicketStatus = null;

        /// <summary>
        /// Create a new matchmaking endpoint that will listen to the given domain.
        /// </summary>
        public MatchmakingEndpoint(
            string domain,
            ProcessCreationRequest createSession = null,
            ProcessPublishRequest publishSession = null,
            ProcessSessionRequest getSession = null,
            ProcessMatchmakingRequest getTicket = null,
            ProcessTicketRequest getTicketStatus = null
        )
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(domain);
            this.createSession = createSession;
            this.publishSession = publishSession;
            this.getSession = getSession;
            this.getTicket = getTicket;
            this.getTicketStatus = getTicketStatus;
        }

        /// <summary>
        /// Create a new matchmaking endpoint that will listen to the given domains.
        /// </summary>
        public MatchmakingEndpoint(
            IEnumerable<string> domains,
            ProcessCreationRequest createSession = null,
            ProcessPublishRequest publishSession = null,
            ProcessSessionRequest getSession = null,
            ProcessMatchmakingRequest getTicket = null,
            ProcessTicketRequest getTicketStatus = null
        )
        {
            _listener = new HttpListener();
            foreach (var domain in domains)
                _listener.Prefixes.Add(domain);
            this.createSession = createSession;
            this.publishSession = publishSession;
            this.getSession = getSession;
            this.getTicket = getTicket;
            this.getTicketStatus = getTicketStatus;
        }

        /// <summary>
        /// Returns true if endpoint is currently listening for matchmaking requests.
        /// </summary>
        public bool IsActive { get; private set; } = false;

        /// <summary>
        /// Start listening for matchmaking requests asynchronously.
        /// </summary>
        public async void Start()
        {
            if (createSession == null || publishSession == null || getSession == null ||
                getTicket == null || getTicketStatus == null)
                throw new MissingMemberException("All request handler callbacks must be assigned to start the endpoint.");


            _listener.Start();
            IsActive = true;

            while (IsActive)
            {
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                try
                {
                    switch (request.Url?.AbsolutePath ?? "/")
                    {
                        case Uris.CreateSession:
                            {
                                var responseObj = await CreateSession(request);
                                WriteOutResponse(response, responseObj.Serialize(), responseObj.responseCode);
                                break;
                            }
                        case Uris.PublishSession:
                            {
                                var responseObj = await PublishSession(request);
                                WriteOutResponse(response, responseObj.Serialize(), responseObj.responseCode);
                                break;
                            }
                        case Uris.SessionData:
                            {
                                var responseObj = await GetSession(request);
                                WriteOutResponse(response, responseObj.Serialize(), responseObj.responseCode);
                                break;
                            }

                        case Uris.GetTicket:
                            {
                                var responseObj = await GetTicket(request);
                                WriteOutResponse(response, responseObj.Serialize(), responseObj.responseCode);
                                break;
                            }
                        case Uris.TicketStatus:
                            {
                                var responseObj = await GetTicketStatus(request);
                                WriteOutResponse(response, responseObj.Serialize(), responseObj.responseCode);
                                break;
                            }

                        default:
                            response.StatusCode = (int)ResponseCodes.NotFound;
                            break;
                    }
                }
                catch
                {
                    response.StatusCode = (int)ResponseCodes.RequestRejected;
                }

                response.OutputStream.Close();
            }

            _listener.Close();
        }

        private async Task<SessionCreationResponse> CreateSession(HttpListenerRequest request)
        {
            var source = GetSource(request);
            var requestObj = Deserialize<SessionCreationRequest>(request);
            return await createSession.Invoke(source, requestObj);
        }

        private async Task<SessionPublishResponse> PublishSession(HttpListenerRequest request)
        {
            var source = GetSource(request);
            var requestObj = Deserialize<SessionPublishRequest>(request);
            if (requestObj.hostAddr == "*")
                requestObj.hostAddr = source.ToString();
            return await publishSession.Invoke(source, requestObj);
        }

        private async Task<SessionDataResponse> GetSession(HttpListenerRequest request)
        {
            var source = GetSource(request);
            var requestObj = Deserialize<SessionDataRequest>(request);
            return await getSession.Invoke(source, requestObj);
        }

        private async Task<MatchmakingTicketResponse> GetTicket(HttpListenerRequest request)
        {
            var source = GetSource(request);
            var requestObj = Deserialize<MatchmakingTicketRequest>(request);
            return await getTicket.Invoke(source, requestObj);
        }

        private async Task<TicketStatusResponse> GetTicketStatus(HttpListenerRequest request)
        {
            var source = GetSource(request);
            var requestObj = Deserialize<TicketStatusRequest>(request);
            return await getTicketStatus.Invoke(source, requestObj);
        }

        private IPAddress GetSource(HttpListenerRequest request)
        {
            return request.Headers["X-Real-IP"] != null ? IPAddress.Parse(request.Headers["X-Real-IP"]) : request.RemoteEndPoint.Address;
        }

        private T Deserialize<T>(HttpListenerRequest request) where T : HttpRequest<T>
        {
            string requestBody = new StreamReader(request.InputStream, Encoding.UTF8).ReadToEnd();
            return HttpRequest<T>.Deserialize<T>(requestBody);
        }

        private void WriteOutResponse(HttpListenerResponse response, string responseBody, ResponseCodes responseCode)
        {
            response.StatusCode = (int)responseCode;
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Close the endpoint.
        /// </summary>
        public void Close()
        {
            IsActive = false;
        }
    }
}