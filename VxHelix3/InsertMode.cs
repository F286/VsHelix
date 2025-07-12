using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities; // Add this namespace for IMultiSelectionBroker

namespace VxHelix3
{
	/// <summary>
	/// Handles key input when in Insert mode.
	/// </summary>
	internal sealed class InsertMode : IInputMode
	{
               public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
               {
                       return false;
               }
	}
}