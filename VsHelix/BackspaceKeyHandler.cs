using System.ComponentModel.Composition;
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
	[Name(nameof(BackspaceKeyHandler))]
	[Order(Before = "Backspace")]
	[VisualStudioContribution]
        internal sealed class BackspaceKeyHandler : ICommandHandler<BackspaceKeyCommandArgs>
        {
                private readonly IEditorOperationsFactoryService _operationsFactory;

                [ImportingConstructor]
                public BackspaceKeyHandler(IEditorOperationsFactoryService operationsFactory)
                {
                        _operationsFactory = operationsFactory;
                }

                public string DisplayName => "Helix Backspace Handler";

                public CommandState GetCommandState(BackspaceKeyCommandArgs args) => CommandState.Available;

                public bool ExecuteCommand(BackspaceKeyCommandArgs args, CommandExecutionContext context)
                {
                        var view = args.TextView;
                        var broker = view.GetMultiSelectionBroker();
                        var ops = _operationsFactory.GetEditorOperations(view);

                        if (ModeManager.Instance.Current == ModeManager.EditorMode.Search && ModeManager.Instance.Search != null)
                                return ModeManager.Instance.Search.HandleChar('\b', view, broker, ops);
                        if (ModeManager.Instance.Current == ModeManager.EditorMode.Match && ModeManager.Instance.Match != null)
                                return ModeManager.Instance.Match.HandleChar('\b', view, broker, ops);
                        if (ModeManager.Instance.Current == ModeManager.EditorMode.Normal)
                                return true; // swallow

                        return false;
                }
        }
}
