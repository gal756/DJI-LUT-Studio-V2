# =====================================================================
# DJI LUT Studio - Native Windows Desktop GUI
# Accelerated by NVIDIA RTX NVENC (10-bit Rec.709 3D LUT)
# =====================================================================

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

# Determine WSL runner or distro
$wslDistro = "Ubuntu-26.04"
$testDistro = wsl.exe -d $wslDistro -e echo "ok" 2>$null
if ($LASTEXITCODE -eq 0) {
    $wslCmd = "wsl.exe -d $wslDistro"
} else {
    $wslCmd = "wsl.exe"
}

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="DJI LUT Studio - Open Source Video Color Grading Suite"
        Height="720" Width="840" MinHeight="680" MinWidth="780"
        WindowStartupLocation="CenterScreen"
        Background="#0D1117" Foreground="#F0F6FC" FontFamily="Segoe UI">
    <Window.Resources>
        <Style TargetType="Label">
            <Setter Property="Foreground" Value="#8B949E"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Margin" Value="0,4,0,2"/>
        </Style>
        <Style TargetType="ComboBox">
            <Setter Property="Background" Value="#161B22"/>
            <Setter Property="Foreground" Value="#F0F6FC"/>
            <Setter Property="BorderBrush" Value="#30363D"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Padding" Value="6,2"/>
        </Style>
        <Style TargetType="TextBox">
            <Setter Property="Background" Value="#161B22"/>
            <Setter Property="Foreground" Value="#F0F6FC"/>
            <Setter Property="BorderBrush" Value="#30363D"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Padding" Value="6,4"/>
        </Style>
    </Window.Resources>

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row="0" Background="#161B22" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="16,12" Margin="0,0,0,16">
            <DockPanel>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="🎬 DJI LUT STUDIO" FontSize="18" FontWeight="Bold" Foreground="#00D2FF" VerticalAlignment="Center"/>
                    <Border Background="#1F3A4B" CornerRadius="4" Padding="6,2" Margin="12,0,0,0" VerticalAlignment="Center">
                        <TextBlock Text="v2.0.0 Windows" FontSize="11" FontWeight="SemiBold" Foreground="#00D2FF"/>
                    </Border>
                </StackPanel>
                <Border HorizontalAlignment="Right" Background="#1A3328" BorderBrush="#00F2A9" BorderThickness="1" CornerRadius="12" Padding="10,4">
                    <StackPanel Orientation="Horizontal">
                        <Ellipse Width="8" Height="8" Fill="#00F2A9" Margin="0,0,6,0" VerticalAlignment="Center"/>
                        <TextBlock Text="NVIDIA RTX 3070 NVENC (10-bit Active)" FontSize="11" FontWeight="SemiBold" Foreground="#00F2A9"/>
                    </StackPanel>
                </Border>
            </DockPanel>
        </Border>

        <!-- Main Body -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="0,0,10,0">
                <!-- Source Input -->
                <Border Background="#161B22" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="16" Margin="0,0,0,14">
                    <StackPanel>
                        <Label Content="SOURCE VIDEO OR FOLDER (DRAG &amp; DROP OR BROWSE)"/>
                        <Grid Margin="0,4,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox x:Name="TxtInputPath" Grid.Column="0" Text="D:\dji" AllowDrop="True"/>
                            <Button x:Name="BtnBrowseFile" Grid.Column="1" Content="Browse Clip..." Background="#21262D" Foreground="#F0F6FC" BorderBrush="#30363D" Margin="8,0,0,0" Padding="12,0" Height="32"/>
                            <Button x:Name="BtnBrowseFolder" Grid.Column="2" Content="Browse Folder..." Background="#21262D" Foreground="#F0F6FC" BorderBrush="#30363D" Margin="8,0,0,0" Padding="12,0" Height="32"/>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Model & LUT Selection -->
                <Border Background="#161B22" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="16" Margin="0,0,0,14">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="16"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0">
                            <Label Content="DJI CAMERA / DRONE MODEL"/>
                            <ComboBox x:Name="CmbDevice"/>
                        </StackPanel>
                        <StackPanel Grid.Column="2">
                            <Label Content="3D CONVERSION LUT (D-LOG / D-LOG M TO REC.709)"/>
                            <ComboBox x:Name="CmbLut"/>
                        </StackPanel>
                    </Grid>
                </Border>

                <!-- Grading Style & Encoder Settings -->
                <Border Background="#161B22" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="16" Margin="0,0,0,14">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="12"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="16"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <!-- Look Style -->
                        <StackPanel Grid.Row="0" Grid.Column="0">
                            <Label Content="POST-LUT LOOK PRESET"/>
                            <ComboBox x:Name="CmbLook">
                                <ComboBoxItem Content="Vibrant Pop (Recommended: Punchy Sky, Greens &amp; Sharpness)" IsSelected="True"/>
                                <ComboBoxItem Content="Natural Rec.709 (100% DJI Baseline Reference)"/>
                                <ComboBoxItem Content="Cinematic Film (Filmic S-Curve, Deep Velvety Shadows)"/>
                                <ComboBoxItem Content="Golden Hour (Warm Amber Sunlight &amp; Sunset Glow)"/>
                                <ComboBoxItem Content="Teal &amp; Orange (Blockbuster Contrast Separation)"/>
                            </ComboBox>
                        </StackPanel>

                        <!-- Codec -->
                        <StackPanel Grid.Row="0" Grid.Column="2">
                            <Label Content="ENCODER CODEC"/>
                            <ComboBox x:Name="CmbCodec">
                                <ComboBoxItem Content="HEVC / H.265 (10-Bit NVENC Hardware)" IsSelected="True"/>
                                <ComboBoxItem Content="H.264 (8/10-Bit NVENC Hardware)"/>
                                <ComboBoxItem Content="Apple ProRes 422 HQ (Professional Mastering)"/>
                            </ComboBox>
                        </StackPanel>

                        <!-- Resolution -->
                        <StackPanel Grid.Row="2" Grid.Column="0">
                            <Label Content="OUTPUT RESOLUTION"/>
                            <ComboBox x:Name="CmbScale">
                                <ComboBoxItem Content="Keep Source Resolution (Up to 4K 60fps)" IsSelected="True"/>
                                <ComboBoxItem Content="Scale to 4K UHD (3840x2160)"/>
                                <ComboBoxItem Content="Scale to 2.7K (2704x1520)"/>
                                <ComboBoxItem Content="Scale to 1080p Proxy (Quick Discord/WhatsApp Share)"/>
                            </ComboBox>
                        </StackPanel>

                        <!-- Options Checkboxes -->
                        <StackPanel Grid.Row="2" Grid.Column="2" VerticalAlignment="Center" Margin="0,18,0,0">
                            <CheckBox x:Name="ChkSharpen" Content="Apply Fine Foliage Unsharp Mask" Foreground="#F0F6FC" IsChecked="True" Margin="0,0,0,6"/>
                            <CheckBox x:Name="ChkPreview" Content="Generate Side-by-Side Comparison JPEG" Foreground="#F0F6FC" IsChecked="True"/>
                        </StackPanel>
                    </Grid>
                </Border>

                <!-- Console / Status Output -->
                <Border Background="#06080B" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,10">
                    <StackPanel>
                        <Label Content="LIVE PIPELINE LOGS" Margin="0,0,0,4"/>
                        <TextBox x:Name="TxtLog" Height="100" FontFamily="Consolas" FontSize="11" Foreground="#00F2A9" Background="#06080B" BorderThickness="0" IsReadOnly="True" VerticalScrollBarVisibility="Auto" TextWrapping="Wrap" Text="DJI LUT Studio GUI Ready. Drag clips or choose a model above."/>
                    </StackPanel>
                </Border>
            </StackPanel>
        </ScrollViewer>

        <!-- Bottom Action Bar -->
        <Border Grid.Row="2" Background="#161B22" BorderBrush="#30363D" BorderThickness="1" CornerRadius="8" Padding="16,12" Margin="0,10,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                    <ProgressBar x:Name="PrgBar" Height="12" Maximum="100" Value="0" Background="#21262D" Foreground="#00D2FF" Margin="0,0,16,4"/>
                    <TextBlock x:Name="LblStatus" Text="Ready to grade." FontSize="11" Foreground="#8B949E"/>
                </StackPanel>
                <Button x:Name="BtnOpenFolder" Grid.Column="1" Content="Open Output Folder" Background="#21262D" Foreground="#F0F6FC" BorderBrush="#30363D" Height="38" Padding="14,0" Margin="0,0,10,0"/>
                <Button x:Name="BtnStartGrade" Grid.Column="2" Content="🚀 APPLY LUT &amp; START GRADING" Background="#00D2FF" Foreground="#0C0F14" FontWeight="Bold" Height="38" Padding="20,0" BorderThickness="0"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$reader = (New-Object System.Xml.XmlNodeReader $xaml)
$window = [System.Windows.Markup.XamlReader]::Load($reader)

# Get Controls
$txtInputPath    = $window.FindName("TxtInputPath")
$btnBrowseFile   = $window.FindName("BtnBrowseFile")
$btnBrowseFolder = $window.FindName("BtnBrowseFolder")
$cmbDevice       = $window.FindName("CmbDevice")
$cmbLut          = $window.FindName("CmbLut")
$cmbLook         = $window.FindName("CmbLook")
$cmbCodec        = $window.FindName("CmbCodec")
$cmbScale        = $window.FindName("CmbScale")
$chkSharpen      = $window.FindName("ChkSharpen")
$chkPreview      = $window.FindName("ChkPreview")
$txtLog          = $window.FindName("TxtLog")
$prgBar          = $window.FindName("PrgBar")
$lblStatus       = $window.FindName("LblStatus")
$btnStartGrade   = $window.FindName("BtnStartGrade")
$btnOpenFolder   = $window.FindName("BtnOpenFolder")

# Populate Devices & LUTs Data
$deviceData = @{
    "DJI O4 Pro / Air Unit"              = @("o4_dlogm_rec709|Official D-Log M to Rec.709 V1 (Recommended)")
    "DJI Avata 2"                        = @("avata2_dlogm_rec709|Official Avata 2 D-Log M to Rec.709 V1")
    "DJI Avata 360"                      = @("avata360_dlogm_rec709|Official Avata 360 D-Log M to Rec.709")
    "DJI Mavic 3 / Pro / Classic"        = @("m3_dlog_rec709_std|Official Mavic 3 D-Log to Rec.709 Standard", "m3_dlog_rec709_vivid|Official Mavic 3 D-Log to Rec.709 Vivid", "m3_dlogm_rec709|Official Mavic 3 D-Log M to Rec.709")
    "DJI Mavic 4 Pro"                    = @("m4p_dlog_std|Official Mavic 4 Pro D-Log to Rec.709 Standard", "m4p_dlog_vivid|Official Mavic 4 Pro D-Log to Rec.709 Vivid", "m4p_dlogm|Official Mavic 4 Pro D-Log M to Rec.709")
    "DJI Air 3 / Air 3S"                 = @("air3_dlogm|Official Air 3 D-Log M to Rec.709 V1", "air3s_dlogm|Official Air 3S D-Log M to Rec.709 V1")
    "DJI Mini 4 Pro / Mini 5 Pro"        = @("mini4_dlogm|Official Mini 4 Pro D-Log M to Rec.709 V1", "mini5_dlogm|Official Mini 5 Pro D-Log M to Rec.709")
    "DJI Osmo Pocket 3"                  = @("op3_dlogm|Official Pocket 3 D-Log M to Rec.709", "op3_spring|Master Look: Spring Pro", "op3_summer|Master Look: Summer Pro", "op3_autumn|Master Look: Autumn Pro", "op3_winter|Master Look: Winter Pro")
    "DJI Osmo Pocket 4 / Pocket 4 Pro"   = @("op4_dlog_std|Official Pocket 4 D-Log to Rec.709", "op4_dlog_vivid|Official Pocket 4 D-Log to Rec.709 Vivid", "op4p_dlog2_std|Official Pocket 4P D-Log2 to Rec.709", "op4p_dlog2_vivid|Official Pocket 4P D-Log2 Vivid")
    "DJI Osmo Action 4"                  = @("oa4_dlogm|Official Action 4 D-Log M to Rec.709", "oa4_ice|Action Look: Ice Pro", "oa4_nature|Action Look: Nature Pro", "oa4_forest|Action Look: Forest Pro")
    "DJI Osmo Action 5 Pro"              = @("oa5p_dlogm|Official Action 5 Pro D-Log M to Rec.709", "oa5p_mei|Master Look: Mei (Plum)", "oa5p_ju|Master Look: Ju (Chrysanthemum)", "oa5p_zhu|Master Look: Zhu (Bamboo)", "oa5p_lan|Master Look: Lan (Orchid)")
    "DJI Osmo Action 6"                  = @("oa6_dlogm|Official Action 6 D-Log M to Rec.709")
    "DJI Osmo 360 / Nano / Lito"         = @("osmo_360_dlogm|Official Osmo 360 D-Log M", "osmo_nano_dlogm|Official Osmo Nano D-Log M", "lito_x1_dlogm|Official Lito X1 D-Log M")
    "DJI Zenmuse X7 / Ronin 4D / Inspire"= @("x7_dlog_rec709|Official Cinema D-Log to Rec.709", "x7_linear_dlog|Official Linear to D-Log")
    "DJI Phantom 4 / Phantom 4 Pro"      = @("p4_dlog_rec709|Official Phantom 4 D-Log to Rec.709", "p4_dlog_srgb|Official Phantom 4 D-Log to sRGB")
    "DJI Generic / Universal D-Log M"    = @("generic_dlogm_rec709|Official Universal DJI D-Log M to Rec.709")
}

foreach ($dev in $deviceData.Keys | Sort-Object) {
    [void]$cmbDevice.Items.Add($dev)
}
$cmbDevice.SelectedItem = "DJI O4 Pro / Air Unit"

function Update-LutDropdown {
    $cmbLut.Items.Clear()
    $selDev = $cmbDevice.SelectedItem
    if ($deviceData.ContainsKey($selDev)) {
        foreach ($lutItem in $deviceData[$selDev]) {
            $parts = $lutItem -split '\|'
            $cbi = New-Object System.Windows.Controls.ComboBoxItem
            $cbi.Tag = $parts[0]
            $cbi.Content = $parts[1]
            [void]$cmbLut.Items.Add($cbi)
        }
    }
    if ($cmbLut.Items.Count -gt 0) {
        $cmbLut.SelectedIndex = 0
    }
}

$cmbDevice.Add_SelectionChanged({ Update-LutDropdown })
Update-LutDropdown

# Browse Buttons
$btnBrowseFile.Add_Click({
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Filter = "Video Files (*.mp4;*.mov;*.mkv)|*.mp4;*.mov;*.mkv|All Files (*.*)|*.*"
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtInputPath.Text = $dlg.FileName
    }
})

$btnBrowseFolder.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtInputPath.Text = $dlg.SelectedPath
    }
})

# Drag & Drop onto TextBox
$txtInputPath.Add_PreviewDragOver({
    param($s, $e)
    if ($e.Data.GetDataPresent([System.Windows.DataFormats]::FileDrop)) {
        $e.Effects = [System.Windows.DragDropEffects]::Copy
        $e.Handled = $true
    }
})

$txtInputPath.Add_Drop({
    param($s, $e)
    if ($e.Data.GetDataPresent([System.Windows.DataFormats]::FileDrop)) {
        $files = $e.Data.GetData([System.Windows.DataFormats]::FileDrop)
        if ($files.Length -gt 0) {
            $txtInputPath.Text = $files[0]
        }
    }
})

# Open Output Folder
$btnOpenFolder.Add_Click({
    $inPath = $txtInputPath.Text.Trim('"')
    if (Test-Path $inPath) {
        if (Test-Path -PathType Leaf $inPath) {
            $outDir = Join-Path (Split-Path $inPath) "graded"
        } else {
            $outDir = Join-Path $inPath "graded"
        }
        if (-not (Test-Path $outDir)) {
            New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        }
        Start-Process "explorer.exe" $outDir
    } else {
        [System.Windows.MessageBox]::Show("Input path does not exist yet.", "DJI LUT Studio")
    }
})

# Start Grading Button Click
$btnStartGrade.Add_Click({
    $targetPath = $txtInputPath.Text.Trim('"')
    if (-not (Test-Path $targetPath)) {
        [System.Windows.MessageBox]::Show("Please enter a valid file or folder path.", "DJI LUT Studio", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
        return
    }

    $lutId = $cmbLut.SelectedItem.Tag
    $lookIdx = $cmbLook.SelectedIndex
    $lookMap = @("vibrant", "natural", "cinematic", "golden", "teal_orange")
    $look = $lookMap[$lookIdx]

    $codecIdx = $cmbCodec.SelectedIndex
    $codecMap = @("hevc", "h264", "prores")
    $codec = $codecMap[$codecIdx]

    $scaleIdx = $cmbScale.SelectedIndex
    $scaleMap = @("keep", "4k", "2.7k", "1080p")
    $scale = $scaleMap[$scaleIdx]

    $sharpenFlag = if ($chkSharpen.IsChecked) { "--sharpen" } else { "" }
    $previewFlag = if ($chkPreview.IsChecked) { "-p" } else { "" }

    $btnStartGrade.IsEnabled = $false
    $lblStatus.Text = "Grading with NVIDIA RTX NVENC in progress..."
    $prgBar.IsIndeterminate = $true
    $txtLog.Text = "[INFO] Launching DJI LUT Studio pipeline...`n[INFO] Target: $targetPath`n[INFO] LUT: $lutId`n[INFO] Look: $look`n"

    $wslScriptPath = "/home/gal756/LUT-adder/pipeline.py"
    $cmdArgs = "-e /usr/bin/python3 $wslScriptPath `"$targetPath`" --look $look -c $codec --scale $scale $sharpenFlag $previewFlag"

    Start-Process -FilePath "wsl.exe" -ArgumentList "-d", $wslDistro, "-e", "/usr/bin/python3", "/home/gal756/LUT-adder/dji_lut_studio/cli.py", "`"$targetPath`"", "-l", $lutId, "--look", $look, "-c", $codec, "--scale", $scale, $sharpenFlag, $previewFlag -Wait

    $prgBar.IsIndeterminate = $false
    $prgBar.Value = 100
    $lblStatus.Text = "Grading completed! Outputs placed in 'graded' folder."
    $txtLog.Text += "`n[SUCCESS] Grading process finished! Open output folder to view clips."
    $btnStartGrade.IsEnabled = $true

    $res = [System.Windows.MessageBox]::Show("Grading complete! Would you like to open the output folder?", "DJI LUT Studio", [System.Windows.MessageBoxButton]::YesNo, [System.Windows.MessageBoxImage]::Information)
    if ($res -eq [System.Windows.MessageBoxResult]::Yes) {
        & $btnOpenFolder.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
    }
})

# Show Window
[void]$window.ShowDialog()
