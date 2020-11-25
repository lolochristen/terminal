// <copyright file="MainWindow.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

using Microsoft.Terminal.Wpf;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WpfTerminalTestNetCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _escapeMode;
        private bool _mouseMode;
        private bool _win32InputMode;

        public MainWindow()
        {
            InitializeComponent();

            Console.SetOut(new TerminalTextWriter(Terminal));

            this.Loaded += (sender, args) =>
            {
                Console.WriteLine("Hello via Console.WriteLine");
                Terminal.SendOutput("\r\n^A: toggle printable ESC\r\n^B: toggle SGR mouse mode\r\n^C: toggle win32 input mode\r\n\r\n");
                Terminal.Focus();
            };

            //Terminal.Connection = new EchoTerminalConnection();

            Terminal.InputReceived += (sender, args) =>
            {
                var data = args.Data;
                if (data.Length == 0)
                {
                    return;
                }

                if (data[0] == '\x01') // ^A
                {
                    _escapeMode = !_escapeMode;

                    Terminal.SendOutput($"Printable ESC mode: {_escapeMode}\r\n");
                }
                else if (data[0] == '\x02') // ^B
                {
                    _mouseMode = !_mouseMode;
                    var decSet = _mouseMode ? "h" : "l";
                    Terminal.SendOutput($"\x1b[?1003{decSet}\x1b[?1006{decSet}");
                    Terminal.SendOutput($"SGR Mouse mode (1003, 1006): {_mouseMode}\r\n");
                }
                else if ((data[0] == '\x03') ||
                         (data == "\x1b[67;46;3;1;8;1_")) // ^C
                {
                    _win32InputMode = !_win32InputMode;
                    var decSet = _win32InputMode ? "h" : "l";
                    Terminal.SendOutput($"\x1b[?9001{decSet}");
                    Terminal.SendOutput($"Win32 input mode: {_win32InputMode}\r\n");

                    // If escape mode isn't currently enabled, turn it on now.
                    if (_win32InputMode && !_escapeMode)
                    {
                        _escapeMode = true;
                        Terminal.SendOutput($"Printable ESC mode: {_escapeMode}\r\n");
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
                    Terminal.SendOutput(str);
                }
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
                "\x1b[0mDefault Color\n";
            Terminal.SendOutput(data);
            Terminal.Focus();
        }

        private void PositionTest(object sender, RoutedEventArgs e)
        {
            var pos = Terminal.GetCursorPosition();
            Terminal.SendOutput($"({pos.X},{pos.Y})");
            Terminal.SetCursorPosition(20, 10);
            Terminal.Focus();

            var line = Console.Read();
        }
    }

    public class TerminalTextWriter : TextWriter
    {
        private readonly TerminalControl _terminalControl;
        private TerminalConnection _terminalConnection;

        public TerminalTextWriter(TerminalControl terminalControl)
        {
            _terminalControl = terminalControl;
        }

        public TerminalTextWriter(TerminalConnection terminalConnection)
        {
            _terminalConnection = terminalConnection;
        }

        public override Encoding Encoding => Encoding.Default;

        public override void Write(string value)
        {
            if (_terminalConnection != null)
                _terminalConnection.SendOutput(value);
            else if (_terminalControl != null)
                _terminalControl.SendOutput(value);
        }

        public override void WriteLine(string value)
        {
            Write(value + "\r\n");
        }
    }
}
