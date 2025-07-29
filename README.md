# VsHelix

VsHelix is a minimal Visual Studio extension that explores a Helix/Kakoune style
"selection first" editing workflow.  It uses Visual Studio's built in multi
cursor functionality as the state store.  The project now includes a basic
command handler and will grow into a small set of core commands.

You can always grab the latest nightly build from the
[releases page](https://github.com/F286/VsHelix/releases/latest/download/VsHelix.vsix).

## Building and Running

1. Install **Visual Studio 2022** with the *Visual Studio extension development*
   workload.
2. Open `VsHelix.sln` in Visual Studio.
3. Press **F5** to launch an Experimental Instance of Visual Studio with the
   extension automatically loaded.
4. Open any text file in the experimental instance.  Use
   `Ctrl+Alt+Click` to place additional carets.
5. Use `w`, `b`, and `e` to move forward, backward, or to the end of a word.
   Uppercase `W`, `B`, and `E` operate on whitespace-delimited WORDs.
6. Use `h`, `j`, `k`, and `l` to move all carets left, down, up, or right by one character or line.
7. Carets display as thin vertical bars in both modes for a consistent insert-style look.
8. Reference highlighting is disabled in Normal mode so only the actual selection is shown.

The provided `VsHelixPackage` is a standard AsyncPackage.  Command handlers are
added via MEF exports.  When the project is built in *Release* configuration it
produces `bin/Release/VsHelix.vsix` which can be installed by double clicking
or using `VSIXInstaller.exe`.

## Project Goals

- Prototype selection‑first commands similar to the Helix editor.
- Use Visual Studio's `IMultiSelectionBroker` API to track selections and
  perform transformations.
- Keep the code base small and easy to extend.
- Visual Studio's status bar now shows the active editing mode using three
  letter abbreviations like `NOR` and `INS`.

## Repository Layout

- `VsHelixPackage.cs` – Package entry point.
- `VsHelix.csproj` – Project definition and SDK references.
- `source.extension.vsixmanifest` – Extension manifest used by Visual Studio.

The extension includes a `HelixCommandHandler` implementing basic motions like
`w`, `b`, and `e` for word movement as well as `h`, `j`, `k`, and `l` for left, down, up, and
right movement across all selections.  The `x` key selects the current line
including its trailing newline and extends to the next line when pressed
repeatedly.  Pressing `C`
copies the current selection to the line below while <kbd>Alt</kbd>+`C`
duplicates the selection on the line above.  Use `o` to open a new line below
each caret and `O` to open one above.  Both commands switch to insert mode at
the newly inserted line with the indentation of the surrounding text.
Commands are resolved using a small Helix-style keymap per mode, allowing
multi-key sequences to be added in the future.
Press `p` pastes clipboard text after each selection. When the clipboard content
ends with a newline it is inserted on its own line; otherwise it is inserted at
the caret positions. Use `y` to yank the current selections to the clipboard.
The `d` and `c` commands yank before deleting so that pasted text is available
afterwards. Hold <kbd>Alt</kbd> while pressing `d` or `c` to delete without
copying.
Press `u` to undo and `U` to redo the last action.
Type a number before any normal-mode command to repeat it that many times.
Pressing <kbd>Esc</kbd> now closes any active IntelliSense sessions before
returning to normal mode. The handler executes ahead of Visual Studio's default
Escape logic so mode transitions always occur reliably.
Pressing <kbd>,</kbd> clears all secondary selections, leaving a single cursor.
Use `s` to select all matches of a regex typed inline. `/` performs an incremental search that highlights matches as you type. While searching, `n` and `N` jump to the next or previous match. Press **Enter** to accept the search or **Esc** to cancel. Enter and Backspace are now dispatched through the same keymap as other characters.
Press `m` followed by another key to manipulate matching pairs. `mm` jumps to the matching bracket, `ms<char>` surrounds the selection, `mr<from><to>` replaces the surrounding characters, `md<char>` removes the surrounding pair, and `ma`/`mi <char>` select around or inside a pair.
Press `v` toggles Visual mode where movements extend the current selections. While in Visual mode, motions grow the selection with a fixed anchor. Use `y`, `d`, `c`, or `p` to yank, delete, change, or replace the selections. `C` and `K` duplicate the selections down or up to form multi-line blocks. Hitting <kbd>Esc</kbd> leaves Visual mode but keeps the selection active in Normal mode.

Press `g` to enter **Goto** mode. The status bar shows `GTO` while waiting for a second key. `gg` jumps to a specified line number (or the start of the file), `ge` goes to the end of the file and `gf` opens files whose paths are under the selections. Use `gh`, `gl` and `gs` for the start, end, and first non-whitespace of the line. `gt`, `gc` and `gb` move the caret to the top, middle or bottom of the visible editor. The keys `gd`, `gy`, `gr` and `gi` trigger Visual Studio's Go To Definition, Type Definition, References and Implementation commands. `ga`/`gm` navigate backward or forward in file history, `gn`/`gp` switch between documents and `g.` returns to the last edit location. `gj` and `gk` move by textual lines.


## Download

You can download the latest VsHelix build from the [releases page](https://github.com/F286/VsHelix/releases/latest/download/VsHelix.vsix).
