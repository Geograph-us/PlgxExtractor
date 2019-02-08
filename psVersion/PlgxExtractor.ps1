using namespace System.Text
using namespace System.Linq
using namespace System.IO
using namespace System.IO.Compression

function Extract($filename)
{
  $filesBeginPattern = [BitConverter]::GetBytes(0x0004000000000003)
  $dirname = $filename.BaseName
  Write-Host -NoNewline "[$($filename.Name)] "

  $fs = [File]::Open($filename, [FileMode]::Open, [FileAccess]::Read, [FileShare]::Read)      
  $br = New-Object BinaryReader($fs)
    
  if ($br.ReadInt32() -ne 0x65d90719)
  {
    Write-Host "ERROR: not PLGX-file"
    $global:LastError += "ERROR: not PLGX-file " + $filename + "`r`n"
    return
  }
  # Plugin-name
  $fs.Position = 0x24
  $size = $br.ReadInt32()
  $value = $br.ReadBytes($size)
  $plugName = $enc.GetString($value)

  # Plugin creation date
  $fs.Position += 2
  $size = $br.ReadInt32()
  $value = $br.ReadBytes($size)
  $date = [DateTime]::Parse($enc.GetString($value))

  # Plugin creation tool
  $fs.Position += 2
  $size = $br.ReadInt32()
  $value = $br.ReadBytes($size)
  $creationToolName = $enc.GetString($value)
  Write-Host "[$($plugName)] [$($date)] [$($creationToolName)]"

  # Go to files list
  $bytesCount = 500
  $filesBegin = Search $br.ReadBytes($bytesCount) $filesBeginPattern

  if ($filesBegin -eq -1)
  {
    Write-Host "ERROR: files list not found"
    $global:LastError += "ERROR: files list not found " + $filename + "`r`n"
    return
  }

  $fs.Position = $fs.Position - $bytesCount + $filesBegin + 14

  $isOk = $true
  while ($true)
  {
    $size = $br.ReadInt32()
    if ($size -lt 1)
    {
      break
    }
    $value = $br.ReadBytes($size)

    # filename
    $name = $enc.GetString($value)
    $fs.Position += 2

    # gzipped file size
    $size = $br.ReadInt32()      
    $buffer = GzipDecompress $br.ReadBytes($size)
    Write-Host "$($name). Compressed Size: $($size). Size: $($buffer.Length)"

    # fix relative path
    $name = $name.Replace("../", "").Replace("..\", "")
    $path = [Path]::Combine($dirname, $name)
    $folder = [Path]::GetDirectoryName($path)
    
    if (!(Test-Path -Path $folder))
    {
      $res = New-Item -ItemType directory -Path $folder
    }
    
    if ($buffer.Length -gt 0 -or $size -eq 0)
    {
      [File]::WriteAllBytes($path, $buffer)
    }
    else
    {
      Write-Host "Can't Extract File: $($name)"
      $isOk = $false
    }

    $fs.Position += 14
  }
  $br.Close()
  $fs.Close()      

  if (!$isOk)
  {
    $global:LastError += "ERROR: not all files was extracted " + $filename + "`r`n"
  }
  
  Write-Host "Extracted!"
  Write-Host ""
}

function Search($sIn, $sFor)
{	
  $numArray = New-Object int[] 256
  $num1 = 0
  $num2 = $sFor.Length - 1
  for ($index = 0; $index -lt 256; ++$index)
  {
    $numArray[$index] = $sFor.Length
  }
  for ($index = 0; $index -lt $num2; ++$index)
  {
    $numArray[$sFor[$index]] = $num2 - $index
  }
  while ($num1 -le $sIn.Length - $sFor.Length)
  {
    for ($index = $num2; $sIn[$num1 + $index] -eq $sFor[$index]; --$index)
    {
      if ($index -eq 0)
      {
        return $num1
      }
    }
    $num1 += $numArray[$sIn[$num1 + $num2]]
  }
  return -1
}

function GzipDecompress($data)
{
  if ($data.Length -lt 1)
  {
    return New-Object byte[] 0
  }
  $decompressedStream = New-Object MemoryStream
  $compressStream = New-Object MemoryStream(,$data)
  $deflateStream = New-Object GzipStream $compressStream, ([CompressionMode]::Decompress)
  $deflateStream.CopyTo($decompressedStream)
  $deflateStream.Close()
  $compressStream.Close()
  $decompressedArray = $decompressedStream.ToArray()
  return $decompressedArray
}

$global:LastError = "`r`n"
$enc = [Encoding]::Default

$files = Get-ChildItem *.plgx
  
Write-Host "Select PLGX-files to extract (comma separated):"
Write-Host "0 - All"
for ($i = 0; $i -lt $files.Count; $i++)
{
  Write-Host ($i + 1).ToString() "-" $files[$i].Name
}
$choice = Read-Host
Write-Host ""

[Array]$choices = $choice -split ',' | % { iex $_ }

if (!$choices.Contains(0))
{
  $files = [Enumerable]::Where($files, [Func[object,int,bool]]{ param($x,$i) $choices.Contains($i + 1) })
}

for ($i = 0; $i -lt $files.Count; $i++)
{
  Extract $files[$i]
}

Write-Host -NoNewline $global:LastError

Write-Host -NoNewline "Press Enter to exit..."
Read-Host
