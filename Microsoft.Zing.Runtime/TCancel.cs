using System;
using System.ComponentModel;

namespace Microsoft.Zing
{
    /// <summary>
    /// Summary description for TCancel.
    /// </summary>
    [Serializable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TCancel 
    {
        private bool cancel;
        public bool Cancel { get { return cancel; } set { cancel = value; } }
    }

}
