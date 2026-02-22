using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Internal;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Http;

namespace Unity.Services.Multiplayer
{
    partial class MultiplayerInitializer : IInitializablePackageV2
    {
        internal static WrappedMatchmakerService InitializeMatchmakerService(
            IAccessToken accessToken,
            IEnvironmentId environmentId,
            IInstallationId installationId,
            IProjectConfiguration projectConfiguration,
            string cloudEnvironment)
        {
            var httpClient = new HttpClient();

            var internalService = new InternalMatchmakerServiceSdk(
                httpClient,
                cloudEnvironment,
                accessToken);

            var matchmakerService = new WrappedMatchmakerService(projectConfiguration, installationId, environmentId, internalService);

            return matchmakerService;
        }
    }
}
