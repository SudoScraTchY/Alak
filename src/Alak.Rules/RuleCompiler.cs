using Alak.Rules;
using Alak.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Rules;

/// <summary>
/// Interface for loading language packs (spec §8).
/// </summary>
public interface IPackLoader
{
    Task<IReadOnlyList<LanguagePack>> LoadAsync();
}

/// <summary>
/// Interface for compiling rules (spec §9).
/// </summary>
public interface IRuleCompiler
{
    Task<PackSet> CompileAsync(IReadOnlyList<LanguagePack> packs);
}

/// <summary>
/// Pack loader implementation (spec §8).
/// </summary>
public sealed class PackLoader : IPackLoader
{
    public async Task<IReadOnlyList<LanguagePack>> LoadAsync()
    {
        return PackSet.BuiltInDefault.Packs;
    }
}

/// <summary>
/// Rule compiler implementation (spec §9).
/// </summary>
public sealed class RuleCompiler : IRuleCompiler
{
    public async Task<PackSet> CompileAsync(IReadOnlyList<LanguagePack> packs)
    {
        return PackSet.Compile(packs);
    }
}