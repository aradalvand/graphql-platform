using System.Text.Json;
using HotChocolate.Execution.Processing;
using HotChocolate.Fusion.Clients;
using HotChocolate.Fusion.Execution;
using HotChocolate.Language;
using static HotChocolate.Fusion.Execution.ExecutorUtils;
using GraphQLRequest = HotChocolate.Fusion.Clients.GraphQLRequest;

namespace HotChocolate.Fusion.Planning;

internal sealed class ResolverNode : QueryPlanNode
{
    private readonly IReadOnlyList<string> _path;

    public ResolverNode(
        int id,
        string subgraphName,
        DocumentNode document,
        ISelectionSet selectionSet,
        IReadOnlyList<string> requires,
        IReadOnlyList<string> path)
        : base(id)
    {
        SubgraphName = subgraphName;
        Document = document;
        SelectionSet = selectionSet;
        Requires = requires;
        _path = path;
    }

    public override QueryPlanNodeKind Kind => QueryPlanNodeKind.Resolver;

    /// <summary>
    /// Gets the schema name on which this request handler executes.
    /// </summary>
    public string SubgraphName { get; }

    /// <summary>
    /// Gets the GraphQL request document.
    /// </summary>
    public DocumentNode Document { get; }

    /// <summary>
    /// Gets the selection set for which this request provides a patch.
    /// </summary>
    public ISelectionSet SelectionSet { get; }

    /// <summary>
    /// Gets the variables that this request handler requires to create a request.
    /// </summary>
    public IReadOnlyList<string> Requires { get; }

    protected override async Task OnExecuteAsync(
        IFederationContext context,
        IExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.TryGetState(SelectionSet, out var workItems))
        {
            var schemaName = SubgraphName;
            var requests = new GraphQLRequest[workItems.Count];
            var selections = workItems[0].SelectionSet.Selections;

            // first we will create a request for all of our work items.
            for (var i = 0; i < workItems.Count; i++)
            {
                var workItem = workItems[i];
                ExtractPartialResult(workItem);
                requests[i] = CreateRequest(workItem.VariableValues);
            }

            // once we have the requests, we will enqueue them for execution with the execution engine.
            // the execution engine will batch these requests if possible.
            var responses = await context.ExecuteAsync(
                schemaName,
                requests,
                cancellationToken)
                .ConfigureAwait(false);

            // before we extract the data from the responses we will enqueue the responses for cleanup
            // so that the memory can be released at the end of the execution.
            // Since we are not fully deserializing the responses we cannot release the memory here
            // but need to wait until the transport layer is finished and disposes the result.
            context.Result.RegisterForCleanup(
                responses,
                r =>
                {
                    for (var i = 0; i < r.Count; i++)
                    {
                        r[i].Dispose();
                    }
                    return default!;
                });

            for (var i = 0; i < requests.Length; i++)
            {
                var response = responses[i];
                var data = UnwrapResult(response);
                var workItem = workItems[i];
                var selectionResults = workItem.SelectionResults;
                var exportKeys = workItem.ExportKeys;
                var variableValues = workItem.VariableValues;

                // we extract the selection data from the request and add it to the workItem results.
                ExtractSelectionResults(selections, schemaName, data, selectionResults);

                // next we need to extract any variables that we need for followup requests.
                ExtractVariables(data, exportKeys, variableValues);
            }
        }
    }

    protected override async Task OnExecuteNodesAsync(
        IFederationContext context,
        IExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.ContainsState(SelectionSet))
        {
            await base.OnExecuteNodesAsync(context, state, cancellationToken).ConfigureAwait(false);
        }
    }

    private GraphQLRequest CreateRequest(IReadOnlyDictionary<string, IValueNode> variableValues)
    {
        ObjectValueNode? vars = null;

        if (Requires.Count > 0)
        {
            var fields = new List<ObjectFieldNode>();

            foreach (var requirement in Requires)
            {
                if (variableValues.TryGetValue(requirement, out var value))
                {
                    fields.Add(new ObjectFieldNode(requirement, value));
                }
                else
                {
                    // TODO : error helper
                    throw new ArgumentException(
                        $"The variable value `{requirement}` was not provided " +
                        "but is required.",
                        nameof(variableValues));
                }
            }

            vars ??= new ObjectValueNode(fields);
        }

        return new GraphQLRequest(SubgraphName, Document, vars, null);
    }

    private JsonElement UnwrapResult(GraphQLResponse response)
    {
        if (_path.Count == 0)
        {
            return response.Data;
        }

        if (response.Data.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            var current = response.Data;

            for (var i = 0; i < _path.Count; i++)
            {
                if (current.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                {
                    return current;
                }

                current.TryGetProperty(_path[i], out var propertyValue);
                current = propertyValue;
            }

            return current;
        }

        return response.Data;
    }

    protected override void FormatProperties(Utf8JsonWriter writer)
    {
        writer.WriteString("schemaName", SubgraphName);
        writer.WriteString("document", Document.ToString(false));
        writer.WriteNumber("selectionSetId", SelectionSet.Id);

        if (_path.Count > 0)
        {
            writer.WritePropertyName("path");
            writer.WriteStartArray();

            foreach (var path in _path)
            {
                writer.WriteStringValue(path);
            }
            writer.WriteEndArray();
        }

        if (Requires.Count > 0)
        {
            writer.WritePropertyName("requires");
            writer.WriteStartArray();

            foreach (var requirement in Requires)
            {
                writer.WriteStartObject();
                writer.WriteString("variable", requirement);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}
