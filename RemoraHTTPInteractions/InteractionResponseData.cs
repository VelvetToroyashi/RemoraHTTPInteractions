using OneOf;
using Remora.Rest.Core;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;

namespace RemoraHTTPInteractions;

public record InteractionWebhookResponse(TaskCompletionSource<InteractionWebhookResponseData> ResponseTCS);

public record InteractionWebhookResponseData(IInteractionResponse Response, Optional<IReadOnlyList<OneOf<FileData, IPartialAttachment>>> Attachments);