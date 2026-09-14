namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Busca ejecutables de herramientas de línea de comandos (mysqldump, pg_dump, mongodump) en el
    /// PATH del sistema o en rutas comunes de instalación en Windows. El resultado se cachea en
    /// memoria por nombre de ejecutable: la búsqueda solo tiene costo real la primera vez que se
    /// necesita cada herramienta, no en cada backup.
    /// </summary>
    public static class ExecutableLocator
    {
        private static readonly Dictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new();

        /// <summary>
        /// Busca <paramref name="executableName"/> (sin extensión) en el PATH, luego en
        /// <paramref name="windowsCommonPaths"/> (solo en Windows), y como último recurso devuelve
        /// <paramref name="unixCommandName"/> tal cual en sistemas no Windows (confiando en el PATH
        /// del shell que ejecute el proceso). Devuelve cadena vacía si no se encuentra nada.
        /// </summary>
        public static string Find(string executableName, string[] windowsCommonPaths, string unixCommandName)
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(executableName, out var cached))
                {
                    return cached;
                }

                string result = FindUncached(executableName, windowsCommonPaths, unixCommandName);

                if (!string.IsNullOrEmpty(result))
                {
                    Cache[executableName] = result;
                }

                return result;
            }
        }

        private static string FindUncached(string executableName, string[] windowsCommonPaths, string unixCommandName)
        {
            string windowsExeName = executableName + ".exe";
            string[] pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);

            foreach (string dir in pathDirs)
            {
                try
                {
                    string candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? windowsExeName : executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignorar entradas de PATH inválidas
                }
            }

            if (OperatingSystem.IsWindows())
            {
                foreach (string commonPath in windowsCommonPaths)
                {
                    if (File.Exists(commonPath))
                    {
                        return commonPath;
                    }
                }

                return string.Empty;
            }

            // En Linux/Mac, confiar en que el PATH del proceso hijo lo resuelva
            return unixCommandName;
        }
    }
}
