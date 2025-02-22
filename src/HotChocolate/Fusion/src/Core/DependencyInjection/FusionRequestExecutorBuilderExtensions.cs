using HotChocolate.Execution.Configuration;
using HotChocolate.Fusion;
using HotChocolate.Fusion.Clients;
using HotChocolate.Fusion.Execution;
using HotChocolate.Fusion.Metadata;
using HotChocolate.Fusion.Pipeline;
using HotChocolate.Fusion.Planning;
using HotChocolate.Language;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class FusionRequestExecutorBuilderExtensions
{
    public static IRequestExecutorBuilder AddFusionGatewayServer(
        this IServiceCollection services,
        string serviceConfiguration)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (string.IsNullOrEmpty(serviceConfiguration))
        {
            throw new ArgumentNullException(nameof(serviceConfiguration));
        }

        var serviceConfDoc = Utf8GraphQLParser.Parse(serviceConfiguration);
        return AddFusionGatewayServer(services, serviceConfDoc);
    }

    public static IRequestExecutorBuilder AddFusionGatewayServer(
        this IServiceCollection services,
        DocumentNode serviceConfiguration)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (serviceConfiguration is null)
        {
            throw new ArgumentNullException(nameof(serviceConfiguration));
        }

        var context = FusionTypeNames.From(serviceConfiguration);
        var rewriter = new FusionGraphConfigurationToSchemaRewriter();
        var schemaDoc = (DocumentNode?)rewriter.Rewrite(serviceConfiguration, new(context));
        var configuration = FusionGraphConfiguration.Load(serviceConfiguration);

        if (schemaDoc is null)
        {
            // todo : exception.
            throw new InvalidOperationException(
                "A valid service configuration must always produce a schema document.");
        }

        return services
            .AddGraphQLServer()
            .AddDocument(schemaDoc)
            .UseField(next => next)
            .UseDefaultGatewayPipeline()
            .AddOperationCompilerOptimizer<OperationQueryPlanCompiler>()
            .ConfigureSchemaServices(
                sc =>
                {
                    sc.AddSingleton<GraphQLClientFactory>(
                        sp =>
                        {
                            var clientFactory = sp.GetApplicationService<IHttpClientFactory>();
                            var map = new Dictionary<string, Func<IGraphQLClient>>();

                            IGraphQLClient Create(string subgraphName)
                                => new HttpGraphQLClient(
                                    subgraphName,
                                    clientFactory.CreateClient(subgraphName));

                            foreach (var subgraphName in configuration.SubgraphNames)
                            {
                                map.Add(subgraphName, () => Create(subgraphName));
                            }

                            return new GraphQLClientFactory(map);
                        });

                    sc.TryAddSingleton(configuration);
                    sc.TryAddSingleton<RequestPlanner>();
                    sc.TryAddSingleton<RequirementsPlanner>();
                    sc.TryAddSingleton<ExecutionPlanBuilder>();
                    sc.TryAddSingleton<FederatedQueryExecutor>();
                });
    }

    private static IRequestExecutorBuilder UseDefaultGatewayPipeline(
        this IRequestExecutorBuilder builder)
    {
        return builder
            .UseInstrumentation()
            .UseExceptions()
            .UseTimeout()
            .UseDocumentCache()
            .UseDocumentParser()
            .UseDocumentValidation()
            .UseOperationCache()
            .UseOperationComplexityAnalyzer()
            .UseOperationResolver()
            .UseOperationVariableCoercion()
            .UseRequest<OperationExecutionMiddleware>();
    }
}
