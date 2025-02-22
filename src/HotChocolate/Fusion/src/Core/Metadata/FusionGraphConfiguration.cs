using HotChocolate.Language;
using HotChocolate.Utilities;

namespace HotChocolate.Fusion.Metadata;

internal sealed class FusionGraphConfiguration
{
    private readonly Dictionary<string, IType> _types;
    private readonly Dictionary<(string Schema, string Type), string> _typeNameLookup = new();

    public FusionGraphConfiguration(
        IReadOnlyList<IType> types,
        IReadOnlyList<HttpClientConfig> httpClientConfigs)
    {
        _types = types.ToDictionary(t => t.Name, StringComparer.Ordinal);
        HttpClientConfigs = httpClientConfigs;
        SubgraphNames = httpClientConfigs.Select(t => t.Subgraph).ToArray();

        foreach (var type in types)
        {
            foreach (var binding in type.Bindings)
            {
                if (!binding.Name.EqualsOrdinal(type.Name))
                {
                    _typeNameLookup.Add((binding.SchemaName, binding.Name), type.Name);
                }
            }
        }
    }

    public IReadOnlyList<string> SubgraphNames { get; }

    public IReadOnlyList<HttpClientConfig> HttpClientConfigs { get; }

    public T GetType<T>(string typeName) where T : IType
    {
        if (_types.TryGetValue(typeName, out var type) && type is T casted)
        {
            return casted;
        }

        throw new InvalidOperationException("Type not found.");
    }

    public T GetType<T>(string schemaName, string typeName) where T : IType
    {
        if (!_typeNameLookup.TryGetValue((schemaName, typeName), out var temp))
        {
            temp = typeName;
        }

        if (_types.TryGetValue(temp, out var type) && type is T casted)
        {
            return casted;
        }

        throw new InvalidOperationException("Type not found.");
    }

    public T GetType<T>(TypeInfo typeInfo) where T : IType
    {
        throw new NotImplementedException();
    }

    public string GetTypeName(string schemaName, string typeName)
    {
        if (!_typeNameLookup.TryGetValue((schemaName, typeName), out var temp))
        {
            temp = typeName;
        }

        return temp;
    }

    public string GetTypeName(TypeInfo typeInfo)
    {
        throw new NotImplementedException();
    }

    public static FusionGraphConfiguration Load(string sourceText)
        => new FusionGraphConfigurationReader().Read(sourceText);

    public static FusionGraphConfiguration Load(DocumentNode document)
        => new FusionGraphConfigurationReader().Read(document);
}

public readonly struct TypeInfo
{
    public TypeInfo(string schemaName, string typeName)
    {
        SchemaName = schemaName;
        TypeName = typeName;
    }

    public string SchemaName { get; }

    public string TypeName { get; }
}
