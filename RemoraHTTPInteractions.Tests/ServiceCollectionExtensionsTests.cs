using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Gateway.Extensions;
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
        
        services.AddDiscordGateway(_ => "dummy");
        services.AddHttpInteractions();

        var serviceProvider = services.BuildServiceProvider();

        var interactionAPI = serviceProvider.GetService<IDiscordRestInteractionAPI>();
        
        Assert.That(interactionAPI, Is.Not.Null);
        Assert.That(interactionAPI, Is.InstanceOf<DiscordWebhookInteractionAPI>());

        var interactionHelper = serviceProvider.GetService<WebhookInteractionHelper>();
        Assert.That(interactionHelper, Is.Not.Null);
        Assert.That(interactionHelper, Is.InstanceOf<WebhookInteractionHelper>());
    }
}
