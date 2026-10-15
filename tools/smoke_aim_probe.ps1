# Holds the right mouse button (the "aim" action) while a smoke or car stress run is going, to prove
# the dev harnesses ignore real input. Start it in the background, then launch the run:
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke_aim_probe.ps1 &
#   "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --quality=0
#
# Before the input shield this made the car checks fail (the player walks at 60% while aiming and
# stops short of the car); after it the run passes. The button is released when the window closes.
param([int]$DownAt = 2, [int]$UpAt = 45)
Add-Type -Namespace Veld -Name MouseProbe -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, System.UIntPtr dwExtraInfo);
'@
Start-Sleep -Seconds $DownAt
[Veld.MouseProbe]::mouse_event(0x0008, 0, 0, 0, [System.UIntPtr]::Zero)
Write-Output "right button down at $DownAt s"
Start-Sleep -Seconds ($UpAt - $DownAt)
[Veld.MouseProbe]::mouse_event(0x0010, 0, 0, 0, [System.UIntPtr]::Zero)
Write-Output "right button up at $UpAt s"
