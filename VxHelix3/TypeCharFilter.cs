using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
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
		private const string ModeKey = nameof(TypeCharFilter) + "_mode";

		private readonly IEditorOperationsFactoryService _editorOperationsFactory;
		//private readonly IInputMode _insertMode = new InsertMode();
		private readonly IInputMode _normalMode = new NormalMode();

		[ImportingConstructor]
		internal TypeCharFilter(IEditorOperationsFactoryService editorOperationsFactory)
		{
			_editorOperationsFactory = editorOperationsFactory;
		}

		private static HelixMode GetMode(ITextView view)
		{
			if (!view.Properties.TryGetProperty(ModeKey, out HelixMode mode))
			{
				mode = HelixMode.Insert;
				view.Properties[ModeKey] = mode;
			}
			return mode;
		}

		private static void SetMode(ITextView view, HelixMode mode)
				=> view.Properties[ModeKey] = mode;

		public string DisplayName => "Helix Emulation (demo)";

		public CommandState GetCommandState(TypeCharCommandArgs args)
				=> CommandState.Unspecified; // let VS decide enable/disable

		public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext ctx)
		{
			var view = args.TextView;
			var broker = view.GetMultiSelectionBroker();
			var ops = _editorOperationsFactory.GetEditorOperations(view);

			var mode = GetMode(view);
			IInputMode handler = _normalMode;
			//IInputMode handler = mode == HelixMode.Insert ? _insertMode : _normalMode;
			if (handler.Handle(args, view, broker, ops, out HelixMode next))
			{
				SetMode(view, next);
				return true;
			}

			return false;
		}
	}
}
