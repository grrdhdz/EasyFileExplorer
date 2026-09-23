using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.FileSystem;

/// <summary>
/// Resolución bajo demanda de la identidad estable NTFS (volumen + FileID).
/// Solo se calcula para elementos que lo necesitan (selección, operaciones,
/// etiquetas) — nunca durante la enumeración de 100k elementos.
/// En sistemas no NTFS o cuando falla, devuelve identidad por ruta (IsStable=false).
/// </summary>
public static class StableIdentity
{
    public static ItemId Resolve(string path)
    {
        var info = TryGetFileIdInfo(path);
        if (info is { } f)
            return new ItemId
            {
                Provider = LocalFileSystemProvider.ProviderId,
                Identity = $"vol:{f.VolumeSerialNumber:x}:file:{f.FileId:x}",
                Path = path,
                IsStable = true,
            };
        return ItemId.ForPath(LocalFileSystemProvider.ProviderId, path);
    }

    private readonly struct FileIdParts
    {
        public required ulong VolumeSerialNumber { get; init; }
        public required ulong FileId { get; init; }
    }

    private static FileIdParts? TryGetFileIdInfo(string path)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        using var handle = CreateFile(
            path,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
            IntPtr.Zero);

        if (handle.IsInvalid)
            return null;

        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<FILE_ID_INFO>());
        try
        {
            if (!GetFileInformationByHandleEx(handle, FILE_INFO_BY_HANDLE_CLASS.FileIdInfo, buffer, (uint)Marshal.SizeOf<FILE_ID_INFO>()))
                return null;
            var info = Marshal.PtrToStructure<FILE_ID_INFO>(buffer);
            return new FileIdParts { VolumeSerialNumber = info.VolumeSerialNumber, FileId = info.FileId.Identifier };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const int FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        FileAccess dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        int dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        FILE_INFO_BY_HANDLE_CLASS dwFileInformationClass,
        IntPtr lpFileInformation,
        uint dwBufferSize);

    private enum FILE_INFO_BY_HANDLE_CLASS
    {
        FileIdInfo = 18,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_128
    {
        public ulong Identifier;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public FILE_ID_128 FileId;
    }
}
