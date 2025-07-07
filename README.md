# VsHelix

VsHelix is a minimal Visual Studio extension that explores a Helix/Kakoune style
"selection first" editing workflow.  It uses Visual Studio's built in multi
cursor functionality as the state store.  The project currently contains only
skeleton code but will grow into a small set of core commands.

## Building and Running

1. Install **Visual Studio 2022** with the *Visual Studio extension development*
   workload.
2. Open `VsHelix.sln` in Visual Studio.
3. In the solution explorer, set the startup to the 'VsHelix' solution.
4. In *Common Properties/Configure Startup Projects* set *Mutiple Startup Projects* then launch VsHelix.
5. Press **F5** to launch an Experimental Instance of Visual Studio with the
   extension automatically loaded.
6. Open any text file in the experimental instance.  Use
   `Ctrl+Alt+Click` to place additional carets.
7. Once the command handler is implemented you will be able to press `w` to
   extend each caret to the start of the next word.

The provided `VsHelixPackage` is a standard AsyncPackage.  Command handlers are
added via MEF exports.  When the project is built in *Release* configuration it
produces `bin/Release/VsHelix.vsix` which can be installed by double clicking
or using `VSIXInstaller.exe`.

## Project Goals

- Prototype selection‑first commands similar to the Helix editor.
- Use Visual Studio's `IMultiSelectionBroker` API to track selections and
  perform transformations.
- Keep the code base small and easy to extend.

## Repository Layout

- `VsHelixPackage.cs` – Package entry point.
- `VsHelix.csproj` – Project definition and SDK references.
- `source.extension.vsixmanifest` – Extension manifest used by Visual Studio.

This repository currently does not include the command handler.  Future commits
will add a `HelixCommandHandler` class implementing the initial `w` command and
other basic functionality.
