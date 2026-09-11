using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WindowTranslator.Extensions;

internal static class PluginParamExtensions
{
    // 設定画面と起動時の検証で、同じ対象のプラグイン設定を復元する。
    public static IReadOnlyList<IPluginParam> GetPluginParams(this IServiceProvider provider, string? name)
        => provider.GetServices<IPluginParam>().Select(param =>
        {
            var configureType = typeof(IConfigureNamedOptions<>).MakeGenericType(param.GetType());
            var configures = (IEnumerable<object>)provider.GetRequiredService(typeof(IEnumerable<>).MakeGenericType(configureType));
            var configureMethod = configureType.GetMethod(nameof(IConfigureNamedOptions<object>.Configure))!;
            foreach (var configure in configures)
            {
                configureMethod.Invoke(configure, [name, param]);
            }
            return param;
        }).ToArray();
}
