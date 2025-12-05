// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.IO.Http.Proxy;
using Snap.Hutao.Win32;
using System.Runtime.CompilerServices;

namespace Snap.Hutao.Core.Logging;

internal static class LoggerFactoryExtension
{
    extension(ILoggingBuilder builder)
    {
        public ILoggingBuilder AddSentryTelemetry()
        {
            return builder.AddSentry(options =>
            {
                options.HttpProxy = HttpProxyUsingSystemProxy.Instance;

#if DEBUG || IS_ALPHA_BUILD || IS_CANARY_BUILD
                // Alpha and Canary produces noisy events
                options.Dsn = "https://2d3047ff2d451986bc7ef395d1f1fe63@o4507525750521856.ingest.us.sentry.io/4510413123682304";
#else
                options.Dsn = "https://2d3047ff2d451986bc7ef395d1f1fe63@o4507525750521856.ingest.us.sentry.io/4510413123682304";
#endif

#if DEBUG
                options.Debug = true;
#endif

                options.AutoSessionTracking = true;
                options.IsGlobalModeEnabled = true;
                options.EnableBackpressureHandling = true;
                options.Release = $"{HutaoRuntime.Version}";
                options.Environment = GetBuildEnvironment();

                // Suppress logs to generate events and breadcrumbs
                options.MinimumBreadcrumbLevel = LogLevel.Information;
                options.MinimumEventLevel = LogLevel.Error;

                options.ProfilesSampleRate = 1.0D;
                options.TracesSampleRate = 1.0D;

                // Use our own exception handling
                options.DisableWinUiUnhandledExceptionIntegration();

                options.ConfigureScope(scope =>
                {
                    scope.User = new()
                    {
                        Id = HutaoRuntime.DeviceId,
                    };

                    scope.SetTag("elevated", HutaoRuntime.IsProcessElevated ? "yes" : "no");
                    scope.SetWebView2Version();
                });

                options.AddExceptionProcessor(new SentryExceptionProcessor());

                options.SetBeforeSend(@event =>
                {
                    Sentry.Protocol.OperatingSystem operatingSystem = @event.Contexts.OperatingSystem;
                    HutaoPrivateWindowsVersion windowsVersion = HutaoNative.Instance.GetCurrentWindowsVersion();
                    operatingSystem.Build = $"{windowsVersion.Build}";
                    operatingSystem.Name = "Windows";
                    operatingSystem.Version = $"{windowsVersion}";

                    return @event;
                });
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetBuildEnvironment()
    {
#if DEBUG
        return "DEBUG";
#elif IS_ALPHA_BUILD
        return "ALPHA";
#elif IS_CANARY_BUILD
        return "CANARY";
#else
        return "RELEASE";
#endif
    }

    extension(Scope scope)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWebView2Version()
        {
            WebView2Version webView2Version = HutaoRuntime.WebView2Version;
            Dictionary<string, object> webView2 = new()
            {
                ["Supported"] = webView2Version.Supported,
                ["Version"] = webView2Version.RawVersion,
            };

            scope.Contexts["WebView2"] = webView2;
        }
    }
}