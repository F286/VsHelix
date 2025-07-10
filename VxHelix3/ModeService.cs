using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;

namespace VxHelix3
{
	[VisualStudioContribution]               // pick any namespace
	public sealed class ModeService
	{
		public enum EditorMode { Normal, Insert }
		public EditorMode Current { get; private set; } = EditorMode.Normal;

		public void EnterInsert() => Current = EditorMode.Insert;
		public void EnterNormal() => Current = EditorMode.Normal;
	}

}
