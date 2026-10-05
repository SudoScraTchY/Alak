using Alak.Core.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Adaptive;

/// <summary>
/// Adaptive learning engine interface (spec §10).
/// </summary>
public interface IAdaptiveEngine
{
    Task LearnFromDismissalAsync(string package, string category, IReadOnlyList<string> features);
    Task RecordDismissAsync(string id, string category);
    Task<Dictionary<string, float>> GetWeightsAsync(string package);
    Task ResetAsync(string? package = null);
}