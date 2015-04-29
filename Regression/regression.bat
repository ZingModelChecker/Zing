@echo off
@if not "%ECHO%" == "" echo %ECHO%

if {%1} == {} (	
	for /d %%d in (*) do call :CHECKDIR %%d %2
) else (
	call :CHECKDIR %1 %2
)
goto :EOF

:CHECKDIR
	echo %1
	cd %1
	if exist compile_param (
		for /f "delims=;" %%n in (compile_param) do ..\..\zc\bin\x64\Debug\zc.exe %%n %1.zing 
	) else (
		@echo no compile_param declared. 
	)
	set /A counter = 0
	if exist param (
		for /f "delims=;" %%n in (param) do call :CHECKFILE %1 "%%n" %2
		@echo --------------------------------------------------------------
		cd ..
		goto :EOF
	) else (
		@echo no param file declared.
	)
	
	
	

:CHECKFILE
	set /A counter = counter + 1
	set "arg=%~2"
	if "%3" == "" (
		
		cmd /c "..\..\Zinger\bin\x64\Debug\Zinger.exe %1.dll %arg% >output_%counter%.txt"
		sleep 1
		cmd /c "grep -v State: golden_%counter%.txt >stripped_golden_%counter%.txt"
		cmd /c "grep -v State: output_%counter%.txt >stripped_output_%counter%.txt"
		cmd /c "diff stripped_golden_%counter%.txt stripped_output_%counter%.txt >diff_%counter%.txt" 
		fc stripped_golden_%counter%.txt stripped_output_%counter%.txt >regression_tmp
		 
		if ERRORLEVEL 1 ( 
			@echo %1 failed with options %arg% 
		) else ( 
			@echo %1 passed with options %arg%
		)
		
		del regression_tmp
		goto :EOF
		
	) 
	if "%3" == "%counter%" (
		
		cmd /c "..\..\Zinger\bin\x64\Debug\Zinger.exe %1.dll %arg% >output_%counter%.txt"
		sleep 1
		cmd /c "grep -v State: golden_%counter%.txt >stripped_golden_%counter%.txt"
		cmd /c "grep -v State: output_%counter%.txt >stripped_output_%counter%.txt"
		cmd /c "diff stripped_golden_%counter%.txt stripped_output_%counter%.txt >diff_%counter%.txt" 
		fc stripped_golden_%counter%.txt stripped_output_%counter%.txt >regression_tmp
		 
		if ERRORLEVEL 1 ( 
			@echo %1 failed with options %arg% 
		) else ( 
			@echo %1 passed with options %arg%
		)
		del regression_tmp
		goto :EOF
	
	)
	goto :EOF
	

