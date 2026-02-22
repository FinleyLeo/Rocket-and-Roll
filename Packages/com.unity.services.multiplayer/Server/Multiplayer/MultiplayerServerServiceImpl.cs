using System.Threading.Tasks;

namespace Unity.Services.Multiplayer
{
    class MultiplayerServerServiceImpl : IMultiplayerServerService
    {
        readonly SessionManager m_SessionManager;

        internal MultiplayerServerServiceImpl(
            SessionManager sessionManager)
        {
            m_SessionManager = sessionManager;
        }

        public Task<IServerSession> CreateSessionAsync(SessionOptions sessionOptions)
        {
            return m_SessionManager.CreateAsync(sessionOptions).ContinueWith(t => t.Result.AsServer());
        }
    }
}
