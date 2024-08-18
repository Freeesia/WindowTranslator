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
    private readonly TaskCompletionSource tcs = new();

    public App()
    {
        this.controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
        InitializeComponent();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        this.tcs.SetResult();
    }

    public Task WaitForStartupAsync()
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        => this.tcs.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
}
