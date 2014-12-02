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
		for /f "delims=;" %%n in (compile_param) do ..\..\zc\bin\Debug\zc.exe %%n %1.zing 
	) else (
		@echo no compile_param declared. 
	)
	set /A counter = 1
	for /f "delims=;" %%n in (param) do call :CHECKFILE %1 "%%n"
	cd ..
	goto :EOF

:CHECKFILE
	set "arg=%~2"
	@echo golden for %arg%
	cmd /c "..\..\Zinger\bin\Debug\Zinger.exe %1.dll %arg% >golden_%counter%.txt"
	set /A counter = counter + 1
	goto :EOF
