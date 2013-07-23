using System;
using System.Compiler;
using System.Diagnostics;
using System.IO;


namespace Microsoft.Zing
{
    internal sealed class RuntimeAssemblyLocation
    {
        internal static string Location = null; //Can be set by compiler in cross compilation scenarios
    }
    public sealed class TargetPlatform
    {
        public static void SetToV1()
        {
            TargetPlatform.SetToV1(null);
        }
        public static void SetToV1(string platformAssembliesLocation)
        {
            System.Compiler.TargetPlatform.SetToV1(platformAssembliesLocation);
            RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "microsoft.zing.runtime.dll");
        }
        public static void SetToV1_1()
        {
            TargetPlatform.SetToV1_1(null);
        }
        public static void SetToV1_1(string platformAssembliesLocation)
        {
            System.Compiler.TargetPlatform.SetToV1_1(platformAssembliesLocation);
            RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "microsoft.zing.runtime.dll");
        }
        /// <summary>
        /// Use this to set the target platform to a platform with a superset of the platform assemblies in version 1.1, but
        /// where the public key tokens and versions numbers are determined by reading in the actual assemblies from
        /// the supplied location. Only assemblies recognized as platform assemblies in version 1.1 will be unified.
        /// </summary>
        public static void SetToPostV1_1(string platformAssembliesLocation)
        {
        }
    }
}

