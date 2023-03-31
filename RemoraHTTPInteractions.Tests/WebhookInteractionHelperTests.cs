using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Interactivity.Extensions;
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
        .AddInteractivity()
        .AddResponder<TCSResponder>()
        .AddSingleton(tcs)
        .AddHTTPInteractionAPIs();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.IsTrue(res.IsSuccess);
        Assert.IsTrue(tcs.Task.IsCompleted);
    }

    /// <summary>
    /// Tests that the <see cref="WebhookInteractionHelper"/> correctly serializes a payload without any attachments.
    /// </summary>
    [Test]
    public async Task CorrectlySerializesPayloadWithoutAttachments()
    {
        var services = new ServiceCollection()
                       .AddDiscordGateway(_ => "dummy")
                       .AddInteractivity()
                       .AddResponder<AttachmentlessResponder>()
                       .AddHTTPInteractionAPIs();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.IsTrue(res.IsSuccess);

        const string expected = """{"type":1}""";
        Assert.AreEqual(expected, res.Entity.Item1);
        Assert.IsFalse(res.Entity.Item2.HasValue);
    }

    /// <summary>
    /// Tests that the <see cref="WebhookInteractionHelper"/> correctly serializes a payload with a single attachment.
    /// </summary>
    [Test]
    public async Task CorrectlySerializesPayloadWithAttachment()
    {
        var services = new ServiceCollection()
                       .AddDiscordGateway(_ => "dummy")
                       .AddInteractivity()
                       .AddResponder<SingleAttachmentResponder>()
                       .AddHTTPInteractionAPIs();
        
        var provider = services.BuildServiceProvider();

        var interaction = """{"id":0, "application_id": 1, "token": "dummy", "version":1, "type":1}""";
        
        var interactionService = provider.GetRequiredService<WebhookInteractionHelper>();
        
        var res = await interactionService.HandleInteractionAsync(interaction);
        
        Assert.IsTrue(res.IsSuccess);
        
        const string expected = """{"type":1,"data":{"attachments":[{"id":"0","filename":"owo.png","description":"No description set."}]}}""";
        Assert.AreEqual(expected, res.Entity.Item1);
        Assert.True(res.Entity.Item2.HasValue);
    }
}

public class TCSResponder : IResponder<IInteractionCreate>
{
    private readonly TaskCompletionSource _tcs;
    private readonly IDiscordRestInteractionAPI _interactions;
    
    public TCSResponder(TaskCompletionSource tcs, IDiscordRestInteractionAPI interactions)
    {
        _tcs = tcs;
        _interactions = interactions;
    }

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        _tcs.SetResult();
        return _interactions.CreateInteractionResponseAsync(gatewayEvent.ID, gatewayEvent.Token, new InteractionResponse(InteractionCallbackType.Pong), ct: ct);
    }
}

public class AttachmentlessResponder : IResponder<IInteractionCreate>
{
    private readonly IDiscordRestInteractionAPI _interactions;
    
    public AttachmentlessResponder(IDiscordRestInteractionAPI interactions)
    {
        _interactions = interactions;
    }

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        return _interactions.CreateInteractionResponseAsync(gatewayEvent.ID, gatewayEvent.Token, new InteractionResponse(InteractionCallbackType.Pong), ct: ct);
    }
}

public class SingleAttachmentResponder : IResponder<IInteractionCreate>
{
    private readonly IDiscordRestInteractionAPI _interactions;

    public SingleAttachmentResponder(IDiscordRestInteractionAPI interactions)
    {
        _interactions = interactions;
    }

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        // This completes synchronously. [Not awaiting] is fine.
        _interactions.CreateInteractionResponseAsync
        (
            gatewayEvent.ID,
            gatewayEvent.Token,
            new InteractionResponse(InteractionCallbackType.Pong),
            new[] { OneOf<FileData, IPartialAttachment>.FromT0(new("owo.png", Stream.Null)) }
        );
        
        return Task.FromResult(Result.FromSuccess());
    }
}