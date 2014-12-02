@echo off
@if not "%ECHO%" == "" echo %ECHO%

for /d %%d in (*) do cp param %%d/param

::for /d %%d in (*) do del %%d\golden.txt
goto :EOF


