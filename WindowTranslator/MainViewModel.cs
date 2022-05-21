using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WindowTranslator;

[ObservableObject]
public partial class MainViewModel
{
    [ObservableProperty]
    private IntPtr windowHandle;
}
