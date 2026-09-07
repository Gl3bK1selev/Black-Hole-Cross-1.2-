using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Linq;

namespace Black_Hole_Cross.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private Dictionary<string, Dictionary<string, string>> _languages;
        private string _currentLanguage = "ru";

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action LanguageChanged;

        public LocalizationService()
        {
            LoadLanguages();
            _currentLanguage = SimpleSettings.Language ?? "ru";
        }

        private void LoadLanguages()
        {
            _languages = new Dictionary<string, Dictionary<string, string>>();

            // Ищем папку Localization
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localizationPath = Path.Combine(baseDir, "Localization");

            // Проверяем несколько возможных путей
            var possiblePaths = new List<string>
            {
                localizationPath,
                Path.Combine(AppContext.BaseDirectory, "Localization"),
                Path.Combine(Directory.GetCurrentDirectory(), "Localization"),
                Path.Combine("..", "..", "..", "Localization")
            };

            string foundPath = null;

            foreach (var path in possiblePaths)
            {
                Debug.WriteLine($"🔍 Проверяю путь: {path}");
                if (Directory.Exists(path))
                {
                    var jsonFiles = Directory.GetFiles(path, "*.json");
                    if (jsonFiles.Length > 0)
                    {
                        foundPath = path;
                        Debug.WriteLine($"✅ Найдена папка с JSON: {path} ({jsonFiles.Length} файлов)");
                        break;
                    }
                }
            }

            if (foundPath == null)
            {
                Debug.WriteLine($"❌ Папка Localization не найдена! Создаю пустую.");
                try
                {
                    if (!Directory.Exists(localizationPath))
                        Directory.CreateDirectory(localizationPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Не удалось создать папку: {ex.Message}");
                }
                return;
            }

            // Загружаем JSON файлы
            var files = Directory.GetFiles(foundPath, "*.json");
            Debug.WriteLine($"📄 Найдено {files.Length} JSON файлов");

            foreach (var file in files)
            {
                try
                {
                    string language = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file);
                    var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (translations != null && translations.Count > 0)
                    {
                        _languages[language] = translations;
                        Debug.WriteLine($"✅ Загружен язык: {language} ({translations.Count} ключей)");
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ Файл {file} пустой или поврежден");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Ошибка загрузки {file}: {ex.Message}");
                }
            }

            if (_languages.Count == 0)
            {
                Debug.WriteLine($"⚠️ Не загружено ни одного языка!");
            }
        }

        public string this[string key]
        {
            get
            {
                if (_languages.TryGetValue(_currentLanguage, out var langDict) &&
                    langDict.TryGetValue(key, out var value))
                {
                    return value;
                }

                Debug.WriteLine($"❌ Ключ не найден: {key}");
                return $"#{key}#";
            }
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value && _languages.ContainsKey(value))
                {
                    _currentLanguage = value;
                    SimpleSettings.Language = value;
                    SimpleSettings.SaveSettings();

                    OnPropertyChanged();
                    OnPropertyChanged("Item");
                    LanguageChanged?.Invoke();
                }
            }
        }

        public void SetLanguage(string language)
        {
            CurrentLanguage = language;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyLanguageChanged()
        {
            LanguageChanged?.Invoke();
        }
    }
}