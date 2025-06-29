using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AK.Wwise.Waapi;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WwiseTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AK.Wwise.Waapi.JsonClient client;
        public JObject rtpc;
        public string[] banks;
        public string[] Ids;
        string[][] bankEventArray;
        public MainWindow()
        {
            InitializeComponent();
        }
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用类级别的 client
                if (client == null)
                    client = new AK.Wwise.Waapi.JsonClient();

                ConnectionStatusTextBlock.Text = $"正在连接到: ws://localhost:{PortInput.Text}/waapi";
                await client.Connect($"ws://localhost:{PortInput.Text}/waapi");

                if (client.IsConnected())
                {
                    ConnectionStatusTextBlock.Text = "连接成功！";
                }
                else
                {
                    ConnectionStatusTextBlock.Text = "连接失败！请检查Wwise是否正在运行。";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusTextBlock.Text = "Error: " + ex.Message;
            }
        }

        private void PortInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(PortInput.Text, out int port) && port >= 1 && port <= 65535)
            {
                if (ConnectButton != null)
                {
                    ConnectButton.IsEnabled = true;
                }
            }
            else
            {
                ConnectButton.IsEnabled = false;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.IsConnected())
            {
                ConnectionStatusTextBlock.Text = "请先连接到Wwise！";
                return;
            }

            string the_Guid = Be_copied_guid.Text;
            string Select_g = Select.Text;

            // 构建 waql 字符串，包含转义的双引号
            string waqlValue = $"\"{the_Guid}\"";

            // 使用 JObject 构造 JSON 参数
            var args = new JObject
            {
                ["waql"] = waqlValue
            };
            try
            {
                JObject result = await client.Call("ak.wwise.core.object.get", args,
                     new
                     {
                          @return = new[] { "@RTPC" }
                     });

                rtpc = result;

                ConnectionStatusTextBlock.Text = result.ToString();
            }
            catch (Exception ex)
            {
                ConnectionStatusTextBlock.Text = "Error: " + ex.Message;
            }
        }

                private async void copybutton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.IsConnected())
            {
                ConnectionStatusTextBlock.Text = "请先连接到Wwise！";
                return;
            }

            string the_Guid = Select.Text;

            var ids = rtpc["return"]?
            .OfType<JObject>()?
            .FirstOrDefault()? // 取第一个 return 元素
            ["@RTPC"]?
            .OfType<JObject>()?
            .Select(jo => (string)jo["id"])?
            .ToArray();


            var stringArray = the_Guid
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("{") && line.EndsWith("}"))
            .ToArray();


            foreach (var id in ids)
            {
                string waqlValue = $"\"{id}\"";
                var args = new JObject
                {
                    ["waql"] = waqlValue
                };
                try
                {
                    JObject result = await client.Call("ak.wwise.core.object.get", args,
                         new
                         {
                             @return = new[] { "type",
                             "path",
                             "@PropertyName",
                             "@ControlInput",
                             "@Curve" }
                         });
                    // 提取 return 数组中的第一个对象
                    JObject data = (JObject)result["return"][0];

                    // 提取 @Curve 的 points
                    JArray points = (JArray)data["@Curve"]["points"];
                    var orderedPoints = new JArray();
                    foreach (var point in points)
                    {
                        var orderedPoint = new JObject
                        {
                            ["x"] = point["x"], // x 在前
                            ["y"] = point["y"], // y 在中
                            ["shape"] = point["shape"] // shape 在后
                        };
                        orderedPoints.Add(orderedPoint);
                    }
                    string pointsString = points.ToString(); // 转换为字符串

                    // 提取 name
                    string name = data["@ControlInput"]["name"].ToString();

                    // 提取 @PropertyName
                    string propertyName = data["@PropertyName"].ToString();

                    // 提取 @ControlInput 的 id
                    string controlInputId = data["@ControlInput"]["id"].ToString();


                    foreach (var guid in stringArray)
                    {
                        var set_args = new JObject
                        {
                            ["objects"] = new JArray
                            {
                                new JObject
                                {
                                    ["object"] = guid,
                                    ["@RTPC"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["type"] = "RTPC",
                                            ["name"] = name,
                                            ["@Curve"] = new JObject
                                            {
                                                ["type"] = "Curve",
                                                ["points"] = orderedPoints
                                            },
                                            ["notes"] = "",
                                            ["@PropertyName"] = propertyName,
                                            ["@ControlInput"] = controlInputId
                                        }
                                    }
                                }
                            }
                        };
                        try
                        {
                            JObject copy = await client.Call("ak.wwise.core.object.set", set_args, null);

                        }
                        catch (Exception ex)
                        {
                            ConnectionStatusTextBlock.Text = "Error: copy" + ex.Message;
                        }
                    }
                    ConnectionStatusTextBlock.Text = "复制成功！";
                }
                catch (Exception ex)
                {
                    ConnectionStatusTextBlock.Text = "Error: get rtpc" + ex.Message;
                }

            }
        }

        

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.IsConnected())
            {
                ConnectionStatusTextBlock.Text = "请先连接到Wwise！";
                return;
            }

            string waqlQuery = "$where type = \"Event\"";

            var args = new JObject
            {
                ["waql"] = waqlQuery
            };

            try
            {
                JObject result = await client.Call("ak.wwise.core.object.get", args,
                    new { @return = new[] { "name", "id" } });

                // 从返回结果中提取所有event的name，并存入字符串数组
                string[] eventNames = result["return"]?
                    .OfType<JObject>()
                    .Select(item => (string)item["name"])
                    .ToArray() ?? Array.Empty<string>();

                // 从返回结果中提取所有event的id，并存入字符串数组
                string[] eventIds = result["return"]?
                    .OfType<JObject>()
                    .Select(item => (string)item["id"])
                    .ToArray() ?? Array.Empty<string>();

                // 显示查询结果, 多个名字用逗号分隔
                ConnectionStatusTextBlock.Text = "event: " + string.Join(", ", eventNames);

                banks = eventNames;
                Ids = eventIds;
            }
            catch (Exception ex)
            {
                ConnectionStatusTextBlock.Text = "Error: " + ex.Message;
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.IsConnected())
            {
                ConnectionStatusTextBlock.Text = "请先连接到Wwise！";
                return;
            }

            try
            {
                var eventBankPairs = new List<string[]>();

                // banks 和 Ids 里存储的是 Event 的名称和 id，通过事件获取对应的 SoundBank 信息
                foreach (var (eventName, eventId) in banks.Zip(Ids, (name, id) => (name, id)))
                {
                    // 调用 WAAPI 接口获取 Event 对应的 SoundBank 列表
                    var args = new JObject
                    {
                        ["waql"] = $"\"{eventId}\" select this, ancestors select referencesTo where type = \"soundbank\""
                    };
                    JObject bankResult = await client.Call("ak.wwise.core.object.get", args,
                        new { @return = new[] { "name", "id" } });

                    // 提取 SoundBank 名称
                    var bankNames = bankResult["return"]?
                        .OfType<JObject>()
                        .Select(item => (string)item["name"])
                        .ToArray() ?? Array.Empty<string>();
                    // 将 Event 名称及对应的 Bank 名称存入数组
                    eventBankPairs.Add(new[] { eventName }.Concat(bankNames).ToArray());
                }

                // 转换为二维数组后显示结果
                bankEventArray = eventBankPairs.ToArray();

                StringBuilder resultText = new StringBuilder("查询结果：\n");
                foreach (var pair in bankEventArray)
                {
                    resultText.AppendLine($"Event: {pair[0]}");
                    resultText.AppendLine($"Banks: {string.Join(", ", pair.Skip(1))}");
                    resultText.AppendLine();
                }
                ConnectionStatusTextBlock.Text = resultText.ToString();
            }
            catch (Exception ex)
            {
                ConnectionStatusTextBlock.Text = "Error: " + ex.Message;
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (bankEventArray == null || bankEventArray.Length == 0)
            {
                MessageBox.Show("没有可用的数据。请先获取 Event 对应的 Bank 数据。");
                return;
            }

            try
            {
                // 创建一个字典来存储 Event 到 Bank 的映射
                var eventToBankMap = new Dictionary<string, string>();

                foreach (var eventBanks in bankEventArray)
                {
                    if (eventBanks.Length < 2) continue; // 没有关联 Bank 的 Event

                    string eventName = eventBanks[0];
                    // 多个 Bank 用逗号分隔
                    eventToBankMap[eventName] = string.Join(", ", eventBanks.Skip(1));
                }

                // 构建 CSV 内容，CSV 头部为 Event,Banks
                var csv = new StringBuilder();
                csv.AppendLine("Event,Banks");
                foreach (var kvp in eventToBankMap)
                {
                    csv.AppendLine($"\"{kvp.Key}\",\"{kvp.Value}\"");
                }

                // 打开文件保存对话框并写入 CSV 文件
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = "EventBankMap.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string fileName = saveFileDialog.FileName;
                    File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show($"数据已成功导出到 {fileName}");
                }
                else
                {
                    MessageBox.Show("导出已取消。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出数据时发生错误: {ex.Message}");
            }
        }
    }
}