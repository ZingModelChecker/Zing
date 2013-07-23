using Z=Microsoft.Zing;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
[assembly: AssemblyTitle(@"")]
[assembly: AssemblyDescription(@"")]
[assembly: AssemblyConfiguration(@"")]
[assembly: AssemblyCompany(@"")]
[assembly: AssemblyProduct(@"")]
[assembly: AssemblyCopyright(@"")]
[assembly: AssemblyTrademark(@"")]
[assembly: AssemblyCulture(@"")]
[assembly: AssemblyVersion(@"1.0.*")]
[assembly: AssemblyDelaySign(false)]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]
namespace Microsoft.Zing
{
  public class Application
    : Z.StateImpl
  {
    public Application()
    {
      
      {
        ;
      }
      {
        globals = new GlobalVars(this);
      }
    }
    public Application(bool initialState) : base(initialState)
    {
      
      {
        ;
      }
      {
        System.Diagnostics.Debug.Assert(initialState);
        globals = new GlobalVars(this);
      }
    }
    public override StateImpl MakeSkeleton()
    {
      return new Application();
    }
    private Application application
    {
      get
      {
        return this;
      }
    }
    private static string[] sourceFiles = new string[] { @"D:\SD\Zing\src.simplified\Regression\AssociativeArrayTest1\AssociativeArrayTest1.zing" };
    public override string[] GetSourceFiles()
    {
      return Application.sourceFiles;
    }
    private static string[] sources = new string[] { @"range MyRange 0 .. 10;

class A {
	int x;	
};

class user {

	static activate void main()
	{
		AssocArray a;
		A obj;
		A obj1;
		A obj2;
		A obj_end;
		int n;
		int i;
		string key1;
		string key2;
		
		a = new AssocArray;
		a.initialize();		
		
		n = choose(MyRange);				

		i = 1;
		while (i <= n) {
			key1 = a.StringHelper(""key1_"",  i);			
			key2 = ""abc"";
			
			obj = new A;
			obj.x = 0;
			
			a.Add(key1, obj);
			a.Add(key2, obj);
			
			obj.x = i;
			
			obj1 = null;
			obj2 = null;
			
			obj1 = a.Lookup(key1);
			obj2 = a.Lookup(key2);
			
			assert(obj1 == obj2);
			assert(obj == obj1);
			assert(obj1.x == i);
			
			i = i + 1;
		}
		
		obj_end = a.Lookup(""key1_10"");		
		assert (obj_end == null);
	}
	
};
" };
    public override string[] GetSources()
    {
      return Application.sources;
    }
    private static object[] boolChoices = new object[] { false, true };
    internal enum Exceptions : int
    {
      _None_ = 0,
    };
    private sealed class GlobalVars
      : ZingGlobals
    {
      public GlobalVars(StateImpl app) : base(app)
      {
        
        {
          ;
        }
        {
          {
            ;
          }
        }
      }
      private GlobalVars() : base()
      {
        
        {
          ;
        }
        {
          {
            ;
          }
        }
      }
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        if ((UnallocatedWrites != null))
        {
          UnallocatedWrites.WriteString(state, bw);
        }
        {
          ;
        }
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(UnallocatedWrites);
      }
      public override void CopyContents(UndoableStorage zgSrc)
      {
        GlobalVars src = (zgSrc as GlobalVars);
        if ((src == null))
        {
          throw new ArgumentException(@"expecting global vars here");
        }
      }
      public override UndoableStorage MakeInstance()
      {
        return new GlobalVars();
      }
      public override object GetValue(int fi)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return null;
            }
          }
        }
      }
      public override void SetValue(int fi, object val)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return;
            }
          }
        }
      }
    }
    private GlobalVars globals;
    protected override ZingGlobals Globals
    {
      get
      {
        return globals;
      }
      set
      {
        globals = ((GlobalVars) value);
      }
    }
    public override Fingerprint ComputeFingerprint()
    {
      Fingerprint globalPrint = new Fingerprint();
      BinaryWriter MyBinWriter = GetBinaryWriter(MySerialNum);
      MyBinWriter.Seek(0, SeekOrigin.Begin);
      globals.WriteString(this, MyBinWriter);
      MemoryStream MyMemStream = GetMemoryStream(MySerialNum);
      globalPrint = FingerprintNonHeapBuffer(MyMemStream.GetBuffer(), ((int) MyMemStream.Position));
      Fingerprint basePrint = base.ComputeFingerprint();
      globalPrint.Concatenate(basePrint);
      return globalPrint;
    }
    public override void WriteString(BinaryWriter bw)
    {
      globals.WriteString(this, bw);
      base.WriteString(bw);
    }
    internal enum ___MyRange : int
    {
      Min = 0,
      Max = 10,
    };
    public static object[] ___MyRangeChoices = new object[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    internal class ___A
      : Z.ZingClass
    {
      public ___A(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___A()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___A(___A c) : base(c)
      {
        
        {
          ;
        }
        {
        }
      }
      protected override short TypeId
      {
        get
        {
          return ___A.typeId;
        }
      }
      private static readonly short typeId = 1;
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        bw.Write(this.TypeId);
        bw.Write(this.priv____x);
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(this.TypeId);
        ft.DoTraversal(this.priv____x);
      }
      public override object Clone()
      {
        ___A newObj = new ___A(this);
        {
          newObj.priv____x = this.priv____x;
        }
        return newObj;
      }
      public override object GetValue(int fi)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return null;
            }
          }
          case 0:
          {
            return priv____x;
          }
        }
      }
      public override void SetValue(int fi, object val)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return;
            }
          }
          case 0:
          {
            {
              priv____x = ((Int32) val);
              return;
            }
          }
        }
      }
      public int priv____x;
      public static int id____x = 0;
      public int ___x
      {
        get
        {
          return priv____x;
        }
        set
        {
          SetDirty();
          priv____x = value;
        }
      }
    }
    internal class ___user
      : Z.ZingClass
    {
      public ___user(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___user()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___user(___user c) : base(c)
      {
        
        {
          ;
        }
        {
        }
      }
      protected override short TypeId
      {
        get
        {
          return ___user.typeId;
        }
      }
      private static readonly short typeId = 2;
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        bw.Write(this.TypeId);
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(this.TypeId);
      }
      public override object Clone()
      {
        ___user newObj = new ___user(this);
        {
        }
        return newObj;
      }
      public override object GetValue(int fi)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return null;
            }
          }
        }
      }
      public override void SetValue(int fi, object val)
      {
        switch (fi) {
          default:
          {
            {
              Debug.Assert(false);
              return;
            }
          }
        }
      }
      [Z.Activate()]
      internal sealed class ___main
        : Z.ZingMethod
      {
        public ___main(Application app)
        {
          
          {
            ;
          }
          {
            application = app;
            nextBlock = Blocks.Enter;
            handlerBlock = Blocks.None;
            locals = new LocalVars(this);
            inputs = new InputVars(this);
            outputs = new OutputVars(this);
          }
        }
        private Application application;
        public override StateImpl StateImpl
        {
          get
          {
            return ((StateImpl) application);
          }
          set
          {
            application = ((Application) value);
          }
        }
        public sealed class LocalVars
          : UndoableStorage
        {
          private ZingMethod stackFrame;
          internal LocalVars(ZingMethod zm)
          {
            
            {
              ;
            }
            {
              stackFrame = zm;
            }
          }
          public override UndoableStorage MakeInstance()
          {
            return new LocalVars(stackFrame);
          }
          public override void CopyContents(UndoableStorage usSrc)
          {
            LocalVars src = (usSrc as LocalVars);
            if ((src == null))
            {
              throw new ArgumentException(@"expecting instance of LocalVars as source");
            }
            this.priv____a = src.priv____a;
            this.priv____obj = src.priv____obj;
            this.priv____obj1 = src.priv____obj1;
            this.priv____obj2 = src.priv____obj2;
            this.priv____obj_end = src.priv____obj_end;
            this.priv____n = src.priv____n;
            this.priv____i = src.priv____i;
            this.priv____key1 = src.priv____key1;
            this.priv____key2 = src.priv____key2;
          }
          public override object GetValue(int fi)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return null;
                }
              }
              case 0:
              {
                return priv____a;
              }
              case 1:
              {
                return priv____obj;
              }
              case 2:
              {
                return priv____obj1;
              }
              case 3:
              {
                return priv____obj2;
              }
              case 4:
              {
                return priv____obj_end;
              }
              case 5:
              {
                return priv____n;
              }
              case 6:
              {
                return priv____i;
              }
              case 7:
              {
                return priv____key1;
              }
              case 8:
              {
                return priv____key2;
              }
            }
          }
          public override void SetValue(int fi, object val)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return;
                }
              }
              case 0:
              {
                {
                  priv____a = ((Z.Pointer) val);
                  return;
                }
              }
              case 1:
              {
                {
                  priv____obj = ((Z.Pointer) val);
                  return;
                }
              }
              case 2:
              {
                {
                  priv____obj1 = ((Z.Pointer) val);
                  return;
                }
              }
              case 3:
              {
                {
                  priv____obj2 = ((Z.Pointer) val);
                  return;
                }
              }
              case 4:
              {
                {
                  priv____obj_end = ((Z.Pointer) val);
                  return;
                }
              }
              case 5:
              {
                {
                  priv____n = ((Int32) val);
                  return;
                }
              }
              case 6:
              {
                {
                  priv____i = ((Int32) val);
                  return;
                }
              }
              case 7:
              {
                {
                  priv____key1 = ((String) val);
                  return;
                }
              }
              case 8:
              {
                {
                  priv____key2 = ((String) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(state.GetCanonicalId(this.priv____a));
            bw.Write(state.GetCanonicalId(this.priv____obj));
            bw.Write(state.GetCanonicalId(this.priv____obj1));
            bw.Write(state.GetCanonicalId(this.priv____obj2));
            bw.Write(state.GetCanonicalId(this.priv____obj_end));
            bw.Write(this.priv____n);
            bw.Write(this.priv____i);
            if ((this.priv____key1 != null))
            {
              bw.Write(this.priv____key1);
            }
            if ((this.priv____key2 != null))
            {
              bw.Write(this.priv____key2);
            }
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____a);
            ft.DoTraversal(this.priv____obj);
            ft.DoTraversal(this.priv____obj1);
            ft.DoTraversal(this.priv____obj2);
            ft.DoTraversal(this.priv____obj_end);
            ft.DoTraversal(this.priv____n);
            ft.DoTraversal(this.priv____i);
            ft.DoTraversal(this.priv____key1);
            ft.DoTraversal(this.priv____key2);
          }
          public Z.Pointer priv____a;
          public static int id____a = 0;
          public Z.Pointer ___a
          {
            get
            {
              return priv____a;
            }
            set
            {
              SetDirty();
              priv____a = value;
            }
          }
          public Z.Pointer priv____obj;
          public static int id____obj = 1;
          public Z.Pointer ___obj
          {
            get
            {
              return priv____obj;
            }
            set
            {
              SetDirty();
              priv____obj = value;
            }
          }
          public Z.Pointer priv____obj1;
          public static int id____obj1 = 2;
          public Z.Pointer ___obj1
          {
            get
            {
              return priv____obj1;
            }
            set
            {
              SetDirty();
              priv____obj1 = value;
            }
          }
          public Z.Pointer priv____obj2;
          public static int id____obj2 = 3;
          public Z.Pointer ___obj2
          {
            get
            {
              return priv____obj2;
            }
            set
            {
              SetDirty();
              priv____obj2 = value;
            }
          }
          public Z.Pointer priv____obj_end;
          public static int id____obj_end = 4;
          public Z.Pointer ___obj_end
          {
            get
            {
              return priv____obj_end;
            }
            set
            {
              SetDirty();
              priv____obj_end = value;
            }
          }
          public int priv____n;
          public static int id____n = 5;
          public int ___n
          {
            get
            {
              return priv____n;
            }
            set
            {
              SetDirty();
              priv____n = value;
            }
          }
          public int priv____i;
          public static int id____i = 6;
          public int ___i
          {
            get
            {
              return priv____i;
            }
            set
            {
              SetDirty();
              priv____i = value;
            }
          }
          public string priv____key1;
          public static int id____key1 = 7;
          public string ___key1
          {
            get
            {
              return priv____key1;
            }
            set
            {
              SetDirty();
              priv____key1 = value;
            }
          }
          public string priv____key2;
          public static int id____key2 = 8;
          public string ___key2
          {
            get
            {
              return priv____key2;
            }
            set
            {
              SetDirty();
              priv____key2 = value;
            }
          }
        }
        public enum Blocks : ushort
        {
          None = 0,
          Enter = 1,
          B0 = 2,
          B1 = 3,
          B2 = 4,
          B3 = 5,
          B4 = 6,
          B5 = 7,
          B6 = 8,
          B7 = 9,
          B8 = 10,
          B9 = 11,
          B10 = 12,
          B11 = 13,
          B12 = 14,
          B13 = 15,
          B14 = 16,
          B15 = 17,
          B16 = 18,
          B17 = 19,
          B18 = 20,
          B19 = 21,
          B20 = 22,
          B21 = 23,
          B22 = 24,
        };
        public LocalVars locals;
        public override UndoableStorage Locals
        {
          get
          {
            return locals;
          }
        }
        public override ushort NextBlock
        {
          get
          {
            return ((ushort) nextBlock);
          }
          set
          {
            nextBlock = ((Blocks) value);
          }
        }
        public override void WriteOutputsString(StateImpl state, BinaryWriter bw)
        {
          outputs.WriteString(state, bw);
        }
        public override string ProgramCounter
        {
          get
          {
            return nextBlock.ToString();
          }
        }
        private Blocks privNextBlock;
        private Blocks privHandlerBlock;
        private class OthersULE
        {
          public bool nextBlockChanged;
          public bool handlerBlockChanged;
          public Blocks savedNextBlock;
          public Blocks savedHandlerBlock;
          public bool IsDirty
          {
            get
            {
              return (nextBlockChanged || handlerBlockChanged);
            }
          }
          public OthersULE()
          {
            
            {
              ;
            }
            {
              nextBlockChanged = false;
              handlerBlockChanged = false;
            }
          }
        }
        private OthersULE othersULE = null;
        private Blocks nextBlock
        {
          get
          {
            return privNextBlock;
          }
          set
          {
            if (((othersULE != null) && !othersULE.nextBlockChanged))
            {
              othersULE.nextBlockChanged = true;
              othersULE.savedNextBlock = privNextBlock;
            }
            privNextBlock = value;
          }
        }
        public Blocks handlerBlock
        {
          get
          {
            return privHandlerBlock;
          }
          set
          {
            if (((othersULE != null) && !othersULE.handlerBlockChanged))
            {
              othersULE.handlerBlockChanged = true;
              othersULE.savedHandlerBlock = privHandlerBlock;
            }
            privHandlerBlock = value;
          }
        }
        public override object DoCheckInOthers()
        {
          OthersULE res;
          if ((othersULE == null))
          {
            othersULE = new OthersULE();
            return null;
          }
          if (!othersULE.IsDirty)
          {
            return null;
          }
          res = othersULE;
          othersULE = new OthersULE();
          return res;
        }
        public override void DoRevertOthers()
        {
          Debug.Assert((othersULE != null));
          if (othersULE.nextBlockChanged)
          {
            privNextBlock = othersULE.savedNextBlock;
            othersULE.nextBlockChanged = false;
          }
          if (othersULE.handlerBlockChanged)
          {
            privHandlerBlock = othersULE.savedHandlerBlock;
            othersULE.handlerBlockChanged = false;
          }
        }
        public override void DoRollbackOthers(object[] ules)
        {
          Debug.Assert((othersULE != null));
          Debug.Assert(!othersULE.IsDirty);
          {
            for (
              int i = 0, n = ules.Length;
              (i < n);
              i++
            )
            {
              if ((ules[i] == null))
              {
                continue;
              }
              OthersULE ule = ((OthersULE) ules[i]);
              if (ule.nextBlockChanged)
              {
                privNextBlock = ule.savedNextBlock;
              }
              if (ule.handlerBlockChanged)
              {
                privHandlerBlock = ule.savedHandlerBlock;
              }
            }
          }
        }
        public override bool RaiseZingException(int exception)
        {
          if ((handlerBlock == Blocks.None))
          {
            return false;
          }
          this.CurrentException = exception;
          nextBlock = handlerBlock;
          return true;
        }
        public override void Dispatch(Z.Process p)
        {
          switch (nextBlock) {
            case Blocks.Enter:
            {
              Enter(p);
              break;
            }
            default:
            {
              throw new ApplicationException();
            }
            case Blocks.B0:
            {
              B0(p);
              break;
            }
            case Blocks.B1:
            {
              B1(p);
              break;
            }
            case Blocks.B2:
            {
              B2(p);
              break;
            }
            case Blocks.B3:
            {
              B3(p);
              break;
            }
            case Blocks.B4:
            {
              B4(p);
              break;
            }
            case Blocks.B5:
            {
              B5(p);
              break;
            }
            case Blocks.B6:
            {
              B6(p);
              break;
            }
            case Blocks.B7:
            {
              B7(p);
              break;
            }
            case Blocks.B8:
            {
              B8(p);
              break;
            }
            case Blocks.B9:
            {
              B9(p);
              break;
            }
            case Blocks.B10:
            {
              B10(p);
              break;
            }
            case Blocks.B11:
            {
              B11(p);
              break;
            }
            case Blocks.B12:
            {
              B12(p);
              break;
            }
            case Blocks.B13:
            {
              B13(p);
              break;
            }
            case Blocks.B14:
            {
              B14(p);
              break;
            }
            case Blocks.B15:
            {
              B15(p);
              break;
            }
            case Blocks.B16:
            {
              B16(p);
              break;
            }
            case Blocks.B17:
            {
              B17(p);
              break;
            }
            case Blocks.B18:
            {
              B18(p);
              break;
            }
            case Blocks.B19:
            {
              B19(p);
              break;
            }
            case Blocks.B20:
            {
              B20(p);
              break;
            }
            case Blocks.B21:
            {
              B21(p);
              break;
            }
            case Blocks.B22:
            {
              B22(p);
              break;
            }
          }
        }
        public override ulong GetRunnableJoinStatements(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return ~((ulong) 0);
            }
          }
        }
        public override void AddBarriers(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return;
            }
          }
        }
        public override bool IsAtomicEntryBlock()
        {
          switch (nextBlock) {
            default:
            {
              return false;
            }
          }
        }
        public override bool ValidEndState
        {
          get
          {
            switch (nextBlock) {
              default:
              {
                return false;
              }
            }
          }
        }
        public override Z.ZingSourceContext Context
        {
          get
          {
            switch (nextBlock) {
              default:
              {
                throw new ApplicationException();
              }
              case Blocks.B0:
              {
                return new ZingSourceContext(0, 771, 772);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 743, 767);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 707, 736);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 684, 693);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 654, 673);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 629, 648);
              }
              case Blocks.B6:
              {
                return new ZingSourceContext(0, 603, 623);
              }
              case Blocks.B7:
              {
                return new ZingSourceContext(0, 571, 592);
              }
              case Blocks.B8:
              {
                return new ZingSourceContext(0, 544, 565);
              }
              case Blocks.B9:
              {
                return new ZingSourceContext(0, 522, 533);
              }
              case Blocks.B10:
              {
                return new ZingSourceContext(0, 505, 516);
              }
              case Blocks.B11:
              {
                return new ZingSourceContext(0, 485, 494);
              }
              case Blocks.B12:
              {
                return new ZingSourceContext(0, 458, 474);
              }
              case Blocks.B13:
              {
                return new ZingSourceContext(0, 436, 452);
              }
              case Blocks.B14:
              {
                return new ZingSourceContext(0, 416, 425);
              }
              case Blocks.B15:
              {
                return new ZingSourceContext(0, 399, 410);
              }
              case Blocks.B16:
              {
                return new ZingSourceContext(0, 376, 388);
              }
              case Blocks.B17:
              {
                return new ZingSourceContext(0, 333, 367);
              }
              case Blocks.B18:
              {
                return new ZingSourceContext(0, 319, 325);
              }
              case Blocks.B19:
              {
                return new ZingSourceContext(0, 302, 307);
              }
              case Blocks.B20:
              {
                return new ZingSourceContext(0, 272, 291);
              }
              case Blocks.B21:
              {
                return new ZingSourceContext(0, 272, 291);
              }
              case Blocks.B22:
              {
                return new ZingSourceContext(0, 247, 261);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 224, 242);
              }
            }
          }
        }
        public override Z.ZingAttribute ContextAttribute
        {
          get
          {
            switch (nextBlock) {
              default:
              {
                return null;
              }
            }
          }
        }
        private static Hints hints = new Hints();
        public override Hints Hints
        {
          get
          {
            return hints;
          }
        }
        private static Hashtable summaries = new Hashtable();
        public override Hashtable SummaryHashtable
        {
          get
          {
            return summaries;
          }
        }
        private static SortedList summaryTable = new SortedList();
        public override SortedList SummaryTable
        {
          get
          {
            return summaryTable;
          }
        }
        private static Hashtable transientStates = new Hashtable();
        public override Hashtable TransientStates
        {
          get
          {
            return transientStates;
          }
        }
        public static int executionCount = 0;
        public override int ExecutionCount
        {
          get
          {
            return executionCount;
          }
          set
          {
            executionCount = value;
          }
        }
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 3;
        public sealed class InputVars
          : UndoableStorage
        {
          internal ZingMethod stackFrame;
          internal InputVars(ZingMethod zm)
          {
            
            {
              ;
            }
            {
              stackFrame = zm;
            }
          }
          public override UndoableStorage MakeInstance()
          {
            return new InputVars(stackFrame);
          }
          public override void CopyContents(UndoableStorage usSrc)
          {
            InputVars src = (usSrc as InputVars);
            if ((src == null))
            {
              throw new ArgumentException(@"expecting instance of InputVars as source");
            }
          }
          public override object GetValue(int fi)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return null;
                }
              }
            }
          }
          public override void SetValue(int fi, object val)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
          }
          public void TraverseFields(FieldTraverser ft)
          {
          }
        }
        public sealed class OutputVars
          : UndoableStorage
        {
          private ZingMethod stackFrame;
          internal OutputVars(ZingMethod zm)
          {
            
            {
              ;
            }
            {
              stackFrame = zm;
            }
          }
          public override UndoableStorage MakeInstance()
          {
            return new OutputVars(stackFrame);
          }
          public override void CopyContents(UndoableStorage usSrc)
          {
            OutputVars src = (usSrc as OutputVars);
            if ((src == null))
            {
              throw new ArgumentException(@"expecting instance of OutputVars as source");
            }
          }
          public override object GetValue(int fi)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return null;
                }
              }
            }
          }
          public override void SetValue(int fi, object val)
          {
            switch (fi) {
              default:
              {
                {
                  Debug.Assert(false);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
          }
          public void TraverseFields(FieldTraverser ft)
          {
          }
        }
        public InputVars inputs;
        public OutputVars outputs;
        public override UndoableStorage Inputs
        {
          get
          {
            return inputs;
          }
        }
        public override UndoableStorage Outputs
        {
          get
          {
            return outputs;
          }
        }
        public override ZingMethod Clone(Z.StateImpl myState, Z.Process myProcess, bool shallowCopy)
        {
          ___main clone = new ___main(((Application) myState));
          clone.locals = new LocalVars(clone);
          clone.locals.CopyContents(locals);
          clone.inputs = new InputVars(clone);
          clone.inputs.CopyContents(inputs);
          clone.outputs = new OutputVars(clone);
          clone.outputs.CopyContents(outputs);
          clone.nextBlock = this.nextBlock;
          clone.handlerBlock = this.handlerBlock;
          clone.SavedAtomicityLevel = this.SavedAtomicityLevel;
          if ((this.Caller != null))
          {
            if (shallowCopy)
            {
              clone.Caller = null;
            }
            else
            {
              clone.Caller = this.Caller.Clone(myState, myProcess, false);
            }
          }
          else
          {
            if ((myProcess != null))
            {
              myProcess.EntryPoint = this;
            }
          }
          return clone;
        }
        public override void WriteString(StateImpl state, BinaryWriter bw)
        {
          bw.Write(typeId);
          bw.Write(((ushort) nextBlock));
          inputs.WriteString(state, bw);
          outputs.WriteString(state, bw);
          locals.WriteString(state, bw);
        }
        public override void TraverseFields(FieldTraverser ft)
        {
          ft.DoTraversal(typeId);
          ft.DoTraversal(nextBlock);
          inputs.TraverseFields(ft);
          outputs.TraverseFields(ft);
          locals.TraverseFields(ft);
        }
        public void B0(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          p.Return(new ZingSourceContext(0, 771, 772), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!(locals.___obj_end == 0))
          {
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"obj_end == null");
          }
          nextBlock = Blocks.B0;
        }
        public void B2(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            locals.___obj_end = (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___Lookup(p, @"key1_10");
          }
          nextBlock = Blocks.B1;
        }
        public void B3(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___i = (locals.___i + 1);
          nextBlock = Blocks.B18;
        }
        public void B4(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!((((Z.Application.___A) application.LookupObject(locals.___obj1))).___x == locals.___i))
          {
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"obj1.x == i");
          }
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!(locals.___obj == locals.___obj1))
          {
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"obj == obj1");
          }
          nextBlock = Blocks.B4;
        }
        public void B6(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!(locals.___obj1 == locals.___obj2))
          {
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"obj1 == obj2");
          }
          nextBlock = Blocks.B5;
        }
        public void B7(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            locals.___obj2 = (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___Lookup(p, locals.___key2);
          }
          nextBlock = Blocks.B6;
        }
        public void B8(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            locals.___obj1 = (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___Lookup(p, locals.___key1);
          }
          nextBlock = Blocks.B7;
        }
        public void B9(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___obj2 = 0;
          nextBlock = Blocks.B8;
        }
        public void B10(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___obj1 = 0;
          nextBlock = Blocks.B9;
        }
        public void B11(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___A) application.LookupObject(locals.___obj))).___x = locals.___i;
          nextBlock = Blocks.B10;
        }
        public void B12(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___Add(p, locals.___key2, locals.___obj);
          }
          nextBlock = Blocks.B11;
        }
        public void B13(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___Add(p, locals.___key1, locals.___obj);
          }
          nextBlock = Blocks.B12;
        }
        public void B14(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___A) application.LookupObject(locals.___obj))).___x = 0;
          nextBlock = Blocks.B13;
        }
        public void B15(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___obj = application.Allocate(new Z.Application.___A(application));
          nextBlock = Blocks.B14;
        }
        public void B16(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___key2 = @"abc";
          nextBlock = Blocks.B15;
        }
        public void B17(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            locals.___key1 = (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___StringHelper(p, @"key1_", locals.___i);
          }
          nextBlock = Blocks.B16;
        }
        public void B18(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((locals.___i <= locals.___n))
          {
            nextBlock = Blocks.B17;
          }
          else
          {
            nextBlock = Blocks.B2;
          }
        }
        public void B19(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___i = 1;
          nextBlock = Blocks.B18;
        }
        public void B20(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___n = ((Int32) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B19;
        }
        public void B21(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(Application.___MyRange)));
          nextBlock = Blocks.B20;
        }
        public void B22(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            (((Microsoft.Zing.___AssocArray) (application.LookupObject(locals.___a)))).___initialize(p);
          }
          nextBlock = Blocks.B21;
        }
        public void Enter(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___a = application.Allocate(new Z.___AssocArray(application));
          nextBlock = Blocks.B22;
        }
      }
    }
    public static object[] GetChoicesForType(System.Type type)
    {
      if ((type == typeof(Application.___MyRange)))
      {
        return Application.___MyRangeChoices;
      }
      else
      {
        if ((type == typeof(bool)))
        {
          return Application.boolChoices;
        }
        else
        {
          throw new ArgumentException((@"Invalid type for choice operator : " + type));
        }
      }
    }
  }
}
