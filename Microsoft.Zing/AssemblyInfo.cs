using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using System.Security;
using System.Security.Permissions;

[assembly: AssemblyTitle("Microsoft.Zing")]
[assembly: AssemblyDescription("Compiler for the Zing language")]
[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: ClassInterface(ClassInterfaceType.AutoDual)]

[assembly: ReliabilityContractAttribute(
   Consistency.MayCorruptInstance, Cer.None)]
