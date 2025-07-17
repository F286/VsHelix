using System.ComponentModel.Composition;
using System.Windows.Controls;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
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
                private readonly IEditorOperationsFactoryService _operationsFactory;

                [ImportingConstructor]
                public EnterKeyHandler(IEditorOperationsFactoryService operationsFactory)
                {
                        _operationsFactory = operationsFactory;
                }

                public string DisplayName => "Helix Enter Handler";

                public CommandState GetCommandState(ReturnKeyCommandArgs args) => CommandState.Available;

                public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext context)
                {
                        var view = args.TextView;
                        var broker = view.GetMultiSelectionBroker();
                        var ops = _operationsFactory.GetEditorOperations(view);

                        if (ModeManager.Instance.Current == ModeManager.EditorMode.Search && ModeManager.Instance.Search != null)
                                return ModeManager.Instance.Search.HandleChar('\r', view, broker, ops);

                        return false;
                }
        }
}
