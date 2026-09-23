using System.Text.Json;
using System.Text.Json.Serialization;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Operations;

/// <summary>
/// Diario persistente de operaciones (JSONL): intención, elementos iniciados,
/// completados, fallidos, cancelados y estado incierto tras cierre abrupto.
/// Reiniciar nunca repite ciegamente: primero reconcilia el diario.
/// </summary>
public sealed class OperationJournal
{
    private readonly string _path;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public OperationJournal(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyFileExplorer", "operations-journal.jsonl");

    public sealed record Entry
    {
        public required DateTimeOffset TimestampUtc { get; init; }
        public required Guid OperationId { get; init; }
        public required string Stage { get; init; } // intent | item-started | item-done | item-failed | item-skipped | completed | cancelled
        public required OperationKind Kind { get; init; }
        public string? ItemPath { get; init; }
        public string? TargetPath { get; init; }
        public string? Error { get; init; }
        public int? NativeErrorCode { get; init; }
    }

    public void Append(Entry entry)
    {
        lock (_lock)
        {
            File.AppendAllText(_path, JsonSerializer.Serialize(entry, Json) + Environment.NewLine);
        }
    }

    /// <summary>Lee todo el diario para reconciliación.</summary>
    public IReadOnlyList<Entry> ReadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return [];

            var entries = new List<Entry>();
            foreach (var line in File.ReadLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<Entry>(line, Json);
                    if (entry is not null)
                        entries.Add(entry);
                }
                catch (JsonException)
                {
                    // Línea corrupta (corte a mitad de escritura): se ignora de forma
                    // conservadora; nunca impide navegar.
                }
            }
            return entries;
        }
    }

    /// <summary>
    /// Reconcilia operaciones interrumpidas: elementos "item-started" sin
    /// item-done/failed posterior quedan en estado incierto.
    /// </summary>
    public IReadOnlyList<Entry> FindUncertainItems()
    {
        var all = ReadAll();
        var uncertain = new List<Entry>();
        var settled = new HashSet<(Guid, string)>();

        foreach (var entry in all)
        {
            if (entry.ItemPath is null)
                continue;
            var key = (entry.OperationId, entry.ItemPath);
            switch (entry.Stage)
            {
                case "item-done":
                case "item-failed":
                case "item-skipped":
                    settled.Add(key);
                    break;
            }
        }

        foreach (var entry in all)
        {
            if (entry.Stage == "item-started" && entry.ItemPath is { } p && !settled.Contains((entry.OperationId, p)))
                uncertain.Add(entry);
        }
        return uncertain;
    }
}
