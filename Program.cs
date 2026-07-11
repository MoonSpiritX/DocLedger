using Avalonia;
using System;
using System.Threading;

namespace 文件台账工具Ava
{
    internal sealed class Program
    {
        private const string MutexName = "AvaLedger_SingleInstance_7E4B2A1C";

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            using var mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                // 另一个实例已在运行，直接退出
                return;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
    }
}
