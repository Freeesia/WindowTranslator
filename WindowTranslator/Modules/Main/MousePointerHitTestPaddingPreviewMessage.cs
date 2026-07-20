namespace WindowTranslator.Modules.Main;

/// <summary>
/// 設定画面でマウスポインター判定の余白(<see cref="Settings.TargetSettingsViewModel.MousePointerHitTestPadding"/>)を
/// 編集中に、対象のオーバーレイへプレビュー表示を指示するメッセージ。
/// </summary>
/// <param name="TargetName">対象の翻訳対象名(<see cref="MainViewModelBase"/>が保持する名前)。</param>
/// <param name="Padding">プレビュー表示する余白(WPF上のピクセル値)。フォーカスが外れた場合はnull。</param>
internal record MousePointerHitTestPaddingPreviewMessage(string TargetName, double? Padding);
