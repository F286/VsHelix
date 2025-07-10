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
				ModeManager.Instance.EnterNormal();
				return true;
			}

			// In normal mode, also cancel 'esc' keys as that would clear multiple selections
			return true;
		}
	}
}