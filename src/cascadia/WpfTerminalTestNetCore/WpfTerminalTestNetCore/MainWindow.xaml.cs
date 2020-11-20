// <copyright file="MainWindow.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Terminal.Wpf;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WpfTerminalTestNetCore
{
    public class EchoConnection : Microsoft.Terminal.Wpf.ITerminalConnection
    {
        public event EventHandler<TerminalOutputEventArgs> TerminalOutput;

        public void Resize(uint rows, uint columns)
        {
            return;
        }

        public void Start()
        {
            TerminalOutput.Invoke(this, new TerminalOutputEventArgs("ECHO CONNECTION\r\n^A: toggle printable ESC\r\n^B: toggle SGR mouse mode\r\n^C: toggle win32 input mode\r\n\r\n"));
            return;
        }

        private bool _escapeMode;
        private bool _mouseMode;
        private bool _win32InputMode;

        public void WriteInput(string data)
        {
            if (data.Length == 0)
            {
                return;
            }

            if (data[0] == '\x01') // ^A
            {
                _escapeMode = !_escapeMode;
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"Printable ESC mode: {_escapeMode}\r\n"));
            }
            else if (data[0] == '\x02') // ^B
            {
                _mouseMode = !_mouseMode;
                var decSet = _mouseMode ? "h" : "l";
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"\x1b[?1003{decSet}\x1b[?1006{decSet}"));
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"SGR Mouse mode (1003, 1006): {_mouseMode}\r\n"));
            }
            else if ((data[0] == '\x03') ||
                     (data == "\x1b[67;46;3;1;8;1_")) // ^C
            {
                _win32InputMode = !_win32InputMode;
                var decSet = _win32InputMode ? "h" : "l";
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"\x1b[?9001{decSet}"));
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"Win32 input mode: {_win32InputMode}\r\n"));

                // If escape mode isn't currently enabled, turn it on now.
                if (_win32InputMode && !_escapeMode)
                {
                    _escapeMode = true;
                    TerminalOutput.Invoke(this, new TerminalOutputEventArgs($"Printable ESC mode: {_escapeMode}\r\n"));
                }
            }
            else
            {
                // Echo back to the terminal, but make backspace/newline work properly.
                var str = data.Replace("\r", "\r\n").Replace("\x7f", "\x08 \x08");
                if (_escapeMode)
                {
                    str = str.Replace("\x1b", "\u241b");
                }
                TerminalOutput.Invoke(this, new TerminalOutputEventArgs(str));
            }
        }

        public void Close()
        {
            return;
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //Terminal.Loaded += Terminal_Loaded;

            Terminal.Theme = new TerminalTheme
            {
                DefaultBackground = Color.FromRgb(0x0c, 0x0c, 0x0c),
                DefaultForeground = Color.FromRgb(0xcc, 0xcc, 0xcc),
                CursorStyle = CursorStyle.BlinkingBar,
                SelectionBackgroundAlpha = .5f,
                DefaultSelectionBackground = Color.FromRgb(0xbb, 0xbb, 0xbb),
                // This is Campbell.
                ColorTable = new Color[] { 
                    Color.FromRgb(0x0C, 0x0C, 0x0C),
                    Color.FromRgb(0xA1, 0x13, 0x0E),
                    Color.FromRgb(0x0F, 0xC5, 0x1F),
                    Color.FromRgb(0x9C, 0xC1, 0x00),
                    Color.FromRgb(0x37, 0x00, 0xDA),
                    Color.FromRgb(0x17, 0x88, 0x98),
                    Color.FromRgb(0x96, 0x3A, 0xDD),
                    Color.FromRgb(0xCC, 0xCC, 0xCC),
                    Color.FromRgb(0x76, 0x76, 0x76),
                    Color.FromRgb(0x48, 0xE7, 0x56),
                    Color.FromRgb(0xC6, 0x16, 0x0C),
                    Color.FromRgb(0xF1, 0xF9, 0xA5),
                    Color.FromRgb(0x78, 0x3B, 0xFF),
                    Color.FromRgb(0x00, 0xB4, 0x9E),
                    Color.FromRgb(0xD6, 0x61, 0xD6),
                    Color.FromRgb(0xF2, 0xF2, 0xF2)}
            };

            Terminal.Connection = new EchoConnection();

            this.Loaded += (sender, args) =>
            {
                Terminal.Focus();
            };
        }

        private void ResizeTest(object sender, RoutedEventArgs e)
        {
            Terminal.ResizeAsync(10, 80, CancellationToken.None)
                .ConfigureAwait(false);
        }

        private void ResetSize(object sender, RoutedEventArgs e)
        {
            Terminal.Margin = new Thickness(0);
        }

        private void ColorTest(object sender, RoutedEventArgs e)
        {
            string data = "\n\x1b[30mBlack" +
                "\x1b[31mRed" +
                "\x1b[32mGreen" +
                "\x1b[33mYellow" +
                "\x1b[34mBlue" +
                "\x1b[35mMagent" +
                "\x1b[36mCyan" +
                "\x1b[37mWhite" +
                "\x1b[30;1mBlack" +
                "\x1b[31;1mRef" +
                "\x1b[32;1mGreen" +
                "\x1b[33;1mYellow" +
                "\x1b[34;1mBlue" +
                "\x1b[35;1mMagenta" +
                "\x1b[36;1mCyan" +
                "\x1b[37;1mWhite" +
                "\x1b[0mDefault Color";
            Terminal.Connection.WriteInput(data);
            Terminal.Focus();
        }

        //private void Terminal_Loaded(object sender, RoutedEventArgs e)
        //{
        //    var theme = new TerminalTheme
        //    {
        //        DefaultBackground = 0x0c0c0c,
        //        DefaultForeground = 0xcccccc,
        //        CursorStyle = CursorStyle.BlinkingBar,
        //        // This is Campbell.
        //        ColorTable = new uint[] { 0x0C0C0C, 0x1F0FC5, 0x0EA113, 0x009CC1, 0xDA3700, 0x981788, 0xDD963A, 0xCCCCCC, 0x767676, 0x5648E7, 0x0CC616, 0xA5F1F9, 0xFF783B, 0x9E00B4, 0xD6D661, 0xF2F2F2 },
        //    };

        //    Terminal.Connection = new EchoConnection();
        //    Terminal.SetTheme(theme, "Cascadia Code", 12);
        //    Terminal.Focus();
        //}
    }
}
