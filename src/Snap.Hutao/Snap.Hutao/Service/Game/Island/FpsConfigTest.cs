// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Setting;
using System.IO;

namespace Snap.Hutao.Service.Game.Island;

internal static class FpsConfigTest
{
    // 测试用，手动更新FPS配置文件
    public static void TestConfigUpdate()
    {
        // 直接从LocalSetting读取当前FPS设置
        int currentFps = LocalSetting.Get(SettingKeys.LaunchTargetFps, 60);
        
        // 配置文件路径
        string configPath = Path.Combine(AppContext.BaseDirectory, "fps_config.ini");
        
        // 读取当前配置
        if (File.Exists(configPath))
        {
            string[] lines = File.ReadAllLines(configPath);
            int configFps = 60;
            
            foreach (string line in lines)
            {
                if (line.StartsWith("FPS="))
                {
                    configFps = int.Parse(line.Substring(4));
                    break;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Current FPS from LocalSetting: {currentFps}");
            System.Diagnostics.Debug.WriteLine($"Current FPS from config file: {configFps}");
            
            if (currentFps != configFps)
            {
                // 更新配置文件
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("FPS="))
                    {
                        lines[i] = $"FPS={currentFps}";
                        break;
                    }
                }
                
                File.WriteAllLines(configPath, lines);
                System.Diagnostics.Debug.WriteLine($"Updated config file with FPS: {currentFps}");
            }
        }
    }
}