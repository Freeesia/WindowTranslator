using System.Windows;
using System.Windows.Media;

namespace HwndExtensions.Utils;

internal static class RectUtil
{
    internal static Rect ElementToRoot(Rect rectElement, Visual element, PresentationSource presentationSource)
    {
        GeneralTransform transformElementToRoot = element.TransformToAncestor(presentationSource.RootVisual);
        Rect rectRoot = transformElementToRoot.TransformBounds(rectElement);

        return rectRoot;
    }

    internal static Rect RootToClient(Rect rectRoot, PresentationSource presentationSource)
    {
        CompositionTarget target = presentationSource.CompositionTarget;
        Matrix matrixRootTransform = GetVisualTransform(target.RootVisual);
        Rect rectRootUntransformed = Rect.Transform(rectRoot, matrixRootTransform);
        Matrix matrixDPI = target.TransformToDevice;
        Rect rectClient = Rect.Transform(rectRootUntransformed, matrixDPI);

        return rectClient;
    }

    internal static Matrix GetVisualTransform(Visual v)
    {
        if (v != null)
        {
            Matrix m = Matrix.Identity;

            Transform transform = VisualTreeHelper.GetTransform(v);
            if (transform != null)
            {
                Matrix cm = transform.Value;
                m = Matrix.Multiply(m, cm);
            }

            Vector offset = VisualTreeHelper.GetOffset(v);
            m.Translate(offset.X, offset.Y);

            return m;
        }

        return Matrix.Identity;
    }
}
