// <copyright file="TerminalResizedEventArgs.cs" company="Microsoft Corporation">
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
    /// Event Arguments when Terminal got resized.
    /// </summary>
    public class TerminalResizedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets resized number of rows.
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// Gets or sets resized number of columns.
        /// </summary>
        public int Columns { get; set; }
    }
}
