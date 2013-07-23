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
    // <summary>
    // Set data type (almost persistent)
    // </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MathSet
    {
        public MathSet()
        {
            data = new ArrayList();
        }

        // unsafe bad implementation for temporary use
        public MathSet(ArrayList array)
        {
            if (array == null)
                data = new ArrayList();
            else
                data = array;
        }

        private ArrayList data;
        public ArrayList Data
        {
            get { return data; }
        }

        public int Count
        {
            get { return data.Count; }
        }

        public bool IsEmpty()
        {
            return (data.Count == 0);
        }

        public bool IsNotEmpty()
        {
            return (data.Count > 0);
        }

        public bool Contains(object element)
        {
            return data.Contains(element);
        }

        public static bool Subset(MathSet s1, MathSet s2)
        {
            foreach (object element in s1.data)
            {
                if (s2.Contains(element))
                    continue;
                else
                    return false;
            }
            return true;
        }

        public static bool Subset(ArrayList s1, MathSet s2)
        {
            foreach (object element in s1)
            {
                if (s2.Contains(element))
                    continue;
                else
                    return false;
            }
            return true;
        }

        public static bool Subset(MathSet s1, ArrayList s2)
        {
            foreach (object element in s1.data)
            {
                if (s2.Contains(element))
                    continue;
                else
                    return false;
            }
            return true;
        }

        public void Add(object element)
        {
            data.Add(element);
        }

        public void Add(ArrayList array)
        {
            foreach(object element in array)
            {
                data.Add(element);
            }
        }

        public void Remove(object element)
        {
            data.Remove(element);
        }

        public void Minus(MathSet set)
        {
            foreach (object element in set.data)
            {
                data.Remove(element);
            }
        }

        public void Minus(ArrayList array)
        {
            foreach (object element in array)
            {
                data.Remove(element);
            }
        }

        public void Union(MathSet set)
        {
            foreach (object element in set.data)
            {
                data.Add(element);
            }
        }

        public void Union(ArrayList array)
        {
            foreach (object element in array)
            {
                data.Add(element);
            }
        }

    }
}