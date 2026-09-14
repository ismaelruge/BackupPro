using System.IO.Compression;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Extrae el único archivo contenido en el .zip que generan los <see cref="IStorageProvider"/>
    /// al guardar un backup (cada uno empaqueta el dump original en un .zip con una sola entrada;
    /// ver por ejemplo <see cref="LocalStorageProvider.SaveBackupAsync"/>). Compartido por el
    /// <c>RestoreBackupAsync</c> de los 4 motores de base de datos soportados.
    /// </summary>
    public static class BackupZipHelper
    {
        public static byte[] ExtractSingleEntry(MemoryStream zipStream)
        {
            zipStream.Position = 0;
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            var entry = archive.Entries.FirstOrDefault();
            if (entry == null)
            {
                throw new InvalidOperationException("El archivo de backup está vacío o no tiene el formato esperado (.zip con un archivo adentro).");
            }

            using var entryStream = entry.Open();
            using var output = new MemoryStream();
            entryStream.CopyTo(output);
            return output.ToArray();
        }
    }
}
