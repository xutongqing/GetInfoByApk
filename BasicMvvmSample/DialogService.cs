using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace BasicMvvmSample;

public static class DialogService
{
    public static Task<bool> ShowConfirmAsync(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            var okBtn = new Button { Content = "OK", Width = 90 };
            var cancelBtn = new Button { Content = "Cancel", Width = 90 };

            var wnd = new Window
            {
                Title = title,
                Width = 420,
                Height = 180,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Margin = new Avalonia.Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, okBtn }
                        }
                    }
                }
            };

            void Close(bool result)
            {
                if (!tcs.TrySetResult(result)) return;
                wnd.Close();
            }

            okBtn.Click += (_, __) => Close(true);
            cancelBtn.Click += (_, __) => Close(false);
            wnd.Closed += (_, __) => tcs.TrySetResult(false); // 右上角关闭 => Cancel

            _ = wnd.ShowDialog(owner); // 不 await，结果由按钮回调决定
        });

        return tcs.Task;
    }
}