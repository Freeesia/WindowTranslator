using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// 優先矩形設定のViewModel
/// </summary>
public partial class PriorityRectViewModel : ObservableObject
{
    [ObservableProperty]
    private double x;

    [ObservableProperty]
    private double y;

    [ObservableProperty]
    private double width;

    [ObservableProperty]
    private double height;

    [ObservableProperty]
    private string keyword = string.Empty;

    /// <summary>
    /// PriorityRectからViewModelを作成
    /// </summary>
    public static PriorityRectViewModel FromPriorityRect(PriorityRect rect)
    {
        return new PriorityRectViewModel
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            Keyword = rect.Keyword
        };
    }

    /// <summary>
    /// ViewModelからPriorityRectを作成
    /// </summary>
    public PriorityRect ToPriorityRect()
    {
        return new PriorityRect(X, Y, Width, Height, Keyword);
    }

    /// <summary>
    /// 表示用の文字列
    /// </summary>
    public string DisplayText => $"({X:P1}, {Y:P1}) - {Width:P1} x {Height:P1}" + 
                                  (string.IsNullOrWhiteSpace(Keyword) ? "" : $" [{Keyword}]");
}

/// <summary>
/// 優先矩形リスト管理のViewModel
/// </summary>
public partial class PriorityRectListViewModel : ObservableObject
{
    public ObservableCollection<PriorityRectViewModel> Rects { get; } = new();

    [ObservableProperty]
    private PriorityRectViewModel? selectedRect;

    [ObservableProperty]
    private int imageWidth = 1920;

    [ObservableProperty]
    private int imageHeight = 1080;

    public PriorityRectListViewModel()
    {
    }

    public PriorityRectListViewModel(IEnumerable<PriorityRect> rects)
    {
        foreach (var rect in rects)
        {
            Rects.Add(PriorityRectViewModel.FromPriorityRect(rect));
        }
    }

    [RelayCommand]
    private void AddRect()
    {
        var window = new RectangleSelectionWindow
        {
            Width = ImageWidth,
            Height = ImageHeight
        };

        if (window.ShowDialog() == true && window.SelectedRect != null)
        {
            var vm = PriorityRectViewModel.FromPriorityRect(window.SelectedRect);
            Rects.Add(vm);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveRect))]
    private void RemoveRect()
    {
        if (SelectedRect != null)
        {
            Rects.Remove(SelectedRect);
            SelectedRect = null;
        }
    }

    private bool CanRemoveRect() => SelectedRect != null;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedRect == null)
        {
            return;
        }

        var index = Rects.IndexOf(SelectedRect);
        if (index > 0)
        {
            Rects.Move(index, index - 1);
        }
    }

    private bool CanMoveUp()
    {
        if (SelectedRect == null)
        {
            return false;
        }
        var index = Rects.IndexOf(SelectedRect);
        return index > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedRect == null)
        {
            return;
        }

        var index = Rects.IndexOf(SelectedRect);
        if (index < Rects.Count - 1)
        {
            Rects.Move(index, index + 1);
        }
    }

    private bool CanMoveDown()
    {
        if (SelectedRect == null)
        {
            return false;
        }
        var index = Rects.IndexOf(SelectedRect);
        return index < Rects.Count - 1;
    }

    [RelayCommand]
    private void EditKeyword()
    {
        if (SelectedRect == null)
        {
            return;
        }

        var dialog = new Microsoft.VisualBasic.Interaction();
        var result = Microsoft.VisualBasic.Interaction.InputBox(
            "キーワードを入力してください（翻訳のコンテキストとして使用されます）:",
            "キーワード編集",
            SelectedRect.Keyword
        );

        if (!string.IsNullOrEmpty(result) || result == string.Empty)
        {
            SelectedRect.Keyword = result;
        }
    }

    /// <summary>
    /// PriorityRectのリストを取得
    /// </summary>
    public List<PriorityRect> GetPriorityRects()
    {
        return Rects.Select(vm => vm.ToPriorityRect()).ToList();
    }

    partial void OnSelectedRectChanged(PriorityRectViewModel? value)
    {
        RemoveRectCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
