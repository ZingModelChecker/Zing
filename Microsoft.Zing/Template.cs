#define USES_HEAP
#define HAS_GLOBALS

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;

// using an alias here to make the Zing runtime references stand out more clearly
using Z = Microsoft.Zing;

[assembly: AssemblyTitle("")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]		
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyDelaySign(false)]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]

namespace Microsoft.Zing
{
	public class Application : Z.StateImpl
	{
		#region Constructors

		public Application()
		{
			globals = new GlobalVars(this);
		}

        public Application(bool initialState) : base(initialState)
        {
            System.Diagnostics.Debug.Assert(initialState);
            globals = new GlobalVars(this);
        }

		#endregion

		#region FactoryMethods
		public override StateImpl MakeSkeleton()
		{
			return new Application();
		}
		#endregion
        
		private Application application { get { return this; } }

		private static string[] sourceFiles = new string[] { /* insert source files here */ };
        public override string[] GetSourceFiles() { return Application.sourceFiles; }

		private static string[] sources = new string[] { /* insert source literals here */ };
		public override string[] GetSources() { return Application.sources; }

		private static object[] boolChoices = new object[] { false, true };

		internal enum Exceptions
		{
			_None_ = 0,
			// Each exception referenced in user code will be added to this enum. In Zing,
			// all exception names are global and need not be declared in any way.
		};

		private sealed class GlobalVars : ZingGlobals
		{
			// for each static data member of a user-defined class, emit a member
			// declaration here...
			//public int member1;

			public GlobalVars(StateImpl app) : base (app) {;}

			private GlobalVars() : base () {;}

			public override void WriteString(StateImpl state, BinaryWriter bw)
			{
                if (UnallocatedWrites != null)
                    UnallocatedWrites.WriteString(state, bw);

				// for each global, emit a call to bm.Write(). For pointer
				// types, write a "canonical" pointer obtained by calling
				// GetCanonicalId().
				//bw.Write(member1);
				;
			}

			public override void TraverseFields(FieldTraverser ft)
			{
                ft.DoTraversal(UnallocatedWrites);

				//ft.DoTraversal(member);
			}

			// copy everything except app, dirty, and savedCopy
			public override void CopyContents(UndoableStorage zgSrc)
			{
				GlobalVars src = zgSrc as GlobalVars;

				if (src == null)
					throw new ArgumentException("expecting global vars here");

				// ...
			}
            
			public override UndoableStorage MakeInstance()
			{
				return new GlobalVars();
			}

			public override object GetValue(int fi)
			{

			}

			public override void SetValue(int fi, object val)
			{

			}

		}

        private GlobalVars globals;
        protected override ZingGlobals Globals { 
            get { return globals; } 
            set { globals = (GlobalVars) value; }
        }
		public override Fingerprint ComputeFingerprint()
		{
			Fingerprint globalPrint = new Fingerprint();
#if HAS_GLOBALS
			// Fingerprinting globals defaults to the nonincremental WriteString method
			// As a prerequirement for incremental fingerprinting of globals
			//   we need 'dirty' bits at coarser levels (currently there is one for the entire globals)
            BinaryWriter MyBinWriter = GetBinaryWriter(MySerialNum);
			MyBinWriter.Seek(0, SeekOrigin.Begin);
			globals.WriteString(this, MyBinWriter);
            MemoryStream MyMemStream = GetMemoryStream(MySerialNum);
			globalPrint = FingerprintNonHeapBuffer(MyMemStream.GetBuffer(), (int)MyMemStream.Position);
			//globalprint = Fingerprint.ComputeFingerprint(StateImpl.MemoryStream.GetBuffer(), (int)StateImpl.MemoryStream.Position, GlobalDomain, 0);
#else
			//globalPrint = new Fingerprint();
#endif
			Fingerprint basePrint = base.ComputeFingerprint();
			globalPrint.Concatenate(basePrint);
			return globalPrint;
		}
		
		public override void WriteString(BinaryWriter bw)
		{
#if HAS_GLOBALS
			globals.WriteString(this, bw);
#endif
			base.WriteString(bw);
		}
	}
}
