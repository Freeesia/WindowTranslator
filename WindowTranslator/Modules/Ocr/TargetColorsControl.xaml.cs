using System.Windows.Controls;

namespace WindowTranslator.Modules.Ocr;
/// <summary>
/// TargetColorsControl.xaml の相互作用ロジック
/// </summary>
public partial class TargetColorsControl : UserControl
{
    public TargetColorsControl()
        => InitializeComponent();
}

public class TargetColorsControlView : IPropertyView
{
    public string ViewName => nameof(TargetColorsControl);

    public Type ViewType => typeof(TargetColorsControl);
}