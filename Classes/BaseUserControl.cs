using Avalonia.Controls;
using Black_Hole_Cross.Services;

namespace BlackHole;

public class BaseUserControl : UserControl
{
    public BaseUserControl()
    {
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    protected virtual void OnLanguageChanged()
    {
    }
}
