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
5. Use `w`, `b`, and `e` to select forward, backward, or to the end of a word.
   Uppercase `W`, `B`, and `E` operate on whitespace-delimited WORDs.
6. Use `h`, `j`, `k`, and `l` to move all carets left, down, up, or right by one character or line.
7. Carets display as thin vertical bars in both modes for a consistent insert-style look.
8. Reference highlighting is disabled in Normal and Visual modes so only the actual selection is shown.

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

### Command Overview

VsHelix exposes a `HelixCommandHandler` with bindings inspired by the Helix editor:

 - **Movement** – `w`, `b`, `e` select by word while `h`, `j`, `k`, and `l` move left, down, up, and right.
- **Line operations** – `x` selects the current line; `C` and <kbd>Alt</kbd>+`C` copy the selection below or above.
- **New lines** – `o` and `O` insert lines below or above the caret and enter insert mode.
- **Clipboard** – `y` yanks, `p` pastes, and `d`/`c` delete while yanking (hold <kbd>Alt</kbd> to delete only).
- **Selections** – `,` clears secondary selections and `v` toggles Visual mode for selection growth.
- **Search** – `s` selects regex matches and `/` performs an incremental search navigated with `n`/`N`.
- **Pairs** – `m` commands jump to, surround, or modify matching brackets and quotes.
- **Misc** – `u`/`U` undo and redo, numeric prefixes repeat commands, and <kbd>Esc</kbd> exits to Normal mode.
- **Goto mode** – `g` followed by another key jumps to lines, files, or definitions.
## Download

You can download the latest VsHelix build from the [releases page](https://github.com/F286/VsHelix/releases/latest/download/VsHelix.vsix).
