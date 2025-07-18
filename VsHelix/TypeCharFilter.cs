﻿using System;
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

namespace VsHelix
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
		private readonly IInputMode _insertMode = new InsertMode();
		private readonly IInputMode _normalMode = new NormalMode();
		private readonly IInputMode _visualMode = new VisualMode();
		private readonly IInputMode _gotoMode = new GotoMode();

		[ImportingConstructor]
		internal TypeCharFilter(IEditorOperationsFactoryService editorOperationsFactory)
		{
			_editorOperationsFactory = editorOperationsFactory;
		}

		public string DisplayName => "Helix Emulation (demo)";

		public CommandState GetCommandState(TypeCharCommandArgs args)
				=> CommandState.Unspecified; // let VS decide enable/disable

		public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext ctx)
		{
			var view = args.TextView;
			var broker = view.GetMultiSelectionBroker();
			var ops = _editorOperationsFactory.GetEditorOperations(view);

			if (ModeManager.Instance.Current == ModeManager.EditorMode.Normal)
			{
				return _normalMode.HandleChar(args.TypedChar, view, broker, ops);
			}
			else if (ModeManager.Instance.Current == ModeManager.EditorMode.Insert)
			{
				return _insertMode.HandleChar(args.TypedChar, view, broker, ops);
			}
			else if (ModeManager.Instance.Current == ModeManager.EditorMode.Visual)
			{
				return _visualMode.HandleChar(args.TypedChar, view, broker, ops);
			}
			else if (ModeManager.Instance.Current == ModeManager.EditorMode.Goto)
			{
				return _gotoMode.HandleChar(args.TypedChar, view, broker, ops);
			}
			else if (ModeManager.Instance.Current == ModeManager.EditorMode.Search && ModeManager.Instance.Search != null)
			{
				return ModeManager.Instance.Search.HandleChar(args.TypedChar, view, broker, ops);
			}

			return false;
		}
	}
}
