using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HwndExtensions.Adorner;
using HwndExtensions.Utils;

namespace HwndExtensions.Host
{
    /// <summary>
    /// A custom control for managing an HwndHost child and presenting an Adornment over it. 
    /// Inherited classes must control the access and life cycle of the HwndHost child
    /// </summary>
    public class HwndHostPresenter : FrameworkElement, IDisposable
    {
        #region Fields

        private UIElement m_child;
        private HwndHost m_hwndHost;
        private bool m_mouseIsOverHwnd;

        private readonly HwndAdorner m_hwndAdorner;
        private UIElement m_adornment;

        #endregion

        #region CTOR

        static HwndHostPresenter()
        {
            EventManager.RegisterClassHandler(typeof(HwndHostPresenter), MouseEnterEvent, new RoutedEventHandler(OnMouseEnterOrLeave));
            EventManager.RegisterClassHandler(typeof(HwndHostPresenter), MouseLeaveEvent, new RoutedEventHandler(OnMouseEnterOrLeave));

            EventManager.RegisterClassHandler(typeof(HwndHostPresenter), HwndExtensions.HwndMouseEnterEvent, new RoutedEventHandler(OnHwndMouseEnterOrLeave));
            EventManager.RegisterClassHandler(typeof(HwndHostPresenter), HwndExtensions.HwndMouseLeaveEvent, new RoutedEventHandler(OnHwndMouseEnterOrLeave));
        }

        public HwndHostPresenter()
        {
            m_hwndAdorner = new HwndAdorner(this);
            AddLogicalChild(m_hwndAdorner.Root);
        }

        #endregion

        #region Child / Host / Adornment 

        /// <summary>
        /// The only visual child
        /// </summary>
        private UIElement Child
        {
            get
            {
                return m_child;
            }

            set
            {
                if (m_child == value) return;

                RemoveVisualChild(m_child);

                m_child = value;

                AddVisualChild(value);
                InvalidateMeasure();
            }
        }

        public HwndHost HwndHost
        {
            get { return m_hwndHost; }
            set
            {
                if (m_hwndHost == value) return;

                RemoveLogicalChild(m_hwndHost);

                m_hwndHost = value;

                AddLogicalChild(value);
                if (Hosting)
                {
                    Child = value;
                }
            }
        }

        public UIElement Adornment
        {
            get { return m_adornment; }
            set
            {
                if (m_adornment == value) return;

                m_adornment = value;
                m_hwndAdorner.Adornment = m_adornment;
            }
        }

        #endregion

        #region Visual / Logical Trees

        /// <summary> 
        /// The Adorner Root is always a logical child
        /// so is The HwndHost if exists
        /// </summary>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (m_hwndHost != null)
                {
                    return new object[] {m_hwndHost, m_hwndAdorner.Root}
                        .GetEnumerator();
                }
                return new SingleChildEnumerator(m_hwndAdorner.Root);
            }
        }

        /// <summary>
        /// Returns the Visual children count.
        /// </summary>
        protected override int VisualChildrenCount
        {
            get { return (m_child == null) ? 0 : 1; }
        }

        /// <summary>
        /// Returns the child at the specified index.
        /// </summary>
        protected override Visual GetVisualChild(int index)
        {
            if ((m_child == null)
                || (index != 0))
            {
                throw new ArgumentOutOfRangeException("index", index, "presenter has one child at the most");
            }

            return m_child;
        }

        #endregion

        #region Measure / Arrange / Render

        protected override Size MeasureOverride(Size constraint)
        {
            UIElement child = Child;
            if (child != null)
            {
                child.Measure(constraint);
                return (child.DesiredSize);
            }
            return (new Size());
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            UIElement child = Child;
            if (child != null)
            {
                child.Arrange(new Rect(arrangeSize));
            }
            return (arrangeSize);
        }

        /// <summary>
        ///     Fills in the background based on the Background property.
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            Brush background = Background;
            if ( background != null )
            {
                // Using the Background brush, draw a rectangle that fills the
                // render bounds of the panel.
                dc.DrawRectangle(background,
                                 null,
                                 new Rect(RenderSize));
            }

            base.OnRender(dc);
        }

        #endregion

        #region Contexn Menu

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            // openning context menu programmatically since it dosn't open when clicking above the HwndHost.
            // we raise the mouse event programmatically, so we can respond to it although the system dosn't 

            if (!e.Handled && e.ChangedButton == MouseButton.Right && this.ContextMenu != null)
            {
                ContextMenu.PlacementTarget = this; // important for receiving the correct DataContext
                ContextMenu.IsOpen = true;
                e.Handled = true;
            }

            base.OnMouseUp(e);
        }

        #endregion

        #region Background property

        /// <summary>
        /// DependencyProperty for <see cref="Background" /> property.
        /// </summary>
        public static readonly DependencyProperty BackgroundProperty =
                DependencyProperty.Register("Background",
                        typeof(Brush),
                        typeof(HwndHostPresenter),
                        new FrameworkPropertyMetadata(null,
                                FrameworkPropertyMetadataOptions.AffectsRender |
                                FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender));

        /// <summary>
        /// The Background property defines the brush used to fill the area between borders.
        /// </summary>
        public Brush Background
        {
            get { return (Brush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        #endregion

        #region IsMouseOverOverride property

        // A property maintaining the Mouse Over state for all content - including an 
        // HwndHost with a Message Loop on another thread
        // HwndHost childs should raise the HwndExtensions.HwndMouseXXX routed events

        private readonly static DependencyPropertyKey IsMouseOverOverridePropertyKey = DependencyProperty.RegisterReadOnly(
            "IsMouseOverOverride",
            typeof(bool),
            typeof(HwndHostPresenter),
            new PropertyMetadata(false)
            );
        public readonly static DependencyProperty IsMouseOverOverrideProperty = IsMouseOverOverridePropertyKey.DependencyProperty;

        public bool IsMouseOverOverride
        {
            get { return (bool)GetValue(IsMouseOverOverrideProperty); }
        }

        private static void OnMouseEnterOrLeave(object sender, RoutedEventArgs e)
        {
            var presenter = (HwndHostPresenter) sender;
            presenter.InvalidateMouseOver();
        }

        private static void OnHwndMouseEnterOrLeave(object sender, RoutedEventArgs e)
        {
            var presenter = (HwndHostPresenter)sender;

            if (e.OriginalSource == presenter.m_hwndHost)
                // Handling this routed event only if its coming from our direct child
            {
                presenter.m_mouseIsOverHwnd = e.RoutedEvent == HwndExtensions.HwndMouseEnterEvent;
                presenter.InvalidateMouseOver();
            }
        }

        private void InvalidateMouseOver()
        {
            bool over = 
                IsMouseOver ||
                (m_hwndHost != null && m_mouseIsOverHwnd);

            SetValue(IsMouseOverOverridePropertyKey, over);
        }

        #endregion

        #region Hosting property

        public static readonly DependencyProperty HostingProperty = DependencyProperty.Register(
            "Hosting", typeof (bool), typeof (HwndHostPresenter), new UIPropertyMetadata(true, OnHostingChanged));

        public bool Hosting
        {
            get { return (bool) GetValue(HostingProperty); }
            set { SetValue(HostingProperty, value); }
        }

        private static void OnHostingChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var presenter = (HwndHostPresenter)d;
            presenter.OnHostingChanged((bool)args.NewValue);
        }

        private void OnHostingChanged(bool hosting)
        {
            if (hosting)
            {
                Child = m_hwndHost;
            }

            else
            {
                Child = null;
            }
        }

        #endregion

        #region IDisposable Imp

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Inherited classes should decide whether to dispose the HwndHost child
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                var host = HwndHost;
                if (host != null)
                    host.Dispose();
            }
        }

        #endregion
    }
}
