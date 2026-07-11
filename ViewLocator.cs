using Avalonia.Controls;
using Avalonia.Controls.Templates;
using 文件台账工具Ava.ViewModels;
using 文件台账工具Ava.Views;

namespace 文件台账工具Ava
{
    /// <summary>
    /// 编译安全的 ViewLocator（兼容 AOT，不依赖反射）
    /// /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is ViewModelBase)
            {
                // 主窗口直接包含 MappingPanel View，不需要通过 ViewLocator 解析
                return null;
            }
            return new TextBlock { Text = "未找到视图: " + param?.GetType().Name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
