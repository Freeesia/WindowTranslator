using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;
using Emoji.Wpf;
using MdXaml.Plugins;

namespace WindowTranslator.Controls;

internal partial class CustomPluginSetup : IPluginSetup
{
    public void Setup(MdXamlPlugins plugins)
    {
        plugins.Inline.Add(LineBreakSplitParser.Instance);
        plugins.Inline.Add(EmojiParser.Instance);
    }

    private partial class LineBreakSplitParser : IInlineParser
    {
        public static LineBreakSplitParser Instance { get; } = new();

        [GeneratedRegex(@"(.*)\n", RegexOptions.Compiled)]
        public partial Regex FirstMatchPattern { get; }

        public IEnumerable<Inline> Parse(string text, Match firstMatch, IMarkdown engine, out int parseTextBegin, out int parseTextEnd)
        {
            parseTextBegin = firstMatch.Index;
            parseTextEnd = firstMatch.Index + firstMatch.Length;

            return [new Run(firstMatch.Groups[1].Value), new LineBreak()];
        }
    }

    private partial class EmojiParser : IInlineParser
    {
        public static EmojiParser Instance { get; } = new();

        [GeneratedRegex(@"(:[a-zA-Z0-9_+-]+:)", RegexOptions.Compiled)]
        public partial Regex FirstMatchPattern { get; }
        public IEnumerable<Inline> Parse(string text, Match firstMatch, IMarkdown engine, out int parseTextBegin, out int parseTextEnd)
        {
            parseTextBegin = firstMatch.Index;
            parseTextEnd = firstMatch.Index + firstMatch.Length;
            return [new EmojiInline() { Text = firstMatch.Groups[1].Value, Foreground = Brushes.Black }];
        }
    }
}
