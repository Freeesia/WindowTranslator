using System.Text.RegularExpressions;
using System.Windows.Documents;
using MdXaml.Plugins;

namespace WindowTranslator.Controls;

public partial class LineBreakSplitPluginSetup : IPluginSetup
{
    public void Setup(MdXamlPlugins plugins)
        => plugins.Inline.Add(LineBreakSplitParser.Instance);

    private partial class LineBreakSplitParser : IInlineParser
    {
        public static LineBreakSplitParser Instance { get; } = new();
        public Regex FirstMatchPattern { get; } = LineBreakRegex();

        [GeneratedRegex(@"(.*)\n", RegexOptions.Compiled)]
        private static partial Regex LineBreakRegex();

        public IEnumerable<Inline> Parse(string text, Match firstMatch, IMarkdown engine, out int parseTextBegin, out int parseTextEnd)
        {
            parseTextBegin = firstMatch.Index;
            parseTextEnd = firstMatch.Index + firstMatch.Length;

            return [new Run(firstMatch.Groups[1].Value), new LineBreak()];
        }
    }
}
