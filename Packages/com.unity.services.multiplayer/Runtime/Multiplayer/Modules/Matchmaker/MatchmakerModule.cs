using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Services.Core.Scheduler.Internal;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;

namespace Unity.Services.Multiplayer
{
    class MatchmakerModule : IModule
    {
        public StoredMatchmakingResults MatchmakingResults { get; private set; }

        /// <summary>
        /// Set to true if the matchmaking is peer-to-peer, false otherwise
        /// </summary>
        public bool IsPeerToPeer { get; set; }

        public MatchmakerModule(
            ISession session,
            IActionScheduler actionScheduler,
            IMatchmakerService matchmakerService)
        {
            m_ActionScheduler = actionScheduler;
            m_MatchmakerService = matchmakerService;
            m_Session = session;
        }

        ISession m_Session;
        NetworkEndpointAddress? m_connectionInfo;
        bool m_shouldInitialize;

        // Backfill control flow properties
        // determine if backfill is allowed (False for P2P, True for Game Server Hosting)
        bool m_backfillIsAllowed;

        // determine if backfill is enabled
        bool m_BackfillIsEnabled;

        // determine if players are automatically removed from the backfill
        bool m_AutomaticallyRemovePlayersFromBackfill = true;

        // Determine if backfilling should start automatically when a player is missing
        bool m_AutomaticallyStartBackfillingWhenPlayerIsMissing = true;

        // Determine the timeout before a player is automatically removed from the backfill if they never connect
        // If value = 0, the player will never be removed
        int m_playerConnectionTimeout = 30;

        // Determine how often (in seconds) the backfill approval loop should run
        int m_BackfillingLoopInterval = 1;

        // determine if backfilling should happen based on the status of the session and the backfill ticket
        bool m_ShouldBackfill => !m_Session.IsLocked && !m_Session.IsPrivate && !m_BackfillIsFull;

        BackfillTicket m_LocalBackfillTicket;

        StoredMatchProperties m_matchProperties;

        int m_BackfillPlayerCount => m_LocalBackfillTicket?.Properties.MatchProperties.Players.Count ?? 0;
        bool m_BackfillIsFull => m_BackfillPlayerCount >= m_Session.MaxPlayers;

        Dictionary<string, DateTime> m_playerWaitingConnection = new();

        readonly IActionScheduler m_ActionScheduler;
        long? m_ActionSchedulerActionID;
        readonly IMatchmakerService m_MatchmakerService;

        bool m_LocalDataDirty;

        const string k_TeamIdProperty = "TeamId";
        const string k_TeamNameProperty = "TeamName";


        public async Task InitializeAsync()
        {
            Logger.LogVerbose($"Matchmaker Module InitializeAsync");

            if (!m_shouldInitialize)
            {
                return;
            }

            bool success = false;

            success = await InitializeMatchmakingResults();
            if (!success)
            {
                throw new InvalidOperationException("Error while initializing Matchmaker Module");
            }

            if (m_backfillIsAllowed)
            {
                InitializeBackfilling();
            }

            if (m_Session.MaxPlayers < MatchmakingResults.MatchProperties.MaxPlayers)
            {
                throw new SessionException(
                    $"MaxPlayers in Session ({m_Session.MaxPlayers}) is less than the MaxPlayers configured in Matchmaker rules ({MatchmakingResults.MatchProperties.MaxPlayers}).",
                    SessionError.InvalidCreateSessionOptions);
            }
        }

        public async Task LeaveAsync()
        {
            if (!m_shouldInitialize)
            {
                return;
            }

            if (m_LocalBackfillTicket != null)
            {
                await StopBackfillingAsync();
            }
        }

        /// <summary>
        /// Allow to start backfilling the session.
        /// Backfilling is currently only supported on Game Server Hosting.
        /// </summary>
        public async Task StartBackfillingAsync()
        {
            // Prevent to start backfilling if not allowed
            if (!m_backfillIsAllowed || !m_shouldInitialize)
                return;

            if (m_ActionSchedulerActionID != null)
            {
                Logger.LogVerbose("Backfilling is already in progress.");
                return;
            }

            if (!m_ShouldBackfill)
            {
                Logger.LogVerbose("Cannot start backfilling - Session is full, locked or private.");
                return;
            }

            CreateBackfillTicketOptions createBackfillOptions = new CreateBackfillTicketOptions(
                queueName: MatchmakingResults.QueueName,
                connection: m_connectionInfo?.Address,
                properties: new BackfillTicketProperties(m_matchProperties.ToMatchProperties()),
                matchId: m_Session.Id,
                poolId: MatchmakingResults.PoolId
            );

            createBackfillOptions.MatchId = m_Session.Id;

            string backfillTicketId;
            try
            {
                backfillTicketId = await m_MatchmakerService.CreateBackfillTicketAsync(createBackfillOptions);
            }
            catch (Exception e)
            {
                Logger.LogError("Error while creating backfill ticket: " + e.Message);
                return;
            }

            var bacfillTicket = await ApproveBackfillTicket(backfillTicketId);
            if (bacfillTicket != null)
                m_LocalBackfillTicket = bacfillTicket;

            Logger.LogVerbose($"Starting backfilling. ticket ID: {m_LocalBackfillTicket.Id}");
            m_BackfillIsEnabled = true;

            ScheduleApproveBackfillLoop();
        }

        /// <summary>
        /// Stop backfilling the session.
        /// </summary>
        public async Task StopBackfillingAsync()
        {
            if (!m_BackfillIsEnabled || !m_backfillIsAllowed || !m_shouldInitialize)
                return;

            m_BackfillIsEnabled = false;
            Logger.LogVerbose($"Stopping backfilling.");

            if (m_ActionSchedulerActionID != null)
            {
                m_ActionScheduler.CancelAction(m_ActionSchedulerActionID.Value);
                m_ActionSchedulerActionID = null;
            }

            if (m_LocalBackfillTicket != null)
            {
                try
                {
                    await m_MatchmakerService.DeleteBackfillTicketAsync(m_LocalBackfillTicket.Id);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error while deleling backfill ticket: {e.Message}");
                    throw;
                }
            }
        }

        void OnPlayerJoined(string playerId)
        {
            if (!m_backfillIsAllowed)
                return;

            // Keep match properties up to date to be able to create a backfill ticket later on.
            if (!LocalMatchPropertiesAreValid())
            {
                throw new SessionException("State of the local match is invalid. Cannot add player.", SessionError.InvalidLocalMatchProperties);
            }

            // If player is already in the local match properties, do not add it again
            if (m_matchProperties.Players.Exists(p => p.Id == playerId))
            {
                return;
            }

            // Try to find the player in the backfill ticket
            bool playerFoundInBackfill = false;


            if (m_LocalBackfillTicket != null)
            {
                if (!LocalBackfillTicketIsValid())
                {
                    throw new SessionException("State of the backfill ticket is invalid. Cannot add player.", SessionError.InvalidBackfillTicket);
                }

                if (FindPlayerByIdInBackfillTicket(playerId, out var player))
                {
                    Logger.LogVerbose($"Player: {playerId} joined from backfilling.");
                    Logger.LogVerbose($"Backfilling status: {m_BackfillPlayerCount}/{m_Session.MaxPlayers}");

                    playerFoundInBackfill = true;

                    m_playerWaitingConnection.Remove(playerId);

                    // make sure we don't add the player twice
                    if (!m_matchProperties.Players.Exists(p => p.Id == playerId))
                    {
                        m_matchProperties.Players.Add(player);
                    }

                    if (FindPlayerTeamInBackfill(playerId, out var teamFromBackfill))
                    {
                        if (FindTeamByIdInLocalMatchProperties(teamFromBackfill.TeamId, out var teamInMatchProperties))
                        {
                            // Make sure the team does not alread contains the player
                            // The team of the player exists in the local match properties
                            // We add the player in the team
                            if (!teamInMatchProperties.PlayerIds.Contains(playerId))
                                teamInMatchProperties.PlayerIds.Add(playerId);
                        }
                        else
                        {
                            // The team does not exist in the local match properties
                            // We create a copy of the team from the backfill with the player and add it on the local match properties
                            var newTeam = new Team(teamFromBackfill.TeamName, teamFromBackfill.TeamId,
                                new List<string> { playerId });
                            m_matchProperties.Teams.Add(newTeam);
                        }
                    }
                    else
                    {
                        throw new SessionException(
                            $"Could not find team with id {teamFromBackfill.TeamId} of the player in the backfill ticket.",
                            SessionError.InvalidBackfillTicket);
                    }
                }
            }

            // If we could not find the player in backfill try to update the match properties and the backfill using the properties on the player
            if (!playerFoundInBackfill)
            {
                Logger.LogVerbose($"Could not find player in backfill ticket. Trying to add the player to the backfill ticket using the session player property {k_TeamIdProperty}.");
                var sessionPlayer = m_Session.Players.FirstOrDefault(p => p.Id == playerId);
                if (sessionPlayer == null)
                {
                    // Cannot find the player on the session - assuming the player left right after joining
                    Logger.LogVerbose($"Cannot find player {playerId} in the session. Player may have left already");
                    return;
                }

                if (!sessionPlayer.Properties.TryGetValue(k_TeamIdProperty, out var teamIdProperty))
                {
                    // Throwing as this is an invalid state that could lead to issues with the matchmaker service and the backfilling process.
                    throw new SessionException(
                        $"Player does not have {k_TeamIdProperty} property. Cannot add player to the backfill ticket.", SessionError.PlayerMissingTeamProperties);
                }

                // add player to local match properties
                var teamId = teamIdProperty.Value;
                if (!FindTeamByIdInLocalMatchProperties(teamId, out var team))
                {
                    // Adding the team to the local match properties
                    team = new Team(teamId, teamId, new List<string>());
                    m_matchProperties.Teams.Add(team);
                }

                var matchmakerPlayer = new Unity.Services.Matchmaker.Models.Player(sessionPlayer.Id, sessionPlayer.Properties);
                team.PlayerIds.Add(playerId);
                m_matchProperties.Players.Add(matchmakerPlayer);

                // Add to backfill ticket if it exists
                if (m_LocalBackfillTicket != null)
                {
                    if (!BackfillTicketIsValid())
                    {
                        throw new SessionException("State of the backfill ticket is invalid. Cannot add player.", SessionError.InvalidBackfillTicket);
                    }

                    // Make sure player is not already in the backfill ticket
                    if (!FindPlayerByIdInBackfillTicket(matchmakerPlayer.Id, out _))
                    {
                        m_LocalBackfillTicket.Properties.MatchProperties.Players.Add(matchmakerPlayer);
                        m_LocalDataDirty = true;
                    }

                    if (FindTeamByIdInBackfillTicket(teamId, out var backfillTeam))
                    {
                        if (!backfillTeam.PlayerIds.Contains(playerId))
                        {
                            backfillTeam.PlayerIds.Add(playerId);
                            m_LocalDataDirty = true;
                        }
                    }
                    else
                    {
                        // If team does not exist in the backfill ticket, create a new team and add the player
                        // Try to get the team name from the player properties
                        sessionPlayer.Properties.TryGetValue(k_TeamIdProperty, out var teamNameProperty);
                        var teamName = teamNameProperty?.Value;
                        backfillTeam = new Team(teamName, teamId, new List<string> { playerId });
                        m_LocalBackfillTicket.Properties.MatchProperties.Teams.Add(backfillTeam);
                        m_LocalDataDirty = true;
                    }
                }
            }
        }

        void OnPlayerLeft(string playerId)
        {
            if (!m_backfillIsAllowed)
                return;

            Logger.LogVerbose($"Player: {playerId} left the session.");
            // Remove player from backfill
            if (m_AutomaticallyRemovePlayersFromBackfill)
            {
                // Remove from local match properties
                if (!LocalMatchPropertiesAreValid())
                {
                    throw new SessionException("State of the local match properties are invalid. Cannot remove player.", SessionError.InvalidLocalMatchProperties);
                }

                var player = m_matchProperties.Players.FirstOrDefault(p => p.Id == playerId);
                if (player == null)
                {
                    Logger.LogVerbose("Player not found in match properties. Cannot remove player.");
                }
                else
                {
                    if (!m_matchProperties.Players.Remove(player))
                    {
                        Logger.LogVerbose("Failed removing the player from the local match properties.");
                    }
                    else
                    {
                        if (!FindPlayerTeamInLocalMatchProperties(playerId, out var team))
                        {
                            Logger.LogVerbose("Cannot find the team for the player. Cannot remove player from the team on the match properties.");
                        }
                        else
                        {
                            team.PlayerIds.Remove(playerId);
                        }
                    }
                }

                // remove from backfill if local backfill ticket exists
                if (m_LocalBackfillTicket != null)
                {
                    RemovePlayerFromBackfill(playerId);
                }

                // When a player leave we check if we should start backfilling
                if (m_ShouldBackfill && !m_BackfillIsEnabled && m_AutomaticallyStartBackfillingWhenPlayerIsMissing)
                    m_ActionScheduler.ScheduleAction(async() => await StartBackfillingAsync());
            }
        }

        void RemovePlayerFromBackfill(string playerId)
        {
            if (!BackfillTicketIsValid())
            {
                throw new SessionException("State of the backfill ticket is invalid. Cannot add player.", SessionError.InvalidBackfillTicket);
            }

            if (!FindPlayerByIdInBackfillTicket(playerId, out var playerFromBackfill))
            {
                Logger.LogVerbose("Player not found in backfill. Cannot remove player.");
            }
            else
            {
                if (m_LocalBackfillTicket.Properties.MatchProperties.Players.Remove(playerFromBackfill))
                    m_LocalDataDirty = true;

                if (!FindPlayerTeamInBackfill(playerId, out var teamFromBackfill))
                {
                    Logger.LogVerbose(
                        "Cannot find a team for the player. Cannot remove player from the right team on the backfill.");
                }
                else
                {
                    teamFromBackfill.PlayerIds.Remove(playerId);
                    m_LocalDataDirty = true;
                    Logger.LogVerbose($"Player removed from local backfill ticket");
                    Logger.LogVerbose($"Backfilling status: {m_BackfillPlayerCount}/{m_Session.MaxPlayers}");
                }
            }
        }

        bool FindPlayerByIdInBackfillTicket(string userID, out Unity.Services.Matchmaker.Models.Player player)
        {
            player = m_LocalBackfillTicket.Properties.MatchProperties.Players.FirstOrDefault(p => p.Id.Equals(userID));
            return player != null;
        }

        bool FindTeamByIdInLocalMatchProperties(string teamId, out Team team)
        {
            team = m_matchProperties.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team != null;
        }

        bool FindTeamByIdInBackfillTicket(string teamId, out Team team)
        {
            team = m_LocalBackfillTicket.Properties.MatchProperties.Teams.FirstOrDefault(t => t.TeamId == teamId);
            return team != null;
        }

        bool FindPlayerTeamInBackfill(string playerId, out Team foundTeam) =>
            FindPlayerTeamFromMatchProperties(playerId, m_LocalBackfillTicket.Properties.MatchProperties,
                out foundTeam);

        bool FindPlayerTeamInLocalMatchProperties(string playerId, out Team foundTeam) =>
            FindPlayerTeamFromMatchProperties(playerId, m_matchProperties.ToMatchProperties(), out foundTeam);

        bool FindPlayerTeamFromMatchProperties(string userID, MatchProperties matchProperties, out Team foundTeam)
        {
            foundTeam = null;
            foreach (var team in matchProperties.Teams)
            {
                if (team.PlayerIds.Contains(userID))
                {
                    foundTeam = team;
                    return true;
                }
            }

            return false;
        }

        void InitializeBackfilling()
        {
            // hook on session events to manage backfilling
            if (m_BackfillingLoopInterval < 1)
            {
                throw new SessionException("Backfilling loop interval must be greater than 0.",
                    SessionError.InvalidOperation);
            }
            m_Session.PlayerHasLeft += OnPlayerLeft;
            m_Session.PlayerJoined += OnPlayerJoined;

            // Start backfilling loop if necessary
            Logger.LogVerbose($"Backfill enabled {m_BackfillIsEnabled}.");
            if (m_BackfillIsEnabled)
            {
                // if there is a backfill ticket Id in the matchmaking results we need to start the backfilling process
                if (MatchmakingResults.MatchProperties.BackfillTicketId != null)
                {
                    // Schedule first approval loop to have a backfill ticket to work with afterward
                    m_ActionSchedulerActionID = m_ActionScheduler.ScheduleAction(async() =>
                    {
                        var backfillTicket = await ApproveBackfillTicket(MatchmakingResults.MatchProperties.BackfillTicketId);
                        if (backfillTicket == null)
                            return;
                        m_LocalBackfillTicket = backfillTicket;
                        Logger.LogVerbose($"Starting backfilling. ticket ID: {m_LocalBackfillTicket.Id}");

                        // Schedule backfilling loop
                        m_ActionSchedulerActionID = null;
                        ScheduleApproveBackfillLoop();
                    }, 0);
                }
                else
                {
                    m_BackfillIsEnabled = false;
                    Logger.LogVerbose("No backfill ticket found in the matchmaking results. Backfilling will not start automatically.");
                }
            }
        }

        void ScheduleApproveBackfillLoop()
        {
            if (m_ActionSchedulerActionID == null)
            {
                m_ActionSchedulerActionID = m_ActionScheduler.ScheduleAction(ApproveBackfillLoop, m_BackfillingLoopInterval);
            }
        }

        async void ApproveBackfillLoop()
        {
            m_ActionSchedulerActionID = null;

            if (!m_BackfillIsEnabled || !m_backfillIsAllowed)
            {
                return;
            }

            if (!m_ShouldBackfill)
            {
                Logger.LogVerbose($"Stopping backfilling - Session is now full, locked or private. {m_BackfillPlayerCount}/{m_Session.MaxPlayers}");
                Logger.LogVerbose($"Session is Locked: {m_Session.IsLocked}");
                Logger.LogVerbose($"Session is Private: {m_Session.IsPrivate}");
                Logger.LogVerbose($"Session is Full: {m_BackfillIsFull}");

                await StopBackfillingAsync();
                return;
            }

            // At the beginning of the loop remove the player that did not connect in time
            ValidateAndRemovePlayersPendingConnection();

            if (m_LocalDataDirty)
            {
                try
                {
                    if (!BackfillTicketIsValid())
                    {
                        Logger.LogError("Backfill ticket is invalid - Cannot update backfill ticket.");
                        return;
                    }
                    Logger.LogVerbose("Updating backfill ticket.");
                    await m_MatchmakerService.UpdateBackfillTicketAsync(m_LocalBackfillTicket.Id, m_LocalBackfillTicket);
                    m_LocalDataDirty = false;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error updating backfill ticket: {e.Message}");
                }
            }
            else
            {
                try
                {
                    if (m_LocalBackfillTicket == null)
                    {
                        Logger.LogError("Local backfill ticket is null. Backfilling needs to be started first.");
                        return;
                    }

                    var backfillTicket = await ApproveBackfillTicket(m_LocalBackfillTicket.Id);
                    if (backfillTicket != null)
                        m_LocalBackfillTicket = backfillTicket;

                    // Add new players to the pending list of players waiting for connection
                    AddNewPlayersToPendingPlayers();
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error approving backfill ticket: {e.Message}");
                }
            }

            if (!m_ShouldBackfill)
            {
                Logger.LogVerbose("Stopping backfilling - Session is now locked or private.");
                await StopBackfillingAsync();
                return;
            }

            ScheduleApproveBackfillLoop();
        }

        void AddNewPlayersToPendingPlayers()
        {
            if (m_playerConnectionTimeout == 0)
                return;

            if (!BackfillTicketIsValid())
                return;

            if (!LocalMatchPropertiesAreValid())
                return;

            // Add the new player IDs to the list of players waiting for connection
            foreach (var backfillPlayer in m_LocalBackfillTicket.Properties.MatchProperties.Players)
            {
                if (!m_matchProperties.Players.Exists(p => p.Id == backfillPlayer.Id))
                {
                    Logger.LogVerbose($"Player {backfillPlayer.Id} added to the list of players waiting for connection. {m_playerConnectionTimeout} seconds to connect.");
                    m_playerWaitingConnection[backfillPlayer.Id] = DateTime.Now;
                }
            }
        }

        void ValidateAndRemovePlayersPendingConnection()
        {
            foreach (var tuple in m_playerWaitingConnection)
            {
                if (tuple.Value.AddSeconds(m_playerConnectionTimeout) > DateTime.Now)
                {
                    Logger.LogVerbose($"Player {tuple.Key} did not connect in time. Removing from backfill.");
                    RemovePlayerFromBackfill(tuple.Key);
                }
            }
        }

        async Task<BackfillTicket> ApproveBackfillTicket(string backfillTicketId)
        {
            try
            {
                return await m_MatchmakerService.ApproveBackfillTicketAsync(backfillTicketId);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error while approving backfill ticket: {e.Message}");
                return null;
            }
        }

        bool BackfillTicketIsValid()
        {
            if (m_LocalBackfillTicket == null)
            {
                Logger.LogVerbose("No local backill data. Backfilling may not have started yet");
                return false;
            }

            return m_LocalBackfillTicket.Properties != null &&
                m_LocalBackfillTicket.Properties.MatchProperties != null &&
                m_LocalBackfillTicket.Properties.MatchProperties.Players != null &&
                m_LocalBackfillTicket.Properties.MatchProperties.Teams != null &&
                m_LocalBackfillTicket.Properties.MatchProperties.Teams.Count > 0;
        }

        async Task<bool> InitializeMatchmakingResults()
        {
            try
            {
                MatchmakingResults = await m_MatchmakerService.GetMatchmakingResultsAsync(m_Session.Id);
            }
            catch (Exception e)
            {
                Logger.LogError("Error while fetching matchmaking results from allocation payload: " + e.Message);
                return false;
            }

            if (MatchmakingResults == null)
            {
                Logger.LogError("Matchmaking results are null");
                return false;
            }

            if (MatchmakingResults.MatchProperties == null ||
                MatchmakingResults.MatchProperties.Teams == null ||
                MatchmakingResults.MatchProperties.Players == null)
            {
                Logger.LogError("Match properties on matchmaking results are invalid");
                return false;
            }

            m_matchProperties = MatchmakingResults.MatchProperties;
            return true;
        }

        internal void SetBackfillingConfiguration(BackfillingConfiguration options)
        {
            m_BackfillIsEnabled = options.Enable;
            m_AutomaticallyRemovePlayersFromBackfill = options.AutomaticallyRemovePlayers;
            m_AutomaticallyStartBackfillingWhenPlayerIsMissing =
                options.AutoStart;
            m_BackfillingLoopInterval = options.BackfillingLoopInterval;
            m_playerConnectionTimeout = options.PlayerConnectionTimeout;
        }

        internal void InitializeMatchmakingModule(bool backfillIsAllowed, NetworkEndpointAddress? connectionInfo)
        {
            m_shouldInitialize = true;
            m_backfillIsAllowed = backfillIsAllowed;
            m_connectionInfo = connectionInfo;
        }

        bool LocalBackfillTicketIsValid() => m_LocalBackfillTicket.Properties != null &&
        m_LocalBackfillTicket.Properties.MatchProperties != null &&
        m_LocalBackfillTicket.Properties.MatchProperties.Players != null &&
        m_LocalBackfillTicket.Properties.MatchProperties.Teams != null;

        bool LocalMatchPropertiesAreValid() =>
            m_matchProperties.Players != null
            && m_matchProperties.Teams != null
            && m_matchProperties.Players != null;
    }
}
