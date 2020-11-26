// <copyright file="TerminalTheme.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.Terminal.Wpf
{
    using System;
    using System.Linq;
    using System.Windows.Media;

    /// <summary>
    /// Enum for the style of cursor to display in the terminal.
    /// </summary>
    public enum CursorStyle : ulong
    {
        /// <summary>
        /// Cursor will be rendered as a blinking block.
        /// </summary>
        BlinkingBlock = 0,

        /// <summary>
        /// Currently identical to <see cref="CursorStyle.BlinkingBlock"/>
        /// </summary>
        BlinkingBlockDefault = 1,

        /// <summary>
        /// Cursor will be rendered as a block that does not blink.
        /// </summary>
        SteadyBlock = 2,

        /// <summary>
        /// Cursor will be rendered as a blinking underline.
        /// </summary>
        BlinkingUnderline = 3,

        /// <summary>
        /// Cursor will be rendered as an underline that does not blink.
        /// </summary>
        SteadyUnderline = 4,

        /// <summary>
        /// Cursor will be rendered as a vertical blinking bar.
        /// </summary>
        BlinkingBar = 5,

        /// <summary>
        /// Cursor will be rendered as a vertical bar that does not blink.
        /// </summary>
        SteadyBar = 6,
    }

    /// <summary>
    /// Structure for color handling in the terminal.
    /// </summary>
    public class TerminalTheme
    {
        /// <summary>
        /// Gets or sets the default background color of the terminal, represented in Win32 COLORREF format.
        /// </summary>
        public Color DefaultBackground { get; set; }

        /// <summary>
        /// Gets or sets the default foreground color of the terminal, represented in Win32 COLORREF format.
        /// </summary>
        public Color DefaultForeground { get; set; }

        /// <summary>
        /// Gets or sets the default selection background color of the terminal.
        /// </summary>
        public Color DefaultSelectionBackground { get; set; }

        /// <summary>
        /// Gets or sets the opacity alpha for the selection color of the terminal, must be between 1.0 and 0.0.
        /// </summary>
        public float SelectionBackgroundAlpha { get; set; }

        /// <summary>
        /// Gets or sets the style of cursor to use in the terminal.
        /// </summary>
        public CursorStyle CursorStyle { get; set; }

        /// <summary>
        /// Gets or sets the color array to use for the terminal, filling the standard vt100 16 color table.
        /// </summary>
        public Color[] ColorTable { get; set; }

        /// <summary>
        /// Return the color in Win32 COLORREF.
        /// </summary>
        /// <param name="color">color.</param>
        /// <returns>Win32 COLORREF.</returns>
        internal static uint ToColorRef(Color color)
        {
            return (uint)System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.FromArgb(color.R, color.G, color.B));
        }

        /// <summary>
        /// IsUndefinedColor.
        /// </summary>
        /// <param name="color">Color.</param>
        /// <returns>if color is undefined.</returns>
        internal static bool IsUndefinedColor(Color color) => color.A == 0 && color.R == 0 && color.B == 0 && color.G == 0;

        /// <summary>
        /// Creates the internalTerminalTheme structure.
        /// </summary>
        /// <param name="fallbackBackground">Background color.</param>
        /// <param name="fallbackForeground">Foreground color.</param>
        /// <returns>TerminalTheme structure.</returns>
        internal TerminalThemeInternal CreateInternal(Color fallbackBackground, Color fallbackForeground)
        {
            if (this.ColorTable.Length != 16)
            {
                throw new ArgumentException("ColorTable needs to hold 16 colors.");
            }

            return new TerminalThemeInternal()
            {
                DefaultBackground = ToColorRef(IsUndefinedColor(this.DefaultBackground) ? fallbackBackground : this.DefaultBackground),
                DefaultForeground = ToColorRef(IsUndefinedColor(this.DefaultForeground) ? fallbackForeground : this.DefaultForeground),
                DefaultSelectionBackground = ToColorRef(this.DefaultSelectionBackground),
                SelectionBackgroundAlpha = this.SelectionBackgroundAlpha,
                CursorStyle = this.CursorStyle,
                ColorTable = this.ColorTable.Select(p => ToColorRef(p)).ToArray(),
            };
        }
    }
}
