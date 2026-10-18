using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Collections.Generic;

namespace DJILutStudio
{
    public class Program
    {
        private static string wslDistro = "Ubuntu-26.04";
        private static string appDir = @"/home/gal756/LUT-adder";

        private static Window mainWindow;
        private static TextBox txtInputPath;
        private static Button btnBrowseFile;
        private static Button btnBrowseFolder;
        private static ComboBox cmbDevice;
        private static ComboBox cmbLut;
        private static ComboBox cmbLook;
        private static ComboBox cmbCodec;
        private static ComboBox cmbScale;
        private static Slider sliderCq;
        private static TextBlock lblCqVal;
        private static Slider sliderIntensity;
        private static TextBlock lblIntensityVal;
        private static CheckBox chkSharpen;
        private static CheckBox chkPreview;
        private static CheckBox chkGyroflow;
        private static Button btnOpenGyroflow;
        private static Button btnStartGrade;
        private static Button btnCancelGrade;
        private static Button btnOpenFolder;
        private static Button btnWatcher;
        private static Button btnGenPreview;
        private static ProgressBar progressBar;
        private static TextBlock lblStatus;
        private static TextBlock lblSpeedEta;
        private static TextBlock lblGpuBadge;
        private static TextBlock lblEngineBadge;
        private static TextBlock lblFootageSpecs;
        private static Border borderFootageSpecs;
        private static TextBox txtLog;
        private static Image imgPreview;
        private static TextBlock lblPreviewPlaceholder;
        private static Border borderPreview;
        private static Border borderGyroAdvisory;
        private static TextBlock lblGyroAdvisoryIcon;
        private static TextBlock lblGyroAdvisoryText;
        private static bool isEisRockSteadyActive = false;
        private static bool isEisHorizonSteadyActive = false;

        private static Process activeProcess = null;
        private static Thread workerThread = null;
        private static volatile bool cancelRequested = false;

        private static FileSystemWatcher folderWatcher = null;
        private static bool isWatching = false;

        public enum GpuVendor { Nvidia, Amd, Intel, Cpu }
        private static GpuVendor detectedGpu = GpuVendor.Cpu;
        private static string detectedGpuBadge = "CPU Multi-Core (libx265)";
        private static string detectedGpuName = "CPU Software Encoding (libx265)";

        private static Dictionary<string, List<LutEntry>> deviceDatabase;

        public class LutEntry
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string RelativeFile { get; set; }
            public bool IsRecommended { get; set; }
            public LutEntry(string id, string name, string file = "", bool rec = false)
            {
                Id = id;
                DisplayName = name;
                RelativeFile = file;
                IsRecommended = rec;
            }
            public override string ToString() { return DisplayName + (IsRecommended ? " ★ [Recommended]" : ""); }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var app = new Application();
                string initialFile = (args != null && args.Length > 0) ? args[0] : null;
                CreateAndShowUI(initialFile);
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fatal Error: " + ex.ToString(), "DJI LUT Studio");
            }
        }

        private static void CreateAndShowUI(string initialFile)
        {
            InitDatabase();

            string xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='DJI LUT Studio PRO - Universal 10-Bit Color Grading &amp; Telemetry Suite'
        Height='800' Width='1060' MinHeight='720' MinWidth='980'
        WindowStartupLocation='CenterScreen'
        Background='#0A0D14' Foreground='#F0F6FC' FontFamily='Segoe UI'
        AllowDrop='True'>

    <Window.Resources>
        <!-- Sleek Studio ScrollBar -->
        <Style TargetType='{x:Type ScrollBar}'>
            <Setter Property='Background' Value='#0A0D14'/>
            <Setter Property='Foreground' Value='#222A3A'/>
            <Setter Property='Width' Value='8'/>
        </Style>

        <!-- Studio Dark TextBox -->
        <Style TargetType='{x:Type TextBox}'>
            <Setter Property='Background' Value='#121722'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='BorderBrush' Value='#222A3A'/>
            <Setter Property='BorderThickness' Value='1'/>
            <Setter Property='Padding' Value='10,6'/>
            <Setter Property='FontSize' Value='13'/>
            <Setter Property='VerticalContentAlignment' Value='Center'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type TextBox}'>
                        <Border Name='Bd' Background='{TemplateBinding Background}'
                                BorderBrush='{TemplateBinding BorderBrush}'
                                BorderThickness='{TemplateBinding BorderThickness}'
                                CornerRadius='6'>
                            <ScrollViewer Name='PART_ContentHost' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsFocused' Value='True'>
                                <Setter TargetName='Bd' Property='BorderBrush' Value='#00E5FF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Standard Button Style -->
        <Style x:Key='SubButton' TargetType='{x:Type Button}'>
            <Setter Property='Background' Value='#182030'/>
            <Setter Property='Foreground' Value='#E6EDF3'/>
            <Setter Property='BorderBrush' Value='#263248'/>
            <Setter Property='BorderThickness' Value='1'/>
            <Setter Property='FontSize' Value='12'/>
            <Setter Property='FontWeight' Value='SemiBold'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Padding' Value='12,6'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Button}'>
                        <Border Name='Bd' Background='{TemplateBinding Background}'
                                BorderBrush='{TemplateBinding BorderBrush}'
                                BorderThickness='{TemplateBinding BorderThickness}'
                                CornerRadius='6'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#25324A'/>
                                <Setter TargetName='Bd' Property='BorderBrush' Value='#00E5FF'/>
                            </Trigger>
                            <Trigger Property='IsPressed' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#121722'/>
                            </Trigger>
                            <Trigger Property='IsEnabled' Value='False'>
                                <Setter TargetName='Bd' Property='Opacity' Value='0.4'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Primary Action Button (Gradient Cyan) -->
        <Style x:Key='PrimaryActionBtn' TargetType='{x:Type Button}'>
            <Setter Property='Background'>
                <Setter.Value>
                    <LinearGradientBrush StartPoint='0,0' EndPoint='1,1'>
                        <GradientStop Color='#00E5FF' Offset='0.0'/>
                        <GradientStop Color='#0088FF' Offset='1.0'/>
                    </LinearGradientBrush>
                </Setter.Value>
            </Setter>
            <Setter Property='Foreground' Value='#06090F'/>
            <Setter Property='FontSize' Value='13.5'/>
            <Setter Property='FontWeight' Value='Bold'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Button}'>
                        <Border Name='Bd' Background='{TemplateBinding Background}' CornerRadius='6' Padding='20,0'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='Bd' Property='Opacity' Value='0.92'/>
                            </Trigger>
                            <Trigger Property='IsPressed' Value='True'>
                                <Setter TargetName='Bd' Property='Opacity' Value='0.75'/>
                            </Trigger>
                            <Trigger Property='IsEnabled' Value='False'>
                                <Setter TargetName='Bd' Property='Opacity' Value='0.35'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Danger / Cancel Button -->
        <Style x:Key='DangerActionBtn' TargetType='{x:Type Button}'>
            <Setter Property='Background' Value='#35181D'/>
            <Setter Property='Foreground' Value='#FF5566'/>
            <Setter Property='BorderBrush' Value='#75222E'/>
            <Setter Property='BorderThickness' Value='1'/>
            <Setter Property='FontSize' Value='12'/>
            <Setter Property='FontWeight' Value='Bold'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Button}'>
                        <Border Name='Bd' Background='{TemplateBinding Background}'
                                BorderBrush='{TemplateBinding BorderBrush}'
                                BorderThickness='{TemplateBinding BorderThickness}'
                                CornerRadius='6' Padding='14,0'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#5A1E27'/>
                                <Setter TargetName='Bd' Property='BorderBrush' Value='#FF5566'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ComboBox ToggleButton -->
        <ControlTemplate x:Key='ComboBoxToggleButton' TargetType='{x:Type ToggleButton}'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition />
                    <ColumnDefinition Width='28' />
                </Grid.ColumnDefinitions>
                <Border x:Name='Border' Grid.ColumnSpan='2' CornerRadius='6'
                        Background='#121722' BorderBrush='#222A3A' BorderThickness='1' />
                <Path x:Name='Arrow' Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center'
                      Data='M 0 0 L 4 4 L 8 0 Z' Fill='#8B949E' />
            </Grid>
            <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='true'>
                    <Setter TargetName='Border' Property='BorderBrush' Value='#00E5FF' />
                    <Setter TargetName='Arrow' Property='Fill' Value='#00E5FF' />
                </Trigger>
                <Trigger Property='IsChecked' Value='true'>
                    <Setter TargetName='Border' Property='BorderBrush' Value='#00E5FF' />
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>

        <!-- Modern ComboBoxItem Style -->
        <Style TargetType='{x:Type ComboBoxItem}'>
            <Setter Property='SnapsToDevicePixels' Value='true'/>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='Height' Value='30'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='FontSize' Value='12.5'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type ComboBoxItem}'>
                        <Border Name='Border' Padding='10,4' SnapsToDevicePixels='true' Background='Transparent' CornerRadius='4'>
                            <ContentPresenter />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsHighlighted' Value='true'>
                                <Setter TargetName='Border' Property='Background' Value='#1C2536'/>
                                <Setter Property='Foreground' Value='#00E5FF'/>
                            </Trigger>
                            <Trigger Property='IsSelected' Value='true'>
                                <Setter TargetName='Border' Property='Background' Value='#163045'/>
                                <Setter Property='Foreground' Value='#00E5FF'/>
                                <Setter Property='FontWeight' Value='SemiBold'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern Dark ComboBox -->
        <Style TargetType='{x:Type ComboBox}'>
            <Setter Property='Height' Value='34'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='FontSize' Value='12.5'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type ComboBox}'>
                        <Grid>
                            <ToggleButton Name='ToggleButton' Template='{StaticResource ComboBoxToggleButton}'
                                          Grid.Column='2' Focusable='false'
                                          IsChecked='{Binding Path=IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'
                                          ClickMode='Press'/>
                            <ContentPresenter Name='ContentSite' IsHitTestVisible='False'
                                              Content='{TemplateBinding SelectionBoxItem}'
                                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                              ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                                              Margin='12,3,28,3' VerticalAlignment='Center' HorizontalAlignment='Left' />
                            <Popup Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}'
                                   AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
                                <Grid Name='DropDown' SnapsToDevicePixels='True'
                                      MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
                                    <Border x:Name='DropDownBorder' Background='#101520' BorderThickness='1'
                                            BorderBrush='#263248' CornerRadius='6' Margin='0,4,0,0'>
                                        <ScrollViewer Margin='4,6' SnapsToDevicePixels='True'>
                                            <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained' />
                                        </ScrollViewer>
                                    </Border>
                                </Grid>
                            </Popup>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern Slider -->
        <Style TargetType='{x:Type Slider}'>
            <Setter Property='SnapsToDevicePixels' Value='true'/>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Slider}'>
                        <Grid>
                            <Border Name='Track' Height='5' Background='#1E2638' CornerRadius='2.5' VerticalAlignment='Center'/>
                            <Track Name='PART_Track'>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='{x:Type Thumb}'>
                                                <Ellipse Width='16' Height='16' Fill='#00E5FF' Stroke='#0A0D14' StrokeThickness='2.5' Cursor='Hand'/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern CheckBox -->
        <Style TargetType='{x:Type CheckBox}'>
            <Setter Property='Foreground' Value='#E6EDF3'/>
            <Setter Property='FontSize' Value='12.5'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type CheckBox}'>
                        <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                            <Border Name='Box' Width='16' Height='16' CornerRadius='4' Background='#121722' BorderBrush='#263248' BorderThickness='1' Margin='0,0,8,0'>
                                <Path Name='Check' Data='M 2 8 L 6 12 L 14 3' Stroke='#06090F' StrokeThickness='2' Visibility='Collapsed' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                            </Border>
                            <ContentPresenter VerticalAlignment='Center'/>
                        </StackPanel>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsChecked' Value='True'>
                                <Setter TargetName='Box' Property='Background' Value='#00E5FF'/>
                                <Setter TargetName='Box' Property='BorderBrush' Value='#00E5FF'/>
                                <Setter TargetName='Check' Property='Visibility' Value='Visible'/>
                            </Trigger>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='Box' Property='BorderBrush' Value='#00E5FF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height='58'/>
            <RowDefinition Height='*'/>
            <RowDefinition Height='70'/>
        </Grid.RowDefinitions>

        <!-- Top Header Bar -->
        <Border Grid.Row='0' Background='#10141D' BorderBrush='#1B2332' BorderThickness='0,0,0,1' Padding='20,0'>
            <DockPanel LastChildFill='False'>
                <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                    <Border Background='#00E5FF' CornerRadius='5' Width='24' Height='24' Margin='0,0,10,0' VerticalAlignment='Center'>
                        <TextBlock Text='▶' FontSize='12' FontWeight='Bold' Foreground='#0A0D14' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='2,0,0,0'/>
                    </Border>
                    <StackPanel VerticalAlignment='Center'>
                        <StackPanel Orientation='Horizontal'>
                            <TextBlock Text='DJI LUT STUDIO' FontSize='16' FontWeight='Bold' Foreground='#00E5FF' VerticalAlignment='Center'/>
                            <Border Background='#162B3D' BorderBrush='#00E5FF' BorderThickness='1' CornerRadius='4' Padding='6,1' Margin='10,0,0,0' VerticalAlignment='Center'>
                                <TextBlock Text='PRO v2.0' FontSize='10' FontWeight='Bold' Foreground='#00E5FF'/>
                            </Border>
                        </StackPanel>
                        <TextBlock Text='Universal 10-Bit Color Grading &amp; Gyro Telemetry Suite' FontSize='10.5' Foreground='#8B949E' Margin='0,1,0,0'/>
                    </StackPanel>
                </StackPanel>

                <!-- Hardware & Telemetry Chips -->
                <StackPanel Orientation='Horizontal' DockPanel.Dock='Right' VerticalAlignment='Center'>
                    <Border Background='#0F241E' BorderBrush='#00F5A0' BorderThickness='1' CornerRadius='12' Padding='10,4' Margin='0,0,8,0'>
                        <StackPanel Orientation='Horizontal'>
                            <Ellipse Width='7' Height='7' Fill='#00F5A0' Margin='0,0,6,0' VerticalAlignment='Center'/>
                            <TextBlock Name='LblGpuBadge' Text='Probing GPU...' FontSize='11' FontWeight='SemiBold' Foreground='#00F5A0'/>
                        </StackPanel>
                    </Border>

                    <Border Background='#141F30' BorderBrush='#2D4566' BorderThickness='1' CornerRadius='12' Padding='8,4' Margin='0,0,8,0'>
                        <TextBlock Name='LblEngineBadge' Text='⚡ Native Windows Engine' FontSize='11' Foreground='#66B2FF'/>
                    </Border>

                    <Border Background='#151A24' BorderBrush='#222A3A' BorderThickness='1' CornerRadius='12' Padding='8,4'>
                        <TextBlock Text='49 Verified 3D LUTs' FontSize='11' Foreground='#8B949E'/>
                    </Border>
                </StackPanel>
            </DockPanel>
        </Border>

        <!-- Main Workspace (Grid Structured) -->
        <Grid Grid.Row='1' Margin='18,10,18,8'>
            <Grid.RowDefinitions>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <!-- Card 1: Input Footage & Metadata Inspector -->
            <Border Grid.Row='0' Background='#10141E' BorderBrush='#1C2538' BorderThickness='1' CornerRadius='8' Padding='12,8' Margin='0,0,0,8'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,5'>
                        <Border Background='#00E5FF' CornerRadius='3' Width='15' Height='15' Margin='0,0,6,0'>
                            <TextBlock Text='1' FontSize='9.5' FontWeight='Bold' Foreground='#0A0D14' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='INPUT FOOTAGE (DRAG &amp; DROP VIDEO CLIP OR FULL FOLDER)' FontSize='11' FontWeight='Bold' Foreground='#8B949E'/>
                    </StackPanel>
                    <Grid Grid.Row='1'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <TextBox Name='TxtInputPath' Grid.Column='0' Text='D:\dji'/>
                        <Button Name='BtnBrowseFile' Grid.Column='1' Content='Browse Clip...' Style='{StaticResource SubButton}' Margin='8,0,0,0' Height='34' Padding='12,0'/>
                        <Button Name='BtnBrowseFolder' Grid.Column='2' Content='Browse Folder...' Style='{StaticResource SubButton}' Margin='8,0,0,0' Height='34' Padding='12,0'/>
                    </Grid>

                    <!-- Technical Specs Ribbon -->
                    <Border Name='BorderFootageSpecs' Grid.Row='2' Background='#0A0F17' BorderBrush='#1A2433' BorderThickness='1' CornerRadius='4' Padding='8,5' Margin='0,6,0,0' Visibility='Collapsed'>
                        <TextBlock Name='LblFootageSpecs' Text='Technical Specifications' FontSize='11' Foreground='#00E5FF' FontFamily='Consolas' TextWrapping='Wrap' LineHeight='16'/>
                    </Border>
                </Grid>
            </Border>

            <!-- Card 2: Camera Model & 3D Conversion LUT -->
            <Border Grid.Row='1' Background='#10141E' BorderBrush='#1C2538' BorderThickness='1' CornerRadius='8' Padding='12,8' Margin='0,0,0,8'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,5'>
                        <Border Background='#00E5FF' CornerRadius='3' Width='15' Height='15' Margin='0,0,6,0'>
                            <TextBlock Text='2' FontSize='9.5' FontWeight='Bold' Foreground='#0A0D14' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='CAMERA HARDWARE &amp; 3D CONVERSION LUT MATRIX' FontSize='11' FontWeight='Bold' Foreground='#8B949E'/>
                    </StackPanel>
                    <Grid Grid.Row='1'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='12'/>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column='0'>
                            <TextBlock Text='DJI Drone / Camera Model:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,3'/>
                            <ComboBox Name='CmbDevice'/>
                        </StackPanel>
                        <StackPanel Grid.Column='2'>
                            <TextBlock Text='3D Conversion LUT (D-Log / D-Log M to Rec.709):' FontSize='11' Foreground='#8B949E' Margin='0,0,0,3'/>
                            <ComboBox Name='CmbLut'/>
                        </StackPanel>
                        <Button Name='BtnCustomLut' Grid.Column='3' Content='Custom...' Style='{StaticResource SubButton}' Margin='8,17,0,0' Height='34' Padding='10,0' ToolTip='Load any custom third-party 3D .cube file'/>
                    </Grid>
                </Grid>
            </Border>

            <!-- Card 3: Look Style & Settings -->
            <Border Grid.Row='2' Background='#10141E' BorderBrush='#1C2538' BorderThickness='1' CornerRadius='8' Padding='12,8' Margin='0,0,0,8'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,5'>
                        <Border Background='#00E5FF' CornerRadius='3' Width='15' Height='15' Margin='0,0,6,0'>
                            <TextBlock Text='3' FontSize='9.5' FontWeight='Bold' Foreground='#0A0D14' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='CREATIVE LOOK STYLES &amp; ENCODING ENGINE' FontSize='11' FontWeight='Bold' Foreground='#8B949E'/>
                    </StackPanel>
                    <Grid Grid.Row='1'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='1.2*'/>
                            <ColumnDefinition Width='10'/>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='10'/>
                            <ColumnDefinition Width='1*'/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column='0'>
                            <TextBlock Text='Post-LUT Look Preset:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,3'/>
                            <ComboBox Name='CmbLook'/>
                        </StackPanel>
                        <StackPanel Grid.Column='2'>
                            <TextBlock Text='Hardware Encoder Codec:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,3'/>
                            <ComboBox Name='CmbCodec'/>
                        </StackPanel>
                        <StackPanel Grid.Column='4'>
                            <TextBlock Text='Resolution Scaling:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,3'/>
                            <ComboBox Name='CmbScale'/>
                        </StackPanel>
                    </Grid>
                    <Grid Grid.Row='2' Margin='0,8,0,0'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='120'/>
                            <ColumnDefinition Width='65'/>
                            <ColumnDefinition Width='15'/>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='110'/>
                            <ColumnDefinition Width='50'/>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='15'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column='0' Text='Quality (CQ):' FontSize='11' Foreground='#8B949E' VerticalAlignment='Center' Margin='0,0,6,0'/>
                        <Slider Name='SliderCq' Grid.Column='1' Minimum='15' Maximum='28' Value='19' VerticalAlignment='Center'/>
                        <TextBlock Name='LblCqVal' Grid.Column='2' Text='19 (High)' FontSize='11' FontWeight='Bold' Foreground='#00E5FF' VerticalAlignment='Center' Margin='6,0,0,0'/>

                        <TextBlock Grid.Column='4' Text='Look Intensity:' FontSize='11' Foreground='#8B949E' VerticalAlignment='Center' Margin='0,0,6,0'/>
                        <Slider Name='SliderIntensity' Grid.Column='5' Minimum='0' Maximum='150' Value='100' VerticalAlignment='Center'/>
                        <TextBlock Name='LblIntensityVal' Grid.Column='6' Text='100%' FontSize='11' FontWeight='Bold' Foreground='#00E5FF' VerticalAlignment='Center' Margin='6,0,0,0'/>

                        <CheckBox Name='ChkSharpen' Grid.Column='8' Content='Foliage Unsharp Mask' IsChecked='True' VerticalAlignment='Center'/>
                        <CheckBox Name='ChkPreview' Grid.Column='10' Content='Generate Split JPEG' IsChecked='True' VerticalAlignment='Center'/>
                    </Grid>
                    <Grid Grid.Row='3' Margin='0,8,0,0'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <CheckBox Name='ChkGyroflow' Grid.Column='0' Content='⚡ Gyroflow Video Stabilization (Avata / O4 / Action)' ToolTip='Automatically stabilize camera motion using embedded gyro data before grading (Requires RockSteady EIS OFF in camera)' IsChecked='False' VerticalAlignment='Center' Foreground='#00F5A0' FontWeight='SemiBold'/>
                        <Button Name='BtnOpenGyroflow' Grid.Column='2' Content='🎯 Open in Gyroflow GUI' Style='{StaticResource SubButton}' Height='26' Padding='10,0' FontSize='11' ToolTip='Open current video directly in the Gyroflow desktop application for manual horizon lock tuning'/>
                    </Grid>

                    <!-- In-Camera EIS & Gyroflow Diagnostic Banner -->
                    <Border Name='BorderGyroAdvisory' Grid.Row='4' Background='#2A160E' BorderBrush='#C86400' BorderThickness='1' CornerRadius='5' Padding='10,6' Margin='0,8,0,0' Visibility='Collapsed'>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width='Auto'/>
                                <ColumnDefinition Width='*'/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Name='LblGyroAdvisoryIcon' Grid.Column='0' Text='⚠️' FontSize='13' VerticalAlignment='Top' Margin='0,0,8,0'/>
                            <TextBlock Name='LblGyroAdvisoryText' Grid.Column='1' Text='EIS Status' FontSize='11' Foreground='#FFB366' TextWrapping='Wrap' LineHeight='15'/>
                        </Grid>
                    </Border>
                </Grid>
            </Border>

            <!-- Card 4: Preview (Left) & Pipeline Logs (Right) -->
            <Grid Grid.Row='3'>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='1.15*'/>
                    <ColumnDefinition Width='10'/>
                    <ColumnDefinition Width='1*'/>
                </Grid.ColumnDefinitions>

                <!-- Left: Comparison Preview -->
                <Border Grid.Column='0' Background='#10141E' BorderBrush='#1C2538' BorderThickness='1' CornerRadius='8' Padding='10'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                        </Grid.RowDefinitions>
                        <DockPanel Margin='0,0,0,6'>
                            <TextBlock Text='BEFORE / AFTER SPLIT COMPARISON' FontSize='11' FontWeight='Bold' Foreground='#8B949E' VerticalAlignment='Center'/>
                            <Button Name='BtnGenPreview' Content='Generate Snapshot' Style='{StaticResource SubButton}' DockPanel.Dock='Right' Height='24' Padding='8,0' FontSize='11'/>
                        </DockPanel>
                        <Border Name='BorderPreview' Grid.Row='1' Background='#06080E' BorderBrush='#1A2230' BorderThickness='1' CornerRadius='6' ClipToBounds='True'>
                            <Grid>
                                <Image Name='ImgPreview' Stretch='Uniform' Visibility='Collapsed'/>
                                <TextBlock Name='LblPreviewPlaceholder' Text='Grade a clip or click [Generate Snapshot] to inspect the 10-bit color grading preview.'
                                           Foreground='#444D5E' FontSize='11.5' TextWrapping='Wrap' TextAlignment='Center'
                                           VerticalAlignment='Center' HorizontalAlignment='Center' Margin='20'/>
                            </Grid>
                        </Border>
                    </Grid>
                </Border>

                <!-- Right: Pipeline Output Logs -->
                <Border Grid.Column='2' Background='#10141E' BorderBrush='#1C2538' BorderThickness='1' CornerRadius='8' Padding='10'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                        </Grid.RowDefinitions>
                        <TextBlock Text='PIPELINE TELEMETRY &amp; CONSOLE' FontSize='11' FontWeight='Bold' Foreground='#8B949E' Margin='0,0,0,6'/>
                        <Border Grid.Row='1' Background='#06080E' BorderBrush='#1A2230' BorderThickness='1' CornerRadius='6' Padding='8'>
                            <TextBox Name='TxtLog' Background='Transparent' BorderThickness='0' FontFamily='Consolas' FontSize='11' Foreground='#00F5A0' IsReadOnly='True' VerticalScrollBarVisibility='Auto' TextWrapping='Wrap' Text='DJI LUT Studio PRO Ready.'/>
                        </Border>
                    </Grid>
                </Border>
            </Grid>
        </Grid>

        <!-- Bottom Action Bar -->
        <Border Grid.Row='2' Background='#10141D' BorderBrush='#1B2332' BorderThickness='0,1,0,0' Padding='20,10'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='*'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column='0' VerticalAlignment='Center' Margin='0,0,16,0'>
                    <ProgressBar Name='ProgressBar' Height='8' Minimum='0' Maximum='100' Value='0' Background='#1A2230' Foreground='#00E5FF'/>
                    <Grid Margin='0,4,0,0'>
                        <TextBlock Name='LblStatus' Text='Ready to grade.' FontSize='11.5' Foreground='#8B949E'/>
                        <TextBlock Name='LblSpeedEta' Text='' FontSize='11' FontWeight='Bold' Foreground='#00E5FF' HorizontalAlignment='Right'/>
                    </Grid>
                </StackPanel>

                <Button Name='BtnWatcher' Grid.Column='1' Content='⏱ Start Watcher' Style='{StaticResource SubButton}' Height='42' Padding='12,0' Margin='0,0,8,0' ToolTip='Automatically monitor folder for incoming SD card footage'/>
                <Button Name='BtnOpenFolder' Grid.Column='2' Content='📁 Open Output' Style='{StaticResource SubButton}' Height='42' Padding='12,0' Margin='0,0,8,0'/>
                <Button Name='BtnCancelGrade' Grid.Column='3' Content='⏹ Stop' Style='{StaticResource DangerActionBtn}' Height='42' Margin='0,0,8,0' Visibility='Collapsed' ToolTip='Cancel active render'/>
                <Button Name='BtnStartGrade' Grid.Column='4' Content='🚀 APPLY LUT &amp; GRADE' Style='{StaticResource PrimaryActionBtn}' Height='42'/>
            </Grid>
        </Border>
    </Grid>
</Window>";

            mainWindow = (Window)XamlReader.Parse(xaml);

            // Wire up controls
            txtInputPath = (TextBox)mainWindow.FindName("TxtInputPath");
            btnBrowseFile = (Button)mainWindow.FindName("BtnBrowseFile");
            btnBrowseFolder = (Button)mainWindow.FindName("BtnBrowseFolder");
            cmbDevice = (ComboBox)mainWindow.FindName("CmbDevice");
            cmbLut = (ComboBox)mainWindow.FindName("CmbLut");
            cmbLook = (ComboBox)mainWindow.FindName("CmbLook");
            cmbCodec = (ComboBox)mainWindow.FindName("CmbCodec");
            cmbScale = (ComboBox)mainWindow.FindName("CmbScale");
            sliderCq = (Slider)mainWindow.FindName("SliderCq");
            lblCqVal = (TextBlock)mainWindow.FindName("LblCqVal");
            sliderIntensity = (Slider)mainWindow.FindName("SliderIntensity");
            lblIntensityVal = (TextBlock)mainWindow.FindName("LblIntensityVal");
            chkSharpen = (CheckBox)mainWindow.FindName("ChkSharpen");
            chkPreview = (CheckBox)mainWindow.FindName("ChkPreview");
            chkGyroflow = (CheckBox)mainWindow.FindName("ChkGyroflow");
            btnOpenGyroflow = (Button)mainWindow.FindName("BtnOpenGyroflow");
            btnStartGrade = (Button)mainWindow.FindName("BtnStartGrade");
            btnCancelGrade = (Button)mainWindow.FindName("BtnCancelGrade");
            btnOpenFolder = (Button)mainWindow.FindName("BtnOpenFolder");
            btnWatcher = (Button)mainWindow.FindName("BtnWatcher");
            btnGenPreview = (Button)mainWindow.FindName("BtnGenPreview");
            progressBar = (ProgressBar)mainWindow.FindName("ProgressBar");
            lblStatus = (TextBlock)mainWindow.FindName("LblStatus");
            lblSpeedEta = (TextBlock)mainWindow.FindName("LblSpeedEta");
            lblGpuBadge = (TextBlock)mainWindow.FindName("LblGpuBadge");
            lblEngineBadge = (TextBlock)mainWindow.FindName("LblEngineBadge");
            lblFootageSpecs = (TextBlock)mainWindow.FindName("LblFootageSpecs");
            borderFootageSpecs = (Border)mainWindow.FindName("BorderFootageSpecs");
            borderGyroAdvisory = (Border)mainWindow.FindName("BorderGyroAdvisory");
            lblGyroAdvisoryIcon = (TextBlock)mainWindow.FindName("LblGyroAdvisoryIcon");
            lblGyroAdvisoryText = (TextBlock)mainWindow.FindName("LblGyroAdvisoryText");
            txtLog = (TextBox)mainWindow.FindName("TxtLog");
            imgPreview = (Image)mainWindow.FindName("ImgPreview");
            lblPreviewPlaceholder = (TextBlock)mainWindow.FindName("LblPreviewPlaceholder");
            borderPreview = (Border)mainWindow.FindName("BorderPreview");

            // Populate Devices
            foreach (var key in deviceDatabase.Keys)
            {
                cmbDevice.Items.Add(key);
            }
            cmbDevice.SelectedIndex = 0;
            cmbDevice.SelectionChanged += (s, e) => PopulateLutsForSelectedDevice();
            PopulateLutsForSelectedDevice();

            cmbLook.Items.Add("Vibrant Pop (Recommended: Punchy Sky & Emerald Greens)");
            cmbLook.Items.Add("Natural Rec.709 (100% DJI Baseline Color Reference)");
            cmbLook.Items.Add("Cinematic Film (35mm S-Curve, Deep Velvety Shadows)");
            cmbLook.Items.Add("Golden Hour (Warm Amber Sunlight & Sunset Radiance)");
            cmbLook.Items.Add("Teal & Orange (Hollywood Blockbuster Color Contrast)");
            cmbLook.SelectedIndex = 0;

            cmbCodec.Items.Add("HEVC / H.265 (Hardware Accelerated 10-Bit)");
            cmbCodec.Items.Add("H.264 (Hardware Accelerated 8/10-Bit)");
            cmbCodec.Items.Add("Apple ProRes 422 HQ (Broadcast Mastering)");
            cmbCodec.SelectedIndex = 0;

            cmbScale.Items.Add("Source Resolution (Up to 4K 60fps)");
            cmbScale.Items.Add("Scale to 4K UHD (3840x2160)");
            cmbScale.Items.Add("Scale to 2.7K (2704x1520)");
            cmbScale.Items.Add("Scale to 1080p Proxy (Quick Web / Social Share)");
            cmbScale.SelectedIndex = 0;

            sliderCq.ValueChanged += (s, e) => {
                int val = (int)sliderCq.Value;
                lblCqVal.Text = val + (val <= 19 ? " (High)" : " (Standard)");
            };

            sliderIntensity.ValueChanged += (s, e) => {
                int val = (int)sliderIntensity.Value;
                lblIntensityVal.Text = val + "%";
            };

            // Custom LUT Button
            Button btnCustomLut = (Button)mainWindow.FindName("BtnCustomLut");
            btnCustomLut.Click += (s, e) => {
                var dlg = new System.Windows.Forms.OpenFileDialog { Filter = "3D LUT Files (*.cube)|*.cube|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    cmbLut.Items.Add(new LutEntry("custom", "Custom: " + Path.GetFileName(dlg.FileName), dlg.FileName, true));
                    cmbLut.SelectedIndex = cmbLut.Items.Count - 1;
                }
            };

            // Browse File & Folder
            btnBrowseFile.Click += (s, e) => {
                var dlg = new System.Windows.Forms.OpenFileDialog { Filter = "Video Files (*.mp4;*.mov;*.mkv)|*.mp4;*.mov;*.mkv|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    txtInputPath.Text = dlg.FileName;
                    AutoSelectDeviceForPath(dlg.FileName);
                    InspectVideoMetadataAsync(dlg.FileName);
                }
            };

            btnBrowseFolder.Click += (s, e) => {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    txtInputPath.Text = dlg.SelectedPath;
                    InspectFolderAsync(dlg.SelectedPath);
                }
            };

            txtInputPath.TextChanged += (s, e) => {
                string p = txtInputPath.Text.Trim('"', '\'');
                if (File.Exists(p)) InspectVideoMetadataAsync(p);
                else if (Directory.Exists(p)) InspectFolderAsync(p);
                else {
                    isEisRockSteadyActive = false;
                    isEisHorizonSteadyActive = false;
                    if (borderFootageSpecs != null) borderFootageSpecs.Visibility = Visibility.Collapsed;
                    if (borderGyroAdvisory != null) borderGyroAdvisory.Visibility = Visibility.Collapsed;
                    if (chkGyroflow != null) {
                        chkGyroflow.IsEnabled = true;
                        chkGyroflow.Content = "⚡ Gyroflow Video Stabilization (Avata / O4 / Action)";
                        chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                        chkGyroflow.ToolTip = "Automatically stabilize camera motion using embedded gyro data before grading (Requires RockSteady EIS OFF in camera)";
                    }
                }
            };

            // Drag and Drop
            mainWindow.Drop += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0) {
                        txtInputPath.Text = files[0];
                        AutoSelectDeviceForPath(files[0]);
                        if (File.Exists(files[0])) InspectVideoMetadataAsync(files[0]);
                        else if (Directory.Exists(files[0])) InspectFolderAsync(files[0]);
                    }
                }
            };

            // Open in Gyroflow
            btnOpenGyroflow.Click += (s, e) => {
                string path = txtInputPath.Text.Trim('"', '\'');
                string gyroExe = FindGyroflowExePath();
                if (string.IsNullOrEmpty(gyroExe) || !File.Exists(gyroExe))
                {
                    var res = MessageBox.Show(
                        "Gyroflow was not detected on your system.\n\n" +
                        "Gyroflow is open-source video stabilization software for DJI Avata, O3/O4 Air Units, and Action cameras.\n\n" +
                        "Would you like to install Gyroflow automatically via Windows Package Manager (winget)?",
                        "Install Gyroflow", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        txtLog.AppendText("\n[GYROFLOW] Launching winget installer...\n");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c winget install --id Gyroflow.Gyroflow --accept-source-agreements --accept-package-agreements",
                            UseShellExecute = true
                        });
                    }
                    return;
                }

                if (isEisRockSteadyActive || isEisHorizonSteadyActive)
                {
                    string modeName = isEisRockSteadyActive ? "RockSteady" : "HorizonSteady";
                    var warnRes = MessageBox.Show(
                        string.Format("Notice: This video was recorded with In-Camera {0} EIS turned ON.\n\n" +
                                      "Because the camera already stabilized this clip electronically, DJI camera firmware suppresses raw gyro motion telemetry in the file.\n\n" +
                                      "Gyroflow GUI will launch, but it will likely report that no motion/gyro data was found.\n\n" +
                                      "Do you still want to open it in Gyroflow GUI?", modeName),
                        "In-Camera EIS Advisory", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (warnRes != MessageBoxResult.Yes) return;
                }

                try
                {
                    string args = "";
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        args = string.Format("--open \"{0}\"", path);
                    }
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = gyroExe,
                        Arguments = args,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    if (!string.IsNullOrEmpty(args))
                        txtLog.AppendText(string.Format("\n[GYROFLOW] Opened Gyroflow Desktop GUI with: {0}\n", Path.GetFileName(path)));
                    else
                        txtLog.AppendText("\n[GYROFLOW] Opened Gyroflow Desktop GUI\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to launch Gyroflow: " + ex.Message, "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Action Buttons
            btnStartGrade.Click += (s, e) => StartGradingProcess();
            btnCancelGrade.Click += (s, e) => CancelGradingProcess();
            btnGenPreview.Click += (s, e) => RunPreviewGeneration();
            btnOpenFolder.Click += (s, e) => OpenGradedFolder();
            btnWatcher.Click += (s, e) => ToggleWatcher();

            if (!string.IsNullOrEmpty(initialFile)) {
                txtInputPath.Text = initialFile.Trim('"', '\'');
                AutoSelectDeviceForPath(initialFile);
                if (File.Exists(initialFile)) InspectVideoMetadataAsync(initialFile);
            }

            // Probe hardware GPU in background
            ThreadPool.QueueUserWorkItem((state) =>
            {
                string ffmpeg = FindFfmpegExe();
                DetectHardwareGpu(ffmpeg);
                mainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    if (lblGpuBadge != null)
                    {
                        lblGpuBadge.Text = detectedGpuBadge;
                    }
                    if (lblEngineBadge != null)
                    {
                        lblEngineBadge.Text = !string.IsNullOrEmpty(ffmpeg) ? "⚡ Native Windows Engine" : "WSL Fallback Engine";
                    }
                    txtLog.AppendText("\n[HARDWARE ACCELERATION] " + detectedGpuName + "\n");
                    if (!string.IsNullOrEmpty(ffmpeg))
                    {
                        txtLog.AppendText("[FFMPEG ENGINE] Native Windows: " + ffmpeg + "\n");
                    }
                }));
            });
        }

        private static void InitDatabase()
        {
            deviceDatabase = new Dictionary<string, List<LutEntry>>();

            deviceDatabase["DJI O4 Pro / Air Unit"] = new List<LutEntry> {
                new LutEntry("o4_dlogm_rec709", "Official O4 Air Unit D-Log M to Rec.709 V1", @"drones\DJI_O4_Air_Unit_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Avata 2"] = new List<LutEntry> {
                new LutEntry("avata2_dlogm_rec709", "Official Avata 2 D-Log M to Rec.709 V1", @"drones\DJI_Avata_2_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Avata 360"] = new List<LutEntry> {
                new LutEntry("avata360_dlogm_rec709", "Official Avata 360 D-Log M to Rec.709", @"drones\DJI_Avata_360_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Mavic 3 / Pro / Classic / Cine"] = new List<LutEntry> {
                new LutEntry("m3_dlog_rec709_std", "Official Mavic 3 D-Log to Rec.709 Standard", @"cinema\zenmuse_x7\D-Log to Rec709.cube", true),
                new LutEntry("m3_dlog_rec709_vivid", "Official Mavic 3 D-Log to Rec.709 (Vivid)", @"drones\DJI_Mavic_3_D-Log_to_Rec709_Vivid.cube"),
                new LutEntry("m3_dlogm_rec709", "Official Mavic 3 D-Log M to Rec.709", @"drones\DJI_Mavic_3_D-Log_M_to_Rec709.cube")
            };
            deviceDatabase["DJI Mavic 4 Pro"] = new List<LutEntry> {
                new LutEntry("m4p_dlog_std", "Official Mavic 4 Pro D-Log to Rec.709 Standard", @"drones\DJI_Mavic_4_Pro_D-Log_to_Rec709_Standard.cube", true),
                new LutEntry("m4p_dlog_vivid", "Official Mavic 4 Pro D-Log to Rec.709 (Vivid)", @"drones\DJI_Mavic_4_Pro_D-Log_to_Rec709_Vivid.cube"),
                new LutEntry("m4p_dlogm", "Official Mavic 4 Pro D-Log M to Rec.709", @"drones\DJI_Mavic_4_Pro_D-Log_M_to_Rec709.cube")
            };
            deviceDatabase["DJI Air 3 / Air 3S"] = new List<LutEntry> {
                new LutEntry("air3_dlogm", "Official Air 3 D-Log M to Rec.709 V1", @"drones\DJI_Air_3_D-Log_M_to_Rec709.cube", true),
                new LutEntry("air3s_dlogm", "Official Air 3S D-Log M to Rec.709 V1", @"drones\DJI_Air_3S_D-Log_M_to_Rec709.cube")
            };
            deviceDatabase["DJI Mini 4 Pro / Mini 5 Pro"] = new List<LutEntry> {
                new LutEntry("mini4_dlogm", "Official Mini 4 Pro D-Log M to Rec.709 V1", @"drones\DJI_Mini_4_Pro_D-Log_M_to_Rec709.cube", true),
                new LutEntry("mini5_dlogm", "Official Mini 5 Pro D-Log M to Rec.709", @"drones\DJI_Mini_5_Pro_D-Log_M_to_Rec709.cube")
            };
            deviceDatabase["DJI Osmo Pocket 3"] = new List<LutEntry> {
                new LutEntry("op3_dlogm", "Official Pocket 3 D-Log M to Rec.709 V1", @"action_pocket\DJI_Osmo_Pocket_3_D-Log_M_to_Rec709.cube", true),
                new LutEntry("op3_spring", "Master Look: Spring Pro (Fresh Greens)", @"creative\pocket_3\DJI OSMO Pocket 3 Spring Pro.cube"),
                new LutEntry("op3_summer", "Master Look: Summer Pro (Deep Cyan & Sun-Kissed)", @"creative\pocket_3\DJI OSMO Pocket 3 Summer Pro.cube"),
                new LutEntry("op3_autumn", "Master Look: Autumn Pro (Warm Terracotta)", @"creative\pocket_3\DJI OSMO Pocket 3 Autumn Pro.cube"),
                new LutEntry("op3_winter", "Master Look: Winter Pro (Cool High-Contrast)", @"creative\pocket_3\DJI OSMO Pocket 3 Winter Pro.cube")
            };
            deviceDatabase["DJI Osmo Pocket 4 / 4 Pro"] = new List<LutEntry> {
                new LutEntry("op4_dlog_std", "Official Pocket 4 D-Log to Rec.709 Standard", @"action_pocket\DJI_Osmo_Pocket_4_D-Log_to_Rec709_V2.cube", true),
                new LutEntry("op4_dlog_vivid", "Official Pocket 4 D-Log to Rec.709 (Vivid)", @"action_pocket\DJI_Osmo_Pocket_4_D-Log_to_Rec709_Vivid_V2.cube"),
                new LutEntry("op4p_dlog_v2", "Official Pocket 4P D-Log to Rec.709 V2", @"action_pocket\DJI_Osmo_Pocket_4P_D-Log_to_Rec709_V2.cube"),
                new LutEntry("op4p_dlog2_std", "Official Pocket 4P D-Log2 to Rec.709", @"action_pocket\DJI_Osmo_Pocket_4P_D-Log2_to_Rec709.cube"),
                new LutEntry("op4p_dlog2_vivid", "Official Pocket 4P D-Log2 to Rec.709 (Vivid)", @"action_pocket\DJI_Osmo_Pocket_4P_D-Log2_to_Rec709_Vivid.cube")
            };
            deviceDatabase["DJI Osmo Action 4"] = new List<LutEntry> {
                new LutEntry("oa4_dlogm", "Official Action 4 D-Log M to Rec.709 V1", @"action_pocket\DJI_Osmo_Action_4_D-Log_M_to_Rec709.cube", true),
                new LutEntry("oa4_ice", "Action Look: Ice Pro (Snow Sports)", @"creative\action_4\DJI OSMO Action 4 Ice Pro.cube"),
                new LutEntry("oa4_nature", "Action Look: Nature Pro (Warm Sunlight)", @"creative\action_4\DJI OSMO Action 4 Nature Pro.cube"),
                new LutEntry("oa4_forest", "Action Look: Forest Pro (Deep Moss & Trail)", @"creative\action_4\DJI OSMO Action 4 Forest Pro.cube")
            };
            deviceDatabase["DJI Osmo Action 5 Pro"] = new List<LutEntry> {
                new LutEntry("oa5p_dlogm", "Official Action 5 Pro D-Log M to Rec.709 V1", @"action_pocket\DJI_Osmo_Action_5_Pro_D-Log_M_to_Rec709.cube", true),
                new LutEntry("oa5p_mei", "Master Look: Mei / Plum (Romantic Magenta)", @"creative\action_5_pro\DJI OSMO Action 5 Pro Mei.cube"),
                new LutEntry("oa5p_ju", "Master Look: Ju / Chrysanthemum (Golden Sunset)", @"creative\action_5_pro\DJI OSMO Action 5 Pro Ju.cube"),
                new LutEntry("oa5p_zhu", "Master Look: Zhu / Bamboo (Vibrant Green)", @"creative\action_5_pro\DJI OSMO Action 5 Pro Zhu.cube"),
                new LutEntry("oa5p_lan", "Master Look: Lan / Orchid (Oceanic Teal)", @"creative\action_5_pro\DJI OSMO Action 5 Pro Lan.cube")
            };
            deviceDatabase["DJI Osmo Action 6"] = new List<LutEntry> {
                new LutEntry("oa6_dlogm", "Official Action 6 D-Log M to Rec.709", @"action_pocket\DJI_Osmo_Action_6_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Lito / Lito X1"] = new List<LutEntry> {
                new LutEntry("lito_x1_dlogm", "Official DJI Lito X1 D-Log M to Rec.709", @"action_pocket\DJI_Lito_X1_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Osmo Nano"] = new List<LutEntry> {
                new LutEntry("osmo_nano_dlogm", "Official DJI Osmo Nano D-Log M to Rec.709", @"action_pocket\DJI_Osmo_Nano_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Osmo 360"] = new List<LutEntry> {
                new LutEntry("osmo_360_dlogm", "Official DJI Osmo 360 Panoramic D-Log M to Rec.709", @"action_pocket\DJI_Osmo_360_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Flip"] = new List<LutEntry> {
                new LutEntry("flip_dlogm_rec709", "Official DJI Flip D-Log M to Rec.709", @"drones\DJI_Flip_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Neo / Generic D-Log M"] = new List<LutEntry> {
                new LutEntry("generic_dlogm_rec709", "Universal DJI Neo / D-Log M to Rec.709", @"generic\DJI_Generic_D-Log_M_to_Rec709.cube", true)
            };
            deviceDatabase["DJI Zenmuse X7 / Ronin 4D / Inspire 3"] = new List<LutEntry> {
                new LutEntry("x7_dlog_rec709", "Official Cinema D-Log to Rec.709", @"cinema\zenmuse_x7\D-Log to Rec709.cube", true),
                new LutEntry("x7_linear_dlog", "Official Linear RAW to D-Log", @"cinema\zenmuse_x7_linear\Linear to D-Log.cube")
            };
            deviceDatabase["DJI Phantom 4 / Phantom 4 Pro"] = new List<LutEntry> {
                new LutEntry("p4_dlog_rec709", "Official Phantom 4 D-Log to Rec.709", @"cinema\phantom_4\DJI_Phantom4_DLOG2Rec709.cube", true),
                new LutEntry("p4_dlog_srgb_ref", "Official Phantom 4 D-Log to sRGB Reference", @"cinema\phantom_4\DJI_Phantom4_DLOG2sRGB.cube"),
                new LutEntry("p4_dlog_srgb", "Official Phantom 4 D-Log to sRGB Improv", @"cinema\phantom_4\DJI_Phantom4_DLOG2sRGB_Improv.cube")
            };
            deviceDatabase["Universal 3D Creative Looks"] = new List<LutEntry> {
                new LutEntry("look_vibrant", "Master Look: Vibrant Pop (Punchy Sky & Greens)", @"looks\Look_Vibrant_Pop.cube", true),
                new LutEntry("look_cinematic", "Master Look: Cinematic 35mm (Filmic S-Curve)", @"looks\Look_Cinematic_Film.cube"),
                new LutEntry("look_golden", "Master Look: Golden Hour (Warm Amber Sunlight)", @"looks\Look_Golden_Hour.cube"),
                new LutEntry("look_teal_orange", "Master Look: Teal & Orange (Hollywood Contrast)", @"looks\Look_Teal_Orange.cube"),
                new LutEntry("look_commercial", "Master Look: Clean Commercial (High-Key Crisp)", @"looks\Look_Clean_Commercial.cube"),
                new LutEntry("look_noir", "Master Look: Moody Noir (Dramatic Monochrome)", @"looks\Look_Moody_Noir_BW.cube"),
                new LutEntry("look_dcinelike", "Master Look: D-Cinelike Natural Booster", @"looks\Look_DCinelike_Natural_Boost.cube")
            };
        }

        private static void PopulateLutsForSelectedDevice()
        {
            cmbLut.Items.Clear();
            string sel = cmbDevice.SelectedItem as string;
            if (sel != null && deviceDatabase.ContainsKey(sel))
            {
                foreach (var lut in deviceDatabase[sel])
                {
                    cmbLut.Items.Add(lut);
                }
            }
            if (cmbLut.Items.Count > 0)
            {
                cmbLut.SelectedIndex = 0;
            }
        }

        private static void AutoSelectDeviceForPath(string path, string encoderTag = "")
        {
            if (string.IsNullOrEmpty(path)) return;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            string encUpper = (encoderTag ?? "").ToUpperInvariant();

            if (nameWithoutExt.Contains("LITO") || encUpper.Contains("LITO"))
                cmbDevice.SelectedItem = "DJI Lito / Lito X1";
            else if (nameWithoutExt.Contains("NANO") || encUpper.Contains("NANO"))
                cmbDevice.SelectedItem = "DJI Osmo Nano";
            else if (nameWithoutExt.Contains("OSMO360") || nameWithoutExt.Contains("OSMO_360") || encUpper.Contains("OSMO 360") || encUpper.Contains("OSMO360"))
                cmbDevice.SelectedItem = "DJI Osmo 360";
            else if (nameWithoutExt.Contains("FLIP") || encUpper.Contains("FLIP"))
                cmbDevice.SelectedItem = "DJI Flip";
            else if (nameWithoutExt.Contains("NEO") || encUpper.Contains("NEO"))
                cmbDevice.SelectedItem = "DJI Neo / Generic D-Log M";
            else if (nameWithoutExt.Contains("AVATA2") || nameWithoutExt.Contains("AVATA_2") || encUpper.Contains("AVATA 2") || encUpper.Contains("AVATA2"))
                cmbDevice.SelectedItem = "DJI Avata 2";
            else if (nameWithoutExt.Contains("AVATA360") || nameWithoutExt.Contains("AVATA_360"))
                cmbDevice.SelectedItem = "DJI Avata 360";
            else if (nameWithoutExt.Contains("AVATA") || encUpper.Contains("AVATA"))
                cmbDevice.SelectedItem = "DJI Avata 2";
            else if (nameWithoutExt.Contains("MAVIC3") || nameWithoutExt.Contains("M3") || encUpper.Contains("MAVIC 3") || encUpper.Contains("M3"))
                cmbDevice.SelectedItem = "DJI Mavic 3 / Pro / Classic / Cine";
            else if (nameWithoutExt.Contains("MAVIC4") || nameWithoutExt.Contains("M4") || encUpper.Contains("MAVIC 4") || encUpper.Contains("M4"))
                cmbDevice.SelectedItem = "DJI Mavic 4 Pro";
            else if (nameWithoutExt.Contains("POCKET3") || nameWithoutExt.Contains("OP3") || encUpper.Contains("POCKET 3") || encUpper.Contains("POCKET3") || encUpper.Contains("OP3"))
                cmbDevice.SelectedItem = "DJI Osmo Pocket 3";
            else if (nameWithoutExt.Contains("POCKET4") || nameWithoutExt.Contains("OP4") || encUpper.Contains("POCKET 4") || encUpper.Contains("POCKET4") || encUpper.Contains("OP4"))
                cmbDevice.SelectedItem = "DJI Osmo Pocket 4 / 4 Pro";
            else if (nameWithoutExt.Contains("ACTION5") || nameWithoutExt.Contains("OA5") || encUpper.Contains("ACTION 5") || encUpper.Contains("ACTION5") || encUpper.Contains("OA5"))
                cmbDevice.SelectedItem = "DJI Osmo Action 5 Pro";
            else if (nameWithoutExt.Contains("ACTION4") || nameWithoutExt.Contains("OA4") || encUpper.Contains("ACTION 4") || encUpper.Contains("ACTION4") || encUpper.Contains("OA4"))
                cmbDevice.SelectedItem = "DJI Osmo Action 4";
            else if (nameWithoutExt.Contains("ACTION6") || nameWithoutExt.Contains("OA6") || encUpper.Contains("ACTION 6") || encUpper.Contains("ACTION6") || encUpper.Contains("OA6"))
                cmbDevice.SelectedItem = "DJI Osmo Action 6";
            else if (nameWithoutExt.Contains("AIR3") || encUpper.Contains("AIR 3") || encUpper.Contains("AIR3") || encUpper.Contains("AIR3S"))
                cmbDevice.SelectedItem = "DJI Air 3 / Air 3S";
            else if (nameWithoutExt.Contains("MINI4") || nameWithoutExt.Contains("MINI5") || encUpper.Contains("MINI 4") || encUpper.Contains("MINI 5"))
                cmbDevice.SelectedItem = "DJI Mini 4 Pro / Mini 5 Pro";
            else if (nameWithoutExt.Contains("X7") || nameWithoutExt.Contains("RONIN") || nameWithoutExt.Contains("INSPIRE") || encUpper.Contains("ZENMUSE") || encUpper.Contains("RONIN") || encUpper.Contains("INSPIRE"))
                cmbDevice.SelectedItem = "DJI Zenmuse X7 / Ronin 4D / Inspire 3";
            else if (nameWithoutExt.Contains("PHANTOM") || nameWithoutExt.StartsWith("P4") || nameWithoutExt.Contains("P4PRO") || nameWithoutExt.Contains("P4P") || encUpper.Contains("PHANTOM"))
                cmbDevice.SelectedItem = "DJI Phantom 4 / Phantom 4 Pro";
            else if (encUpper.Contains("O4P") || encUpper.Contains("O4") || encUpper.Contains("O3"))
                cmbDevice.SelectedItem = "DJI O4 Pro / Air Unit";
            else
                cmbDevice.SelectedItem = "DJI O4 Pro / Air Unit";
        }

        private static void InspectVideoMetadataAsync(string videoPath)
        {
            if (borderFootageSpecs == null || lblFootageSpecs == null) return;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string ffprobe = FindFfprobeExe();
                    if (string.IsNullOrEmpty(ffprobe) || !File.Exists(ffprobe)) return;

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ffprobe,
                        Arguments = string.Format("-v error -show_entries stream=width,height,r_frame_rate,pix_fmt,bit_rate:stream_tags=handler_name:format=duration,bit_rate:format_tags=comment,encoder -of default=noprint_wrappers=1 \"{0}\"", videoPath),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string outStr = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(3500);

                        Match mW = Regex.Match(outStr, @"width=(\d+)");
                        Match mH = Regex.Match(outStr, @"height=(\d+)");
                        Match mFps = Regex.Match(outStr, @"r_frame_rate=(\d+)/(\d+)");
                        Match mPix = Regex.Match(outStr, @"pix_fmt=([^\r\n]+)");
                        Match mDur = Regex.Match(outStr, @"duration=([0-9.]+)");
                        Match mBr = Regex.Match(outStr, @"bit_rate=(\d+)");
                        Match mEnc = Regex.Match(outStr, @"TAG:encoder=([^\r\n]+)");
                        Match mCom = Regex.Match(outStr, @"TAG:comment=([^\r\n]+)");

                        string w = mW.Success ? mW.Groups[1].Value : "";
                        string h = mH.Success ? mH.Groups[1].Value : "";
                        double fps = 0;
                        if (mFps.Success)
                        {
                            double num = double.Parse(mFps.Groups[1].Value);
                            double den = double.Parse(mFps.Groups[2].Value);
                            if (den > 0) fps = num / den;
                        }
                        string pix = mPix.Success ? mPix.Groups[1].Value.Trim() : "";
                        double dur = mDur.Success ? double.Parse(mDur.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
                        long br = mBr.Success ? long.Parse(mBr.Groups[1].Value) : 0;
                        double mbps = (br > 0) ? (br / 1000000.0) : 0;

                        string encoder = mEnc.Success ? mEnc.Groups[1].Value.Trim() : "";
                        string comment = mCom.Success ? mCom.Groups[1].Value.Trim() : "";

                        bool hasDjiMeta = outStr.Contains("TAG:handler_name=DJI meta") || outStr.Contains("djmd");
                        bool isRockSteady = comment.IndexOf("EIS:RS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            comment.IndexOf("RockSteady", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isHorizonSteady = comment.IndexOf("EIS:HS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               comment.IndexOf("HorizonSteady", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isEisOff = comment.IndexOf("EIS:OFF", StringComparison.OrdinalIgnoreCase) >= 0;

                        isEisRockSteadyActive = isRockSteady;
                        isEisHorizonSteadyActive = isHorizonSteady;

                        string resTag = (w == "3840" && h == "2160") ? "4K UHD" : (w == "2704" ? "2.7K" : (w == "1920" ? "1080p" : ""));
                        string resStr = string.Format("{0}x{1}{2}", w, h, !string.IsNullOrEmpty(resTag) ? (" (" + resTag + ")") : "");
                        string fpsStr = (fps > 0) ? string.Format("{0:F2} fps", fps) : "";
                        string durStr = (dur > 0) ? FormatSeconds(dur) : "";
                        string brStr = (mbps > 0) ? string.Format("{0:F0} Mbps", mbps) : "";

                        string eisBadge = "";
                        if (isRockSteady) eisBadge = "  •  🛡 In-Camera EIS: RockSteady Active";
                        else if (isHorizonSteady) eisBadge = "  •  🛡 In-Camera EIS: HorizonSteady Active";
                        else if (isEisOff) eisBadge = "  •  ⚡ Gyro Telemetry: Active (EIS: OFF)";

                        string badgeText = string.Format("📹 {0}  •  ⏱ {1}  •  ⚡ {2}  •  🎨 {3}  •  📶 {4}{5}",
                            resStr, durStr, fpsStr, pix, brStr, eisBadge);

                        mainWindow.Dispatcher.Invoke(new Action(() =>
                        {
                            AutoSelectDeviceForPath(videoPath, encoder);

                            lblFootageSpecs.Text = badgeText;
                            borderFootageSpecs.Visibility = Visibility.Visible;

                            if (borderGyroAdvisory != null && lblGyroAdvisoryText != null && lblGyroAdvisoryIcon != null)
                            {
                                if (isRockSteady || isHorizonSteady)
                                {
                                    string eisName = isRockSteady ? "RockSteady" : "HorizonSteady";
                                    borderGyroAdvisory.Background = new SolidColorBrush(Color.FromRgb(42, 22, 14));
                                    borderGyroAdvisory.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 100, 0));
                                    lblGyroAdvisoryIcon.Text = "⚠️";
                                    lblGyroAdvisoryText.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 102));
                                    lblGyroAdvisoryText.Text = string.Format(
                                        "IN-CAMERA {0} EIS ACTIVE: The camera electronically stabilized this clip during recording, which suppresses/zeroes raw gyro motion telemetry in the video file. Gyroflow CANNOT stabilize this footage.\n" +
                                        "Tip: To stabilize with Gyroflow in post, turn RockSteady EIS to OFF in your DJI camera settings before recording. You can still grade this video directly with 3D LUTs below.",
                                        eisName.ToUpperInvariant());
                                    borderGyroAdvisory.Visibility = Visibility.Visible;

                                    if (chkGyroflow != null)
                                    {
                                        chkGyroflow.IsChecked = false;
                                        chkGyroflow.IsEnabled = false;
                                        chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 70));
                                        chkGyroflow.Content = string.Format("⚡ Gyroflow Video Stabilization (Disabled: In-Camera {0} EIS Active)", eisName);
                                        chkGyroflow.ToolTip = string.Format("In-camera {0} EIS was turned ON during shooting. DJI camera firmware disables raw gyro telemetry.", eisName);
                                    }

                                    txtLog.AppendText(string.Format("\n[GYRO TELEMETRY ADVISORY] In-camera {0} EIS detected in file headers ({1}). Raw gyro motion telemetry is disabled by camera firmware. Gyroflow video stabilization is unavailable for this clip. To enable Gyroflow on future flights, set EIS to OFF in camera settings.\n", eisName, comment));
                                }
                                else if (isEisOff)
                                {
                                    borderGyroAdvisory.Background = new SolidColorBrush(Color.FromRgb(10, 36, 23));
                                    borderGyroAdvisory.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 168, 107));
                                    lblGyroAdvisoryIcon.Text = "✔";
                                    lblGyroAdvisoryText.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                                    lblGyroAdvisoryText.Text = "GYRO TELEMETRY VERIFIED (EIS: OFF): Camera recorded raw motion sensor telemetry (djmd). Gyroflow video stabilization is fully supported!";
                                    borderGyroAdvisory.Visibility = Visibility.Visible;

                                    if (chkGyroflow != null)
                                    {
                                        chkGyroflow.IsEnabled = true;
                                        chkGyroflow.IsChecked = true;
                                        chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                                        chkGyroflow.Content = "⚡ Gyroflow Video Stabilization (Avata / O4 / Action) — Gyro Ready";
                                        chkGyroflow.ToolTip = "Raw gyro motion telemetry detected and ready for Gyroflow stabilization.";
                                    }

                                    txtLog.AppendText("\n[GYRO TELEMETRY] In-camera EIS: OFF. Raw gyro telemetry track (djmd) verified. Ready for Gyroflow stabilization.\n");
                                }
                                else
                                {
                                    if (hasDjiMeta)
                                    {
                                        borderGyroAdvisory.Background = new SolidColorBrush(Color.FromRgb(10, 36, 23));
                                        borderGyroAdvisory.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 168, 107));
                                        lblGyroAdvisoryIcon.Text = "ℹ";
                                        lblGyroAdvisoryText.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                                        lblGyroAdvisoryText.Text = "DJI Telemetry stream detected (djmd). Gyroflow stabilization available.";
                                        borderGyroAdvisory.Visibility = Visibility.Visible;

                                        if (chkGyroflow != null)
                                        {
                                            chkGyroflow.IsEnabled = true;
                                            chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                                            chkGyroflow.Content = "⚡ Gyroflow Video Stabilization (Avata / O4 / Action)";
                                        }
                                    }
                                    else
                                    {
                                        borderGyroAdvisory.Visibility = Visibility.Collapsed;
                                        if (chkGyroflow != null)
                                        {
                                            chkGyroflow.IsEnabled = true;
                                            chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
                                            chkGyroflow.Content = "⚡ Gyroflow Video Stabilization (Avata / O4 / Action)";
                                        }
                                    }
                                }
                            }
                        }));
                    }
                }
                catch {}
            });
        }

        private static void InspectFolderAsync(string folderPath)
        {
            isEisRockSteadyActive = false;
            isEisHorizonSteadyActive = false;
            if (borderGyroAdvisory != null) borderGyroAdvisory.Visibility = Visibility.Collapsed;
            if (chkGyroflow != null)
            {
                chkGyroflow.IsEnabled = true;
                chkGyroflow.Content = "⚡ Gyroflow Video Stabilization (Avata / O4 / Action)";
                chkGyroflow.Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 160));
            }
            if (borderFootageSpecs == null || lblFootageSpecs == null) return;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string[] files = Directory.GetFiles(folderPath);
                    int count = 0;
                    foreach (var f in files)
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if ((ext == ".mp4" || ext == ".mov" || ext == ".mkv") && !f.Contains("_graded") && !f.Contains("_stabilized"))
                        {
                            count++;
                        }
                    }
                    mainWindow.Dispatcher.Invoke(new Action(() =>
                    {
                        lblFootageSpecs.Text = string.Format("📁 BATCH FOLDER: {0} compatible video clip(s) ready for color grading", count);
                        borderFootageSpecs.Visibility = Visibility.Visible;
                    }));
                }
                catch {}
            });
        }

        private static string FindFfmpegExe()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string local = Path.Combine(baseDir, "bin", "ffmpeg.exe");
                if (File.Exists(local)) return local;

                local = Path.Combine(baseDir, "ffmpeg.exe");
                if (File.Exists(local)) return local;

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string wingetDir = Path.Combine(localApp, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(wingetDir))
                {
                    foreach (var d in Directory.GetDirectories(wingetDir, "*FFmpeg*"))
                    {
                        string bin = Path.Combine(d, "ffmpeg-9.0.1-essentials_build", "bin", "ffmpeg.exe");
                        if (File.Exists(bin)) return bin;
                        foreach (var sub in Directory.GetDirectories(d, "bin", SearchOption.AllDirectories))
                        {
                            string target = Path.Combine(sub, "ffmpeg.exe");
                            if (File.Exists(target)) return target;
                        }
                    }
                }

                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (var p in pathEnv.Split(';'))
                    {
                        try
                        {
                            string candidate = Path.Combine(p.Trim(), "ffmpeg.exe");
                            if (File.Exists(candidate)) return candidate;
                        }
                        catch {}
                    }
                }
            }
            catch {}
            return null;
        }

        private static string FindFfprobeExe()
        {
            try
            {
                string ffmpeg = FindFfmpegExe();
                if (!string.IsNullOrEmpty(ffmpeg))
                {
                    string dir = Path.GetDirectoryName(ffmpeg);
                    string probe = Path.Combine(dir, "ffprobe.exe");
                    if (File.Exists(probe)) return probe;
                }
            }
            catch {}
            return null;
        }

        private static string FindGyroflowExePath()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string wingetDir = Path.Combine(localApp, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(wingetDir))
                {
                    foreach (var d in Directory.GetDirectories(wingetDir, "Gyroflow.Gyroflow*"))
                    {
                        string target = Path.Combine(d, "Gyroflow.exe");
                        if (File.Exists(target)) return target;
                    }
                }

                string directLocal = Path.Combine(localApp, "Gyroflow", "Gyroflow.exe");
                if (File.Exists(directLocal)) return directLocal;

                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progLocal = Path.Combine(progFiles, "Gyroflow", "Gyroflow.exe");
                if (File.Exists(progLocal)) return progLocal;

                string prog86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string prog86Local = Path.Combine(prog86, "Gyroflow", "Gyroflow.exe");
                if (File.Exists(prog86Local)) return prog86Local;
            }
            catch {}
            return null;
        }

        private static void DetectHardwareGpu(string ffmpegExe)
        {
            if (string.IsNullOrEmpty(ffmpegExe) || !File.Exists(ffmpegExe))
            {
                detectedGpu = GpuVendor.Cpu;
                detectedGpuBadge = "CPU Multi-Core (libx265)";
                detectedGpuName = "CPU Software Encoding (libx265)";
                return;
            }

            string[] encoders = new string[] { "hevc_nvenc", "hevc_amf", "hevc_qsv" };
            GpuVendor[] vendors = new GpuVendor[] { GpuVendor.Nvidia, GpuVendor.Amd, GpuVendor.Intel };
            string[] badges = new string[] { "● NVIDIA NVENC 10-Bit Active", "● AMD Radeon AMF 10-Bit Active", "● Intel Arc QSV 10-Bit Active" };
            string[] names = new string[] {
                "NVIDIA GeForce / RTX (NVENC Hardware Acceleration)",
                "AMD Radeon RX (AMF Hardware Acceleration)",
                "Intel Arc / Iris Xe (QSV Hardware Acceleration)"
            };

            for (int i = 0; i < encoders.Length; i++)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ffmpegExe,
                        Arguments = "-hide_banner -f lavfi -i color=c=black:s=256x256:d=0.04 -c:v " + encoders[i] + " -f null -",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(3500);
                        if (p.ExitCode == 0)
                        {
                            detectedGpu = vendors[i];
                            detectedGpuBadge = badges[i];
                            detectedGpuName = names[i];
                            return;
                        }
                    }
                }
                catch {}
            }

            detectedGpu = GpuVendor.Cpu;
            detectedGpuBadge = "CPU Multi-Core (libx265)";
            detectedGpuName = "CPU Multi-Core Software Encoding (libx265 / libx264)";
        }

        private static string ResolveLutPath(LutEntry entry)
        {
            if (entry == null) return null;
            if (File.Exists(entry.RelativeFile)) return entry.RelativeFile;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(entry.RelativeFile))
            {
                string candidate = Path.Combine(baseDir, "lut_library", entry.RelativeFile.Replace('/', '\\'));
                if (File.Exists(candidate)) return candidate;

                candidate = Path.Combine(baseDir, entry.RelativeFile.Replace('/', '\\'));
                if (File.Exists(candidate)) return candidate;

                string lutLib = Path.Combine(baseDir, "lut_library");
                if (Directory.Exists(lutLib))
                {
                    string fn = Path.GetFileName(entry.RelativeFile);
                    string[] matches = Directory.GetFiles(lutLib, fn, SearchOption.AllDirectories);
                    if (matches.Length > 0) return matches[0];
                }
            }
            return null;
        }

        private static double GetVideoDurationSeconds(string ffprobeExe, string videoPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ffprobeExe,
                    Arguments = string.Format("-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{0}\"", videoPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string outStr = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    double dur;
                    if (double.TryParse(outStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out dur))
                    {
                        return dur;
                    }
                }
            }
            catch {}
            return 0;
        }

        private static string FormatSeconds(double sec)
        {
            int isec = Math.Max(0, (int)sec);
            int h = isec / 3600;
            int m = (isec % 3600) / 60;
            int s = isec % 60;
            if (h > 0) return string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, s);
            return string.Format("{0:D2}:{1:D2}", m, s);
        }

        private static void StartGradingProcess()
        {
            if (activeProcess != null && !activeProcess.HasExited)
            {
                MessageBox.Show("A grading job is currently running.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string inputPath = txtInputPath.Text.Trim('"', '\'');
            if (string.IsNullOrEmpty(inputPath) || (!File.Exists(inputPath) && !Directory.Exists(inputPath)))
            {
                MessageBox.Show("Please enter a valid video file or folder path.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LutEntry lutEntry = cmbLut.SelectedItem as LutEntry;
            string lutId = lutEntry != null ? lutEntry.Id : "o4_dlogm_rec709";

            string[] lookMap = { "vibrant", "natural", "cinematic", "golden", "teal_orange" };
            string look = lookMap[Math.Max(0, cmbLook.SelectedIndex)];

            string[] codecMap = { "hevc", "h264", "prores" };
            string codec = codecMap[Math.Max(0, cmbCodec.SelectedIndex)];

            string[] scaleMap = { "keep", "4k", "2.7k", "1080p" };
            string scale = scaleMap[Math.Max(0, cmbScale.SelectedIndex)];

            int cq = (int)sliderCq.Value;
            double intensity = sliderIntensity.Value / 100.0;
            bool sharpen = (chkSharpen.IsChecked == true);
            bool genPreview = (chkPreview.IsChecked == true);
            bool isStabilizing = (chkGyroflow.IsChecked == true);

            if (isStabilizing && (isEisRockSteadyActive || isEisHorizonSteadyActive))
            {
                string modeName = isEisRockSteadyActive ? "RockSteady" : "HorizonSteady";
                MessageBox.Show(
                    string.Format("Cannot run Gyroflow stabilization on this clip.\n\n" +
                                  "This video was shot with in-camera {0} EIS turned ON, so DJI camera firmware suppressed raw gyro motion telemetry in the file.\n\n" +
                                  "Please uncheck 'Gyroflow Stabilization' to grade this clip directly with the 3D LUT.", modeName),
                    "In-Camera EIS Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            cancelRequested = false;
            btnStartGrade.IsEnabled = false;
            btnStartGrade.Content = "RENDERING...";
            btnCancelGrade.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            lblStatus.Text = isStabilizing ? "⚡ Step 1/2: Initializing Gyroflow Motion Analysis..." : "Grading with " + detectedGpuBadge + "...";
            lblSpeedEta.Text = "";

            txtLog.AppendText("\n========================================\n");
            txtLog.AppendText(string.Format("[START] Target: {0}\n", Path.GetFileName(inputPath)));
            if (isStabilizing) txtLog.AppendText("[STABILIZATION] Gyroflow Stabilization: ENABLED (Analyzing Motion Telemetry)\n");
            txtLog.AppendText(string.Format("[LUT] {0}\n", lutEntry != null ? lutEntry.DisplayName : lutId));
            txtLog.AppendText(string.Format("[HARDWARE ACCEL] {0}\n", detectedGpuName));
            txtLog.AppendText(string.Format("[LOOK] {0} ({1:F0}%) | [CODEC] {2} | [CQ] {3}\n", look.ToUpperInvariant(), intensity * 100, codec.ToUpperInvariant(), cq));

            workerThread = new Thread(() =>
            {
                string ffmpegExe = FindFfmpegExe();
                if (!string.IsNullOrEmpty(ffmpegExe) && File.Exists(ffmpegExe))
                {
                    RunNativeGrading(ffmpegExe, inputPath, lutEntry, look, intensity, codec, cq, scale, sharpen, genPreview, isStabilizing);
                }
                else
                {
                    RunWslGrading(inputPath, lutId, look, codec, cq, scale, sharpen, genPreview, isStabilizing);
                }
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private static void CancelGradingProcess()
        {
            if (activeProcess != null && !activeProcess.HasExited)
            {
                cancelRequested = true;
                txtLog.AppendText("\n[ABORTING] User requested render cancellation...\n");
                lblStatus.Text = "Cancelling render...";
                try
                {
                    activeProcess.Kill();
                }
                catch {}
            }
        }

        private static void RunNativeGrading(
            string ffmpegExe, string inputPath, LutEntry lutEntry, string look,
            double intensity, string codec, int cq, string scale, bool sharpen, bool genPreview, bool isStabilizing)
        {
            try
            {
                List<string> filesToGrade = new List<string>();
                if (File.Exists(inputPath))
                {
                    filesToGrade.Add(inputPath);
                }
                else if (Directory.Exists(inputPath))
                {
                    foreach (var f in Directory.GetFiles(inputPath))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".mp4" || ext == ".mov" || ext == ".mkv")
                        {
                            if (!f.Contains("_graded") && !f.Contains("_stabilized"))
                            {
                                filesToGrade.Add(f);
                            }
                        }
                    }
                }

                if (filesToGrade.Count == 0)
                {
                    throw new Exception("No compatible video files (.mp4, .mov, .mkv) found in path.");
                }

                string lutFullPath = ResolveLutPath(lutEntry);
                if (string.IsNullOrEmpty(lutFullPath) || !File.Exists(lutFullPath))
                {
                    throw new Exception("Selected 3D LUT file was not found: " + (lutEntry != null ? lutEntry.DisplayName : "Unknown"));
                }

                string ffprobeExe = FindFfprobeExe();

                for (int fileIdx = 0; fileIdx < filesToGrade.Count; fileIdx++)
                {
                    if (cancelRequested) break;

                    string currentFile = filesToGrade[fileIdx];
                    string baseFileName = Path.GetFileNameWithoutExtension(currentFile);
                    string targetDir = Path.Combine(Path.GetDirectoryName(currentFile), "graded");
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string gradedFile = Path.Combine(targetDir, baseFileName + "_graded.mp4");

                    mainWindow.Dispatcher.Invoke(new Action(() =>
                    {
                        txtLog.AppendText(string.Format("\n[{0}/{1}] Processing Clip: {2}\n", fileIdx + 1, filesToGrade.Count, Path.GetFileName(currentFile)));
                    }));

                    // 1. Gyroflow stabilization
                    if (isStabilizing)
                    {
                        string gyroExe = FindGyroflowExePath();
                        if (string.IsNullOrEmpty(gyroExe) || !File.Exists(gyroExe))
                        {
                            throw new Exception("Gyroflow executable was not found. Please install Gyroflow or uncheck Stabilization.");
                        }

                        string expectedStab = Path.Combine(
                            Path.GetDirectoryName(currentFile),
                            baseFileName + "_stabilized.mp4"
                        );
                        if (File.Exists(expectedStab))
                        {
                            try { File.Delete(expectedStab); } catch {}
                        }

                        string gpuTarget = "nvidia";
                        if (detectedGpu == GpuVendor.Amd) gpuTarget = "amd";
                        else if (detectedGpu == GpuVendor.Intel) gpuTarget = "intel";
                        else if (detectedGpu == GpuVendor.Cpu) gpuTarget = "cpu";

                        string outParams = "{ 'codec': 'H.265/HEVC', 'bitrate': 180, 'use_gpu': true, 'audio': true }";
                        string gyroArgs = string.Format("\"{0}\" --stdout-progress -f -r {1} -p \"{2}\"", currentFile, gpuTarget, outParams);

                        ProcessStartInfo gyroPsi = new ProcessStartInfo
                        {
                            FileName = gyroExe,
                            Arguments = gyroArgs,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        activeProcess = new Process { StartInfo = gyroPsi };
                        activeProcess.OutputDataReceived += (s, e) => ParseProcessOutput(e.Data);
                        activeProcess.ErrorDataReceived += (s, e) => ParseProcessOutput(e.Data);

                        activeProcess.Start();
                        activeProcess.BeginOutputReadLine();
                        activeProcess.BeginErrorReadLine();
                        activeProcess.WaitForExit();

                        if (cancelRequested) break;

                        if (activeProcess.ExitCode != 0 || !File.Exists(expectedStab))
                        {
                            throw new Exception("Gyroflow stabilization could not find raw motion data in this clip.\n" +
                                                "Notice: If RockSteady / HorizonSteady in-camera EIS was enabled, raw gyro stream is disabled by DJI. " +
                                                "Please uncheck 'Gyroflow Stabilization' to grade this clip directly with the 3D LUT.");
                        }

                        currentFile = expectedStab;
                        mainWindow.Dispatcher.Invoke(new Action(() =>
                        {
                            progressBar.Value = 0;
                            lblStatus.Text = "🎬 Step 2/2: Gyroflow complete! Starting 10-Bit LUT Color Grading...";
                            lblSpeedEta.Text = "Initializing Hardware Encoder...";
                            txtLog.AppendText("[GYROFLOW DONE] Stabilized: " + Path.GetFileName(expectedStab) + "\n");
                        }));
                    }

                    if (cancelRequested) break;

                    // 2. FFmpeg 3D LUT Grading
                    double durationSec = 0;
                    if (!string.IsNullOrEmpty(ffprobeExe))
                    {
                        durationSec = GetVideoDurationSeconds(ffprobeExe, currentFile);
                    }

                    string escapedLut = lutFullPath.Replace("\\", "/").Replace(":", "\\:");
                    string vf = string.Format("lut3d=file='{0}':interp=tetrahedral", escapedLut);

                    // Scaled look parameters
                    if (look == "vibrant")
                    {
                        double sat = 1.0 + (0.14 * intensity);
                        double con = 1.0 + (0.04 * intensity);
                        double unsh = 0.35 * intensity;
                        vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            ",eq=saturation={0:F2}:contrast={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", sat, con, unsh);
                    }
                    else if (look == "cinematic")
                    {
                        double con = 1.0 + (0.06 * intensity);
                        double sat = 1.0 + (0.08 * intensity);
                        double unsh = 0.30 * intensity;
                        vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            ",eq=contrast={0:F2}:brightness=-0.01:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                    }
                    else if (look == "golden")
                    {
                        double con = 1.0 + (0.05 * intensity);
                        double sat = 1.0 + (0.10 * intensity);
                        double unsh = 0.30 * intensity;
                        vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            ",colorbalance=rs=0.03:gs=0.01:bs=-0.03:rm=0.04:gm=0.01:bm=-0.04,eq=contrast={0:F2}:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                    }
                    else if (look == "teal_orange")
                    {
                        double con = 1.0 + (0.06 * intensity);
                        double sat = 1.0 + (0.12 * intensity);
                        double unsh = 0.35 * intensity;
                        vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            ",colorbalance=rs=-0.02:gs=0.01:bs=0.04:rm=0.04:gm=0.01:bm=-0.03:rh=0.05:gh=0.02:bh=-0.04,eq=contrast={0:F2}:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                    }
                    else if (sharpen)
                    {
                        vf += ",unsharp=lx=3:ly=3:la=0.40:cx=3:cy=3:ca=0.0";
                    }

                    if (scale == "4k") vf += ",scale=3840:2160:flags=lanczos";
                    else if (scale == "2.7k") vf += ",scale=2704:1520:flags=lanczos";
                    else if (scale == "1080p") vf += ",scale=1920:1080:flags=lanczos";

                    vf += ",format=yuv420p10le";

                    string codecArgs = "";
                    if (codec == "prores")
                    {
                        codecArgs = "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le";
                    }
                    else if (codec == "h264")
                    {
                        if (detectedGpu == GpuVendor.Nvidia)
                            codecArgs = string.Format("-c:v h264_nvenc -preset p6 -tune hq -rc vbr -cq {0} -b:v 0 -spatial-aq 1 -temporal-aq 1", cq);
                        else if (detectedGpu == GpuVendor.Amd)
                            codecArgs = "-c:v h264_amf -quality quality -rc cbr";
                        else if (detectedGpu == GpuVendor.Intel)
                            codecArgs = string.Format("-c:v h264_qsv -preset slow -global_quality {0}", cq);
                        else
                            codecArgs = string.Format("-c:v libx264 -crf {0} -preset slow", cq);
                    }
                    else // hevc
                    {
                        if (detectedGpu == GpuVendor.Nvidia)
                            codecArgs = string.Format("-c:v hevc_nvenc -preset p6 -tune hq -rc vbr -cq {0} -b:v 0 -spatial-aq 1 -temporal-aq 1", cq);
                        else if (detectedGpu == GpuVendor.Amd)
                            codecArgs = "-c:v hevc_amf -quality quality -rc cbr";
                        else if (detectedGpu == GpuVendor.Intel)
                            codecArgs = string.Format("-c:v hevc_qsv -preset slow -global_quality {0}", cq);
                        else
                            codecArgs = string.Format("-c:v libx265 -crf {0} -preset medium", cq);
                    }

                    string tagStr = (codec == "hevc") ? "-tag:v hvc1" : "-tag:v avc1";
                    string ffmpegArgs = string.Format(
                        "-y -i \"{0}\" -vf \"{1}\" {2} -c:a copy {3} -map_metadata 0 -movflags +faststart \"{4}\"",
                        currentFile, vf, codecArgs, tagStr, gradedFile
                    );

                    ProcessStartInfo ffmpegPsi = new ProcessStartInfo
                    {
                        FileName = ffmpegExe,
                        Arguments = ffmpegArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    activeProcess = new Process { StartInfo = ffmpegPsi };
                    activeProcess.ErrorDataReceived += (s, e) => ParseFfmpegOutput(e.Data, durationSec);

                    activeProcess.Start();
                    activeProcess.BeginErrorReadLine();
                    activeProcess.WaitForExit();

                    if (cancelRequested) break;

                    bool success = (activeProcess.ExitCode == 0 && File.Exists(gradedFile));

                    if (success && genPreview)
                    {
                        string outPrev = Path.Combine(targetDir, baseFileName + "_preview.jpg");
                        GenerateNativePreviewSnapshot(ffmpegExe, filesToGrade[fileIdx], lutFullPath, look, intensity, sharpen, outPrev);
                    }

                    if (!success)
                    {
                        throw new Exception("FFmpeg render exited with code " + activeProcess.ExitCode);
                    }
                }

                mainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    btnStartGrade.IsEnabled = true;
                    btnStartGrade.Content = "🚀 APPLY LUT & GRADE";
                    btnCancelGrade.Visibility = Visibility.Collapsed;

                    if (cancelRequested)
                    {
                        progressBar.Value = 0;
                        lblStatus.Text = "Render cancelled by user.";
                        lblSpeedEta.Text = "";
                        txtLog.AppendText("\n[CANCELLED] Render aborted.\n");
                    }
                    else
                    {
                        progressBar.Value = 100;
                        lblStatus.Text = string.Format("Grading Complete! Processed {0} clip(s).", filesToGrade.Count);
                        lblSpeedEta.Text = "✔ Render Finished";
                        txtLog.AppendText("\n[SUCCESS] Color grading finished! Files saved in 'graded' folder.\n");
                        LoadPreviewImageIfAvailable(inputPath);
                    }
                }));
            }
            catch (Exception ex)
            {
                mainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    btnStartGrade.IsEnabled = true;
                    btnStartGrade.Content = "🚀 APPLY LUT & GRADE";
                    btnCancelGrade.Visibility = Visibility.Collapsed;
                    lblStatus.Text = "Error: " + ex.Message;
                    txtLog.AppendText("\n[ERROR] " + ex.Message + "\n");
                }));
            }
        }

        private static void RunWslGrading(
            string inputPath, string lutId, string look, string codec,
            int cq, string scale, bool sharpen, bool genPreview, bool isStabilizing)
        {
            try
            {
                string sharpenFlag = sharpen ? "--sharpen" : "";
                string previewFlag = genPreview ? "-p" : "";
                string gyroFlag = isStabilizing ? "-g" : "";

                string args = string.Format("-d {0} -e /usr/bin/python3 {1}/dji_lut_studio/cli.py \"{2}\" -l {3} --look {4} -c {5} --cq {6} --scale {7} {8} {9} {10} --overwrite",
                    wslDistro, appDir, inputPath, lutId, look, codec, cq, scale, sharpenFlag, previewFlag, gyroFlag);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                activeProcess = new Process { StartInfo = psi };
                activeProcess.OutputDataReceived += (s, e) => ParseProcessOutput(e.Data);
                activeProcess.ErrorDataReceived += (s, e) => ParseProcessOutput(e.Data);

                activeProcess.Start();
                activeProcess.BeginOutputReadLine();
                activeProcess.BeginErrorReadLine();
                activeProcess.WaitForExit();

                bool success = (activeProcess.ExitCode == 0);
                mainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    btnStartGrade.IsEnabled = true;
                    btnStartGrade.Content = "🚀 APPLY LUT & GRADE";
                    btnCancelGrade.Visibility = Visibility.Collapsed;
                    if (success)
                    {
                        progressBar.Value = 100;
                        lblStatus.Text = "Grading Complete! Placed in 'graded' folder.";
                        lblSpeedEta.Text = "✔ Render Finished";
                        txtLog.AppendText("[SUCCESS] Grading complete!\n");
                        LoadPreviewImageIfAvailable(inputPath);
                    }
                    else
                    {
                        lblStatus.Text = "Grading exited with errors. Check logs.";
                        txtLog.AppendText("[ERROR] Process exited with code " + activeProcess.ExitCode + "\n");
                    }
                }));
            }
            catch (Exception ex)
            {
                mainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    btnStartGrade.IsEnabled = true;
                    btnStartGrade.Content = "🚀 APPLY LUT & GRADE";
                    btnCancelGrade.Visibility = Visibility.Collapsed;
                    lblStatus.Text = "Error: " + ex.Message;
                    txtLog.AppendText("[EXCEPTION] " + ex.Message + "\n");
                }));
            }
        }

        private static void ParseFfmpegOutput(string line, double totalDurationSec)
        {
            if (string.IsNullOrEmpty(line)) return;

            mainWindow.Dispatcher.Invoke(new Action(() =>
            {
                txtLog.AppendText(line + "\n");
                txtLog.ScrollToEnd();

                Match mTime = Regex.Match(line, @"time=(\d+):(\d+):(\d+(?:\.\d+)?)");
                if (mTime.Success)
                {
                    int h = int.Parse(mTime.Groups[1].Value);
                    int m = int.Parse(mTime.Groups[2].Value);
                    double s = double.Parse(mTime.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double currentSec = h * 3600 + m * 60 + s;

                    double pct = 0;
                    if (totalDurationSec > 0)
                    {
                        pct = Math.Min(100.0, Math.Max(0.0, (currentSec / totalDurationSec) * 100.0));
                        progressBar.Value = (int)pct;
                        lblStatus.Text = string.Format("🎬 Step 2/2: Applying 3D LUT & Grading: {0:F1}%", pct);
                    }

                    Match mFps = Regex.Match(line, @"fps=\s*(\d+(?:\.\d+)?)");
                    Match mSpd = Regex.Match(line, @"speed=\s*(\d+(?:\.\d+)?x)");

                    string fpsStr = mFps.Success ? (mFps.Groups[1].Value + " fps") : "";
                    string spdStr = mSpd.Success ? mSpd.Groups[1].Value : "";

                    string etaStr = "";
                    if (totalDurationSec > 0 && currentSec > 0)
                    {
                        double speedVal = 1.0;
                        if (mSpd.Success && double.TryParse(mSpd.Groups[1].Value.TrimEnd('x'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out speedVal) && speedVal > 0)
                        {
                            double remainSec = (totalDurationSec - currentSec) / speedVal;
                            etaStr = "ETA: " + FormatSeconds(remainSec);
                        }
                    }

                    List<string> parts = new List<string>();
                    if (!string.IsNullOrEmpty(fpsStr)) parts.Add(fpsStr);
                    if (!string.IsNullOrEmpty(spdStr)) parts.Add("(" + spdStr + ")");
                    if (!string.IsNullOrEmpty(etaStr)) parts.Add("• " + etaStr);

                    lblSpeedEta.Text = string.Join(" ", parts.ToArray());
                }
            }));
        }

        private static void ParseProcessOutput(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            mainWindow.Dispatcher.Invoke(new Action(() =>
            {
                txtLog.AppendText(line + "\n");
                txtLog.ScrollToEnd();

                // 1. Gyroflow stabilization progress
                if (line.Contains("GYROFLOW STABILIZING") || line.Contains("Rendering progress"))
                {
                    Match mPct = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
                    if (mPct.Success)
                    {
                        double pct = double.Parse(mPct.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        progressBar.Value = Math.Min(100, Math.Max(0, (int)pct));
                        lblStatus.Text = string.Format("⚡ Step 1/2: Gyroflow Stabilizing: {0:F1}%", pct);
                    }

                    Match mFrames = Regex.Match(line, @"Frames:\s*(\d+/\d+)");
                    Match mEta = Regex.Match(line, @"ETA:\s*([^\s\|]+)");
                    string etaPart = mEta.Success ? ("ETA: " + mEta.Groups[1].Value) : "";
                    string framesPart = mFrames.Success ? ("Frames: " + mFrames.Groups[1].Value) : "";

                    if (!string.IsNullOrEmpty(framesPart) && !string.IsNullOrEmpty(etaPart))
                        lblSpeedEta.Text = framesPart + " • " + etaPart;
                    else if (!string.IsNullOrEmpty(framesPart))
                        lblSpeedEta.Text = framesPart;
                    else if (!string.IsNullOrEmpty(etaPart))
                        lblSpeedEta.Text = etaPart;
                    return;
                }

                // 2. Gyroflow completion / transition to Step 2
                if (line.Contains("GYROFLOW DONE") || line.Contains("Proceeding to 3D LUT") || line.Contains("Rendering completed"))
                {
                    progressBar.Value = 0;
                    lblStatus.Text = "🎬 Step 2/2: Gyroflow finished! Starting 10-Bit LUT Color Grading...";
                    lblSpeedEta.Text = "Initializing Hardware Encoder...";
                    return;
                }

                // 3. 10-bit LUT Color Grading progress (WSL stream)
                if (line.Contains("[GRADING]") || line.Contains("fps") || line.Contains("time="))
                {
                    Match mPct = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
                    if (mPct.Success)
                    {
                        double pct = double.Parse(mPct.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        progressBar.Value = Math.Min(100, Math.Max(0, (int)pct));
                        lblStatus.Text = string.Format("🎬 Step 2/2: Applying 3D LUT & Grading: {0:F1}%", pct);
                    }

                    Match mSpd = Regex.Match(line, @"(\d+(?:\.\d+)?\s*fps.*?)(?:\||$)");
                    Match mEta = Regex.Match(line, @"ETA:\s*([0-9:]+)");
                    string spdPart = mSpd.Success ? mSpd.Groups[1].Value.Trim() : "";
                    string etaPart = mEta.Success ? ("ETA: " + mEta.Groups[1].Value) : "";

                    if (!string.IsNullOrEmpty(spdPart) && !string.IsNullOrEmpty(etaPart))
                        lblSpeedEta.Text = spdPart + " • " + etaPart;
                    else if (!string.IsNullOrEmpty(spdPart))
                        lblSpeedEta.Text = spdPart;
                    else if (!string.IsNullOrEmpty(etaPart))
                        lblSpeedEta.Text = etaPart;
                    return;
                }
            }));
        }

        private static void GenerateNativePreviewSnapshot(
            string ffmpegExe, string videoPath, string lutFullPath, string look, double intensity, bool sharpen, string outPreview)
        {
            try
            {
                string escapedLut = lutFullPath.Replace("\\", "/").Replace(":", "\\:");
                string vf = string.Format("lut3d=file='{0}':interp=tetrahedral", escapedLut);

                if (look == "vibrant")
                {
                    double sat = 1.0 + (0.14 * intensity);
                    double con = 1.0 + (0.04 * intensity);
                    double unsh = 0.35 * intensity;
                    vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        ",eq=saturation={0:F2}:contrast={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", sat, con, unsh);
                }
                else if (look == "cinematic")
                {
                    double con = 1.0 + (0.06 * intensity);
                    double sat = 1.0 + (0.08 * intensity);
                    double unsh = 0.30 * intensity;
                    vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        ",eq=contrast={0:F2}:brightness=-0.01:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                }
                else if (look == "golden")
                {
                    double con = 1.0 + (0.05 * intensity);
                    double sat = 1.0 + (0.10 * intensity);
                    double unsh = 0.30 * intensity;
                    vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        ",colorbalance=rs=0.03:gs=0.01:bs=-0.03:rm=0.04:gm=0.01:bm=-0.04,eq=contrast={0:F2}:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                }
                else if (look == "teal_orange")
                {
                    double con = 1.0 + (0.06 * intensity);
                    double sat = 1.0 + (0.12 * intensity);
                    double unsh = 0.35 * intensity;
                    vf += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        ",colorbalance=rs=-0.02:gs=0.01:bs=0.04:rm=0.04:gm=0.01:bm=-0.03:rh=0.05:gh=0.02:bh=-0.04,eq=contrast={0:F2}:saturation={1:F2},unsharp=lx=3:ly=3:la={2:F2}:cx=3:cy=3:ca=0.0", con, sat, unsh);
                }
                else if (sharpen)
                {
                    vf += ",unsharp=lx=3:ly=3:la=0.40:cx=3:cy=3:ca=0.0";
                }

                vf += ",format=yuv420p10le";

                string filterComplex = string.Format(
                    "[0:v]trim=start=1:duration=1,scale=1920:1080:flags=lanczos,split=2[left_raw][right_raw];" +
                    "[right_raw]{0}[graded];" +
                    "[left_raw]drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='ORIGINAL (RAW)':x=40:y=40:fontsize=36:fontcolor=white:box=1:boxcolor=black@0.6[left_labeled];" +
                    "[graded]drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='GRADED ({1})':x=40:y=40:fontsize=36:fontcolor=white:box=1:boxcolor=black@0.6[right_labeled];" +
                    "[left_labeled][right_labeled]hstack[out]",
                    vf, look.ToUpperInvariant()
                );

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = string.Format("-y -ss 1.0 -i \"{0}\" -filter_complex \"{1}\" -map \"[out]\" -vframes 1 -q:v 2 \"{2}\"",
                        videoPath, filterComplex, outPreview),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(30000);
                }
            }
            catch {}
        }

        private static void RunPreviewGeneration()
        {
            string inputPath = txtInputPath.Text.Trim('"', '\'');
            if (!File.Exists(inputPath))
            {
                MessageBox.Show("Please select a single video file to preview.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LutEntry lutEntry = cmbLut.SelectedItem as LutEntry;
            string lutId = lutEntry != null ? lutEntry.Id : "o4_dlogm_rec709";
            string[] lookMap = { "vibrant", "natural", "cinematic", "golden", "teal_orange" };
            string look = lookMap[Math.Max(0, cmbLook.SelectedIndex)];
            double intensity = sliderIntensity.Value / 100.0;
            bool sharpen = (chkSharpen.IsChecked == true);

            btnGenPreview.IsEnabled = false;
            btnGenPreview.Content = "Generating...";

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string outDir = Path.Combine(Path.GetDirectoryName(inputPath), "graded");
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    string outPrev = Path.Combine(outDir, Path.GetFileNameWithoutExtension(inputPath) + "_preview.jpg");

                    string ffmpegExe = FindFfmpegExe();
                    if (!string.IsNullOrEmpty(ffmpegExe) && File.Exists(ffmpegExe))
                    {
                        string lutFullPath = ResolveLutPath(lutEntry);
                        GenerateNativePreviewSnapshot(ffmpegExe, inputPath, lutFullPath, look, intensity, sharpen, outPrev);
                    }
                    else
                    {
                        string script = string.Format(
                            "import sys; sys.path.insert(0, '{0}'); " +
                            "from dji_lut_studio.manifest import manifest; " +
                            "from dji_lut_studio.engine import generate_preview_snapshot; " +
                            "from pathlib import Path; " +
                            "p_lut = manifest.resolve_lut_file('{1}'); " +
                            "generate_preview_snapshot(Path('{2}'), p_lut, Path('{3}'), look_style='{4}', sharpen=True)",
                            appDir, lutId, inputPath.Replace("\\", "/"), outPrev.Replace("\\", "/"), look
                        );

                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = "wsl.exe",
                            Arguments = string.Format("-d {0} -e /usr/bin/python3 -c \"{1}\"", wslDistro, script),
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process p = Process.Start(psi);
                        p.WaitForExit();
                    }

                    mainWindow.Dispatcher.Invoke(new Action(() =>
                    {
                        btnGenPreview.IsEnabled = true;
                        btnGenPreview.Content = "Generate Snapshot";
                        if (File.Exists(outPrev))
                        {
                            LoadPreviewImageDirect(outPrev);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    mainWindow.Dispatcher.Invoke(new Action(() =>
                    {
                        btnGenPreview.IsEnabled = true;
                        btnGenPreview.Content = "Generate Snapshot";
                        txtLog.AppendText("[PREVIEW ERROR] " + ex.Message + "\n");
                    }));
                }
            });
        }

        private static void LoadPreviewImageIfAvailable(string inputPath)
        {
            try
            {
                string dir = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath);
                string gradedDir = Path.Combine(dir, "graded");
                if (Directory.Exists(gradedDir))
                {
                    string[] jpegs = Directory.GetFiles(gradedDir, "*_preview.jpg");
                    if (jpegs.Length > 0)
                    {
                        LoadPreviewImageDirect(jpegs[jpegs.Length - 1]);
                    }
                }
            }
            catch { }
        }

        private static void LoadPreviewImageDirect(string path)
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                imgPreview.Source = bmp;
                imgPreview.Visibility = Visibility.Visible;
                lblPreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private static void OpenGradedFolder()
        {
            string inPath = txtInputPath.Text.Trim('"', '\'');
            string targetDir = "";
            if (File.Exists(inPath)) targetDir = Path.Combine(Path.GetDirectoryName(inPath), "graded");
            else if (Directory.Exists(inPath)) targetDir = Path.Combine(inPath, "graded");

            if (!string.IsNullOrEmpty(targetDir))
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                Process.Start("explorer.exe", targetDir);
            }
            else
            {
                MessageBox.Show("Please choose a valid input file or folder first.", "DJI LUT Studio");
            }
        }

        private static void ToggleWatcher()
        {
            string inPath = txtInputPath.Text.Trim('"', '\'');
            if (!Directory.Exists(inPath))
            {
                if (File.Exists(inPath)) inPath = Path.GetDirectoryName(inPath);
                else
                {
                    MessageBox.Show("Please select a valid folder to watch (e.g. D:\\dji).", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            if (isWatching)
            {
                if (folderWatcher != null)
                {
                    folderWatcher.EnableRaisingEvents = false;
                    folderWatcher.Dispose();
                    folderWatcher = null;
                }
                isWatching = false;
                btnWatcher.Content = "⏱ Start Watcher";
                txtLog.AppendText("\n[WATCHER STOPPED] Live folder monitoring deactivated.\n");
                return;
            }

            isWatching = true;
            btnWatcher.Content = "🟢 Watching... (Click to Stop)";
            txtLog.AppendText(string.Format("\n[WATCHER ACTIVE] Monitoring folder: {0}\n", inPath));

            folderWatcher = new FileSystemWatcher(inPath);
            folderWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;
            folderWatcher.Filter = "*.*";
            folderWatcher.Created += (s, e) =>
            {
                string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if ((ext == ".mp4" || ext == ".mov" || ext == ".mkv") && !e.FullPath.Contains("_graded") && !e.FullPath.Contains("_stabilized"))
                {
                    mainWindow.Dispatcher.Invoke(new Action(() =>
                    {
                        txtLog.AppendText(string.Format("\n[WATCHER DETECTED] New incoming file: {0}\n", e.Name));
                        txtInputPath.Text = e.FullPath;
                        AutoSelectDeviceForPath(e.FullPath);
                    }));

                    // Wait for copy to complete
                    ThreadPool.QueueUserWorkItem((st) =>
                    {
                        Thread.Sleep(2000);
                        bool ready = false;
                        for (int attempt = 0; attempt < 10; attempt++)
                        {
                            try
                            {
                                using (var stream = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                                {
                                    if (stream.Length > 0) { ready = true; break; }
                                }
                            }
                            catch { Thread.Sleep(1500); }
                        }
                        if (ready)
                        {
                            mainWindow.Dispatcher.Invoke(new Action(() => StartGradingProcess()));
                        }
                    });
                }
            };
            folderWatcher.EnableRaisingEvents = true;
        }
    }
}
