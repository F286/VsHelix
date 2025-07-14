using Moq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using VsHelix;

namespace VsHelix.Tests;

public class NormalModeTests
{
	[Fact]
	public void Typing_i_enters_insert_mode()
	{
	   var manager = new ModeManager();
	   var selections = new SelectionManager();
	   manager.EnterNormal();
	   manager.ShowModeAction = _ => { };

	    var snapshot = new Mock<ITextSnapshot>().Object;
	    var buffer = new Mock<ITextBuffer>().Object;
	    var view = new Mock<ITextView>();
	    view.SetupGet(v => v.TextSnapshot).Returns(snapshot);
	    view.SetupGet(v => v.TextBuffer).Returns(buffer);

	    var broker = new Mock<IMultiSelectionBroker>();
	    broker.SetupGet(b => b.TextView).Returns(view.Object);
	    broker.SetupGet(b => b.AllSelections).Returns(Array.Empty<ISelection>());
	    broker.Setup(b => b.PerformActionOnAllSelections(It.IsAny<Action<ISelectionTransformer>>()))
		 .Callback(() => { });

	    var ops = new Mock<IEditorOperations>().Object;
	    var args = new TypeCharCommandArgs(view.Object, buffer, 'i');

	    var mode = new NormalMode(manager, selections);
	    var handled = mode.Handle(args, view.Object, broker.Object, ops);

	    Assert.True(handled);
	    Assert.Equal(ModeManager.EditorMode.Insert, manager.Current);
	}

	[Fact]
	public void Typing_w_invokes_selection_action()
	{
	   var manager = new ModeManager();
	   var selections = new SelectionManager();
	   manager.EnterNormal();
	   manager.ShowModeAction = _ => { };

	    var snapshot = new Mock<ITextSnapshot>().Object;
	    var buffer = new Mock<ITextBuffer>().Object;
	    var view = new Mock<ITextView>();
	    view.SetupGet(v => v.TextSnapshot).Returns(snapshot);
	    view.SetupGet(v => v.TextBuffer).Returns(buffer);

	    var broker = new Mock<IMultiSelectionBroker>();
	    broker.SetupGet(b => b.TextView).Returns(view.Object);
	    broker.SetupGet(b => b.AllSelections).Returns(Array.Empty<ISelection>());

	    var called = false;
	    broker.Setup(b => b.PerformActionOnAllSelections(It.IsAny<Action<ISelectionTransformer>>()))
		 .Callback(() => called = true);

	    var ops = new Mock<IEditorOperations>().Object;
	    var args = new TypeCharCommandArgs(view.Object, buffer, 'w');

	    var mode = new NormalMode(manager, selections);
	    var handled = mode.Handle(args, view.Object, broker.Object, ops);

	    Assert.True(handled);
	    Assert.True(called);
	    Assert.Equal(ModeManager.EditorMode.Normal, manager.Current);
	}
}
