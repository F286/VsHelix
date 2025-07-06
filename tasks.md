# Initial Tasks

This repository begins as an empty Visual Studio extension.  The following
incremental tasks will build up basic Helix style behaviour.

1. **Basic motion**
   - Create a `HelixCommandHandler` class exported via MEF.
   - Handle the `w` key using `IMultiSelectionBroker` to extend each caret to the
     start of the next word.
   - Verify the command inside the Experimental Instance.

2. **Delete operator**
   - Add handling for the `d` key.
   - Delete the text in all active selections using a single undo transaction.

3. **Change operator**
   - Implement the `c` key: delete selections and enter insert mode at each
     caret.

4. **Documentation**
   - Update `README.md` with usage examples for each new command.

5. **Further motions**
   - Add additional motions (`b`, `e`, `0`, `$`) reusing the
     `PredefinedSelectionTransformations` helpers.

These tasks should be completed in order.  Each step should compile using
`msbuild VsHelix.sln /restore` and should be verified in the experimental
instance of Visual Studio.
