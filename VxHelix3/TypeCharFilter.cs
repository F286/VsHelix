using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace VxHelix3
{
	[Export(typeof(ICommandHandler))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[Name(nameof(TypeCharFilter))]
	[Order(Before = "TypeChar")]
	[VisualStudioContribution]
	internal sealed class TypeCharFilter
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
