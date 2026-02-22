using System;
using System.Threading.Tasks;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Facade for session browsing, session management and matchmaking.
    /// </summary>
    public interface IMultiplayerServerService
    {
        /// <summary>
        /// Creates a server session.
        /// </summary>
        /// <param name="sessionOptions">The options for the resulting session</param>
        /// <returns>The created server session</returns>
        /// <exception cref="ArgumentNullException">If a parameter is null.</exception>
        /// <exception cref="SessionException">Provides a specific session error type and error message.</exception>
        public Task<IServerSession> CreateSessionAsync(SessionOptions sessionOptions);
    }

    /// <summary>
    /// The entry class of the Multiplayer SDK and session system.
    /// </summary>
    public static class MultiplayerServerService
    {
        /// <summary>
        /// A static instance of the Multiplayer service and session system.
        /// </summary>
        public static IMultiplayerServerService Instance { get; internal set; }
    }
}
