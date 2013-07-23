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
		..\..\zc\bin\Debug\zc.exe %1.zing
	)
	for /f %%n in (param) do call :CHECKFILE %1 %%n
	cd ..
	goto :EOF

:CHECKFILE
	set arg=%2
	set opt=%arg::=%
	if "%opt%" == "" ( 
		cmd /c "..\..\Zinger\bin\Debug\Zinger.exe %1.dll >golden.txt"
	) 
	goto :EOF
