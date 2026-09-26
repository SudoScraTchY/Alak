// alak-pack CLI (spec §8.5, PCK-009): validate | test | coverage | lint | bundle | new
using Alak.Packs.Tool;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length == 0) { PrintUsage(); return 2; }

var cmd = args[0].ToLowerInvariant();
var rest = args.Skip(1).ToArray();
try
{
    return cmd switch
    {
        "validate" => CmdValidate(rest),
        "test" => CmdTest(rest),
        "coverage" => CmdCoverage(rest),
        "lint" => CmdLint(rest),
        "bundle" => CmdBundle(rest),
        "new" => CmdNew(rest),
        _ => PrintUsage(),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int CmdValidate(string[] a)
{
    var root = a.FirstOrDefault() ?? "packs/en";
    var pack = Pack.Load(root);
    var v = new PackValidator();
    var ok = v.Validate(pack);
    foreach (var e in v.Errors) Console.Error.WriteLine($"  ✗ {e}");
    foreach (var w in v.Warnings) Console.WriteLine($"  ⚠ {w}");
    Console.WriteLine(ok
        ? $"✓ {pack.Manifest.Id} v{pack.Manifest.Version}: valid ({pack.Concepts.Count} concepts)"
        : $"✗ {root}: {v.Errors.Count} error(s)");
    return ok ? 0 : 1;
}

static int CmdTest(string[] a)
{
    var root = a.FirstOrDefault() ?? "packs/en";
    var pack = Pack.Load(root);
    var testsDir = Path.Combine(root, "tests");
    if (!Directory.Exists(testsDir)) { Console.WriteLine($"no tests in {root}/tests"); return 0; }
    var pass = 0; var fail = 0;
    foreach (var file in Directory.EnumerateFiles(testsDir, "*.json").OrderBy(f => f))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var testCase in doc.RootElement.EnumerateArray())
        {
            var input = testCase.GetProperty("input").GetString()!;
            var expectConcept = testCase.GetProperty("expect").GetString()!;
            var folded = Fold(input);
            var hit = pack.Concepts.TryGetValue(expectConcept, out var c)
                && c.Flatten().Any(t => folded.Contains(Fold(t.Term)));
            if (hit) { pass++; Console.WriteLine($"  ✓ {Path.GetFileName(file)}: '{Trunc(input)}' → {expectConcept}"); }
            else { fail++; Console.Error.WriteLine($"  ✗ {Path.GetFileName(file)}: '{Trunc(input)}' expected {expectConcept}"); }
        }
    }
    Console.WriteLine($"{pass} passed, {fail} failed");
    return fail == 0 ? 0 : 1;
}

static int CmdCoverage(string[] a)
{
    var root = a.FirstOrDefault() ?? "packs/en";
    var pack = Pack.Load(root);
    Console.WriteLine($"coverage for {pack.Manifest.Id} (PCK-006: ≥5 terms = complete)");
    var incomplete = 0;
    foreach (var (cid, c) in pack.Concepts.OrderBy(kv => kv.Key))
    {
        var n = c.Flatten().Count();
        if (n < 5) { incomplete++; Console.WriteLine($"  ✗ {cid,-32} {n} terms"); }
        else Console.WriteLine($"  ✓ {cid,-32} {n} terms");
    }
    Console.WriteLine(incomplete == 0 ? "all concepts complete" : $"{incomplete} concept(s) incomplete");
    return 0;
}

static int CmdLint(string[] a)
{
    var root = a.FirstOrDefault() ?? "packs/en";
    var pack = Pack.Load(root);
    var issues = 0;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var (cid, c) in pack.Concepts)
        foreach (var (term, _) in c.Flatten())
            if (!seen.Add($"{cid}\0{term}"))
            { Console.Error.WriteLine($"  ✗ duplicate term '{term}' in {cid}"); issues++; }
    foreach (var (cid, c) in pack.Concepts)
        foreach (var (term, _) in c.Flatten())
        {
            if (term != term.Trim()) { Console.Error.WriteLine($"  ✗ '{term}' ({cid}) has stray whitespace"); issues++; }
            if (term.Contains("~~")) { Console.Error.WriteLine($"  ✗ '{term}' ({cid}) has doubled '~'"); issues++; }
        }
    Console.WriteLine(issues == 0 ? $"✓ {pack.Manifest.Id}: clean" : $"{issues} lint issue(s)");
    return issues == 0 ? 0 : 1;
}

static int CmdBundle(string[] a)
{
    var root = a.FirstOrDefault() ?? "packs/en";
    var pack = Pack.Load(root);
    var outFile = a.Skip(1).FirstOrDefault() ?? $"{pack.Manifest.Id}-{pack.Manifest.Version}.alakpack";
    if (File.Exists(outFile)) File.Delete(outFile);
    using (var fs = File.Create(outFile))
    using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var entry = zip.CreateEntry(Path.GetRelativePath(root, file).Replace('\\', '/'));
            using var es = entry.Open();
            using var src = File.OpenRead(file);
            src.CopyTo(es);
        }
    }
    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outFile)));
    Console.WriteLine($"✓ {outFile} ({new FileInfo(outFile).Length} bytes)");
    Console.WriteLine($"  sha256: {hash.ToLowerInvariant()}");
    return 0;
}

static int CmdNew(string[] a)
{
    if (a.Length == 0) { Console.Error.WriteLine("usage: alak-pack new <id>"); return 2; }
    var id = a[0];
    var root = Path.Combine("packs", id);
    if (Directory.Exists(root)) { Console.Error.WriteLine($"packs/{id} already exists"); return 1; }
    Directory.CreateDirectory(Path.Combine(root, "tests"));
    File.WriteAllText(Path.Combine(root, "pack.json"), $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "{{id}}",
          "nativeName": "{{id}}",
          "version": "0.1.0",
          "scripts": [],
          "direction": "ltr",
          "calendars": ["gregorian"],
          "dependsOn": []
        }
        """);
    File.WriteAllText(Path.Combine(root, "lexicon.json"), "{\n  \"concepts\": {}\n}\n");
    Console.WriteLine($"✓ scaffolded packs/{id} (edit lexicon.json, then: alak-pack validate packs/{id})");
    return 0;
}

static string Fold(string s)
{
    var sb = new System.Text.StringBuilder();
    foreach (var ch in s.ToLowerInvariant())
    {
        if (char.IsWhiteSpace(ch)) continue;
        sb.Append(ch);
    }
    return sb.ToString();
}

static string Trunc(string s) => s.Length <= 40 ? s : s[..37] + "...";

static int PrintUsage()
{
    Console.WriteLine("""
        alak-pack — language pack toolchain (spec §8.5, PCK-009)

        usage:
          alak-pack validate <packDir>    schema + safety validation (PCK-001, PCK-004)
          alak-pack test <packDir>        run the pack's own tests/ cases
          alak-pack coverage <packDir>    per-concept term counts (PCK-006)
          alak-pack lint <packDir>        duplicates, style, conflicts
          alak-pack bundle <packDir> [out]  → .alakpack + SHA-256
          alak-pack new <id>              scaffold a new pack
        """);
    return 2;
}
