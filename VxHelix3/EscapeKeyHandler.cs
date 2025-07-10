using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text; // Required for VirtualSnapshotPoint

namespace VxHelix3
{
	[Export(typeof(ICommandHandler))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[Name(nameof(EscapeKeyHandler))]
	[Order(Before = "TypeChar")]
	// This attribute was missing in the original file, but is required for modern extensions.
	// If you already have it in your real file, that's great.
	// [VisualStudioContribution] 
	internal sealed class EscapeKeyHandler : ICommandHandler<EscapeKeyCommandArgs>
	{
		public string DisplayName => "Helix Escape Handler";

		public CommandState GetCommandState(EscapeKeyCommandArgs args)
			=> CommandState.Available;

		public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext context)
		{
			if (ModeManager.Instance.Current == ModeManager.EditorMode.Insert)
			{
				// CORRECTED: Get the TextView from the command arguments (args), not the context.
				var broker = args.TextView.GetMultiSelectionBroker();

				// Check if the SelectionManager has selections waiting to be restored.
				if (SelectionManager.Instance.HasSavedSelections)
				{
					// If so, restore them.
					SelectionManager.Instance.RestoreSelections(broker);
				}
				else
				{
					// This is the standard Vim/Helix behavior for when you press Esc after typing.
					// It moves the caret back one position so it's not on top of the character just typed.
					broker.PerformActionOnAllSelections(transformer =>
					{
						var currentPoint = transformer.Selection.InsertionPoint.Position;
						if (currentPoint > 0)
						{
							var newPoint = new VirtualSnapshotPoint(currentPoint - 1);
							// CORRECTED: Use the correct overload for MoveTo on the transformer to collapse the selection.
							transformer.MoveTo(newPoint, newPoint, newPoint, PositionAffinity.Successor);
						}
					});
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
