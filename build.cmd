@echo off
dotnet publish src\PhotoDrop -c Release -o dist\PhotoDrop --nologo
echo.
echo Built dist\PhotoDrop\PhotoDrop.exe
