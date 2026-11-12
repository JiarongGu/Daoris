# PLACEWORK1g — copy-on-write clones measured (2026-11-11)

Evidence, dated. The design (`docs/2026-11-10-place-work-design.md`, §4.4 `clone` and row 1g) chooses a per-file block clone
(`FSCTL_DUPLICATE_EXTENTS_TO_FILE`) on the place's own ReFS volume, a probe refusing it elsewhere and `auto` then taking `copy`. This
measures that choice on this machine: whether a ReFS or Dev Drive volume exists, what a clone of a large folder costs against a plain
copy in time and in bytes, and what the probe sees on NTFS. No product code exists for `CloneIsolation` yet; this changes none.

## The machine

Windows 11 Pro 10.0.26300 (past 24H2), with the C:, D: and E: volumes all NTFS. No ReFS or Dev Drive volume was
mounted (`Get-Volume`), though `fsutil devdrv query` reports developer volumes enabled with a third-party antivirus filter attached
to them (`QQSysMonX64`), which every Dev Drive figure below includes. The session is not elevated, yet Hyper-V's cmdlets worked, so the
runner can make one: a dynamic 16 GB VHDX formatted `ReFS -DevDrive` and mounted as `G:` (ReFS 3.14, 4,096-byte clusters, checksums
on metadata only). That is the "ReFS volume the runner makes" row 1g's proof names, and it is available here without an administrator
prompt; whether a given CI runner permits it is not known.

```
New-VHD -Path probe.vhdx -SizeBytes 16GB -Dynamic ; Mount-VHD -Path probe.vhdx
$n = (Get-VHD -Path probe.vhdx).Number
Initialize-Disk -Number $n -PartitionStyle GPT
New-Partition -DiskNumber $n -UseMaximumSize -AssignDriveLetter |
  Format-Volume -FileSystem ReFS -DevDrive -NewFileSystemLabel PW1G -Confirm:$false
fsutil fsinfo refsinfo G:        # ReFS 3.14, Bytes Per Cluster 4096
fsutil devdrv query G:           # a trusted developer volume
Dismount-VHD -Path probe.vhdx   # afterwards; the VHDX was deleted
```

## What was timed

The script in the appendix builds a folder of N files of deterministic random content on a volume, then copies it three ways and
reads wall time and the volume's free-space change (`Write-VolumeCache` and five seconds' settling before each reading, because a
Dev Drive writes back late and an unsettled reading went negative in a first run):

- **plain**: `File.Copy` per file (Windows' copy engine, `CopyFileEx`);
- **robocopy**: `robocopy /E /MT:1`;
- **clone**: the design's per-file block clone: `SetLength`, then `FSCTL_DUPLICATE_EXTENTS_TO_FILE` over the whole clusters, the sub-cluster
  tail written ordinarily.

```
powershell -NoProfile -ExecutionPolicy Bypass -File measure.ps1 -Root G:\bench -Files 10000 -MinKB 4 -MaxKB 256 -Label refs10k
powershell -NoProfile -ExecutionPolicy Bypass -File measure.ps1 -Root G:\bench -Files 20000 -MinKB 1 -MaxKB 16 -Label refs20k-small
powershell -NoProfile -ExecutionPolicy Bypass -File measure.ps1 -Root G:\bench -Files 4 -MinKB 524288 -MaxKB 1048576 -Dirs 1 -Label refs-large
# the same three on NTFS (a folder on D:), -Root <dir on D:>, labels ntfs10k, ntfs20k-small, ntfs-large
```

Every clone was checked against its source (MD5 on 500 files, none differing).

## Results

Single runs on a machine with other work running (a family of agents' builds); the repeat of the 10,000-file row shows the spread.

| Folder | Volume | Plain copy | robocopy | Clone | Bytes the clone cost |
|---|---|---|---|---|---|
| 10,000 files, 1.24 GiB (4 to 256 KiB) | ReFS Dev Drive | 42.2 s, 26.7 s (repeat) | 78.2 s, 96.4 s | **25.5 s, 16.1 s** | 59 MB, 63 MB (4.4%, 4.7%) |
| same | NTFS | 38.0 s | 31.4 s | refused | plain copy 1.40 GB (105%) |
| 20,000 files, 0.16 GiB (1 to 16 KiB) | ReFS Dev Drive | 93.5 s | 134.9 s | **34.0 s** | 108 MB (62%) |
| same | NTFS | 27.6 s | 51.2 s | refused | plain copy 268 MB (153%) |
| 4 files, 2.59 GiB (0.5 to 1 GiB) | ReFS Dev Drive | 0.14 s | 0.40 s | **0.08 s** | ~0 (under 0.1 MB) |
| same | NTFS | 1.94 s | 7.53 s | refused | plain copy 2.83 GB (102%) |

What the figures say:

1. **Bytes.** A clone's cost is the file system's metadata plus each file's sub-cluster tail: about 5 to 6 KB per file here
   (59 MB over 10,000 files; 108 MB over 20,000 small files). It does not grow with the data: 2.6 GiB of large files cost under
   0.1 MB. A folder of small files, which is most source trees, saves less than its size suggests (62% of 0.16 GiB), but the figure
   is still the same per-file constant, not a copy. Overwriting 64 MB of a clone of a 933 MB file consumed 67.0 MB, the written range
   only; the other 869 MB stayed shared.
2. **Time grows with the count of files, not their size**, as the survey said. Per file the clone cost 1.6 to 2.5 ms (10,000 files)
   and 1.7 ms (20,000 small files); the four large files took 0.08 s in all. On NTFS a plain copy cost 1.4 ms a file for the small
   files and 3.8 ms for the mixed ones, with its time also growing with bytes (1.94 s for 2.59 GiB).
3. **Against a plain copy on the same volume the clone is 1.7 to 2.8 times faster** (2.8 times on 20,000 small files), because
   `CopyFileEx` and robocopy add per-file work (streams, attributes, security) on top of the same block clone. Against NTFS's plain
   copy, a clone on this Dev Drive is faster for mixed folders (16 to 26 s against 31 to 38 s for 10,000 files) and slower for many
   tiny files (34 s against 28 s for 20,000 files of 1 to 16 KiB), because the antivirus filter and ReFS metadata cost per file
   dominate there. Large folders of few files are where it wins outright (0.08 s against 1.94 s).
4. **Windows' own copy already clones on a Dev Drive.** `File.Copy` and robocopy to the same ReFS volume consumed 3 to 5% of the
   bytes (10,000 files) and nothing measurable (large files), not the 100% they cost on NTFS. The design's statement "which Windows'
   copy engine uses there from 24H2" holds on 10.0.26300.
5. **The probe's refusal is clean on NTFS.** `FSCTL_DUPLICATE_EXTENTS_TO_FILE` returned Win32 error 1 (`ERROR_INVALID_FUNCTION`) on the
   first file, at once (0.2 s), so a probe file per volume tells `copy` from `clone` with the answer the design remembers. It also
   showed what the refusal leaves behind: the destination, already sized by `SetLength`, stayed on disk (NTFS allocated the whole
   size: 0.99 GB for the first of the large files).

## What it means for the design

The design's choice stands: the per-file `FSCTL_DUPLICATE_EXTENTS_TO_FILE` clone on the place's own ReFS volume, the probe, and `auto`
taking `copy` on NTFS. No product code is changed and no decision is reopened. The measurement adds four constraints row 1g's builder
should meet, none contradicting the design:

- **Align to the volume's cluster**, which is a per-volume figure (4,096 here; `GetDiskFreeSpace` gives it): clone the whole clusters
  and write the tail ordinarily. The source offset, destination offset and count must be multiples of the cluster.
- **The probe clones a probe file, and a refusal removes what it made**, so a refused destination is never left sized on NTFS.
- **The time to record is per file**: the row's "clone time per 10,000 files" is 16 to 26 s on this machine (1.6 to 2.6 ms a file).
  The design bounds only `copy` (`copyBound`, 2 GB and 20,000 files) and leaves `clone` unbounded; the measurement agrees on bytes
  (a clone of 2.6 GiB took 0.08 s) and shows the wait is the file count's, so a folder of several tens of thousands of files is
  where a person would still wait a minute with the clone. The row should say that wait in the start's words rather than add a bound.
- **Do not rely on `File.Copy`** to clone: it did here, but it is 1.7 to 2.8 times slower than the direct clone and gives no answer
  when the volume cannot clone (it silently copies), which the `Refusal` must be able to say.

## Not measured

A Dev Drive made by `Format-Volume` on a physical partition (the VHDX is the same file system); a volume with 64 KiB clusters;
folders past 20,000 files; concurrent clones (two sessions starting at once); the same on a CI runner. APFS and btrfs/XFS are not
reachable from this machine (WSL here is ext4).

## Appendix: `measure.ps1`

```powershell
param([string]$Root, [int]$Files = 10000, [int]$MinKB = 4, [int]$MaxKB = 256, [string]$Label = "run", [int]$Dirs = 100, [int]$Cluster = 4096)

Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class Cow {
  [StructLayout(LayoutKind.Sequential)]
  struct DUP { public IntPtr Src; public long SrcOff; public long DstOff; public long Count; }
  [DllImport("kernel32.dll", SetLastError = true)]
  static extern bool DeviceIoControl(SafeFileHandle h, uint code, ref DUP inb, int inSize, IntPtr outb, int outSize, out int ret, IntPtr ov);
  const uint FSCTL_DUPLICATE_EXTENTS_TO_FILE = 0x00098344;
  public static long Cluster = 4096;
  public static int LastError;
  public static int Refused;
  public static void CopyTree(string src, string dst, bool clone) {
    Directory.CreateDirectory(dst);
    foreach (var f in Directory.EnumerateFiles(src)) {
      var t = Path.Combine(dst, Path.GetFileName(f));
      if (clone) { if (!CloneFile(f, t)) { Refused++; return; } } else File.Copy(f, t);
    }
    foreach (var d in Directory.EnumerateDirectories(src)) CopyTree(d, Path.Combine(dst, Path.GetFileName(d)), clone);
  }
  public static bool CloneFile(string src, string dst) {
    using (var s = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var d = new FileStream(dst, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
      long len = s.Length;
      if (len == 0) return true;
      long whole = len / Cluster * Cluster;
      d.SetLength(len);
      long done = 0, chunk = 1L << 30;
      while (done < whole) {
        long n = Math.Min(chunk, whole - done);
        var dup = new DUP { Src = s.SafeFileHandle.DangerousGetHandle(), SrcOff = done, DstOff = done, Count = n };
        int ret;
        if (!DeviceIoControl(d.SafeFileHandle, FSCTL_DUPLICATE_EXTENTS_TO_FILE, ref dup, Marshal.SizeOf(typeof(DUP)), IntPtr.Zero, 0, out ret, IntPtr.Zero)) {
          LastError = Marshal.GetLastWin32Error(); return false;
        }
        done += n;
      }
      if (whole < len) {
        var buf = new byte[len - whole];
        s.Position = whole; int r = 0; while (r < buf.Length) { int k = s.Read(buf, r, buf.Length - r); if (k <= 0) break; r += k; }
        d.Position = whole; d.Write(buf, 0, r);
      }
      return true;
    }
  }
}
"@
[Cow]::Cluster = $Cluster

$drive = $Root.Substring(0, 1)
function Settle { Write-VolumeCache -DriveLetter $drive -ErrorAction SilentlyContinue; Start-Sleep -Seconds 5 }
function Free { (New-Object IO.DriveInfo $drive).AvailableFreeSpace }

$src = Join-Path $Root "src"
Remove-Item -Recurse -Force $Root -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $src | Out-Null

$rng = New-Object Random 1093
$total = 0L
for ($i = 0; $i -lt $Files; $i++) {
  $dir = Join-Path $src ("d{0:D3}" -f ($i % $Dirs))
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  $size = $rng.Next($MinKB * 1024, $MaxKB * 1024 + 1)
  $buf = New-Object byte[] $size; $rng.NextBytes($buf)
  [IO.File]::WriteAllBytes((Join-Path $dir ("f{0:D5}.bin" -f $i)), $buf)
  $total += $size
}
"[$Label] fixture: $Files files, {0:N0} bytes" -f $total

function Measure-Copy([string]$name, [scriptblock]$body) {
  $dst = Join-Path $Root $name
  Settle; $before = Free
  $t = [Diagnostics.Stopwatch]::StartNew()
  & $body $dst
  $t.Stop()
  Settle; $after = Free
  "[$Label] {0,-9} {1,8:N2} s  volume bytes consumed {2,15:N0}  ({3:N1}% of the folder)" -f $name, $t.Elapsed.TotalSeconds, ($before - $after), (100.0 * ($before - $after) / $total)
}

Measure-Copy "plain"    { param($dst) [Cow]::CopyTree($src, $dst, $false) }
Measure-Copy "robocopy" { param($dst) robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /MT:1 | Out-Null }
Measure-Copy "clone"    { param($dst) [Cow]::CopyTree($src, $dst, $true) }
if ([Cow]::Refused -gt 0) { "[$Label] clone refused by the volume, Win32 error $([Cow]::LastError)" }

$bad = 0
foreach ($f in Get-ChildItem $src -Recurse -File | Select-Object -First 500) {
  $c = Join-Path (Join-Path (Join-Path $Root "clone") $f.Directory.Name) $f.Name
  if ((Get-FileHash $f.FullName -Algorithm MD5).Hash -ne (Get-FileHash $c -Algorithm MD5).Hash) { $bad++ }
}
"[$Label] clone verified against the source on 500 files: $bad differ"

$victim = Get-ChildItem (Join-Path $Root "clone") -Recurse -File | Sort-Object Length -Descending | Select-Object -First 1
Settle; $before = Free
$n = [Math]::Min($victim.Length, 64MB)
$fs = [IO.File]::Open($victim.FullName, 'Open', 'ReadWrite'); $fs.Position = 0; $fs.Write((New-Object byte[] $n), 0, $n); $fs.Dispose()
Settle
"[$Label] overwriting {0:N0} bytes of a clone of a {1:N0}-byte file consumed {2:N0} bytes" -f $n, $victim.Length, ($before - (Free))
```
