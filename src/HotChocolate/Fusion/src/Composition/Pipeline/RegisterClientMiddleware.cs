namespace HotChocolate.Fusion.Composition.Pipeline;

/// <summary>
/// Middleware that adds client configurations for subgraphs with the distributed fusion graph.
/// </summary>
internal sealed class RegisterClientMiddleware : IMergeMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(CompositionContext context, MergeDelegate next)
    {
        foreach (var configuration in context.Configurations)
        {
            foreach (var client in configuration.Clients)
            {
                switch (client)
                {
                    case HttpClientConfiguration httpClient:
                        context.FusionGraph.Directives.Add(
                            context.FusionTypes.CreateHttpDirective(
                                configuration.Name,
                                httpClient.BaseAddress));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(client));
                }
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
