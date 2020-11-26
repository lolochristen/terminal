// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#include "pch.h"
#include "CommandPalette.h"

#include <LibraryResources.h>

#include "CommandPalette.g.cpp"

using namespace winrt;
using namespace winrt::TerminalApp;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::System;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Microsoft::Terminal::Settings::Model;

namespace winrt::TerminalApp::implementation
{
    CommandPalette::CommandPalette() :
        _switcherStartIdx{ 0 }
    {
        InitializeComponent();

        _filteredActions = winrt::single_threaded_observable_vector<winrt::TerminalApp::FilteredCommand>();
        _nestedActionStack = winrt::single_threaded_vector<winrt::TerminalApp::FilteredCommand>();
        _currentNestedCommands = winrt::single_threaded_vector<winrt::TerminalApp::FilteredCommand>();
        _allCommands = winrt::single_threaded_vector<winrt::TerminalApp::FilteredCommand>();
        _tabActions = winrt::single_threaded_vector<winrt::TerminalApp::FilteredCommand>();
        _commandLineHistory = winrt::single_threaded_vector<winrt::TerminalApp::FilteredCommand>();

        _switchToMode(CommandPaletteMode::ActionMode);

        if (CommandPaletteShadow())
        {
            // Hook up the shadow on the command palette to the backdrop that
            // will actually show it. This needs to be done at runtime, and only
            // if the shadow actually exists. ThemeShadow isn't supported below
            // version 18362.
            CommandPaletteShadow().Receivers().Append(_shadowBackdrop());
            // "raise" the command palette up by 16 units, so it will cast a shadow.
            _backdrop().Translation({ 0, 0, 16 });
        }

        // Whatever is hosting us will enable us by setting our visibility to
        // "Visible". When that happens, set focus to our search box.
        RegisterPropertyChangedCallback(UIElement::VisibilityProperty(), [this](auto&&, auto&&) {
            if (Visibility() == Visibility::Visible)
            {
                if (_currentMode == CommandPaletteMode::TabSwitchMode)
                {
                    _searchBox().Visibility(Visibility::Collapsed);
                    _filteredActionsView().SelectedIndex(_switcherStartIdx);
                    _filteredActionsView().ScrollIntoView(_filteredActionsView().SelectedItem());
                    _filteredActionsView().Focus(FocusState::Keyboard);

                    // Do this right after becoming visible so we can quickly catch scenarios where
                    // modifiers aren't held down (e.g. command palette invocation).
                    _anchorKeyUpHandler();
                }
                else
                {
                    _filteredActionsView().SelectedIndex(0);
                    _searchBox().Focus(FocusState::Programmatic);
                    _updateFilteredActions();
                }

                TraceLoggingWrite(
                    g_hTerminalAppProvider, // handle to TerminalApp tracelogging provider
                    "CommandPaletteOpened",
                    TraceLoggingDescription("Event emitted when the Command Palette is opened"),
                    TraceLoggingWideString(L"Action", "Mode", "which mode the palette was opened in"),
                    TraceLoggingKeyword(MICROSOFT_KEYWORD_MEASURES),
                    TelemetryPrivacyDataTag(PDT_ProductAndServicePerformance));
            }
            else
            {
                // Raise an event to return control to the Terminal.
                _dismissPalette();
            }
        });

        // Focusing the ListView when the Command Palette control is set to Visible
        // for the first time fails because the ListView hasn't finished loading by
        // the time Focus is called. Luckily, We can listen to SizeChanged to know
        // when the ListView has been measured out and is ready, and we'll immediately
        // revoke the handler because we only needed to handle it once on initialization.
        _sizeChangedRevoker = _filteredActionsView().SizeChanged(winrt::auto_revoke, [this](auto /*s*/, auto /*e*/) {
            if (_currentMode == CommandPaletteMode::TabSwitchMode)
            {
                _filteredActionsView().Focus(FocusState::Keyboard);
            }
            _sizeChangedRevoker.revoke();
        });

        _filteredActionsView().SelectionChanged({ this, &CommandPalette::_selectedCommandChanged });
    }

    // Method Description:
    // - Moves the focus up or down the list of commands. If we're at the top,
    //   we'll loop around to the bottom, and vice-versa.
    // Arguments:
    // - moveDown: if true, we're attempting to move to the next item in the
    //   list. Otherwise, we're attempting to move to the previous.
    // Return Value:
    // - <none>
    void CommandPalette::SelectNextItem(const bool moveDown)
    {
        const auto selected = _filteredActionsView().SelectedIndex();
        const int numItems = ::base::saturated_cast<int>(_filteredActionsView().Items().Size());
        // Wraparound math. By adding numItems and then calculating modulo numItems,
        // we clamp the values to the range [0, numItems) while still supporting moving
        // upward from 0 to numItems - 1.
        const auto newIndex = ((numItems + selected + (moveDown ? 1 : -1)) % numItems);
        _filteredActionsView().SelectedIndex(newIndex);
        _filteredActionsView().ScrollIntoView(_filteredActionsView().SelectedItem());
    }

    // Method Description:
    // - Scroll the command palette to the specified index
    // Arguments:
    // - index within a list view of commands
    // Return Value:
    // - <none>
    void CommandPalette::_scrollToIndex(uint32_t index)
    {
        auto numItems = _filteredActionsView().Items().Size();

        if (numItems == 0)
        {
            // if the list is empty no need to scroll
            return;
        }

        auto clampedIndex = std::clamp<int32_t>(index, 0, numItems - 1);
        _filteredActionsView().SelectedIndex(clampedIndex);
        _filteredActionsView().ScrollIntoView(_filteredActionsView().SelectedItem());
    }

    // Method Description:
    // - Computes the number of visible commands
    // Arguments:
    // - <none>
    // Return Value:
    // - the approximate number of items visible in the list (in other words the size of the page)
    uint32_t CommandPalette::_getNumVisibleItems()
    {
        const auto container = _filteredActionsView().ContainerFromIndex(0);
        const auto item = container.try_as<winrt::Windows::UI::Xaml::Controls::ListViewItem>();
        const auto itemHeight = ::base::saturated_cast<int>(item.ActualHeight());
        const auto listHeight = ::base::saturated_cast<int>(_filteredActionsView().ActualHeight());
        return listHeight / itemHeight;
    }

    // Method Description:
    // - Scrolls the focus one page up the list of commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::ScrollPageUp()
    {
        auto selected = _filteredActionsView().SelectedIndex();
        auto numVisibleItems = _getNumVisibleItems();
        _scrollToIndex(selected - numVisibleItems);
    }

    // Method Description:
    // - Scrolls the focus one page down the list of commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::ScrollPageDown()
    {
        auto selected = _filteredActionsView().SelectedIndex();
        auto numVisibleItems = _getNumVisibleItems();
        _scrollToIndex(selected + numVisibleItems);
    }

    // Method Description:
    // - Moves the focus to the top item in the list of commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::ScrollToTop()
    {
        _scrollToIndex(0);
    }

    // Method Description:
    // - Moves the focus to the bottom item in the list of commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::ScrollToBottom()
    {
        _scrollToIndex(_filteredActionsView().Items().Size() - 1);
    }

    // Method Description:
    // - Called when the command selection changes. We'll use this in the tab
    //   switcher to "preview" tabs as the user navigates the list of tabs. To
    //   do that, we'll dispatch the switch to tab command for this tab, but not
    //   dismiss the switcher.
    // Arguments:
    // - <unused>
    // Return Value:
    // - <none>
    void CommandPalette::_selectedCommandChanged(const IInspectable& /*sender*/,
                                                 const Windows::UI::Xaml::RoutedEventArgs& /*args*/)
    {
        if (_currentMode == CommandPaletteMode::TabSwitchMode)
        {
            const auto selectedCommand = _filteredActionsView().SelectedItem();
            if (const auto filteredCommand = selectedCommand.try_as<winrt::TerminalApp::FilteredCommand>())
            {
                const auto& actionAndArgs = filteredCommand.Command().Action();
                _dispatch.DoAction(actionAndArgs);
            }
        }
    }

    void CommandPalette::_previewKeyDownHandler(IInspectable const& /*sender*/,
                                                Windows::UI::Xaml::Input::KeyRoutedEventArgs const& e)
    {
        auto key = e.OriginalKey();

        // Some keypresses such as Tab, Return, Esc, and Arrow Keys are ignored by controls because
        // they're not considered input key presses. While they don't raise KeyDown events,
        // they do raise PreviewKeyDown events.
        //
        // Only give anchored tab switcher the ability to cycle through tabs with the tab button.
        // For unanchored mode, accessibility becomes an issue when we try to hijack tab since it's
        // a really widely used keyboard navigation key.
        if (_currentMode == CommandPaletteMode::TabSwitchMode && key == VirtualKey::Tab)
        {
            auto const state = CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Shift);
            if (WI_IsFlagSet(state, CoreVirtualKeyStates::Down))
            {
                SelectNextItem(false);
                e.Handled(true);
            }
            else
            {
                SelectNextItem(true);
                e.Handled(true);
            }
        }
    }

    // Method Description:
    // - Process keystrokes in the input box. This is used for moving focus up
    //   and down the list of commands in Action mode, and for executing
    //   commands in both Action mode and Commandline mode.
    // Arguments:
    // - e: the KeyRoutedEventArgs containing info about the keystroke.
    // Return Value:
    // - <none>
    void CommandPalette::_keyDownHandler(IInspectable const& /*sender*/,
                                         Windows::UI::Xaml::Input::KeyRoutedEventArgs const& e)
    {
        auto key = e.OriginalKey();

        if (key == VirtualKey::Up)
        {
            // Action Mode: Move focus to the next item in the list.
            SelectNextItem(false);
            e.Handled(true);
        }
        else if (key == VirtualKey::Down)
        {
            // Action Mode: Move focus to the previous item in the list.
            SelectNextItem(true);
            e.Handled(true);
        }
        else if (key == VirtualKey::PageUp)
        {
            // Action Mode: Move focus to the first visible item in the list.
            ScrollPageUp();
            e.Handled(true);
        }
        else if (key == VirtualKey::PageDown)
        {
            // Action Mode: Move focus to the last visible item in the list.
            ScrollPageDown();
            e.Handled(true);
        }
        else if (key == VirtualKey::Home)
        {
            // Action Mode: Move focus to the first item in the list.
            ScrollToTop();
            e.Handled(true);
        }
        else if (key == VirtualKey::End)
        {
            // Action Mode: Move focus to the last item in the list.
            ScrollToBottom();
            e.Handled(true);
        }
        else if (key == VirtualKey::Enter)
        {
            const auto selectedCommand = _filteredActionsView().SelectedItem();
            const auto filteredCommand = selectedCommand.try_as<winrt::TerminalApp::FilteredCommand>();
            _dispatchCommand(filteredCommand);
            e.Handled(true);
        }
        else if (key == VirtualKey::Escape)
        {
            // Dismiss the palette if the text is empty, otherwise clear the
            // search string.
            if (_searchBox().Text().empty())
            {
                _dismissPalette();
            }
            else
            {
                _searchBox().Text(L"");
            }

            e.Handled(true);
        }
        else if (key == VirtualKey::Back)
        {
            // If the last filter text was empty, and we're backspacing from
            // that state, then the user "backspaced" the virtual '>' we're
            // using as the action mode indicator. Switch into commandline mode.
            if (_searchBox().Text().empty() && _lastFilterTextWasEmpty && _currentMode == CommandPaletteMode::ActionMode)
            {
                _switchToMode(CommandPaletteMode::CommandlineMode);
            }

            e.Handled(true);
        }
        else
        {
            const auto vkey = ::gsl::narrow_cast<WORD>(e.OriginalKey());

            // In the interest of not telling all modes to check for keybindings, limit to TabSwitch mode for now.
            if (_currentMode == CommandPaletteMode::TabSwitchMode)
            {
                auto const ctrlDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Control), CoreVirtualKeyStates::Down);
                auto const altDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Menu), CoreVirtualKeyStates::Down);
                auto const shiftDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Shift), CoreVirtualKeyStates::Down);

                auto success = _bindings.TryKeyChord({
                    ctrlDown,
                    altDown,
                    shiftDown,
                    vkey,
                });

                if (success)
                {
                    e.Handled(true);
                }
            }
        }
    }

    // Method Description:
    // - Implements the Alt handler
    // Return value:
    // - whether the key was handled
    bool CommandPalette::OnDirectKeyEvent(const uint32_t vkey, const uint8_t /*scanCode*/, const bool down)
    {
        auto handled = false;
        if (_currentMode == CommandPaletteMode::TabSwitchMode)
        {
            if (vkey == VK_MENU && !down)
            {
                _anchorKeyUpHandler();
                handled = true;
            }
        }
        return handled;
    }

    void CommandPalette::_keyUpHandler(IInspectable const& /*sender*/,
                                       Windows::UI::Xaml::Input::KeyRoutedEventArgs const& e)
    {
        if (_currentMode == CommandPaletteMode::TabSwitchMode)
        {
            _anchorKeyUpHandler();
            e.Handled(true);
        }
    }

    // Method Description:
    // - Handles anchor key ups during TabSwitchMode.
    //   We assume that at least one modifier key should be held down in order to "anchor"
    //   the ATS UI in place. So this function is called to check if any modifiers are
    //   still held down, and if not, dispatch the selected tab action and close the ATS.
    // Return value:
    // - <none>
    void CommandPalette::_anchorKeyUpHandler()
    {
        auto const ctrlDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Control), CoreVirtualKeyStates::Down);
        auto const altDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Menu), CoreVirtualKeyStates::Down);
        auto const shiftDown = WI_IsFlagSet(CoreWindow::GetForCurrentThread().GetKeyState(winrt::Windows::System::VirtualKey::Shift), CoreVirtualKeyStates::Down);

        if (!ctrlDown && !altDown && !shiftDown)
        {
            const auto selectedCommand = _filteredActionsView().SelectedItem();
            if (const auto filteredCommand = selectedCommand.try_as<winrt::TerminalApp::FilteredCommand>())
            {
                _dispatchCommand(filteredCommand);
            }
        }
    }

    // Method Description:
    // - This event is triggered when someone clicks anywhere in the bounds of
    //   the window that's _not_ the command palette UI. When that happens,
    //   we'll want to dismiss the palette.
    // Arguments:
    // - <unused>
    // Return Value:
    // - <none>
    void CommandPalette::_rootPointerPressed(Windows::Foundation::IInspectable const& /*sender*/,
                                             Windows::UI::Xaml::Input::PointerRoutedEventArgs const& /*e*/)
    {
        _dismissPalette();
    }

    // Method Description:
    // - This event is only triggered when someone clicks in the space right
    //   next to the text box in the command palette. We _don't_ want that click
    //   to light dismiss the palette, so we'll mark it handled here.
    // Arguments:
    // - e: the PointerRoutedEventArgs that we want to mark as handled
    // Return Value:
    // - <none>
    void CommandPalette::_backdropPointerPressed(Windows::Foundation::IInspectable const& /*sender*/,
                                                 Windows::UI::Xaml::Input::PointerRoutedEventArgs const& e)
    {
        e.Handled(true);
    }

    // Method Description:
    // - This event is called when the user clicks on an individual item from
    //   the list. We'll get the item that was clicked and dispatch the command
    //   that the user clicked on.
    // Arguments:
    // - e: an ItemClickEventArgs who's ClickedItem() will be the command that was clicked on.
    // Return Value:
    // - <none>
    void CommandPalette::_listItemClicked(Windows::Foundation::IInspectable const& /*sender*/,
                                          Windows::UI::Xaml::Controls::ItemClickEventArgs const& e)
    {
        const auto selectedCommand = e.ClickedItem();
        if (const auto filteredCommand = selectedCommand.try_as<winrt::TerminalApp::FilteredCommand>())
        {
            _dispatchCommand(filteredCommand);
        }
    }

    // Method Description:
    // This event is called when the user clicks on an ChevronLeft button right
    // next to the ParentCommandName (e.g. New Tab...) above the subcommands list.
    // It'll go up a level when the users click the button.
    // Arguments:
    // - sender: the button that got clicked
    // Return Value:
    // - <none>
    void CommandPalette::_moveBackButtonClicked(Windows::Foundation::IInspectable const& /*sender*/,
                                                Windows::UI::Xaml::RoutedEventArgs const&)
    {
        _nestedActionStack.Clear();
        ParentCommandName(L"");
        _currentNestedCommands.Clear();
        _searchBox().Focus(FocusState::Programmatic);
        _updateFilteredActions();
        _filteredActionsView().SelectedIndex(0);
    }

    // Method Description:
    // - This is called when the user selects a command with subcommands. It
    //   will update our UI to now display the list of subcommands instead, and
    //   clear the search text so the user can search from the new list of
    //   commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::_updateUIForStackChange()
    {
        if (_searchBox().Text().empty())
        {
            // Manually call _filterTextChanged, because setting the text to the
            // empty string won't update it for us (as it won't actually change value.)
            _filterTextChanged(nullptr, nullptr);
        }

        // Changing the value of the search box will trigger _filterTextChanged,
        // which will cause us to refresh the list of filterable commands.
        _searchBox().Text(L"");
        _searchBox().Focus(FocusState::Programmatic);
    }

    // Method Description:
    // - Retrieve the list of commands that we should currently be filtering.
    //   * If the user has command with subcommands, this will return that command's subcommands.
    //   * If we're in Tab Switcher mode, return the tab actions.
    //   * Otherwise, just return the list of all the top-level commands.
    // Arguments:
    // - <none>
    // Return Value:
    // - A list of Commands to filter.
    Collections::IVector<winrt::TerminalApp::FilteredCommand> CommandPalette::_commandsToFilter()
    {
        switch (_currentMode)
        {
        case CommandPaletteMode::ActionMode:
            if (_nestedActionStack.Size() > 0)
            {
                return _currentNestedCommands;
            }

            return _allCommands;
        case CommandPaletteMode::TabSearchMode:
            return _tabActions;
        case CommandPaletteMode::TabSwitchMode:
            return _tabActions;
        case CommandPaletteMode::CommandlineMode:
            return _commandLineHistory;
        default:
            return _allCommands;
        }
    }

    // Method Description:
    // - Helper method for retrieving the action from a command the user
    //   selected, and dispatching that command. Also fires a tracelogging event
    //   indicating that the user successfully found the action they were
    //   looking for.
    // Arguments:
    // - command: the Command to dispatch. This might be null.
    // Return Value:
    // - <none>
    void CommandPalette::_dispatchCommand(winrt::TerminalApp::FilteredCommand const& filteredCommand)
    {
        if (_currentMode == CommandPaletteMode::CommandlineMode)
        {
            _dispatchCommandline(filteredCommand);
        }
        else if (filteredCommand)
        {
            if (filteredCommand.Command().HasNestedCommands())
            {
                // If this Command had subcommands, then don't dispatch the
                // action. Instead, display a new list of commands for the user
                // to pick from.
                _nestedActionStack.Append(filteredCommand);
                ParentCommandName(filteredCommand.Command().Name());
                _currentNestedCommands.Clear();
                for (const auto& nameAndCommand : filteredCommand.Command().NestedCommands())
                {
                    const auto action = nameAndCommand.Value();
                    auto nestedFilteredCommand{ winrt::make<FilteredCommand>(action) };
                    _currentNestedCommands.Append(nestedFilteredCommand);
                }

                _updateUIForStackChange();
            }
            else
            {
                // First stash the search text length, because _close will clear this.
                const auto searchTextLength = _searchBox().Text().size();

                // An action from the root command list has depth=0
                const auto nestedCommandDepth = _nestedActionStack.Size();

                // Close before we dispatch so that actions that open the command
                // palette like the Tab Switcher will be able to have the last laugh.
                _close();

                const auto actionAndArgs = filteredCommand.Command().Action();
                _dispatch.DoAction(actionAndArgs);

                TraceLoggingWrite(
                    g_hTerminalAppProvider, // handle to TerminalApp tracelogging provider
                    "CommandPaletteDispatchedAction",
                    TraceLoggingDescription("Event emitted when the user selects an action in the Command Palette"),
                    TraceLoggingUInt32(searchTextLength, "SearchTextLength", "Number of characters in the search string"),
                    TraceLoggingUInt32(nestedCommandDepth, "NestedCommandDepth", "the depth in the tree of commands for the dispatched action"),
                    TraceLoggingKeyword(MICROSOFT_KEYWORD_MEASURES),
                    TelemetryPrivacyDataTag(PDT_ProductAndServicePerformance));
            }
        }
    }
    // Method Description:
    // - Get all the input text in _searchBox that follows any leading spaces.
    // Arguments:
    // - <none>
    // Return Value:
    // - the string of input following any number of leading spaces
    std::wstring CommandPalette::_getTrimmedInput()
    {
        const std::wstring input{ _searchBox().Text() };
        if (input.empty())
        {
            return input;
        }

        // Trim leading whitespace
        const auto firstNonSpace = input.find_first_not_of(L" ");
        if (firstNonSpace == std::wstring::npos)
        {
            // All the following characters are whitespace.
            return L"";
        }

        return input.substr(firstNonSpace);
    }

    // Method Description:
    // - Dispatch the current search text as a ExecuteCommandline action.
    // Arguments:
    // - filteredCommand - Selected filtered command - might be null
    // Return Value:
    // - <none>
    void CommandPalette::_dispatchCommandline(winrt::TerminalApp::FilteredCommand const& command)
    {
        const auto filteredCommand = command ? command : _buildCommandLineCommand(_getTrimmedInput());
        if (filteredCommand.has_value())
        {
            if (_commandLineHistory.Size() == CommandLineHistoryLength)
            {
                _commandLineHistory.RemoveAtEnd();
            }
            _commandLineHistory.InsertAt(0, filteredCommand.value());

            TraceLoggingWrite(
                g_hTerminalAppProvider, // handle to TerminalApp tracelogging provider
                "CommandPaletteDispatchedCommandline",
                TraceLoggingDescription("Event emitted when the user runs a commandline in the Command Palette"),
                TraceLoggingKeyword(MICROSOFT_KEYWORD_MEASURES),
                TelemetryPrivacyDataTag(PDT_ProductAndServicePerformance));

            if (_dispatch.DoAction(filteredCommand.value().Command().Action()))
            {
                _close();
            }
        }
    }

    std::optional<winrt::TerminalApp::FilteredCommand> CommandPalette::_buildCommandLineCommand(std::wstring const& commandLine)
    {
        if (commandLine.empty())
        {
            return std::nullopt;
        }

        // Build the ExecuteCommandline action from the values we've parsed on the commandline.
        ExecuteCommandlineArgs args{ commandLine };
        ActionAndArgs executeActionAndArgs{ ShortcutAction::ExecuteCommandline, args };
        Command command{};
        command.Action(executeActionAndArgs);
        command.Name(commandLine);
        return winrt::make<FilteredCommand>(command);
    }

    // Method Description:
    // - Helper method for closing the command palette, when the user has _not_
    //   selected an action. Also fires a tracelogging event indicating that the
    //   user closed the palette without running a command.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::_dismissPalette()
    {
        _close();

        TraceLoggingWrite(
            g_hTerminalAppProvider, // handle to TerminalApp tracelogging provider
            "CommandPaletteDismissed",
            TraceLoggingDescription("Event emitted when the user dismisses the Command Palette without selecting an action"),
            TraceLoggingKeyword(MICROSOFT_KEYWORD_MEASURES),
            TelemetryPrivacyDataTag(PDT_ProductAndServicePerformance));
    }

    // Method Description:
    // - Event handler for when the text in the input box changes. In Action
    //   Mode, we'll update the list of displayed commands, and select the first one.
    // Arguments:
    // - <unused>
    // Return Value:
    // - <none>
    void CommandPalette::_filterTextChanged(IInspectable const& /*sender*/,
                                            Windows::UI::Xaml::RoutedEventArgs const& /*args*/)
    {
        if (_currentMode == CommandPaletteMode::CommandlineMode)
        {
            _evaluatePrefix();
        }

        // We're setting _lastFilterTextWasEmpty here, because if the user tries
        // to backspace the last character in the input, the Backspace KeyDown
        // event will fire _before_ _filterTextChanged does. Updating the value
        // here will ensure that we can check this case appropriately.
        _lastFilterTextWasEmpty = _searchBox().Text().empty();

        _updateFilteredActions();

        // In the command line mode we want the user to explicitly select the command
        _filteredActionsView().SelectedIndex(_currentMode == CommandPaletteMode::CommandlineMode ? -1 : 0);

        if (_currentMode == CommandPaletteMode::TabSearchMode || _currentMode == CommandPaletteMode::ActionMode)
        {
            _noMatchesText().Visibility(_filteredActions.Size() > 0 ? Visibility::Collapsed : Visibility::Visible);
        }
        else
        {
            _noMatchesText().Visibility(Visibility::Collapsed);
        }
    }

    void CommandPalette::_evaluatePrefix()
    {
        // This will take you from commandline mode, into action mode. The
        // backspace handler in _keyDownHandler will handle taking us from
        // action mode to commandline mode.
        auto newMode = CommandPaletteMode::CommandlineMode;

        auto inputText = _getTrimmedInput();
        if (inputText.size() > 0)
        {
            if (inputText[0] == L'>')
            {
                newMode = CommandPaletteMode::ActionMode;
            }
        }

        if (newMode != _currentMode)
        {
            //_switchToMode will remove the '>' character from the input.
            _switchToMode(newMode);
        }
    }

    Collections::IObservableVector<winrt::TerminalApp::FilteredCommand> CommandPalette::FilteredActions()
    {
        return _filteredActions;
    }

    void CommandPalette::SetKeyBindings(Microsoft::Terminal::TerminalControl::IKeyBindings bindings)
    {
        _bindings = bindings;
    }

    void CommandPalette::SetCommands(Collections::IVector<Command> const& actions)
    {
        _populateFilteredActions(_allCommands, actions);
        _updateFilteredActions();
    }

    void CommandPalette::SetTabActions(Collections::IVector<Command> const& tabs, const bool clearList)
    {
        _populateFilteredActions(_tabActions, tabs);
        // The smooth remove/add animations that happen during
        // UpdateFilteredActions don't work very well with changing the tab
        // order, because of the sheer amount of remove/adds. So, let's just
        // clear & rebuild the list when we change the set of tabs.
        //
        // Some callers might actually want smooth updating, like when the list
        // of tabs changes.
        if (clearList && _currentMode == CommandPaletteMode::TabSwitchMode)
        {
            _filteredActions.Clear();
        }
        _updateFilteredActions();
    }

    // Method Description:
    // - This helper function is responsible to update a collection of filtered commands (e.g., tab switcher commands)
    // with the new values
    // Arguments:
    // - vectorToPopulate - the vector of filtered commands to populate
    // - actions - the raw commands to use
    // Return Value:
    // - <none>
    void CommandPalette::_populateFilteredActions(Collections::IVector<winrt::TerminalApp::FilteredCommand> const& vectorToPopulate, Collections::IVector<Command> const& actions)
    {
        vectorToPopulate.Clear();
        for (const auto& action : actions)
        {
            auto filteredCommand{ winrt::make<FilteredCommand>(action) };
            vectorToPopulate.Append(filteredCommand);
        }
    }

    void CommandPalette::EnableCommandPaletteMode()
    {
        _switchToMode(CommandPaletteMode::ActionMode);
        _updateFilteredActions();
    }

    void CommandPalette::_switchToMode(CommandPaletteMode mode)
    {
        // The smooth remove/add animations that happen during
        // UpdateFilteredActions don't work very well when switching between
        // modes because of the sheer amount of remove/adds. So, let's just
        // clear + append when switching between modes.
        if (mode != _currentMode)
        {
            _currentMode = mode;
            _filteredActions.Clear();
            auto commandsToFilter = _commandsToFilter();

            for (auto action : commandsToFilter)
            {
                _filteredActions.Append(action);
            }
        }

        _searchBox().Text(L"");
        _searchBox().Select(_searchBox().Text().size(), 0);
        // Leaving this block of code outside the above if-statement
        // guarantees that the correct text is shown for the mode
        // whenever _switchToMode is called.
        switch (_currentMode)
        {
        case CommandPaletteMode::TabSearchMode:
        case CommandPaletteMode::TabSwitchMode:
        {
            SearchBoxPlaceholderText(RS_(L"TabSwitcher_SearchBoxText"));
            NoMatchesText(RS_(L"TabSwitcher_NoMatchesText"));
            ControlName(RS_(L"TabSwitcherControlName"));
            PrefixCharacter(L"");
            break;
        }
        case CommandPaletteMode::CommandlineMode:
            SearchBoxPlaceholderText(RS_(L"CmdPalCommandlinePrompt"));
            NoMatchesText(L"");
            ControlName(RS_(L"CommandPaletteControlName"));
            PrefixCharacter(L"");
            break;
        case CommandPaletteMode::ActionMode:
        default:
            SearchBoxPlaceholderText(RS_(L"CommandPalette_SearchBox/PlaceholderText"));
            NoMatchesText(RS_(L"CommandPalette_NoMatchesText/Text"));
            ControlName(RS_(L"CommandPaletteControlName"));
            PrefixCharacter(L">");
            break;
        }
    }

    // Method Description:
    // - Produce a list of filtered actions to reflect the current contents of
    //   the input box.
    // Arguments:
    // - A collection that will receive the filtered actions
    // Return Value:
    // - <none>
    std::vector<winrt::TerminalApp::FilteredCommand> CommandPalette::_collectFilteredActions()
    {
        std::vector<winrt::TerminalApp::FilteredCommand> actions;

        winrt::hstring searchText{ _getTrimmedInput() };

        auto commandsToFilter = _commandsToFilter();
        for (const auto& action : commandsToFilter)
        {
            // Update filter for all commands
            // This will modify the highlighting but will also lead to re-computation of weight (and consequently sorting).
            // Pay attention that it already updates the highlighting in the UI
            action.UpdateFilter(searchText);

            // if there is active search we skip commands with 0 weight
            if (searchText.empty() || action.Weight() > 0)
            {
                actions.push_back(action);
            }
        }

        // We want to present the commands sorted,
        // unless we are in the TabSwitcherMode and TabSearchMode,
        // in which we want to preserve the original order (to be aligned with the tab view)
        if (_currentMode != CommandPaletteMode::TabSearchMode && _currentMode != CommandPaletteMode::TabSwitchMode && _currentMode != CommandPaletteMode::CommandlineMode)
        {
            std::sort(actions.begin(), actions.end(), FilteredCommand::Compare);
        }
        return actions;
    }

    // Method Description:
    // - Update our list of filtered actions to reflect the current contents of
    //   the input box.
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::_updateFilteredActions()
    {
        auto actions = _collectFilteredActions();

        // Make _filteredActions look identical to actions, using only Insert and Remove.
        // This allows WinUI to nicely animate the ListView as it changes.
        for (uint32_t i = 0; i < _filteredActions.Size() && i < actions.size(); i++)
        {
            for (uint32_t j = i; j < _filteredActions.Size(); j++)
            {
                if (_filteredActions.GetAt(j).Command() == actions[i].Command())
                {
                    for (uint32_t k = i; k < j; k++)
                    {
                        _filteredActions.RemoveAt(i);
                    }
                    break;
                }
            }

            if (_filteredActions.GetAt(i).Command() != actions[i].Command())
            {
                _filteredActions.InsertAt(i, actions[i]);
            }
        }

        // Remove any extra trailing items from the destination
        while (_filteredActions.Size() > actions.size())
        {
            _filteredActions.RemoveAtEnd();
        }

        // Add any extra trailing items from the source
        while (_filteredActions.Size() < actions.size())
        {
            _filteredActions.Append(actions[_filteredActions.Size()]);
        }
    }

    void CommandPalette::SetDispatch(const winrt::TerminalApp::ShortcutActionDispatch& dispatch)
    {
        _dispatch = dispatch;
    }

    // Method Description:
    // - Dismiss the command palette. This will:
    //   * select all the current text in the input box
    //   * set our visibility to Hidden
    //   * raise our Closed event, so the page can return focus to the active Terminal
    // Arguments:
    // - <none>
    // Return Value:
    // - <none>
    void CommandPalette::_close()
    {
        Visibility(Visibility::Collapsed);

        // Reset visibility in case anchor mode tab switcher just finished.
        _searchBox().Visibility(Visibility::Visible);

        // Clear the text box each time we close the dialog. This is consistent with VsCode.
        _searchBox().Text(L"");

        _nestedActionStack.Clear();

        ParentCommandName(L"");
        _currentNestedCommands.Clear();
    }

    void CommandPalette::EnableTabSwitcherMode(const bool searchMode, const uint32_t startIdx)
    {
        _switcherStartIdx = startIdx;

        if (searchMode)
        {
            _switchToMode(CommandPaletteMode::TabSearchMode);
        }
        else
        {
            _switchToMode(CommandPaletteMode::TabSwitchMode);
        }

        _updateFilteredActions();
    }

}
