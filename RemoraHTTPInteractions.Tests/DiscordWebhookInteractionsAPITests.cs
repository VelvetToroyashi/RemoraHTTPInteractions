using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Rest;
using Remora.Discord.Rest.Extensions;
using RemoraHTTPInteractions.Extensions;
using RemoraHTTPInteractions.Services;

namespace RemoraHTTPInteractions.Tests;

public class DiscordWebhookInteractionsAPITests
{
    /// <summary>
    /// Tests that the <see cref="DiscordWebhookInteractionsAPI"/> can set a webhook response to be retrieved later.
    /// </summary>
    [Test]
    public async Task APISuccessfullySetsReponse()
    {
        var res = new InteractionWebhookResponse(new());
        InMemoryDataStore<string, InteractionWebhookResponse>.Instance.TryAddValue("123", res);

        var services = new ServiceCollection()
                       .AddDiscordRest(_ => ("dummy", DiscordTokenType.Bot))
                       .AddHttpInteractions();
        
        var provider = services.BuildServiceProvider();
        
        var api = provider.GetRequiredService<IDiscordRestInteractionAPI>();
        
        var response = await api.CreateInteractionResponseAsync
        (
            DiscordSnowflake.New(123),
            "123",
            new InteractionResponse(InteractionCallbackType.DeferredUpdateMessage)
        );
        
        Assert.That(response.IsSuccess);
        Assert.That(res.ResponseTCS.Task.IsCompleted);
        Assert.That(res.ResponseTCS.Task.Result.Response.Type, Is.EqualTo(InteractionCallbackType.DeferredUpdateMessage));
    }
    
    [Test]
    public async Task APIFailsForNonexistentInteraction()
    {
        var services = new ServiceCollection()
                       .AddDiscordRest(_ => ("dummy", DiscordTokenType.Bot))
                       .AddHttpInteractions();
        
        var provider = services.BuildServiceProvider();
        
        var api = provider.GetRequiredService<IDiscordRestInteractionAPI>();
        
        var response = await api.CreateInteractionResponseAsync
        (
            DiscordSnowflake.New(123),
            "123",
            new InteractionResponse(InteractionCallbackType.DeferredUpdateMessage)
        );
        
        Assert.That(response.IsSuccess, Is.False);
    }
}
