# Repo Guidelines for Agents

This repository implements a small Visual Studio extension which aims to mimic
a Helix style "selection first" workflow.  Development is intentionally simple
and only targets Visual Studio 2022.

## Coding Conventions

- Language: **C#** targeting .NET Framework 4.7.2.
- Use Visual Studio's `IMultiSelectionBroker` API for all caret and selection
  manipulation.  Do not attempt to track caret state manually.
- Command handlers are exported with `ICommandHandler<TypeCharCommandArgs>` and
  should remain as small as possible.
- Keep the project minimal; avoid adding unnecessary dependencies or copying in
  large portions of the VsVim code base.
- Document new commands and behaviour in `README.md` when adding features.

## Testing

- Ensure the project builds by running `msbuild VsHelix.sln /restore`.
- Manual testing is performed via Visual Studio's Experimental Instance.
  Launch with **F5** to verify commands operate on multiple carets correctly.

Any pull request should include updates to documentation and compile cleanly
using the above steps.
