using GuildLibraryConverter.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace GuildLibraryConverter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal static AppConfig Config { get; private set; } = AppConfig.DesignerInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                using var configFile = File.OpenRead("config.json");
                Config = JsonSerializer.DeserializeAsync<AppConfig>(configFile).AsTask().Result;
            }
            catch
            {
                {
                    using var configFile = File.OpenWrite("config.json");
                    configFile.SetLength(0);
                    JsonSerializer.SerializeAsync(configFile, AppConfig.DesignerInstance, new JsonSerializerOptions()
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    }).Wait();
                }
                MessageBox.Show("已创建config.json，请修改后重新启动。", "万用表格式转换");
                Environment.Exit(0);
                return;
            }
            base.OnStartup(e);
        }
    }
}
