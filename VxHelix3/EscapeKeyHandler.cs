using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace VxHelix3
{
	[Export(typeof(ICommandHandler))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[Name(nameof(EscapeKeyHandler))]
	[Order(Before = "TypeChar")]
	[VisualStudioContribution] 
	internal sealed class EscapeKeyHandler : ICommandHandler<EscapeKeyCommandArgs>
	{
		public string DisplayName => "Helix Escape Handler";

		public CommandState GetCommandState(EscapeKeyCommandArgs args)
			=> CommandState.Available;

		public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext context)
		{
			if (ModeManager.Instance.Current == ModeManager.EditorMode.Insert)
			{
				// Get the TextView from the command arguments (args), not the context.
				var broker = args.TextView.GetMultiSelectionBroker();

				// Check if the SelectionManager has selections waiting to be restored.
				if (SelectionManager.Instance.HasSavedSelections)
				{
					// If so, restore them.
					SelectionManager.Instance.RestoreSelections(broker);
				}

				// Now that the selection is handled, switch the mode.
				ModeManager.Instance.EnterNormal();
				return true; // Command was handled.
			}

			// In normal mode, also cancel 'esc' keys as that would clear multiple selections.
			// This prevents Visual Studio's default behavior of collapsing all carets to one.
			return true;
		}
	}
}
