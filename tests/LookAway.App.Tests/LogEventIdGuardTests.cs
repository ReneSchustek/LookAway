using System.Reflection;
using LookAway.App.ViewModels;
using LookAway.Core.Entities;
using LookAway.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace LookAway.App.Tests;

/// <summary>
/// Jede Ereignisnummer im Protokoll gehört genau einer Meldung.
/// </summary>
/// <remarks>
/// Wer nach einer Nummer filtert, will genau eine Art Ereignis sehen. Zwei Meldungen unter
/// derselben Nummer mischen sich in jeder Auswertung, und weil Source-Generator und Übersetzer
/// das nicht bemängeln, fällt es erst beim Lesen eines Protokolls auf.
/// </remarks>
public sealed class LogEventIdGuardTests
{
    [Fact]
    public void EveryEventId_IsUsedOnce()
    {
        List<(string Method, int EventId)> messages = [.. LoggerMessages()];

        Assert.NotEmpty(messages);
        Assert.True(
            Duplicates(messages).Count == 0,
            "Doppelt vergebene Ereignisnummern: " + string.Join("; ", Duplicates(messages)));
    }

    // Gegenprobe: Die Auswertung muss eine Dublette auch finden, sonst stünde der Wächter blind grün.
    [Fact]
    public void Duplicates_FindsASharedNumber()
    {
        (string, int)[] messages = [("A.One", 1300), ("B.Two", 1300), ("C.Three", 1301)];

        Assert.Equal(["1300: A.One, B.Two"], Duplicates(messages));
    }

    private static List<string> Duplicates(IEnumerable<(string Method, int EventId)> messages)
        => [.. messages
            .GroupBy(message => message.EventId)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(m => m.Method).Order(StringComparer.Ordinal))}")];

    private static IEnumerable<(string Method, int EventId)> LoggerMessages()
    {
        Assembly[] products =
        [
            typeof(SettingsViewModel).Assembly,
            typeof(Settings).Assembly,
            typeof(JsonSettingsRepository).Assembly,
        ];

        foreach (Type type in products.SelectMany(LoadableTypes))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<LoggerMessageAttribute>() is { } attribute)
                {
                    yield return ($"{type.Name}.{method.Name}", attribute.EventId);
                }
            }
        }
    }

    // Typen, die sich ohne WinUI-Laufzeit nicht laden lassen, enthalten keine Protokollmeldungen;
    // die übrigen reichen für die Prüfung.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
