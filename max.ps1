param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

dotnet run --project "$PSScriptRoot\Max\Max.csproj" -- $Args
