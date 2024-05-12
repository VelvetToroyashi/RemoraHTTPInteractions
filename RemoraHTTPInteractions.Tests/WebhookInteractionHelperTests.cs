using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using RemoraHTTPInteractions.Extensions;
using RemoraHTTPInteractions.Services;
using OneOf;

namespace RemoraHTTPInteractions.Tests;

public class WebhookInteractionHelperTests
{
    /// <summary>
    /// Tests that the <see cref="WebhookInteractionHelper"/> informs the dispatcher about the incoming event.
    /// </summary>
    [Test]
    public async Task HandleInteractionAsyncDispatchesPayload()
    {
        var tcs = new TaskCompletionSource();
        
        var services = new ServiceCollection()
        .AddDiscordGateway(_ => "dummy")
        .AddResponder<TCSResponder>()
        .AddSingleton(tcs)
        .AddHttpInteractions();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.That(res.IsSuccess);
        Assert.That(tcs.Task.IsCompleted);
    }

    /// <summary>
    /// Tests that the <see cref="WebhookInteractionHelper"/> correctly serializes a payload without any attachments.
    /// </summary>
    [Test]
    public async Task CorrectlySerializesPayloadWithoutAttachments()
    {
        var services = new ServiceCollection()
                       .AddDiscordGateway(_ => "dummy")
                       .AddResponder<AttachmentlessResponder>()
                       .AddHttpInteractions();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.That(res.IsSuccess);

        const string expected = """{"type":1}""";
        Assert.That(res.Entity.Item1, Is.EqualTo(expected));
        Assert.That(res.Entity.Item2.HasValue, Is.False);
    }

    /// <summary>
    /// Tests that the <see cref="WebhookInteractionHelper"/> correctly serializes a payload with a single attachment.
    /// </summary>
    [Test]
    public async Task CorrectlySerializesPayloadWithAttachment()
    {
        var services = new ServiceCollection()
                       .AddDiscordGateway(_ => "dummy")
                       .AddResponder<SingleAttachmentResponder>()
                       .AddHttpInteractions();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.That(res.IsSuccess);
        
        const string expected = """{"type":1,"data":{"attachments":[{"id":"0","filename":"owo.png","description":"No description set."}]}}""";
        Assert.That(res.Entity.Item1, Is.EqualTo(expected));
        Assert.That(res.Entity.Item2.HasValue, Is.True);
    }
}

public class TCSResponder(TaskCompletionSource tcs, IDiscordRestInteractionAPI interactions)
: IResponder<IInteractionCreate>
{

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        tcs.SetResult();
        return interactions.CreateInteractionResponseAsync(gatewayEvent.ID, gatewayEvent.Token, new InteractionResponse(InteractionCallbackType.Pong), ct: ct);
    }
}

public class AttachmentlessResponder(IDiscordRestInteractionAPI interactions) : IResponder<IInteractionCreate>
{

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        return interactions.CreateInteractionResponseAsync(gatewayEvent.ID, gatewayEvent.Token, new InteractionResponse(InteractionCallbackType.Pong), ct: ct);
    }
}

public class SingleAttachmentResponder(IDiscordRestInteractionAPI interactions) : IResponder<IInteractionCreate>
{

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        // This completes synchronously. [Not awaiting] is fine.
        interactions.CreateInteractionResponseAsync
        (
            gatewayEvent.ID,
            gatewayEvent.Token,
            new InteractionResponse(InteractionCallbackType.Pong),
            new[] { OneOf<FileData, IPartialAttachment>.FromT0(new("owo.png", Stream.Null)) },
            ct
        );
        
        return Task.FromResult(Result.FromSuccess());
    }
}