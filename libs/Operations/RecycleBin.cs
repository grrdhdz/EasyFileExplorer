using Microsoft.VisualBasic.FileIO;
using VbFileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace EasyFileExplorer.Operations;

/// <summary>
/// Papelera de Windows mediante el mismo mecanismo del Shell que Explorer
/// (FOF_ALLOWUNDO vía FileIO). El borrado permanente es siempre una decisión
/// explícita del usuario (Shift+Delete), indicada antes de ejecutarse.
/// </summary>
public static class RecycleBin
{
    /// <summary>Envía a la papelera; devuelve false si el elemento no existe ya.</summary>
    public static void SendFile(string path)
    {
        VbFileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }

    public static void SendDirectory(string path)
    {
        VbFileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }

    /// <summary>Borrado permanente explícito.</summary>
    public static void DeletePermanently(string path, bool isFolder)
    {
        if (isFolder)
            VbFileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently, UICancelOption.ThrowException);
        else
            VbFileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently, UICancelOption.ThrowException);
    }

    /// <summary>
    /// La papelera no está disponible en todos los destinos (p. ej. algunas rutas
    /// de red o proveedores); la UI debe avisar antes de borrar permanentemente.
    /// </summary>
    public static bool IsRecycleBinLikelyAvailable(string path) =>
        !path.StartsWith(@"\\", StringComparison.Ordinal);
}
