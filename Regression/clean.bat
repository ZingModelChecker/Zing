@echo off
@if not "%ECHO%" == "" echo %ECHO%

del *~
for /d %%d in (*) do call :CLEAN %%d
goto :EOF

:CLEAN
	echo %1
	cd %1
	del %1.dll i_* o_* stripped*.txt output*.txt diff*.txt *~
	cd ..
	goto :EOF
