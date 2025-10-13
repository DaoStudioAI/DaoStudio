Get-ChildItem -Recurse -Include *.cs, *.axaml -Exclude *.g.cs | 
ForEach-Object { 
    $lineCount = (Get-Content $_.FullName).Count
    [PSCustomObject]@{ 
        FileName  = $_.Name
        LineCount = $lineCount
    }
} | 
Tee-Object -Variable fileData |
Format-Table -Property FileName, LineCount

$sum = ($fileData | Measure-Object -Property LineCount -Sum).Sum
Write-Host "Total Line Count: $sum"