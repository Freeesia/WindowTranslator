using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace HwndExtensions.Utils
{
    public class DispatchUI
    {
        public static Dispatcher MainDispatcher;

        public static Dispatcher CurrentDispatcher()
        {
            return Application.Current != null
                ? Application.Current.Dispatcher
                : MainDispatcher;
        }

        /// <summary>
        /// Verify access to the main UI thread if exists
        /// </summary>
        public static void VerifyAccess()
        {
            var dispatcher = MainDispatcher ?? (Application.Current == null ? null : Application.Current.Dispatcher);
            if (dispatcher != null)
            {
                dispatcher.VerifyAccess();
            }
        }

        /// <summary>
        /// Run the current action on the UI thread if exists
        /// <param name="action">The action to run on ui thread</param>
        /// <param name="invokeBlocking">Invoke the action with blocking (invoke) use.</param>
        /// </summary>
        public static void OnUIThread(Action action, bool invokeBlocking = false)
        {
            // if no application is running or the main dispatcher run on the current thread
            if (MainDispatcher == null && Application.Current == null)
            {
                action();
                return;
            }

            // get the current dispatcher, check access and run where needed
            Dispatcher dispatcherObject = MainDispatcher ?? Application.Current.Dispatcher;

            if (dispatcherObject == null || dispatcherObject.CheckAccess())
            {
                action();
            }
            else
            {
                // run the invocation blocking or async
                if (invokeBlocking)
                {
                    dispatcherObject.Invoke(action);
                }
                else
                {
                    dispatcherObject.BeginInvoke(action);
                }
            }
        }

        public static bool OnUIThreadAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal,
            Dispatcher dispatcher = null)
        {
            dispatcher = dispatcher ??
                         MainDispatcher ?? (Application.Current == null ? null : Application.Current.Dispatcher);
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(action, priority);
                return true;
            }

            return false;
        }
    }
}
