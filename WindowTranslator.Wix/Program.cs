using System;
using System.Diagnostics;
using WixSharp;
using Path = System.IO.Path;

const string Manufacturer = "StudioFreesia";
const string App = "WindowTranslator";
const string ArtifactsDir = @"..\artifacts";
const string PublishDir = @"..\publish";
const string Executable = $"{App}.exe";

var exePath = Path.Combine(Environment.CurrentDirectory, ArtifactsDir, Executable);
var info = FileVersionInfo.GetVersionInfo(exePath);
var version = info.FileVersion;

var project = new ManagedProject(App,
    new Dir(@$"%LocalAppData%\{Manufacturer}\{App}",
        new File(exePath) { AddCloseAction = true },
        new Files(Path.Combine(ArtifactsDir, "*.*"), p => !p.EndsWith(Executable))),
    // スタートメニューにショートカットを追加
    new Dir(@$"%ProgramMenu%\{Manufacturer}\{App}",
        new ExeFileShortcut(App, $"[INSTALLDIR]{App}", "")));

project.RebootSupressing = RebootSupressing.Suppress;
project.GUID = new("1D495A96-C8B4-4314-A08B-60665007A446");
project.Platform = Platform.x64;
project.Language = "ja-JP";
project.Version = new(version);

// コントロールパネルの情報を設定
project.ControlPanelInfo = new()
{
    Manufacturer = Manufacturer,
    ProductIcon = @"..\docs\images\wt.ico",
    UrlInfoAbout = "https://github.com/Freeesia/WindowTranslator",
    UrlUpdateInfo = "https://github.com/Freeesia/WindowTranslator/releases",
};

// どっちか片方しか設定できない
//project.MajorUpgrade = MajorUpgrade.Default;
project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;

project.BackgroundImage = @"installer_back.png";
project.ValidateBackgroundImage = false;
project.BannerImage = @"installer_bunner.png";

// ユーザーレベルのインストールを強制する
project.Scope = InstallScope.perUser;

// ライセンスファイルの設定
project.LicenceFile = @"..\LICENSE.rtf";

// インストール後にアプリを起動するオプション
project.AfterInstall += static e =>
{
    // アンインストール時には起動しない
    if (!e.IsUninstalling)
    {
        Process.Start(e.InstallDir.PathCombine(Executable));
    }
};

project.BuildMsi(Path.Combine(PublishDir, $"{App}-{version}.msi"));
