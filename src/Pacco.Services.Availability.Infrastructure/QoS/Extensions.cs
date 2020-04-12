using System;
using System.Collections;
using System.Collections.Generic;
using Convey;
using Convey.CQRS.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Convey.CQRS.Events;
using Convey.CQRS.Queries;
using Convey.Types;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    internal static class Extensions
    {
        private const string SectionName = "qoSTracking";
        private const string Idx = "_idx";
        private const string Arr = "_arr";

        private const long LongElapsedMilliseconds = 60000;
        private static IQoSCacheFormatter _formatter;

        public static IConveyBuilder AddQoSTrackingDecorators(this IConveyBuilder builder)
        {
            var qoSTrackingOptions = builder.GetOptions<QoSTrackingOptions>(SectionName);

            if (qoSTrackingOptions.Enabled)
            {
                builder.Services.AddSingleton(qoSTrackingOptions);
                builder.Services.AddSingleton<IQoSTrackingSampler, QoSTrackingSampler>();
                builder.Services.AddSingleton<IQoSCacheFormatter, QoSCacheFormatter>();
                builder.Services.AddSingleton<IQoSViolateRaiser, QoSViolateRaiser>();

                builder.Services.AddTransient<IQoSTimeViolationChecker, QoSTimeViolationChecker>();

                builder.Services.TryDecorate(typeof(ICommandHandler<>), typeof(QoSTrackerCommandHandlerDecorator<>));
            }

            return builder;
        }

        public static IApplicationBuilder UseCache(
            this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var serviceName = scope.ServiceProvider.GetService<AppOptions>().Service;

            var commandHandlerType = typeof(ICommandHandler<>);
            var eventHandlerType = typeof(IEventHandler<>);
            var queryHandlerType = typeof(IQueryHandler<,>);

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var allHandlerClasses = allAssemblies
                .SelectMany(s => s.GetTypes())
                .Where(type => !type.Name.Contains("Decorator")) // Remove Decorators for preventing duplicates
                .Select(type =>
                    type.GetInterfaces()
                        .FirstOrDefault(i =>
                            i.IsGenericType &&
                            (i.GetGenericTypeDefinition() == commandHandlerType
                            || i.GetGenericTypeDefinition() == eventHandlerType
                            || i.GetGenericTypeDefinition() == queryHandlerType)))
                .Where(type => type is {})
                .Select(type => new { HandlerType = type.Name[1], GenericTypeName = type.GenericTypeArguments[0].Name }) // Get first generic type of handlers (command/event/query type)
                .Select(type => serviceName.ToLower() + type.HandlerType + type.GenericTypeName) // Select command/event/query name
                .Distinct()
                .Select(name => name.ToUnderscoreCase());

            SetDefaultCacheValues(scope, allHandlerClasses);

            return app;
        }

        private static void SetDefaultCacheValues(IServiceScope scope, IEnumerable<string> allHandlersClasses)
        {
            var cache = scope.ServiceProvider.GetService<IDistributedCache>();
            _formatter = scope.ServiceProvider.GetService<IQoSCacheFormatter>();

            var serializedLongElapsedMilliseconds = _formatter.SerializeNumber(LongElapsedMilliseconds);

            allHandlersClasses
                .ToList()
                .ForEach(key =>
                {
                    if (cache.Get(key) is {}) return;
                    cache.Set(key, serializedLongElapsedMilliseconds);
                });
        }
    }

    internal static class CommandExtensions
    {
        public static string GetCommandName<TCommand>(this TCommand command) where TCommand : class, ICommand
        {
            return ToUnderscoreCase("C" + command.GetType().Name);
        }

        public static string ToUnderscoreCase(this string str)
            => string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString()))
                .ToLowerInvariant();
    }
}
