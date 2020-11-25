// <copyright file="InputReceivedEventArgs.cs" company="Microsoft Corporation">
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
    /// Event Arguments for terminal data received.
    /// </summary>
    public class InputReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets data recevied from Terminal.
        /// </summary>
        public string Data { get; set; }
    }
}
