using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Tray.Controls;

namespace WindowTranslator.Controls;

public class NotifyIcon2 : NotifyIcon, ICommandSource
{
    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(NotifyIcon2), new PropertyMetadata(null));
    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(NotifyIcon2), new PropertyMetadata(null));
    /// <summary>Identifies the <see cref="CommandTarget"/> dependency property.</summary>
    public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(nameof(CommandTarget), typeof(IInputElement), typeof(NotifyIcon2), new PropertyMetadata(null));

    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public NotifyIcon2()
    {
        this.DataContextChanged += NotifyIcon2_DataContextChanged;
    }

    private void NotifyIcon2_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (this.Menu != null)
        {
            this.Menu.DataContext = this.DataContext;
        }
    }

    protected override void OnMenuChanged(ContextMenu contextMenu)
    {
        contextMenu.DataContext = this.DataContext;
        base.OnMenuChanged(contextMenu);
    }

    override protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.DataContextChanged -= NotifyIcon2_DataContextChanged;
        }
        base.Dispose(disposing);
    }

    protected override void OnLeftClick()
    {
        if (Command is { } command)
        {
            if (command is RoutedCommand routed)
            {
                var target = CommandTarget ?? this;
                if (routed.CanExecute(CommandParameter, target))
                {
                    routed.Execute(CommandParameter, target);
                }
            }
            else if (command.CanExecute(CommandParameter))
            {
                command.Execute(CommandParameter);
            }
        }
        else
        {
            base.OnLeftClick();
        }
    }
}
