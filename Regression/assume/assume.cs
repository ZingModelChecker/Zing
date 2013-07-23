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
    private static string[] sourceFiles = new string[] { @"D:\Root\ZING\src.V1\Regression\assume\assume.zing" };
    public override string[] GetSourceFiles()
    {
      return Application.sourceFiles;
    }
    private static string[] sources = new string[] { @"//
// assume.zing - the ""assume"" statement
//

//
// In Zing, the ""assume"" statement is used to identify states that
// are not interesting to consider. One simple example of this, in
// which we wish to apply a constraint to a set of non-deterministic
// choices, is demonstrated here. We perform two ""choose"" operations
// on the enumeration E, which has 3 elements. Following the second
// ""choose"", there are 9 alternatives available for consideration by
// the state explorer. A ""choose"" statement allows us to effectively
// prune those combinations where some predicate is satisfied. In this
// case, our predicate eliminates those states in which the same choice
// was made by each ""choose"", which reduces the number of states to
// consider from 9 to 6.
//
// In concurrent systems, the assume statement can also be helpful. To
// ensure that a Zing channel never contains more than one element, we
// can require message senders to block until the channel is empty and
// then (atomically) send their message. Alternatively, we can simply
// ""assume"" the emptiness of the channel before sending (atomically,
// again). The advantage here is that instead of reaching an undesirable
// state and then waiting for the desired predicate to become true, we
// simply remove from consideration those interleavings for which the
// predicate was allowed to become false. For this scenario, we wind up
// only considering ""fair"" interleavings between producers and consumers.
//

enum E { element1, element2, element3 };

class Test {
    activate static void main() {
        E e1;
        E e2;
        
        e1 = choose(E);
        e2 = choose(E);
        
        assume(e1 != e2);
        
        trace(""e1={0}, e2={1}"", e1, e2);
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
    internal enum ___E : int
    {
      ___element1,
      ___element2,
      ___element3,
    };
    private static object[] ___EChoices = new object[] { ___E.___element1, ___E.___element2, ___E.___element3 };
    internal class ___Test
      : Z.ZingClass
    {
      public ___Test(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Test()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Test(___Test c) : base(c)
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
          return ___Test.typeId;
        }
      }
      private static readonly short typeId = 1;
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
        ___Test newObj = new ___Test(this);
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
            this.priv____e1 = src.priv____e1;
            this.priv____e2 = src.priv____e2;
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
                return priv____e1;
              }
              case 1:
              {
                return priv____e2;
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
                  priv____e1 = ((Application.___E) val);
                  return;
                }
              }
              case 1:
              {
                {
                  priv____e2 = ((Application.___E) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(((byte) this.priv____e1));
            bw.Write(((byte) this.priv____e2));
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____e1);
            ft.DoTraversal(this.priv____e2);
          }
          public Application.___E priv____e1;
          public static int id____e1 = 0;
          public Application.___E ___e1
          {
            get
            {
              return priv____e1;
            }
            set
            {
              SetDirty();
              priv____e1 = value;
            }
          }
          public Application.___E priv____e2;
          public static int id____e2 = 1;
          public Application.___E ___e2
          {
            get
            {
              return priv____e2;
            }
            set
            {
              SetDirty();
              priv____e2 = value;
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
                return new ZingSourceContext(0, 1788, 1789);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 1750, 1781);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 1713, 1719);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 1678, 1692);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 1678, 1692);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 1653, 1667);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 1653, 1667);
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
        private static readonly short typeId = 2;
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
          p.Return(new ZingSourceContext(0, 1788, 1789), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          application.Trace(new ZingSourceContext(0, 1750, 1781), null, @"e1={0}, e2={1}", locals.___e1, locals.___e2);
          nextBlock = Blocks.B0;
        }
        public void B2(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!(locals.___e1 != locals.___e2))
          {
            this.StateImpl.Exception = new Z.ZingAssumeFailureException(@"e1 != e2");
          }
          nextBlock = Blocks.B1;
        }
        public void B3(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___e2 = ((___E) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B2;
        }
        public void B4(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(Application.___E)));
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___e1 = ((___E) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B4;
        }
        public void Enter(Z.Process p)
        {
          p.MiddleOfTransition = false;
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(Application.___E)));
          nextBlock = Blocks.B5;
        }
      }
    }
    public static object[] GetChoicesForType(System.Type type)
    {
      if ((type == typeof(Application.___E)))
      {
        return Application.___EChoices;
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
