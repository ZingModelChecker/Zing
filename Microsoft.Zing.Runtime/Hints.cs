using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.Zing
{
#if false
	public enum Constants : ushort { OMEGA = ushort.MaxValue };

	public struct HintElement
	{
	}
#endif
	
	// <summary>
	// Represents hints for a procedure or for global variables
	// </summary>
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
	public class Hints
	{
		private Hashtable HintTable;

		public Hints():this(null) {}

		public Hints(Hashtable hintTable)
		{
			this.HintTable = hintTable;
		}

		public bool LookupHint(ushort pc)
		{
			if (HintTable == null)
			{
				return false;
			}
			else
			{
				return HintTable.ContainsKey(pc);
			}
		}

		public void InstallHint(ushort pc)
		{
			if (HintTable == null)
			{
				HintTable = new Hashtable();
			}

			//print the hint info
			//Console.WriteLine("creating hint in {0}", this.MethodName);
			//this.PrintHint(h);

			if (!HintTable.ContainsKey(pc))
			{
				HintTable.Add(pc,null);
			}
		}
	}
}