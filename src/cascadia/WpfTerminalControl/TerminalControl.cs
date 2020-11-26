// <copyright file="TerminalControl.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Terminal.Wpf
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// A terminal control based on Windows Terminal components. This control can receive and render standard VT100 sequences.
    /// </summary>
    /// <remarks>
    /// To change the theme <see cref="TerminalTheme"/> needs to be assign to property <see cref="Theme"/>.
    /// </remarks>
    public class TerminalControl : Control
    {
        /// <summary>
        /// TerminalConnection Property.
        /// </summary>
        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register("Theme", typeof(TerminalTheme), typeof(TerminalControl), new PropertyMetadata(defaultTerminalTheme, ThemeChanged));

        /// <summary>
        /// TerminalConnection Property.
        /// </summary>
        public static readonly DependencyProperty ConnectionProperty =
            DependencyProperty.Register("Connection", typeof(TerminalConnection), typeof(TerminalControl), new PropertyMetadata(null, ConnectionChanged));

        private static TerminalTheme defaultTerminalTheme = new TerminalTheme()
        {
            CursorStyle = CursorStyle.BlinkingBar,
            SelectionBackgroundAlpha = .5f,
            DefaultSelectionBackground = Color.FromRgb(0xbb, 0xbb, 0xbb),

            // This is Campbell.
            ColorTable = new Color[]
            {
                    Color.FromRgb(0x0C, 0x0C, 0x0C),
                    Color.FromRgb(0xA1, 0x13, 0x0E),
                    Color.FromRgb(0x0F, 0xC5, 0x1F),
                    Color.FromRgb(0x9C, 0xC1, 0x00),
                    Color.FromRgb(0x37, 0x00, 0xDA),
                    Color.FromRgb(0x17, 0x88, 0x98),
                    Color.FromRgb(0x96, 0x3A, 0xDD),
                    Color.FromRgb(0xCC, 0xCC, 0xCC),
                    Color.FromRgb(0x76, 0x76, 0x76),
                    Color.FromRgb(0xC6, 0x16, 0x0C),
                    Color.FromRgb(0x48, 0xE7, 0x56),
                    Color.FromRgb(0xF1, 0xF9, 0xA5),
                    Color.FromRgb(0x78, 0x3B, 0xFF),
                    Color.FromRgb(0xD6, 0x61, 0xD6),
                    Color.FromRgb(0x00, 0xB4, 0x9E),
                    Color.FromRgb(0xF2, 0xF2, 0xF2),
            },
        };

        private int accumulatedDelta = 0;
        private ScrollBar scrollbar;
        private TerminalContainer termContainer;

        static TerminalControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TerminalControl), new FrameworkPropertyMetadata(typeof(TerminalControl)));
        }

        /// <summary>
        /// Events raised when input on the Terminal was received.
        /// </summary>
        public event EventHandler<InputReceivedEventArgs> InputReceived;

        /// <summary>
        /// Event raised when terminal got resized.
        /// </summary>
        public event EventHandler<TerminalResizedEventArgs> TerminalResized;

        /// <summary>
        /// Gets or sets a value indicating whether if the renderer should automatically resize to fill the control
        /// on user action.
        /// </summary>
        public bool AutoResize
        {
            get => this.termContainer.AutoResize;
            set => this.termContainer.AutoResize = value;
        }

        /// <summary>
        /// Gets the current character columns available to the terminal.
        /// </summary>
        public int Columns => this.termContainer.Columns;

        /// <summary>
        /// Gets the current character rows available to the terminal.
        /// </summary>
        public int Rows => this.termContainer.Rows;

        /// <summary>
        /// Gets or sets the Theme.
        /// </summary>
        public TerminalTheme Theme
        {
            get { return (TerminalTheme)this.GetValue(ThemeProperty); }
            set { this.SetValue(ThemeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the connection to a terminal backend.
        /// </summary>
        public TerminalConnection Connection
        {
            get { return (TerminalConnection)this.GetValue(ConnectionProperty); }
            set { this.SetValue(ConnectionProperty, value); }
        }

        /// <summary>
        /// Gets size of the terminal renderer.
        /// </summary>
        private Size TerminalRendererSize
        {
            get => this.termContainer.TerminalRendererSize;
        }

        /// <summary>
        /// Gets the selected text in the terminal, clearing the selection. Otherwise returns an empty string.
        /// </summary>
        /// <returns>Selected text, empty string if no content is selected.</returns>
        public string GetSelectedText()
        {
            return this.termContainer.GetSelectedText();
        }

        /// <inheritdoc/>
        public override void OnApplyTemplate()
        {
            this.termContainer = this.Template.FindName("PART_TermContainer", this) as TerminalContainer;
            this.scrollbar = this.Template.FindName("PART_Scrollbar", this) as ScrollBar;

            this.termContainer.TerminalScrolled += this.TermControl_TerminalScrolled;
            this.termContainer.UserScrolled += this.TermControl_UserScrolled;
            this.termContainer.TerminalResized += this.TermControl_TerminalResized;
            this.termContainer.TerminalInput += this.TermControl_TerminalInput;
            this.scrollbar.MouseWheel += this.Scrollbar_MouseWheel;
            this.scrollbar.Scroll += this.Scrollbar_Scroll;
            this.GotFocus += this.TerminalControl_GotFocus;

            this.Loaded += this.TerminalControlLoaded;
        }

        /// <summary>
        /// Resizes the terminal to the specified rows and columns.
        /// </summary>
        /// <param name="rows">Number of rows to display.</param>
        /// <param name="columns">Number of columns to display.</param>
        /// <param name="cancellationToken">Cancellation token for this task.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ResizeAsync(uint rows, uint columns, CancellationToken cancellationToken)
        {
            this.termContainer.Resize(rows, columns);

#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            await this.Dispatcher.BeginInvoke(
                new Action(delegate() { this/*.terminalGrid*/.Margin = this.CalculateMargins(); }),
                System.Windows.Threading.DispatcherPriority.Render);
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
        }

        /// <summary>
        /// Resizes the terminal to the specified dimensions.
        /// </summary>
        /// <param name="rendersize">Rendering size for the terminal in device independent units.</param>
        /// <returns>A tuple of (int, int) representing the number of rows and columns in the terminal.</returns>
        public (int rows, int columns) TriggerResize(Size rendersize)
        {
            var dpiScale = VisualTreeHelper.GetDpi(this);
            rendersize.Width *= dpiScale.DpiScaleX;
            rendersize.Height *= dpiScale.DpiScaleY;

            this.termContainer.Resize(rendersize);

            return (this.Rows, this.Columns);
        }

        /// <summary>
        /// Sets the cursor position.
        /// </summary>
        /// <param name="x">X.</param>
        /// <param name="y">Y.</param>
        public void SetCursorPosition(short x, short y)
        {
            this.termContainer.SetCursorPosition(x, y);
        }

        /// <summary>
        /// Gets the sursor position.
        /// </summary>
        /// <returns>(X, Y) position.</returns>
        public (short X, short Y) GetCursorPosition()
        {
            return this.termContainer.GetCursorPosition();
        }

        /// <summary>
        /// Sends data to the Terminal.
        /// </summary>
        /// <param name="data">Data to send.</param>
        public void SendOutput(string data)
        {
            this.termContainer.TerminalSendOutput(data);
        }

        /// <summary>
        /// Implementation when input received.
        /// </summary>
        /// <param name="eventArgs">Event arguments.</param>
        protected virtual void OnInputReceived(InputReceivedEventArgs eventArgs)
        {
            this.InputReceived?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Implementation when terminal got resized.
        /// </summary>
        /// <param name="eventArgs">Event arguments.</param>
        protected virtual void OnTerminalResized(TerminalResizedEventArgs eventArgs)
        {
            this.TerminalResized?.Invoke(this, eventArgs);
        }

        /// <inheritdoc/>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            var dpiScale = VisualTreeHelper.GetDpi(this);

            // termContainer requires scaled sizes.
            this.termContainer.TerminalControlSize = new Size()
            {
                Width = (sizeInfo.NewSize.Width - this.scrollbar.ActualWidth) * dpiScale.DpiScaleX,
                Height = sizeInfo.NewSize.Height * dpiScale.DpiScaleY,
            };

            if (!this.AutoResize)
            {
                // Renderer will not resize on control resize. We have to manually calculate the margin to fill in the space.
                this/*.terminalGrid*/.Margin = this.CalculateMargins(sizeInfo.NewSize);

                // Margins stop resize events, therefore we have to manually check if more space is available and raise
                //  a resize event if needed.
                this.termContainer.RaiseResizedIfDrawSpaceIncreased();
            }

            base.OnRenderSizeChanged(sizeInfo);
        }

        /// <summary>
        /// Sets the theme for the terminal. This includes font family, size, color, as well as background and foreground colors.
        /// </summary>
        /// <param name="theme">The color theme to use in the terminal.</param>
        protected void SetTheme(TerminalTheme theme)
        {
            PresentationSource source = PresentationSource.FromVisual(this);

            if (source == null)
            {
                return;
            }

            // default colors and font from control..
            var fontFamily = this.FontFamily.FamilyNames.FirstOrDefault().Value;
            var fontSize = (short)this.FontSize;

            Color fallbackBackground, fallbackForeground;
            var brush = this.Background as SolidColorBrush;
            fallbackBackground = brush != null ? brush.Color : Colors.Black;

            brush = this.Foreground as SolidColorBrush;
            fallbackForeground = brush != null ? brush.Color : Colors.White;

            var themeInternal = theme.CreateInternal(fallbackBackground, fallbackForeground);
            this.termContainer.SetTheme(themeInternal, fontFamily, fontSize);
        }

        private static void ThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var terminalControl = (TerminalControl)d;
            if (terminalControl.termContainer != null)
            {
                terminalControl.SetTheme(e.NewValue as TerminalTheme);
            }
        }

        private static void ConnectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var terminalControl = (TerminalControl)d;
            if (e.NewValue != null)
            {
                ((TerminalConnection)e.NewValue).AttachToTerminalControl(terminalControl);
            }

            if (e.OldValue != null)
            {
                ((TerminalConnection)e.OldValue).DetachFromTerminalControl();
            }
        }

        /// <summary>
        /// Calculates the margins that should surround the terminal renderer, if any.
        /// </summary>
        /// <param name="controlSize">New size of the control. Uses the control's current size if not provided.</param>
        /// <returns>The new terminal control margin thickness in device independent units.</returns>
        private Thickness CalculateMargins(Size controlSize = default)
        {
            var dpiScale = VisualTreeHelper.GetDpi(this);
            double width = 0, height = 0;

            if (controlSize == default)
            {
                controlSize = new Size()
                {
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                };
            }

            // During initialization, the terminal renderer size will be 0 and the terminal renderer
            // draws on all available space. Therefore no margins are needed until resized.
            if (this.TerminalRendererSize.Width != 0)
            {
                width = controlSize.Width - (this.TerminalRendererSize.Width / dpiScale.DpiScaleX);
            }

            if (this.TerminalRendererSize.Height != 0)
            {
                height = controlSize.Height - (this.TerminalRendererSize.Height / dpiScale.DpiScaleY);
            }

            width -= this.scrollbar.ActualWidth;

            // Prevent negative margin size.
            width = width < 0 ? 0 : width;
            height = height < 0 ? 0 : height;

            return new Thickness(0, 0, width, height);
        }

        private void Scrollbar_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            this.TermControl_UserScrolled(sender, e.Delta);
        }

        private void Scrollbar_Scroll(object sender, ScrollEventArgs e)
        {
            var viewTop = (int)e.NewValue;
            this.termContainer.UserScroll(viewTop);
        }

        private void TermControl_TerminalScrolled(object sender, (int viewTop, int viewHeight, int bufferSize) e)
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                this.scrollbar.Minimum = 0;
                this.scrollbar.Maximum = e.bufferSize - e.viewHeight;
                this.scrollbar.Value = e.viewTop;
                this.scrollbar.ViewportSize = e.viewHeight;
            });
        }

        private void TermControl_UserScrolled(object sender, int delta)
        {
            var lineDelta = 120 / SystemParameters.WheelScrollLines;
            this.accumulatedDelta += delta;

            if (this.accumulatedDelta < lineDelta && this.accumulatedDelta > -lineDelta)
            {
                return;
            }

            this.Dispatcher.InvokeAsync(() =>
            {
                var lines = -this.accumulatedDelta / lineDelta;
                this.scrollbar.Value += lines;
                this.accumulatedDelta = 0;

                this.termContainer.UserScroll((int)this.scrollbar.Value);
            });
        }

        private void TermControl_TerminalResized(object sender, (uint rows, uint columns) e)
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                this.OnTerminalResized(new TerminalResizedEventArgs() { Rows = (int)e.rows, Columns = (int)e.columns });
            });
        }

        private void TermControl_TerminalInput(object sender, string e)
        {
            // not dispatch
            this.OnInputReceived(new InputReceivedEventArgs() { Data = e });
        }

        private void TerminalControl_GotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            this.termContainer.Focus();
        }

        private void TerminalControlLoaded(object sender, EventArgs args)
        {
            // init
            this.SetTheme(this.Theme);
        }
    }
}
