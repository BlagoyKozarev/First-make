using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Core.Engine.Services;

/// <summary>
/// Unit normalization using aliases from units.yaml
/// </summary>
public class UnitNormalizer
{
    private readonly Dictionary<string, string> _variantToCanonical = new(StringComparer.OrdinalIgnoreCase);

    public UnitNormalizer(string yamlPath)
    {
        LoadAliases(yamlPath);
    }

    private void LoadAliases(string yamlPath)
    {
        if (!File.Exists(yamlPath))
            throw new FileNotFoundException($"units.yaml not found at {yamlPath}");

        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<UnitsConfig>(yaml);

        if (config?.Aliases == null)
            return;

        // Build variant -> canonical mapping
        foreach (var alias in config.Aliases)
        {
            // Canonical maps to itself
            _variantToCanonical[alias.Canonical] = alias.Canonical;

            // Each variant maps to canonical
            if (alias.Variants != null)
            {
                foreach (var variant in alias.Variants)
                {
                    _variantToCanonical[variant] = alias.Canonical;
                }
            }
        }
    }

    /// <summary>
    /// Normalize unit to canonical form
    /// </summary>
    public string Normalize(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return string.Empty;

        var trimmed = unit.Trim();

        // Direct lookup (case-insensitive)
        if (_variantToCanonical.TryGetValue(trimmed, out var canonical))
            return canonical;

        // No match - return as-is (lowercase, trimmed)
        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Check if two units are equivalent
    /// </summary>
    public bool AreEquivalent(string unit1, string unit2)
    {
        var norm1 = Normalize(unit1);
        var norm2 = Normalize(unit2);
        return string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase);
    }
}

// YAML deserialization models
internal class UnitsConfig
{
    public List<UnitAlias>? Aliases { get; set; }
}

internal class UnitAlias
{
    public string Canonical { get; set; } = string.Empty;
    public List<string>? Variants { get; set; }
}
