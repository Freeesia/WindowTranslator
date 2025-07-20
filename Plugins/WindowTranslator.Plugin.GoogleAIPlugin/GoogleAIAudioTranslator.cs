using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowTranslator.Plugin.GoogleAIPlugin;
internal sealed class GoogleAIAudioTranslator : IDisposable
{
    private readonly int pid;

    public GoogleAIAudioTranslator(int pid)
    {
        this.pid = pid;
    }

    public void Dispose()
    {
    }
}
