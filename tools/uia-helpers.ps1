# Shared UIAutomation helpers for driving the WPF app in tests/screenshots.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Shot {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static void FocusAndSize(IntPtr hWnd, int x, int y, int w, int h) {
        ShowWindow(hWnd, 9); // SW_RESTORE
        MoveWindow(hWnd, x, y, w, h, true);
        SetForegroundWindow(hWnd);
    }
}
"@

function Find-UiaById {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Id,
        [int]$TimeoutMs = 5000
    )
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $el = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -ne $el) { return $el }
        Start-Sleep -Milliseconds 150
    }
    return $null
}

function Find-UiaByName {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [int]$TimeoutMs = 5000
    )
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $el = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -ne $el) { return $el }
        Start-Sleep -Milliseconds 150
    }
    return $null
}

function Invoke-UiaClick {
    param([System.Windows.Automation.AutomationElement]$Element)
    if ($null -eq $Element) { throw "Invoke-UiaClick: element is null" }
    # prefer pattern-based activation: works regardless of window z-order
    try {
        $invoke = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
        return
    } catch { }
    try {
        $toggle = $Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $toggle.Toggle()
        return
    } catch { }
    try {
        $sel = $Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $sel.Select()
        return
    } catch { }
    # last resort: a real mouse click at the element's clickable point
    $pt = $Element.GetClickablePoint()
    $orig = [System.Windows.Forms.Cursor]::Position
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($pt.X, $pt.Y)
    Start-Sleep -Milliseconds 80
    $sig = '[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);'
    $type = Add-Type -MemberDefinition $sig -Name NativeClick -Namespace Win32 -PassThru
    $type::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)  # LEFTDOWN
    $type::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)  # LEFTUP
    Start-Sleep -Milliseconds 80
    [System.Windows.Forms.Cursor]::Position = $orig
}

function Set-UiaText {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [string]$Value
    )
    if ($null -eq $Element) { throw "Set-UiaText: element is null" }
    $vp = $null
    try { $vp = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern) } catch { }
    if ($null -eq $vp) { throw "Element '$($Element.Current.Name)' does not support ValuePattern" }
    $vp.SetValue($Value)
}

function Save-WindowShot {
    param(
        [IntPtr]$Hwnd,
        [string]$OutFile
    )
    # re-assert position/foreground and make sure the window is not minimized
    # (a minimized window reports a rect at -32000, which captures as garbage)
    [Win32Shot]::FocusAndSize($Hwnd, 20, 20, 1480, 820)
    Start-Sleep -Milliseconds 400
    $rect = New-Object Win32Shot+RECT
    [Win32Shot]::GetWindowRect($Hwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0 -or $rect.Left -lt -20000) { throw "Bad window rect for $OutFile (window minimized or gone)" }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g.Dispose()
    $bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  saved $OutFile"
}

function Focus-Window {
    param([IntPtr]$Hwnd, [int]$X = 20, [int]$Y = 20, [int]$W = 1480, [int]$H = 820)
    [Win32Shot]::FocusAndSize($Hwnd, $X, $Y, $W, $H)
    Start-Sleep -Milliseconds 600
}
