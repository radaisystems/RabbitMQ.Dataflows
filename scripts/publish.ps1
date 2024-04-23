$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputBasePath = Join-Path $scriptDirectory "../PublishedApp"

$projectPaths = @(
    "src\RadAI.Compression\RadAI.Compression.csproj",
    "src\RadAI.Data\RadAI.Data.csproj",
    "src\RadAI.Dataflows\RadAI.Dataflows.csproj",
    "src\RadAI.Encryption\RadAI.Encryption.csproj",
    "src\RadAI.Hashing\RadAI.Hashing.csproj",
    "src\RadAI.Metrics\RadAI.Metrics.csproj",
    "src\RadAI.RabbitMQ\RadAI.RabbitMQ.csproj",
    "src\RadAI.Serialization\RadAI.Serialization.csproj",
    "src\RadAI.Utilities\RadAI.Utilities.csproj"
)


if ((Test-Path $outputBasePath)) {
    Remove-Item -Recurse -Force $outputBasePath
}


New-Item -ItemType Directory -Path $outputBasePath | Out-Null


foreach ($relativeProjectPath in $projectPaths) {
    $projectPath = Join-Path $scriptDirectory "../$relativeProjectPath"
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($relativeProjectPath)
    $outputPath = Join-Path $outputBasePath $projectName

    Write-Host "Building and packing $projectName..."
    
    New-Item -ItemType Directory -Path $outputPath | Out-Null
    
    dotnet build $projectPath --configuration Release --output $outputPath
    
    Write-Host "Done packing $projectName!"
}

Write-Host "All projects packed!"