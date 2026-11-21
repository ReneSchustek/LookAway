using System.Text.RegularExpressions;

namespace LookAway.Core.Tests;

/// <summary>
/// Jeder Typ unter <c>src</c> und <c>tests</c> sagt in einem Kopf-Kommentar, wofür es ihn gibt.
/// </summary>
/// <remarks>
/// Für öffentliche Typen erzwingt das schon der Übersetzer: Die Projekte erzeugen eine
/// XML-Doku, und CS1591 ist wie jede Warnung ein Fehler. Interne und private Typen fallen dort
/// heraus, die Testprojekte erzeugen gar keine Doku, und gerade die App-Schicht besteht fast
/// nur aus internen Typen. Diese Prüfung schließt die Lücke.
/// <para>
/// Sie liest Zeilen statt eines Syntaxbaums. Ein Parser wäre genauer, brächte aber eine neue
/// Abhängigkeit für eine Frage, die der einheitlich formatierte Bestand auch so beantwortet:
/// Eine Typdeklaration beginnt am Zeilenanfang, Attribute stehen in eigenen Zeilen darüber.
/// Damit die Prüfung nicht blind grün steht, belegen die Schnipsel-Fälle unten, dass sie
/// fehlende Köpfe tatsächlich findet.
/// </para>
/// </remarks>
public sealed partial class TypeHeadCommentGuardTests
{
    private static readonly string[] SourceFolders = ["src", "tests"];

    [Fact]
    public void EveryTypeInSourceAndTests_HasAHead()
    {
        List<string> headless = [];
        foreach (string path in SourceFiles())
        {
            foreach (TypeDeclaration declaration in Analyze(File.ReadAllLines(path)).Where(d => !d.HasHead))
            {
                headless.Add($"{Relative(path)}:{declaration.Line} {declaration.Name}");
            }
        }

        Assert.True(
            headless.Count == 0,
            "Diese Typen stehen ohne Kopf da. Ein Typ ohne Kopf zwingt den nächsten Leser, aus dem Rumpf "
            + "zu erraten, wofür es ihn gibt:\n  " + string.Join("\n  ", headless));
    }

    // Gegen ein Raster, das nichts mehr trifft: Jede Datei im Bestand enthält mindestens einen
    // Typ. Findet die Prüfung in einer Datei keinen, ist ihr Muster kaputt, nicht die Datei.
    [Fact]
    public void TheGuardFindsATypeInEveryFile()
    {
        string[] files = [.. SourceFiles()];
        string[] blind = [.. files.Where(path => Analyze(File.ReadAllLines(path)).Count == 0).Select(Relative)];

        Assert.NotEmpty(files);
        Assert.True(blind.Length == 0, "Keine Typdeklaration erkannt in: " + string.Join(", ", blind));
    }

    [Theory]
    [InlineData("/// <summary>Zählt Pausen.</summary>\ninternal sealed class A\n{\n}", true)]
    [InlineData("// Zählt Pausen.\nprivate sealed record A(int B);", true)]
    [InlineData("/// <inheritdoc />\ninternal sealed class A\n{\n}", true)]
    [InlineData("/// <summary>Zählt Pausen.</summary>\n[Obsolete(\n    \"x\")]\n[Serializable]\ninternal sealed class A\n{\n}", true)]
    [InlineData("internal sealed class A\n{\n}", false)]
    [InlineData("/// <summary>Zählt Pausen.</summary>\n\ninternal sealed class A\n{\n}", false)]
    [InlineData("// ---------------------------------\ninternal sealed class A\n{\n}", false)]
    [InlineData("// int count = 0;\ninternal sealed class A\n{\n}", false)]
    [InlineData("/// <remarks>Ohne Zusammenfassung.</remarks>\ninternal sealed class A\n{\n}", false)]
    [InlineData("[Serializable]\ninternal readonly record struct A(int B);", false)]
    public void HasHead_RecognizesHeadsAndTheirAbsence(string source, bool expected)
    {
        ArgumentNullException.ThrowIfNull(source);
        Assert.Equal(expected, Analyze(source.Split('\n')).Single().HasHead);
    }

    [Fact]
    public void NestedTypes_AreCheckedToo()
    {
        string source = "/// <summary>Außen.</summary>\ninternal sealed class A\n{\n    private sealed class B\n    {\n    }\n}";

        List<TypeDeclaration> found = Analyze(source.Split('\n'));

        Assert.Equal(["A", "B"], found.Select(d => d.Name));
        Assert.False(found[1].HasHead);
    }

    private static List<TypeDeclaration> Analyze(string[] lines)
    {
        bool[] isAttribute = MarkAttributeLines(lines);
        List<TypeDeclaration> found = [];

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = TypeDeclarationLine().Match(lines[i]);
            if (match.Success)
            {
                found.Add(new TypeDeclaration(match.Groups["name"].Value, i + 1, HasHeadAbove(lines, isAttribute, i)));
            }
        }

        return found;
    }

    // Ein Attribut kann sich über mehrere Zeilen ziehen ([SuppressMessage(...)] mit Begründung).
    // Gezählt werden die eckigen Klammern außerhalb von Zeichenketten, bis die Klammer zu ist.
    private static bool[] MarkAttributeLines(string[] lines)
    {
        bool[] isAttribute = new bool[lines.Length];
        int depth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string code = StringLiteral().Replace(lines[i], string.Empty).Trim();
            if (depth == 0 && !code.StartsWith('['))
            {
                continue;
            }

            isAttribute[i] = true;
            depth += code.Count(c => c == '[') - code.Count(c => c == ']');
            depth = Math.Max(depth, 0);
        }

        return isAttribute;
    }

    // Der Kopf ist der zusammenhängende Kommentarblock direkt über der Deklaration, über die
    // Attribute hinweg. Eine Leerzeile dazwischen trennt ihn ab: Dann gehört er zu etwas anderem.
    private static bool HasHeadAbove(string[] lines, bool[] isAttribute, int declarationIndex)
    {
        int j = declarationIndex - 1;
        while (j >= 0 && isAttribute[j])
        {
            j--;
        }

        List<string> block = [];
        while (j >= 0 && lines[j].TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            block.Add(lines[j].Trim());
            j--;
        }

        if (block.Count == 0)
        {
            return false;
        }

        // Ein XML-Kopf braucht eine Zusammenfassung oder den Verweis auf die geerbte;
        // ein bloßes <remarks> ohne <summary> zeigt IntelliSense nicht an.
        if (block.Any(line => line.StartsWith("///", StringComparison.Ordinal)))
        {
            return block.Any(line => line.Contains("<summary>", StringComparison.Ordinal)
                || line.Contains("<inheritdoc", StringComparison.Ordinal));
        }

        return block.Any(IsProse);
    }

    // Eine Zeile, die etwas sagt: keine Trennlinie, kein auskommentierter Code, kein Merkposten.
    private static bool IsProse(string comment)
    {
        string text = comment.TrimStart('/').Trim();
        return text.Length > 0
            && !SeparatorLine().IsMatch(text)
            && !CommentedOutCode().IsMatch(text)
            && !text.StartsWith("TODO", StringComparison.Ordinal);
    }

    private static IEnumerable<string> SourceFiles()
    {
        string root = RepositoryRoot();
        return SourceFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    // Erzeugter Code (XAML-Partials, Source-Generatoren) liegt unter obj und schreibt niemand von Hand.
    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Relative(string path)
        => Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LookAway.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    [GeneratedRegex(@"^\s*(?:(?:public|internal|private|protected|file|sealed|static|abstract|partial|readonly|ref|unsafe|new)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex TypeDeclarationLine();

    [GeneratedRegex(@"""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"^[-=*#~_─═]{3,}")]
    private static partial Regex SeparatorLine();

    [GeneratedRegex(@"[;{}]\s*$|^(?:using|var|return|public|private|internal)\b")]
    private static partial Regex CommentedOutCode();

    // Eine gefundene Deklaration: Name, Zeile (ab 1) und ob ein Kopf darüber steht.
    private sealed record TypeDeclaration(string Name, int Line, bool HasHead);
}
