using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BasicMvvmSample.ViewModels;

public sealed class DumpViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> Logs { get; } = new();

    double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    string _status = "Idle";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); }
    }

    string _address = "https://localhost:5001"; // 改成你的
    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public void Log(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"{ts} {line}");
        if (Logs.Count > 2000) Logs.RemoveAt(0); // 简单截断
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}