param(
    [string]$Xlsx = "C:\Users\user\Desktop\sallimni Super market\tools\catalog.xlsx",
    [string]$Out  = "C:\Users\user\Desktop\sallimni Super market\src\Sallimni.Api\Seeding\stores-seed.tsv"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$tmp = [System.IO.Path]::GetTempFileName()
$zip = [System.IO.Compression.ZipFile]::OpenRead($Xlsx)
try {
    $entry = $zip.Entries | Where-Object { $_.FullName -eq "xl/worksheets/sheet1.xml" }
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $tmp, $true)
} finally { $zip.Dispose() }

$settings = New-Object System.Xml.XmlReaderSettings
$settings.IgnoreWhitespace = $true
$reader = [System.Xml.XmlReader]::Create($tmp, $settings)

$sw = New-Object System.IO.StreamWriter($Out, $false, (New-Object System.Text.UTF8Encoding($false)))
$sw.WriteLine("Name`tBranchId`tRating`tMinOrder`tDeliveryTime`tDeliveryFee`tCategory")

$cells = @{}
$curRef = $null
$rowNum = 0
$written = 0

function Clean([string]$s) {
    if ($null -eq $s) { return "" }
    return ($s -replace "[\t\r\n]", " ").Trim()
}

while ($reader.Read()) {
    if ($reader.NodeType -eq [System.Xml.XmlNodeType]::Element) {
        switch ($reader.LocalName) {
            "row" { $cells = @{}; $rowNum = [int]$reader.GetAttribute("r") }
            "c"   { $curRef = ($reader.GetAttribute("r") -replace '[0-9]', '') }
            "v"   { $v = $reader.ReadElementContentAsString(); if ($curRef) { $cells[$curRef] = $v } }
            "t"   { $v = $reader.ReadElementContentAsString(); if ($curRef) { $cells[$curRef] = $v } }
        }
    }
    elseif ($reader.NodeType -eq [System.Xml.XmlNodeType]::EndElement -and $reader.LocalName -eq "row") {
        if ($rowNum -gt 1) {
            $name = Clean $cells["A"]
            if ($name -ne "") {
                $line = (Clean $cells["A"]) + "`t" + (Clean $cells["B"]) + "`t" + (Clean $cells["C"]) + "`t" +
                        (Clean $cells["D"]) + "`t" + (Clean $cells["E"]) + "`t" + (Clean $cells["F"]) + "`t" + (Clean $cells["G"])
                $sw.WriteLine($line)
                $written++
            }
        }
    }
}
$reader.Dispose()
$sw.Dispose()
Remove-Item $tmp -Force
Write-Output "Wrote $written store rows to $Out"
