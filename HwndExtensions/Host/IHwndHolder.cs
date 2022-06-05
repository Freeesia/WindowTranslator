using System.Windows;

namespace HwndExtensions.Host
{
    public interface IHwndHolder
    {
        void CollapseHwnd(bool freezeBounds = false);
        void FreezeHwndBounds();
        void ExpandHwnd();

        Rect LatestHwndBounds { get; }
        Rect FreezedHwndBounds { get; }
    }
}
