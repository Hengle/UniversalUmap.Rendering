@echo off
setlocal

:: Define the path to NVIDIA Texture Tools folder (make sure this path is correct)
set NVTOOLSPATH=C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools

:: Define the input directory
set INPUT_DIR=%cd%

:: Loop through all .dds files in the input directory
for %%F in (%INPUT_DIR%\*.dds) do (
    echo Converting %%F to HDR...
    "%NVTOOLSPATH%\nvtt_export.exe" %%F --preset-file %INPUT_DIR%\config.dpf  --output "%INPUT_DIR%\%%~nF.hdr"
)

echo Conversion completed.
pause
endlocal
