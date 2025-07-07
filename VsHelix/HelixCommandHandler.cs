using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
	//[Export(typeof(ICommandHandler))]
	//[Name(nameof(HelixCommandHandler))]
	//[ContentType("code")]
	//[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	//public class HelixCommandHandler : ICommandHandler<EditorCommandArgs> // Replace EditorCommandArgs with more specific CommandArgs class
	//{
	//	public string DisplayName => nameof(HelixCommandHandler);

	//	public bool ExecuteCommand(EditorCommandArgs args, CommandExecutionContext executionContext)
	//	{
	//		throw new NotImplementedException();
	//	}

	//	public CommandState GetCommandState(EditorCommandArgs args)
	//	{
	//		return CommandState.Available;
	//	}
	//}

	[Export(typeof(ICommandHandler))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[Name(nameof(HelixCommandHandler))]
	[Order(Before = "TypeChar")]
	internal sealed class HelixCommandHandler
			   : ICommandHandler<TypeCharCommandArgs>
	{
		public string DisplayName => "Helix Emulation (demo)";

		public CommandState GetCommandState(TypeCharCommandArgs args)
			=> CommandState.Unspecified;        // let VS decide enable/disable

		public bool ExecuteCommand(TypeCharCommandArgs args,
								   CommandExecutionContext ctx)
		{
			// Example: on 'w' expand to "word"
			if (args.TypedChar == 'w')
			{
				var view = args.TextView;
				using var edit = view.TextBuffer.CreateEdit();
				edit.Insert(view.Caret.Position.BufferPosition, "word");
				edit.Apply();
				return true;                   // we handled it, stop bubbling
			}

			return false;                      // not ours, pass to next handler
		}
	}
}