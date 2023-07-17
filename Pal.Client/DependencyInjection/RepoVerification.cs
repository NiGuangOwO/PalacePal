using Dalamud.Plugin;
using Microsoft.Extensions.Logging;

namespace Pal.Client.DependencyInjection
{
    internal sealed class RepoVerification
    {
        public RepoVerification(ILogger<RepoVerification> logger, DalamudPluginInterface pluginInterface, Chat chat)
        {
            logger.LogInformation("Install source: {Repo}", pluginInterface.SourceRepository);
            /*if (!pluginInterface.IsDev
                && !pluginInterface.SourceRepository.StartsWith("https://raw.githubusercontent.com/carvelli/")
                && !pluginInterface.SourceRepository.StartsWith("https://github.com/carvelli/"))
            {
                chat.Error(string.Format(Localization.Error_WrongRepository,
                    "https://github.com/carvelli/Dalamud-Plugins"));
                throw new InvalidOperationException();
            }*/
        }
    }
}
