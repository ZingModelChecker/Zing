@echo off
@if not "%ECHO%" == "" echo %ECHO%

if {%1} == {} (	
	for /d %%d in (*) do call :CHECKDIR %%d
) else (
	call :CHECKDIR %1
)
goto :EOF

:CHECKDIR
	echo %1
	cd %1
	if exist compile_param (
		for /f "delims=" %%n in (compile_param) do ..\..\zc\bin\Debug\zc.exe %1.zing %%n
	) else (
		..\..\zc\bin\Debug\zc.exe -preemptive %1.zing
	)
	for /f %%n in (param) do call :CHECKFILE %1 %%n
	call :COMPARE_OUTPUT
	@echo --------------------------------------------------------------
	cd ..
	goto :EOF

:CHECKFILE
	set arg=%2
	set opt=%arg::=%
	if "%opt%" == "" ( 
		cmd /c "..\..\Zinger\bin\Debug\Zinger.exe %1.dll >output.txt"
		sleep 1
		cmd /c "grep -v State: golden.txt >stripped_golden.txt"
		cmd /c "grep -v State: output.txt >stripped_output.txt"
		cmd /c "diff stripped_golden.txt stripped_output.txt >diff.txt" 
		fc stripped_golden.txt stripped_output.txt >regression_tmp
	) 

	if %ERRORLEVEL% == 1 ( 
		@echo %1 failed with options %opt% 
	) else ( 
		@echo %1 passed with options %opt%
	)
	del regression_tmp
	goto :EOF

:COMPARE_OUTPUT
	set flag=0
	set status=0
	set biterror=0
	for /f %%n in (param) do call :FIND_STATUS %%n
	if %biterror% == 1 (
		@echo At least one option reports a status different from others
	) else (
		@echo All options report the same status
	)
	goto :EOF

:FIND_STATUS
	set arg=%1
	set opt=%arg::=%
	if "%arg%" == ":" (
		grep "Check passed" output.txt >regression_tmp
	) else (
		grep "Check passed" output_%opt%.txt >regression_tmp
	)
	if %flag% == 0 (
		set flag=1
		set status=%ERRORLEVEL%
	) else (
		if %status% == %ERRORLEVEL% ( 
			set biterror=0
		) else (
			set biterror=1
		)
	)
	del regression_tmp
	goto :EOF
