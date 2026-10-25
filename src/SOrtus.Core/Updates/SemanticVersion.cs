namespace SOrtus.Updates;

/// <summary>
/// 语义化版本（SemVer 2.0 子集）。
/// 存在意义：`0.1.10` 与 `0.1.9` 用字符串比较会判成"更旧"，是这个功能最容易上线即错的地方，
/// 因此所有版本比较必须走这里，禁止在别处用 string.Compare 比较版本。
/// 支持前导 v、预发布后缀（-beta.1）与构建元数据（+sha，忽略）。
/// </summary>
public readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    public SemanticVersion(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>预发布标识（无则为 null）。非 null 表示低于同号正式版。</summary>
    public string? PreRelease { get; }

    public bool IsPreRelease => PreRelease is not null;

    /// <summary>解析版本字符串。接受 "1.2.3"、"v1.2.3"、"1.2.3-beta.1"、"1.2.3+abc"。</summary>
    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();

        // 前导 v / V（tag 常见写法）
        if (span.Length > 0 && (span[0] == 'v' || span[0] == 'V'))
        {
            span = span[1..];
        }

        // 丢弃构建元数据（SemVer 规定其不参与比较）
        var plusIndex = span.IndexOf('+');
        if (plusIndex >= 0)
        {
            span = span[..plusIndex];
        }

        string? preRelease = null;
        var dashIndex = span.IndexOf('-');
        if (dashIndex >= 0)
        {
            preRelease = span[(dashIndex + 1)..];
            span = span[..dashIndex];
            if (preRelease.Length == 0)
            {
                return false;
            }
        }

        var parts = span.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        if (!TryParseNumber(parts[0], out var major))
        {
            return false;
        }

        var minor = 0;
        var patch = 0;
        if (parts.Length > 1 && !TryParseNumber(parts[1], out minor))
        {
            return false;
        }

        if (parts.Length > 2 && !TryParseNumber(parts[2], out patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    /// <summary>仅接受非负十进制整数（不接受空格、正负号、空串）。</summary>
    private static bool TryParseNumber(string text, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 9)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (ch is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (ch - '0');
        }

        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        var byMinor = Minor.CompareTo(other.Minor);
        if (byMinor != 0)
        {
            return byMinor;
        }

        var byPatch = Patch.CompareTo(other.Patch);
        if (byPatch != 0)
        {
            return byPatch;
        }

        // 同版本号：有预发布标识的更低（1.0.0-beta < 1.0.0）
        if (PreRelease is null && other.PreRelease is null)
        {
            return 0;
        }

        if (PreRelease is null)
        {
            return 1;
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>按 SemVer 规则逐段比较预发布标识：数字段按数值、字母段按序数、段少者更低。</summary>
    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');

        var shared = Math.Min(leftParts.Length, rightParts.Length);
        for (var i = 0; i < shared; i++)
        {
            var l = leftParts[i];
            var r = rightParts[i];

            var lIsNumber = TryParseNumber(l, out var lNumber);
            var rIsNumber = TryParseNumber(r, out var rNumber);

            if (lIsNumber && rIsNumber)
            {
                var byNumber = lNumber.CompareTo(rNumber);
                if (byNumber != 0)
                {
                    return byNumber;
                }

                continue;
            }

            // 数字标识恒低于字母标识
            if (lIsNumber)
            {
                return -1;
            }

            if (rIsNumber)
            {
                return 1;
            }

            var byText = string.CompareOrdinal(l, r);
            if (byText != 0)
            {
                return byText < 0 ? -1 : 1;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    public bool Equals(SemanticVersion other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString()
        => PreRelease is null
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);

    public static bool operator !=(SemanticVersion left, SemanticVersion right) => !left.Equals(right);
}
