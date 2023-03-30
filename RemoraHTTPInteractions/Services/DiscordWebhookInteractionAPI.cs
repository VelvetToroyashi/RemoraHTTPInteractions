using OneOf;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching.API;
using Remora.Discord.Interactivity.Services;
using Remora.Rest.Core;
using Remora.Results;
using RemoraHTTPInteractions.Extensions;

namespace RemoraHTTPInteractions.Services;

public class DiscordWebhookInteractionAPI : IDiscordRestInteractionAPI
{
    private readonly IDiscordRestInteractionAPI _underlying;
    private readonly InMemoryDataService<string, InteractionWebhookResponse> _dataService;
    
    public DiscordWebhookInteractionAPI(IDiscordRestInteractionAPI underlying, InMemoryDataService<string, InteractionWebhookResponse> dataService)
    {
        if (underlying is CachingDiscordRestInteractionAPI)
        {
            throw new InvalidOperationException
            (
                $"The call to `.{nameof(ServiceCollectionExtensions.AddHTTPInteractionAPIs)}` " +
                $"should be called before `.{nameof(Remora.Discord.Caching.Extensions.ServiceCollectionExtensions.AddDiscordCaching)}`"
            );
        }
        
        _underlying = underlying;
        _dataService = dataService;
    }

    /// <summary>
    /// This method does NOT create an interaction response, but instead sets the requisite data where it can then be returned to an ASP.NET Core controller/endpoint.
    /// </summary>
    /// <inheritdoc/>
    public async Task<Result> CreateInteractionResponseAsync
    (
        Snowflake interactionID,
        string interactionToken,
        IInteractionResponse response,
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>> attachments = new Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>>(),
        CancellationToken ct = default
    )
    {
        var interactionResult = await _dataService.LeaseDataAsync(interactionToken);
        
        if (!interactionResult.IsSuccess)
        {
            // The interaction was either already responded to, or doesn't exist.
            // If it hasn't, however, this is always infallible
            return Result.FromError(interactionResult); 
        }
        
        await using var interaction = interactionResult.Entity;
        
        if (interaction.Data.ResponseTCS.Task.IsCompleted)
        {
            return Result.FromSuccess(); // Too late, we've already sent a response
        }
        
        interaction.Data.ResponseTCS.SetResult(new(response, attachments));
        
        // TODO: if (interactionID.Timestamp < (DateTime.UtcNow.AddSeconds(-3))) return Result.FromError(); 

        return Result.FromSuccess();
    }

    /// <inheritdoc />
    public Task<Result<IMessage>> GetOriginalInteractionResponseAsync(Snowflake applicationID, string interactionToken, CancellationToken ct = default) =>
    _underlying.GetOriginalInteractionResponseAsync(applicationID, interactionToken, ct);

    /// <inheritdoc />
    public Task<Result<IMessage>> EditOriginalInteractionResponseAsync
    (
        Snowflake applicationID,
        string token,
        Optional<string?> content = new Optional<string?>(),
        Optional<IReadOnlyList<IEmbed>?> embeds = new Optional<IReadOnlyList<IEmbed>?>(),
        Optional<IAllowedMentions?> allowedMentions = new Optional<IAllowedMentions?>(),
        Optional<IReadOnlyList<IMessageComponent>?> components = new Optional<IReadOnlyList<IMessageComponent>?>(),
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>?> attachments = new Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>?>(),
        CancellationToken ct = default
    )
        => _underlying.EditOriginalInteractionResponseAsync(applicationID, token, content, embeds, allowedMentions, components, attachments, ct);

    /// <inheritdoc />
    public Task<Result> DeleteOriginalInteractionResponseAsync(Snowflake applicationID, string token, CancellationToken ct = default) => 
        _underlying.DeleteOriginalInteractionResponseAsync(applicationID, token, ct);

    /// <inheritdoc />
    public Task<Result<IMessage>> CreateFollowupMessageAsync
    (
        Snowflake applicationID,
        string token,
        Optional<string> content = new Optional<string>(),
        Optional<bool> isTTS = new Optional<bool>(),
        Optional<IReadOnlyList<IEmbed>> embeds = new Optional<IReadOnlyList<IEmbed>>(),
        Optional<IAllowedMentions> allowedMentions = new Optional<IAllowedMentions>(),
        Optional<IReadOnlyList<IMessageComponent>> components = new Optional<IReadOnlyList<IMessageComponent>>(),
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>> attachments = new Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>>(),
        Optional<MessageFlags> flags = new Optional<MessageFlags>(),
        CancellationToken ct = default
    )
        => _underlying.CreateFollowupMessageAsync(applicationID, token, content, isTTS, embeds, allowedMentions, components, attachments, flags, ct);

    /// <inheritdoc />
    public Task<Result<IMessage>> GetFollowupMessageAsync(Snowflake applicationID, string token, Snowflake messageID, CancellationToken ct = default) => 
        _underlying.GetFollowupMessageAsync(applicationID, token, messageID, ct);

    /// <inheritdoc />
    public Task<Result<IMessage>> EditFollowupMessageAsync
    (
        Snowflake applicationID,
        string token,
        Snowflake messageID,
        Optional<string?> content = new Optional<string?>(),
        Optional<IReadOnlyList<IEmbed>?> embeds = new Optional<IReadOnlyList<IEmbed>?>(),
        Optional<IAllowedMentions?> allowedMentions = new Optional<IAllowedMentions?>(),
        Optional<IReadOnlyList<IMessageComponent>?> components = new Optional<IReadOnlyList<IMessageComponent>?>(),
        Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>?> attachments = new Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>?>(),
        CancellationToken ct = default
    )
        => _underlying.EditFollowupMessageAsync(applicationID, token, messageID, content, embeds, allowedMentions, components, attachments, ct);

    /// <inheritdoc />
    public Task<Result> DeleteFollowupMessageAsync(Snowflake applicationID, string token, Snowflake messageID, CancellationToken ct = default) => 
        _underlying.DeleteFollowupMessageAsync(applicationID, token, messageID, ct);
}
