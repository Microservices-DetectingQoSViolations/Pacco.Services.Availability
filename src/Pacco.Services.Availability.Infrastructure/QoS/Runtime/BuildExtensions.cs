using System;
using System.Collections.Generic;
using System.Text;
using Convey;
using Microsoft.AspNetCore.Builder;
using Prometheus;
using Prometheus.DotNetRuntime;

namespace Pacco.Services.Availability.Infrastructure.QoS.Runtime
{
    public static class BuildExtensions
    {
        public static void RegisterRuntimeMetrics(this IConveyBuilder builder)
        {
            DotNetRuntimeStatsBuilder
                .Customize()
                .WithGcStats()
                .WithThreadPoolStats()
                .StartCollecting();
        }

        public static IApplicationBuilder UseRuntimeMetrics(this IApplicationBuilder app)
        {
            return app.UseEndpoints(endpoints =>
                endpoints.MapMetrics("runtime_metrics"));
        }
    }
}
