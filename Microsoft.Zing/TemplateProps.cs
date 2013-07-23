namespace Microsoft.Zing
{
    public class PropertyTemplates
    {
        public int globalAccessor 
        {
            get 
            {
                return _fieldName; 
            }
            set 
            { 
                SetDirty();
                _fieldName = value; 
            }
        }

        public int localAccessor 
        {
            get 
            {
                return _fieldName;
            }
            set 
            {
                SetDirty();
                _fieldName = value;
            }
        }

		public int inputAccessor 
		{
			get 
			{
				return _fieldName;
			}
			set 
			{
				SetDirty();
				_fieldName = value;
			}
		}

		public int outputAccessor 
		{
			get 
			{
				return _fieldName;
			}
			set 
			{
				SetDirty();
				_fieldName = value;
			}
		}

       public int lastFunctionOutputAccessor 
        {
            get 
            {
                  return _fieldName;
            }
        }

        public int structAccessor 
        {
            get 
            {
                return _fieldName;
            }
            set 
            {
                SetDirty();
                _fieldName = value;
            }
        }

        public override int thisAccessor 
        {
            get 
            {
                return _fieldName;
            }
            set 
            {
                _fieldName = value;
            }
        }

		public int ptrFieldAccessor 
		{
			get 
			{
				return _fieldName;
			}
			set 
			{
				SetDirty();
				_fieldName = value;
			}
		}

        public int fieldAccessor 
        {
            get 
            {
                return _fieldName;
            }
            set 
            {
                SetDirty();
                _fieldName = value;
            }
        }
    }
}
