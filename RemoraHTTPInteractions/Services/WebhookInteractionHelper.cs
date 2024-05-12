using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OneOf;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace RemoraHTTPInteractions.Services;

/// <summary>
/// An intermediate service that glues ASP.NET Core to Remora.Discord.
/// </summary>
public class WebhookInteractionHelper
{
    private readonly TimeProvider _time;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IResponderDispatchService _dispatch;
    private readonly InMemoryDataStore<string, InteractionWebhookResponse> _data;
    
    /// <summary>
    /// Creates a new instance of the <see cref="WebhookInteractionHelper"/> class.
    /// </summary>
    /// <param name="jsonOptions">The JSON Serializer options.</param>
    /// <param name="dispatch">The responder dispatch service.</param>
    /// <param name="data">The In-Memory Data Service.</param>
    public WebhookInteractionHelper
    (
        IOptionsMonitor<JsonSerializerOptions> jsonOptions,
        IResponderDispatchService dispatch,
        TimeProvider? time = default
    )
    {
        _dispatch = dispatch;
        _jsonOptions = jsonOptions.Get("Discord");
        _data = InMemoryDataStore<string, InteractionWebhookResponse>.Instance;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Handles an incoming request from Discord. This method assumes the given payload is valid. (e.g. <see cref="DiscordHeaders.VerifySignature"/> returns true).
    /// </summary>
    /// <param name="json">The JSON body of the request.</param>
    /// <returns>A result containing the serialized payload for the request, as well as any potential streams for uploaded files.</returns>
    /// <remarks>It is up to the caller to return a form-encoded response if streams are present. See https://discord.dev/reference#uploading-files for more info.</remarks>
    [ExcludeFromCodeCoverage]
    public Task<Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>> HandleInteractionAsync(string json)
    {
        var interaction = JsonSerializer.Deserialize<IInteractionCreate>(json, _jsonOptions)!;

        return HandleInteractionCoreAsync(interaction);
    }
    
    [ExcludeFromCodeCoverage]
    public Task<Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>> HandleInteractionAsync(IInteractionCreate interaction)
    {
        return HandleInteractionCoreAsync(interaction);
    }

    private async Task<Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>> HandleInteractionCoreAsync(IInteractionCreate interaction)
    {
        // This method assumes a valid interaction has been received.
        // Sanity check: Ensure three seconds have not passed since the interaction was received.
        if (interaction.ID.Timestamp + TimeSpan.FromSeconds(3) < _time.GetUtcNow())
        {
            return Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>.FromError(new InteractionTimeoutError());
        }

        InteractionWebhookResponse data = new(new TaskCompletionSource<InteractionWebhookResponseData>());
        _data.TryAddValue(interaction.Token, data);
        
        await _dispatch.DispatchAsync(new Payload<IInteractionCreate>(interaction));

        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
        Task completedTask = await Task.WhenAny(data.ResponseTCS.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            await _data.DeleteAsync(interaction.Token);
            return Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>.FromError(new InteractionTimeoutError());
        }
        
        InteractionWebhookResponseData response = data.ResponseTCS.Task.Result;

        if (!response.Attachments.IsDefined(out var attachments))
        {
            var json = JsonSerializer.Serialize(response.Response, _jsonOptions);
            return Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>.FromSuccess((json, default));
        }
        else
        {
            var streams = attachments
                          .Where(attachment => attachment.IsT0)
                          .ToDictionary(attachment => attachment.AsT0.Name, attachment => attachment.AsT0.Content);

            var json = JsonSerializer.Serialize(NormalizeAttachments((InteractionResponse)response.Response, attachments), _jsonOptions);
            return Result<(string, Optional<IReadOnlyDictionary<string, Stream>>)>.FromSuccess((json, new(streams)));
        }
    }
    
    /// <summary>
    /// "Normalizes" a payload to be serialized by ensuring all streams are in the attachments list, as well as any pre-existing attachments.
    /// </summary>
    /// <param name="response">The response to normalize.</param>
    /// <param name="userAttachments">A list of attachments to be normalized.</param>
    /// <returns>The normalized payload.</returns>
    internal static InteractionResponse NormalizeAttachments(InteractionResponse response, IReadOnlyList<OneOf<FileData, IPartialAttachment>> userAttachments)
    {
        
        if (!response.Data.IsDefined(out var val) || !val.IsT0)
        {
            //return response;
            // TODO! This is a workaround for a bug in Remora.Discord
            // tl;dr Remora requires the payload to have content, however
            // you're implored not to set the attachments field on the payload 
            // for...some reason. Because a response consisting only of attachments
            // is technically valid, data may not be present, and we set a default here.
            val = new InteractionMessageCallbackData();
        }

        var data = val.AsT0;

        var attachments = userAttachments.Select
        (
            (f, i) => f.Match
            (
                data => new PartialAttachment(DiscordSnowflake.New((ulong)i), data.Name, data.Description),
                attachment => attachment
            )
        )
        .ToList();
        
        return response with { Data = new(((InteractionMessageCallbackData)data)! with { Attachments = attachments }) };
    }
}

file record InteractionTimeoutError
(
    string Message = "The response window for the initial interaction callback has expired."
) : IResultError;