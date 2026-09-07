using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Black_Hole_Cross.Services
{
    public static class ThemeService
    {
        public static void ChangeTheme(string themeName)
        {
            var app = Application.Current;
            if (app == null) return;

            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

            var cleanName = themeName
                .Replace(".axaml", "", StringComparison.OrdinalIgnoreCase)
                .Replace(".xaml", "", StringComparison.OrdinalIgnoreCase);

          
            var resourcePath = $"avares://{assemblyName}/Themes/{cleanName}.axaml";
            var themeUri = new Uri(resourcePath);

            try
            {
               
                if (!AssetLoader.Exists(themeUri))
                {
                   
                    var altUri = new Uri($"avares://{assemblyName}/themes/{cleanName}.axaml");
                    if (AssetLoader.Exists(altUri))
                    {
                        themeUri = altUri;
                    }
                }

               
                var newThemeDict = (ResourceDictionary)AvaloniaXamlLoader.Load(themeUri);

               
                foreach (var key in newThemeDict.Keys)
                {
                    app.Resources[key] = newThemeDict[key];
                }

                System.Diagnostics.Debug.WriteLine($"✅ УСПЕХ! Тема '{cleanName}' загружена по URI: {themeUri}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка при открытии ресурса темы '{cleanName}': {ex.Message}");
            }
        }
    }
}
