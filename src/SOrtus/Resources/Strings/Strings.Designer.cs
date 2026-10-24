using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

#nullable enable

namespace SOrtus.Resources.Strings;

/// <summary>
/// 强类型资源访问器（原由 PublicResXFileCodeGenerator 生成；SDK 构建不执行 VS 生成器，故随源码提交）。
/// 中性 resx（中文）为权威源，en-US 为卫星程序集（规范 4.4 / 7）。
/// </summary>
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
public class Strings
{
    private static ResourceManager? _resourceMan;
    private static CultureInfo? _resourceCulture;

    internal Strings()
    {
    }

    public static ResourceManager ResourceManager =>
        _resourceMan ??= new ResourceManager("SOrtus.Resources.Strings.Strings", typeof(Strings).Assembly);

    public static CultureInfo Culture
    {
        get => _resourceCulture ?? CultureInfo.InvariantCulture;
        set => _resourceCulture = value;
    }
}
