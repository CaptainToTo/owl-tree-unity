using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OwlTree.Matchmaking
{
    /// <summary>
    /// Use to send matchmaking requests to your matchmaking endpoint.
    /// </summary>
    public class MatchmakingClient
    {
        /// <summary>
        /// The URL this client will send requests to.
        /// </summary>
        public string EndpointUrl { get; private set; }

        private HttpClient _client;

        /// <summary>
        /// Use to send matchmaking requests to your matchmaking endpoint.
        /// </summary>
        public MatchmakingClient(string endpointUrl)
        {
            EndpointUrl = endpointUrl;
            _client = new HttpClient();
        }

        /// <summary>
        /// Create a hosted relay session.
        /// </summary>
        public async Task<SessionCreationResponse> CreateSession(SessionCreationRequest request)
        {
            return await SendRequest<SessionCreationResponse>(request.Serialize(), Uris.CreateSession);
        }

        /// <summary>
        /// Publish a peer-to-peer session.
        /// </summary>
        public async Task<SessionPublishResponse> PublishSession(SessionPublishRequest request)
        {
            return await SendRequest<SessionPublishResponse>(request.Serialize(), Uris.PublishSession);
        }

        /// <summary>
        /// Get the connection args for a session.
        /// </summary>
        public async Task<SessionDataResponse> GetSession(SessionDataRequest request)
        {
            return await SendRequest<SessionDataResponse>(request.Serialize(), Uris.SessionData);
        }

        /// <summary>
        /// Join the matchmaking queue.
        /// </summary>
        public async Task<MatchmakingTicketResponse> GetTicket(MatchmakingTicketRequest request)
        {
            return await SendRequest<MatchmakingTicketResponse>(request.Serialize(), Uris.GetTicket);
        }

        /// <summary>
        /// Get the status of an existing matchmaking ticket.
        /// </summary>
        public async Task<TicketStatusResponse> GetTicketStatus(TicketStatusRequest request)
        {
            return await SendRequest<TicketStatusResponse>(request.Serialize(), Uris.TicketStatus);
        }

        private async Task<B> SendRequest<B>(string request, string uri) where B : HttpResponse<B>, new()
        {
            try
            {
                var content = new StringContent(request, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(EndpointUrl + uri, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    return HttpResponse<B>.Deserialize(responseContent);
                }
                else
                {
                    switch ((int)response.StatusCode)
                    {
                        case (int)ResponseCodes.RequestRejected: return HttpResponse<B>.GetRequestRejected<B>();
                        case (int)ResponseCodes.NotFound:
                        default:
                            return HttpResponse<B>.GetNotFound<B>();
                    }
                }
            }
            catch
            {
                return HttpResponse<B>.GetExceptionThrow<B>();
            }
        }
    }
}