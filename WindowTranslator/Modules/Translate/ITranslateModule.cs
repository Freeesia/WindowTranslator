using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowTranslator.Modules.Translate;
internal interface ITranslateModule
{
    ValueTask<string[]> TranslateAsync(string[] srcTexts);
}
