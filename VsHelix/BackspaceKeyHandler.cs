using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
[Export(typeof(ICommandHandler))]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Name(nameof(BackspaceKeyHandler))]
[Order(Before = "Backspace")]
[VisualStudioContribution]
internal sealed class BackspaceKeyHandler : ICommandHandler<BackspaceKeyCommandArgs>
{
public string DisplayName => "Helix Backspace Handler";

public CommandState GetCommandState(BackspaceKeyCommandArgs args) => CommandState.Available;

public bool ExecuteCommand(BackspaceKeyCommandArgs args, CommandExecutionContext context)
{
if (ModeManager.Instance.Current == ModeManager.EditorMode.Search && ModeManager.Instance.Search != null)
{
ModeManager.Instance.Search.HandleBackspace();
return true;
}
return false;
}
}
}
