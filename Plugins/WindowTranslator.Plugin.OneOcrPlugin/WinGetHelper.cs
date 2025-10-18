using System.Diagnostics;
using Microsoft.Management.Deployment;

namespace WindowTranslator.Plugin.OneOcrPlugin;

/// <summary>
/// WinGet COM APIを使用してMicrosoft Storeアプリを管理するヘルパークラス
/// </summary>
internal static class WinGetHelper
{
    private const string MsStoreSourceName = "msstore";

    /// <summary>
    /// 指定されたProduct IDのパッケージ情報を取得する
    /// </summary>
    /// <param name="productId">Microsoft Store Product ID (例: 9MZ95KL8MR0L)</param>
    /// <returns>パッケージ情報。見つからない場合はnull</returns>
    public static async Task<PackageInfo?> GetPackageInfoAsync(string productId)
    {
        try
        {
            var (manager, catalog) = await ConnectToCatalogAsync().ConfigureAwait(false);
            if (catalog == null)
            {
                return null;
            }

            var findOptions = CreateFindOptions(productId);
            var result = await catalog.FindPackagesAsync(findOptions).AsTask().ConfigureAwait(false);
            
            var match = result.Matches.FirstOrDefault();
            if (match == null)
            {
                return null;
            }

            var pkg = match.CatalogPackage;
            var version = pkg.DefaultInstallVersion;

            return new PackageInfo
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Version = version?.Version,
                Source = version?.PackageCatalog?.Info?.Name ?? string.Empty,
                CatalogPackage = pkg
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// インストール済みのパッケージ情報を取得する
    /// </summary>
    /// <param name="productId">Microsoft Store Product ID</param>
    /// <returns>インストール済みパッケージ情報。見つからない場合はnull</returns>
    public static async Task<PackageInfo?> GetInstalledPackageInfoAsync(string productId)
    {
        try
        {
            var (manager, catalog) = await ConnectToCompositeAsync().ConfigureAwait(false);
            if (catalog == null)
            {
                return null;
            }

            var findOptions = CreateFindOptions(productId);
            var result = await catalog.FindPackagesAsync(findOptions).AsTask().ConfigureAwait(false);

            var match = result.Matches.FirstOrDefault();
            if (match == null)
            {
                return null;
            }

            var pkg = match.CatalogPackage;
            var installedVersion = pkg.InstalledVersion;

            return new PackageInfo
            {
                Id = pkg.Id,
                Name = pkg.Name,
                Version = installedVersion?.Version,
                Source = installedVersion?.PackageCatalog?.Info?.Name ?? string.Empty,
                CatalogPackage = pkg
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// パッケージをアップグレードする
    /// </summary>
    /// <param name="packageInfo">アップグレード対象のパッケージ情報</param>
    /// <returns>アップグレードが成功したかどうか</returns>
    public static async Task<bool> UpgradePackageAsync(PackageInfo packageInfo)
    {
        try
        {
            var (manager, _) = await ConnectToCatalogAsync().ConfigureAwait(false);
            if (manager == null || packageInfo.CatalogPackage == null)
            {
                return false;
            }

            var options = CreateUpgradeOptions();
            var result = await manager.UpgradePackageAsync(packageInfo.CatalogPackage, options).AsTask().ConfigureAwait(false);

            return result.Status == InstallResultStatus.Ok;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Microsoft Storeカタログに接続する
    /// </summary>
    private static async Task<(PackageManager? manager, PackageCatalog? catalog)> ConnectToCatalogAsync()
    {
        try
        {
            var factory = new WindowsPackageManagerStandardFactory();
            var manager = factory.CreatePackageManager();

            var catalogRef = manager.GetPackageCatalogByName(MsStoreSourceName);
            if (catalogRef == null)
            {
                return (null, null);
            }

            var connectResult = await catalogRef.ConnectAsync().AsTask().ConfigureAwait(false);
            if (connectResult.Status != ConnectResultStatus.Ok)
            {
                return (null, null);
            }

            return (manager, connectResult.PackageCatalog);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// ローカルとMicrosoft Storeの複合カタログに接続する
    /// </summary>
    private static async Task<(PackageManager? manager, PackageCatalog? catalog)> ConnectToCompositeAsync()
    {
        try
        {
            var factory = new WindowsPackageManagerStandardFactory();
            var manager = factory.CreatePackageManager();

            var localRef = manager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
            var storeRef = manager.GetPackageCatalogByName(MsStoreSourceName);

            var options = factory.CreateCreateCompositePackageCatalogOptions();
            options.Catalogs.Add(localRef);
            options.Catalogs.Add(storeRef);
            options.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

            var compositeRef = manager.CreateCompositePackageCatalog(options);
            var connectResult = await compositeRef.ConnectAsync().AsTask().ConfigureAwait(false);

            if (connectResult.Status != ConnectResultStatus.Ok)
            {
                return (null, null);
            }

            return (manager, connectResult.PackageCatalog);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// 検索オプションを作成する
    /// </summary>
    private static FindPackagesOptions CreateFindOptions(string productId)
    {
        var factory = new WindowsPackageManagerStandardFactory();
        var findOptions = factory.CreateFindPackagesOptions();
        
        var filter = factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.Equals;
        filter.Value = productId;
        
        findOptions.Filters.Add(filter);
        return findOptions;
    }

    /// <summary>
    /// アップグレードオプションを作成する
    /// </summary>
    private static UpgradeOptions CreateUpgradeOptions()
    {
        var factory = new WindowsPackageManagerStandardFactory();
        var options = factory.CreateUpgradeOptions();
        options.AcceptPackageAgreements = true;
        return options;
    }

    /// <summary>
    /// バージョン文字列を比較する
    /// </summary>
    /// <param name="version1">バージョン1</param>
    /// <param name="version2">バージョン2</param>
    /// <returns>version1がversion2より新しい場合は1、古い場合は-1、同じ場合は0</returns>
    public static int CompareVersions(string version1, string version2)
    {
        if (Version.TryParse(version1, out var v1) && Version.TryParse(version2, out var v2))
        {
            return v1.CompareTo(v2);
        }

        // Version型でパースできない場合は文字列比較
        return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// パッケージ情報を保持するクラス
/// </summary>
internal class PackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public CatalogPackage? CatalogPackage { get; set; }
}

/// <summary>
/// WinGet COM APIの標準ファクトリ
/// </summary>
file class WindowsPackageManagerStandardFactory : IWindowsPackageManagerFactory
{
    public PackageManager CreatePackageManager() => new();

    public FindPackagesOptions CreateFindPackagesOptions() => new();

    public CreateCompositePackageCatalogOptions CreateCreateCompositePackageCatalogOptions() => new();

    public InstallOptions CreateInstallOptions() => new();

    public UninstallOptions CreateUninstallOptions() => new();

    public UpgradeOptions CreateUpgradeOptions() => new();

    public PackageMatchFilter CreatePackageMatchFilter() => new();
}

/// <summary>
/// WinGet COM APIのファクトリインターフェース
/// </summary>
file interface IWindowsPackageManagerFactory
{
    PackageManager CreatePackageManager();
    FindPackagesOptions CreateFindPackagesOptions();
    CreateCompositePackageCatalogOptions CreateCreateCompositePackageCatalogOptions();
    InstallOptions CreateInstallOptions();
    UninstallOptions CreateUninstallOptions();
    UpgradeOptions CreateUpgradeOptions();
    PackageMatchFilter CreatePackageMatchFilter();
}
