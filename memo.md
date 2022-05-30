* ウィンドウの取り込み
* ウィンドウのキャプチャ
* ライブラリ
  * OCR
  * キャッシュ

https://qiita.com/kouh/items/7b55fbc72ffc91983575
https://blog.tmyt.jp/entry/2019/09/26/071634
https://docs.microsoft.com/ja-jp/dotnet/desktop/wpf/advanced/hosting-win32-content-in-wpf
https://yotiky.hatenablog.com/entry/unity_uaal-wpf
https://nakatsudotnet.blog.fc2.com/blog-entry-28.html
https://gist.github.com/itsho/8b0e761d9114e27c8570fbf95465bbfc

https://github.com/michaelsutton/hwnd-adorner
https://github.com/microsoft/Windows.UI.Composition-Win32-Samples

## オーバーレイの選択肢

* キャプチャーそのまま
  * CompositonXaml?
* ウィンドウ埋め込み
  * 埋め込んだウィンドウにオーバーレイする仕組みの実装
  * マウスのスルー
  * https://www.nuget.org/packages/AirspaceFixer/
* ウィンドウ最前面
  * 対象ウィンドウ位置・サイズの特定
    * `User32.GetWindowRect()`
  * 自身のウィンドウの移動
    * ↑で取得した値を入れるとDPIの違いでサイズが一致しない
    * ポジションがうまく設定できない
  * ウインドウ最前面の設定
    * `Window.Topmost`
  * マウスのスルー