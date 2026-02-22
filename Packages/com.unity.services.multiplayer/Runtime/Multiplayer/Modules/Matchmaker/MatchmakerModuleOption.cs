using System;

namespace Unity.Services.Multiplayer
{
    class MatchmakerModuleOption : IModuleOption
    {
        readonly BackfillingConfiguration m_Options;

        public MatchmakerModuleOption(BackfillingConfiguration options)
        {
            m_Options = options;
        }

        public Type Type => typeof(MatchmakerModuleOption);
        internal bool IsPeerToPeer { get; set; }

        public void Process(SessionHandler session)
        {
            var module = session.GetModule<MatchmakerModule>();
            if (module == null)
            {
                throw new SessionException(
                    "Trying to setup connection in session but the module isn't registered.", SessionError.MatchmakerModuleMissing);
            }

            module.IsPeerToPeer = IsPeerToPeer;
            if (session.IsHost)
            {
                module.SetBackfillingConfiguration(m_Options);
            }
        }
    }
}
