using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Zing
{
    public interface IZingPlugin
    {
        /// <summary>
        /// Descriptions : 
        /// Zing plugin invoked from Zing models to Log specific info
        /// </summary>
        /// <param name="Params"></param>
        void Invoke(params object[] Params);

        /// <summary>
        /// Zing plugin function end is called once zinger has finished exploration and should be
        /// used to print statistics corresponding to the logs
        /// </summary>
        void End();

    }
}
