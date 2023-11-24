using Microsoft.VisualBasic;
using Richa.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Richa
{
    public partial class Main : Page, IDisposable
    {
        public Main(SEClient.Tcp.Client? tcpClient)
        {
            InitializeComponent();

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

        private void SaveLoggedData()
        {
            if (_logger.HasRecords)
            {
                _logger.IsEnabled = false;
                _logger.SaveTo($"richa_{DateTime.Now:u}.txt".ToPath());
            }
        }

        // Handlers

        private void DataSource_Closed(object? sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    SaveLoggedData();
                });
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
            _logger.Add(LogSource.Driver, btn.Tag.ToString()!, btn.Content.ToString()!);
        }

        private void Stop_Click(object? _, RoutedEventArgs e)
        {
            _handler.Reset();
            Finalize();
            Application.Current.Shutdown();
        }
    }
}
