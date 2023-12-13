using Richa.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Richa;

public static class ButtonClass
{
    public static readonly string DrivingStage = "DrivingStage";
    public static readonly string Task = "Task";
}
public static class DrivingStage
{
    public static readonly string Manual = "Manual";
    public static readonly string Critical = "Critical";
    public static readonly string NonCritical = "NonCritical";
}


public class InvertConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class Main : Page, IDisposable, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsDriverBusyWithTask
    {
        get => _isDriverBusyWithTask;
        set
        {
            _isDriverBusyWithTask = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDriverBusyWithTask)));
        }
    }

    public Main(SEClient.Tcp.Client? tcpClient)
    {
        InitializeComponent();

        DataContext = this;

        _tcpClient = tcpClient;

        if (_tcpClient != null)
        {
            _tcpClient.Disconnected += DataSource_Closed;
            _tcpClient.Sample += TcpClient_Sample;
        }

        ScreenLogger.Initialize(txbOutput, wrpScreenLogger);

        _handler = new PlaneIntersectionHander(new Dictionary<Panel, Plane.Plane>()
        {
            { stpWindshield, new Plane.Plane("Windshield") },
            { stpLeftMirror, new Plane.Plane("LeftMirror") },
            { stpLeftDashboard, new Plane.Plane("LeftDashboard") },
            { stpRearView, new Plane.Plane("RearView") },
            { stpCentralConsole, new Plane.Plane("CentralConsole") },
            { stpRightMirror, new Plane.Plane("RightMirror") },
        });

        btnFinished.Click += Stop_Click;
    }

    public void Finalize()
    {
        SaveLoggedData();

        if (_tcpClient?.IsConnected ?? false)
        {
            _tcpClient.Stop();
        }
    }

    public void Dispose()
    {
        _tcpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly SEClient.Tcp.Client? _tcpClient;
    readonly PlaneIntersectionHander _handler;

    readonly FlowLogger _logger = FlowLogger.Instance;
    readonly Statistics _statistics = Statistics.Instance;

    bool _isDriverBusyWithTask = false;

    private void SaveLoggedData()
    {
        if (_logger.HasRecords)
        {
            _logger.IsEnabled = false;
            var timestamp = $"{DateTime.Now:u}";
            if (_logger.SaveTo($"richa_{timestamp}.txt".ToPath()) == SavingResult.Save)
            {
                _statistics.SaveTo($"richa_{timestamp}_stat.txt".ToPath());
            }
        }
    }

    private void ShowManualDrivingTimer()
    {
        string ToTime(long seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        long manualDrivingDuration = 60*5; // seconds
        int tickInterval = 200;
        long endsAt = DateTime.Now.Ticks + manualDrivingDuration * 10_000_000;
        var timer = new Timer(tickInterval)
        {
            AutoReset = true
        };
        timer.Elapsed += (s, e) =>
        {
            var leftSeconds = (int)((endsAt - DateTime.Now.Ticks) / 10_000_000);
            if (DateTime.Now.Ticks >= endsAt)
            {
                System.Media.SystemSounds.Beep.Play();
                Dispatcher.Invoke(() =>
                {
                    lblManualDrivingDurationLeft.Visibility = Visibility.Hidden;
                });
                timer.Stop();
            }
            else
            {
                Dispatcher.Invoke(() => lblManualDrivingDurationLeft.Content = ToTime(leftSeconds));
            }
        };
        timer.Start();

        lblManualDrivingDurationLeft.Content = ToTime(manualDrivingDuration);
        lblManualDrivingDurationLeft.Visibility = Visibility.Visible;
    }

    // Handlers

    private void DataSource_Closed(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.Invoke(SaveLoggedData);
        }
        catch (TaskCanceledException) { }
    }

    private void TcpClient_Sample(object? sender, SEClient.Tcp.Data.Sample sample)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                _handler.Feed(sample);
            });
        }
        catch (TaskCanceledException) { }
    }

    // UI

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.IsDebugging)
        {
            lblDebug.Visibility = Visibility.Visible;
        }
    }

    private void ControlButton_Click(object sender, RoutedEventArgs e)
    {
        Button btn = (sender as Button)!;
        var buttonClass = btn.Tag.ToString()!;
        var data = btn.Content.ToString()!;
        _logger.Add(LogSource.Driver, buttonClass, data);

        if (buttonClass == ButtonClass.DrivingStage)
        {
            btn.IsEnabled = false;
            var drivingStage = data;
            _statistics.SetStage(drivingStage);

            if (data == DrivingStage.Manual)
            {
                ShowManualDrivingTimer();
            }
        }
        else if (buttonClass == ButtonClass.Task)
        {
            IsDriverBusyWithTask = !IsDriverBusyWithTask;
            _statistics.SetStage(IsDriverBusyWithTask);
        }
    }

    private void Stop_Click(object? _, RoutedEventArgs e)
    {
        _handler.Reset();
        Finalize();
        Application.Current.Shutdown();
    }
}
