using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BackupPro.Binders
{
    /// <summary>
    /// Recorta espacios en blanco al inicio/final de todo valor string que llegue por form/query/ruta,
    /// antes de que llegue al ViewModel — validación "trim" del lado del servidor, aplicada una sola
    /// vez de forma global (ver <see cref="TrimmingModelBinderProvider"/> y su registro en
    /// Program.cs) en vez de tener que repetir <c>.Trim()</c> en cada controlador. Las propiedades de
    /// contraseña quedan excluidas (ver <see cref="TrimmingRules"/>).
    /// </summary>
    public class TrimmingModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            string? value = valueProviderResult.FirstValue;
            bool skipTrim = TrimmingRules.ShouldSkipTrim(bindingContext.ModelMetadata.PropertyName);

            bindingContext.Result = string.IsNullOrEmpty(value) || skipTrim
                ? ModelBindingResult.Success(value)
                : ModelBindingResult.Success(value.Trim());

            return Task.CompletedTask;
        }
    }

    public class TrimmingModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Metadata.ModelType == typeof(string) ? new TrimmingModelBinder() : null;
        }
    }
}
