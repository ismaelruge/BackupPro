namespace BackupPro.Binders
{
    /// <summary>
    /// Decide qué propiedades string NO se deben recortar automáticamente al hacer model binding.
    /// Separado de <see cref="TrimmingModelBinder"/> para que la regla en sí sea una función pura,
    /// fácil de testear sin necesidad de simular todo el pipeline de model binding de ASP.NET Core.
    /// </summary>
    public static class TrimmingRules
    {
        /// <summary>
        /// Las contraseñas nunca se recortan: un espacio al inicio o al final ahí es parte del valor
        /// que el usuario quiso escribir, no un error de tipeo para corregir en silencio. Se detecta
        /// por convención de nombre (todas las propiedades de contraseña del proyecto contienen
        /// "Password"), no por atributo, para no tener que anotar cada ViewModel.
        /// </summary>
        public static bool ShouldSkipTrim(string? propertyName) =>
            propertyName != null && propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase);
    }
}
