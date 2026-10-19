using System.Reflection;
using Wanetra.Application.Lan;

namespace Wanetra.Infrastructure.Lan;

// The IEEE MA-L registry (scripts/update-oui.ts) compiled into the assembly, loaded on first use.
public sealed class EmbeddedOuiDatabase : IOuiLookup
{
    private const string ResourceName = "Wanetra.Infrastructure.Lan.oui.txt";

    private readonly Lazy<Dictionary<string, string>> vendors = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public int Count => vendors.Value.Count;

    public string? Lookup(string mac)
    {
        var normalized = MacAddress.Normalize(mac);
        return normalized is not null && vendors.Value.TryGetValue(MacAddress.Prefix(normalized), out var vendor) ? vendor : null;
    }

    private static Dictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        var vendors = new Dictionary<string, string>(45_000, StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (tab == 6)
            {
                vendors[line[..6]] = line[(tab + 1)..];
            }
        }

        return vendors;
    }
}
