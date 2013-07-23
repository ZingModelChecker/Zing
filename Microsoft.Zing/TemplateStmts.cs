namespace Microsoft.Zing
{
    public class StatementTemplates
    {        
        void InitializeGlobal()
        {
            this.globals._FieldName = _FieldInitializer;
        }

        void CloneField()
        {
            newObj._FieldName = this._FieldName;
        }

        void CopyField()
        {
            this._FieldName = src._FieldName;
        }

        void InitComplexInstanceField()
        {
            this._FieldName = _expr;
        }

        void ChoiceHelperBody()
        {
            if (type == typeof(Application._ChoiceType))
                return Application._ChoiceTypeChoices;
        }

        void ChoiceHelperEnd()
        {
            if (type == typeof(bool))
                return Application.boolChoices;
            else
                throw new ArgumentException("Invalid type for choice operator : " + type);
        }

        void ConditionalWriter()
        {
            if (_Name != null)
                bw.Write(_Name);
        }
        
        void SimpleWriter()
        {
            bw.Write(_Name);
        }

        void EnumWriter()
        {
            bw.Write((byte) _Name);
        }

        void StructWriter()
        {
            _Name.WriteString(state, bw);
        }

        void ComplexWriter()
        {
            bw.Write(state.GetCanonicalId(_Name));
        }

		void FieldTraverser()
		{
			ft.DoTraversal(_Name);	
		}
		
        void DispatchSwitch()
        {
            // all we want from this is the case...
            switch (foo)
            {
                case Blocks._BlockName:
                    _BlockName(p); break;
            }
        }

        void RunnableSwitchSelect()
        {
            // all we want from this is the case...
            switch (foo)
            {
                case Blocks._BlockName:
                    return _expr;
            }
        }

        void RelatedSwitchSelect()
        {
            // all we want from this is the case...
            switch (foo)
            {
                case Blocks._BlockName:
                    goto case Blocks._TargetName;
            }
        }

        void ValidEndStateSwitch()
        {
            // all we want from this is the case...
            switch (foo)
            {
                case Blocks._BlockName:
                    return true;
            }
        }

        void ContextAttributeSwitch()
        {
            // all we want from this is the case...
            switch (foo)
            {
                case Blocks._BlockName:
                    return new Z.Attributes._AttributeName();
            }
        }

        void ReturnBlockTransfer()
        {
            p.Return(_context, _contextAttr);
        }

        void SetReturnValue()
        {
            outputs._ReturnValue = _rval;
        }

        void CreateCallFrame()
        {
            Z.Application._calleeClass._Callee callee = new Z.Application._calleeClass._Callee(application);
        }

        void CreateCallFrameForInterface()
        {
            Z.Application._calleeClass._Callee callee = ((Z.Application._calleeClass.CreateMethods)application.LookupObject(_thisExpr))._CreateMethod(application);
        }

        void InvokeMethod()
        {
            p.Call(callee);
        }

        void SetIsCall()
        {
            StateImpl.IsCall = true;
        }

        void InvokeAsyncMethod()
        {
            application.CreateProcess(application, callee, _methodName, _context, _contextAttr);
#if REDUCE_LOCAL_ACCESS
            application.UpdateMover(true, false); 
#endif
        }

        void SetInputParameter()
        {
            callee.inputs._paramName = _src;
        }

        void SetThis()
        {
            callee.This = _thisExpr;
        }

        void FetchOutputParameter()
        {
            _dest = ((Z.Application._CalleeClass._Callee) p.LastFunctionCompleted).outputs._paramName;
        }

        void FetchReturnValue()
        {
            _dest = ((Z.Application._CalleeClass._Callee) p.LastFunctionCompleted).outputs._Lfc_ReturnValue;
        }

        void NativeZOMCallWithAssignment()
        {
            _Dest = _Source;
        }

        void InvalidateLastFunctionCompleted()
        {
            p.LastFunctionCompleted = null;
        }

        void AssertWithComment()
        {
            if (!_expr)
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(_exprString, _comment);
        }

        void AssertWithoutComment()
        {
            if (!_expr)
                this.StateImpl.Exception = new Z.ZingAssertionFailureException(_exprString);
        }

        void Accept ()
        {
            this.StateImpl.IsAcceptingState = _expr;
        }

        void Event()
        {
            this.StateImpl.AddExternalEvent(new ExternalEvent(_chanExpr, _msgExpr, _dirExpr), _context, _contextAttr);
        }

        void TauEvent()
        {
            this.StateImpl.AddExternalEvent(new ExternalEvent(true), _context, _contextAttr);
        }

        void Assume()
        {
            if (!_expr)
                this.StateImpl.Exception = new Z.ZingAssumeFailureException(_exprString);
        }

        void Send()
        {
            ((Z.Application._chanType) application.LookupObject(_chanExpr)).Send(this.StateImpl, _msgExpr, _context, _contextAttr);
        }

        void Yield()
        {
            
        }

        void ReceivePattern()
        {
            _target = (_targetType) ((Z.Application._chanType) application.LookupObject(_chanExpr)).Receive(this.StateImpl, _context, _contextAttr);
        }

        void StartChooseByType()
        {
            application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(_typeExpr)));
        }

        void StartChooseByBoolType()
        {
            application.SetPendingChoices(p, Microsoft.Zing.Application.GetChoicesForType(typeof(bool)));
        }

        void StartChooseByValue()
        {
            application.SetPendingChoices(p, ((ZingCollectionType) application.LookupObject(_ptrExpr)).GetChoices());
        }

        void FinishChoose()
        {
            _target = (_targetType) application.GetSelectedChoiceValue(p);
        }

        void InvalidBlockingSelect()
        {
            this.StateImpl.Exception = new Z.ZingInvalidBlockingSelectException();
        }

        void PropagateException()
        {
            p.RaiseZingException(this.CurrentException);
        }

        void SetException()
        {
            CurrentException = (int) Exceptions._exception;
        }

        void SetHandler()
        {
            handlerBlock = Blocks._blockName;
        }

        void Trace()
        {
            application.Trace(_context, _contextAttr /*, _arguments*/);
        }

        void InvokePlugin()
        {
            application.InvokePlugin(/*_arguments*/);
        }

        void InvokeSched ()
        {
            application.InvokeScheduler(/*_arguments*/);
        }

        void SelectStatementProlog()
        {
            this.SavedRunnableJoinStatements = this.GetRunnableJoinStatements(p);
        }

        void NDSelectBlock()
        {
            SavedRunnableJoinStatements = (ulong) application.GetSelectedChoiceValue(p);
        }

        void SetAddItem()
        {
            ((ZingSet) application.LookupObject(_ptrExpr)).Add(_itemExpr);
        }

        void SetRemoveItem()
        {
            ((ZingSet) application.LookupObject(_ptrExpr)).Remove(_itemExpr);
        }

        void SetAddSet()
        {
            ((ZingSet) application.LookupObject(_ptrExpr)).AddSet((ZingSet) application.LookupObject(_itemExpr));
        }

        void SetRemoveSet()
        {
            ((ZingSet) application.LookupObject(_ptrExpr)).RemoveSet((ZingSet) application.LookupObject(_itemExpr));
        }

        void foreachIncrementer()
        {
            locals._iterator = locals._iterator + 1;
        }

        void foreachDeref()
        {
            _tmpVar = ((Z.Application._collectionType) application.LookupObject(_collectionExpr))[locals._iterator];
        }

        void foreachInit()
        {
            locals._iterator = 0;
        }

        void ReturnStmt()
        {
            return;
        }

		void SetFieldInfoSwitch()
		{
			switch (fi)
			{
				default:
				{
					Debug.Assert(false);
					return;
				}
			}
		}

		void GetFieldInfoSwitch()
		{
			switch (fi)
			{
				default:
				{
					Debug.Assert(false);
					return null;
				}
			}
		}

		void SetFieldInfoCase()
		{
			// all we want from this is the case...
			switch (foo)
			{
				case _fieldId: 
				{
					_fieldName = (_fieldType) val;
					return;
				}
			}
		}

		void GetFieldInfoCase()
		{
			// all we want from this is the case...
			switch (foo)
			{
				case _fieldId: 
					return _fieldName;
			}
		}
    }
}
