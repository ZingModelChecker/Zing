using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Zing
{
    public abstract class Via 
    {
		//silent is set to true for vias generated during
		//silent calls and returns in the summarization algorithm
		public bool silent;

		public abstract object Clone();

        public static void PrintTrace(Via[] bts)
        {
            int len = bts.Length;
            int i;

            Console.Write("{0} transitions: ", len);
            for (i = 0; i < len; i++)
                Console.Write(bts[i].ToString());
            Console.WriteLine("");
        }

    }
    
    public sealed class ViaChoose : Via
    {
        public readonly int ChoiceNumber;

		public override object Clone()
		{
			return new ViaChoose(ChoiceNumber, silent);
		}

        public ViaChoose(int n) 
        {
            ChoiceNumber = n;
			silent = false;
        }

		public ViaChoose(int n, bool s) 
		{
			ChoiceNumber = n;
			silent = s;
		}


        public override string ToString()
        {
            return ("->" + ChoiceNumber.ToString(CultureInfo.CurrentUICulture));
        }

        public override bool Equals(object o)
        {
            ViaChoose vc = o as ViaChoose;

            if (vc == null) return false;

            return (ChoiceNumber == vc.ChoiceNumber);
        }

        public override int GetHashCode()
        {
            return ChoiceNumber;
        }
    }

    public sealed class ViaExecute : Via
    {
		int processExecuted;
		public int ProcessExecuted
		{
			get { return processExecuted; }	
		}

		public override object Clone()
		{
			return new ViaExecute(processExecuted,silent);
			
		}

        public ViaExecute(int n)
        {
            processExecuted = n;
			silent = false;
        }

		public ViaExecute(int n, bool s)
		{
			processExecuted = n;
			silent = s;
		}


		public void ChangeProcessExecuted(int n)
		{
			processExecuted = n;
		}

        public override string ToString()
        {
            return "=>" + ProcessExecuted.ToString(CultureInfo.CurrentUICulture);
        }

        public override bool Equals(object o)
        {
            ViaExecute ve = o as ViaExecute;

            if (ve == null) return false;

            return (ProcessExecuted == ve.ProcessExecuted);
        }

        public override int GetHashCode()
        {
            return ProcessExecuted;
        }
    }

}
