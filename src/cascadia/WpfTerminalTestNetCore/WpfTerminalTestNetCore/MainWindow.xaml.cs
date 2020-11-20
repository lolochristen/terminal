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
    }
}
