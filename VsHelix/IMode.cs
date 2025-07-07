using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
    /// <summary>
    /// Defines the contract for a mode handler.
    /// </summary>
    internal interface IMode
    {
        bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations, out HelixMode nextMode);
    }
}
