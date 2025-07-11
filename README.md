# VsHelix

VsHelix is a minimal Visual Studio extension that explores a Helix/Kakoune style
"selection first" editing workflow.  It uses Visual Studio's built in multi
cursor functionality as the state store.  The project now includes a basic
command handler and will grow into a small set of core commands.

## Building and Running

1. Install **Visual Studio 2022** with the *Visual Studio extension development*
   workload.
2. Open `VsHelix.sln` in Visual Studio.
3. Press **F5** to launch an Experimental Instance of Visual Studio with the
   extension automatically loaded.
4. Open any text file in the experimental instance.  Use
   `Ctrl+Alt+Click` to place additional carets.
5. Press `w` to extend each caret to the start of the next word.

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

The extension includes a `HelixCommandHandler` implementing the `w` command
which moves each selection to the start of the next word.
