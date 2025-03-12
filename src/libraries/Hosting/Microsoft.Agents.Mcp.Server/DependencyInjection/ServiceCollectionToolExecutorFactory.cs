using Microsoft.Agents.Mcp.Server.Methods.Tools;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsList;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;


#if (!NET9_0_OR_GREATER)
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
#endif

namespace Microsoft.Agents.Mcp.Server.DependencyInjection;

public class ServiceCollectionToolExecutorFactory : IOperationExecutorFactory
{
    internal static string DefaultToolKey = nameof(DefaultToolKey);

    private ImmutableDictionary<string, IMcpToolExecutor> _dictionary;
    private ImmutableArray<ToolDefinition> _definitions;

    public ServiceCollectionToolExecutorFactory(IServiceProvider serviceProvider)
    {
        var services = serviceProvider.GetKeyedServices<IMcpToolExecutor>(DefaultToolKey);
        _dictionary = services.ToImmutableDictionary(t => t.Id, t => t);
        _definitions = services.Select(d => new ToolDefinition()
        {
            Name = d.Id,
            Description = d.Description,
            InputSchema = GetSchemaType(d.InputType)
        }).ToImmutableArray();
    }

    private JsonNode GetSchemaType(Type inputType)
    {
#if (NET9_0_OR_GREATER)
        return JsonSerializerOptions.Default.GetJsonSchemaAsNode(inputType, exporterOptions)
#else
        JSchema schema = _generator.Generate(inputType);
        return JsonNode.Parse(schema.ToString()) ?? throw new ArgumentException("Invalid tool input type");
#endif
    }

    public IMcpToolExecutor GetExecutor(string name) => _dictionary[name];

    public ImmutableArray<ToolDefinition> GetDefinitions() => _definitions;

#if (NET9_0_OR_GREATER)
    JsonSchemaExporterOptions exporterOptions = new()
    {
        TransformSchemaNode = (context, schema) =>
        {
            // Determine if a type or property and extract the relevant attribute provider.
            ICustomAttributeProvider? attributeProvider = context.PropertyInfo is not null
                ? context.PropertyInfo.AttributeProvider
                : context.TypeInfo.Type;

            // Look up any description attributes.
            DescriptionAttribute? descriptionAttr = attributeProvider?
                .GetCustomAttributes(inherit: true)
                .Select(attr => attr as DescriptionAttribute)
                .FirstOrDefault(attr => attr is not null);

            // Apply description attribute to the generated schema.
            if (descriptionAttr != null)
            {
                if (schema is not JsonObject jObj)
                {
                    // Handle the case where the schema is a Boolean.
                    JsonValueKind valueKind = schema.GetValueKind();
                    Debug.Assert(valueKind is JsonValueKind.True or JsonValueKind.False);
                    schema = jObj = new JsonObject();
                    if (valueKind is JsonValueKind.False)
                    {
                        jObj.Add("not", true);
                    }
                }

                jObj.Insert(0, "description", descriptionAttr.Description);
            }

            return schema;
        }
    };
#else
    private static readonly JSchemaGenerator _generator = new()
    {
        DefaultRequired = Newtonsoft.Json.Required.DisallowNull
    };
#endif
}