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
/// Pack loader implementation (spec §8). Loads all active packs (en, fa, fa-latn).
/// </summary>
public sealed class PackLoader : IPackLoader
{
    public Task<IReadOnlyList<LanguagePack>> LoadAsync()
    {
        var enPack = LoadPack("en", PackSet.BuiltInEn.Packs[0]);
        var faPack = LoadPack("fa", PackSet.BuiltInFaPack);
        var faLatnPack = LoadPack("fa-latn", new LanguagePack("fa-latn", new Dictionary<string, IReadOnlyList<(string, float)>>()));

        IReadOnlyList<LanguagePack> result = new List<LanguagePack> { enPack, faPack, faLatnPack };
        return Task.FromResult(result);
    }

    private static LanguagePack LoadPack(string id, LanguagePack fallback)
    {
        var asm = typeof(PackSet).Assembly;
        var resourceNames = new[] { $"packs.{id}.lexicon.json", $"Alak.Rules.packs.{id}.lexicon.json" };
        foreach (var r in resourceNames)
        {
            using var stream = asm.GetManifestResourceStream(r);
            if (stream is not null)
            {
                return PackSet.LoadPackFromStream(stream, id);
            }
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "packs", id, "lexicon.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "packs", id, "lexicon.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "packs", id, "lexicon.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "packs", id, "lexicon.json"),
        };

        foreach (var p in candidates)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    using var fs = File.OpenRead(full);
                    return PackSet.LoadPackFromStream(fs, id);
                }
            }
            catch { }
        }

        return fallback;
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