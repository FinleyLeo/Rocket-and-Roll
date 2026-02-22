using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Scheduler.Internal;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;

namespace Unity.Services.Multiplayer
{
    interface ISessionMatchmaking
    {
        public event Action<MatchmakerState> StateChanged;
        public event Action MatchFound;
        public event Action MatchFailed;
        public event Action<ISession> MatchJoined;
        public event Action MatchJoinFailed;

        public MatchmakerState State { get; }

        public string TicketId { get; }

        public Task CancelAsync();
        public Task<ISession> JoinAsync();
    }

    class Matchmaker : ISessionMatchmaking
    {
        const int k_PollingDelaySeconds = 1;
        const int k_MatchmakingResultsRetryCount = 3;

        const string k_AssignmentTimeoutMessage = "Matchmaking took longer than the timeout value configured for the pool.";
        const string k_AssignmentFailedMessage = "Unknown failure while matchmaking.";

        public event Action<MatchmakerState> StateChanged;
        public event Action MatchFound;
        public event Action MatchFailed;
        public event Action<ISession> MatchJoined;
        public event Action MatchJoinFailed;

        public MatchmakerState State { get; internal set; }
        public MatchmakerAssignmentType AssignmentType { get; internal set; }
        public string TicketId { get; internal set; }

        bool IsAuthorized => m_AccessToken.AccessToken != null;

        MatchIdAssignment m_MatchIdAssignment;
        MultiplayAssignment m_MultiplayAssignment;
        readonly SessionOptions m_SessionOptions;

        long? m_PollingActionId;

        readonly ISessionManager m_SessionManager;
        readonly IActionScheduler m_ActionScheduler;
        readonly IMatchmakerService m_MatchmakerService;
        readonly IPlayerId m_PlayerId;
        readonly IAccessToken m_AccessToken;
        readonly IAccessTokenObserver m_AccessTokenObserver;
        readonly TaskCompletionSource<ISession> m_SessionCompletionSource;

        const int k_MultiplaySessionJoinTimeoutSeconds = 10;

        internal Matchmaker(
            string ticketId,
            SessionOptions sessionOptions,
            ISessionManager sessionManager,
            IActionScheduler actionScheduler,
            IMatchmakerService matchmaker,
            IPlayerId playerId,
            IAccessToken accessToken,
            IAccessTokenObserver accessTokenObserver,
            TaskCompletionSource<ISession> completionSource)
        {
            TicketId = ticketId;
            m_SessionOptions = sessionOptions;

            m_SessionManager = sessionManager;
            m_ActionScheduler = actionScheduler;
            m_MatchmakerService = matchmaker;
            m_PlayerId = playerId;
            m_AccessToken = accessToken;
            m_AccessTokenObserver = accessTokenObserver;
            m_SessionCompletionSource = completionSource;

            m_PlayerId.PlayerIdChanged += OnPlayerIdChanged;
            m_AccessTokenObserver.AccessTokenChanged += OnAccessTokenChanged;

            SetState(MatchmakerState.InProgress);
            SchedulePolling(0);
        }

        public async Task<ISession> JoinAsync()
        {
            ValidateAuthorization();
            ValidateTicketId();
            ValidateValidAssignment();

            if (State != MatchmakerState.MatchFound && State != MatchmakerState.JoinFailed)
            {
                throw new SessionException("Invalid Matchmaker State to join.", SessionError.InvalidMatchmakerState);
            }

            switch (AssignmentType)
            {
                case MatchmakerAssignmentType.MatchId:
                    return await JoinMatchIdAssignmentAsync();
            }

            SetState(MatchmakerState.JoinFailed);
            OnMatchJoinFailed();
            throw new SessionException("Invalid assignment", SessionError.InvalidMatchmakerAssignment);
        }

        async Task<ISession> JoinMatchIdAssignmentAsync()
        {
            try
            {
                var matchmakerModuleOption =
                    m_SessionOptions.GetOption<MatchmakerModuleOption>() ??
                    AddDefaultMatchmakerModuleOption(m_SessionOptions);
                matchmakerModuleOption.IsPeerToPeer = true;

                await ValidateMaxPlayersAsync(m_MatchIdAssignment.MatchId);

                var matchIdSession = await m_SessionManager.CreateOrJoinAsync(m_MatchIdAssignment.MatchId, m_SessionOptions);
                SetState(MatchmakerState.Joined);
                OnMatchJoined(matchIdSession);
                return matchIdSession;
            }
            catch (SessionException e)
            {
                SetState(MatchmakerState.JoinFailed);
                OnMatchJoinFailed();
                m_SessionCompletionSource.TrySetException(e);
                throw;
            }
            catch (Exception e)
            {
                SetState(MatchmakerState.JoinFailed);
                OnMatchJoinFailed();
                var sessionException = new SessionException(e.Message, SessionError.Unknown);
                m_SessionCompletionSource.TrySetException(sessionException);
                throw;
            }
        }

        static MatchmakerModuleOption AddDefaultMatchmakerModuleOption(SessionOptions sessionOptions)
        {
            // BackfillingConfiguration is not allowed in peer to peer, so
            // it's fine to pass a 'default' backfilling configuration.
            MatchmakerModuleOption defaultOption = new(BackfillingConfiguration.WithBackfillingConfiguration());
            sessionOptions.WithOption(defaultOption);

            return defaultOption;
        }

        async Task WaitForSeconds(double seconds)
        {
            var tcs = new TaskCompletionSource<object>();
            m_ActionScheduler.ScheduleAction(() => tcs.SetResult(null), seconds);
            await tcs.Task;
        }

        public async Task CancelAsync()
        {
            ValidateAuthorization();
            ValidateTicketId();

            if (State != MatchmakerState.InProgress)
            {
                throw new SessionException("Invalid Matchmaker State to cancel.", SessionError.InvalidMatchmakerState);
            }

            try
            {
                await m_MatchmakerService.DeleteTicketAsync(TicketId);
            }
            catch (Exception)
            {
            }

            TicketId = null;
            SetState(MatchmakerState.Canceled);
        }

        public void Reset()
        {
            SetState(MatchmakerState.None);
        }

        internal void SetState(MatchmakerState state)
        {
            State = state;
            if (State is MatchmakerState.Canceled or MatchmakerState.None)
            {
                m_SessionCompletionSource?.TrySetCanceled();
            }
            StateChanged?.Invoke(State);
        }

        internal void SetMatchIdAssignment(MatchIdAssignment assignment)
        {
            AssignmentType = MatchmakerAssignmentType.MatchId;
            m_MatchIdAssignment = assignment;
            SetState(MatchmakerState.MatchFound);
            OnMatchFound();
        }

        async void OnMatchFound()
        {
            MatchFound?.Invoke();
            var session = await JoinAsync();
            m_SessionCompletionSource?.TrySetResult(session);
        }

        void OnMatchJoined(ISession session)
        {
            MatchJoined?.Invoke(session);
        }

        void OnMatchJoinFailed()
        {
            MatchJoinFailed?.Invoke();
        }

        internal void SetMatchFailure(string message, SessionError reason)
        {
            Logger.LogVerbose(message);
            SetState(MatchmakerState.MatchFailed);
            MatchFailed?.Invoke();
            throw new SessionException(message, reason);
        }

        internal void SchedulePolling(int seconds)
        {
            if (!m_PollingActionId.HasValue)
            {
                m_PollingActionId = m_ActionScheduler.ScheduleAction(RunScheduledPolling, seconds);
            }
        }

        internal void CancelPolling()
        {
            if (m_PollingActionId.HasValue)
            {
                m_ActionScheduler.CancelAction(m_PollingActionId.Value);
                m_PollingActionId = null;
            }
        }

        internal async void RunScheduledPolling()
        {
            m_PollingActionId = null;

            try
            {
                await PollTicketStatusAsync();
            }
            catch (Exception e)
            {
                _ = m_SessionCompletionSource.TrySetException(e);
            }

            if (State == MatchmakerState.InProgress)
            {
                SchedulePolling(k_PollingDelaySeconds);
            }
        }

        async Task PollTicketStatusAsync()
        {
            try
            {
                var ticketResponse = await m_MatchmakerService.GetTicketAsync(TicketId);

                if (ticketResponse.Type == typeof(MultiplayAssignment))
                {
                    throw new SessionException("This operation is not supported.", SessionError.InvalidMatchmakerAssignment);
                }

                if (ticketResponse.Type == typeof(MatchIdAssignment) &&
                    ticketResponse.Value is MatchIdAssignment matchIdAssignment)
                {
                    Logger.LogVerbose("Ticket response is being treated as a MatchIdAssignment");

                    switch (matchIdAssignment.Status)
                    {
                        case MatchIdAssignment.StatusOptions.Found:
                            SetMatchIdAssignment(matchIdAssignment);
                            return;
                        case MatchIdAssignment.StatusOptions.InProgress:
                            return;
                        case MatchIdAssignment.StatusOptions.Failed:
                            var failedMessage = $"Ticket {matchIdAssignment.Status}: {(string.IsNullOrEmpty(matchIdAssignment.Message) ? k_AssignmentFailedMessage: matchIdAssignment.Message)}";
                            SetMatchFailure(failedMessage, SessionError.MatchmakerAssignmentFailed);
                            return;
                        case MatchIdAssignment.StatusOptions.Timeout:
                            var timeoutMessage = $"Ticket {matchIdAssignment.Status}: {(string.IsNullOrEmpty(matchIdAssignment.Message) ? k_AssignmentTimeoutMessage: matchIdAssignment.Message)}";
                            SetMatchFailure(timeoutMessage, SessionError.MatchmakerAssignmentTimeout);
                            return;
                        default:
                            return;
                    }
                }

                const string message = nameof(m_MatchmakerService.GetTicketAsync) + " returned an invalid assignment type. This operation is not supported.";
                throw new SessionException(message, SessionError.InvalidMatchmakerAssignment);
            }
            catch (MatchmakerServiceException e)
            {
                // Raise the right events & state change
                throw ConvertException(e);
            }
        }

        void ValidateAuthorization()
        {
            if (!IsAuthorized)
            {
                throw new SessionException("Player is not authorized", SessionError.NotAuthorized);
            }
        }

        void ValidateTicketId()
        {
            if (string.IsNullOrEmpty(TicketId))
            {
                throw new SessionException("Invalid matchmaker ticket", SessionError.InvalidMatchmakerTicket);
            }
        }

        void ValidateValidAssignment()
        {
            if (AssignmentType == MatchmakerAssignmentType.None)
            {
                throw new SessionException("No assignment found", SessionError.InvalidMatchmakerAssignment);
            }
        }

        private void OnPlayerIdChanged(string obj)
        {
            Reset();
        }

        private void OnAccessTokenChanged(string accessToken)
        {
            if (accessToken == null)
            {
                Reset();
            }
        }

        async Task ValidateMaxPlayersAsync(string matchId)
        {
            var retryCount = 1;

            StoredMatchmakingResults matchmakingResults = null;
            while (matchmakingResults == null && retryCount <= k_MatchmakingResultsRetryCount)
            {
                try
                {
                    matchmakingResults = await m_MatchmakerService.GetMatchmakingResultsAsync(matchId);
                }
                catch (MatchmakerServiceException mme) when (retryCount < k_MatchmakingResultsRetryCount &&
                                                             mme.Reason == MatchmakerExceptionReason.EntityNotFound)
                {
                    Logger.LogVerbose(
                        "Matchmaking results not found. It may not yet be available. Retrying with count: " +
                        retryCount);
                    await WaitForSeconds(1);
                }
                catch (Exception e)
                {
                    Logger.LogError("Error when trying to fetch matchmaking results: " + e.Message);
                    break;
                }

                retryCount++;
            }

            if (matchmakingResults != null)
            {
                if (m_SessionOptions.MaxPlayers < matchmakingResults.MatchProperties.MaxPlayers)
                {
                    var sessionException = new SessionException(
                        $"{nameof(SessionOptions.MaxPlayers)} in {nameof(SessionOptions)} ({m_SessionOptions.MaxPlayers}) is less than the MaxPlayers configured in Matchmaker rules ({matchmakingResults.MatchProperties.MaxPlayers}).",
                        SessionError.InvalidParameter);
                    m_SessionCompletionSource?.TrySetException(sessionException);
                    throw sessionException;
                }

                if (m_SessionOptions.MaxPlayers > matchmakingResults.MatchProperties.MaxPlayers)
                {
                    Logger.LogVerbose($"{nameof(SessionOptions.MaxPlayers)} in {nameof(SessionOptions)} ({m_SessionOptions.MaxPlayers}) is greater than the MaxPlayers configured in Matchmaker rules ({matchmakingResults.MatchProperties.MaxPlayers}).");
                }
            }
        }

        SessionException ConvertException(MatchmakerServiceException exception)
        {
            return new SessionException(exception.Message, SessionError.Unknown);
        }
    }
}
