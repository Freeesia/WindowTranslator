using System.Windows;
using System.Windows.Media;

namespace HwndExtensions.Utils;

internal static class RectUtil
{
    internal static Rect ElementToRoot(Rect rectElement, Visual element, PresentationSource presentationSource)
        => element.TransformToAncestor(presentationSource.RootVisual).TransformBounds(rectElement);

    internal static Rect RootToClient(Rect rectRoot, PresentationSource presentationSource)
    {
        var target = presentationSource.CompositionTarget;
        var matrixRootTransform = GetVisualTransform(target.RootVisual);
        var rectRootUntransformed = Rect.Transform(rectRoot, matrixRootTransform);
        var matrixDPI = target.TransformToDevice;
        return Rect.Transform(rectRootUntransformed, matrixDPI);
    }

    internal static Matrix GetVisualTransform(Visual v)
    {
        if (v == null)
        {
            return Matrix.Identity;
        }
        var m = Matrix.Identity;

        if (VisualTreeHelper.GetTransform(v) is { } transform)
        {
            m = Matrix.Multiply(m, transform.Value);
        }

        var offset = VisualTreeHelper.GetOffset(v);
        m.Translate(offset.X, offset.Y);

        return m;
    }
}
