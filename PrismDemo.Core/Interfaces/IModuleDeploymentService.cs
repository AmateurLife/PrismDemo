using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    public interface IModuleDeploymentService
    {
        /// <summary>
        /// 获取源目录中所有未部署的模块信息
        /// </summary>
        Task<List<AvailableModuleInfo>> GetAvailableModulesAsync(string sourcePath);

        /// <summary>
        /// 将指定模块从源目录部署到工作目录
        /// </summary>
        Task DeployModuleAsync(string sourceDllPath, string targetModuleName);

        /// <summary>
        /// 获取当前已部署的最新模块列表（每个模块只返回最新版本）
        /// </summary>
        List<DiscoveredModuleInfo> GetLatestDeployedModules();
    }
    public class AvailableModuleInfo
    {
        public string SourcePath { get; set; }
        public string ModuleName { get; set; }
        public string Version { get; set; } // 可选，用于显示
    }
    public class DiscoveredModuleInfo
    {
        public string ModuleName { get; set; }
        public string FilePath { get; set; }
        public string Version { get; set; } // 这里用时间戳或版本号
    }
}
