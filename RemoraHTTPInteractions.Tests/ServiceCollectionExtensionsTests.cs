using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Discord.Rest;
using Remora.Discord.Rest.Extensions;
using RemoraHTTPInteractions.Extensions;
using RemoraHTTPInteractions.Services;

namespace RemoraHTTPInteractions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddRemoraHttpInteractions_ShouldAddServices()
    {
        var services = new ServiceCollection();
        
        services.AddInteractivity();
        services.AddDiscordGateway(_ => "dummy");
        services.AddHTTPInteractionAPIs();

        var serviceProvider = services.BuildServiceProvider();

        var interactionAPI = serviceProvider.GetService<IDiscordRestInteractionAPI>();
        
        Assert.NotNull(interactionAPI);
        Assert.IsInstanceOf<DiscordWebhookInteractionAPI>(interactionAPI);

        var interactionHelper = serviceProvider.GetRequiredService<WebhookInteractionHelper>();
        Assert.NotNull(interactionHelper);
        Assert.IsInstanceOf<WebhookInteractionHelper>(interactionHelper);
    }
}
