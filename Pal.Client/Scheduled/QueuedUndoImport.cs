using System;
using Microsoft.Extensions.Logging;
using Pal.Client.DependencyInjection;
using Pal.Client.Windows;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedUndoImport : IQueueOnFrameworkThread
    {
        public QueuedUndoImport(Guid exportId)
        {
            ExportId = exportId;
        }

        private Guid ExportId { get; }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedUndoImport>
        {
            private readonly ImportService _importService;
            private readonly ConfigWindow _configWindow;

            public Handler(ILogger<Handler> logger, ImportService importService, ConfigWindow configWindow)
                : base(logger)
            {
                _importService = importService;
                _configWindow = configWindow;
            }

            protected override void Run(QueuedUndoImport queued, ref bool recreateLayout)
            {
                recreateLayout = true;

                _importService.RemoveById(queued.ExportId);
                _configWindow.UpdateLastImport();
            }
        }
    }
}
