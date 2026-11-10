$ErrorActionPreference = 'Stop'

$publishDir = (Resolve-Path '.\publish').Path
$zipPath = 'C:\Users\marco\source\repos\alchemistC137\MemByte-IM\publish.zip'

if (Test-Path $zipPath) {
	Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)

Get-ChildItem -Path $publishDir -Recurse -File | ForEach-Object {
	$fullPath = $_.FullName
	$relativePath = $fullPath.Substring($publishDir.Length + 1)
	$relativePath = $relativePath.Replace([System.IO.Path]::DirectorySeparatorChar, '/')
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $fullPath, $relativePath, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}

$zip.Dispose()

$zip2 = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$zip2.Entries | Where-Object { $_.FullName -match 'ReconnectModal' } | Select-Object FullName, Length
$zip2.Dispose()

Write-Output "Zip creado correctamente"
