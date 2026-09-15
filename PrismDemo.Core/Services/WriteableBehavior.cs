using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Views;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PrismDemo.Core.Services
{
    /// <summary>
    /// 可写入控件的附加行为（Attached Property）
    /// 
    /// 使用方式：在 XAML 中为 TextBlock 等控件添加以下属性即可启用点击写入功能
    /// 
    ///   xmlns:core="clr-namespace:PrismDemo.Core.Services;assembly=PrismDemo.Core"
    /// 
    ///   &lt;TextBlock 
    ///       core:WriteableBehavior.IsEnabled="True"
    ///       core:WriteableBehavior.WriteableService="{Binding}"
    ///       core:WriteableBehavior.ConnectData="{Binding}" /&gt;
    /// 
    /// 属性说明：
    ///   - IsEnabled: True 表示点击可触发写入，False 表示只读
    ///   - WriteableService: 绑定到实现了 IWriteableService 的服务（用于写入 OPC）
    ///   - ConnectData: 绑定的 ConnectData 对象，包含 OpcAddress、Type、Value 等信息
    /// </summary>
    public static class WriteableBehavior
    {
        #region IsEnabled - 控制是否可点击写入

        /// <summary>
        /// 附加属性：是否启用点击写入功能
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(WriteableBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
            => (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value)
            => obj.SetValue(IsEnabledProperty, value);

        #endregion

        #region WriteableService - 写入服务

        /// <summary>
        /// 附加属性：写入服务（需要实现 IWriteableService 接口）
        /// </summary>
        public static readonly DependencyProperty WriteableServiceProperty =
            DependencyProperty.RegisterAttached(
                "WriteableService",
                typeof(IWriteableService),
                typeof(WriteableBehavior),
                new PropertyMetadata(null));

        public static IWriteableService GetWriteableService(DependencyObject obj)
            => (IWriteableService)obj.GetValue(WriteableServiceProperty);

        public static void SetWriteableService(DependencyObject obj, IWriteableService value)
            => obj.SetValue(WriteableServiceProperty, value);

        #endregion

        #region ConnectData - OPC 数据对象

        /// <summary>
        /// 附加属性：OPC 数据对象（包含 OpcAddress、Type、Value 等）
        /// </summary>
        public static readonly DependencyProperty ConnectDataProperty =
            DependencyProperty.RegisterAttached(
                "ConnectData",
                typeof(ConnectData),
                typeof(WriteableBehavior),
                new PropertyMetadata(null));

        public static ConnectData GetConnectData(DependencyObject obj)
            => (ConnectData)obj.GetValue(ConnectDataProperty);

        public static void SetConnectData(DependencyObject obj, ConnectData value)
            => obj.SetValue(ConnectDataProperty, value);

        #endregion

        /// <summary>
        /// 当 IsEnabled 属性变化时触发
        /// </summary>
        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            if ((bool)e.NewValue)
            {
                // 启用：设置手型光标，订阅鼠标点击事件
                element.Cursor = Cursors.Hand;
                element.MouseLeftButtonDown += OnElementMouseLeftButtonDown;
            }
            else
            {
                // 禁用：恢复箭头光标，取消订阅鼠标点击事件
                element.Cursor = Cursors.Arrow;
                element.MouseLeftButtonDown -= OnElementMouseLeftButtonDown;
            }
        }

        /// <summary>
        /// 处理控件点击事件
        /// 逻辑：
        ///   1. 获取 WriteableService（写入服务）
        ///   2. 获取 ConnectData（OPC 数据）
        ///   3. 根据 Type 决定处理方式：
        ///      - bool/boolean: 直接切换值（ToggleBoolValue），后台异步写入不阻塞UI
        ///      - 其他类型: 弹出输入对话框，输入后后台异步写入（WriteValue）
        /// </summary>
        private static async void OnElementMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            var writeableService = GetWriteableService(element);
            if (writeableService == null)
                return;

            var connectData = GetConnectData(element);
            if (connectData == null && element.DataContext is ConnectData dc)
            {
                connectData = dc;
            }

            if (connectData == null)
                return;

            var type = connectData.Type?.ToLowerInvariant() ?? "";

            if (type == "bool" || type == "boolean")
            {
                var newValue = !Convert.ToBoolean(connectData.Value);
                // 后台线程写入，不阻塞UI
                await Task.Run(() => writeableService.WriteValue(connectData, newValue));
                connectData.Value = newValue;
            }
            else
            {
                var window = Window.GetWindow(element);
                var inputDialog = new InputDialog(
                    connectData.Name,
                    connectData.Value?.ToString() ?? ""
                );
                inputDialog.Owner = window;
                if (inputDialog.ShowDialog() == true)
                {
                    var convertedValue = ConvertToType(inputDialog.InputValue, type);
                    // 后台线程写入，不阻塞UI
                    await Task.Run(() => writeableService.WriteValue(connectData, convertedValue));
                    connectData.Value = convertedValue;
                }
            }
        }

        /// <summary>
        /// 将字符串转换为指定类型
        /// </summary>
        private static object ConvertToType(string value, string type)
        {
            return type switch
            {
                "float" => float.Parse(value),
                "double" => double.Parse(value),
                "int" or "int32" => int.Parse(value),
                "short" => short.Parse(value),
                _ => value
            };
        }
    }
}