using System;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Microsoft.Zing")]
[assembly: AssemblyDescription("Compiler for the Zing language")]
[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: ClassInterface(ClassInterfaceType.AutoDual)]
[assembly: ReliabilityContractAttribute(
   Consistency.MayCorruptInstance, Cer.None)]