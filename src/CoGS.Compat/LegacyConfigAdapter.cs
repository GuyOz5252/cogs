using CoGS.Core;
using Newtonsoft.Json.Linq;

namespace CoGS.Compat;

/// <summary>
/// Reads a legacy Newtonsoft-based pipeline JSON configuration and produces
/// <see cref="PipelineDefinition"/> instances compatible with the modern CoGS runtime.
/// <para>
/// This adapter exists solely for migration. New services should use the
/// fluent <see cref="PipelinesBuilder"/> DSL instead.
/// </para>
/// </summary>
public static class LegacyConfigAdapter
{
    /// <summary>
    /// Parses a legacy JSON string into pipeline definitions.
    /// </summary>
    /// <param name="json">Raw JSON string in the old config format.</param>
    /// <param name="typeResolver">
    /// Resolves a <c>$type</c> string to a CLR <see cref="Type"/>. Pass your own
    /// resolver that knows about the component types in your assembly.
    /// </param>
    /// <returns>A list of pipeline definitions derived from the legacy config.</returns>
    public static IReadOnlyList<PipelineDefinition> FromLegacyJson(
        string json,
        Func<string, Type?> typeResolver)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(typeResolver);

        var root = JObject.Parse(json);
        var serverConfig = root["ServerConfiguration"];

        if (serverConfig is null)
        {
            return Array.Empty<PipelineDefinition>();
        }

        var components = new List<ComponentDefinition>();
        var subscriptions = new List<SubscriptionDefinition>();

        var componentsArray = serverConfig["Components"]?["$values"];
        if (componentsArray is null)
        {
            return Array.Empty<PipelineDefinition>();
        }

        foreach (var compToken in componentsArray)
        {
            var factory = compToken["ComponentFactory"];
            if (factory is null)
            {
                continue;
            }

            var typeName = factory["$type"]?.ToString();
            var componentName = factory["ComponentName"]?.ToString();

            if (typeName is null || componentName is null)
            {
                continue;
            }

            var resolvedType = typeResolver(typeName);
            if (resolvedType is null)
            {
                Console.WriteLine($"[CoGS.Compat] WARNING: Could not resolve type '{typeName}'. Skipping component '{componentName}'.");
                continue;
            }

            components.Add(new ComponentDefinition(
                componentName,
                resolvedType,
                optionsKey: null,
                ComponentRole.Unknown));

            var subsArray = compToken["Subscriptions"]?["$values"];
            if (subsArray is null)
            {
                continue;
            }

            foreach (var subToken in subsArray)
            {
                var publisherName = subToken["ComponentName"]?.ToString();
                if (publisherName is not null)
                {
                    subscriptions.Add(new SubscriptionDefinition(
                        subscriberComponentName: componentName,
                        publisherComponentName: publisherName));
                }
            }
        }

        var pipeline = new PipelineDefinition("Legacy", components, subscriptions);
        return [pipeline];
    }
}
