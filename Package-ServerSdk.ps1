param(
	[Parameter(Position=0,Mandatory=$false)][String]$BuildConfig="Release",
	[Parameter(Position=1,Mandatory=$false)][String]$BUILD_NUMBER="5.0.0",
	[Parameter(Position=2,Mandatory=$false)][Boolean]$CleanFirst=$false,
	[String]$BuildVerbosity="normal", #normal,quiet,verbose
	[String]$BuildVersion="15.0"
)

[String]$BuildExecutable = 
	$(If ($BuildVersion -eq "12.0" -OR $BuildVersion -eq "14.0") { "${env:ProgramFiles(x86)}\MSBuild\$BuildVersion\Bin\MSBuild.exe"; } `
		Else { "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"; });
[String]$BuildProperties = "/property:Configuration=$BuildConfig;BUILD_NUMBER=$BUILD_NUMBER";

[String]$InvocationPath = Split-Path $MyInvocation.MyCommand.Path;

& $BuildExecutable $("$InvocationPath\ServerSdk.proj") $("/verbosity:$BuildVerbosity") #$BuildProperties
