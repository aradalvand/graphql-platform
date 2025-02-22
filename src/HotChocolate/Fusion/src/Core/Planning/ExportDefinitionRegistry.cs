using System.Diagnostics.CodeAnalysis;
using HotChocolate.Execution.Processing;
using HotChocolate.Fusion.Metadata;
using HotChocolate.Language;

namespace HotChocolate.Fusion.Planning;

internal sealed class ExportDefinitionRegistry
{
    private readonly Dictionary<(ISelectionSet, string), string> _stateKeyLookup = new();
    private readonly Dictionary<string, ExportDefinition> _exportLookup = new(StringComparer.Ordinal);
    private readonly List<ExportDefinition> _exports = new();
    private readonly string _groupKey = "_fusion_exports_";
    private int _stateId;

    public IReadOnlyCollection<ExportDefinition> All => _exportLookup.Values;

    public string Register(
        ISelectionSet selectionSet,
        FieldVariableDefinition variableDefinition,
        IExecutionStep providingExecutionStep)
    {
        var exportDefinition = new ExportDefinition(
            $"_{_groupKey}_{++_stateId}",
            selectionSet,
            variableDefinition,
            providingExecutionStep);
        _exportLookup.Add(exportDefinition.StateKey, exportDefinition);
        _stateKeyLookup.Add((selectionSet, variableDefinition.Name), exportDefinition.StateKey);
        _exports.Add(exportDefinition);
        return exportDefinition.StateKey;
    }

    public void RegisterAdditionExport(
        FieldVariableDefinition variableDefinition,
        IExecutionStep providingExecutionStep,
        string stateKey)
    {
        var originalExport = _exportLookup[stateKey];
        var exportDefinition = new ExportDefinition(
            stateKey,
            originalExport.SelectionSet,
            variableDefinition,
            providingExecutionStep);
        _exports.Add(exportDefinition);
    }

    public bool TryGetStateKey(
        ISelectionSet selectionSet,
        string variableName,
        [NotNullWhen(true)] out string? stateKey,
        [NotNullWhen(true)] out IExecutionStep? executionStep)
    {
        if (_stateKeyLookup.TryGetValue((selectionSet, variableName), out stateKey))
        {
            executionStep = _exportLookup[stateKey].ExecutionStep;
            return true;
        }

        stateKey = null;
        executionStep = null;
        return false;
    }

    public IReadOnlyList<VariableDefinitionNode> CreateVariableDefinitions(
        IReadOnlyCollection<string> stateKeys,
        IReadOnlyDictionary<string, ITypeNode>? argumentTypes)
    {
        if (stateKeys.Count == 0 || argumentTypes is null)
        {
            return Array.Empty<VariableDefinitionNode>();
        }

        var definitions = new VariableDefinitionNode[stateKeys.Count];
        var index = 0;

        foreach (var stateKey in stateKeys)
        {
            var variableDefinition = _exportLookup[stateKey].VariableDefinition;
            definitions[index++] = new VariableDefinitionNode(
                null,
                new VariableNode(stateKey),
                argumentTypes[variableDefinition.Name],
                null,
                Array.Empty<DirectiveNode>());
        }

        return definitions;
    }

    public IEnumerable<ISelectionNode> GetExportSelections(
        IExecutionStep executionStep,
        ISelectionSet selectionSet)
    {
        foreach (var exportDefinition in _exports)
        {
            if (ReferenceEquals(exportDefinition.ExecutionStep, executionStep) &&
                ReferenceEquals(exportDefinition.SelectionSet, selectionSet))
            {
                // TODO : we need to transform this for better selection during execution
                var selection = exportDefinition.VariableDefinition.Select;
                var stateKey = exportDefinition.StateKey;
                yield return selection.WithAlias(new NameNode(stateKey));
            }
        }
    }

}
