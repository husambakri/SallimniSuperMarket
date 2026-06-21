param(
    [string]$Xlsx = "C:\Users\user\Desktop\sallimni Super market\tools\catalog.xlsx",
    [string]$Out  = "C:\Users\user\Desktop\sallimni Super market\src\Sallimni.Api\Seeding\catalog-seed.tsv"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Extract products sheet (sheet2.xml) from the xlsx archive to a temp file
$tmp = [System.IO.Path]::GetTempFileName()
$zip = [System.IO.Compression.ZipFile]::OpenRead($Xlsx)
try {
    $entry = $zip.Entries | Where-Object { $_.FullName -eq "xl/worksheets/sheet2.xml" }
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $tmp, $true)
} finally { $zip.Dispose() }

# Streaming read, row by row
$settings = New-Object System.Xml.XmlReaderSettings
$settings.IgnoreWhitespace = $true
$reader = [System.Xml.XmlReader]::Create($tmp, $settings)

$sw = New-Object System.IO.StreamWriter($Out, $false, (New-Object System.Text.UTF8Encoding($false)))
$sw.WriteLine("Store`tCategory`tSub`tName`tPrice`tImageUrl`tSku`tNameAr`tDescription")

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
            "row" {
                $cells = @{}
                $rowNum = [int]$reader.GetAttribute("r")
            }
            "c" {
                $r = $reader.GetAttribute("r")
                $curRef = ($r -replace '[0-9]', '')
            }
            "v" {
                $val = $reader.ReadElementContentAsString()
                if ($curRef) { $cells[$curRef] = $val }
            }
            "t" {
                $val = $reader.ReadElementContentAsString()
                if ($curRef) { $cells[$curRef] = $val }
            }
        }
    }
    elseif ($reader.NodeType -eq [System.Xml.XmlNodeType]::EndElement -and $reader.LocalName -eq "row") {
        if ($rowNum -gt 1) {
            $store  = Clean $cells["A"]
            $cat    = Clean $cells["B"]
            $sub    = Clean $cells["C"]
            $name   = Clean $cells["D"]
            $price  = Clean $cells["E"]
            $img    = Clean $cells["H"]
            $sku    = Clean $cells["I"]
            $nameAr = Clean $cells["J"]
            $desc   = Clean $cells["K"]
            if ($store -ne "" -and $name -ne "") {
                $sw.WriteLine("$store`t$cat`t$sub`t$name`t$price`t$img`t$sku`t$nameAr`t$desc")
                $written++
            }
        }
    }
}
$reader.Dispose()
$sw.Dispose()
Remove-Item $tmp -Force

Write-Output "Wrote $written rows to $Out"
