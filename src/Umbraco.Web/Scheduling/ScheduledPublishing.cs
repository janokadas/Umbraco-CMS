﻿using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;

namespace Umbraco.Web.Scheduling
{
    internal class ScheduledPublishing : RecurringTaskBase
    {
        private readonly IRuntimeState _runtime;
        private readonly IContentService _contentService;
        private readonly ILogger _logger;

        public ScheduledPublishing(IBackgroundTaskRunner<RecurringTaskBase> runner, int delayMilliseconds, int periodMilliseconds,
            IRuntimeState runtime, IContentService contentService, ILogger logger)
            : base(runner, delayMilliseconds, periodMilliseconds)
        {
            _runtime = runtime;
            _contentService = contentService;
            _logger = logger;
        }

        public override bool PerformRun()
        {
            if (Suspendable.ScheduledPublishing.CanRun == false)
                return true; // repeat, later

            switch (_runtime.ServerRole)
            {
                case ServerRole.Replica:
                    _logger.Debug<ScheduledPublishing>("Does not run on replica servers.");
                    return true; // DO repeat, server role can change
                case ServerRole.Unknown:
                    _logger.Debug<ScheduledPublishing>("Does not run on servers with unknown role.");
                    return true; // DO repeat, server role can change
            }

            // ensure we do not run if not main domain, but do NOT lock it
            if (_runtime.IsMainDom == false)
            {
                _logger.Debug<ScheduledPublishing>("Does not run if not MainDom.");
                return false; // do NOT repeat, going down
            }

            // do NOT run publishing if not properly running
            if (_runtime.Level != RuntimeLevel.Run)
            {
                _logger.Debug<ScheduledPublishing>("Does not run if run level is not Run.");
                return true; // repeat/wait
            }

            try
            {
                // run
                // fixme context & events during scheduled publishing?
                // in v7 we create an UmbracoContext and an HttpContext, and cache instructions
                // are batched, and we have to explicitly flush them, how is it going to work here?
                var result = _contentService.PerformScheduledPublish(DateTime.Now).ToList();
                if (result.Count > 0)
                    foreach(var grouped in result.GroupBy(x => x.Result))
                        _logger.Info<ScheduledPublishing>("Scheduled publishing result: '{StatusCount}' items with status {Status}", grouped.Count(), grouped.Key);
            }
            catch (Exception ex)
            {
                _logger.Error<ScheduledPublishing>(ex, "Failed.");
            }

            return true; // repeat
        }

        public override bool IsAsync => false;
    }
}
