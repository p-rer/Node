using System.Windows;
using YukkuriMovieMaker.Commons;

namespace Node;

internal class ArgumentsEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create()
    {
        return new ArgumentsEditor();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not ArgumentsEditor editor)
            return;

        editor.ItemProperties = itemProperties;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not ArgumentsEditor editor)
            return;

        editor.ItemProperties = null;
    }
}