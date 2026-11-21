using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;

namespace LookAway.Benchmarks;

/// <summary>
/// Einstieg des Messprojekts. Gestartet über <c>tools/run-benchmarks.ps1</c>, das die Berichte je
/// Lauf ablegt; die Befehlszeile geht unverändert an BenchmarkDotNet (etwa <c>--filter</c>).
/// </summary>
internal static class Program
{
    /// <summary>Führt die ausgewählten Messungen aus.</summary>
    /// <param name="args">Befehlszeile für BenchmarkDotNet.</param>
    public static void Main(string[] args)
    {
        // Standard-Werkzeugkette: Jede Messklasse läuft in einem eigenen Prozess, damit sich Speicher
        // und JIT der Messungen nicht gegenseitig beeinflussen. Zusätzlich zur Konsole entsteht ein
        // Markdown-Bericht je Messklasse.
        IConfig config = DefaultConfig.Instance.AddExporter(MarkdownExporter.GitHub);

        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
