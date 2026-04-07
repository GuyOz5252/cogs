using System.Reflection;
using CoGS.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoGS.Server.Configuration;

internal sealed class CogsTypeServiceRegistrar
{
    private readonly IReadOnlyDictionary<string, Type> _typeMap;

    public CogsTypeServiceRegistrar()
    {
        _typeMap = ScanCogsTypes();
    }

    public void RegisterRuleTree(IServiceCollection services, IConfigurationSection section)
    {
        var typeName = section["Type"]
                       ?? throw new InvalidOperationException(
                           $"Configuration section '{section.Path}' must have a 'Type' property.");

        if (!_typeMap.TryGetValue(typeName, out var clrType))
        {
            throw new InvalidOperationException(
                $"Unknown [CogsType] '{typeName}'. Registered types: [{string.Join(", ", _typeMap.Keys)}].");
        }

        var singleChildren = new List<string>();
        var collectionChildren = new List<string>();

        foreach (var child in section.GetChildren())
        {
            if (string.Equals(child.Key, "Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (child["Type"] is not null)
            {
                RegisterRuleTree(services, child);
                singleChildren.Add(child.Path);
            }
            else if (child.GetChildren().Any(c => c["Type"] is not null))
            {
                foreach (var element in child.GetChildren())
                {
                    RegisterRuleTree(services, element);
                }
                collectionChildren.Add(child.Path);
            }
        }

        var capturedType = clrType;
        var capturedPath = section.Path;
        var capturedSingles = singleChildren.ToArray();
        var capturedCollections = collectionChildren.ToArray();

        services.AddKeyedSingleton<IRule>(capturedPath, (sp, _) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var thisSection = config.GetSection(capturedPath);
            var childArgs = new List<object>();

            foreach (var singlePath in capturedSingles)
            {
                childArgs.Add(sp.GetRequiredKeyedService<IRule>(singlePath));
            }

            foreach (var collectionPath in capturedCollections)
            {
                var items = config.GetSection(collectionPath)
                    .GetChildren()
                    .Select(c => sp.GetRequiredKeyedService<IRule>(c.Path))
                    .ToList();
                childArgs.Add(items);
            }

            var instance = ActivatorUtilities.CreateInstance(sp, capturedType, childArgs.ToArray());
            thisSection.Bind(instance);
            return (IRule)instance;
        });
    }

    private static Dictionary<string, Type> ScanCogsTypes()
    {
        var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] exportedTypes;
            try
            {
                exportedTypes = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (var type in exportedTypes)
            {
                if (type is { IsAbstract: false, IsClass: true })
                {
                    var attribute = type.GetCustomAttribute<CogsTypeAttribute>();
                    if (attribute is not null)
                    {
                        types[attribute.Name] = type;
                    }
                }
            }
        }

        return types;
    }
}
