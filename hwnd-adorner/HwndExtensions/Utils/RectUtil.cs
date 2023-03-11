using System.Windows;
using System.Windows.Media;

namespace HwndExtensions.Utils
{
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

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
            Matrix matrixRootTransform = RectUtil.GetVisualTransform(target.RootVisual);
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

        internal static RECT FromRect(Rect rect)
        {
            RECT rc = new RECT();

            rc.top = DoubleToInt(rect.Y);
            rc.left = DoubleToInt(rect.X);
            rc.bottom = DoubleToInt(rect.Bottom);
            rc.right = DoubleToInt(rect.Right);

            return rc;
        }

        internal static Rect ToRect(RECT rc)
        {
            Rect rect = new Rect();

            rect.X = rc.left;
            rect.Y = rc.top;
            rect.Width = rc.right - rc.left;
            rect.Height = rc.bottom - rc.top;

            return rect;
        }

        public static int DoubleToInt(double val)
        {
            return (0 < val) ? (int)(val + 0.5) : (int)(val - 0.5);
        }
    }
}
