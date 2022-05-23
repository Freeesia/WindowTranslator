using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.Ocr;
public interface IOcrModule
{
    ValueTask<TextRect[]> RecognizeAsync(SoftwareBitmap bitmap);
}
