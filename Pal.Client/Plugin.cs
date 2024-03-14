using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Pal.Client.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Pal.Client.Properties;
using ECommons;
using ECommons.DalamudServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using ECommons.Configuration;
using ECommons.Schedulers;
using Dalamud.Plugin.Services;

namespace Pal.Client
{
    /// <summary>
    /// With all DI logic elsewhere, this plugin shell really only takes care of a few things around events that
    /// need to be sent to different receivers depending on priority or configuration .
    /// </summary>
    /// <see cref="DependencyInjectionContext"/>
    internal sealed class Plugin : IDalamudPlugin
    {
        private readonly CancellationTokenSource _initCts = new();

        private  DalamudPluginInterface _pluginInterface;
        private ICommandManager _commandManager;
        private IClientState _clientState;
        private IChatGui _chatGui;
        private IFramework _framework;

        private readonly TaskCompletionSource<IServiceScope> _rootScopeCompletionSource = new();
        private ELoadState _loadState = ELoadState.Initializing;

        private DependencyInjectionContext? _dependencyInjectionContext;
        private ILogger _logger = DependencyInjectionContext.LoggerProvider.CreateLogger<Plugin>();
        private WindowSystem? _windowSystem;
        internal IServiceScope? _rootScope;
        private Action? _loginAction;

        internal static Plugin P = null!;
        internal AdditionalConfiguration Config;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IClientState clientState,
            IChatGui chatGui,
            IFramework framework)
        {
            P = this;
            ECommonsMain.Init(pluginInterface, this, Module.SplatoonAPI, Module.DalamudReflector);
#if DEBUG
            new TickScheduler(delegate
            {
                Config = EzConfig.Init<AdditionalConfiguration>(); // TODO temp solution, move it to main config later (maybe)

                // set up the current UI language before creating anything
                Localization.Culture = new CultureInfo(Svc.PluginInterface.UiLanguage);

                Svc.Commands.AddHandler("/pal", new CommandInfo(OnCommand)
                {
                    HelpMessage = Localization.Command_pal_HelpText
                });
                Task.Run(async () => await CreateDependencyContext());
            });
#else
            if (pluginInterface.IsDev || !pluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins/main/pluginmaster.json"))
            {
                isDev = true;
                Svc.Chat.PrintError("[Palace Pal]为防止闲鱼小店倒卖插件，请通过仓库链接在线安装!");
                Svc.Chat.PrintError("[Palace Pal]如果你是花钱买的，赶紧退款吧，人傻钱多当我没说");
                Svc.Chat.PrintError($"[Palace Pal]插件安装仓库: {Svc.PluginInterface.SourceRepository} ,非本插件汉化者本人仓库!");
            }
            else
            {
                new TickScheduler(delegate
                {
                    Config = EzConfig.Init<AdditionalConfiguration>(); // TODO temp solution, move it to main config later (maybe)

                    // set up the current UI language before creating anything
                    Localization.Culture = new CultureInfo(Svc.PluginInterface.UiLanguage);

                    Svc.Commands.AddHandler("/pal", new CommandInfo(OnCommand)
                    {
                        HelpMessage = Localization.Command_pal_HelpText
                    });

                    Task.Run(async () => await CreateDependencyContext());
                });
            }
#endif
            //PunishLibMain.Init(pluginInterface, this);
        }

        public string Name => Localization.Palace_Pal;

        private async Task CreateDependencyContext()
        {
            try
            {
                _dependencyInjectionContext = Svc.PluginInterface.Create<DependencyInjectionContext>(this)
                                              ?? throw new Exception("Could not create DI root context class");
                var serviceProvider = _dependencyInjectionContext.BuildServiceContainer();
                _initCts.Token.ThrowIfCancellationRequested();

                _logger = serviceProvider.GetRequiredService<ILogger<Plugin>>();
                _windowSystem = serviceProvider.GetRequiredService<WindowSystem>();
                _rootScope = serviceProvider.CreateScope();

                var loader = _rootScope.ServiceProvider.GetRequiredService<DependencyContextInitializer>();
                await loader.InitializeAsync(_initCts.Token);

                await Svc.Framework.RunOnFrameworkThread(() =>
                {
                    Svc.PluginInterface.UiBuilder.Draw += Draw;
                    Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
                    Svc.PluginInterface.LanguageChanged += LanguageChanged;
                    Svc.ClientState.Login += Login;
                });
                _rootScopeCompletionSource.SetResult(_rootScope);
                _loadState = ELoadState.Loaded;
            }
            catch (ObjectDisposedException e)
            {
                _rootScopeCompletionSource.SetException(e);
                _loadState = ELoadState.Error;
            }
            catch (OperationCanceledException e)
            {
                _rootScopeCompletionSource.SetException(e);
                _loadState = ELoadState.Error;
            }
            catch (Exception e)
            {
                _rootScopeCompletionSource.SetException(e);
                _logger.LogError(e, "Async load failed");
                ShowErrorOnLogin(() =>
                    new Chat(Svc.Chat).Error(string.Format(Localization.Error_LoadFailed,
                        $"{e.GetType()} - {e.Message}")));

                _loadState = ELoadState.Error;
            }
        }

        private void ShowErrorOnLogin(Action? loginAction)
        {
            if (Svc.ClientState.IsLoggedIn)
            {
                loginAction?.Invoke();
                _loginAction = null;
            }
            else
                _loginAction = loginAction;
        }

        private void Login()
        {
            _loginAction?.Invoke();
            _loginAction = null;
        }

        private void OnCommand(string command, string arguments)
        {
            arguments = arguments.Trim();

            Task.Run(async () =>
            {
                IServiceScope rootScope;
                Chat chat;

                try
                {
                    rootScope = await _rootScopeCompletionSource.Task;
                    chat = rootScope.ServiceProvider.GetRequiredService<Chat>();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not wait for command root scope");
                    return;
                }

                try
                {
                    IPalacePalConfiguration configuration =
                        rootScope.ServiceProvider.GetRequiredService<IPalacePalConfiguration>();
                    if (configuration.FirstUse && arguments != "" && arguments != "config")
                    {
                        chat.Error(Localization.Error_FirstTimeSetupRequired);
                        return;
                    }

                    Action<string> commandHandler = rootScope.ServiceProvider
                        .GetRequiredService<IEnumerable<ISubCommand>>()
                        .SelectMany(cmd => cmd.GetHandlers())
                        .Where(cmd => cmd.Key == arguments.ToLowerInvariant())
                        .Select(cmd => cmd.Value)
                        .SingleOrDefault(missingCommand =>
                        {
                            chat.Error(string.Format(Localization.Command_pal_UnknownSubcommand, missingCommand,
                                command));
                        });
                    commandHandler.Invoke(arguments);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not execute command '{Command}' with arguments '{Arguments}'", command,
                        arguments);
                    chat.Error(string.Format(Localization.Error_CommandFailed,
                        $"{e.GetType()} - {e.Message}"));
                }
            });
        }

        private void OpenConfigUi()
            => _rootScope!.ServiceProvider.GetRequiredService<PalConfigCommand>().Execute();

        private void LanguageChanged(string languageCode)
        {
            _logger.LogInformation("Language set to '{Language}'", languageCode);

            Localization.Culture = new CultureInfo(languageCode);
            _windowSystem!.Windows.OfType<ILanguageChanged>()
                .Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            _rootScope!.ServiceProvider.GetRequiredService<RenderAdapter>().DrawLayers();
            _windowSystem!.Draw();
        }

        public void Dispose()
        {
            if (!isDev)
            {
                Svc.Commands.RemoveHandler("/pal");

                if (_loadState == ELoadState.Loaded)
                {
                    Svc.PluginInterface.UiBuilder.Draw -= Draw;
                    Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
                    Svc.PluginInterface.LanguageChanged -= LanguageChanged;
                    Svc.ClientState.Login -= Login;
                }

                _initCts.Cancel();
                _rootScope?.Dispose();
                _dependencyInjectionContext?.Dispose();
            }
            //PunishLibMain.Dispose();
            ECommonsMain.Dispose();
        }

        private enum ELoadState
        {
            Initializing,
            Loaded,
            Error
        }
    }
}
