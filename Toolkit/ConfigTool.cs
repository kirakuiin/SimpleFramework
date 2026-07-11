using System.Text;
using SimpleFramework.Utility;

namespace SimpleFramework.Toolkit;

/// <summary>
/// 用来读写ini配置文件的工具类
/// </summary>
/// <param name="isAutoFlush">写入配置时是否立即写到文件</param>
public sealed class IniConfigTool(bool isAutoFlush = false) : Disposable, IUtility
{
    private string _configPath = "";  // 配置文件路径

    /// <summary>
    /// 存储读取的数据
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _config = new();

    /// <summary>
    /// 从指定 UTF-8 INI 文件加载配置；文件不存在时清空内存配置并保留该路径供后续保存。
    /// </summary>
    /// <param name="filePath">配置文件路径。</param>
    public void LoadConfig(string filePath)
    {
        if (IsDisposed)
        {
            ToolkitLog.Warning("IniConfigTool已释放，无法加载配置");
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            ToolkitLog.Warning("文件路径不能为空");
            return;
        }

        try
        {
            _configPath = filePath;
            
            if (!File.Exists(filePath))
            {
                ToolkitLog.Info($"配置文件 {filePath} 不存在");
                _config.Clear();
                return;
            }
            
            _config.Clear();
            
            string currentSection = "";

            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string trimmedLine = line.Trim();

                // 跳过空行和注释行
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // 处理section行
                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    currentSection = trimmedLine[1..^1].Trim();
                    if (!_config.ContainsKey(currentSection))
                    {
                        _config[currentSection] = new Dictionary<string, string>();
                    }
                    continue;
                }

                // 处理key=value行
                int equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = trimmedLine[..equalIndex].Trim();
                    string value = trimmedLine[(equalIndex + 1)..].Trim();

                    if (string.IsNullOrEmpty(currentSection))
                        currentSection = "DEFAULT";

                    if (!_config.ContainsKey(currentSection))
                        _config[currentSection] = new Dictionary<string, string>();

                    _config[currentSection][key] = value;
                }
            }

            ToolkitLog.Info($"成功加载配置文件: {filePath}");
        }
        catch (Exception ex)
        {
            ToolkitLog.Error($"加载配置文件失败: {filePath}", ex);
        }
    }
    
    /// <summary>
    /// 保存配置，默认保存到已经打开的配置中
    /// <remarks>如果没有打开配置或没有指明配置路径则无事发生</remarks>
    /// </summary>
    /// <param name="filePath">保存路径，如果为空则使用当前路径</param>
    public void SaveConfig(string filePath="")
    {
        if (IsDisposed)
        {
            ToolkitLog.Warning("配置工具已经关闭, 无法保存");
            return;
        }

        var savePath = string.IsNullOrEmpty(filePath) ? _configPath : filePath;

        if (string.IsNullOrEmpty(savePath))
        {
            ToolkitLog.Warning("保存配置失败：未指定文件路径");
            return;
        }

        try
        {
            // 确保目录存在
            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(savePath, false, Encoding.UTF8);

            // 写入各个section
            foreach (var section in _config)
            {
                // 如果不是默认section，写入section标题
                if (!string.IsNullOrEmpty(section.Key))
                {
                    writer.WriteLine($"[{section.Key}]");
                }

                // 写入section中的key-value对
                foreach (var kvp in section.Value)
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }

                // 在不同section之间添加空行
                if (!string.IsNullOrEmpty(section.Key))
                {
                    writer.WriteLine();
                }
            }

            ToolkitLog.Info($"配置已保存到: {savePath}");
        }
        catch (Exception ex)
        {
            ToolkitLog.Error($"保存配置文件失败: {savePath}", ex);
        }
    }

    /// <summary>
    /// 读取配置值，如果不存在则返回默认值
    /// </summary>
    /// <remarks>
    /// 字符串保持原文；<see cref="DateTime"/> 与 <see cref="DateTimeOffset"/> 使用往返格式；
    /// 其余由 <see cref="Convert.ChangeType(object,Type,IFormatProvider)"/> 支持的基础类型使用不变区域性转换。
    /// 枚举、GUID 与自定义对象等不受该转换支持的目标类型返回默认值并记录错误。
    /// </remarks>
    /// <param name="section">配置节</param>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <typeparam name="T">值类型</typeparam>
    /// <returns>配置值或默认值</returns>
    public T Get<T>(string section, string key, T defaultValue = default!)
    {
        if (IsDisposed)
        {
            ToolkitLog.Warning("IniConfigTool已释放，无法读取配置值");
            return defaultValue;
        }

        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(key))
            return defaultValue;

        var rawValue = "";
        try
        {
            if (!_config.TryGetValue(section, out var sectionDict) ||
                !sectionDict.TryGetValue(key, out var readValue))
            {
                ToolkitLog.Warning($"配置项 [{section}].{key} 不存在，返回默认值: {defaultValue}");
                return defaultValue;
            }

            rawValue = readValue;

            // 如果类型已经是string，直接返回
            if (typeof(T) == typeof(string))
                return (T)(object)rawValue;

            if (typeof(T) == typeof(DateTime))
            {
                return (T)(object)DateTime.Parse(
                    rawValue,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind);
            }

            if (typeof(T) == typeof(DateTimeOffset))
            {
                return (T)(object)DateTimeOffset.ParseExact(
                    rawValue,
                    "O",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None);
            }

            return (T)Convert.ChangeType(
                rawValue,
                typeof(T),
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            ToolkitLog.Error($"配置项 [{section}].{key} 类型转换失败 (期望类型: {typeof(T).Name}), 值: {rawValue} 返回默认值", ex);
            return defaultValue;
        }
    }

    /// <summary>
    /// 设置配置值
    /// </summary>
    /// <remarks>
    /// 字符串保持原文；<see cref="DateTime"/> 与 <see cref="DateTimeOffset"/> 使用往返格式；
    /// 其他 <see cref="IFormattable"/> 值使用不变区域性格式。
    /// </remarks>
    /// <param name="section">配置节</param>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    /// <typeparam name="T">值类型</typeparam>
    public void Set<T>(string section, string key, T value)
    {
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(key))
        {
            ToolkitLog.Warning("设置配置值失败：section和key不能为空");
            return;
        }

        if (IsDisposed)
        {
            ToolkitLog.Warning("IniConfigTool已释放，无法设置配置值");
            return;
        }

        try
        {
            // 确保section存在
            if (!_config.ContainsKey(section))
                _config[section] = new Dictionary<string, string>();

            var stringValue = value switch
            {
                null => "",
                string text => text,
                DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
            _config[section][key] = stringValue;

            ToolkitLog.Info($"设置配置项 [{section}].{key} = {stringValue}");

            // 如果启用自动保存，立即写入文件
            if (isAutoFlush && !string.IsNullOrEmpty(_configPath))
            {
                Flush();
            }
        }
        catch (Exception ex)
        {
            ToolkitLog.Error($"设置配置项 [{section}].{key} 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 立即将内存中的配置写入到文件
    /// </summary>
    public void Flush()
    {
        if (IsDisposed)
        {
            ToolkitLog.Warning("IniConfigTool已释放，无法执行Flush操作");
            return;
        }

        if (string.IsNullOrEmpty(_configPath))
        {
            ToolkitLog.Warning("Flush失败：未设置配置文件路径");
            return;
        }

        SaveConfig(_configPath);
    }

    /// <summary>
    /// 重新读取已经打开的配置文件
    /// </summary>
    public void Reload()
    {
        if (IsDisposed)
        {
            ToolkitLog.Warning("IniConfigTool已释放，无法执行Reload操作");
            return;
        }

        if (string.IsNullOrEmpty(_configPath))
        {
            ToolkitLog.Warning("Reload失败：未设置配置文件路径");
            return;
        }

        LoadConfig(_configPath);
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~IniConfigTool()
    {
        Dispose(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        
        if (disposing)
        {
            // 释放托管资源
            // 如果有未保存的修改且未启用自动保存，自动保存
            if (!isAutoFlush && !string.IsNullOrEmpty(_configPath) && _config.Count > 0)
            {
                Flush();
            }
            _config.Clear();
        }

        IsDisposed = true;
        ToolkitLog.Info("IniConfigTool已释放资源");
    }
}
