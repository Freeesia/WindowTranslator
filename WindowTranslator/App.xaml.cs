using Composition.WindowsRuntimeHelpers;
using System.Windows;
using Windows.System;

namespace WindowTranslator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly DispatcherQueueController? controller;

    public App()
    {
        this.controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
        InitializeComponent();
    }
}
