﻿using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using System;

namespace Pal.Client.Net
{
    internal sealed partial class RemoteApi : IDisposable
    {
#if DEBUG
        public static string RemoteUrl { get; } = "https://deep-dungeon.api.puni.sh";
#else
        public const string RemoteUrl = "https://deep-dungeon.api.puni.sh";
#endif
        private readonly string _userAgent =
            $"{typeof(RemoteApi).Assembly.GetName().Name?.Replace(" ", "")}/{typeof(RemoteApi).Assembly.GetName().Version?.ToString()}";

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RemoteApi> _logger;
        private readonly Chat _chat;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;

        private GrpcChannel? _channel;
        private LoginInfo _loginInfo = new(null);
        private bool _warnedAboutUpgrade;

        public RemoteApi(
            ILoggerFactory loggerFactory,
            ILogger<RemoteApi> logger,
            Chat chat,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _chat = chat;
            _configurationManager = configurationManager;
            _configuration = configuration;
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing gRPC channel");
            _channel?.Dispose();
            _channel = null;
        }
    }
}
