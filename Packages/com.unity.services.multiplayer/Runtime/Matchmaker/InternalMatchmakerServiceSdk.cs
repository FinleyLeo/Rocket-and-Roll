using Unity.Services.Authentication.Internal;
using Unity.Services.Matchmaker.Apis.Backfill;
using Unity.Services.Matchmaker.Apis.Matches;
using Unity.Services.Matchmaker.Apis.Tickets;
using Unity.Services.Matchmaker.Http;

namespace Unity.Services.Matchmaker
{
    /// <summary>
    /// InternalMatchmakerService
    /// </summary>
    internal class InternalMatchmakerServiceSdk : IMatchmakerServiceSdk
    {
        /// <summary>
        /// Constructor for InternalMatchmakerService
        /// </summary>
        /// <param name="httpClient">The HttpClient for InternalMatchmakerService.</param>
        /// <param name="cloudEnvironment">The current environment</param>
        /// <param name="accessToken">The Authentication token for the service.</param>
        public InternalMatchmakerServiceSdk(HttpClient httpClient, string cloudEnvironment = "production", IAccessToken accessToken = null)
        {
            BackfillApi = new BackfillApiClient(httpClient, accessToken);

            TicketsApi = new TicketsApiClient(httpClient, accessToken);

            MatchesApi = new MatchesApiClient(httpClient, accessToken);

            AccessToken = accessToken;

            Configuration = new Configuration(GetBasePath(cloudEnvironment), 10, 4, null);
        }

        /// <summary> Instance of IBackfillApiClient interface</summary>
        public IBackfillApiClient BackfillApi { get; set; }

        /// <summary> Instance of ITicketsApiClient interface</summary>
        public ITicketsApiClient TicketsApi { get; set; }

        /// <summary> Instance of IMatchesApiClient interface</summary>
        public IMatchesApiClient MatchesApi { get; set; }

        /// <summary> Instance of AccessToken interface</summary>
        public IAccessToken AccessToken { get; set; }

        /// <summary> Configuration properties for the service.</summary>
        public Configuration Configuration { get; set; }

        private string GetBasePath(string cloudEnvironment)
        {
            if (cloudEnvironment == "staging")
            {
                return "https://matchmaker-stg.services.api.unity.com";
            }
            return "https://matchmaker.services.api.unity.com";
        }
    }
}
