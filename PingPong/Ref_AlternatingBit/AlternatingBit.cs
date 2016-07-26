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
      this.globals.priv____Main____QueueSize = 2;
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
    private static string[] sourceFiles = new string[] { @"D:\Workspace\Zing\Regression\AlternatingBit\AlternatingBit.zing" };
    public override string[] GetSourceFiles()
    {
      return Application.sourceFiles;
    }
    private static string[] sources = new string[] { @"class Msg {
    bool body;
    bool bit;
};

class Ack {
    bool bit;
};

chan MsgChan Msg;
chan AckChan Ack;

chan BoolChan bool;

class Sender {
    static MsgChan xmit;
    static AckChan recv;
    
    static void TransmitMsg(bool body, bool bit)
    {
        Msg m;
        
        select {
            wait(true) -> {
                assume(sizeof(xmit) < Main.QueueSize);
                m = new Msg;
                m.body = body;
                m.bit = bit;
                send(xmit, m);
            }
            wait(true) -> /* lost message */ ;
        }
    }
    
    static void Run()
    {
        bool currentBit = false;
        Ack a;
        bool body;
        bool gotAck;
        
        while (true) {
            atomic {
                body = choose(bool);
                send(Main.reliableChan, body);
                
                TransmitMsg(body, currentBit);
                
                gotAck = false;
            }
            
            while (!gotAck) {
                atomic {
                    select first {
                        receive(recv, a) -> gotAck = (a.bit == currentBit);
                        timeout -> TransmitMsg(body, currentBit);
                    }
                }
            }
            
            currentBit = !currentBit;
        }
    }
};

class Receiver {
    static MsgChan recv;
    static AckChan xmit;
    
    static void TransmitAck(bool bit)
    {
        Ack a;
        
        select {
            wait(true) -> {
                a = new Ack;
                a.bit = bit;
                send(xmit, a);
            }
            wait(true) -> /* lost ack */ ;
        }
    }
    
    static void Run()
    {
        bool expectedBit = false;
        bool trueBody;
        Msg m;
        
        // Loop forever consuming messages
        while (true) {
            select { receive(recv, m) -> ; }
                
            atomic {
                // Always send an ack with the same bit
                TransmitAck(m.bit);
                
                if (expectedBit == m.bit) {
                    // Consume the message here and verify it's body matches
                    // what we received through the reliable channel
                    select { receive(Main.reliableChan, trueBody) -> ; }
                    assert(trueBody == m.body);
                    
                    expectedBit = !expectedBit;
                }
            }
        }
    }
};


class Main {

    static int QueueSize = 2;
    
    static BoolChan reliableChan;
    
    activate static void Run()
    {
        atomic {
            reliableChan = new BoolChan;
            
            Sender.xmit = Receiver.recv = new MsgChan;
            Sender.recv = Receiver.xmit = new AckChan;
            
            async Sender.Run();
            async Receiver.Run();
        }
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
        bw.Write(state.GetCanonicalId(priv____Sender____xmit));
        bw.Write(state.GetCanonicalId(priv____Sender____recv));
        bw.Write(state.GetCanonicalId(priv____Receiver____recv));
        bw.Write(state.GetCanonicalId(priv____Receiver____xmit));
        bw.Write(priv____Main____QueueSize);
        bw.Write(state.GetCanonicalId(priv____Main____reliableChan));
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(UnallocatedWrites);
        ft.DoTraversal(priv____Sender____xmit);
        ft.DoTraversal(priv____Sender____recv);
        ft.DoTraversal(priv____Receiver____recv);
        ft.DoTraversal(priv____Receiver____xmit);
        ft.DoTraversal(priv____Main____QueueSize);
        ft.DoTraversal(priv____Main____reliableChan);
      }
      public override void CopyContents(UndoableStorage zgSrc)
      {
        GlobalVars src = (zgSrc as GlobalVars);
        if ((src == null))
        {
          throw new ArgumentException(@"expecting global vars here");
        }
        this.priv____Sender____xmit = src.priv____Sender____xmit;
        this.priv____Sender____recv = src.priv____Sender____recv;
        this.priv____Receiver____recv = src.priv____Receiver____recv;
        this.priv____Receiver____xmit = src.priv____Receiver____xmit;
        this.priv____Main____QueueSize = src.priv____Main____QueueSize;
        this.priv____Main____reliableChan = src.priv____Main____reliableChan;
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
          case 0:
          {
            return priv____Sender____xmit;
          }
          case 1:
          {
            return priv____Sender____recv;
          }
          case 2:
          {
            return priv____Receiver____recv;
          }
          case 3:
          {
            return priv____Receiver____xmit;
          }
          case 4:
          {
            return priv____Main____QueueSize;
          }
          case 5:
          {
            return priv____Main____reliableChan;
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
              priv____Sender____xmit = ((Z.Pointer) val);
              return;
            }
          }
          case 1:
          {
            {
              priv____Sender____recv = ((Z.Pointer) val);
              return;
            }
          }
          case 2:
          {
            {
              priv____Receiver____recv = ((Z.Pointer) val);
              return;
            }
          }
          case 3:
          {
            {
              priv____Receiver____xmit = ((Z.Pointer) val);
              return;
            }
          }
          case 4:
          {
            {
              priv____Main____QueueSize = ((Int32) val);
              return;
            }
          }
          case 5:
          {
            {
              priv____Main____reliableChan = ((Z.Pointer) val);
              return;
            }
          }
        }
      }
      public Z.Pointer priv____Sender____xmit;
      public static int id____Sender____xmit = 0;
      public Z.Pointer ___Sender____xmit
      {
        get
        {
          return priv____Sender____xmit;
        }
        set
        {
          SetDirty();
          priv____Sender____xmit = value;
        }
      }
      public Z.Pointer priv____Sender____recv;
      public static int id____Sender____recv = 1;
      public Z.Pointer ___Sender____recv
      {
        get
        {
          return priv____Sender____recv;
        }
        set
        {
          SetDirty();
          priv____Sender____recv = value;
        }
      }
      public Z.Pointer priv____Receiver____recv;
      public static int id____Receiver____recv = 2;
      public Z.Pointer ___Receiver____recv
      {
        get
        {
          return priv____Receiver____recv;
        }
        set
        {
          SetDirty();
          priv____Receiver____recv = value;
        }
      }
      public Z.Pointer priv____Receiver____xmit;
      public static int id____Receiver____xmit = 3;
      public Z.Pointer ___Receiver____xmit
      {
        get
        {
          return priv____Receiver____xmit;
        }
        set
        {
          SetDirty();
          priv____Receiver____xmit = value;
        }
      }
      public int priv____Main____QueueSize;
      public static int id____Main____QueueSize = 4;
      public int ___Main____QueueSize
      {
        get
        {
          return priv____Main____QueueSize;
        }
        set
        {
          SetDirty();
          priv____Main____QueueSize = value;
        }
      }
      public Z.Pointer priv____Main____reliableChan;
      public static int id____Main____reliableChan = 5;
      public Z.Pointer ___Main____reliableChan
      {
        get
        {
          return priv____Main____reliableChan;
        }
        set
        {
          SetDirty();
          priv____Main____reliableChan = value;
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
    internal class ___Msg
      : Z.ZingClass
    {
      public ___Msg(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Msg()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Msg(___Msg c) : base(c)
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
          return ___Msg.typeId;
        }
      }
      private static readonly short typeId = 1;
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        bw.Write(this.TypeId);
        bw.Write(this.priv____body);
        bw.Write(this.priv____bit);
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(this.TypeId);
        ft.DoTraversal(this.priv____body);
        ft.DoTraversal(this.priv____bit);
      }
      public override object Clone()
      {
        ___Msg newObj = new ___Msg(this);
        {
          newObj.priv____body = this.priv____body;
          newObj.priv____bit = this.priv____bit;
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
            return priv____body;
          }
          case 1:
          {
            return priv____bit;
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
              priv____body = ((Boolean) val);
              return;
            }
          }
          case 1:
          {
            {
              priv____bit = ((Boolean) val);
              return;
            }
          }
        }
      }
      public bool priv____body;
      public static int id____body = 0;
      public bool ___body
      {
        get
        {
          return priv____body;
        }
        set
        {
          SetDirty();
          priv____body = value;
        }
      }
      public bool priv____bit;
      public static int id____bit = 1;
      public bool ___bit
      {
        get
        {
          return priv____bit;
        }
        set
        {
          SetDirty();
          priv____bit = value;
        }
      }
    }
    internal class ___Ack
      : Z.ZingClass
    {
      public ___Ack(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Ack()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Ack(___Ack c) : base(c)
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
          return ___Ack.typeId;
        }
      }
      private static readonly short typeId = 2;
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        bw.Write(this.TypeId);
        bw.Write(this.priv____bit);
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(this.TypeId);
        ft.DoTraversal(this.priv____bit);
      }
      public override object Clone()
      {
        ___Ack newObj = new ___Ack(this);
        {
          newObj.priv____bit = this.priv____bit;
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
            return priv____bit;
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
              priv____bit = ((Boolean) val);
              return;
            }
          }
        }
      }
      public bool priv____bit;
      public static int id____bit = 0;
      public bool ___bit
      {
        get
        {
          return priv____bit;
        }
        set
        {
          SetDirty();
          priv____bit = value;
        }
      }
    }
    internal sealed class ___MsgChan
      : Z.ZingChan
    {
      public ___MsgChan(Application app) : base(app)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___MsgChan()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___MsgChan(___MsgChan c) : base(c)
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
          return ___MsgChan.typeId;
        }
      }
      private static readonly short typeId = 3;
      public override Type MessageType
      {
        get
        {
          return typeof(Microsoft.Zing.Application.___Msg);
        }
      }
      public override object Clone()
      {
        ___MsgChan newObj = new ___MsgChan(this);
        foreach (Z.Pointer ptr in this.Queue)
        {
          newObj.Queue.Enqueue(ptr);
        }
        return newObj;
      }
    }
    internal sealed class ___AckChan
      : Z.ZingChan
    {
      public ___AckChan(Application app) : base(app)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___AckChan()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___AckChan(___AckChan c) : base(c)
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
          return ___AckChan.typeId;
        }
      }
      private static readonly short typeId = 4;
      public override Type MessageType
      {
        get
        {
          return typeof(Microsoft.Zing.Application.___Ack);
        }
      }
      public override object Clone()
      {
        ___AckChan newObj = new ___AckChan(this);
        foreach (Z.Pointer ptr in this.Queue)
        {
          newObj.Queue.Enqueue(ptr);
        }
        return newObj;
      }
    }
    internal sealed class ___BoolChan
      : Z.ZingChan
    {
      public ___BoolChan(Application app) : base(app)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___BoolChan()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___BoolChan(___BoolChan c) : base(c)
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
          return ___BoolChan.typeId;
        }
      }
      private static readonly short typeId = 5;
      public override Type MessageType
      {
        get
        {
          return typeof(System.Boolean);
        }
      }
      public override object Clone()
      {
        ___BoolChan newObj = new ___BoolChan(this);
        foreach (System.Boolean v in this.Queue)
        {
          newObj.Queue.Enqueue(v);
        }
        return newObj;
      }
      public override void WriteString(StateImpl state, BinaryWriter bw)
      {
        bw.Write(this.TypeId);
        bw.Write(this.Count);
        foreach (System.Boolean x in this.Queue)
        {
          bw.Write(x);
        }
      }
      public override void TraverseFields(FieldTraverser ft)
      {
        ft.DoTraversal(this.TypeId);
        ft.DoTraversal(this.Count);
        foreach (System.Boolean x in this.Queue)
        {
          ft.DoTraversal(x);
        }
      }
    }
    internal class ___Sender
      : Z.ZingClass
    {
      public ___Sender(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Sender()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Sender(___Sender c) : base(c)
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
          return ___Sender.typeId;
        }
      }
      private static readonly short typeId = 6;
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
        ___Sender newObj = new ___Sender(this);
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
      internal sealed class ___TransmitMsg
        : Z.ZingMethod
      {
        public ___TransmitMsg(Application app)
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
            this.priv____m = src.priv____m;
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
                return priv____m;
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
                  priv____m = ((Z.Pointer) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(state.GetCanonicalId(this.priv____m));
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____m);
          }
          public Z.Pointer priv____m;
          public static int id____m = 0;
          public Z.Pointer ___m
          {
            get
            {
              return priv____m;
            }
            set
            {
              SetDirty();
              priv____m = value;
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
          }
        }
        public override ulong GetRunnableJoinStatements(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return ~((ulong) 0);
            }
            case Blocks.Enter:
            {
              return (((true ? 1ul : 0ul)) | ((true ? 2ul : 0ul)));
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
                return new ZingSourceContext(0, 609, 610);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 515, 528);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 485, 496);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 453, 466);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 423, 434);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 367, 373);
              }
              case Blocks.B6:
              {
                return new ZingSourceContext(0, 367, 543);
              }
              case Blocks.B7:
              {
                return new ZingSourceContext(0, 609, 610);
              }
              case Blocks.B8:
              {
                return new ZingSourceContext(0, 591, 592);
              }
              case Blocks.B9:
              {
                return new ZingSourceContext(0, 312, 602);
              }
              case Blocks.B10:
              {
                return new ZingSourceContext(0, 558, 602);
              }
              case Blocks.B11:
              {
                return new ZingSourceContext(0, 334, 558);
              }
              case Blocks.B12:
              {
                return new ZingSourceContext(0, 312, 602);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 312, 602);
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
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 7;
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
            this.priv____body = src.priv____body;
            this.priv____bit = src.priv____bit;
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
                return priv____body;
              }
              case 1:
              {
                return priv____bit;
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
                  priv____body = ((Boolean) val);
                  return;
                }
              }
              case 1:
              {
                {
                  priv____bit = ((Boolean) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(this.priv____body);
            bw.Write(this.priv____bit);
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____body);
            ft.DoTraversal(this.priv____bit);
          }
          public bool priv____body;
          public static int id____body = 0;
          public bool ___body
          {
            get
            {
              return priv____body;
            }
            set
            {
              SetDirty();
              priv____body = value;
            }
          }
          public bool priv____bit;
          public static int id____bit = 1;
          public bool ___bit
          {
            get
            {
              return priv____bit;
            }
            set
            {
              SetDirty();
              priv____bit = value;
            }
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
          ___TransmitMsg clone = new ___TransmitMsg(((Application) myState));
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
        public ___TransmitMsg(Application app, bool ___body, bool ___bit)
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
          inputs.___body = ___body;
          inputs.___bit = ___bit;
        }
        public void B0(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          p.Return(new ZingSourceContext(0, 609, 610), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___MsgChan) application.LookupObject(application.globals.___Sender____xmit))).Send(this.StateImpl, locals.___m, new ZingSourceContext(0, 515, 528), null);
          nextBlock = Blocks.B0;
        }
        public void B2(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___Msg) application.LookupObject(locals.___m))).___bit = inputs.___bit;
          nextBlock = Blocks.B1;
        }
        public void B3(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___Msg) application.LookupObject(locals.___m))).___body = inputs.___body;
          nextBlock = Blocks.B2;
        }
        public void B4(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___m = application.Allocate(new Z.Application.___Msg(application));
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!((((ZingCollectionType) application.LookupObject(application.globals.___Sender____xmit))).Count < application.globals.___Main____QueueSize))
          {
            this.StateImpl.Exception = new Z.ZingAssumeFailureException(@"sizeof(xmit) < Main.QueueSize)");
          }
          nextBlock = Blocks.B4;
        }
        public void B6(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B5;
        }
        public void B7(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B0;
        }
        public void B8(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B7;
        }
        public void B9(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
          p.Return(new ZingSourceContext(0, 609, 610), null);
          StateImpl.IsReturn = true;
        }
        public void B10(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((((this.SavedRunnableJoinStatements & 2ul)) != 0ul))
          {
            nextBlock = Blocks.B8;
          }
          else
          {
            nextBlock = Blocks.B9;
          }
        }
        public void B11(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((((this.SavedRunnableJoinStatements & 1ul)) != 0ul))
          {
            nextBlock = Blocks.B6;
          }
          else
          {
            nextBlock = Blocks.B10;
          }
        }
        public void B12(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          SavedRunnableJoinStatements = ((ulong) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B11;
        }
        public void Enter(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
          if (application.SetPendingSelectChoices(p, SavedRunnableJoinStatements))
          {
            nextBlock = Blocks.B12;
          }
          else
          {
            nextBlock = Blocks.B11;
          }
        }
      }
      internal sealed class ___Run
        : Z.ZingMethod
      {
        public ___Run(Application app)
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
            this.priv____currentBit = src.priv____currentBit;
            this.priv____a = src.priv____a;
            this.priv____body = src.priv____body;
            this.priv____gotAck = src.priv____gotAck;
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
                return priv____currentBit;
              }
              case 1:
              {
                return priv____a;
              }
              case 2:
              {
                return priv____body;
              }
              case 3:
              {
                return priv____gotAck;
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
                  priv____currentBit = ((Boolean) val);
                  return;
                }
              }
              case 1:
              {
                {
                  priv____a = ((Z.Pointer) val);
                  return;
                }
              }
              case 2:
              {
                {
                  priv____body = ((Boolean) val);
                  return;
                }
              }
              case 3:
              {
                {
                  priv____gotAck = ((Boolean) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(this.priv____currentBit);
            bw.Write(state.GetCanonicalId(this.priv____a));
            bw.Write(this.priv____body);
            bw.Write(this.priv____gotAck);
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____currentBit);
            ft.DoTraversal(this.priv____a);
            ft.DoTraversal(this.priv____body);
            ft.DoTraversal(this.priv____gotAck);
          }
          public bool priv____currentBit;
          public static int id____currentBit = 0;
          public bool ___currentBit
          {
            get
            {
              return priv____currentBit;
            }
            set
            {
              SetDirty();
              priv____currentBit = value;
            }
          }
          public Z.Pointer priv____a;
          public static int id____a = 1;
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
          public bool priv____body;
          public static int id____body = 2;
          public bool ___body
          {
            get
            {
              return priv____body;
            }
            set
            {
              SetDirty();
              priv____body = value;
            }
          }
          public bool priv____gotAck;
          public static int id____gotAck = 3;
          public bool ___gotAck
          {
            get
            {
              return priv____gotAck;
            }
            set
            {
              SetDirty();
              priv____gotAck = value;
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
          }
        }
        public override ulong GetRunnableJoinStatements(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return ~((ulong) 0);
            }
            case Blocks.B12:
            {
              return ((((((Z.Application.___AckChan) application.LookupObject(application.globals.___Sender____recv))).CanReceive ? 1ul : 0ul)) | ((true ? 2ul : 0ul)));
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
            case Blocks.B12:
            {
              return true;
            }
            case Blocks.B20:
            {
              return true;
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
                return new ZingSourceContext(0, 1390, 1391);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 1348, 1372);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 1390, 1391);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 1165, 1194);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 1165, 1194);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 1233, 1262);
              }
              case Blocks.B6:
              {
                return new ZingSourceContext(0, 1233, 1262);
              }
              case Blocks.B7:
              {
                return new ZingSourceContext(0, 1233, 1262);
              }
              case Blocks.B8:
              {
                return new ZingSourceContext(0, 1105, 1285);
              }
              case Blocks.B9:
              {
                return new ZingSourceContext(0, 1222, 1285);
              }
              case Blocks.B10:
              {
                return new ZingSourceContext(0, 1145, 1222);
              }
              case Blocks.B11:
              {
                return new ZingSourceContext(0, 1145, 1222);
              }
              case Blocks.B12:
              {
                return new ZingSourceContext(0, 1105, 1285);
              }
              case Blocks.B13:
              {
                return new ZingSourceContext(0, 1047, 1054);
              }
              case Blocks.B14:
              {
                return new ZingSourceContext(0, 1390, 1391);
              }
              case Blocks.B15:
              {
                return new ZingSourceContext(0, 982, 996);
              }
              case Blocks.B16:
              {
                return new ZingSourceContext(0, 982, 996);
              }
              case Blocks.B17:
              {
                return new ZingSourceContext(0, 916, 945);
              }
              case Blocks.B18:
              {
                return new ZingSourceContext(0, 850, 879);
              }
              case Blocks.B19:
              {
                return new ZingSourceContext(0, 812, 831);
              }
              case Blocks.B20:
              {
                return new ZingSourceContext(0, 812, 831);
              }
              case Blocks.B21:
              {
                return new ZingSourceContext(0, 765, 769);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 656, 680);
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
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 8;
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
          ___Run clone = new ___Run(((Application) myState));
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
          p.Return(new ZingSourceContext(0, 1390, 1391), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___currentBit = !locals.___currentBit;
          nextBlock = Blocks.B21;
        }
        public void B2(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B13;
        }
        public void B3(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          locals.___gotAck = ((((Z.Application.___Ack) application.LookupObject(locals.___a))).___bit == locals.___currentBit);
          nextBlock = Blocks.B2;
        }
        public void B4(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            p.LastFunctionCompleted = null;
          }
          nextBlock = Blocks.B2;
        }
        public void B6(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            Z.Application.___Sender.___TransmitMsg callee = new Z.Application.___Sender.___TransmitMsg(application);
            callee.inputs.priv____body = locals.___body;
            callee.inputs.priv____bit = locals.___currentBit;
            p.Call(callee);
            StateImpl.IsCall = true;
          }
          nextBlock = Blocks.B5;
        }
        public void B7(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          nextBlock = Blocks.B6;
        }
        public void B8(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
          p.Return(new ZingSourceContext(0, 1390, 1391), null);
          StateImpl.IsReturn = true;
        }
        public void B9(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          if ((((this.SavedRunnableJoinStatements & 2ul)) != 0ul))
          {
            nextBlock = Blocks.B7;
          }
          else
          {
            nextBlock = Blocks.B8;
          }
        }
        public void B10(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          if ((((this.SavedRunnableJoinStatements & 1ul)) != 0ul))
          {
            nextBlock = Blocks.B11;
          }
          else
          {
            nextBlock = Blocks.B9;
          }
        }
        public void B11(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            locals.___a = ((Pointer) (((Z.Application.___AckChan) application.LookupObject(application.globals.___Sender____recv))).Receive(this.StateImpl, new ZingSourceContext(0, 1145, 1152), null));
          }
          nextBlock = Blocks.B4;
        }
        public void B12(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
          nextBlock = Blocks.B10;
        }
        public void B13(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (!locals.___gotAck)
          {
            nextBlock = Blocks.B12;
          }
          else
          {
            nextBlock = Blocks.B1;
          }
        }
        public void B14(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B13;
        }
        public void B15(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          locals.___gotAck = false;
          nextBlock = Blocks.B14;
        }
        public void B16(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            p.LastFunctionCompleted = null;
          }
          nextBlock = Blocks.B15;
        }
        public void B17(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            Z.Application.___Sender.___TransmitMsg callee = new Z.Application.___Sender.___TransmitMsg(application);
            callee.inputs.priv____body = locals.___body;
            callee.inputs.priv____bit = locals.___currentBit;
            p.Call(callee);
            StateImpl.IsCall = true;
          }
          nextBlock = Blocks.B16;
        }
        public void B18(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          (((Z.Application.___BoolChan) application.LookupObject(application.globals.___Main____reliableChan))).Send(this.StateImpl, locals.___body, new ZingSourceContext(0, 850, 879), null);
          nextBlock = Blocks.B17;
        }
        public void B19(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          locals.___body = ((Boolean) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B18;
        }
        public void B20(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(bool)));
          nextBlock = Blocks.B19;
        }
        public void B21(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (true)
          {
            nextBlock = Blocks.B20;
          }
          else
          {
            nextBlock = Blocks.B0;
          }
        }
        public void Enter(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___currentBit = false;
          nextBlock = Blocks.B21;
        }
      }
    }
    internal class ___Receiver
      : Z.ZingClass
    {
      public ___Receiver(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Receiver()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Receiver(___Receiver c) : base(c)
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
          return ___Receiver.typeId;
        }
      }
      private static readonly short typeId = 9;
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
        ___Receiver newObj = new ___Receiver(this);
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
      internal sealed class ___TransmitAck
        : Z.ZingMethod
      {
        public ___TransmitAck(Application app)
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
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(state.GetCanonicalId(this.priv____a));
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____a);
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
          }
        }
        public override ulong GetRunnableJoinStatements(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return ~((ulong) 0);
            }
            case Blocks.Enter:
            {
              return (((true ? 1ul : 0ul)) | ((true ? 2ul : 0ul)));
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
                return new ZingSourceContext(0, 1760, 1761);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 1670, 1683);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 1640, 1651);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 1610, 1621);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 1610, 1698);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 1760, 1761);
              }
              case Blocks.B6:
              {
                return new ZingSourceContext(0, 1742, 1743);
              }
              case Blocks.B7:
              {
                return new ZingSourceContext(0, 1555, 1753);
              }
              case Blocks.B8:
              {
                return new ZingSourceContext(0, 1713, 1753);
              }
              case Blocks.B9:
              {
                return new ZingSourceContext(0, 1577, 1713);
              }
              case Blocks.B10:
              {
                return new ZingSourceContext(0, 1555, 1753);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 1555, 1753);
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
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 10;
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
            this.priv____bit = src.priv____bit;
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
                return priv____bit;
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
                  priv____bit = ((Boolean) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(this.priv____bit);
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____bit);
          }
          public bool priv____bit;
          public static int id____bit = 0;
          public bool ___bit
          {
            get
            {
              return priv____bit;
            }
            set
            {
              SetDirty();
              priv____bit = value;
            }
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
          ___TransmitAck clone = new ___TransmitAck(((Application) myState));
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
        public ___TransmitAck(Application app, bool ___bit)
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
          inputs.___bit = ___bit;
        }
        public void B0(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          p.Return(new ZingSourceContext(0, 1760, 1761), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___AckChan) application.LookupObject(application.globals.___Receiver____xmit))).Send(this.StateImpl, locals.___a, new ZingSourceContext(0, 1670, 1683), null);
          nextBlock = Blocks.B0;
        }
        public void B2(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          (((Z.Application.___Ack) application.LookupObject(locals.___a))).___bit = inputs.___bit;
          nextBlock = Blocks.B1;
        }
        public void B3(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___a = application.Allocate(new Z.Application.___Ack(application));
          nextBlock = Blocks.B2;
        }
        public void B4(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B0;
        }
        public void B6(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B5;
        }
        public void B7(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
          p.Return(new ZingSourceContext(0, 1760, 1761), null);
          StateImpl.IsReturn = true;
        }
        public void B8(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((((this.SavedRunnableJoinStatements & 2ul)) != 0ul))
          {
            nextBlock = Blocks.B6;
          }
          else
          {
            nextBlock = Blocks.B7;
          }
        }
        public void B9(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((((this.SavedRunnableJoinStatements & 1ul)) != 0ul))
          {
            nextBlock = Blocks.B4;
          }
          else
          {
            nextBlock = Blocks.B8;
          }
        }
        public void B10(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          SavedRunnableJoinStatements = ((ulong) application.GetSelectedChoiceValue(p));
          nextBlock = Blocks.B9;
        }
        public void Enter(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
          if (application.SetPendingSelectChoices(p, SavedRunnableJoinStatements))
          {
            nextBlock = Blocks.B10;
          }
          else
          {
            nextBlock = Blocks.B9;
          }
        }
      }
      internal sealed class ___Run
        : Z.ZingMethod
      {
        public ___Run(Application app)
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
            this.priv____expectedBit = src.priv____expectedBit;
            this.priv____trueBody = src.priv____trueBody;
            this.priv____m = src.priv____m;
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
                return priv____expectedBit;
              }
              case 1:
              {
                return priv____trueBody;
              }
              case 2:
              {
                return priv____m;
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
                  priv____expectedBit = ((Boolean) val);
                  return;
                }
              }
              case 1:
              {
                {
                  priv____trueBody = ((Boolean) val);
                  return;
                }
              }
              case 2:
              {
                {
                  priv____m = ((Z.Pointer) val);
                  return;
                }
              }
            }
          }
          public void WriteString(StateImpl state, BinaryWriter bw)
          {
            bw.Write(this.priv____expectedBit);
            bw.Write(this.priv____trueBody);
            bw.Write(state.GetCanonicalId(this.priv____m));
          }
          public void TraverseFields(FieldTraverser ft)
          {
            ft.DoTraversal(this.priv____expectedBit);
            ft.DoTraversal(this.priv____trueBody);
            ft.DoTraversal(this.priv____m);
          }
          public bool priv____expectedBit;
          public static int id____expectedBit = 0;
          public bool ___expectedBit
          {
            get
            {
              return priv____expectedBit;
            }
            set
            {
              SetDirty();
              priv____expectedBit = value;
            }
          }
          public bool priv____trueBody;
          public static int id____trueBody = 1;
          public bool ___trueBody
          {
            get
            {
              return priv____trueBody;
            }
            set
            {
              SetDirty();
              priv____trueBody = value;
            }
          }
          public Z.Pointer priv____m;
          public static int id____m = 2;
          public Z.Pointer ___m
          {
            get
            {
              return priv____m;
            }
            set
            {
              SetDirty();
              priv____m = value;
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
          }
        }
        public override ulong GetRunnableJoinStatements(Process p)
        {
          switch (nextBlock) {
            default:
            {
              return ~((ulong) 0);
            }
            case Blocks.B8:
            {
              return (((((Z.Application.___BoolChan) application.LookupObject(application.globals.___Main____reliableChan))).CanReceive ? 1ul : 0ul));
            }
            case Blocks.B17:
            {
              return (((((Z.Application.___MsgChan) application.LookupObject(application.globals.___Receiver____recv))).CanReceive ? 1ul : 0ul));
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
            case Blocks.B11:
            {
              return true;
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
                return new ZingSourceContext(0, 2586, 2587);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 2586, 2587);
              }
              case Blocks.B2:
              {
                return new ZingSourceContext(0, 2508, 2534);
              }
              case Blocks.B3:
              {
                return new ZingSourceContext(0, 2437, 2463);
              }
              case Blocks.B4:
              {
                return new ZingSourceContext(0, 2412, 2413);
              }
              case Blocks.B5:
              {
                return new ZingSourceContext(0, 2363, 2414);
              }
              case Blocks.B6:
              {
                return new ZingSourceContext(0, 2372, 2414);
              }
              case Blocks.B7:
              {
                return new ZingSourceContext(0, 2372, 2414);
              }
              case Blocks.B8:
              {
                return new ZingSourceContext(0, 2363, 2414);
              }
              case Blocks.B9:
              {
                return new ZingSourceContext(0, 2170, 2191);
              }
              case Blocks.B10:
              {
                return new ZingSourceContext(0, 2111, 2129);
              }
              case Blocks.B11:
              {
                return new ZingSourceContext(0, 2111, 2129);
              }
              case Blocks.B12:
              {
                return new ZingSourceContext(0, 2586, 2587);
              }
              case Blocks.B13:
              {
                return new ZingSourceContext(0, 1993, 1994);
              }
              case Blocks.B14:
              {
                return new ZingSourceContext(0, 1964, 1995);
              }
              case Blocks.B15:
              {
                return new ZingSourceContext(0, 1973, 1995);
              }
              case Blocks.B16:
              {
                return new ZingSourceContext(0, 1973, 1995);
              }
              case Blocks.B17:
              {
                return new ZingSourceContext(0, 1964, 1995);
              }
              case Blocks.B18:
              {
                return new ZingSourceContext(0, 1943, 1947);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 1807, 1832);
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
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 11;
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
          ___Run clone = new ___Run(((Application) myState));
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
          p.Return(new ZingSourceContext(0, 2586, 2587), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B18;
        }
        public void B2(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          locals.___expectedBit = !locals.___expectedBit;
          nextBlock = Blocks.B1;
        }
        public void B3(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          if (!(locals.___trueBody == (((Z.Application.___Msg) application.LookupObject(locals.___m))).___body))
          {
            this.StateImpl.Exception = new Z.ZingAssertionFailureException(@"trueBody == m.body)");
          }
          nextBlock = Blocks.B2;
        }
        public void B4(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          nextBlock = Blocks.B3;
        }
        public void B5(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
          p.Return(new ZingSourceContext(0, 2586, 2587), null);
          StateImpl.IsReturn = true;
        }
        public void B6(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          if ((((this.SavedRunnableJoinStatements & 1ul)) != 0ul))
          {
            nextBlock = Blocks.B7;
          }
          else
          {
            nextBlock = Blocks.B5;
          }
        }
        public void B7(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            locals.___trueBody = ((Boolean) (((Z.Application.___BoolChan) application.LookupObject(application.globals.___Main____reliableChan))).Receive(this.StateImpl, new ZingSourceContext(0, 2372, 2379), null));
          }
          nextBlock = Blocks.B4;
        }
        public void B8(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
          nextBlock = Blocks.B6;
        }
        public void B9(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          if ((locals.___expectedBit == (((Z.Application.___Msg) application.LookupObject(locals.___m))).___bit))
          {
            nextBlock = Blocks.B8;
          }
          else
          {
            nextBlock = Blocks.B1;
          }
        }
        public void B10(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            p.LastFunctionCompleted = null;
          }
          nextBlock = Blocks.B9;
        }
        public void B11(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            Z.Application.___Receiver.___TransmitAck callee = new Z.Application.___Receiver.___TransmitAck(application);
            callee.inputs.priv____bit = (((Z.Application.___Msg) application.LookupObject(locals.___m))).___bit;
            p.Call(callee);
            StateImpl.IsCall = true;
          }
          nextBlock = Blocks.B10;
        }
        public void B12(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B11;
        }
        public void B13(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B12;
        }
        public void B14(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
          p.Return(new ZingSourceContext(0, 2586, 2587), null);
          StateImpl.IsReturn = true;
        }
        public void B15(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if ((((this.SavedRunnableJoinStatements & 1ul)) != 0ul))
          {
            nextBlock = Blocks.B16;
          }
          else
          {
            nextBlock = Blocks.B14;
          }
        }
        public void B16(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          {
            locals.___m = ((Pointer) (((Z.Application.___MsgChan) application.LookupObject(application.globals.___Receiver____recv))).Receive(this.StateImpl, new ZingSourceContext(0, 1973, 1980), null));
          }
          nextBlock = Blocks.B13;
        }
        public void B17(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
          nextBlock = Blocks.B15;
        }
        public void B18(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          if (true)
          {
            nextBlock = Blocks.B17;
          }
          else
          {
            nextBlock = Blocks.B0;
          }
        }
        public void Enter(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          locals.___expectedBit = false;
          nextBlock = Blocks.B18;
        }
      }
    }
    internal class ___Main
      : Z.ZingClass
    {
      public ___Main(Application application) : base(application)
      {
        
        {
          ;
        }
        {
        }
      }
      public ___Main()
      {
        
        {
          ;
        }
        {
        }
      }
      private ___Main(___Main c) : base(c)
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
          return ___Main.typeId;
        }
      }
      private static readonly short typeId = 12;
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
        ___Main newObj = new ___Main(this);
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
      internal sealed class ___Run
        : Z.ZingMethod
      {
        public ___Run(Application app)
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
        public enum Blocks : ushort
        {
          None = 0,
          Enter = 1,
          B0 = 2,
          B1 = 3,
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
            case Blocks.Enter:
            {
              return true;
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
                return new ZingSourceContext(0, 3013, 3014);
              }
              case Blocks.B1:
              {
                return new ZingSourceContext(0, 3013, 3014);
              }
              case Blocks.Enter:
              {
                return new ZingSourceContext(0, 2760, 2787);
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
        public override int CompareTo(object obj)
        {
          return 0;
        }
        private static readonly short typeId = 13;
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
          ___Run clone = new ___Run(((Application) myState));
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
          p.Return(new ZingSourceContext(0, 3013, 3014), null);
          StateImpl.IsReturn = true;
        }
        public void B1(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 0);
          nextBlock = Blocks.B0;
        }
        public void Enter(Z.Process p)
        {
          p.AtomicityLevel = (this.SavedAtomicityLevel + 1);
          {
            application.globals.___Main____reliableChan = application.Allocate(new Z.Application.___BoolChan(application));
            application.globals.___Sender____xmit = application.globals.___Receiver____recv = application.Allocate(new Z.Application.___MsgChan(application));
            application.globals.___Sender____recv = application.globals.___Receiver____xmit = application.Allocate(new Z.Application.___AckChan(application));
            {
              Z.Application.___Sender.___Run callee = new Z.Application.___Sender.___Run(application);
              application.CreateProcess(application, callee, @"___Sender.___Run", new ZingSourceContext(0, 2948, 2960), null);
            }
            {
              Z.Application.___Receiver.___Run callee = new Z.Application.___Receiver.___Run(application);
              application.CreateProcess(application, callee, @"___Receiver.___Run", new ZingSourceContext(0, 2981, 2995), null);
            }
          }
          nextBlock = Blocks.B1;
        }
      }
    }
    public static object[] GetChoicesForType(System.Type type)
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
