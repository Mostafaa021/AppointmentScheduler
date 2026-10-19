using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AppointmentScheduler.Api.Idempotency;

/// <summary>Documents the required <c>Idempotency-Key</c> header on every write the <see cref="IdempotencyFilter"/> guards.</summary>
internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var description = context.Description;
        if (!IdempotencyFilter.IsWrite(description.HttpMethod ?? "")
            || description.ActionDescriptor.EndpointMetadata.OfType<SkipIdempotencyAttribute>().Any())
            return Task.CompletedTask;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyHeaders.Key,
            In = ParameterLocation.Header,
            Required = true,
            Description = "A UUID per user action, reused on retries. Same key + same request → the stored response "
                          + "(Idempotent-Replayed: true); same key + different request → 422 IDEMPOTENCY_KEY_REUSED.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = IdempotencyHeaders.MaxKeyLength },
        });
        return Task.CompletedTask;
    }
}
