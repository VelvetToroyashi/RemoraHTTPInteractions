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
using Remora.Discord.Interactivity.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace RemoraHTTPInteractions.Services;

/// <summary>
/// An intermediate service that glues ASP.NET Core to Remora.Discord.
/// </summary>
public class WebhookInteractionHelper
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ResponderDispatchService _dispatch;
    private readonly InMemoryDataService<string, InteractionWebhookResponse> _data;
    
    /// <summary>
    /// Creates a new instance of the <see cref="WebhookInteractionHelper"/> class.
    /// </summary>
    /// <param name="jsonOptions">The JSON Serializer options.</param>
    /// <param name="dispatch">The responder dispatch service.</param>
    /// <param name="data">The In-Memory Data Service.</param>
    public WebhookInteractionHelper
    (
        IOptionsMonitor<JsonSerializerOptions> jsonOptions,
        ResponderDispatchService dispatch
    )
    {
        _jsonOptions = jsonOptions.Get("Discord");
        _dispatch = dispatch;
        _data = InMemoryDataService<string, InteractionWebhookResponse>.Instance;
    }

    /// <summary>
    /// Handles an incoming request from Discord. This method assumes the given payload is valid. (e.g. <see cref="DiscordHeaders.VerifySignature"/> returns true).
    /// </summary>
    /// <param name="json">The JSON body of the request.</param>
    /// <returns>A result containing the serialized payload for the request, as well as any potential streams for uploaded files.</returns>
    /// <remarks>It is up to the caller to return a form-encoded response if streams are present. See https://discord.dev/reference#uploading-files for more info.</remarks>
    [ExcludeFromCodeCoverage]
    public Task<Result<(string, Optional<IReadOnlyList<Stream>>)>> HandleInteractionAsync(string json)
    {
        var interaction = JsonSerializer.Deserialize<IInteractionCreate>(json, _jsonOptions);

        return HandleInteractionAsync(interaction);
    }

    /// <summary>
    /// Handles an incoming request from Discord. This method assumes the given payload is valid. (e.g. <see cref="DiscordHeaders.VerifySignature"/> returns true).
    /// </summary>
    /// <param name="stream">The JSON body of the request.</param>
    /// <returns>A result containing the serialized payload for the request, as well as any potential streams for uploaded files.</returns>
    /// <remarks>It is up to the caller to return a form-encoded response if streams are present. See https://discord.dev/reference#uploading-files for more info.</remarks>
    [ExcludeFromCodeCoverage]
    public async Task<Result<(string, Optional<IReadOnlyList<Stream>>)>> HandleInteractionAsync(Stream stream)
    {
        var interaction = await JsonSerializer.DeserializeAsync<IInteractionCreate>(stream, _jsonOptions);
        
        return await HandleInteractionAsync(interaction);
    }
    
    internal async Task<Result<(string, Optional<IReadOnlyList<Stream>>)>> HandleInteractionAsync(IInteractionCreate interaction)
    {
        // This method assumes a valid interaction has been received.

        var data = new InteractionWebhookResponse(new());
        _data.TryAddData(interaction.Token, data);
        
        _dispatch.DispatchAsync(new Payload<IInteractionCreate>(interaction));

        var response = await data.ResponseTCS.Task;

        _data.TryRemoveData(interaction.Token);

        if (!response.Attachments.IsDefined(out var attachments))
        {
            var json = JsonSerializer.Serialize(response.Response, _jsonOptions);
            return Result<(string, Optional<IReadOnlyList<Stream>>)>.FromSuccess((json, default));
        }
        else
        {
            var streams = new List<Stream>();

            foreach (var attachment in attachments)
            {
                if (!attachment.IsT0)
                {
                    continue;
                }

                streams.Add(attachment.AsT0.Content);
            }

            var json = JsonSerializer.Serialize(NormalizeAttachments((InteractionResponse)response.Response, attachments), _jsonOptions);
            return Result<(string, Optional<IReadOnlyList<Stream>>)>.FromSuccess((json, new(streams.ToArray())));
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
