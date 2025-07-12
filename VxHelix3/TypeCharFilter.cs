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
using Microsoft.VisualStudio.Text.IncrementalSearch;
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
                private readonly IInputMode _insertMode = new InsertMode();
                private readonly SearchMode _searchMode;
                private readonly IInputMode _normalMode;

                [ImportingConstructor]
                internal TypeCharFilter(
                        IEditorOperationsFactoryService editorOperationsFactory,
                        ITextSearchService textSearchService,
                        IIncrementalSearchFactoryService incrementalSearchFactory)
                {
                        _editorOperationsFactory = editorOperationsFactory;
                        _searchMode = new SearchMode(textSearchService, incrementalSearchFactory);
                        _normalMode = new NormalMode(_searchMode);
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
                                return _normalMode.Handle(args, view, broker, ops);
                        }
                        else if (ModeManager.Instance.Current == ModeManager.EditorMode.Insert)
                        {
                                return _insertMode.Handle(args, view, broker, ops);
                        }

			return false;
		}
	}
}
