using System.Runtime.CompilerServices;
using System.Threading.Channels;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.FileSystem;

/// <summary>
/// Vigilancia de cambios de una carpeta local mediante FileSystemWatcher.
/// Si el buffer interno se desborda, emite <see cref="FileChangeKind.Lost"/> para
/// que el consumidor reconcilie con una enumeración nueva.
/// </summary>
public static class LocalChangeWatcher
{
    public static async IAsyncEnumerable<FileChangeEvent> WatchAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<FileChangeEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        using var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.Attributes,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = true,
        };

        void OnCreated(object s, FileSystemEventArgs e) =>
            channel.Writer.TryWrite(new FileChangeEvent { Kind = FileChangeKind.Created, Path = e.FullPath });
        void OnDeleted(object s, FileSystemEventArgs e) =>
            channel.Writer.TryWrite(new FileChangeEvent { Kind = FileChangeKind.Deleted, Path = e.FullPath });
        void OnChanged(object s, FileSystemEventArgs e) =>
            channel.Writer.TryWrite(new FileChangeEvent { Kind = FileChangeKind.Modified, Path = e.FullPath });
        void OnRenamed(object s, RenamedEventArgs e) =>
            channel.Writer.TryWrite(new FileChangeEvent { Kind = FileChangeKind.Renamed, Path = e.FullPath, OldPath = e.OldFullPath });
        void OnError(object s, ErrorEventArgs e) =>
            channel.Writer.TryWrite(new FileChangeEvent { Kind = FileChangeKind.Lost, Path = path });

        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Changed += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        using var registration = cancellationToken.Register(() => channel.Writer.TryComplete());

        await foreach (var ev in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return ev;
    }
}
