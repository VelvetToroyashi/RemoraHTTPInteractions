using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Rest.Extensions;
using RemoraHTTPInteractions.Services;

namespace RemoraHTTPInteractions.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds HTTP-specific API services to the service collection to handle HTTP-based interactions.
    /// </summary>
    /// <param name="services">The service collection to add the APIs to.</param>
    /// <returns>The service collection to chain calls with.</returns>
    /// <remarks>This method MUST be called before <see cref="Remora.Discord.Caching.Extensions.ServiceCollectionExtensions.AddDiscordCaching"/>
    /// as the added API intercepts calls to CreateInteractionResponseAsync. Failing to do so may lead to undefined behavior.</remarks>
    public static IServiceCollection AddHTTPInteractionAPIs(this IServiceCollection services)
    {
        services.Decorate<IDiscordRestInteractionAPI, DiscordWebhookInteractionAPI>();
        services.AddSingleton<WebhookInteractionHelper>();

        return services;
    }
    
}
