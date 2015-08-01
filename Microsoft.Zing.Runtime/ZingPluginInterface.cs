using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Zing
{
    public abstract class ZingerPluginState 
    {
        public abstract ZingerPluginState Clone();
    }
    public abstract class ZingerPluginInterface
    {
        public abstract void Invoke(int threadID, ZingerPluginState ZPluginState, params object[] Params);

        public abstract void EndPlugin();
    }
}
