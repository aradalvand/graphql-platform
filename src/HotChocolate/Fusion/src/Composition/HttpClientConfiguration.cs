namespace HotChocolate.Fusion.Composition;

/// <summary>
/// Represents the configuration for an HTTP client that can be used to fetch data from a subgraph.
/// </summary>
public sealed class HttpClientConfiguration : IClientConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientConfiguration"/> class.
    /// </summary>
    /// <param name="baseAddress">
    /// The base address of the client.
    /// </param>
    public HttpClientConfiguration(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Gets the base address of the client.
    /// </summary>
    public Uri BaseAddress { get; }
}
