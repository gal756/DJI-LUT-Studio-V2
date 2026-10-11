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
        private static CheckBox chkSharpen;
        private static CheckBox chkPreview;
        private static Button btnStartGrade;
        private static Button btnOpenFolder;
        private static Button btnWatcher;
        private static Button btnGenPreview;
        private static ProgressBar progressBar;
        private static TextBlock lblStatus;
        private static TextBlock lblSpeedEta;
        private static TextBox txtLog;
        private static Image imgPreview;
        private static TextBlock lblPreviewPlaceholder;
        private static Border borderPreview;

        private static Process activeProcess = null;
        private static Thread workerThread = null;

        private static Dictionary<string, List<LutEntry>> deviceDatabase;

        public class LutEntry
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public bool IsRecommended { get; set; }
            public LutEntry(string id, string name, bool rec = false)
            {
                Id = id;
                DisplayName = name;
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
        Title='DJI LUT Studio - Universal Video Color Grading Suite'
        Height='760' Width='980' MinHeight='700' MinWidth='920'
        WindowStartupLocation='CenterScreen'
        Background='#0D1117' Foreground='#F0F6FC' FontFamily='Segoe UI'
        AllowDrop='True'>

    <Window.Resources>
        <!-- Modern ScrollViewer Style -->
        <Style TargetType='{x:Type ScrollBar}'>
            <Setter Property='Background' Value='#0D1117'/>
            <Setter Property='Foreground' Value='#30363D'/>
        </Style>

        <!-- Dark TextBox Style -->
        <Style TargetType='{x:Type TextBox}'>
            <Setter Property='Background' Value='#161B22'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='BorderBrush' Value='#30363D'/>
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
                                <Setter TargetName='Bd' Property='BorderBrush' Value='#00D2FF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Standard Button Style -->
        <Style x:Key='SubButton' TargetType='{x:Type Button}'>
            <Setter Property='Background' Value='#21262D'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='BorderBrush' Value='#30363D'/>
            <Setter Property='BorderThickness' Value='1'/>
            <Setter Property='FontSize' Value='12.5'/>
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
                                <Setter TargetName='Bd' Property='Background' Value='#30363D'/>
                                <Setter TargetName='Bd' Property='BorderBrush' Value='#8B949E'/>
                            </Trigger>
                            <Trigger Property='IsPressed' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#161B22'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Primary Action Button -->
        <Style x:Key='PrimaryActionBtn' TargetType='{x:Type Button}'>
            <Setter Property='Background' Value='#00D2FF'/>
            <Setter Property='Foreground' Value='#0D1117'/>
            <Setter Property='FontSize' Value='13.5'/>
            <Setter Property='FontWeight' Value='Bold'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Button}'>
                        <Border Name='Bd' Background='{TemplateBinding Background}' CornerRadius='6'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#33DCFF'/>
                            </Trigger>
                            <Trigger Property='IsPressed' Value='True'>
                                <Setter TargetName='Bd' Property='Background' Value='#00B4DB'/>
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
                        Background='#161B22' BorderBrush='#30363D' BorderThickness='1' />
                <Path x:Name='Arrow' Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center'
                      Data='M 0 0 L 4 4 L 8 0 Z' Fill='#8B949E' />
            </Grid>
            <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='true'>
                    <Setter TargetName='Border' Property='BorderBrush' Value='#00D2FF' />
                    <Setter TargetName='Arrow' Property='Fill' Value='#00D2FF' />
                </Trigger>
                <Trigger Property='IsChecked' Value='true'>
                    <Setter TargetName='Border' Property='BorderBrush' Value='#00D2FF' />
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
                        <Border Name='Border' Padding='10,4' CornerRadius='4' Margin='2,1'
                                Background='Transparent' SnapsToDevicePixels='true'>
                            <ContentPresenter VerticalAlignment='Center' />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsHighlighted' Value='true'>
                                <Setter TargetName='Border' Property='Background' Value='#1F3A4B'/>
                            </Trigger>
                            <Trigger Property='IsSelected' Value='true'>
                                <Setter TargetName='Border' Property='Background' Value='#00D2FF'/>
                                <Setter Property='Foreground' Value='#0D1117'/>
                                <Setter Property='FontWeight' Value='Bold'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern ComboBox Style -->
        <Style TargetType='{x:Type ComboBox}'>
            <Setter Property='SnapsToDevicePixels' Value='true'/>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Auto'/>
            <Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/>
            <Setter Property='ScrollViewer.CanContentScroll' Value='true'/>
            <Setter Property='Height' Value='34'/>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='FontSize' Value='12.5'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type ComboBox}'>
                        <Grid>
                            <ToggleButton Name='ToggleButton' Template='{StaticResource ComboBoxToggleButton}'
                                          Focusable='false'
                                          IsChecked='{Binding Path=IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'
                                          ClickMode='Press'/>
                            <ContentPresenter Name='ContentSite' IsHitTestVisible='False'
                                              Content='{TemplateBinding SelectionBoxItem}'
                                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                              ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                                              Margin='12,3,30,3' VerticalAlignment='Center' HorizontalAlignment='Left' />
                            <Popup Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}'
                                   AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
                                <Grid Name='DropDown' SnapsToDevicePixels='True'
                                      MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
                                    <Border Name='DropDownBorder' Background='#161B22' BorderThickness='1'
                                            BorderBrush='#30363D' CornerRadius='6' Margin='0,2,0,0'>
                                        <ScrollViewer Margin='4,6' SnapsToDevicePixels='True'>
                                            <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Circular' />
                                        </ScrollViewer>
                                    </Border>
                                </Grid>
                            </Popup>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern CheckBox Style -->
        <Style TargetType='{x:Type CheckBox}'>
            <Setter Property='Foreground' Value='#F0F6FC'/>
            <Setter Property='FontSize' Value='12.5'/>
            <Setter Property='Cursor' Value='Hand'/>
        </Style>

        <!-- Modern Slider Style -->
        <Style TargetType='{x:Type Slider}'>
            <Setter Property='SnapsToDevicePixels' Value='true'/>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='Focusable' Value='false'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Slider}'>
                        <Grid VerticalAlignment='Center'>
                            <Border Height='4' Background='#30363D' CornerRadius='2'/>
                            <Track Name='PART_Track'>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='{x:Type Thumb}'>
                                                <Ellipse Width='14' Height='14' Fill='#00D2FF' Stroke='#0D1117' StrokeThickness='2' Cursor='Hand'/>
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
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height='56'/>
            <RowDefinition Height='*'/>
            <RowDefinition Height='68'/>
        </Grid.RowDefinitions>

        <!-- Top Header Bar -->
        <Border Grid.Row='0' Background='#161B22' BorderBrush='#30363D' BorderThickness='0,0,0,1' Padding='20,0'>
            <DockPanel LastChildFill='False'>
                <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                    <TextBlock Text='🎬 DJI LUT STUDIO' FontSize='16' FontWeight='Bold' Foreground='#00D2FF' VerticalAlignment='Center'/>
                    <Border Background='#1F3A4B' CornerRadius='4' Padding='6,2' Margin='12,0,0,0' VerticalAlignment='Center'>
                        <TextBlock Text='v2.0.0 PRO' FontSize='10.5' FontWeight='Bold' Foreground='#00D2FF'/>
                    </Border>
                </StackPanel>

                <StackPanel Orientation='Horizontal' DockPanel.Dock='Right' VerticalAlignment='Center'>
                    <Border Background='#1A3328' BorderBrush='#00F2A9' BorderThickness='1' CornerRadius='12' Padding='10,4' Margin='0,0,10,0'>
                        <StackPanel Orientation='Horizontal'>
                            <Ellipse Width='7' Height='7' Fill='#00F2A9' Margin='0,0,6,0' VerticalAlignment='Center'/>
                            <TextBlock Text='NVIDIA RTX 3070 (10-Bit Active)' FontSize='11' FontWeight='SemiBold' Foreground='#00F2A9'/>
                        </StackPanel>
                    </Border>
                    <Border Background='#21262D' BorderBrush='#30363D' BorderThickness='1' CornerRadius='12' Padding='8,4'>
                        <TextBlock Text='49 3D LUTs Loaded' FontSize='11' Foreground='#8B949E'/>
                    </Border>
                </StackPanel>
            </DockPanel>
        </Border>

        <!-- Main Workspace (No Scroll, Grid Structured) -->
        <Grid Grid.Row='1' Margin='20,12,20,10'>
            <Grid.RowDefinitions>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <!-- Card 1: Input Footage -->
            <Border Grid.Row='0' Background='#161B22' BorderBrush='#30363D' BorderThickness='1' CornerRadius='8' Padding='14,10' Margin='0,0,0,10'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,6'>
                        <Border Background='#00D2FF' CornerRadius='3' Width='16' Height='16' Margin='0,0,6,0'>
                            <TextBlock Text='1' FontSize='10' FontWeight='Bold' Foreground='#0D1117' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='INPUT FOOTAGE (DRAG &amp; DROP FILE OR DIRECTORY)' FontSize='11.5' FontWeight='Bold' Foreground='#8B949E'/>
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
                </Grid>
            </Border>

            <!-- Card 2: Camera & LUT Selection -->
            <Border Grid.Row='1' Background='#161B22' BorderBrush='#30363D' BorderThickness='1' CornerRadius='8' Padding='14,10' Margin='0,0,0,10'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,6'>
                        <Border Background='#00D2FF' CornerRadius='3' Width='16' Height='16' Margin='0,0,6,0'>
                            <TextBlock Text='2' FontSize='10' FontWeight='Bold' Foreground='#0D1117' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='CAMERA MODEL &amp; 3D CONVERSION LUT' FontSize='11.5' FontWeight='Bold' Foreground='#8B949E'/>
                    </StackPanel>
                    <Grid Grid.Row='1'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='14'/>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column='0'>
                            <TextBlock Text='DJI Camera / Drone Model:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,4'/>
                            <ComboBox Name='CmbDevice'/>
                        </StackPanel>
                        <StackPanel Grid.Column='2'>
                            <TextBlock Text='3D Conversion LUT (D-Log / D-Log M to Rec.709):' FontSize='11' Foreground='#8B949E' Margin='0,0,0,4'/>
                            <ComboBox Name='CmbLut'/>
                        </StackPanel>
                        <Button Name='BtnCustomLut' Grid.Column='3' Content='Custom...' Style='{StaticResource SubButton}' Margin='8,18,0,0' Height='34' Padding='10,0'/>
                    </Grid>
                </Grid>
            </Border>

            <!-- Card 3: Look Style & Settings -->
            <Border Grid.Row='2' Background='#161B22' BorderBrush='#30363D' BorderThickness='1' CornerRadius='8' Padding='14,10' Margin='0,0,0,10'>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                    </Grid.RowDefinitions>
                    <StackPanel Orientation='Horizontal' Margin='0,0,0,6'>
                        <Border Background='#00D2FF' CornerRadius='3' Width='16' Height='16' Margin='0,0,6,0'>
                            <TextBlock Text='3' FontSize='10' FontWeight='Bold' Foreground='#0D1117' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='GRADING STYLE &amp; ENCODER SETTINGS' FontSize='11.5' FontWeight='Bold' Foreground='#8B949E'/>
                    </StackPanel>
                    <Grid Grid.Row='1'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='1.2*'/>
                            <ColumnDefinition Width='12'/>
                            <ColumnDefinition Width='1*'/>
                            <ColumnDefinition Width='12'/>
                            <ColumnDefinition Width='1*'/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column='0'>
                            <TextBlock Text='Post-LUT Look Preset:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,4'/>
                            <ComboBox Name='CmbLook'/>
                        </StackPanel>
                        <StackPanel Grid.Column='2'>
                            <TextBlock Text='Encoder Codec:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,4'/>
                            <ComboBox Name='CmbCodec'/>
                        </StackPanel>
                        <StackPanel Grid.Column='4'>
                            <TextBlock Text='Output Resolution:' FontSize='11' Foreground='#8B949E' Margin='0,0,0,4'/>
                            <ComboBox Name='CmbScale'/>
                        </StackPanel>
                    </Grid>
                    <Grid Grid.Row='2' Margin='0,10,0,0'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='160'/>
                            <ColumnDefinition Width='60'/>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='Auto'/>
                            <ColumnDefinition Width='20'/>
                            <ColumnDefinition Width='Auto'/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column='0' Text='Quality (CQ):' FontSize='11.5' Foreground='#8B949E' VerticalAlignment='Center' Margin='0,0,8,0'/>
                        <Slider Name='SliderCq' Grid.Column='1' Minimum='15' Maximum='28' Value='19' VerticalAlignment='Center'/>
                        <TextBlock Name='LblCqVal' Grid.Column='2' Text='19 (High)' FontSize='11.5' FontWeight='Bold' Foreground='#00D2FF' VerticalAlignment='Center' Margin='8,0,0,0'/>
                        <CheckBox Name='ChkSharpen' Grid.Column='4' Content='Apply Foliage Unsharp Mask' IsChecked='True' VerticalAlignment='Center'/>
                        <CheckBox Name='ChkPreview' Grid.Column='6' Content='Generate Comparison JPEG' IsChecked='True' VerticalAlignment='Center'/>
                    </Grid>
                </Grid>
            </Border>

            <!-- Card 4: Preview (Left) & Pipeline Logs (Right) -->
            <Grid Grid.Row='3'>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='1.1*'/>
                    <ColumnDefinition Width='12'/>
                    <ColumnDefinition Width='1*'/>
                </Grid.ColumnDefinitions>

                <!-- Left: Comparison Preview -->
                <Border Grid.Column='0' Background='#161B22' BorderBrush='#30363D' BorderThickness='1' CornerRadius='8' Padding='12'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                        </Grid.RowDefinitions>
                        <DockPanel Margin='0,0,0,6'>
                            <TextBlock Text='BEFORE / AFTER SPLIT COMPARISON' FontSize='11.5' FontWeight='Bold' Foreground='#8B949E' VerticalAlignment='Center'/>
                            <Button Name='BtnGenPreview' Content='Generate Snapshot' Style='{StaticResource SubButton}' DockPanel.Dock='Right' Height='26' Padding='8,0' FontSize='11'/>
                        </DockPanel>
                        <Border Name='BorderPreview' Grid.Row='1' Background='#06080B' BorderBrush='#21262D' BorderThickness='1' CornerRadius='6' ClipToBounds='True'>
                            <Grid>
                                <TextBlock Name='LblPreviewPlaceholder' Text='Click ""Generate Snapshot"" to preview color grade' Foreground='#484F58' FontSize='12' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                                <Image Name='ImgPreview' Stretch='Uniform' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/>
                            </Grid>
                        </Border>
                    </Grid>
                </Border>

                <!-- Right: Console Logs -->
                <Border Grid.Column='2' Background='#161B22' BorderBrush='#30363D' BorderThickness='1' CornerRadius='8' Padding='12'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                        </Grid.RowDefinitions>
                        <TextBlock Text='PIPELINE CONSOLE' FontSize='11.5' FontWeight='Bold' Foreground='#8B949E' Margin='0,0,0,6'/>
                        <Border Grid.Row='1' Background='#06080B' BorderBrush='#21262D' BorderThickness='1' CornerRadius='6' Padding='8'>
                            <TextBox Name='TxtLog' Background='Transparent' BorderThickness='0' FontFamily='Consolas' FontSize='11' Foreground='#00F2A9' IsReadOnly='True' VerticalScrollBarVisibility='Auto' TextWrapping='Wrap' Text='DJI LUT Studio v2.0.0 Native Engine Ready.'/>
                        </Border>
                    </Grid>
                </Border>
            </Grid>
        </Grid>

        <!-- Bottom Action Bar -->
        <Border Grid.Row='2' Background='#161B22' BorderBrush='#30363D' BorderThickness='0,1,0,0' Padding='20,10'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='*'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column='0' VerticalAlignment='Center' Margin='0,0,20,0'>
                    <ProgressBar Name='ProgressBar' Height='10' Minimum='0' Maximum='100' Value='0' Background='#21262D' Foreground='#00D2FF'/>
                    <Grid Margin='0,4,0,0'>
                        <TextBlock Name='LblStatus' Text='Ready to grade.' FontSize='11.5' Foreground='#8B949E'/>
                        <TextBlock Name='LblSpeedEta' Text='' FontSize='11' FontWeight='Bold' Foreground='#00D2FF' HorizontalAlignment='Right'/>
                    </Grid>
                </StackPanel>

                <Button Name='BtnWatcher' Grid.Column='1' Content='⏱ Watcher Mode' Style='{StaticResource SubButton}' Height='42' Padding='14,0' Margin='0,0,10,0'/>
                <Button Name='BtnOpenFolder' Grid.Column='2' Content='📁 Open Folder' Style='{StaticResource SubButton}' Height='42' Padding='14,0' Margin='0,0,12,0'/>
                <Button Name='BtnStartGrade' Grid.Column='3' Content='🚀 APPLY LUT &amp; GRADE' Style='{StaticResource PrimaryActionBtn}' Height='42' Padding='22,0'/>
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
            chkSharpen = (CheckBox)mainWindow.FindName("ChkSharpen");
            chkPreview = (CheckBox)mainWindow.FindName("ChkPreview");
            btnStartGrade = (Button)mainWindow.FindName("BtnStartGrade");
            btnOpenFolder = (Button)mainWindow.FindName("BtnOpenFolder");
            btnWatcher = (Button)mainWindow.FindName("BtnWatcher");
            btnGenPreview = (Button)mainWindow.FindName("BtnGenPreview");
            progressBar = (ProgressBar)mainWindow.FindName("ProgressBar");
            lblStatus = (TextBlock)mainWindow.FindName("LblStatus");
            lblSpeedEta = (TextBlock)mainWindow.FindName("LblSpeedEta");
            txtLog = (TextBox)mainWindow.FindName("TxtLog");
            imgPreview = (Image)mainWindow.FindName("ImgPreview");
            lblPreviewPlaceholder = (TextBlock)mainWindow.FindName("LblPreviewPlaceholder");
            borderPreview = (Border)mainWindow.FindName("BorderPreview");

            // Populate Dropdowns
            foreach (var key in deviceDatabase.Keys)
            {
                cmbDevice.Items.Add(key);
            }
            cmbDevice.SelectedIndex = 0;
            cmbDevice.SelectionChanged += (s, e) => PopulateLutsForSelectedDevice();
            PopulateLutsForSelectedDevice();

            cmbLook.Items.Add("Vibrant Pop (Recommended: Punchy Sky, Greens & Sharpness)");
            cmbLook.Items.Add("Natural Rec.709 (100% DJI Baseline Color Reference)");
            cmbLook.Items.Add("Cinematic Film (Filmic 35mm S-Curve, Rich Shadows)");
            cmbLook.Items.Add("Golden Hour (Warm Amber Sunlight & Sunset Glow)");
            cmbLook.Items.Add("Teal & Orange (Blockbuster Complementary Contrast)");
            cmbLook.SelectedIndex = 0;

            cmbCodec.Items.Add("HEVC / H.265 (10-Bit RTX NVENC Hardware)");
            cmbCodec.Items.Add("H.264 (8/10-Bit RTX NVENC Hardware)");
            cmbCodec.Items.Add("Apple ProRes 422 HQ (Broadcast Mastering)");
            cmbCodec.SelectedIndex = 0;

            cmbScale.Items.Add("Keep Source Resolution (Up to 4K 60fps)");
            cmbScale.Items.Add("Scale to 4K UHD (3840x2160)");
            cmbScale.Items.Add("Scale to 2.7K (2704x1520)");
            cmbScale.Items.Add("Scale to 1080p Proxy (Quick Discord / WhatsApp share)");
            cmbScale.SelectedIndex = 0;

            sliderCq.ValueChanged += (s, e) => {
                int val = (int)sliderCq.Value;
                lblCqVal.Text = val + (val <= 19 ? " (High)" : " (Standard)");
            };

            // Custom LUT Button
            Button btnCustomLut = (Button)mainWindow.FindName("BtnCustomLut");
            btnCustomLut.Click += (s, e) => {
                var dlg = new System.Windows.Forms.OpenFileDialog { Filter = "3D LUT Files (*.cube)|*.cube|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    cmbLut.Items.Add(new LutEntry(dlg.FileName, "Custom: " + Path.GetFileName(dlg.FileName), true));
                    cmbLut.SelectedIndex = cmbLut.Items.Count - 1;
                }
            };

            // Browse File & Folder
            btnBrowseFile.Click += (s, e) => {
                var dlg = new System.Windows.Forms.OpenFileDialog { Filter = "Video Files (*.mp4;*.mov;*.mkv)|*.mp4;*.mov;*.mkv|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    txtInputPath.Text = dlg.FileName;
                    AutoSelectDeviceForPath(dlg.FileName);
                }
            };

            btnBrowseFolder.Click += (s, e) => {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    txtInputPath.Text = dlg.SelectedPath;
                }
            };

            // Drag and Drop
            mainWindow.Drop += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0) {
                        txtInputPath.Text = files[0];
                        AutoSelectDeviceForPath(files[0]);
                    }
                }
            };

            // Action Buttons
            btnStartGrade.Click += (s, e) => StartGradingProcess();
            btnGenPreview.Click += (s, e) => RunPreviewGeneration();
            btnOpenFolder.Click += (s, e) => OpenGradedFolder();
            btnWatcher.Click += (s, e) => ToggleWatcher();

            if (!string.IsNullOrEmpty(initialFile)) {
                txtInputPath.Text = initialFile.Trim('"');
                AutoSelectDeviceForPath(initialFile);
            }
        }

        private static void InitDatabase()
        {
            deviceDatabase = new Dictionary<string, List<LutEntry>>();

            deviceDatabase["DJI O4 Pro / Air Unit"] = new List<LutEntry> {
                new LutEntry("o4_dlogm_rec709", "Official O4 D-Log M to Rec.709 V1", true)
            };
            deviceDatabase["DJI Avata 2"] = new List<LutEntry> {
                new LutEntry("avata2_dlogm_rec709", "Official Avata 2 D-Log M to Rec.709 V1", true)
            };
            deviceDatabase["DJI Avata 360"] = new List<LutEntry> {
                new LutEntry("avata360_dlogm_rec709", "Official Avata 360 D-Log M to Rec.709", true)
            };
            deviceDatabase["DJI Mavic 3 / Pro / Classic"] = new List<LutEntry> {
                new LutEntry("m3_dlog_rec709_std", "Official Mavic 3 D-Log to Rec.709 Standard", true),
                new LutEntry("m3_dlog_rec709_vivid", "Official Mavic 3 D-Log to Rec.709 (Vivid)"),
                new LutEntry("m3_dlogm_rec709", "Official Mavic 3 D-Log M to Rec.709")
            };
            deviceDatabase["DJI Mavic 4 Pro"] = new List<LutEntry> {
                new LutEntry("m4p_dlog_std", "Official Mavic 4 Pro D-Log to Rec.709 Standard", true),
                new LutEntry("m4p_dlog_vivid", "Official Mavic 4 Pro D-Log to Rec.709 (Vivid)"),
                new LutEntry("m4p_dlogm", "Official Mavic 4 Pro D-Log M to Rec.709")
            };
            deviceDatabase["DJI Air 3 / Air 3S"] = new List<LutEntry> {
                new LutEntry("air3_dlogm", "Official Air 3 D-Log M to Rec.709 V1", true),
                new LutEntry("air3s_dlogm", "Official Air 3S D-Log M to Rec.709 V1")
            };
            deviceDatabase["DJI Mini 4 Pro / Mini 5 Pro"] = new List<LutEntry> {
                new LutEntry("mini4_dlogm", "Official Mini 4 Pro D-Log M to Rec.709 V1", true),
                new LutEntry("mini5_dlogm", "Official Mini 5 Pro D-Log M to Rec.709")
            };
            deviceDatabase["DJI Osmo Pocket 3"] = new List<LutEntry> {
                new LutEntry("op3_dlogm", "Official Pocket 3 D-Log M to Rec.709 V1", true),
                new LutEntry("op3_spring", "Master Look: Spring Pro (Fresh Greens)"),
                new LutEntry("op3_summer", "Master Look: Summer Pro (Deep Cyan & Sun-Kissed)"),
                new LutEntry("op3_autumn", "Master Look: Autumn Pro (Warm Terracotta)"),
                new LutEntry("op3_winter", "Master Look: Winter Pro (Cool High-Contrast)")
            };
            deviceDatabase["DJI Osmo Pocket 4 / 4 Pro"] = new List<LutEntry> {
                new LutEntry("op4_dlog_std", "Official Pocket 4 D-Log to Rec.709 Standard", true),
                new LutEntry("op4_dlog_vivid", "Official Pocket 4 D-Log to Rec.709 (Vivid)"),
                new LutEntry("op4p_dlog2_std", "Official Pocket 4P D-Log2 to Rec.709"),
                new LutEntry("op4p_dlog2_vivid", "Official Pocket 4P D-Log2 to Rec.709 (Vivid)")
            };
            deviceDatabase["DJI Osmo Action 4"] = new List<LutEntry> {
                new LutEntry("oa4_dlogm", "Official Action 4 D-Log M to Rec.709 V1", true),
                new LutEntry("oa4_ice", "Action Look: Ice Pro (Snow Sports)"),
                new LutEntry("oa4_nature", "Action Look: Nature Pro (Warm Sunlight)"),
                new LutEntry("oa4_forest", "Action Look: Forest Pro (Deep Moss & Trail)")
            };
            deviceDatabase["DJI Osmo Action 5 Pro"] = new List<LutEntry> {
                new LutEntry("oa5p_dlogm", "Official Action 5 Pro D-Log M to Rec.709 V1", true),
                new LutEntry("oa5p_mei", "Master Look: Mei / Plum (Romantic Magenta)"),
                new LutEntry("oa5p_ju", "Master Look: Ju / Chrysanthemum (Golden Sunset)"),
                new LutEntry("oa5p_zhu", "Master Look: Zhu / Bamboo (Vibrant Green)"),
                new LutEntry("oa5p_lan", "Master Look: Lan / Orchid (Oceanic Teal)")
            };
            deviceDatabase["DJI Osmo Action 6"] = new List<LutEntry> {
                new LutEntry("oa6_dlogm", "Official Action 6 D-Log M to Rec.709", true)
            };
            deviceDatabase["DJI Osmo 360 / Nano / Lito"] = new List<LutEntry> {
                new LutEntry("osmo_360_dlogm", "Official Osmo 360 Panoramic D-Log M", true),
                new LutEntry("osmo_nano_dlogm", "Official Osmo Nano D-Log M"),
                new LutEntry("lito_x1_dlogm", "Official Lito X1 D-Log M")
            };
            deviceDatabase["DJI Zenmuse X7 / Ronin 4D / Inspire 3"] = new List<LutEntry> {
                new LutEntry("x7_dlog_rec709", "Official Cinema D-Log to Rec.709", true),
                new LutEntry("x7_linear_dlog", "Official Linear RAW to D-Log")
            };
            deviceDatabase["DJI Phantom 4 / Phantom 4 Pro"] = new List<LutEntry> {
                new LutEntry("p4_dlog_rec709", "Official Phantom 4 D-Log to Rec.709", true),
                new LutEntry("p4_dlog_srgb", "Official Phantom 4 D-Log to sRGB Improv")
            };
            deviceDatabase["DJI Generic / Universal D-Log M"] = new List<LutEntry> {
                new LutEntry("generic_dlogm_rec709", "Universal DJI D-Log M to Rec.709", true)
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

        private static void AutoSelectDeviceForPath(string path)
        {
            string upper = Path.GetFileName(path).ToUpperInvariant();
            if (upper.Contains("AVATA")) cmbDevice.SelectedItem = "DJI Avata 2";
            else if (upper.Contains("MAVIC3") || upper.Contains("M3")) cmbDevice.SelectedItem = "DJI Mavic 3 / Pro / Classic";
            else if (upper.Contains("MAVIC4") || upper.Contains("M4")) cmbDevice.SelectedItem = "DJI Mavic 4 Pro";
            else if (upper.Contains("POCKET3") || upper.Contains("OP3")) cmbDevice.SelectedItem = "DJI Osmo Pocket 3";
            else if (upper.Contains("POCKET4") || upper.Contains("OP4")) cmbDevice.SelectedItem = "DJI Osmo Pocket 4 / 4 Pro";
            else if (upper.Contains("ACTION4") || upper.Contains("OA4")) cmbDevice.SelectedItem = "DJI Osmo Action 4";
            else if (upper.Contains("ACTION5") || upper.Contains("OA5")) cmbDevice.SelectedItem = "DJI Osmo Action 5 Pro";
            else if (upper.Contains("AIR3")) cmbDevice.SelectedItem = "DJI Air 3 / Air 3S";
            else if (upper.Contains("MINI4") || upper.Contains("MINI5")) cmbDevice.SelectedItem = "DJI Mini 4 Pro / Mini 5 Pro";
            else cmbDevice.SelectedItem = "DJI O4 Pro / Air Unit";
        }

        private static void StartGradingProcess()
        {
            if (activeProcess != null && !activeProcess.HasExited)
            {
                MessageBox.Show("A grading job is currently running.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string inputPath = txtInputPath.Text.Trim('"');
            if (string.IsNullOrEmpty(inputPath) || (!File.Exists(inputPath) && !Directory.Exists(inputPath)))
            {
                MessageBox.Show("Please enter a valid file or folder path.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            string sharpenFlag = (chkSharpen.IsChecked == true) ? "--sharpen" : "";
            string previewFlag = (chkPreview.IsChecked == true) ? "-p" : "";

            btnStartGrade.IsEnabled = false;
            btnStartGrade.Content = "GRADING...";
            progressBar.Value = 0;
            lblStatus.Text = "Grading with NVIDIA RTX NVENC in progress...";
            lblSpeedEta.Text = "";

            txtLog.AppendText("\n========================================\n");
            txtLog.AppendText(string.Format("[START] Video: {0}\n", Path.GetFileName(inputPath)));
            txtLog.AppendText(string.Format("[LUT] {0} ({1})\n", lutEntry != null ? lutEntry.DisplayName : lutId, lutId));
            txtLog.AppendText(string.Format("[LOOK] {0} | [CODEC] {1} | [CQ] {2}\n", look.ToUpperInvariant(), codec.ToUpperInvariant(), cq));

            workerThread = new Thread(() =>
            {
                try
                {
                    string args = string.Format("-d {0} -e /usr/bin/python3 {1}/dji_lut_studio/cli.py \"{2}\" -l {3} --look {4} -c {5} --cq {6} --scale {7} {8} {9} --overwrite",
                        wslDistro, appDir, inputPath, lutId, look, codec, cq, scale, sharpenFlag, previewFlag);

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
                        lblStatus.Text = "Error: " + ex.Message;
                        txtLog.AppendText("[EXCEPTION] " + ex.Message + "\n");
                    }));
                }
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private static void ParseProcessOutput(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            mainWindow.Dispatcher.Invoke(new Action(() =>
            {
                txtLog.AppendText(line + "\n");
                txtLog.ScrollToEnd();

                Match mPct = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
                if (mPct.Success)
                {
                    double pct = double.Parse(mPct.Groups[1].Value);
                    progressBar.Value = Math.Min(100, Math.Max(0, (int)pct));
                    lblStatus.Text = string.Format("Encoding: {0:F1}%", pct);
                }

                Match mSpd = Regex.Match(line, @"\|\s*([^\n\r]+?)\s*\|");
                if (mSpd.Success)
                {
                    lblSpeedEta.Text = mSpd.Groups[1].Value.Trim();
                }
            }));
        }

        private static void RunPreviewGeneration()
        {
            string inputPath = txtInputPath.Text.Trim('"');
            if (!File.Exists(inputPath))
            {
                MessageBox.Show("Please select a single video file to preview.", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LutEntry lutEntry = cmbLut.SelectedItem as LutEntry;
            string lutId = lutEntry != null ? lutEntry.Id : "o4_dlogm_rec709";
            string[] lookMap = { "vibrant", "natural", "cinematic", "golden", "teal_orange" };
            string look = lookMap[Math.Max(0, cmbLook.SelectedIndex)];

            btnGenPreview.IsEnabled = false;
            btnGenPreview.Content = "Generating...";

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string outDir = Path.Combine(Path.GetDirectoryName(inputPath), "graded");
                    string outPrev = Path.Combine(outDir, Path.GetFileNameWithoutExtension(inputPath) + "_preview.jpg");

                    string script = string.Format(
                        "from dji_lut_studio.manifest import manifest; " +
                        "from dji_lut_studio.engine import generate_preview_snapshot; " +
                        "from pathlib import Path; " +
                        "p_lut = manifest.resolve_lut_file('{0}'); " +
                        "generate_preview_snapshot(Path('{1}'), p_lut, Path('{2}'), look_style='{3}', sharpen=True)",
                        lutId, inputPath.Replace("\\", "/"), outPrev.Replace("\\", "/"), look
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
            string inPath = txtInputPath.Text.Trim('"');
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
            string inPath = txtInputPath.Text.Trim('"');
            if (string.IsNullOrEmpty(inPath) || !Directory.Exists(inPath))
            {
                MessageBox.Show("Please select a folder to watch (e.g. D:\\dji\\incoming).", "DJI LUT Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string outDir = Path.Combine(inPath, "graded");
            string args = string.Format("-d {0} -e /usr/bin/python3 {1}/dji_lut_studio/cli.py -w \"{2}\" -o \"{3}\" --look vibrant -p",
                wslDistro, appDir, inPath, outDir);

            Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"DJI LUT Studio - Folder Watcher\" wsl.exe " + args));
        }
    }
}
