// <copyright file="TerminalThemeInternal.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Terminal.Wpf
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Structure for color handling in the terminal.
    /// </summary>
    /// <remarks>Keep in sync with HwndTerminal.hpp.</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct TerminalThemeInternal
    {
        /// <summary>
        /// The default background color of the terminal, represented in Win32 COLORREF format.
        /// </summary>
        public uint DefaultBackground;

        /// <summary>
        /// The default foreground color of the terminal, represented in Win32 COLORREF format.
        /// </summary>
        public uint DefaultForeground;

        /// <summary>
        /// The default selection background color of the terminal, represented in Win32 COLORREF format.
        /// </summary>
        public uint DefaultSelectionBackground;

        /// <summary>
        /// The opacity alpha for the selection color of the terminal, must be between 1.0 and 0.0.
        /// </summary>
        public float SelectionBackgroundAlpha;

        /// <summary>
        /// The style of cursor to use in the terminal.
        /// </summary>
        public CursorStyle CursorStyle;

        /// <summary>
        /// The color array to use for the terminal, filling the standard vt100 16 color table, represented in Win32 COLORREF format.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 16)]
        public uint[] ColorTable;
    }
}
