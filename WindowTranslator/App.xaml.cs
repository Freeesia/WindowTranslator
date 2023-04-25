using Composition.WindowsRuntimeHelpers;
using System.Windows;
using Windows.System;

namespace WindowTranslator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
#pragma warning disable IDE0052 // WinUIのコントロール使うために初期化する必要がある
    private readonly DispatcherQueueController? controller;
#pragma warning restore IDE0052

    public App()
    {
        this.controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
        InitializeComponent();
    }
}
