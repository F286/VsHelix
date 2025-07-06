using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

namespace VsHelix
{
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name(nameof(HelixCommandHandler))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HelixCommandHandler : ICommandHandler<TypeCharCommandArgs>
    {
        private readonly IMultiSelectionBroker _selectionBroker;
        private readonly IEditorOperationsFactoryService _operationsFactory;

        [ImportingConstructor]
        public HelixCommandHandler(
            IMultiSelectionBroker selectionBroker,
            IEditorOperationsFactoryService operationsFactory)
        {
            _selectionBroker = selectionBroker ?? throw new ArgumentNullException(nameof(selectionBroker));
            _operationsFactory = operationsFactory ?? throw new ArgumentNullException(nameof(operationsFactory));
        }

        public string DisplayName => "Helix Basic Motion";

        public CommandState GetCommandState(TypeCharCommandArgs args)
        {
            return CommandState.Available;
        }

        public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
        {
            if (args.TypedChar != 'w')
            {
                return false;
            }

            var view = args.TextView;
            var operations = _operationsFactory.GetEditorOperations(view);

            using (_selectionBroker.BeginBatchOperation())
            {
                foreach (var selection in _selectionBroker.AllSelections)
                {
                    view.Caret.MoveTo(selection.End);
                    operations.MoveToNextWord(false);
                }
            }

            return true;
        }
    }
}
