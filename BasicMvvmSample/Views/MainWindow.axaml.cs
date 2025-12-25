using Avalonia.Controls;
using Avalonia.Interactivity;
using BasicMvvmSample.ViewModels;

namespace BasicMvvmSample.Views
{
    public partial class MainWindow : Window
    {
        private readonly DumpViewModel _vm = new();
        private DumpClientRunner? _runner;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            _runner = new DumpClientRunner(this, _vm);

            Closing += (_, __) =>
            {
                _runner?.Stop();
            };
        }

        private async void Start_Click(object? sender, RoutedEventArgs e)
        {
            _runner ??= new DumpClientRunner(this, _vm);
            await _runner.StartAsync();
        }

        private void Stop_Click(object? sender, RoutedEventArgs e)
        {
            _runner?.Stop();
        }
    }
}
