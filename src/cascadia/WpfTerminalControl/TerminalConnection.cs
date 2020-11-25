// <copyright file="TerminalConnection.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Terminal.Wpf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Base class to implement simple connections to a terminal.
    /// </summary>
    public abstract class TerminalConnection
    {
        private TerminalControl terminalControl;

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int Rows => this.terminalControl.Rows;

        /// <summary>
        /// Gets the number of columns.
        /// </summary>
        public int Columns => this.terminalControl.Columns;

        /// <summary>
        /// Called when input is received. Override in implementions.
        /// </summary>
        /// <param name="data">Received data.</param>
        public virtual void OnInputReceived(string data)
        {
        }

        /// <summary>
        /// Sends Output to terminal.
        /// </summary>
        /// <param name="data">Data to send.</param>
        public void SendOutput(string data)
        {
            this.terminalControl?.SendOutput(data);
        }

        /// <summary>
        /// When terminal is loaded.
        /// </summary>
        public virtual void OnLoaded()
        {
        }

        /// <summary>
        /// Called when terminal gets resized.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="rows">Rows.</param>
        public virtual void OnResized(int columns, int rows)
        {
        }

        /// <summary>
        /// Gets the selected Text.
        /// </summary>
        /// <returns>Selected text.</returns>
        public string GetSelectedText()
        {
            return this.terminalControl?.GetSelectedText();
        }

        /// <summary>
        /// Gets the cusor position.
        /// </summary>
        /// <returns>position (X, Y).</returns>
        public (short X, short Y) GetCursorPosition()
        {
            if (this.terminalControl == null)
            {
                return (0, 0);
            }

            return this.terminalControl.GetCursorPosition();
        }

        /// <summary>
        /// Sets the Cursor Position.
        /// </summary>
        /// <param name="x">X.</param>
        /// <param name="y">Y.</param>
        public void SetCursorPosition(short x, short y)
        {
            this.terminalControl?.SetCursorPosition(x, y);
        }

        /// <summary>
        /// Attaches control.
        /// </summary>
        /// <param name="terminalControl">terminal control.</param>
        internal void AttachToTerminalControl(TerminalControl terminalControl)
        {
            this.terminalControl = terminalControl;
            this.terminalControl.Loaded += this.TerminalControl_OnLoaded;
            this.terminalControl.InputReceived += this.TerminalControl_InputReceived;
            this.terminalControl.TerminalResized += this.TerminalControl_TerminalResized;
        }

        /// <summary>
        /// Detaches from control.
        /// </summary>
        internal void DetachFromTerminalControl()
        {
            this.terminalControl.Loaded -= this.TerminalControl_OnLoaded;
            this.terminalControl.InputReceived -= this.TerminalControl_InputReceived;
            this.terminalControl.TerminalResized -= this.TerminalControl_TerminalResized;
            this.terminalControl = null;
        }

        private void TerminalControl_OnLoaded(object sender, EventArgs args)
        {
            this.OnLoaded();
        }

        private void TerminalControl_TerminalResized(object sender, TerminalResizedEventArgs e)
        {
            this.OnResized(e.Columns, e.Rows);
        }

        private void TerminalControl_InputReceived(object sender, InputReceivedEventArgs e)
        {
            this.OnInputReceived(e.Data);
        }
    }
}
