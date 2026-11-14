using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace XvdTool.Streaming.Commands;

public readonly struct XvdFilePattern
{
    private readonly Regex m_pattern;
    private readonly bool m_fullPath;

    public XvdFilePattern(string patternStr)
    {
        if (string.IsNullOrWhiteSpace(patternStr)) 
        {
            throw new ArgumentException("Pattern cannot be null or empty", nameof(patternStr));
        }

        var normalized = patternStr.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal)) {
            normalized = normalized[2..];
        }
        normalized = normalized.TrimStart('/');
        
        var regexPattern = "^" + Regex.Escape(normalized)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        m_pattern = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        m_fullPath = normalized.Contains('/');
    }

    public bool Matches(string path, string fileName)
    {
        var target = m_fullPath ? path : fileName;
        return m_pattern.IsMatch(target);
    }

    public static string NormalizePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal)) {
            normalized = normalized[2..];
        }
        return normalized.TrimStart('/');
    }
    
    public sealed class Converter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => 
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
  
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                return new XvdFilePattern(str);
            }
  
            return base.ConvertFrom(context, culture, value);
        }
    }
}