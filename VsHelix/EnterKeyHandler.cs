using System.ComponentModel.Composition;
using System.Windows.Controls;
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
	[Name(nameof(EnterKeyHandler))]
	[Order(Before = "Return")]
	[VisualStudioContribution]
	internal sealed class EnterKeyHandler : ICommandHandler<ReturnKeyCommandArgs>
	{
		public string DisplayName => "Helix Enter Handler";

		public CommandState GetCommandState(ReturnKeyCommandArgs args) => CommandState.Available;

		public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext context)
		{
			if (ModeManager.Instance.Current == ModeManager.EditorMode.Search && ModeManager.Instance.Search != null)
			{
				if (SelectionManager.Instance.HasSavedSelections)
				{
					SelectionManager.Instance.ClearSelections();
				}
				ModeManager.Instance.Search.Finish();
				return true;
			}
			return false;
		}
	}
}
