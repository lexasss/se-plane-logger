using Richa.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows;

namespace Richa.Logging;

internal class Statistics
{
    public static Statistics Instance => _instance ??= new();

    public void SetStage(string drivingStage, bool isDriverBusyWithTask)
    {
        _drivingStage = drivingStage;
        _isDriverBusyWithTask = isDriverBusyWithTask;
    }

    public void SetStage(string drivingStage) => _drivingStage = drivingStage;
    public void SetStage(bool isDriverBusyWIthTask) => _isDriverBusyWithTask = isDriverBusyWIthTask;

    public void Feed(string name, Plane.Plane.Event evt)
    {
        if (string.IsNullOrEmpty(_drivingStage))
            return;

        string key = MakePlaneKey(name);

        if (!_planes.ContainsKey(key))
        {
            _planes.Add(key, new Entry());
        }

        var entry = _planes[key];
        entry.Feed(evt);
    }

    public bool SaveTo(string filename)
    {
        var lines = _planes.Select(item =>
        {
            var p = item.Key.Split('_');
            var (planeName, drivingStage, focus) = (p[0], p[1], bool.Parse(p[2]) ? "task" : "road");
            return $"{drivingStage}\t{focus}\t{planeName}\t{item.Value.TotalTime}";
        }).ToImmutableSortedSet();

        return Save(filename, lines);
    }

    // Internal methods

    class Entry
    {
        public long TotalTime => _totalTime;

        public void Feed(Plane.Plane.Event evt)
        {
            if (evt == Plane.Plane.Event.Enter)
            {
                _startedAt = Timestamp.Ms;
            }
            else if (_startedAt > 0)
            {
                _totalTime += Timestamp.Ms - _startedAt;
                _startedAt = 0;
            }
        }

        // Internal

        private long _totalTime = 0;
        private long _startedAt = 0;
    }

    static Statistics? _instance = null;

    Dictionary<string, Entry> _planes = new();
    string? _drivingStage = null;
    bool _isDriverBusyWithTask = false;

    private string MakePlaneKey(string name) => $"{name}_{_drivingStage}_{_isDriverBusyWithTask}";

    private static bool Save(string filename, IEnumerable<object> records, string header = "")
    {
        if (!Path.IsPathFullyQualified(filename))
        {
            filename = Path.Combine(FlowLogger.Instance.Folder, filename);
        }

        var folder = Path.GetDirectoryName(filename) ?? "";

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        using StreamWriter writer = File.CreateText(filename);

        try
        {
            if (!string.IsNullOrEmpty(header))
            {
                writer.WriteLine(header);
            }

            writer.WriteLine(string.Join("\n", records));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save data into\n'{filename}':\n\n{ex.Message}",
                Application.Current.MainWindow.Title + " - Statistics",
                MessageBoxButton.OK);
        }

        return false;
    }
}
