﻿using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SeriousWebcamSettings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VideoCaptureDevice _webcam = null;
        private NewFrameEventHandler _newFrameHandler = null;
        List<CameraVideoSettingEntity> _currentCameraSettings = null;
        private bool _closing = false;
        private Timer _autoRefreshTimer = null;
        private string _videoDevice = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AForge.Video.DirectShow.VideoCaptureDeviceForm();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (_newFrameHandler != null)
                {
                    _webcam.NewFrame -= _newFrameHandler;
                    _webcam.SignalToStop();
                }

                _newFrameHandler = new NewFrameEventHandler(FinalVideo_NewFrame);

                _webcam = dlg.VideoDevice;
                _webcam.NewFrame += _newFrameHandler;

                InitializeCameraSettings();

                _webcam.Start();

                _chkAutoRefresh.IsChecked = true;

                _videoDevice = dlg.VideoDevice.Name;

                _btnChooseDevice.IsEnabled = true;
                _btnShowDisplayProperties.IsEnabled = true;
                _btnSave.IsEnabled = true;
                _btnLoad.IsEnabled = true;
                _btnForceRefresh.IsEnabled = true;
                _chkAutoRefresh.IsEnabled = true;
            }
        }

        private void FinalVideo_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!_closing)
            {
                Dispatcher.Invoke(() => _imgFrame.Source = BitmapToImageSource(eventArgs.Frame));
            }
        }

        private ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _closing = true;

            if (_newFrameHandler != null)
            {
                _webcam.NewFrame -= _newFrameHandler;
                _webcam.SignalToStop();
            }
        }

        private void _btnShowDisplayProperties_Click(object sender, RoutedEventArgs e)
        {
            if (_webcam != null)
            {
                _webcam.DisplayPropertyPage(new WindowInteropHelper(this).Handle);
            }
        }

        private void _btnForceRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_webcam == null || _currentCameraSettings == null)
            {
                return;
            }

            SetCameraValues();
        }

        private void _chkAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            StartAutoRefresh();
        }

        private void _chkAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            StopAutoRefresh();
        }

        private void SetCameraValues()
        {
            if (_webcam == null || _currentCameraSettings == null)
            {
                return;
            }

            foreach (var setting in _currentCameraSettings)
            {
                SetCameraValue(setting);
            }
        }

        private void SetCameraValue(CameraVideoSettingEntity setting)
        {
            if (_webcam == null || _currentCameraSettings == null)
            {
                return;
            }

            if (setting.Setting < CameraVideoSettingProperty.Brightness)
            {
                _webcam.SetCameraProperty(
                    (CameraControlProperty)setting.Setting,
                    setting.Value,
                    setting.ControlValue);
            }
            else
            {
                _webcam.SetVideoProperty(
                    (VideoProcAmpProperty)(setting.Setting - CameraVideoSettingProperty.Brightness),
                    setting.Value,
                    (VideoProcAmpFlags)setting.ControlValue);
            }
        }


        private void InitializeCameraSettings()
        {
            if (_webcam == null)
            {
                return;
            }

            _settingsControl.Children.Clear();
            _lblUnsupportedSettings.Content = "";


            List<string> unsupported = new List<string>();
            _currentCameraSettings = new List<CameraVideoSettingEntity>();

            foreach (var settingKind in Enum.GetValues<CameraVideoSettingProperty>())
            {
                var setting = GetCameraValue(settingKind);
                _currentCameraSettings.Add(setting);
                if (setting.ControlRange == CameraControlFlags.None)
                {
                    unsupported.Add(setting.Setting.ToString());
                }
                else
                {
                    _settingsControl.Children.Add(new CameraVideoSettingControl(setting));
                }
            }

            _lblUnsupportedSettings.Content = String.Join(", ", unsupported);
            _lblUnsupportedSettingsHeader.Visibility = Visibility.Visible;
        }

        private CameraVideoSettingEntity GetCameraValue(CameraVideoSettingProperty cameraVideoSetting)
        {
            CameraControlFlags returnValueFlag = CameraControlFlags.None;
            CameraControlFlags returnRangeFlag = CameraControlFlags.None;
            int returnValue = 0;
            int minValue = 0;
            int maxValue = 0;
            int stepSizeValue = 0;
            int defaultValue = 0;

            if (cameraVideoSetting < CameraVideoSettingProperty.Brightness)
            {
                _webcam.GetCameraProperty(
                    (CameraControlProperty)cameraVideoSetting,
                    out returnValue,
                    out returnValueFlag);

                _webcam.GetCameraPropertyRange(
                    (CameraControlProperty)cameraVideoSetting,
                    out minValue,
                    out maxValue,
                    out stepSizeValue,
                    out defaultValue,
                    out returnRangeFlag);
            }
            else
            {
                VideoProcAmpFlags videoReturnValueFlag = VideoProcAmpFlags.None;
                VideoProcAmpFlags videoReturnRangeFlag = VideoProcAmpFlags.None;

                _webcam.GetVideoProperty(
                    (VideoProcAmpProperty)(cameraVideoSetting - CameraVideoSettingProperty.Brightness),
                    out returnValue,
                    out videoReturnValueFlag);

                _webcam.GetVideoPropertyRange(
                    (VideoProcAmpProperty)(cameraVideoSetting - CameraVideoSettingProperty.Brightness),
                    out minValue,
                    out maxValue,
                    out stepSizeValue,
                    out defaultValue,
                    out videoReturnRangeFlag);

                returnValueFlag = (CameraControlFlags)videoReturnValueFlag;
                returnRangeFlag = (CameraControlFlags)videoReturnRangeFlag;
            }

            var returnObject = new CameraVideoSettingEntity()
            {
                ControlValue = returnValueFlag,
                ControlRange = returnRangeFlag,
                Default = defaultValue,
                Max = maxValue,
                Min = minValue,
                Setting = cameraVideoSetting,
                StepSize = stepSizeValue,
                Value = returnValue
            };

            returnObject.SaveCallback = () => SetCameraValue(returnObject);

            return returnObject;
        }

        private void StartAutoRefresh()
        {
            if (_autoRefreshTimer == null)
            {
                _autoRefreshTimer = new Timer(AutoRefreshTimerTick, null, 1000, 1000);
            }
        }

        private void AutoRefreshTimerTick(object? state)
        {
            SetCameraValues();
        }

        private void StopAutoRefresh()
        {
            if (_autoRefreshTimer != null)
            {
                _autoRefreshTimer.Dispose();
                _autoRefreshTimer = null;
            }
        }

        private void _btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_webcam == null)
            {
                return;
            }

            var dlg = new System.Windows.Forms.SaveFileDialog()
            {
                FileName = _videoDevice + ".sws", // Default file name
                DefaultExt = ".sws", // Default file extension
                Filter = "Serious Webcam Settings|*.sws|All files|*.*", // Filter files by extension
                InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory
            };

            // Show save file dialog box
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Save settings
                var configs = new StringBuilder();
                foreach (var setting in _currentCameraSettings)
                {
                    configs.AppendLine(setting.Setting.ToString() + " = " + (setting.IsAuto ? "Auto" : setting.Value.ToString()));
                }

                File.WriteAllText(dlg.FileName, configs.ToString());
            }
        }

        private void _btnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (_webcam == null)
            {
                return;
            }

            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                FileName = _videoDevice + ".sws", // Default file name
                DefaultExt = ".sws", // Default file extension
                Filter = "Serious Webcam Settings|*.sws|All files|*.*", // Filter files by extension
                InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory
            };

            // Show save file dialog box
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var configs = new Dictionary<string, string>();
                // Save settings
                foreach (string line in File.ReadAllLines(dlg.FileName))
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var config = line.Split("=");
                    configs[config[0].Trim()] = config[1].Trim();
                }

                foreach (var setting in _currentCameraSettings)
                {
                    var settingName = setting.Setting.ToString();
                    if (configs.ContainsKey(settingName))
                    {
                        if (configs[settingName] == "Auto")
                        {
                            setting.ControlValue = CameraControlFlags.Auto;
                            setting.RaisePropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("IsAuto"));
                        }
                        else
                        {
                            setting.Value = int.Parse(configs[settingName]);
                            setting.RaisePropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("Value"));
                        }


                    }
                }

                SetCameraValues();
            }

        }
    }
}
