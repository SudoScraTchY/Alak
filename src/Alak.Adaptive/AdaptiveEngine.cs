using Alak.Core.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Adaptive;

/// <summary>
/// Adaptive learning engine implementation (spec §10).
/// Learns from user dismissals to improve per-package classification.
/// </summary>
public sealed class AdaptiveEngine : IAdaptiveEngine
{
    private readonly Dictionary<string, Dictionary<string, float>> _weights = new();

    public AdaptiveEngine()
    {
    }

    public async Task LearnFromDismissalAsync(string package, string category, IReadOnlyList<string> features)
    {
        if (!_weights.ContainsKey(package))
        {
            _weights[package] = new Dictionary<string, float>();
        }

        foreach (var feature in features)
        {
            var key = $"{category}:{feature}";
            if (!_weights[package].ContainsKey(key))
            {
                _weights[package][key] = 0f;
            }
            _weights[package][key] += 0.1f; // Increment weight
        }

        // TODO: Persist to storage
        await Task.CompletedTask;
    }

    public async Task<Dictionary<string, float>> GetWeightsAsync(string package)
    {
        if (_weights.TryGetValue(package, out var weights))
        {
            return new Dictionary<string, float>(weights);
        }
        return new Dictionary<string, float>();
    }

    public async Task ResetAsync(string? package = null)
    {
        if (package != null)
        {
            _weights.Remove(package);
        }
        else
        {
            _weights.Clear();
        }
        await Task.CompletedTask;
    }

    public async Task RecordDismissAsync(string id, string category)
    {
        // Basic implementation - could be enhanced to learn from dismissals
        await Task.CompletedTask;
    }
}