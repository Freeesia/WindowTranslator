//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace Composition.WindowsRuntimeHelpers
{
    public static class CompositionHelper
    {
        [ComImport]
        [Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositorInterop
        {
            void CreateCompositionSurfaceForHandle(IntPtr swapChain, out IntPtr surface);

            void CreateCompositionSurfaceForSwapChain(IntPtr swapChain, out IntPtr surface);

            void CreateGraphicsDevice(IntPtr renderingDevice, out IntPtr device);
        }

        [ComImport]
        [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositorDesktopInterop
        {
            IntPtr CreateDesktopWindowTarget(IntPtr hwnd, bool isTopmost);
        }

        public static CompositionTarget CreateDesktopWindowTarget(this Compositor compositor, IntPtr hwnd, bool isTopmost)
        {
            var desktopInterop = compositor.As<ICompositorDesktopInterop>();
            var targetPtr = desktopInterop.CreateDesktopWindowTarget(hwnd, isTopmost);
            return MarshalInterface<DesktopWindowTarget>.FromAbi(targetPtr);
        }

        public static ICompositionSurface CreateCompositionSurfaceForSwapChain(this Compositor compositor, SharpDX.DXGI.SwapChain1 swapChain)
        {
            var interop = compositor.As<ICompositorInterop>();
            interop.CreateCompositionSurfaceForSwapChain(swapChain.NativePointer, out var surface);
            return MarshalInterface<ICompositionSurface>.FromAbi(surface);
        }
    }
}
