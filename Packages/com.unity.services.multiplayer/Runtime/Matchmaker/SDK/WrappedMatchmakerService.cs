using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Unity.Services.Matchmaker.Http;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Matchmaker.Backfill;
using Unity.Services.Matchmaker.PayloadProxy;
using Unity.Services.Matchmaker.Overrides;
using System.Collections.Generic;
using System.Text;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Analytics.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Internal;
using UnityEngine;
using Logger = Unity.Services.Multiplayer.Logger;

namespace Unity.Services.Matchmaker
{
    internal class WrappedMatchmakerService : IMatchmakerSdkConfiguration, IMatchmakerService
    {
        const string CloudEnvironmentKey = "com.unity.services.core.cloud-environment";

        internal readonly IMatchmakerServiceSdk m_MatchmakerService;
        private readonly IPayloadProxyApiClient m_payloadProxyClient;
        private readonly IABRemoteConfig m_abRemoteConfig;
        private readonly IABAnalytics m_abAnalytics;
        private string m_payloadProxyToken;

        internal WrappedMatchmakerService(
            IProjectConfiguration projectConfiguration,
            IInstallationId installationId,
            IEnvironmentId environmentIdProvider,
            IMatchmakerServiceSdk matchmakerService,
            IPayloadProxyApiClient payloadProxyClient = null,
            IABRemoteConfig abRemoteConfig = null,
            IABAnalytics abAnalytics = null)
        {
            m_MatchmakerService = matchmakerService;
            m_payloadProxyClient = payloadProxyClient ?? new PayloadProxyClient();

            var projectId = Application.cloudProjectId;
            var environmentId = environmentIdProvider?.EnvironmentId ?? "";

            if (abAnalytics == null)
            {
                if (CoreRegistry.Instance.TryGetServiceComponent<IAnalyticsStandardEventComponent>(out var softAnalytics))
                {
                    m_abAnalytics = softAnalytics != null ? new ABAnalytics(environmentId, softAnalytics) : null;
                }
            }
            else
            {
                m_abAnalytics = abAnalytics;
            }

            // Having abRemoteConfig without analytics would break ABTesting
            if (m_abAnalytics == null)
                return;

            if (abRemoteConfig == null)
            {
                var userId = installationId?.GetOrCreateIdentifier();
                var cloudEnvironment = projectConfiguration?.GetString(CloudEnvironmentKey);

                m_abRemoteConfig = new ABRemoteConfig(new HttpClient(), userId, cloudEnvironment, projectId, environmentId);
            }
            else
            {
                m_abRemoteConfig = abRemoteConfig;
            }
        }

        /// <summary>
        /// Sets the base path in configuration.
        /// </summary>
        /// <param name="basePath">The base path to set in configuration.</param>
        public void SetBasePath(string basePath)
        {
            m_MatchmakerService.Configuration.BasePath = basePath;
        }

        #region Wrapped Tickets API

        /// <inheritdoc/>
        /// <exception cref="System.ArgumentNullException">An exception thrown when the request is missing the minimum required number of players.</exception>
        public async Task<CreateTicketResponse> CreateTicketAsync(List<Player> players, CreateTicketOptions options)
        {
            EnsureSignedIn();

            if (players == null || players.Count < 1)
            {
                throw new ArgumentNullException(nameof(players), "Cannot create a matchmaking ticket without at least 1 player to add to the queue!");
            }
            var queueName = options?.QueueName;
            var attributes = options?.Attributes;

            if (m_abRemoteConfig != null)
            {
                // Note that the refresh will only be done the first time this method is called.
                await m_abRemoteConfig.RefreshGameOverridesAsync();
            }

            var model = new CreateTicketRequest(
                players,
                queueName,
                attributes,
                m_abRemoteConfig?.Overrides);

            var request = new Tickets.CreateTicketRequest(default, model);
            var response = await TryCatchRequest(m_MatchmakerService.TicketsApi.CreateTicketAsync, request);

            var result = response.Result;

            if (result.AbTestingResult != null && result.AbTestingResult.IsAbTesting)
            {
                m_abAnalytics?.SubmitUserAssignmentConfirmedEvent(result.AbTestingResult.VariantId, m_abRemoteConfig?.AssignmentId);
            }

            return result;
        }

        /// <inheritdoc/>
        /// <exception cref="System.ArgumentNullException">An exception thrown when a required parameter is null, empty, or containing only whitespace.</exception>
        public async Task DeleteTicketAsync(string ticketId)
        {
            EnsureSignedIn();
            if (string.IsNullOrWhiteSpace(ticketId))
            {
                throw new ArgumentNullException("ticketId", "Argument should be non-null, non-empty & not only whitespaces.");
            }

            var request = new Tickets.DeleteTicketRequest(ticketId);
            await TryCatchRequest(m_MatchmakerService.TicketsApi.DeleteTicketAsync, request);
        }

        /// <inheritdoc/>
        /// <exception cref="System.ArgumentNullException">An exception thrown when a required parameter is null, empty, or containing only whitespace.</exception>
        public async Task<TicketStatusResponse> GetTicketAsync(string ticketId)
        {
            EnsureSignedIn();
            if (string.IsNullOrWhiteSpace(ticketId))
            {
                throw new ArgumentNullException("ticketId", "Argument should be non-null, non-empty & not only whitespaces.");
            }

            var request = new Tickets.GetTicketStatusRequest(ticketId);
            var response = await TryCatchRequest(m_MatchmakerService.TicketsApi.GetTicketStatusAsync, request);
            return response.Result;
        }

        #endregion

        #region Wrapped Backfill API

        /// <inheritdoc/>
        public async Task<BackfillTicket> ApproveBackfillTicketAsync(string backfillTicketId)
        {
            var proxyResponse = await m_payloadProxyClient.GetPayloadProxyJwtAsync();
            m_payloadProxyToken = proxyResponse.Token;

            var request = new ApproveBackfillTicketRequest(backfillTicketId);
            var response = await TryCatchRequest(m_MatchmakerService.BackfillApi.ApproveBackfillTicketAsync, request);

            //Convert the legacy response to the user-facing response.
            return response.Result.GetCompatibilityModel();
        }

        /// <inheritdoc/>
        public async Task<string> CreateBackfillTicketAsync(CreateBackfillTicketOptions options)
        {
            var proxyResponse = await m_payloadProxyClient.GetPayloadProxyJwtAsync();
            m_payloadProxyToken = proxyResponse.Token;

            var request = new Backfill.CreateBackfillTicketRequest(options.GetLegacyModel());
            var response = await TryCatchRequest(m_MatchmakerService.BackfillApi.CreateBackfillTicketAsync, request);
            return response.Result.Id;
        }

        /// <inheritdoc/>
        public async Task DeleteBackfillTicketAsync(string backfillTicketId)
        {
            var proxyResponse = await m_payloadProxyClient.GetPayloadProxyJwtAsync();
            m_payloadProxyToken = proxyResponse.Token;

            var request = new DeleteBackfillTicketRequest(backfillTicketId);
            await TryCatchRequest(m_MatchmakerService.BackfillApi.DeleteBackfillTicketAsync, request);
        }

        /// <inheritdoc/>
        public async Task UpdateBackfillTicketAsync(string backfillTicketId, BackfillTicket ticket)
        {
            var proxyResponse = await m_payloadProxyClient.GetPayloadProxyJwtAsync();
            m_payloadProxyToken = proxyResponse.Token;

            var request = new UpdateBackfillTicketRequest(backfillTicketId, ticket.GetLegacyModel());
            await TryCatchRequest(m_MatchmakerService.BackfillApi.UpdateBackfillTicketAsync, request);
        }

        #endregion

        #region Wrapped Matches API

        /// <inheritdoc/>
        /// <exception cref="System.ArgumentNullException">An exception thrown when the request is missing the match id.</exception>
        public async Task<StoredMatchmakingResults> GetMatchmakingResultsAsync(string matchId)
        {
            EnsureSignedIn();

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentNullException("matchId", "Argument should be non-null, non-empty & not only whitespaces.");
            }

            var request = new Matches.GetMatchmakingResultsRequest(matchId, Application.cloudProjectId);
            var response = await TryCatchRequest(m_MatchmakerService.MatchesApi.GetMatchmakingResultsAsync, request);

            var result = response.Result;
            return result;
        }

        #endregion

        #region Helper Functions

        // Helper function to reduce code duplication of try-catch
        private async Task<Response> TryCatchRequest<TRequest>(Func<TRequest, Configuration, Task<Response>> func, TRequest request)
        {
            Response response = null;
            try
            {
                response = await func(request, m_MatchmakerService.Configuration);
            }
            catch (HttpException he)
            {
                int httpErrorStatusCode = (int)he.Response.StatusCode;
                MatchmakerExceptionReason reason = MatchmakerExceptionReason.Unknown;
                if (he.Response.IsNetworkError)
                {
                    reason = MatchmakerExceptionReason.NetworkError;
                }
                else if (he.Response.IsHttpError)
                {
                    //Elevate unhandled http codes to lobby enum range
                    if (httpErrorStatusCode < 1000)
                    {
                        httpErrorStatusCode += (int)MatchmakerExceptionReason.Min;
                        if (Enum.IsDefined(typeof(MatchmakerExceptionReason), httpErrorStatusCode))
                        {
                            reason = (MatchmakerExceptionReason)httpErrorStatusCode;
                        }
                    }
                }

                ResolveErrorWrapping(reason, he);
            }
            catch (Exception e)
            {
                ResolveErrorWrapping(MatchmakerExceptionReason.Unknown, e);
            }

            return response;
        }

        // Helper function to reduce code duplication of try-catch (with payload token)
        private async Task<Response> TryCatchRequest<TRequest>(Func<TRequest, string, Configuration, Task<Response>> func, TRequest request)
        {
            Response response = null;
            try
            {
                response = await func(request, m_payloadProxyToken, m_MatchmakerService.Configuration);
            }
            catch (HttpException he)
            {
                int httpErrorStatusCode = (int)he.Response.StatusCode;
                MatchmakerExceptionReason reason = MatchmakerExceptionReason.Unknown;
                if (he.Response.IsNetworkError)
                {
                    reason = MatchmakerExceptionReason.NetworkError;
                }
                else if (he.Response.IsHttpError)
                {
                    //Elevate unhandled http codes to lobby enum range
                    if (httpErrorStatusCode < 1000)
                    {
                        httpErrorStatusCode += (int)MatchmakerExceptionReason.Min;
                        if (Enum.IsDefined(typeof(MatchmakerExceptionReason), httpErrorStatusCode))
                        {
                            reason = (MatchmakerExceptionReason)httpErrorStatusCode;
                        }
                    }
                }

                ResolveErrorWrapping(reason, he);
            }
            catch (Exception e)
            {
                ResolveErrorWrapping(MatchmakerExceptionReason.Unknown, e);
            }

            return response;
        }

        // Helper function to reduce code duplication of try-catch (generic version)
        private async Task<Response<TReturn>> TryCatchRequest<TRequest, TReturn>(Func<TRequest, Configuration, Task<Response<TReturn>>> func, TRequest request)
        {
            Response<TReturn> response = null;
            try
            {
                response = await func(request, m_MatchmakerService.Configuration);
            }
            catch (HttpException he)
            {
                int httpErrorStatusCode = (int)he.Response.StatusCode;
                MatchmakerExceptionReason reason = MatchmakerExceptionReason.Unknown;
                if (he.Response.IsNetworkError)
                {
                    reason = MatchmakerExceptionReason.NetworkError;
                }
                else if (he.Response.IsHttpError)
                {
                    //Elevate unhandled http codes to lobby enum range
                    if (httpErrorStatusCode < 1000)
                    {
                        httpErrorStatusCode += (int)MatchmakerExceptionReason.Min;
                        if (Enum.IsDefined(typeof(MatchmakerExceptionReason), httpErrorStatusCode))
                        {
                            reason = (MatchmakerExceptionReason)httpErrorStatusCode;
                        }
                    }
                }

                ResolveErrorWrapping(reason, he);
            }
            catch (Exception e)
            {
                ResolveErrorWrapping(MatchmakerExceptionReason.Unknown, e);
            }

            return response;
        }

        // Helper function to reduce code duplication of try-catch (generic version with proxy token)
        //TODO - is there a way to not have to duplicate for variable parameters?
        private async Task<Response<TReturn>> TryCatchRequest<TRequest, TReturn>(Func<TRequest, string, Configuration, Task<Response<TReturn>>> func, TRequest request)
        {
            Response<TReturn> response = null;
            try
            {
                response = await func(request, m_payloadProxyToken, m_MatchmakerService.Configuration);
            }
            catch (HttpException he)
            {
                int httpErrorStatusCode = (int)he.Response.StatusCode;
                MatchmakerExceptionReason reason = MatchmakerExceptionReason.Unknown;
                if (he.Response.IsNetworkError)
                {
                    reason = MatchmakerExceptionReason.NetworkError;
                }
                else if (he.Response.IsHttpError)
                {
                    //Elevate unhandled http codes to lobby enum range
                    if (httpErrorStatusCode < 1000)
                    {
                        httpErrorStatusCode += (int)MatchmakerExceptionReason.Min;
                        if (Enum.IsDefined(typeof(MatchmakerExceptionReason), httpErrorStatusCode))
                        {
                            reason = (MatchmakerExceptionReason)httpErrorStatusCode;
                        }
                    }
                }

                ResolveErrorWrapping(reason, he);
            }
            catch (Exception e)
            {
                ResolveErrorWrapping(MatchmakerExceptionReason.Unknown, e);
            }

            return response;
        }

        // Helper function to resolve the new wrapped error/exception based on input parameter
        private void ResolveErrorWrapping(MatchmakerExceptionReason reason, Exception exception = null)
        {
            if (reason == MatchmakerExceptionReason.Unknown)
            {
                Logger.LogError($"{Enum.GetName(typeof(MatchmakerExceptionReason), reason)} ({(int)reason}). Message: Something went wrong.");
                throw new MatchmakerServiceException(reason, "Something went wrong.", exception);
            }
            else
            {
                //Check if the exception is of type HttpException<ProblemDetails> - extract api user-facing message
                HttpException<ProblemDetails> apiException = exception as HttpException<ProblemDetails>;
                if (apiException != null)
                {
                    ProblemDetails ae = apiException.ActualError;
                    if (ae != null)
                    {
                        var jsonObject = ae.Errors.GetAs<JsonObject>();
                        string errorBody = jsonObject?.obj == null ? ae.Detail : (Environment.NewLine + JsonConvert.SerializeObject(jsonObject));

                        //Log both details and errors as the API isn't consistent at the moment with how the errors are returned.
                        Logger.LogError($"{Enum.GetName(typeof(MatchmakerExceptionReason), reason)} ({(int)reason}) " +
                            $"{Environment.NewLine} Title: {apiException.ActualError.Title} " +
                            $"{Environment.NewLine} Errors: {errorBody}" +
                            $"{Environment.NewLine}");
                    }

                    throw new MatchmakerServiceException(reason, apiException.Response.ErrorMessage, apiException);
                }
                else
                {
                    //Other general exception message handling
                    Logger.LogError($"{Enum.GetName(typeof(MatchmakerExceptionReason), reason)} ({(int)reason}). Message: {exception.Message}");
                    throw new MatchmakerServiceException(reason, exception.Message, exception);
                }
            }
        }

        #endregion

        private void EnsureSignedIn()
        {
            if (m_MatchmakerService.AccessToken.AccessToken == null)
            {
                throw new MatchmakerServiceException(MatchmakerExceptionReason.Unauthorized, "You are not signed in to the Authentication Service. Please sign in.");
            }
        }
    }

    /// <summary>
    /// Helper extension class for converting to/from Backfill v2 api data models.
    /// </summary>
    internal static class BackfillApiCompatibilityExtensions
    {
        internal static Models.CreateBackfillTicketRequest GetLegacyModel(this CreateBackfillTicketOptions options)
        {
            var legacyProperties = options.Properties.GetLegacyModel();
            return new Models.CreateBackfillTicketRequest(options.Connection, legacyProperties, options.QueueName, options.Attributes, options.PoolId, options.MatchId);
        }

        internal static LegacyBackfillTicket GetLegacyModel(this BackfillTicket options)
        {
            var legacyProperties = options.Properties.GetLegacyModel();
            return new LegacyBackfillTicket(options.Id, options.Connection, options.Attributes, legacyProperties);
        }

        internal static Dictionary<string, byte[]> GetLegacyModel(this BackfillTicketProperties properties)
        {
            var legacyModel = new Dictionary<string, byte[]>();

            //Convert models to byte[]
            var json = JsonConvert.SerializeObject(properties);
            var dataBytes = Encoding.UTF8.GetBytes(json);
            legacyModel.Add("Data", dataBytes);

            return legacyModel;
        }

        internal static BackfillTicket GetCompatibilityModel(this LegacyBackfillTicket legacyTicket)
        {
            //Decode and deserialize properties.
            var propertiesJson = Encoding.UTF8.GetString(legacyTicket.Properties["Data"]);
            var deserialized = JsonConvert.DeserializeObject<BackfillTicketProperties>(propertiesJson);

            return new BackfillTicket(legacyTicket.Id, legacyTicket.Connection, legacyTicket.Attributes, deserialized);
        }
    }
}
