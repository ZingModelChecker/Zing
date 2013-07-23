using System;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Microsoft.Zing
{
    /// <exclude/>
    internal class Utils
    {
        private Utils() { }

        internal static string Unmangle(string type)
        {
            if (type.StartsWith("___"))
                type = type.Substring(3);

            return type.Replace("+___", "+").Replace(".___", ".");
        }

        internal static string Unmangle(Type type)
        {
            return Unmangle(type.ToString());
        }

        internal static string Externalize(string type)
        {
            const string prefix = "Microsoft.Zing.Application+";
            string typeName = Unmangle(type);
            if (typeName.StartsWith(prefix))
                typeName = typeName.Substring(prefix.Length).Replace('+', '.');

            return typeName;
        }

        internal static string Externalize(Type type)
        {
            return Externalize(type.ToString());
        }

        internal static bool FingerprintInPreCommit = true;

        private static object[] emptyArgs = new object[] {};

        // <summary>
        // Locate field 'containerName' in object 'obj' and return its value as 'containerObj'.
        // Then, find file 'memberName' in that and return its FieldInfo.
        // </summary>
        // <param name="obj"></param>
        // <param name="containerName"></param>
        // <param name="memberName"></param>
        // <param name="containerObj">The value of "obj.containerName".</param>
        // <param name="fi">The field info for "obj.containerName.memberName".</param>
        private static void GetContainerAndFieldInfo(
            object obj,
            string containerName,
            string memberName,
            out object containerObj,
            out FieldInfo fi)
        {
            containerObj = null;
            fi = null;

            System.Type objType = obj.GetType();

            try
            {
                containerObj = objType.InvokeMember(containerName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, obj, emptyArgs, CultureInfo.CurrentUICulture);
            }
            catch (System.MissingFieldException)
            {
                throw new ArgumentException("Internal error: container field missing");
            }

            System.Type containerType = containerObj.GetType();
            fi = containerType.GetField("priv_" + memberName, BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
        }

        private static void GetContainerAndFieldInfoForReturnValue (
            object obj,
            string containerName,
            string memberName,
            out object containerObj,
            out FieldInfo fi)
        {
            containerObj = null;
            fi = null;

            System.Type objType = obj.GetType();

            try
            {
                containerObj = objType.InvokeMember(containerName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                    null, obj, emptyArgs, CultureInfo.CurrentUICulture);
            }
            catch (System.MissingFieldException)
            {
                throw new ArgumentException("Internal error: container field missing");
            }

            System.Type containerType = containerObj.GetType();
            fi = containerType.GetField("priv_" + memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        internal static bool ContainsVariable(object obj, string containerName, string memberName)
        {
            object containerObj;
            FieldInfo fi;

            GetContainerAndFieldInfo(obj, containerName, "___" + memberName, out containerObj, out fi);

            return fi != null;
        }


        internal static object LookupValueByName(object obj, string containerName, string memberName)
        {
            object containerObj;
            FieldInfo fi;
            if(memberName == "ReturnValue")
            {
                GetContainerAndFieldInfoForReturnValue(obj, containerName, memberName, out containerObj, out fi);
            }
            else
            {
                GetContainerAndFieldInfo(obj, containerName, "___" + memberName, out containerObj, out fi);
            }
            

            if (fi == null)
                return null;

            return containerObj.GetType().InvokeMember(
                fi.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField,
                null, containerObj, emptyArgs, CultureInfo.CurrentUICulture);
        }
    }
}
