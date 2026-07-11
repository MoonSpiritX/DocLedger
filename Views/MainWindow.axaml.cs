using System;
using System.IO;
using Avalonia.Controls;

namespace 文件台账工具Ava.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                try
                {
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AvaIcon.ico");
                    if (File.Exists(iconPath))
                        Icon = new WindowIcon(iconPath);
                }
                catch
                {
                    // 图标加载失败时使用系统默认图标
                }
            }
        }
    }
}
