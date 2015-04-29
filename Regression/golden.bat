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
	for /f "delims=;" %%n in (param) do call :CHECKFILE %1 "%%n" %2
	cd ..
	goto :EOF

:CHECKFILE
	set /A counter = counter + 1
	set "arg=%~2"
	if "%3" == "" (
		@echo golden for %arg%
		cmd /c "..\..\Zinger\bin\x64\Debug\Zinger.exe %1.dll %arg% >golden_%counter%.txt"
		goto :EOF
	) else if "%3" == "%counter%" (
		@echo golden for %arg%
		cmd /c "..\..\Zinger\bin\x64\Debug\Zinger.exe %1.dll %arg% >golden_%counter%.txt"
		goto :EOF
	) else (
		goto :EOF
	)
	