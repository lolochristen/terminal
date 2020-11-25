// <copyright file="EchoTerminalConnection.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Terminal.Wpf
{
    /// <summary>
    /// Simple echo terminal connection.
    /// </summary>
    public class EchoTerminalConnection : TerminalConnection
    {
        /// <inheritdoc />
        public override void OnInputReceived(string data)
        {
            data = data.Replace("\n", "\r\n");
            this.SendOutput(data);
        }
    }
}
