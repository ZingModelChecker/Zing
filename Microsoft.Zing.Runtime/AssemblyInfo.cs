using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;

//
// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//

[assembly: AssemblyTitle("Microsoft.Zing")]
[assembly: AssemblyDescription("Runtime for the Zing language")]

[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCulture("")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]


[assembly: ReliabilityContractAttribute(
   Consistency.MayCorruptInstance, Cer.None)]
